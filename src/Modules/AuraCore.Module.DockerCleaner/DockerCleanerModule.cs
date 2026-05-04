using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Application.Shared;
using AuraCore.Domain.Enums;
using AuraCore.Module.DockerCleaner.Models;

namespace AuraCore.Module.DockerCleaner;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class DockerCleanerModule : IOptimizationModule
{
    public string Id => "docker-cleaner";
    public string DisplayName => "Docker Cleaner";
    public OptimizationCategory Category => OptimizationCategory.DiskCleanup;
    public RiskLevel Risk => RiskLevel.Low;
    public SupportedPlatform Platform => SupportedPlatform.Linux | SupportedPlatform.MacOS;

    public DockerReport? LastReport { get; private set; }

    public async Task<ModuleAvailability> CheckRuntimeAvailabilityAsync(CancellationToken ct = default)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return ModuleAvailability.WrongPlatform(SupportedPlatform.Linux | SupportedPlatform.MacOS);

        if (!await ProcessRunner.CommandExistsAsync("docker", ct))
            return ModuleAvailability.ToolNotInstalled("docker",
                "https://docs.docker.com/engine/install/");

        return ModuleAvailability.Available;
    }

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        if (!IsSupportedPlatform())
            return new ScanResult(Id, false, 0, 0, $"{DisplayName} requires Linux or macOS.");

        try
        {
            // Check docker availability via 'docker version'
            var versionResult = await ProcessRunner.RunAsync("docker", "version --format \"{{.Server.Version}}\"", ct);
            if (!versionResult.Success)
            {
                LastReport = DockerReport.None();
                return new ScanResult(Id, false, 0, 0, "Docker daemon not running or docker CLI not installed");
            }

            var dockerVersion = versionResult.Stdout.Trim();

            // Get disk usage via 'docker system df --format "{{json .}}"'
            var dfResult = await ProcessRunner.RunAsync("docker", "system df --format \"{{json .}}\"", ct);
            if (!dfResult.Success)
            {
                LastReport = DockerReport.None();
                return new ScanResult(Id, false, 0, 0, "Failed to query docker system df");
            }

            // Parse counts via piped commands (sh -c)
            int totalContainers = await CountAsync("ps -a -q", ct);
            int stoppedContainers = await CountAsync("ps -a -f status=exited -f status=dead -q", ct);
            int danglingImages = await CountAsync("images -q --filter \\\"dangling=true\\\"", ct);
            int unusedVolumes = await CountAsync("volume ls -q -f dangling=true", ct);

            // Parse system df output - each line is JSON with Type, TotalCount, Active, Size, Reclaimable
            long imagesBytes = 0, volumesBytes = 0, buildCacheBytes = 0, totalReclaimable = 0;
            // Phase 4.3.3 additive: per-category reclaimable so UI can compute "safe cleanup" (non-volume) savings.
            long imagesReclaimable = 0, containersReclaimable = 0, volumesReclaimable = 0, buildCacheReclaimable = 0;
            foreach (var line in dfResult.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    using var doc = JsonDocument.Parse(line.Trim());
                    var root = doc.RootElement;
                    var type = root.TryGetProperty("Type", out var t) ? t.GetString() : "";
                    var reclaimable = root.TryGetProperty("Reclaimable", out var r) ? r.GetString() ?? "" : "";
                    var size = root.TryGetProperty("Size", out var s) ? s.GetString() ?? "" : "";

                    long reclaimableBytes = ParseDockerSize(reclaimable);
                    long sizeBytes = ParseDockerSize(size);

                    switch (type)
                    {
                        case "Images":
                            imagesBytes = sizeBytes;
                            imagesReclaimable = reclaimableBytes;
                            totalReclaimable += reclaimableBytes;
                            break;
                        case "Containers":
                            containersReclaimable = reclaimableBytes;
                            totalReclaimable += reclaimableBytes;
                            break;
                        case "Local Volumes":
                            volumesBytes = sizeBytes;
                            volumesReclaimable = reclaimableBytes;
                            totalReclaimable += reclaimableBytes;
                            break;
                        case "Build Cache":
                            buildCacheBytes = sizeBytes;
                            buildCacheReclaimable = reclaimableBytes;
                            totalReclaimable += reclaimableBytes;
                            break;
                    }
                }
                catch (JsonException) { /* skip malformed lines */ }
            }

            var report = new DockerReport(
                DockerAvailable: true,
                DockerVersion: dockerVersion,
                TotalContainers: totalContainers,
                StoppedContainers: stoppedContainers,
                DanglingImages: danglingImages,
                UnusedVolumes: unusedVolumes,
                ImagesTotalBytes: imagesBytes,
                VolumesTotalBytes: volumesBytes,
                BuildCacheBytes: buildCacheBytes,
                TotalReclaimableBytes: totalReclaimable,
                ImagesReclaimableBytes: imagesReclaimable,
                ContainersReclaimableBytes: containersReclaimable,
                VolumesReclaimableBytes: volumesReclaimable,
                BuildCacheReclaimableBytes: buildCacheReclaimable);

            LastReport = report;
            int itemCount = stoppedContainers + danglingImages + unusedVolumes;
            return new ScanResult(Id, true, itemCount, totalReclaimable);
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

        if (!IsSupportedPlatform())
            return new OptimizationResult(Id, operationId, false, 0, 0, TimeSpan.Zero);

        try
        {
            // Verify docker is available before doing any work
            var versionResult = await ProcessRunner.RunAsync("docker", "version --format \"{{.Server.Version}}\"", ct);
            if (!versionResult.Success)
                return new OptimizationResult(Id, operationId, false, 0, 0, DateTime.UtcNow - start);

            var items = plan.SelectedItemIds?.ToList() ?? new List<string>();
            int totalSteps = Math.Max(items.Count, 1);

            for (int i = 0; i < items.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var itemId = items[i];
                progress?.Report(new TaskProgress(Id, (double)i / totalSteps * 100, $"Running {itemId}..."));

                var args = itemId switch
                {
                    "prune-containers"     => "container prune -f",
                    "prune-dangling-images"=> "image prune -f",
                    "prune-all-images"     => "image prune -a -f",
                    "prune-volumes"        => "volume prune -a -f",
                    "prune-build-cache"    => "builder prune -f",
                    "prune-all-build-cache"=> "builder prune -a -f",
                    "prune-system"         => "system prune -f",
                    "prune-system-all"     => "system prune -a --volumes -f",
                    _ => ""
                };

                if (string.IsNullOrEmpty(args)) continue;

                var result = await ProcessRunner.RunAsync("docker", args, ct, timeoutSeconds: 300);
                if (result.Success)
                {
                    processed++;
                    freed += ParseReclaimedSpace(result.Stdout);
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

    private bool IsSupportedPlatform()
    {
        return (Platform.HasFlag(SupportedPlatform.Linux) && OperatingSystem.IsLinux())
            || (Platform.HasFlag(SupportedPlatform.MacOS) && OperatingSystem.IsMacOS())
            || (Platform.HasFlag(SupportedPlatform.Windows) && OperatingSystem.IsWindows());
    }

    private static async Task<int> CountAsync(string args, CancellationToken ct)
    {
        // Wrap in sh -c to pipe through wc -l. Discard stderr to keep counts clean
        // when docker returns warnings (e.g., "no images found").
        var cmd = $"docker {args} 2>/dev/null | wc -l";
        var result = await ProcessRunner.RunAsync("/bin/sh", $"-c \"{cmd}\"", ct);
        return int.TryParse(result.Stdout.Trim(), out var count) ? count : 0;
    }

    private static long ParseDockerSize(string size)
    {
        if (string.IsNullOrWhiteSpace(size)) return 0;
        // size format: "1.234GB", "567MB", "12.3kB", etc. (sometimes with extra "(XX%)" suffix for reclaimable)
        var match = Regex.Match(size, @"([\d.]+)\s*([KMGT]?B)", RegexOptions.IgnoreCase);
        if (!match.Success) return 0;

        if (!double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return 0;

        var unit = match.Groups[2].Value.ToUpperInvariant();
        long multiplier = unit switch
        {
            "B"  => 1L,
            "KB" => 1024L,
            "MB" => 1024L * 1024,
            "GB" => 1024L * 1024 * 1024,
            "TB" => 1024L * 1024 * 1024 * 1024,
            _    => 1L
        };
        return (long)(value * multiplier);
    }

    private static long ParseReclaimedSpace(string output)
    {
        // Expected format: "Total reclaimed space: 123.4MB" or "Total reclaimed space: 1.5 GB"
        var match = Regex.Match(output, @"Total reclaimed space:\s*([\d.]+)\s*([KMGT]?B)", RegexOptions.IgnoreCase);
        if (!match.Success) return 0;

        if (!double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return 0;

        var unit = match.Groups[2].Value.ToUpperInvariant();
        long multiplier = unit switch
        {
            "B"  => 1L,
            "KB" => 1024L,
            "MB" => 1024L * 1024,
            "GB" => 1024L * 1024 * 1024,
            "TB" => 1024L * 1024 * 1024 * 1024,
            _    => 1L
        };
        return (long)(value * multiplier);
    }
}

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public static class DockerCleanerRegistration
{
    public static IServiceCollection AddDockerCleanerModule(this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, DockerCleanerModule>();
}
