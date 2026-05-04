using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Application.Shared;
using AuraCore.Domain.Enums;
using AuraCore.Module.KernelCleaner.Models;

namespace AuraCore.Module.KernelCleaner;

[SupportedOSPlatform("linux")]
public sealed class KernelCleanerModule : IOptimizationModule
{
    public string Id => "kernel-cleaner";
    public string DisplayName => "Kernel Cleaner";
    public OptimizationCategory Category => OptimizationCategory.DiskCleanup;
    public RiskLevel Risk => RiskLevel.Medium;
    public SupportedPlatform Platform => SupportedPlatform.Linux;

    public KernelReport? LastReport { get; private set; }
    private const int DefaultKeepCount = 2;  // Keep current + 1 previous

    public async Task<ModuleAvailability> CheckRuntimeAvailabilityAsync(CancellationToken ct = default)
    {
        if (!OperatingSystem.IsLinux())
            return ModuleAvailability.WrongPlatform(SupportedPlatform.Linux);

        if (!await ProcessRunner.CommandExistsAsync("apt-get", ct))
            return ModuleAvailability.ToolNotInstalled("apt-get",
                "Currently supports apt-based distributions only (Debian, Ubuntu).");

        return ModuleAvailability.Available;
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
                LastReport = KernelReport.None();
                return new ScanResult(Id, false, 0, 0, "No supported package manager found (apt, dnf)");
            }

            var currentKernel = await GetCurrentKernelAsync(ct);
            var kernels = pm switch
            {
                "apt" => await ListKernelsAptAsync(currentKernel, ct),
                "dnf" => await ListKernelsDnfAsync(currentKernel, ct),
                _ => new List<KernelInfo>()
            };

            // Sort by version, mark latest
            var sorted = kernels.OrderBy(k => k.Version, StringComparer.Ordinal).ToList();
            if (sorted.Count > 0)
            {
                var last = sorted[^1];
                sorted[^1] = last with { IsLatest = true };
            }

            // Compute removable bytes (all except current + latest N kept)
            var removable = sorted.Where(k => !k.IsCurrent && !k.IsLatest).ToList();
            long removableBytes = removable.Sum(k => k.SizeBytes);

            var report = new KernelReport(sorted, currentKernel, removableBytes, pm, true);
            LastReport = report;

            return new ScanResult(Id, true, removable.Count, removableBytes);
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

            var currentKernel = await GetCurrentKernelAsync(ct);
            var items = plan.SelectedItemIds?.ToList() ?? new List<string>();
            int totalSteps = Math.Max(items.Count, 1);

            for (int i = 0; i < items.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var itemId = items[i];
                progress?.Report(new TaskProgress(Id, (double)i / totalSteps * 100, $"Processing {itemId}..."));

                bool ok = false;
                long itemBytes = 0;

                if (itemId == "remove-old")
                {
                    // Run auto-remove
                    (ok, itemBytes) = await AutoRemoveOldKernelsAsync(pm, ct);
                }
                else if (itemId.StartsWith("remove:"))
                {
                    var version = itemId["remove:".Length..];
                    // Safety: never remove current kernel
                    if (string.IsNullOrEmpty(currentKernel) ||
                        version == currentKernel ||
                        version.Contains(currentKernel))
                    {
                        Debug.WriteLine($"[{Id}] Refused to remove current kernel {currentKernel}");
                        continue;
                    }
                    (ok, itemBytes) = await RemoveSpecificKernelAsync(pm, version, ct);
                }
                else if (itemId == "remove-all-but-current")
                {
                    // Aggressive removal — keep ONLY current
                    (ok, itemBytes) = await RemoveAllButCurrentAsync(pm, currentKernel, ct);
                }

                if (ok)
                {
                    processed++;
                    freed += itemBytes;
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
        if (await ProcessRunner.CommandExistsAsync("apt", ct)) return "apt";
        if (await ProcessRunner.CommandExistsAsync("dnf", ct)) return "dnf";
        return "";
    }

    private static async Task<string> GetCurrentKernelAsync(CancellationToken ct)
    {
        var result = await ProcessRunner.RunAsync("uname", "-r", ct);
        return result.Success ? result.Stdout.Trim() : "";
    }

    private static async Task<List<KernelInfo>> ListKernelsAptAsync(string currentKernel, CancellationToken ct)
    {
        var kernels = new List<KernelInfo>();
        // dpkg -l lists packages; filter for linux-image-<version>
        var result = await ProcessRunner.RunAsync("/bin/sh",
            "-c \"dpkg -l 2>/dev/null | grep '^ii  linux-image-[0-9]' | awk '{print $2}'\"", ct);
        if (!result.Success) return kernels;

        var packages = result.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var pkg in packages)
        {
            // e.g., linux-image-6.8.0-52-generic -> 6.8.0-52-generic
            var version = pkg.StartsWith("linux-image-") ? pkg["linux-image-".Length..] : pkg;
            var sizeResult = await ProcessRunner.RunAsync("dpkg-query",
                $"-W -f='${{Installed-Size}}' {pkg}", ct);
            long sizeBytes = 0;
            if (sizeResult.Success && long.TryParse(sizeResult.Stdout.Trim('\'', ' ', '\n', '\r'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var sizeKb))
                sizeBytes = sizeKb * 1024;

            bool isCurrent = !string.IsNullOrEmpty(currentKernel) && version == currentKernel;
            kernels.Add(new KernelInfo(version, pkg, isCurrent, IsLatest: false, sizeBytes, null));
        }
        return kernels;
    }

    private static async Task<List<KernelInfo>> ListKernelsDnfAsync(string currentKernel, CancellationToken ct)
    {
        var kernels = new List<KernelInfo>();
        // rpm -q kernel gives: kernel-6.6.12-200.fc39.x86_64
        var result = await ProcessRunner.RunAsync("/bin/sh",
            "-c \"rpm -q kernel 2>/dev/null | sort -V\"", ct);
        if (!result.Success) return kernels;

        var packages = result.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var pkg in packages)
        {
            // e.g., kernel-6.6.12-200.fc39.x86_64 -> 6.6.12-200.fc39.x86_64
            var version = pkg.StartsWith("kernel-") ? pkg["kernel-".Length..] : pkg;
            var sizeResult = await ProcessRunner.RunAsync("/bin/sh",
                $"-c \"rpm -q --queryformat '%{{SIZE}}' {pkg} 2>/dev/null\"", ct);
            long sizeBytes = 0;
            if (sizeResult.Success && long.TryParse(sizeResult.Stdout.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var bytes))
                sizeBytes = bytes;

            bool isCurrent = !string.IsNullOrEmpty(currentKernel) && version == currentKernel;
            kernels.Add(new KernelInfo(version, pkg, isCurrent, IsLatest: false, sizeBytes, null));
        }
        return kernels;
    }

