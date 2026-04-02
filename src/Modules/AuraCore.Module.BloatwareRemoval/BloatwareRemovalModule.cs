using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using AuraCore.Module.BloatwareRemoval.Models;

namespace AuraCore.Module.BloatwareRemoval;

public sealed class BloatwareRemovalModule : IOptimizationModule
{
    public string Id => "bloatware-removal";
    public string DisplayName => "Bloatware Removal";
    public OptimizationCategory Category => OptimizationCategory.BloatwareRemoval;
    public RiskLevel Risk => RiskLevel.High;

    public BloatwareScanReport? LastReport { get; private set; }

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        var apps = await Task.Run(() => EnumerateAppxPackages(), ct);
        LastReport = new BloatwareScanReport { Apps = apps };
        return new ScanResult(Id, true, LastReport.TotalApps, LastReport.TotalRemovableBytes);
    }

    public async Task<OptimizationResult> OptimizeAsync(
        OptimizationPlan plan, IProgress<TaskProgress>? progress = null, CancellationToken ct = default)
    {
        if (plan.SelectedItemIds.Count == 0)
            return new OptimizationResult(Id, "", false, 0, 0, TimeSpan.Zero);

        var start = DateTime.UtcNow;
        int removed = 0;
        long freedBytes = 0;
        int total = plan.SelectedItemIds.Count;

        for (int i = 0; i < total; i++)
        {
            ct.ThrowIfCancellationRequested();
            var packageName = plan.SelectedItemIds[i];

            progress?.Report(new TaskProgress(Id,
                (double)(i + 1) / total * 100,
                $"Removing: {packageName.Split('_')[0]}"));

            var (success, size) = await RemovePackageAsync(packageName);
            if (success)
            {
                removed++;
                freedBytes += size;
            }
        }

        return new OptimizationResult(Id, Guid.NewGuid().ToString(), true, removed, freedBytes, DateTime.UtcNow - start);
    }

    public Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default)
        => Task.FromResult(false); // Could reinstall via winget in future

    public Task RollbackAsync(string operationId, CancellationToken ct = default)
        => Task.CompletedTask;

    private static List<AppxInfo> EnumerateAppxPackages()
    {
        var apps = new List<AppxInfo>();

        try
        {
            // Use PowerShell to get all AppX packages for current user
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -Command \"Get-AppxPackage | Select-Object Name, PackageFullName, Publisher, Version, InstallLocation, IsFramework, SignatureKind | ConvertTo-Json -Compress\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc is null) return apps;

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(30000);

            if (string.IsNullOrWhiteSpace(output)) return apps;

            // Parse JSON manually (avoid System.Text.Json dependency in module)
            // PowerShell outputs an array of objects
            apps = ParsePowerShellOutput(output);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[bloatware-removal] Error: {ex.Message}"); }

        return apps;
    }

    private static List<AppxInfo> ParsePowerShellOutput(string json)
    {
        var apps = new List<AppxInfo>();

        try
        {
            // Use System.Text.Json which is included in net8.0
            var items = System.Text.Json.JsonSerializer.Deserialize<List<PsAppxPackage>>(json);
            if (items is null) return apps;

            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.Name)) continue;

                var isFramework = item.IsFramework;
                var (category, risk, reason) = ClassifyApp(item.Name, item.Publisher ?? "", isFramework);

                // Estimate size from install location
                long size = 0;
                if (!string.IsNullOrEmpty(item.InstallLocation))
                {
                    try
                    {
                        if (Directory.Exists(item.InstallLocation))
                        {
                            size = new DirectoryInfo(item.InstallLocation)
                                .EnumerateFiles("*", new EnumerationOptions
                                {
                                    IgnoreInaccessible = true,
                                    RecurseSubdirectories = true
                                })
                                .Sum(f => { try { return f.Length; } catch { return 0L; } });
                        }
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[bloatware-removal] Error: {ex.Message}"); }
                }

                // Build a friendly display name from the package name
                var displayName = item.Name;
                if (displayName.StartsWith("Microsoft."))
                    displayName = displayName.Replace("Microsoft.", "");
                displayName = displayName.Replace(".", " ");

                var communityData = CommunityScores.GetScore(item.Name);

                apps.Add(new AppxInfo
                {
                    PackageFullName = item.PackageFullName ?? "",
                    Name = item.Name,
                    DisplayName = displayName,
                    Publisher = item.Publisher ?? "",
                    Version = item.Version ?? "",
                    InstallLocation = item.InstallLocation ?? "",
                    EstimatedSizeBytes = size,
                    Category = category,
                    Risk = risk,
                    RiskReason = reason,
                    IsFramework = isFramework,
                    CommunityScore = communityData.RemovalScore,
                    CommunityVotes = communityData.Votes
                });
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[bloatware-removal] Error: {ex.Message}"); }

        // Sort: bloat first, then caution, then user, then system
        apps.Sort((a, b) =>
        {
            var catOrder = GetCategoryOrder(a.Category).CompareTo(GetCategoryOrder(b.Category));
            return catOrder != 0 ? catOrder : string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
        });

        return apps;
    }

    private static int GetCategoryOrder(BloatCategory cat) => cat switch
    {
        BloatCategory.MicrosoftBloat => 0,
        BloatCategory.OemBloat => 1,
        BloatCategory.UserInstalled => 2,
        BloatCategory.SystemRequired => 3,
        BloatCategory.Framework => 4,
        _ => 5
    };

    private static (BloatCategory category, BloatRisk risk, string reason) ClassifyApp(
        string name, string publisher, bool isFramework)
    {
        if (isFramework)
            return (BloatCategory.Framework, BloatRisk.System, "Runtime framework — required by other apps");

        // Check system-required first
        if (BloatwareDatabase.SystemRequired.Any(s => name.Contains(s, StringComparison.OrdinalIgnoreCase)))
            return (BloatCategory.SystemRequired, BloatRisk.System, "Required for Windows to function properly");

        // Check caution apps
        if (BloatwareDatabase.CautionApps.Contains(name))
            return (BloatCategory.MicrosoftBloat, BloatRisk.Caution, "Optional Microsoft app — many users want this");

        // Check known Microsoft bloat
        if (BloatwareDatabase.MicrosoftBloat.Contains(name))
            return (BloatCategory.MicrosoftBloat, BloatRisk.Safe, "Pre-installed Microsoft app — safe to remove");

        // Check OEM bloat patterns
        if (BloatwareDatabase.OemBloatPatterns.Any(p =>
            name.Contains(p, StringComparison.OrdinalIgnoreCase) ||
            publisher.Contains(p, StringComparison.OrdinalIgnoreCase)))
            return (BloatCategory.OemBloat, BloatRisk.Safe, "OEM pre-installed software");

        // Microsoft publisher but not in our lists
        if (name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
            publisher.Contains("Microsoft", StringComparison.OrdinalIgnoreCase))
            return (BloatCategory.UserInstalled, BloatRisk.Caution, "Microsoft app — check if you use it");

        // Everything else is user-installed
        return (BloatCategory.UserInstalled, BloatRisk.Warning, "Third-party app — only remove if you don't use it");
    }

    private static async Task<(bool success, long estimatedSize)> RemovePackageAsync(string packageFullName)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"Remove-AppxPackage -Package '{packageFullName}'\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc is null) return (false, 0);

            await proc.WaitForExitAsync();
            return (proc.ExitCode == 0, 0);
        }
        catch
        {
            return (false, 0);
        }
    }

    private sealed class PsAppxPackage
    {
        public string? Name { get; set; }
        public string? PackageFullName { get; set; }
        public string? Publisher { get; set; }
        public string? Version { get; set; }
        public string? InstallLocation { get; set; }
        public bool IsFramework { get; set; }
    }
}

public static class BloatwareRemovalRegistration
{
    public static IServiceCollection AddBloatwareRemovalModule(
        this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, BloatwareRemovalModule>();
}
