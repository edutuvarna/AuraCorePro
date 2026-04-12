using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Application.Shared;
using AuraCore.Domain.Enums;
using AuraCore.Module.JournalCleaner.Models;

namespace AuraCore.Module.JournalCleaner;

public sealed class JournalCleanerModule : IOptimizationModule
{
    public string Id => "journal-cleaner";
    public string DisplayName => "Journal Cleaner";
    public OptimizationCategory Category => OptimizationCategory.DiskCleanup;
    public RiskLevel Risk => RiskLevel.Low;
    public SupportedPlatform Platform => SupportedPlatform.Linux;

    public JournalReport? LastReport { get; private set; }

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsLinux())
            return new ScanResult(Id, false, 0, 0, $"{DisplayName} is Linux-only.");

        try
        {
            if (!await ProcessRunner.CommandExistsAsync("journalctl", ct))
            {
                LastReport = JournalReport.None();
                return new ScanResult(Id, false, 0, 0, "systemd-journald (journalctl) not available");
            }

            var currentBytes = await GetDiskUsageAsync(ct);
            var oldestEntry = await GetOldestBootDateAsync(ct);
            var fileCount = await GetJournalFileCountAsync(ct);
            var recommended = ComputeRecommendedLimit(currentBytes);

            var report = new JournalReport(currentBytes, oldestEntry, fileCount, recommended, true);
            LastReport = report;

            long potentialSavings = currentBytes > recommended ? currentBytes - recommended : 0;
            return new ScanResult(Id, true, fileCount, potentialSavings);
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
            if (!await ProcessRunner.CommandExistsAsync("journalctl", ct))
                return new OptimizationResult(Id, operationId, false, 0, 0, DateTime.UtcNow - start);

            var items = plan.SelectedItemIds?.ToList() ?? new List<string>();
            int totalSteps = Math.Max(items.Count, 1);

            for (int i = 0; i < items.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var itemId = items[i];
                progress?.Report(new TaskProgress(Id, (double)i / totalSteps * 100, $"Vacuuming journal ({itemId})..."));

                var beforeBytes = await GetDiskUsageAsync(ct);
                var args = itemId switch
                {
                    "vacuum-500m" => "--vacuum-size=500M",
                    "vacuum-1g" => "--vacuum-size=1G",
                    "vacuum-7days" => "--vacuum-time=7d",
                    "vacuum-30days" => "--vacuum-time=30d",
                    _ => ""
                };

                if (string.IsNullOrEmpty(args)) continue;

                var result = await ProcessRunner.RunAsync("journalctl", args, ct);
                if (result.Success)
                {
                    processed++;
                    var afterBytes = await GetDiskUsageAsync(ct);
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

    private static async Task<long> GetDiskUsageAsync(CancellationToken ct)
    {
        var result = await ProcessRunner.RunAsync("journalctl", "--disk-usage", ct);
        if (!result.Success || string.IsNullOrWhiteSpace(result.Stdout)) return 0;

        // Output like: "Archived and active journals take up 1.5G in the file system."
        var match = Regex.Match(result.Stdout, @"([\d.]+)\s*([KMGT]?)", RegexOptions.IgnoreCase);
        if (!match.Success) return 0;

        if (!double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return 0;

        var suffix = match.Groups[2].Value.ToUpperInvariant();
        long multiplier = suffix switch
        {
            "K" => 1024L,
            "M" => 1024L * 1024,
            "G" => 1024L * 1024 * 1024,
            "T" => 1024L * 1024 * 1024 * 1024,
            _ => 1L
        };

        return (long)(value * multiplier);
    }

    private static async Task<DateTime?> GetOldestBootDateAsync(CancellationToken ct)
    {
        var result = await ProcessRunner.RunAsync("/bin/sh", "-c \"journalctl --list-boots --no-pager 2>/dev/null | head -1\"", ct);
        if (!result.Success || string.IsNullOrWhiteSpace(result.Stdout)) return null;

        // Try to extract a date pattern (YYYY-MM-DD)
        var match = Regex.Match(result.Stdout, @"(\d{4}-\d{2}-\d{2})");
        if (match.Success && DateTime.TryParse(match.Groups[1].Value, out var date))
            return date;
        return null;
    }

    private static async Task<int> GetJournalFileCountAsync(CancellationToken ct)
    {
        var result = await ProcessRunner.RunAsync("/bin/sh", "-c \"ls /var/log/journal/ 2>/dev/null | wc -l\"", ct);
        return int.TryParse(result.Stdout.Trim(), out var count) ? count : 0;
    }

    private static long ComputeRecommendedLimit(long currentBytes)
    {
        const long MB = 1024L * 1024;
        if (currentBytes > 500 * MB) return 500 * MB;
        if (currentBytes > 200 * MB) return 200 * MB;
        return currentBytes; // already small, no cleanup needed
    }
}

public static class JournalCleanerRegistration
{
    public static IServiceCollection AddJournalCleanerModule(this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, JournalCleanerModule>();
}