    private static async Task<(bool ok, long bytesFreed)> AutoRemoveOldKernelsAsync(string pm, CancellationToken ct)
    {
        // Measure /boot size before
        var beforeResult = await ProcessRunner.RunAsync("/bin/sh", "-c \"du -sb /boot 2>/dev/null | awk '{print $1}'\"", ct);
        long before = long.TryParse(beforeResult.Stdout.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var b) ? b : 0;

        var result = pm switch
        {
            "apt" => await ProcessRunner.RunAsync("apt-get", "autoremove --purge -y", ct, timeoutSeconds: 300),
            "dnf" => await ProcessRunner.RunAsync("dnf", "remove --oldinstallonly -y", ct, timeoutSeconds: 300),
            _ => new ProcessRunner.Result(false, -1, "", "", "Unknown PM")
        };

        var afterResult = await ProcessRunner.RunAsync("/bin/sh", "-c \"du -sb /boot 2>/dev/null | awk '{print $1}'\"", ct);
        long after = long.TryParse(afterResult.Stdout.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var a) ? a : 0;

        long delta = before - after;
        return (result.Success, delta > 0 ? delta : 0);
    }

    private static async Task<(bool ok, long bytesFreed)> RemoveSpecificKernelAsync(string pm, string version, CancellationToken ct)
    {
        var beforeResult = await ProcessRunner.RunAsync("/bin/sh", "-c \"du -sb /boot 2>/dev/null | awk '{print $1}'\"", ct);
        long before = long.TryParse(beforeResult.Stdout.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var b) ? b : 0;

        var result = pm switch
        {
            "apt" => await ProcessRunner.RunAsync("apt-get", $"remove --purge -y linux-image-{version}", ct, timeoutSeconds: 300),
            "dnf" => await ProcessRunner.RunAsync("dnf", $"remove -y kernel-{version}", ct, timeoutSeconds: 300),
            _ => new ProcessRunner.Result(false, -1, "", "", "Unknown PM")
        };

        var afterResult = await ProcessRunner.RunAsync("/bin/sh", "-c \"du -sb /boot 2>/dev/null | awk '{print $1}'\"", ct);
        long after = long.TryParse(afterResult.Stdout.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var a) ? a : 0;

        long delta = before - after;
        return (result.Success, delta > 0 ? delta : 0);
    }

    private static async Task<(bool ok, long bytesFreed)> RemoveAllButCurrentAsync(string pm, string currentKernel, CancellationToken ct)
    {
        // Aggressive removal — remove every installed kernel except the currently-running one.
        // Refuses if currentKernel is empty (safety).
        if (string.IsNullOrEmpty(currentKernel))
            return (false, 0);

        var beforeResult = await ProcessRunner.RunAsync("/bin/sh", "-c \"du -sb /boot 2>/dev/null | awk '{print $1}'\"", ct);
        long before = long.TryParse(beforeResult.Stdout.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var b) ? b : 0;

        // Discover non-current kernels and remove them one-by-one to preserve safety.
        var kernels = pm switch
        {
            "apt" => await ListKernelsAptAsync(currentKernel, ct),
            "dnf" => await ListKernelsDnfAsync(currentKernel, ct),
            _ => new List<KernelInfo>()
        };

        bool anySuccess = false;
        foreach (var k in kernels)
        {
            ct.ThrowIfCancellationRequested();
            if (k.IsCurrent || k.Version == currentKernel || k.Version.Contains(currentKernel))
                continue;
            var (ok, _) = await RemoveSpecificKernelAsync(pm, k.Version, ct);
            if (ok) anySuccess = true;
        }

        var afterResult = await ProcessRunner.RunAsync("/bin/sh", "-c \"du -sb /boot 2>/dev/null | awk '{print $1}'\"", ct);
        long after = long.TryParse(afterResult.Stdout.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var a) ? a : 0;

        long delta = before - after;
        return (anySuccess || kernels.Count == 0, delta > 0 ? delta : 0);
    }
}

[SupportedOSPlatform("linux")]
public static class KernelCleanerRegistration
{
    public static IServiceCollection AddKernelCleanerModule(this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, KernelCleanerModule>();
}
