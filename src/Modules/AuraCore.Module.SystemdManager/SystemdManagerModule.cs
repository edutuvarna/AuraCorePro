using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.Application.Shared;
using AuraCore.Domain.Enums;
using AuraCore.Module.SystemdManager.Models;

namespace AuraCore.Module.SystemdManager;

[SupportedOSPlatform("linux")]
public sealed class SystemdManagerModule : IOptimizationModule, IOperationModule
{
    public string Id => "systemd-manager";
    public string DisplayName => "Systemd Manager";
    public OptimizationCategory Category => OptimizationCategory.SystemHealth;
    public RiskLevel Risk => RiskLevel.Medium;
    public SupportedPlatform Platform => SupportedPlatform.Linux;

    public SystemdReport? LastReport { get; private set; }

    public async Task<ModuleAvailability> CheckRuntimeAvailabilityAsync(CancellationToken ct = default)
    {
        if (!OperatingSystem.IsLinux())
            return ModuleAvailability.WrongPlatform(SupportedPlatform.Linux);

        if (!await ProcessRunner.CommandExistsAsync("systemctl", ct))
            return ModuleAvailability.ToolNotInstalled("systemctl",
                "Switch to a systemd-based Linux distribution.");

        return ModuleAvailability.Available;
    }

    // Known services often safe to disable/mask (user must opt-in)
    private static readonly Dictionary<string, string> KnownBloatware = new()
    {
        ["ModemManager.service"] = "Modem Manager - only needed if using dial-up/mobile modem",
        ["snapd.service"] = "Snap daemon - only needed if using snap packages",
        ["cups.service"] = "CUPS print service - only needed if using a printer",
        ["cups-browsed.service"] = "CUPS network printer discovery",
        ["bluetooth.service"] = "Bluetooth - only needed if using Bluetooth devices",
        ["avahi-daemon.service"] = "Zeroconf service discovery - rarely needed",
    };

    private static readonly HashSet<string> AllowedActions = new()
    {
        "start", "stop", "restart", "enable", "disable", "mask", "unmask"
    };

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsLinux())
            return new ScanResult(Id, false, 0, 0, $"{DisplayName} is Linux-only.");

        try
        {
            if (!await ProcessRunner.CommandExistsAsync("systemctl", ct))
            {
                LastReport = SystemdReport.None();
                return new ScanResult(Id, false, 0, 0, "systemctl not available (not a systemd system)");
            }

            var services = await ListServicesAsync(ct);
            var running = services.Count(s => s.Active == "active" && s.Sub == "running");
            var failed = services.Count(s => s.IsFailed);
            var recommendations = services.Count(s => !string.IsNullOrEmpty(s.Recommendation));

            var report = new SystemdReport(
                Services: services,
                TotalCount: services.Count,
                RunningCount: running,
                FailedCount: failed,
                RecommendationCount: recommendations,
                IsAvailable: true);

            LastReport = report;
            return new ScanResult(Id, true, services.Count, 0);
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
            if (!await ProcessRunner.CommandExistsAsync("systemctl", ct))
                return new OptimizationResult(Id, operationId, false, 0, 0, DateTime.UtcNow - start);

            var items = plan.SelectedItemIds?.ToList() ?? new List<string>();
            int totalSteps = Math.Max(items.Count, 1);

            for (int i = 0; i < items.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var itemId = items[i];
                progress?.Report(new TaskProgress(Id, (double)i / totalSteps * 100, $"Processing {itemId}..."));

                // Parse "action:service-name" format
                var colonIdx = itemId.IndexOf(':');
                if (colonIdx < 0) continue;

                var action = itemId[..colonIdx].ToLowerInvariant();
                var serviceName = itemId[(colonIdx + 1)..];

                if (!AllowedActions.Contains(action)) continue;
                if (string.IsNullOrWhiteSpace(serviceName)) continue;

                // Sanitize service name (allow only valid systemd unit-name chars)
                if (!serviceName.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_' || c == '@')) continue;

                var result = await ProcessRunner.RunAsync("systemctl", $"{action} {serviceName}", ct, timeoutSeconds: 60);
                if (result.Success) processed++;
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

    /// <summary>
    /// Phase 6.17 Wave F: rich-result wrapper with privilege guard for systemctl service-state changes.
    /// Linux-only — Windows/macOS short-circuit with Failed result.
    /// </summary>
    public async Task<OperationResult> RunOperationAsync(
        OptimizationPlan plan,
        IPrivilegedActionGuard guard,
        IProgress<TaskProgress>? progress = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        if (!OperatingSystem.IsLinux())
        {
            sw.Stop();
            return OperationResult.Failed("Systemd Manager is Linux-only.", sw.Elapsed);
        }

        if (!await guard.TryGuardAsync(
                actionDescription: "Modify systemd service state (enable/disable/mask)",
                remediationCommandOverride: null,
                ct: ct))
        {
            sw.Stop();
            return OperationResult.Skipped(
                "Privilege helper required",
                "sudo bash /opt/auracorepro/install-privhelper.sh");
        }

        try
        {
            var legacy = await OptimizeAsync(plan, progress, ct);
            sw.Stop();
            if (!legacy.Success)
                return OperationResult.Failed("Service state change failed", sw.Elapsed);
            // Service ops don't free bytes — pass 0.
            return OperationResult.Success(0, legacy.ItemsProcessed, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return OperationResult.Failed($"{ex.GetType().Name}: {ex.Message}", sw.Elapsed);
        }
    }

    public Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default) => Task.FromResult(false);
    public Task RollbackAsync(string operationId, CancellationToken ct = default) => Task.CompletedTask;

    // ---- Helpers ----

    private static async Task<List<SystemdServiceInfo>> ListServicesAsync(CancellationToken ct)
    {
        var services = new List<SystemdServiceInfo>();
        var result = await ProcessRunner.RunAsync("systemctl",
            "list-units --type=service --all --no-pager --no-legend --plain", ct, timeoutSeconds: 60);
        if (!result.Success) return services;

        foreach (var rawLine in result.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // Columns (tab or multi-space separated): UNIT LOAD ACTIVE SUB DESCRIPTION
            var parts = line.Split(new[] { ' ', '\t' }, 5, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) continue;

            var unit = parts[0];
            var load = parts[1];
            var active = parts[2];
            var sub = parts[3];
            var description = parts.Length >= 5 ? parts[4] : "";

            var isFailed = active == "failed";
            string? recommendation = null;
            if (KnownBloatware.TryGetValue(unit, out var reason))
                recommendation = reason;
            else if (isFailed)
                recommendation = "Failed service - consider restarting or investigating logs";

            // is-enabled status is intentionally not queried per-service - checking each
            // would require N extra process invocations. Consumers can query on-demand via
            // systemctl is-enabled <unit> when displaying per-row detail.
            services.Add(new SystemdServiceInfo(
                Unit: unit,
                Load: load,
                Active: active,
                Sub: sub,
                Description: description,
                IsEnabled: false,
                IsFailed: isFailed,
                Recommendation: recommendation));
        }

        return services;
    }
}

[SupportedOSPlatform("linux")]
public static class SystemdManagerRegistration
{
    public static IServiceCollection AddSystemdManagerModule(this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, SystemdManagerModule>();
}
