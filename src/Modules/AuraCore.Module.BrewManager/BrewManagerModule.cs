using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Application.Shared;
using AuraCore.Domain.Enums;
using AuraCore.Module.BrewManager.Models;

namespace AuraCore.Module.BrewManager;

public sealed class BrewManagerModule : IOptimizationModule
{
    public string Id => "brew-manager";
    public string DisplayName => "Homebrew Manager";
    public OptimizationCategory Category => OptimizationCategory.ApplicationManagement;
    public RiskLevel Risk => RiskLevel.Low;
    public SupportedPlatform Platform => SupportedPlatform.MacOS;

    public BrewReport? LastReport { get; private set; }

    // Apple Silicon first, then Intel, then PATH
    private static readonly string[] BrewPaths = {
        "/opt/homebrew/bin/brew",
        "/usr/local/bin/brew"
    };

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsMacOS())
            return new ScanResult(Id, false, 0, 0, $"{DisplayName} is macOS-only.");

        try
        {
            var brewPath = await DetectBrewPathAsync(ct);
            if (string.IsNullOrEmpty(brewPath))
            {
                LastReport = BrewReport.None();
                return new ScanResult(Id, false, 0, 0, "Homebrew not found (checked /opt/homebrew, /usr/local, and PATH)");
            }

            var version = await GetBrewVersionAsync(brewPath, ct);
            var formulas = await ListPackagesAsync(brewPath, "list --formula", ct);
            var casks = await ListPackagesAsync(brewPath, "list --cask", ct);
            var outdated = await GetOutdatedAsync(brewPath, ct);
            var cacheBytes = await GetCacheBytesAsync(brewPath, ct);

            var report = new BrewReport(
                BrewInstalled: true,
                BrewPath: brewPath,
                BrewVersion: version,
                InstalledFormulas: formulas.Count,
                InstalledCasks: casks.Count,
                Outdated: outdated,
                CacheBytes: cacheBytes);

            LastReport = report;
            return new ScanResult(Id, true, outdated.Count, cacheBytes);
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

        if (!OperatingSystem.IsMacOS())
            return new OptimizationResult(Id, operationId, false, 0, 0, TimeSpan.Zero);

        try
        {
            var brewPath = await DetectBrewPathAsync(ct);
            if (string.IsNullOrEmpty(brewPath))
                return new OptimizationResult(Id, operationId, false, 0, 0, DateTime.UtcNow - start);

            var items = plan.SelectedItemIds?.ToList() ?? new List<string>();
            int totalSteps = Math.Max(items.Count, 1);

            for (int i = 0; i < items.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var itemId = items[i];
                progress?.Report(new TaskProgress(Id, (double)i / totalSteps * 100, $"Running brew {itemId}..."));

                bool ok = false;
                long itemFreed = 0;

                if (itemId == "cleanup")
                {
                    (ok, itemFreed) = await RunCleanupAsync(brewPath, ct);
                }
                else if (itemId == "upgrade-all")
                {
                    ok = (await ProcessRunner.RunAsync(brewPath, "upgrade", ct, timeoutSeconds: 600)).Success;
                }
                else if (itemId.StartsWith("upgrade:"))
                {
                    var pkg = itemId["upgrade:".Length..];
                    ok = (await ProcessRunner.RunAsync(brewPath, $"upgrade {pkg}", ct, timeoutSeconds: 600)).Success;
                }
                else if (itemId == "cleanup-and-upgrade")
                {
                    (ok, itemFreed) = await RunCleanupAsync(brewPath, ct);
                    var upgradeOk = (await ProcessRunner.RunAsync(brewPath, "upgrade", ct, timeoutSeconds: 600)).Success;
                    ok = ok && upgradeOk;
                }

                if (ok)
                {
                    processed++;
                    freed += itemFreed;
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

    private static async Task<string> DetectBrewPathAsync(CancellationToken ct)
    {
        foreach (var path in BrewPaths)
        {
            if (File.Exists(path))
                return path;
        }
        // Fallback to PATH lookup
        var which = await ProcessRunner.RunAsync("which", "brew", ct);
        if (which.Success && !string.IsNullOrWhiteSpace(which.Stdout))
            return which.Stdout.Trim();
        return "";
    }

    private static async Task<string> GetBrewVersionAsync(string brewPath, CancellationToken ct)
    {
        var result = await ProcessRunner.RunAsync(brewPath, "--version", ct);
        if (!result.Success) return "";
        // Output: "Homebrew 4.x.y\nHomebrew/homebrew-core (git revision ...; last commit ...)"
        var firstLine = result.Stdout.Split('\n').FirstOrDefault() ?? "";
        return firstLine.Trim();
    }

    private static async Task<List<string>> ListPackagesAsync(string brewPath, string args, CancellationToken ct)
    {
        var result = await ProcessRunner.RunAsync(brewPath, args, ct);
        if (!result.Success) return new List<string>();
        return result.Stdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static async Task<List<BrewPackageInfo>> GetOutdatedAsync(string brewPath, CancellationToken ct)
    {
        var outdated = new List<BrewPackageInfo>();

        // Formula
        var formulaResult = await ProcessRunner.RunAsync(brewPath, "outdated --formula --verbose", ct);
        if (formulaResult.Success)
        {
            foreach (var line in formulaResult.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                // Line format: "name current_version < latest_version" or "name current -> latest"
                var parts = line.Split(new[] { ' ', '<', '>' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                    outdated.Add(new BrewPackageInfo(parts[0], parts[1], parts[^1], IsCask: false));
                else if (parts.Length >= 1)
                    outdated.Add(new BrewPackageInfo(parts[0], "?", "?", IsCask: false));
            }
        }

        // Cask
        var caskResult = await ProcessRunner.RunAsync(brewPath, "outdated --cask --verbose", ct);
        if (caskResult.Success)
        {
            foreach (var line in caskResult.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = line.Split(new[] { ' ', '<', '>' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                    outdated.Add(new BrewPackageInfo(parts[0], parts[1], parts[^1], IsCask: true));
                else if (parts.Length >= 1)
                    outdated.Add(new BrewPackageInfo(parts[0], "?", "?", IsCask: true));
            }
        }

        return outdated;
    }

    private static async Task<long> GetCacheBytesAsync(string brewPath, CancellationToken ct)
    {
        var cacheResult = await ProcessRunner.RunAsync(brewPath, "--cache", ct);
        if (!cacheResult.Success) return 0;

        var cachePath = cacheResult.Stdout.Trim();
        if (string.IsNullOrEmpty(cachePath) || !Directory.Exists(cachePath)) return 0;

        // macOS default du uses 512-byte blocks. Use du -sk (kilobytes) then multiply by 1024.
        // Wrap in /bin/sh -c to allow the pipe to awk.
        var duResult = await ProcessRunner.RunAsync(
            "/bin/sh",
            $"-c \"du -sk '{cachePath}' 2>/dev/null | awk '{{print $1}}'\"",
            ct);
        if (!duResult.Success) return 0;

        if (long.TryParse(duResult.Stdout.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var kb))
            return kb * 1024;
        return 0;
    }

    private static async Task<(bool ok, long freedBytes)> RunCleanupAsync(string brewPath, CancellationToken ct)
    {
        var result = await ProcessRunner.RunAsync(brewPath, "cleanup --prune=all", ct, timeoutSeconds: 300);
        if (!result.Success) return (false, 0);

        // Parse "This operation has freed approximately X.YZ GB of disk space."
        var match = Regex.Match(result.Stdout, @"freed approximately ([\d.]+)\s*([KMGT]?B)", RegexOptions.IgnoreCase);
        if (!match.Success) return (true, 0);

        if (!double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return (true, 0);

        var unit = match.Groups[2].Value.ToUpperInvariant();
        long multiplier = unit switch
        {
            "B" => 1L,
            "KB" => 1024L,
            "MB" => 1024L * 1024,
            "GB" => 1024L * 1024 * 1024,
            "TB" => 1024L * 1024 * 1024 * 1024,
            _ => 1L
        };

        return (true, (long)(value * multiplier));
    }
}

public static class BrewManagerRegistration
{
    public static IServiceCollection AddBrewManagerModule(this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, BrewManagerModule>();
}
