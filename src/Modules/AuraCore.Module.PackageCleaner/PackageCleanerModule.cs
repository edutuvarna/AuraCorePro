using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Application.Shared;
using AuraCore.Domain.Enums;
using AuraCore.Module.PackageCleaner.Models;

namespace AuraCore.Module.PackageCleaner;

public sealed class PackageCleanerModule : IOptimizationModule
{
    public string Id => "package-cleaner";
    public string DisplayName => "Package Cleaner";
    public OptimizationCategory Category => OptimizationCategory.DiskCleanup;
    public RiskLevel Risk => RiskLevel.Medium;
    public SupportedPlatform Platform => SupportedPlatform.Linux;

    public PackageCleanerReport? LastReport { get; private set; }

    // Priority order for package manager detection
    private static readonly string[] SupportedPMs = { "apt", "dnf", "pacman", "zypper" };

    public async Task<ModuleAvailability> CheckRuntimeAvailabilityAsync(CancellationToken ct = default)
    {
        if (!OperatingSystem.IsLinux())
            return ModuleAvailability.WrongPlatform(SupportedPlatform.Linux);

        foreach (var tool in SupportedPMs)
            if (await ProcessRunner.CommandExistsAsync(tool, ct))
                return ModuleAvailability.Available;

        return ModuleAvailability.ToolNotInstalled("apt/dnf/pacman/zypper",
            "Install a supported package manager (apt, dnf, pacman, or zypper).");
    }

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsLinux())
            return new ScanResult(Id, false, 0, 0, $"{DisplayName} is Linux-only.");

        try
        {
            var pm = await DetectPackageManagerAsync(ct);
            if (string.IsNullOrEmpty(pm))
            {
                LastReport = PackageCleanerReport.None();
                return new ScanResult(Id, false, 0, 0, "No supported package manager found (apt, dnf, pacman, zypper)");
            }

            var cacheBytes = await GetCacheBytesAsync(pm, ct);
            var installed = await GetInstalledCountAsync(pm, ct);
            var orphans = await GetOrphanCountAsync(pm, ct);

            var report = new PackageCleanerReport(pm, cacheBytes, installed, orphans, true);
            LastReport = report;
            return new ScanResult(Id, true, orphans, cacheBytes);
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
        long freed = 0;

        if (!OperatingSystem.IsLinux())
            return new OptimizationResult(Id, operationId, false, 0, 0, TimeSpan.Zero);

        try
        {
            var pm = await DetectPackageManagerAsync(ct);
            if (string.IsNullOrEmpty(pm))
                return new OptimizationResult(Id, operationId, false, 0, 0, DateTime.UtcNow - start);

            var items = plan.SelectedItemIds?.ToList() ?? new List<string>();
            int totalSteps = Math.Max(items.Count, 1);

            for (int i = 0; i < items.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var itemId = items[i];
                progress?.Report(new TaskProgress(Id, (double)i / totalSteps * 100, $"Running {itemId}..."));

                var beforeBytes = await GetCacheBytesAsync(pm, ct);
                bool ok = itemId switch
                {
                    "clean-cache" => await CleanCacheAsync(pm, ct),
                    "autoremove" => await AutoRemoveAsync(pm, ct),
                    _ => false
                };

                if (ok)
                {
                    processed++;
                    var afterBytes = await GetCacheBytesAsync(pm, ct);
                    var delta = beforeBytes - afterBytes;
                    if (delta > 0) freed += delta;
                }
            }

            progress?.Report(new TaskProgress(Id, 100, "Complete"));
            return new OptimizationResult(Id, operationId, true, processed, freed, DateTime.UtcNow - start);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{Id}] Optimize error: {ex.Message}");
            return new OptimizationResult(Id, operationId, false, processed, freed, DateTime.UtcNow - start);
        }
    }

    public Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default) => Task.FromResult(false);
    public Task RollbackAsync(string operationId, CancellationToken ct = default) => Task.CompletedTask;

    // ---- Helpers ----
    private static async Task<string> DetectPackageManagerAsync(CancellationToken ct)
    {
        foreach (var pm in SupportedPMs)
        {
            if (await ProcessRunner.CommandExistsAsync(pm, ct))
                return pm;
        }
        return "";
    }

    private static async Task<long> GetCacheBytesAsync(string pm, CancellationToken ct)
    {
        var path = pm switch
        {
            "apt" => "/var/cache/apt/archives/",
            "dnf" => "/var/cache/dnf/",
            "pacman" => "/var/cache/pacman/pkg/",
            "zypper" => "/var/cache/zypp/packages/",
            _ => ""
        };
        if (string.IsNullOrEmpty(path)) return 0;

        var result = await ProcessRunner.RunAsync("du", $"-sb {path}", ct);
        if (!result.Success || string.IsNullOrWhiteSpace(result.Stdout)) return 0;

        var firstToken = result.Stdout.Split('\t', ' ', '\n')[0];
        return long.TryParse(firstToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bytes) ? bytes : 0;
    }

    private static async Task<int> GetInstalledCountAsync(string pm, CancellationToken ct)
    {
        // Use sh -c to allow pipes
        var cmd = pm switch
        {
            "apt" => "apt list --installed 2>/dev/null | wc -l",
            "dnf" => "dnf list installed 2>/dev/null | wc -l",
            "pacman" => "pacman -Q | wc -l",
            "zypper" => "zypper se --installed-only 2>/dev/null | wc -l",
            _ => ""
        };
        if (string.IsNullOrEmpty(cmd)) return 0;

        var result = await ProcessRunner.RunAsync("/bin/sh", $"-c \"{cmd}\"", ct);
        return int.TryParse(result.Stdout.Trim(), out var count) ? count : 0;
    }

    private static async Task<int> GetOrphanCountAsync(string pm, CancellationToken ct)
    {
        var cmd = pm switch
        {
            "apt" => "apt-get -s autoremove 2>/dev/null | grep -c '^Remv'",
            "dnf" => "dnf autoremove --assumeno 2>/dev/null | grep -c '^Remove'",
            "pacman" => "pacman -Qdt 2>/dev/null | wc -l",
            "zypper" => "zypper packages --unneeded 2>/dev/null | wc -l",
            _ => ""
        };
        if (string.IsNullOrEmpty(cmd)) return 0;

        var result = await ProcessRunner.RunAsync("/bin/sh", $"-c \"{cmd}\"", ct);
        return int.TryParse(result.Stdout.Trim(), out var count) ? count : 0;
    }

    private static async Task<bool> CleanCacheAsync(string pm, CancellationToken ct)
    {
        var result = pm switch
        {
            "apt" => await ProcessRunner.RunAsync("apt-get", "clean", ct),
            "dnf" => await ProcessRunner.RunAsync("dnf", "clean all", ct),
            "pacman" => await ProcessRunner.RunAsync("pacman", "-Sc --noconfirm", ct),
            "zypper" => await ProcessRunner.RunAsync("zypper", "clean --all", ct),
            _ => new ProcessRunner.Result(false, -1, "", "", "Unknown PM")
        };
        return result.Success;
    }

    private static async Task<bool> AutoRemoveAsync(string pm, CancellationToken ct)
    {
        var result = pm switch
        {
            "apt" => await ProcessRunner.RunAsync("apt-get", "autoremove -y --purge", ct),
            "dnf" => await ProcessRunner.RunAsync("dnf", "autoremove -y", ct),
            "pacman" => await ProcessRunner.RunAsync("/bin/sh", "-c \"pacman -Rns $(pacman -Qdtq) --noconfirm\"", ct),
            "zypper" => await ProcessRunner.RunAsync("zypper", "remove --clean-deps -y", ct),
            _ => new ProcessRunner.Result(false, -1, "", "", "Unknown PM")
        };
        return result.Success;
    }
}

public static class PackageCleanerRegistration
{
    public static IServiceCollection AddPackageCleanerModule(this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, PackageCleanerModule>();
}
