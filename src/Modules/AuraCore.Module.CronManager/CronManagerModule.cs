using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Application.Shared;
using AuraCore.Domain.Enums;
using AuraCore.Module.CronManager.Models;

namespace AuraCore.Module.CronManager;

public sealed class CronManagerModule : IOptimizationModule
{
    public string Id => "cron-manager";
    public string DisplayName => "Cron Manager";
    public OptimizationCategory Category => OptimizationCategory.ShellCustomization;
    public RiskLevel Risk => RiskLevel.Low;
    public SupportedPlatform Platform => SupportedPlatform.Linux;

    public CronReport? LastReport { get; private set; }

    // Valid @-prefix schedules
    private static readonly HashSet<string> AtSchedules = new()
    {
        "@reboot", "@yearly", "@annually", "@monthly", "@weekly", "@daily", "@hourly", "@midnight"
    };

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsLinux())
            return new ScanResult(Id, false, 0, 0, $"{DisplayName} is Linux-only.");

        try
        {
            if (!await ProcessRunner.CommandExistsAsync("crontab", ct))
            {
                LastReport = CronReport.None();
                return new ScanResult(Id, false, 0, 0, "crontab command not available");
            }

            var userJobs = await ReadUserCrontabAsync(ct);
            var systemJobs = new List<CronJobInfo>();

            // /etc/crontab
            if (File.Exists("/etc/crontab"))
            {
                try
                {
                    var lines = File.ReadAllLines("/etc/crontab");
                    systemJobs.AddRange(ParseCronFile("/etc/crontab", lines, hasUserField: true));
                }
                catch { /* skip unreadable */ }
            }

            // /etc/cron.d/*
            if (Directory.Exists("/etc/cron.d"))
            {
                try
                {
                    foreach (var file in Directory.EnumerateFiles("/etc/cron.d"))
                    {
                        ct.ThrowIfCancellationRequested();
                        var name = Path.GetFileName(file);
                        if (name.StartsWith('.')) continue;
                        try
                        {
                            var lines = File.ReadAllLines(file);
                            systemJobs.AddRange(ParseCronFile(file, lines, hasUserField: true));
                        }
                        catch { /* skip unreadable */ }
                    }
                }
                catch { /* skip directory enumeration failure */ }
            }

            // /etc/cron.{hourly,daily,weekly,monthly}/*
            foreach (var period in new[] { "hourly", "daily", "weekly", "monthly" })
            {
                var dir = $"/etc/cron.{period}";
                if (!Directory.Exists(dir)) continue;
                try
                {
                    foreach (var file in Directory.EnumerateFiles(dir))
                    {
                        ct.ThrowIfCancellationRequested();
                        var name = Path.GetFileName(file);
                        if (name.StartsWith('.')) continue;
                        // These are scripts — no cron syntax, just run at the period
                        var exists = File.Exists(file);
                        systemJobs.Add(new CronJobInfo(
                            Source: dir,
                            LineNumber: 0,
                            Schedule: $"periodic:{period}",
                            Command: file,
                            IsValid: true,
                            CommandExists: exists,
                            Issue: exists ? null : "Script file missing"));
                    }
                }
                catch { /* skip directory enumeration failure */ }
            }

            // Post-scan: check if commands exist for all parsed jobs
            var updatedUserJobs = new List<CronJobInfo>();
            var updatedSystemJobs = new List<CronJobInfo>();
            foreach (var job in userJobs)
            {
                ct.ThrowIfCancellationRequested();
                updatedUserJobs.Add(await CheckCommandExistenceAsync(job, ct));
            }
            foreach (var job in systemJobs)
            {
                ct.ThrowIfCancellationRequested();
                // For periodic scripts we already set CommandExists; skip re-check
                if (job.Schedule.StartsWith("periodic:"))
                    updatedSystemJobs.Add(job);
                else
                    updatedSystemJobs.Add(await CheckCommandExistenceAsync(job, ct));
            }

            var totalJobs = updatedUserJobs.Count + updatedSystemJobs.Count;
            var deadJobs = updatedUserJobs.Concat(updatedSystemJobs).Count(j => !j.CommandExists);
            var invalidJobs = updatedUserJobs.Concat(updatedSystemJobs).Count(j => !j.IsValid);

            var report = new CronReport(
                UserJobs: updatedUserJobs,
                SystemJobs: updatedSystemJobs,
                TotalJobs: totalJobs,
                DeadJobCount: deadJobs,
                InvalidJobCount: invalidJobs,
                IsAvailable: true);

            LastReport = report;
            return new ScanResult(Id, true, totalJobs, 0);
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
                progress?.Report(new TaskProgress(Id, (double)i / totalSteps * 100, $"Running {itemId}..."));

                if (itemId == "backup")
                {
                    if (await BackupCrontabsAsync(ct)) processed++;
                }
                // Other items (list-orphaned, etc.) are inspection-only, no-op
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

    private static async Task<List<CronJobInfo>> ReadUserCrontabAsync(CancellationToken ct)
    {
        var result = await ProcessRunner.RunAsync("crontab", "-l", ct);
        // Non-zero exit with "no crontab" is normal for users without cron jobs
        if (!result.Success) return new List<CronJobInfo>();

        var lines = result.Stdout.Split('\n', StringSplitOptions.None);
        return ParseCronFile("user", lines, hasUserField: false);
    }

    private static List<CronJobInfo> ParseCronFile(string source, IReadOnlyList<string> lines, bool hasUserField)
    {
        var jobs = new List<CronJobInfo>();

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            // Skip env assignments like PATH=..., MAILTO=...
            if (Regex.IsMatch(line, @"^\w+\s*=")) continue;

            // Split into whitespace-separated tokens, preserving the command as the last N tokens
            var tokens = Regex.Split(line, @"\s+");

            string schedule;
            int commandStart;

            if (tokens[0].StartsWith('@'))
            {
                // @daily, @reboot, etc. - single field + optional user + command
                schedule = tokens[0];
                commandStart = hasUserField ? 2 : 1;
                if (!AtSchedules.Contains(schedule))
                {
                    jobs.Add(new CronJobInfo(
                        Source: source, LineNumber: i + 1, Schedule: schedule,
                        Command: commandStart < tokens.Length
                            ? string.Join(' ', tokens.Skip(commandStart))
                            : "",
                        IsValid: false, CommandExists: false,
                        Issue: $"Unknown @-schedule: {schedule}"));
                    continue;
                }
            }
            else
            {
                // 5 schedule fields + optional user + command
                int minTokens = 5 + (hasUserField ? 2 : 1);
                if (tokens.Length < minTokens)
                {
                    jobs.Add(new CronJobInfo(
                        Source: source, LineNumber: i + 1, Schedule: "",
                        Command: line, IsValid: false, CommandExists: false,
                        Issue: "Too few fields"));
                    continue;
                }
                schedule = string.Join(' ', tokens.Take(5));
                commandStart = hasUserField ? 6 : 5;
            }

            if (commandStart >= tokens.Length)
            {
                jobs.Add(new CronJobInfo(
                    Source: source, LineNumber: i + 1, Schedule: schedule,
                    Command: "", IsValid: false, CommandExists: false,
                    Issue: "Missing command"));
                continue;
            }

            var command = string.Join(' ', tokens.Skip(commandStart));
            jobs.Add(new CronJobInfo(
                Source: source, LineNumber: i + 1, Schedule: schedule,
                Command: command, IsValid: true, CommandExists: false, Issue: null));
        }

        return jobs;
    }

    private static async Task<CronJobInfo> CheckCommandExistenceAsync(CronJobInfo job, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(job.Command)) return job;

        // Extract first token of command (the binary/script)
        var parts = job.Command.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return job;
        var firstToken = parts[0];

        // Strip common prefixes like /bin/sh -c "..."
        // For simple heuristic: just check the literal first token
        bool exists;
        if (firstToken.StartsWith('/'))
            exists = File.Exists(firstToken);
        else
            exists = await ProcessRunner.CommandExistsAsync(firstToken, ct);

        return job with
        {
            CommandExists = exists,
            Issue = exists ? job.Issue : $"Command '{firstToken}' not found"
        };
    }

    private static async Task<bool> BackupCrontabsAsync(CancellationToken ct)
    {
        try
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(home)) return false;

            var dir = Path.Combine(home, ".auracore");
            Directory.CreateDirectory(dir);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var backupPath = Path.Combine(dir, $"cron-backup-{timestamp}.txt");

            using var writer = new StreamWriter(backupPath);

            await writer.WriteLineAsync("=== User crontab ===");
            var user = await ProcessRunner.RunAsync("crontab", "-l", ct);
            await writer.WriteAsync(user.Success ? user.Stdout : "(no user crontab)\n");

            await writer.WriteLineAsync();
            await writer.WriteLineAsync("=== /etc/crontab ===");
            if (File.Exists("/etc/crontab"))
                await writer.WriteAsync(await File.ReadAllTextAsync("/etc/crontab", ct));

            await writer.WriteLineAsync();
            await writer.WriteLineAsync("=== /etc/cron.d/ ===");
            if (Directory.Exists("/etc/cron.d"))
            {
                foreach (var file in Directory.EnumerateFiles("/etc/cron.d"))
                {
                    try
                    {
                        await writer.WriteLineAsync($"--- {file} ---");
                        await writer.WriteAsync(await File.ReadAllTextAsync(file, ct));
                    }
                    catch { }
                }
            }

            return true;
        }
        catch { return false; }
    }
}

public static class CronManagerRegistration
{
    public static IServiceCollection AddCronManagerModule(this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, CronManagerModule>();
}
