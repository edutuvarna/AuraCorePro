using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Application.Shared;
using AuraCore.Domain.Enums;
using AuraCore.Module.DefaultsOptimizer.Models;

namespace AuraCore.Module.DefaultsOptimizer;

[SupportedOSPlatform("macos")]
public sealed class DefaultsOptimizerModule : IOptimizationModule
{
    public string Id => "defaults-optimizer";
    public string DisplayName => "Defaults Optimizer";
    public OptimizationCategory Category => OptimizationCategory.ShellCustomization;
    public RiskLevel Risk => RiskLevel.Medium;
    public SupportedPlatform Platform => SupportedPlatform.MacOS;

    public DefaultsReport? LastReport { get; private set; }

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsMacOS())
            return new ScanResult(Id, false, 0, 0, $"{DisplayName} is macOS-only.");

        try
        {
            if (!await ProcessRunner.CommandExistsAsync("defaults", ct))
            {
                LastReport = DefaultsReport.None();
                return new ScanResult(Id, false, 0, 0, "defaults command not available");
            }

            var tweaksWithCurrent = new List<DefaultsTweak>();
            foreach (var tweak in DefaultsTweaksCatalog.All)
            {
                ct.ThrowIfCancellationRequested();
                var current = await ReadCurrentValueAsync(tweak.Domain, tweak.Key, ct);
                tweaksWithCurrent.Add(tweak with { CurrentValue = current });
            }

            var applied = tweaksWithCurrent.Count(t => t.IsApplied);
            var pending = tweaksWithCurrent.Count - applied;

            var report = new DefaultsReport(
                Tweaks: tweaksWithCurrent,
                TotalCount: tweaksWithCurrent.Count,
                AppliedCount: applied,
                PendingCount: pending,
                IsAvailable: true);

            LastReport = report;
            return new ScanResult(Id, true, pending, 0);
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
            if (!await ProcessRunner.CommandExistsAsync("defaults", ct))
                return new OptimizationResult(Id, operationId, false, 0, 0, DateTime.UtcNow - start);

            var items = plan.SelectedItemIds?.ToList() ?? new List<string>();
            int totalSteps = Math.Max(items.Count, 1);
            var affectedApps = new HashSet<string>();

            for (int i = 0; i < items.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var itemId = items[i];
                progress?.Report(new TaskProgress(Id, (double)i / totalSteps * 100, $"Applying {itemId}..."));

                // Handle "all" as a convenience alias
                if (itemId == "all")
                {
                    foreach (var tweak in DefaultsTweaksCatalog.All)
                    {
                        ct.ThrowIfCancellationRequested();
                        if (await ApplyTweakAsync(tweak, ct))
                        {
                            processed++;
                            TrackAffectedApp(tweak.Domain, affectedApps);
                        }
                    }
                    continue;
                }

                // Single tweak by Id
                var single = DefaultsTweaksCatalog.FindById(itemId);
                if (single == null) continue;

                if (await ApplyTweakAsync(single, ct))
                {
                    processed++;
                    TrackAffectedApp(single.Domain, affectedApps);
                }
            }

            // Restart affected apps to apply changes
            foreach (var app in affectedApps)
            {
                ct.ThrowIfCancellationRequested();
                await ProcessRunner.RunAsync("killall", app, ct, timeoutSeconds: 10);
                // Ignore failures - app may not be running
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

    private static async Task<string?> ReadCurrentValueAsync(string domain, string key, CancellationToken ct)
    {
        // Validate inputs to prevent shell injection
        if (!IsValidDomain(domain) || !IsValidKey(key)) return null;

        var result = await ProcessRunner.RunAsync("defaults", $"read {domain} {key}", ct);
        if (!result.Success) return null;
        return result.Stdout.Trim();
    }

    private static async Task<bool> ApplyTweakAsync(DefaultsTweak tweak, CancellationToken ct)
    {
        if (!IsValidDomain(tweak.Domain) || !IsValidKey(tweak.Key)) return false;
        if (!IsValidType(tweak.Type)) return false;
        if (!IsValidValue(tweak.RecommendedValue)) return false;

        var args = $"write {tweak.Domain} {tweak.Key} -{tweak.Type} {tweak.RecommendedValue}";
        var result = await ProcessRunner.RunAsync("defaults", args, ct);
        return result.Success;
    }

    private static void TrackAffectedApp(string domain, HashSet<string> affected)
    {
        // Map domain to app name to killall
        if (domain.StartsWith("com.apple.finder", StringComparison.OrdinalIgnoreCase))
            affected.Add("Finder");
        else if (domain.StartsWith("com.apple.dock", StringComparison.OrdinalIgnoreCase))
            affected.Add("Dock");
        else if (domain.StartsWith("com.apple.screencapture", StringComparison.OrdinalIgnoreCase))
            affected.Add("SystemUIServer");
        // NSGlobalDomain and desktopservices may not need app restart
    }

    private static bool IsValidDomain(string s) =>
        !string.IsNullOrWhiteSpace(s) && s.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_');

    private static bool IsValidKey(string s) =>
        !string.IsNullOrWhiteSpace(s) && s.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_');

    private static bool IsValidType(string s) =>
        s is "bool" or "int" or "float" or "string" or "array" or "dict";

    private static bool IsValidValue(string s) =>
        !string.IsNullOrWhiteSpace(s) && s.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_');
}

[SupportedOSPlatform("macos")]
public static class DefaultsOptimizerRegistration
{
    public static IServiceCollection AddDefaultsOptimizerModule(this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, DefaultsOptimizerModule>();
}
