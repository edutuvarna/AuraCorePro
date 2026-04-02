using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using AuraCore.Module.AppInstaller.Models;

namespace AuraCore.Module.AppInstaller;

public sealed class AppInstallerModule : IOptimizationModule
{
    public string Id => "app-installer";
    public string DisplayName => "App Installer";
    public OptimizationCategory Category => OptimizationCategory.ApplicationManagement;
    public RiskLevel Risk => RiskLevel.Low;

    public AppInstallerReport? LastReport { get; private set; }

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        var report = await Task.Run(async () =>
        {
            var wingetOk = await IsWinGetAvailableAsync();
            var installed = wingetOk ? await GetInstalledAppsAsync(ct) : new List<InstalledApp>();
            var bundles = AppBundles.GetAll();

            // Mark which bundle apps are already installed
            var installedIds = new HashSet<string>(installed.Select(a => a.Id), StringComparer.OrdinalIgnoreCase);
            foreach (var bundle in bundles)
                foreach (var app in bundle.Apps)
                    app.IsInstalled = installedIds.Contains(app.WinGetId);

            return new AppInstallerReport
            {
                InstalledApps = installed,
                Bundles = bundles,
                WinGetAvailable = wingetOk
            };
        }, ct);

        LastReport = report;
        return new ScanResult(Id, true, report.InstalledApps.Count, 0);
    }

    public async Task<OptimizationResult> OptimizeAsync(
        OptimizationPlan plan, IProgress<TaskProgress>? progress = null, CancellationToken ct = default)
    {
        // plan.SelectedItemIds format: "install:AppId" or "uninstall:AppId"
        var start = DateTime.UtcNow;
        int succeeded = 0;
        int total = plan.SelectedItemIds.Count;

        for (int i = 0; i < total; i++)
        {
            ct.ThrowIfCancellationRequested();
            var item = plan.SelectedItemIds[i];
            var parts = item.Split(':', 2);
            if (parts.Length < 2) continue;

            var action = parts[0];
            var appId = parts[1];
            var appName = appId.Split('.').Last();

            progress?.Report(new TaskProgress(Id,
                (double)(i + 1) / total * 100,
                action == "install" ? $"Installing: {appName}..." : $"Removing: {appName}..."));

            bool ok = action switch
            {
                "install" => await RunWinGetAsync($"install --id {appId} --accept-package-agreements --accept-source-agreements --silent", ct),
                "uninstall" => await RunWinGetAsync($"uninstall --id {appId} --silent", ct),
                _ => false
            };

            if (ok) succeeded++;
        }

        return new OptimizationResult(Id, Guid.NewGuid().ToString(), true, succeeded, 0, DateTime.UtcNow - start);
    }

    public Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default)
        => Task.FromResult(false);

    public Task RollbackAsync(string operationId, CancellationToken ct = default)
        => Task.CompletedTask;

    /// <summary>
    /// Exports the current installed app list as a JSON string.
    /// </summary>
    public async Task<string> ExportInstalledAppsAsync(CancellationToken ct = default)
    {
        var apps = await GetInstalledAppsAsync(ct);
        var exportData = new AppListExport
        {
            ExportDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            MachineName = Environment.MachineName,
            AppCount = apps.Count,
            Apps = apps.Select(a => new AppListExportEntry
            {
                Id = a.Id,
                Name = a.Name,
                Version = a.Version,
                Publisher = a.Publisher
            }).OrderBy(a => a.Name).ToList()
        };
        return JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Parses a previously exported JSON file and returns the list of WinGet IDs for installation.
    /// </summary>
    public static List<AppListExportEntry> ParseImportFile(string json)
    {
        try
        {
            var data = JsonSerializer.Deserialize<AppListExport>(json);
            return data?.Apps ?? new List<AppListExportEntry>();
        }
        catch
        {
            return new List<AppListExportEntry>();
        }
    }

    public async Task<List<WinGetApp>> SearchAsync(string query, CancellationToken ct = default)
    {
        var results = new List<WinGetApp>();
        try
        {
            var output = await RunWinGetOutputAsync($"search \"{query}\" --accept-source-agreements", ct);
            if (string.IsNullOrEmpty(output)) return results;

            // Parse winget table output
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            bool headerPassed = false;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.StartsWith("---") || line.StartsWith("Name"))
                {
                    headerPassed = true;
                    if (line.StartsWith("---")) continue;
                    continue;
                }
                if (!headerPassed) continue;
                if (string.IsNullOrWhiteSpace(line)) continue;

                // WinGet output is space-aligned columns — split by 2+ spaces
                var parts = System.Text.RegularExpressions.Regex.Split(line, @"\s{2,}")
                    .Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

                if (parts.Length >= 2)
                {
                    results.Add(new WinGetApp
                    {
                        Name = parts[0].Trim(),
                        Id = parts.Length > 1 ? parts[1].Trim() : "",
                        Version = parts.Length > 2 ? parts[2].Trim() : "",
                        Publisher = parts.Length > 3 ? parts[3].Trim() : "",
                    });
                }
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[app-installer] Error: {ex.Message}"); }
        return results.Take(20).ToList();
    }

    private static async Task<List<InstalledApp>> GetInstalledAppsAsync(CancellationToken ct)
    {
        var apps = new List<InstalledApp>();
        try
        {
            var output = await RunWinGetOutputAsync("list --accept-source-agreements", ct);
            if (string.IsNullOrEmpty(output)) return apps;

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            bool headerPassed = false;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.StartsWith("---"))
                {
                    headerPassed = true;
                    continue;
                }
                if (!headerPassed || line.StartsWith("Name")) continue;
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = System.Text.RegularExpressions.Regex.Split(line, @"\s{2,}")
                    .Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

                if (parts.Length >= 2)
                {
                    apps.Add(new InstalledApp
                    {
                        Name = parts[0].Trim(),
                        Id = parts.Length > 1 ? parts[1].Trim() : "",
                        Version = parts.Length > 2 ? parts[2].Trim() : "",
                        Publisher = parts.Length > 3 ? parts[3].Trim() : "",
                    });
                }
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[app-installer] Error: {ex.Message}"); }
        return apps;
    }

    private static async Task<bool> IsWinGetAvailableAsync()
    {
        try
        {
            var output = await RunWinGetOutputAsync("--version", default);
            return !string.IsNullOrEmpty(output) && output.Contains("v");
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[app-installer] Error: {ex.Message}"); return false; }
    }

    private static async Task<bool> RunWinGetAsync(string args, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "winget",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc is null) return false;
            await proc.WaitForExitAsync(ct);
            return proc.ExitCode == 0;
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[app-installer] Error: {ex.Message}"); return false; }
    }

    private static async Task<string> RunWinGetOutputAsync(string args, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "winget",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            };
            using var proc = Process.Start(psi);
            if (proc is null) return "";
            var output = await proc.StandardOutput.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);
            return output;
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[app-installer] Error: {ex.Message}"); return ""; }
    }
    public async Task<List<OutdatedApp>> GetOutdatedAppsAsync(CancellationToken ct = default)
    {
        var results = new List<OutdatedApp>();
        try
        {
            var output = await RunWinGetOutputAsync("upgrade --accept-source-agreements", ct);
            if (string.IsNullOrEmpty(output)) return results;

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            bool headerPassed = false;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.StartsWith("---")) { headerPassed = true; continue; }
                if (!headerPassed || line.StartsWith("Name") || string.IsNullOrWhiteSpace(line)) continue;
                if (line.Contains("upgrade(s) available") || line.Contains("winget")) continue;

                var parts = System.Text.RegularExpressions.Regex.Split(line, @"\s{2,}")
                    .Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

                if (parts.Length >= 4)
                {
                    results.Add(new OutdatedApp
                    {
                        Name = parts[0].Trim(),
                        Id = parts[1].Trim(),
                        CurrentVersion = parts[2].Trim(),
                        AvailableVersion = parts[3].Trim()
                    });
                }
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[app-installer] Error: {ex.Message}"); }
        return results;
    }

    public async Task<(int updated, int failed)> UpdateAppsAsync(
        List<string> appIds, IProgress<TaskProgress>? progress = null, CancellationToken ct = default)
    {
        int updated = 0, failed = 0;

        if (appIds.Count == 0)
        {
            // Update all
            progress?.Report(new TaskProgress(Id, 50, "Updating all apps..."));
            var ok = await RunWinGetAsync("upgrade --all --accept-package-agreements --accept-source-agreements --silent", ct);
            return ok ? (1, 0) : (0, 1);
        }

        for (int i = 0; i < appIds.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var appId = appIds[i];
            var name = appId.Split('.').Last();
            progress?.Report(new TaskProgress(Id, (double)(i + 1) / appIds.Count * 100,
                $"Updating {name}..."));

            var ok = await RunWinGetAsync($"upgrade --id {appId} --accept-package-agreements --accept-source-agreements --silent", ct);
            if (ok) updated++; else failed++;
        }

        return (updated, failed);
    }
}

public static class AppInstallerRegistration
{
    public static IServiceCollection AddAppInstallerModule(
        this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, AppInstallerModule>();
}
