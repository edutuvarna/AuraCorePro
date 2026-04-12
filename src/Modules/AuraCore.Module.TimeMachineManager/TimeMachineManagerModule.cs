using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Application.Shared;
using AuraCore.Domain.Enums;
using AuraCore.Module.TimeMachineManager.Models;

namespace AuraCore.Module.TimeMachineManager;

public sealed class TimeMachineManagerModule : IOptimizationModule
{
    public string Id => "time-machine-manager";
    public string DisplayName => "Time Machine Manager";
    public OptimizationCategory Category => OptimizationCategory.SystemHealth;
    public RiskLevel Risk => RiskLevel.Low;
    public SupportedPlatform Platform => SupportedPlatform.MacOS;

    public TimeMachineReport? LastReport { get; private set; }

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsMacOS())
            return new ScanResult(Id, false, 0, 0, $"{DisplayName} is macOS-only.");

        try
        {
            if (!await ProcessRunner.CommandExistsAsync("tmutil", ct))
            {
                LastReport = TimeMachineReport.None();
                return new ScanResult(Id, false, 0, 0, "tmutil not available");
            }

            // Check Time Machine destination configuration
            var destInfo = await ProcessRunner.RunAsync("tmutil", "destinationinfo", ct);
            var configured = destInfo.Success &&
                             !destInfo.Stdout.Contains("No destinations", StringComparison.OrdinalIgnoreCase);

            string? destination = null;
            if (configured)
            {
                // Parse "Name : X" line from destinationinfo output
                var match = Regex.Match(destInfo.Stdout, @"Name\s*:\s*(.+)");
                if (match.Success)
                    destination = match.Groups[1].Value.Trim();
            }

            // List backups
            var backups = new List<TimeMachineBackup>();
            if (configured)
            {
                var listResult = await ProcessRunner.RunAsync("tmutil", "listbackups", ct);
                if (listResult.Success)
                {
                    foreach (var rawLine in listResult.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        var line = rawLine;
                        var dateMatch = Regex.Match(line, @"(\d{4}-\d{2}-\d{2})-(\d{6})");
                        DateTime date = DateTime.MinValue;
                        if (dateMatch.Success)
                        {
                            var datePart = dateMatch.Groups[1].Value;
                            var timePart = dateMatch.Groups[2].Value; // HHMMSS
                            // Insert colons so HHMMSS -> HH:MM:SS
                            var iso = $"{datePart}T{timePart.Insert(4, ":").Insert(2, ":")}";
                            DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out date);
                        }
                        backups.Add(new TimeMachineBackup(date, line, 0));
                    }
                }
            }

            // List local snapshots
            var snapshots = new List<string>();
            var snapshotResult = await ProcessRunner.RunAsync("tmutil", "localsnapshots", ct);
            if (snapshotResult.Success)
            {
                foreach (var line in snapshotResult.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (line.Contains("com.apple.TimeMachine") || Regex.IsMatch(line, @"\d{4}-\d{2}-\d{2}"))
                        snapshots.Add(line);
                }
            }

            var report = new TimeMachineReport(
                Configured: configured,
                Destination: destination,
                Backups: backups,
                LocalSnapshotCount: snapshots.Count,
                LocalSnapshots: snapshots,
                IsAvailable: true);

            LastReport = report;

            // ItemsFound = backup count + snapshot count. We don't measure byte savings.
            return new ScanResult(Id, true, backups.Count + snapshots.Count, 0);
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
            if (!await ProcessRunner.CommandExistsAsync("tmutil", ct))
                return new OptimizationResult(Id, operationId, false, 0, 0, DateTime.UtcNow - start);

            // Ensure LastReport is populated so we can validate user-supplied paths
            if (LastReport == null)
                await ScanAsync(new ScanOptions(), ct);

            var validBackupPaths = new HashSet<string>(
                LastReport?.Backups.Select(b => b.Path) ?? Enumerable.Empty<string>(),
                StringComparer.Ordinal);

            var items = plan.SelectedItemIds?.ToList() ?? new List<string>();
            int totalSteps = Math.Max(items.Count, 1);

            for (int i = 0; i < items.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var itemId = items[i];
                progress?.Report(new TaskProgress(Id, (double)i / totalSteps * 100, $"Processing {itemId}..."));

                if (itemId == "delete-local-snapshots")
                {
                    // Aggressive: ask for 1TB of free space (impossible), which forces
                    // tmutil to remove all thinnable local snapshots. No sudo required.
                    var r = await ProcessRunner.RunAsync(
                        "tmutil",
                        "thinlocalsnapshots / 999999999999 1",
                        ct,
                        timeoutSeconds: 300);
                    if (r.Success) processed++;
                }
                else if (itemId.StartsWith("delete-old-backups:", StringComparison.Ordinal))
                {
                    var daysStr = itemId["delete-old-backups:".Length..];
                    // Strict validation: positive integer only
                    if (!int.TryParse(daysStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days) || days <= 0)
                        continue;

                    var cutoff = DateTime.Now.AddDays(-days);
                    if (LastReport == null) continue;

                    foreach (var backup in LastReport.Backups.Where(b => b.Date != DateTime.MinValue && b.Date < cutoff))
                    {
                        ct.ThrowIfCancellationRequested();
                        // Path came from LastReport.Backups (parsed from tmutil listbackups output).
                        // Additional sanitization against shell metacharacters anyway.
                        if (ContainsShellMetacharacter(backup.Path)) continue;

                        var escapedPath = backup.Path.Replace("\"", "\\\"");
                        var deleteCmd = $"sudo -n tmutil delete \"{escapedPath}\"";
                        var r = await ProcessRunner.RunAsync(
                            "/bin/sh",
                            $"-c \"{deleteCmd}\"",
                            ct,
                            timeoutSeconds: 300);
                        if (r.Success) processed++;
                    }
                }
                else if (itemId.StartsWith("delete-backup:", StringComparison.Ordinal))
                {
                    var path = itemId["delete-backup:".Length..];

                    // Strict safety: path MUST exactly match one from the scan report.
                    // This prevents arbitrary `tmutil delete <path>` from user input.
                    if (!validBackupPaths.Contains(path)) continue;

                    // Extra defense: reject shell metacharacters even if the path is in LastReport.
                    if (ContainsShellMetacharacter(path)) continue;

                    var escapedPath = path.Replace("\"", "\\\"");
                    var deleteCmd = $"sudo -n tmutil delete \"{escapedPath}\"";
                    var r = await ProcessRunner.RunAsync(
                        "/bin/sh",
                        $"-c \"{deleteCmd}\"",
                        ct,
                        timeoutSeconds: 300);
                    if (r.Success) processed++;
                }
                // Unknown item IDs are silently ignored
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

    /// <summary>
    /// Defensive check against command-injection characters. Even though we pass paths
    /// through quoted /bin/sh -c, any unexpected metacharacter is a red flag.
    /// </summary>
    private static bool ContainsShellMetacharacter(string input)
    {
        // Semicolons, pipes, backticks, $ (command substitution), backslashes other than escapes,
        // newlines, and ampersands should never legitimately appear in a Time Machine backup path.
        foreach (var ch in input)
        {
            if (ch == ';' || ch == '|' || ch == '`' || ch == '$' ||
                ch == '&' || ch == '\n' || ch == '\r' || ch == '<' || ch == '>')
                return true;
        }
        return false;
    }
}

public static class TimeMachineManagerRegistration
{
    public static IServiceCollection AddTimeMachineManagerModule(this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, TimeMachineManagerModule>();
}
