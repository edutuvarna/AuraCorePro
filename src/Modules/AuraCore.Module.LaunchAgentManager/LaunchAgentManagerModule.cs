using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Application.Shared;
using AuraCore.Domain.Enums;
using AuraCore.Module.LaunchAgentManager.Models;

namespace AuraCore.Module.LaunchAgentManager;

public sealed class LaunchAgentManagerModule : IOptimizationModule
{
    public string Id => "launch-agent-manager";
    public string DisplayName => "Launch Agent Manager";
    public OptimizationCategory Category => OptimizationCategory.SystemHealth;
    public RiskLevel Risk => RiskLevel.Medium;
    public SupportedPlatform Platform => SupportedPlatform.MacOS;

    public LaunchAgentReport? LastReport { get; private set; }

    // Known bloatware prefixes - user must explicitly opt-in to disable any of these.
    private static readonly string[] BloatwarePrefixes =
    {
        "com.adobe.",
        "com.microsoft.update",
        "com.google.keystone",
        "com.oracle.",
    };

    private static readonly HashSet<string> AllowedActions = new()
    {
        "unload", "disable", "enable"
    };

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsMacOS())
            return new ScanResult(Id, false, 0, 0, $"{DisplayName} is macOS-only.");

        try
        {
            if (!await ProcessRunner.CommandExistsAsync("launchctl", ct))
            {
                LastReport = LaunchAgentReport.None();
                return new ScanResult(Id, false, 0, 0, "launchctl not available");
            }

            // Get currently loaded agents via 'launchctl list'
            var loadedLabels = await GetLoadedLabelsAsync(ct);

            var agents = new List<LaunchAgentInfo>();
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (!string.IsNullOrEmpty(home))
            {
                agents.AddRange(EnumeratePlists(
                    Path.Combine(home, "Library", "LaunchAgents"),
                    LaunchAgentLocation.UserAgent,
                    loadedLabels));
            }

            agents.AddRange(EnumeratePlists(
                "/Library/LaunchAgents",
                LaunchAgentLocation.SystemUserAgent,
                loadedLabels));

            agents.AddRange(EnumeratePlists(
                "/Library/LaunchDaemons",
                LaunchAgentLocation.SystemDaemon,
                loadedLabels));

            // Note: /System/Library/LaunchAgents/ is intentionally NOT enumerated -
            // Apple-provided agents are read-only (SIP-protected) and must never be touched.

            int loaded = agents.Count(a => a.IsLoaded);
            int bloatware = agents.Count(a => a.IsBloatware);

            var report = new LaunchAgentReport(
                Agents: agents,
                TotalCount: agents.Count,
                LoadedCount: loaded,
                BloatwareCount: bloatware,
                IsAvailable: true);

            LastReport = report;
            return new ScanResult(Id, true, agents.Count, 0);
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
            if (!await ProcessRunner.CommandExistsAsync("launchctl", ct))
                return new OptimizationResult(Id, operationId, false, 0, 0, DateTime.UtcNow - start);

            // Ensure we have LastReport so we can resolve plist paths and locations
            if (LastReport == null)
                await ScanAsync(new ScanOptions(), ct);

            var agentsByLabel = LastReport?.Agents
                .GroupBy(a => a.Label)
                .ToDictionary(g => g.Key, g => g.First())
                ?? new Dictionary<string, LaunchAgentInfo>();

            var items = plan.SelectedItemIds?.ToList() ?? new List<string>();
            int totalSteps = Math.Max(items.Count, 1);

            for (int i = 0; i < items.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var itemId = items[i];
                progress?.Report(new TaskProgress(Id, (double)i / totalSteps * 100, $"Processing {itemId}..."));

                // Parse "action:label" format
                var colonIdx = itemId.IndexOf(':');
                if (colonIdx < 0) continue;

                var action = itemId[..colonIdx].ToLowerInvariant();
                var label = itemId[(colonIdx + 1)..];

                if (!AllowedActions.Contains(action)) continue;
                if (string.IsNullOrWhiteSpace(label)) continue;

                // Strict label sanitization - only standard bundle identifier chars allowed.
                // Rejects shell metacharacters, spaces, quotes, semicolons, etc.
                if (!label.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_'))
                    continue;

                // Resolve the agent from the scan report
                if (!agentsByLabel.TryGetValue(label, out var info)) continue;

                // Never operate on Apple system agents (SIP-protected, would fail anyway)
                if (info.Location == LaunchAgentLocation.AppleAgent) continue;

                bool ok = false;
                switch (action)
                {
                    case "unload":
                    {
                        // unload takes the plist file path directly
                        var r = await ProcessRunner.RunAsync(
                            "launchctl",
                            $"unload \"{info.PlistPath}\"",
                            ct,
                            timeoutSeconds: 60);
                        ok = r.Success;
                        break;
                    }
                    case "disable":
                    {
                        var target = BuildDomainTarget(info.Location, label);
                        // Wrap in /bin/sh -c so $(id -u) expands for user-scoped agents
                        var r = await ProcessRunner.RunAsync(
                            "/bin/sh",
                            $"-c \"launchctl disable {target}\"",
                            ct,
                            timeoutSeconds: 60);
                        ok = r.Success;
                        break;
                    }
                    case "enable":
                    {
                        var target = BuildDomainTarget(info.Location, label);
                        var r = await ProcessRunner.RunAsync(
                            "/bin/sh",
                            $"-c \"launchctl enable {target}\"",
                            ct,
                            timeoutSeconds: 60);
                        ok = r.Success;
                        break;
                    }
                }

                if (ok) processed++;
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
    /// Build the launchctl domain target string for disable/enable operations.
    /// User agents use gui/&lt;uid&gt;/&lt;label&gt;; system daemons use system/&lt;label&gt;.
    /// </summary>
    private static string BuildDomainTarget(LaunchAgentLocation location, string label)
    {
        return location == LaunchAgentLocation.SystemDaemon
            ? $"system/{label}"
            : $"gui/$(id -u)/{label}";
    }

    private static async Task<HashSet<string>> GetLoadedLabelsAsync(CancellationToken ct)
    {
        var labels = new HashSet<string>();
        var result = await ProcessRunner.RunAsync("launchctl", "list", ct);
        if (!result.Success) return labels;

        // Output format: PID<TAB>Status<TAB>Label (first line is a header)
        bool headerSkipped = false;
        foreach (var line in result.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!headerSkipped)
            {
                headerSkipped = true;
                if (line.Contains("PID") && line.Contains("Label")) continue;
            }

            var parts = line.Split(new[] { '\t', ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
                labels.Add(parts[2]);
        }
        return labels;
    }

    private static List<LaunchAgentInfo> EnumeratePlists(
        string dirPath,
        LaunchAgentLocation location,
        HashSet<string> loadedLabels)
    {
        var list = new List<LaunchAgentInfo>();
        if (!Directory.Exists(dirPath)) return list;

        try
        {
            var files = Directory.EnumerateFiles(dirPath, "*.plist", SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                var filename = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrEmpty(filename)) continue;

                var isLoaded = loadedLabels.Contains(filename);
                var isBloatware = BloatwarePrefixes.Any(p =>
                    filename.StartsWith(p, StringComparison.OrdinalIgnoreCase));

                string? recommendation = null;
                if (isBloatware)
                    recommendation = "Known bloatware - consider disabling";

                list.Add(new LaunchAgentInfo(
                    Label: filename,
                    PlistPath: file,
                    Location: location,
                    IsLoaded: isLoaded,
                    IsBloatware: isBloatware,
                    Recommendation: recommendation));
            }
        }
        catch
        {
            // Permission denied or IO failure - skip this directory silently
        }

        return list;
    }
}

public static class LaunchAgentManagerRegistration
{
    public static IServiceCollection AddLaunchAgentManagerModule(this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, LaunchAgentManagerModule>();
}
