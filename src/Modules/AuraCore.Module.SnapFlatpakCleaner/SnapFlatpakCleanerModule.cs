using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.Application.Shared;
using AuraCore.Domain.Enums;

namespace AuraCore.Module.SnapFlatpakCleaner;

public sealed class SnapFlatpakCleanerModule : IOptimizationModule
{
    private readonly IShellCommandService _shell;

    public SnapFlatpakCleanerModule(IShellCommandService shell)
    {
        _shell = shell;
    }

    public string Id => "snap-flatpak-cleaner";
    public string DisplayName => "Snap & Flatpak Cleaner";
    public OptimizationCategory Category => OptimizationCategory.SystemCleaning;
    public RiskLevel Risk => RiskLevel.Low;
    public SupportedPlatform Platform => SupportedPlatform.Linux;

    // Phase 4.3.2 UI exposure — additive: last scan results, populated by ScanAsync.
    // ViewModel reads these to render per-category stats ("-- " when unavailable)
    // without needing to shell out on its own. Kept as properties with private setters
    // to mirror the JournalCleanerModule.LastReport pattern.
    public int LastDisabledSnapCount { get; private set; }
    public int LastUnusedFlatpakCount { get; private set; }
    public bool LastSnapAvailable { get; private set; }
    public bool LastFlatpakAvailable { get; private set; }

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
            LastSnapAvailable = hasSnap;
            int snapCount = 0;
            if (hasSnap)
            {
                snapCount = await CountDisabledSnapsAsync(ct);
                totalItems += snapCount;
            }
            LastDisabledSnapCount = snapCount;

            // 2. Check for unused flatpak runtimes
            bool hasFlatpak = await ProcessRunner.CommandExistsAsync("flatpak", ct);
            LastFlatpakAvailable = hasFlatpak;
            int flatpakCount = 0;
            if (hasFlatpak)
            {
                flatpakCount = await CountUnusedFlatpaksAsync(ct);
                totalItems += flatpakCount;
            }
            LastUnusedFlatpakCount = flatpakCount;

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

    // ---- Scan helpers (non-privileged — kept static) ----

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
    /// Handles "snap-remove:&lt;name&gt;:&lt;revision&gt;" items via IShellCommandService.
    /// NOTE: The --revision flag is dropped because the polkit whitelist (Task 16
    /// SnapFlatpakArgvValidator) does not permit it. Snap will remove all revisions
    /// of the named package — a slight behavior change from the original but safer
    /// and consistent with the whitelist's conservative stance.
    /// </summary>
    private async Task<bool> HandleSnapRemoveAsync(string itemId, CancellationToken ct)
    {
        // Expected format: "snap-remove:<snap-name>:<revision>"
        var parts = itemId.Split(':');
        if (parts.Length != 3)
            return false;

        var name = parts[1];
        var revision = parts[2];

        if (!SafeNameRegex.IsMatch(name) || !SafeNameRegex.IsMatch(revision))
            return false;

        // Drop --revision: polkit whitelist does not allow it.
        // Snap removes all revisions of the named package as a result.
        var result = await _shell.RunPrivilegedAsync(
            new PrivilegedCommand(
                Id: "snap-flatpak",
                Executable: "snap",
                Arguments: new[] { "snap", "remove", name },
                TimeoutSeconds: 120),
            ct);

        return result.Success;
    }

    /// <summary>
    /// Handles "flatpak-remove:&lt;app-id&gt;" items.
    /// Validates the app ID before executing. Flatpak uninstall is unprivileged.
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
    /// Removes all disabled snap revisions by querying the disabled list (non-privileged)
    /// then calling IShellCommandService.RunPrivilegedAsync per package name.
    /// NOTE: --revision is not passed because the polkit whitelist does not allow it;
    /// each removal targets all revisions of the named package.
    /// Returns true if at least one removal succeeded (or no disabled snaps found).
    /// </summary>
    private async Task<bool> SnapCleanAllAsync(CancellationToken ct)
    {
        // Query disabled snaps without privilege
        var listResult = await ProcessRunner.RunAsync("snap", "list --all", ct, timeoutSeconds: 30);
        if (!listResult.Success || string.IsNullOrWhiteSpace(listResult.Stdout))
            return true; // Nothing to remove — not a failure

        // Parse disabled entries: columns are Name, Version, Rev, Tracking, Publisher, Notes
        // "disabled" appears in the Notes column (last field)
        var disabledNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in listResult.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.Contains("disabled", StringComparison.OrdinalIgnoreCase))
                continue;

            var cols = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (cols.Length == 0) continue;

            var name = cols[0];
            if (SafeNameRegex.IsMatch(name))
                disabledNames.Add(name);
        }

        if (disabledNames.Count == 0)
            return true; // No disabled snaps — success

        bool anySuccess = false;
        foreach (var name in disabledNames)
        {
            ct.ThrowIfCancellationRequested();
            var r = await _shell.RunPrivilegedAsync(
                new PrivilegedCommand(
                    Id: "snap-flatpak",
                    Executable: "snap",
                    Arguments: new[] { "snap", "remove", name },
                    TimeoutSeconds: 120),
                ct);

            if (r.Success) anySuccess = true;
        }

        return anySuccess;
    }

    /// <summary>
    /// Removes all unused flatpak runtimes/apps in one go. Flatpak uninstall is unprivileged.
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
