using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Application.Shared;
using AuraCore.Domain.Enums;
using AuraCore.Module.SwapOptimizer.Models;

namespace AuraCore.Module.SwapOptimizer;

[SupportedOSPlatform("linux")]
public sealed class SwapOptimizerModule : IOptimizationModule
{
    public string Id => "swap-optimizer";
    public string DisplayName => "Swap Optimizer";
    public OptimizationCategory Category => OptimizationCategory.MemoryOptimization;
    public RiskLevel Risk => RiskLevel.Medium;
    public SupportedPlatform Platform => SupportedPlatform.Linux;

    public SwapReport? LastReport { get; private set; }

    private const long GB = 1024L * 1024 * 1024;

    public async Task<ModuleAvailability> CheckRuntimeAvailabilityAsync(CancellationToken ct = default)
    {
        if (!OperatingSystem.IsLinux())
            return ModuleAvailability.WrongPlatform(SupportedPlatform.Linux);

        if (!await ProcessRunner.CommandExistsAsync("swapon", ct))
            return ModuleAvailability.ToolNotInstalled("swapon",
                "Install the util-linux package (provides swapon).");

        return ModuleAvailability.Available;
    }

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsLinux())
            return new ScanResult(Id, false, 0, 0, $"{DisplayName} is Linux-only.");

        try
        {
            var swappiness = ReadSwappiness();
            var ramBytes = ReadRamBytes();
            var devices = await ReadSwapDevicesAsync(ct);
            var zramAvailable = await ProcessRunner.CommandExistsAsync("zramctl", ct);
            var zramEnabled = zramAvailable && await IsZramEnabledAsync(ct);

            var recommended = ComputeRecommendedSwappiness(ramBytes, swappiness);

            long totalSwap = devices.Sum(d => d.SizeBytes);
            long usedSwap = devices.Sum(d => d.UsedBytes);

            var report = new SwapReport(
                CurrentSwappiness: swappiness,
                RecommendedSwappiness: recommended,
                Devices: devices,
                TotalSwapBytes: totalSwap,
                UsedSwapBytes: usedSwap,
                ZramAvailable: zramAvailable,
                ZramEnabled: zramEnabled,
                RamBytes: ramBytes,
                IsAvailable: true);

            LastReport = report;
            return new ScanResult(Id, true, devices.Count, 0);
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
                progress?.Report(new TaskProgress(Id, (double)i / totalSteps * 100, $"Applying {itemId}..."));

                int? swappinessValue = null;

                if (itemId == "set-swappiness-recommended")
                {
                    if (LastReport == null)
                        await ScanAsync(new ScanOptions(), ct);
                    swappinessValue = LastReport?.RecommendedSwappiness;
                }
                else if (itemId.StartsWith("set-swappiness:", StringComparison.Ordinal))
                {
                    var valStr = itemId["set-swappiness:".Length..];
                    if (int.TryParse(valStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                        && parsed >= 0 && parsed <= 100)
                    {
                        swappinessValue = parsed;
                    }
                }

                if (swappinessValue.HasValue)
                {
                    if (await ApplySwappinessAsync(swappinessValue.Value, ct))
                        processed++;
                }
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

    private static int ReadSwappiness()
    {
        try
        {
            if (!File.Exists("/proc/sys/vm/swappiness")) return 60;
            var text = File.ReadAllText("/proc/sys/vm/swappiness").Trim();
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var val) ? val : 60;
        }
        catch { return 60; }
    }

    private static long ReadRamBytes()
    {
        try
        {
            if (!File.Exists("/proc/meminfo")) return 0;
            foreach (var line in File.ReadAllLines("/proc/meminfo"))
            {
                if (line.StartsWith("MemTotal:", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2
                        && long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var kb))
                        return kb * 1024;
                }
            }
        }
        catch { }
        return 0;
    }

    private static async Task<List<SwapDeviceInfo>> ReadSwapDevicesAsync(CancellationToken ct)
    {
        var devices = new List<SwapDeviceInfo>();
        // Use swapon for cleaner output
        var result = await ProcessRunner.RunAsync("swapon",
            "--show=NAME,TYPE,SIZE,USED,PRIO --bytes --noheadings", ct);
        if (!result.Success || string.IsNullOrWhiteSpace(result.Stdout)) return devices;

        foreach (var line in result.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5) continue;

            long size = long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) ? s : 0;
            long used = long.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var u) ? u : 0;
            int prio = int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) ? p : 0;

            // Detect zram by path prefix
            var type = parts[1];
            if (parts[0].StartsWith("/dev/zram", StringComparison.OrdinalIgnoreCase))
                type = "zram";

            devices.Add(new SwapDeviceInfo(
                Path: parts[0],
                Type: type,
                SizeBytes: size,
                UsedBytes: used,
                Priority: prio));
        }
        return devices;
    }

    private static async Task<bool> IsZramEnabledAsync(CancellationToken ct)
    {
        var result = await ProcessRunner.RunAsync("zramctl", "--noheadings", ct);
        return result.Success && !string.IsNullOrWhiteSpace(result.Stdout);
    }

    private static int ComputeRecommendedSwappiness(long ramBytes, int current)
    {
        // Desktop/laptop with >= 4GB RAM: 10 (prefer RAM, less swap thrashing)
        // Low RAM (< 4GB): 60 (default, more swap tolerance)
        if (ramBytes >= 4 * GB) return 10;
        return 60;
    }

    private static async Task<bool> ApplySwappinessAsync(int value, CancellationToken ct)
    {
        if (value < 0 || value > 100) return false;

        // Runtime change via sysctl (requires sudo)
        var runtime = await ProcessRunner.RunAsync("sudo", $"-n sysctl vm.swappiness={value}", ct);

        // Persistent change via /etc/sysctl.d/99-auracore-swap.conf
        // Use tee via sh -c to handle sudo redirection
        var persistCmd = $"echo 'vm.swappiness={value}' | sudo -n tee /etc/sysctl.d/99-auracore-swap.conf >/dev/null";
        var persist = await ProcessRunner.RunAsync("/bin/sh", $"-c \"{persistCmd}\"", ct);

        // Consider it successful if either worked
        return runtime.Success || persist.Success;
    }
}

[SupportedOSPlatform("linux")]
public static class SwapOptimizerRegistration
{
    public static IServiceCollection AddSwapOptimizerModule(this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, SwapOptimizerModule>();
}
