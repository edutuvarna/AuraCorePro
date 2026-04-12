using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Application.Shared;
using AuraCore.Domain.Enums;

namespace AuraCore.Module.SnapFlatpakCleaner;

public sealed class SnapFlatpakCleanerModule : IOptimizationModule
{
    public string Id => "snap-flatpak-cleaner";
    public string DisplayName => "Snap & Flatpak Cleaner";
    public OptimizationCategory Category => OptimizationCategory.SystemCleaning;
    public RiskLevel Risk => RiskLevel.Low;
    public SupportedPlatform Platform => SupportedPlatform.Linux;

    /// <summary>
    /// Validates snap package names and flatpak application IDs.
    /// Allows alphanumeric characters, dashes, dots, and underscores.
    /// </summary>
    private static readonly Regex SafeNameRegex = new(@"^[a-zA-Z0-9._\-]+$", RegexOptions.Compiled);

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsLinux())
            return new ScanResult(Id, false, 0, 0, $"{DisplayName} is Linux-only.");

        try
        {
            int totalItems = 0;

            // 1. Check for disabled snap revisions
            bool hasSnap = await ProcessRunner.CommandExistsAsync("snap", ct);
            if (hasSnap)
            {
                totalItems += await CountDisabledSnapsAsync(ct);
            }

            // 2. Check for unused flatpak runtimes
            bool hasFlatpak = await ProcessRunner.CommandExistsAsync("flatpak", ct);
            if (hasFlatpak)
            {
                totalItems += await CountUnusedFlatpaksAsync(ct);
            }

            if (!hasSnap && !hasFlatpak)
            {
                return new ScanResult(Id, false, 0, 0,
                    "Neither snap nor flatpak is installed.");
            }

            return new ScanResult(Id, true, totalItems, 0);
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
            var items = plan.SelectedItemIds?.ToList() ?? new List<string>();
            int totalSteps = Math.Max(items.Count, 1);

            for (int i = 0; i < items.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var itemId = items[i];
                progress?.Report(new TaskProgress(Id, (double)i / totalSteps * 100, $"Running {itemId}..."));

                bool ok = false;

                if (itemId.StartsWith("snap-remove:"))
                {
                    ok = await HandleSnapRemoveAsync(itemId, ct);
                }
                else if (itemId.StartsWith("flatpak-remove:"))
                {
                    ok = await HandleFlatpakRemoveAsync(itemId, ct);
                }
                else if (itemId == "snap-clean-all")
                {
                    ok = await SnapCleanAllAsync(ct);
                }
                else if (itemId == "flatpak-clean-all")
                {
                    ok = await FlatpakCleanAllAsync(ct);
                }

                if (ok) processed++;
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

    // ---- Scan helpers ----

    /// <summary>
    /// Runs <c>snap list --all</c> and counts entries with "disabled" status (old revisions).
    /// </summary>
    private static async Task<int> CountDisabledSnapsAsync(CancellationToken ct)
    {
        var result = await ProcessRunner.RunAsync("snap", "list --all", ct);
        if (!result.Success || string.IsNullOrWhiteSpace(result.Stdout))
            return 0;

        int count = 0;
        foreach (var line in result.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Contains("disabled", StringComparison.OrdinalIgnoreCase))
                count++;
        }
        return count;
    }

    /// <summary>
    /// Runs <c>flatpak list --unused --columns=application</c> and counts unused runtimes.
    /// </summary>
    private static async Task<int> CountUnusedFlatpaksAsync(CancellationToken ct)
    {
        var result = await ProcessRunner.RunAsync("flatpak", "list --unused --columns=application", ct);
        if (!result.Success || string.IsNullOrWhiteSpace(result.Stdout))
            return 0;

        // Each non-empty line is an unused runtime/application
        int count = 0;
        foreach (var line in result.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!string.IsNullOrWhiteSpace(line))
                count++;
        }
        return count;
    }

    // ---- Optimize helpers ----

    /// <summary>
    /// Handles "snap-remove:&lt;name&gt;:&lt;revision&gt;" items.
    /// Validates both name and revision before executing.
    /// </summary>
    private static async Task<bool> HandleSnapRemoveAsync(string itemId, CancellationToken ct)
    {
        // Expected format: "snap-remove:<snap-name>:<revision>"
        var parts = itemId.Split(':');
        if (parts.Length != 3)
            return false;

        var name = parts[1];
        var revision = parts[2];

        if (!SafeNameRegex.IsMatch(name) || !SafeNameRegex.IsMatch(revision))
            return false;

        var result = await ProcessRunner.RunAsync(
            "sudo", $"-n snap remove {name} --revision={revision}",
            ct, timeoutSeconds: 120);
        return result.Success;
    }

    /// <summary>
    /// Handles "flatpak-remove:&lt;app-id&gt;" items.
    /// Validates the app ID before executing.
    /// </summary>
    private static async Task<bool> HandleFlatpakRemoveAsync(string itemId, CancellationToken ct)
    {
        // Expected format: "flatpak-remove:<app-id>"
        var parts = itemId.Split(':', 2);
        if (parts.Length != 2)
            return false;

        var appId = parts[1];

        if (!SafeNameRegex.IsMatch(appId))
            return false;

        var result = await ProcessRunner.RunAsync(
            "flatpak", $"uninstall -y {appId}",
            ct, timeoutSeconds: 120);
        return result.Success;
    }

    /// <summary>
    /// Removes all disabled snap revisions in one go via shell pipeline.
    /// </summary>
    private static async Task<bool> SnapCleanAllAsync(CancellationToken ct)
    {
        var cmd = "snap list --all | awk '/disabled/{print $1, $3}' | while read name rev; do sudo snap remove \"$name\" --revision=\"$rev\"; done";
        var result = await ProcessRunner.RunAsync("/bin/sh", $"-c \"{cmd}\"", ct, timeoutSeconds: 120);
        return result.Success;
    }

    /// <summary>
    /// Removes all unused flatpak runtimes/apps in one go.
    /// </summary>
    private static async Task<bool> FlatpakCleanAllAsync(CancellationToken ct)
    {
        var result = await ProcessRunner.RunAsync(
            "flatpak", "uninstall --unused -y",
            ct, timeoutSeconds: 120);
        return result.Success;
    }
}

public static class SnapFlatpakCleanerRegistration
{
    public static IServiceCollection AddSnapFlatpakCleanerModule(this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, SnapFlatpakCleanerModule>();
}
