using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Application.Shared;
using AuraCore.Domain.Enums;
using AuraCore.Module.MacAppInstaller.Models;

namespace AuraCore.Module.MacAppInstaller;

[SupportedOSPlatform("macos")]
public sealed class MacAppInstallerModule : IOptimizationModule
{
    public string Id => "mac-app-installer";
    public string DisplayName => "Mac App Installer";
    public OptimizationCategory Category => OptimizationCategory.ApplicationManagement;
    public RiskLevel Risk => RiskLevel.Low;
    public SupportedPlatform Platform => SupportedPlatform.MacOS;

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsMacOS())
            return new ScanResult(Id, false, 0, 0, $"{DisplayName} is macOS-only.");

        try
        {
            var brewPath = GetBrewPath();
            if (string.IsNullOrEmpty(brewPath))
                return new ScanResult(Id, false, 0, 0, "Homebrew is not installed. Install it from https://brew.sh");

            int totalApps = 0;
            int installedApps = 0;

            // Get list of installed formulae + casks in one shot for efficiency.
            var formulaeList = await ProcessRunner.RunAsync("/bin/sh", $"-c \"{brewPath} list --formula -1 2>/dev/null\"", ct);
            var casksList = await ProcessRunner.RunAsync("/bin/sh", $"-c \"{brewPath} list --cask -1 2>/dev/null\"", ct);

            var installedFormulae = formulaeList.Success
                ? new HashSet<string>(formulaeList.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()))
                : new HashSet<string>();
            var installedCasks = casksList.Success
                ? new HashSet<string>(casksList.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()))
                : new HashSet<string>();

            foreach (var bundle in MacAppBundles.AllBundles)
            {
                foreach (var app in bundle.Apps)
                {
                    ct.ThrowIfCancellationRequested();
                    totalApps++;

                    // Strip any flags/suffixes from package name (shouldn't exist for brew, but defensive)
                    var pkgName = app.PackageName.Split(' ', 2)[0];
                    app.IsInstalled = app.Source switch
                    {
                        MacPackageSource.BrewFormula => installedFormulae.Contains(pkgName),
                        MacPackageSource.BrewCask => installedCasks.Contains(pkgName),
                        _ => false
                    };
                    if (app.IsInstalled) installedApps++;
                }
            }

            var availableToInstall = totalApps - installedApps;
            return new ScanResult(Id, true, availableToInstall, 0);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{Id}] Scan error: {ex.Message}");
            return new ScanResult(Id, false, 0, 0, ex.Message);
        }
    }

    public async Task<OptimizationResult> OptimizeAsync(
        OptimizationPlan plan,
        IProgress<TaskProgress>? progress = null,
        CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;
        var operationId = Guid.NewGuid().ToString("N")[..8];
        int processed = 0;

        if (!OperatingSystem.IsMacOS())
            return new OptimizationResult(Id, operationId, false, 0, 0, TimeSpan.Zero);

        try
        {
            var brewPath = GetBrewPath();
            if (string.IsNullOrEmpty(brewPath))
                return new OptimizationResult(Id, operationId, false, 0, 0, DateTime.UtcNow - start);

            var items = plan.SelectedItemIds?.ToList() ?? new List<string>();
            int totalSteps = Math.Max(items.Count, 1);

            for (int i = 0; i < items.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var itemId = items[i];
                progress?.Report(new TaskProgress(Id, (double)i / totalSteps * 100, $"Processing {itemId}..."));

                // Item format: "install:<app-id>" or "uninstall:<app-id>"
                var colonIdx = itemId.IndexOf(':');
                if (colonIdx < 0) continue;

                var action = itemId[..colonIdx].ToLowerInvariant();
                var appId = itemId[(colonIdx + 1)..];

                if (action != "install" && action != "uninstall") continue;
                if (string.IsNullOrWhiteSpace(appId)) continue;

                var app = MacAppBundles.FindById(appId);
                if (app == null) continue;

                bool success = action == "install"
                    ? await InstallAsync(brewPath, app, ct)
                    : await UninstallAsync(brewPath, app, ct);

                if (success) processed++;
            }

            progress?.Report(new TaskProgress(Id, 100, "Complete"));
            return new OptimizationResult(Id, operationId, true, processed, 0, DateTime.UtcNow - start);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{Id}] Optimize error: {ex.Message}");
            return new OptimizationResult(Id, operationId, false, processed, 0, DateTime.UtcNow - start);
        }
    }

    public Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default) => Task.FromResult(false);
    public Task RollbackAsync(string operationId, CancellationToken ct = default) => Task.CompletedTask;

    // ---- Helpers ----

    private static string GetBrewPath()
    {
        // Apple Silicon: /opt/homebrew/bin/brew
        // Intel:        /usr/local/bin/brew
        if (File.Exists("/opt/homebrew/bin/brew")) return "/opt/homebrew/bin/brew";
        if (File.Exists("/usr/local/bin/brew")) return "/usr/local/bin/brew";
        return string.Empty;
    }

    private static async Task<bool> InstallAsync(string brewPath, MacBundleApp app, CancellationToken ct)
    {
        if (!IsValidPackageName(app.PackageName)) return false;

        var cmd = app.Source switch
        {
            MacPackageSource.BrewFormula => $"{brewPath} install {app.PackageName}",
            MacPackageSource.BrewCask => $"{brewPath} install --cask {app.PackageName}",
            _ => ""
        };
        if (string.IsNullOrEmpty(cmd)) return false;

        var result = await ProcessRunner.RunAsync("/bin/sh", $"-c \"{cmd}\"", ct, timeoutSeconds: 900);
        return result.Success;
    }

    private static async Task<bool> UninstallAsync(string brewPath, MacBundleApp app, CancellationToken ct)
    {
        if (!IsValidPackageName(app.PackageName)) return false;
        var pkgName = app.PackageName.Split(' ', 2)[0];

        var cmd = app.Source switch
        {
            MacPackageSource.BrewFormula => $"{brewPath} uninstall {pkgName}",
            MacPackageSource.BrewCask => $"{brewPath} uninstall --cask {pkgName}",
            _ => ""
        };
        if (string.IsNullOrEmpty(cmd)) return false;

        var result = await ProcessRunner.RunAsync("/bin/sh", $"-c \"{cmd}\"", ct, timeoutSeconds: 300);
        return result.Success;
    }

    private static bool IsValidPackageName(string name)
    {
        // Allow only: letters, digits, dashes, underscores, dots, plus signs, @, spaces
        foreach (var c in name)
        {
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_' && c != '.' && c != '+' && c != '@' && c != ' ')
                return false;
        }
        return true;
    }
}

[SupportedOSPlatform("macos")]
public static class MacAppInstallerRegistration
{
    public static IServiceCollection AddMacAppInstallerModule(this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, MacAppInstallerModule>();
}
