using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.Application.Shared;
using AuraCore.Domain.Enums;
using AuraCore.Module.PurgeableSpaceManager.Models;

namespace AuraCore.Module.PurgeableSpaceManager;

public sealed class PurgeableSpaceManagerModule : IOptimizationModule
{
    private readonly IShellCommandService _shell;

    public PurgeableSpaceManagerModule(IShellCommandService shell)
    {
        _shell = shell;
    }

    public string Id => "purgeable-space-manager";
    public string DisplayName => "Purgeable Space Manager";
    public OptimizationCategory Category => OptimizationCategory.DiskCleanup;
    public RiskLevel Risk => RiskLevel.Medium;
    public SupportedPlatform Platform => SupportedPlatform.MacOS;

    public PurgeableReport? LastReport { get; private set; }

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsMacOS())
            return new ScanResult(Id, false, 0, 0, $"{DisplayName} is macOS-only.");

        try
        {
            // Run diskutil info /
            var diskInfo = await ProcessRunner.RunAsync("diskutil", "info /", ct);
            if (!diskInfo.Success)
            {
                LastReport = PurgeableReport.None();
                return new ScanResult(Id, false, 0, 0, "diskutil info / failed");
            }

            // Parse sizes
            var volumeFree = ParseByteValue(diskInfo.Stdout, "Volume Free Space");
            var containerFree = ParseByteValue(diskInfo.Stdout, "Container Free Space");
            var purgeable = volumeFree > containerFree ? volumeFree - containerFree : 0;

            // Phase 4.4.2: also parse total capacity so the UI can compute
            // Used/Purgeable/Free proportions. diskutil output on modern APFS
            // systems usually shows "Container Total Space"; older / non-APFS
            // volumes may show "Volume Total Space" instead. Fall back in that
            // order and default to 0 so the additive contract still holds if
            // neither label is present.
            var totalCapacity = ParseByteValue(diskInfo.Stdout, "Container Total Space");
            if (totalCapacity <= 0)
                totalCapacity = ParseByteValue(diskInfo.Stdout, "Volume Total Space");

            // List local snapshots
            var snapshots = await ListLocalSnapshotsAsync(ct);

            var report = new PurgeableReport(
                VolumeFreeBytes: volumeFree,
                ContainerFreeBytes: containerFree,
                PurgeableBytes: purgeable,
                LocalSnapshotCount: snapshots.Count,
                LocalSnapshots: snapshots,
                IsAvailable: true,
                TotalCapacityBytes: totalCapacity);

            LastReport = report;

            // ItemsFound = snapshot count (most removable)
            return new ScanResult(Id, true, snapshots.Count, purgeable);
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

        if (!OperatingSystem.IsMacOS())
            return new OptimizationResult(Id, operationId, false, 0, 0, TimeSpan.Zero);

        try
        {
            var items = plan.SelectedItemIds?.ToList() ?? new List<string>();
            int totalSteps = Math.Max(items.Count, 1);

            for (int i = 0; i < items.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var itemId = items[i];
                progress?.Report(new TaskProgress(Id, (double)i / totalSteps * 100, $"Running {itemId}..."));

                var beforeFree = await GetContainerFreeBytesAsync(ct);
                bool ok = false;

                if (itemId == "thin-snapshots")
                {
                    // Aggressive: ask for 1TB of free space (9.99GB = 9990000000 bytes; max 10 digits per validator),
                    // which forces tmutil to remove all thinnable local snapshots.
                    // Action id "purgeable" per mini-spec §3.9; argv: ["thinlocalsnapshots", "/", bytes, urgency]
                    var r = await _shell.RunPrivilegedAsync(
                        new PrivilegedCommand(
                            Id: "purgeable",
                            Executable: "tmutil",
                            Arguments: new[] { "thinlocalsnapshots", "/", "9990000000", "1" },
                            TimeoutSeconds: 300),
                        ct);
                    ok = r.Success;
                }
                else if (itemId == "run-periodic")
                {
                    // TODO(phase-5.2.2): this call-site is dead code (no UI/plan path feeds
                    // itemId="run-periodic" today) but retains a raw `sudo -n periodic ...`
                    // invocation that the 5.2.2 safety sweep flagged. Two follow-ups:
                    //   (a) remove the branch entirely if "run-periodic" is never reintroduced,
                    //   (b) OR add a new "run-periodic" action id to Task 16/27 ActionWhitelist
                    //       with a strict argv validator `["daily"|"weekly"|"monthly"...]`
                    //       + polkit / launchd policy entry, then migrate the call.
                    // Until resolved, this branch will fail closed via ProcessRunner — safe
                    // because the branch is unreachable from the product surface.
                    var r = await ProcessRunner.RunAsync("sudo", "-n periodic daily weekly monthly", ct, timeoutSeconds: 300);
                    ok = r.Success;
                }
                else if (itemId == "clean-user-caches")
                {
                    ok = await CleanOldUserCachesAsync(ct);
                }
                else if (itemId == "all")
                {
                    var r1 = await _shell.RunPrivilegedAsync(
                        new PrivilegedCommand(
                            Id: "purgeable",
                            Executable: "tmutil",
                            Arguments: new[] { "thinlocalsnapshots", "/", "9990000000", "1" },
                            TimeoutSeconds: 300),
                        ct);
                    ok = r1.Success;
                    var userCachesOk = await CleanOldUserCachesAsync(ct);
                    ok = ok || userCachesOk;
                }

                if (ok)
                {
                    processed++;
                    var afterFree = await GetContainerFreeBytesAsync(ct);
                    var delta = afterFree - beforeFree;
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

    private static long ParseByteValue(string text, string labelPrefix)
    {
        // diskutil output example:
        // "Container Free Space:      123456789 Bytes (12.3 GB)"
        // Pattern: <label>: <number> Bytes
        var pattern = @$"{Regex.Escape(labelPrefix)}:\s*([\d,]+)\s*Bytes";
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (!match.Success) return 0;

        var numStr = match.Groups[1].Value.Replace(",", "");
        return long.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bytes) ? bytes : 0;
    }

    private static async Task<long> GetContainerFreeBytesAsync(CancellationToken ct)
    {
        var result = await ProcessRunner.RunAsync("diskutil", "info /", ct);
        if (!result.Success) return 0;
        return ParseByteValue(result.Stdout, "Container Free Space");
    }

    private static async Task<List<string>> ListLocalSnapshotsAsync(CancellationToken ct)
    {
        var snapshots = new List<string>();
        var result = await ProcessRunner.RunAsync("tmutil", "localsnapshots", ct);
        if (!result.Success) return snapshots;

        // Output format: one snapshot per line, starts with "com.apple.TimeMachine." or contains date
        foreach (var line in result.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.Contains("com.apple.TimeMachine") || Regex.IsMatch(line, @"\d{4}-\d{2}-\d{2}"))
                snapshots.Add(line);
        }
        return snapshots;
    }

    private static async Task<bool> CleanOldUserCachesAsync(CancellationToken ct)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home)) return false;

        var cachesPath = Path.Combine(home, "Library", "Caches");
        if (!Directory.Exists(cachesPath)) return false;

        // Find files older than 30 days and delete them conservatively
        // Use find -type f -mtime +30 -delete (BSD find on macOS)
        var cmd = $"find '{cachesPath}' -type f -mtime +30 -delete 2>/dev/null; echo done";
        var result = await ProcessRunner.RunAsync("/bin/sh", $"-c \"{cmd}\"", ct, timeoutSeconds: 300);
        return result.Success;
    }
}

public static class PurgeableSpaceManagerRegistration
{
    public static IServiceCollection AddPurgeableSpaceManagerModule(this IServiceCollection services)
    {
        services.AddSingleton<PurgeableSpaceManagerModule>();
        services.AddSingleton<IOptimizationModule>(sp => sp.GetRequiredService<PurgeableSpaceManagerModule>());
        return services;
    }
}
