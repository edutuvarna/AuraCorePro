using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.Application.Shared;
using AuraCore.Domain.Enums;
using AuraCore.Module.SpotlightManager.Models;

namespace AuraCore.Module.SpotlightManager;

[SupportedOSPlatform("macos")]
public sealed class SpotlightManagerModule : IOptimizationModule
{
    private readonly IShellCommandService _shell;

    public SpotlightManagerModule(IShellCommandService shell)
    {
        _shell = shell;
    }

    public string Id => "spotlight-manager";
    public string DisplayName => "Spotlight Manager";
    public OptimizationCategory Category => OptimizationCategory.SystemHealth;
    public RiskLevel Risk => RiskLevel.Low;
    public SupportedPlatform Platform => SupportedPlatform.MacOS;

    public SpotlightReport? LastReport { get; private set; }

    private static readonly HashSet<string> AllowedActions = new(StringComparer.Ordinal) { "disable", "enable", "rebuild" };

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsMacOS())
            return new ScanResult(Id, false, 0, 0, $"{DisplayName} is macOS-only.");

        try
        {
            if (!await ProcessRunner.CommandExistsAsync("mdutil", ct))
            {
                LastReport = SpotlightReport.None();
                return new ScanResult(Id, false, 0, 0, "mdutil not available");
            }

            var result = await ProcessRunner.RunAsync("mdutil", "-sa", ct);
            if (!result.Success)
            {
                LastReport = SpotlightReport.None();
                return new ScanResult(Id, false, 0, 0, "mdutil -sa failed");
            }

            var volumes = ParseMdutilStatus(result.Stdout);
            var enabled = volumes.Count(v => v.IndexingEnabled);
            var disabled = volumes.Count - enabled;

            var report = new SpotlightReport(
                Volumes: volumes,
                TotalVolumes: volumes.Count,
                EnabledCount: enabled,
                DisabledCount: disabled,
                IsAvailable: true);

            LastReport = report;
            return new ScanResult(Id, true, volumes.Count, 0);
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
            if (!await ProcessRunner.CommandExistsAsync("mdutil", ct))
                return new OptimizationResult(Id, operationId, false, 0, 0, DateTime.UtcNow - start);

            // Ensure LastReport is populated so we can validate user-supplied volume paths
            if (LastReport == null)
                await ScanAsync(new ScanOptions(), ct);

            var knownVolumes = new HashSet<string>(
                LastReport?.Volumes.Select(v => v.MountPoint) ?? Enumerable.Empty<string>(),
                StringComparer.Ordinal);

            var items = plan.SelectedItemIds?.ToList() ?? new List<string>();
            int totalSteps = Math.Max(items.Count, 1);

            for (int i = 0; i < items.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var itemId = items[i];
                progress?.Report(new TaskProgress(Id, (double)i / totalSteps * 100, $"Processing {itemId}..."));

                var colonIdx = itemId.IndexOf(':');
                if (colonIdx < 0) continue;

                var action = itemId[..colonIdx].ToLowerInvariant();
                var volume = itemId[(colonIdx + 1)..];

                if (!AllowedActions.Contains(action)) continue;
                if (string.IsNullOrWhiteSpace(volume)) continue;

                // Strict safety: volume MUST exactly match one from the scan report.
                if (!knownVolumes.Contains(volume)) continue;

                // Extra defense: reject shell metacharacters even if the path is in LastReport.
                if (ContainsShellMetacharacter(volume)) continue;

                // Build argv per daemon's SpotlightArgvValidator:
                // - disable: ["-i", "off", volume]
                // - enable: ["-i", "on", volume]
                // - rebuild: ["-E", volume]
                var argv = action switch
                {
                    "disable" => new[] { "-i", "off", volume },
                    "enable" => new[] { "-i", "on", volume },
                    "rebuild" => new[] { "-E", volume },
                    _ => null
                };
                if (argv is null) continue;

                // Use IShellCommandService with action id "spotlight".
                // Daemon hardcodes /usr/bin/mdutil per Task 27 SpotlightArgvValidator.
                var r = await _shell.RunPrivilegedAsync(
                    new PrivilegedCommand(
                        Id: "spotlight",
                        Executable: "mdutil",
                        Arguments: argv,
                        TimeoutSeconds: 120),
                    ct);
                if (r.Success) processed++;
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
    /// Parses output of "mdutil -sa".
    /// Typical format:
    ///   /Volumes/MyDisk:
    ///           Indexing enabled.
    ///   /System/Volumes/Data:
    ///           Indexing enabled.
    /// </summary>
    private static List<SpotlightVolumeInfo> ParseMdutilStatus(string output)
    {
        var volumes = new List<SpotlightVolumeInfo>();
        var lines = output.Split('\n');

        string? currentPath = null;
        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line)) continue;

            // New volume header line: starts at column 0 and ends with ':'
            if (!line.StartsWith('\t') && !line.StartsWith(' ') && line.EndsWith(':'))
            {
                currentPath = line[..^1];
                continue;
            }

            // Status line (typically indented). Accept "Indexing enabled" / "disabled".
            var trimmed = line.Trim();
            if (currentPath != null)
            {
                bool? enabled = null;
                if (trimmed.Contains("Indexing and searching disabled", StringComparison.OrdinalIgnoreCase))
                    enabled = false;
                else if (trimmed.Contains("Indexing enabled", StringComparison.OrdinalIgnoreCase))
                    enabled = true;
                else if (trimmed.Contains("Indexing disabled", StringComparison.OrdinalIgnoreCase))
                    enabled = false;

                if (enabled.HasValue)
                {
                    volumes.Add(new SpotlightVolumeInfo(
                        MountPoint: currentPath,
                        IndexingEnabled: enabled.Value,
                        IndexSizeBytes: 0));
                    currentPath = null;
                }
            }
        }

        return volumes;
    }

    /// <summary>
    /// Defensive check against command-injection characters. A legitimate mount
    /// point path from "mdutil -sa" should never contain these characters.
    /// </summary>
    private static bool ContainsShellMetacharacter(string input)
    {
        foreach (var ch in input)
        {
            if (ch is ';' or '|' or '`' or '$' or '&' or '<' or '>' or '\n' or '\r' or '\\' or '\0')
                return true;
        }
        return false;
    }
}

[SupportedOSPlatform("macos")]
public static class SpotlightManagerRegistration
{
    public static IServiceCollection AddSpotlightManagerModule(this IServiceCollection services)
    {
        services.AddSingleton<SpotlightManagerModule>();
        services.AddSingleton<IOptimizationModule>(sp => sp.GetRequiredService<SpotlightManagerModule>());
        return services;
    }
}
