using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Application.Shared;
using AuraCore.Domain.Enums;
using AuraCore.Module.LinuxAppInstaller.Models;

namespace AuraCore.Module.LinuxAppInstaller;

public sealed class LinuxAppInstallerModule : IOptimizationModule
{
    public string Id => "linux-app-installer";
    public string DisplayName => "Linux App Installer";
    public OptimizationCategory Category => OptimizationCategory.ApplicationManagement;
    public RiskLevel Risk => RiskLevel.Low;
    public SupportedPlatform Platform => SupportedPlatform.Linux;

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsLinux())
            return new ScanResult(Id, false, 0, 0, $"{DisplayName} is Linux-only.");

        try
        {
            var aptAvailable = await ProcessRunner.CommandExistsAsync("apt", ct);
            var snapAvailable = await ProcessRunner.CommandExistsAsync("snap", ct);
            var flatpakAvailable = await ProcessRunner.CommandExistsAsync("flatpak", ct);

            int totalApps = 0;
            int installedApps = 0;

            // Mark installed status for each app (best-effort — some sources may not be available)
            foreach (var bundle in LinuxAppBundles.AllBundles)
            {
                foreach (var app in bundle.Apps)
                {
                    ct.ThrowIfCancellationRequested();
                    totalApps++;
                    app.IsInstalled = await CheckInstalledAsync(app, aptAvailable, snapAvailable, flatpakAvailable, ct);
                    if (app.IsInstalled) installedApps++;
                }
            }

            // Items found: how many are NOT installed (available to install)
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

        if (!OperatingSystem.IsLinux())
            return new OptimizationResult(Id, operationId, false, 0, 0, TimeSpan.Zero);

        try
        {
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

                var app = LinuxAppBundles.FindById(appId);
                if (app == null) continue;

                bool success = action == "install"
                    ? await InstallAsync(app, ct)
                    : await UninstallAsync(app, ct);

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

    private static async Task<bool> CheckInstalledAsync(
        LinuxBundleApp app, bool aptAvailable, bool snapAvailable, bool flatpakAvailable,
        CancellationToken ct)
    {
        // Extract base package name (strip suffixes like "--classic")
        var pkgName = app.PackageName.Split(' ', 2)[0];

        switch (app.Source)
        {
            case LinuxPackageSource.Apt:
                if (!aptAvailable) return false;
                var aptResult = await ProcessRunner.RunAsync("/bin/sh", $"-c \"dpkg -l '{pkgName}' 2>/dev/null | grep -q '^ii'\"", ct);
                return aptResult.Success;

            case LinuxPackageSource.Snap:
                if (!snapAvailable) return false;
                var snapResult = await ProcessRunner.RunAsync("/bin/sh", $"-c \"snap list '{pkgName}' 2>/dev/null | grep -q '^{pkgName}'\"", ct);
                return snapResult.Success;

            case LinuxPackageSource.Flatpak:
                if (!flatpakAvailable) return false;
                var flatResult = await ProcessRunner.RunAsync("/bin/sh", $"-c \"flatpak list --app 2>/dev/null | grep -q '{pkgName}'\"", ct);
                return flatResult.Success;

            case LinuxPackageSource.Dnf:
                return false;  // not implemented

            default:
                return false;
        }
    }

    private static async Task<bool> InstallAsync(LinuxBundleApp app, CancellationToken ct)
    {
        // Validate package name to prevent shell injection
        if (!IsValidPackageName(app.PackageName)) return false;

        var cmd = app.Source switch
        {
            LinuxPackageSource.Apt => $"sudo -n apt-get install -y {app.PackageName}",
            LinuxPackageSource.Snap => $"sudo -n snap install {app.PackageName}",
            LinuxPackageSource.Flatpak => $"flatpak install -y flathub {app.PackageName}",
            _ => ""
        };
        if (string.IsNullOrEmpty(cmd)) return false;

        var result = await ProcessRunner.RunAsync("/bin/sh", $"-c \"{cmd}\"", ct, timeoutSeconds: 600);
        return result.Success;
    }

    private static async Task<bool> UninstallAsync(LinuxBundleApp app, CancellationToken ct)
    {
        if (!IsValidPackageName(app.PackageName)) return false;

        // For uninstall, strip flags like "--classic"
        var pkgName = app.PackageName.Split(' ', 2)[0];

        var cmd = app.Source switch
        {
            LinuxPackageSource.Apt => $"sudo -n apt-get remove -y {pkgName}",
            LinuxPackageSource.Snap => $"sudo -n snap remove {pkgName}",
            LinuxPackageSource.Flatpak => $"flatpak uninstall -y {pkgName}",
            _ => ""
        };
        if (string.IsNullOrEmpty(cmd)) return false;

        var result = await ProcessRunner.RunAsync("/bin/sh", $"-c \"{cmd}\"", ct, timeoutSeconds: 300);
        return result.Success;
    }

    private static bool IsValidPackageName(string name)
    {
        // Allow only: letters, digits, dashes, underscores, dots, plus signs, hyphens, spaces (for flags)
        foreach (var c in name)
        {
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_' && c != '.' && c != '+' && c != ' ')
                return false;
        }
        return true;
    }
}

public static class LinuxAppInstallerRegistration
{
    public static IServiceCollection AddLinuxAppInstallerModule(this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, LinuxAppInstallerModule>();
}
