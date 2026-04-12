using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Application.Shared;
using AuraCore.Domain.Enums;
using AuraCore.Module.XcodeCleaner.Models;

namespace AuraCore.Module.XcodeCleaner;

public sealed class XcodeCleanerModule : IOptimizationModule
{
    public string Id => "xcode-cleaner";
    public string DisplayName => "Xcode Cleaner";
    public OptimizationCategory Category => OptimizationCategory.DiskCleanup;
    public RiskLevel Risk => RiskLevel.Low;
    public SupportedPlatform Platform => SupportedPlatform.MacOS;

    public XcodeCleanerReport? LastReport { get; private set; }

    // Category definitions: Id, DisplayName, RelativePath
    private static readonly (string Id, string Name, string RelPath)[] CategoryDefs = new[]
    {
        ("derived-data", "Derived Data", "Library/Developer/Xcode/DerivedData"),
        ("archives", "Archives", "Library/Developer/Xcode/Archives"),
        ("simulator-caches", "Simulator Caches", "Library/Developer/CoreSimulator/Caches"),
        ("simulator-devices", "Simulator Devices", "Library/Developer/CoreSimulator/Devices"),
        ("xcode-cache", "Xcode Cache", "Library/Caches/com.apple.dt.Xcode"),
        ("ios-device-support", "iOS Device Support", "Library/Developer/Xcode/iOS DeviceSupport"),
        ("watchos-device-support", "watchOS Device Support", "Library/Developer/Xcode/watchOS DeviceSupport"),
        ("tvos-device-support", "tvOS Device Support", "Library/Developer/Xcode/tvOS DeviceSupport"),
    };

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsMacOS())
            return new ScanResult(Id, false, 0, 0, $"{DisplayName} is macOS-only.");

        try
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(home))
            {
                LastReport = XcodeCleanerReport.None();
                return new ScanResult(Id, false, 0, 0, "Cannot determine user home directory");
            }

            var categories = new List<XcodeCacheCategory>();
            long totalBytes = 0;
            int totalItems = 0;

            foreach (var def in CategoryDefs)
            {
                ct.ThrowIfCancellationRequested();
                var fullPath = Path.Combine(home, def.RelPath);
                var info = await MeasureCategoryAsync(def.Id, def.Name, fullPath, ct);
                categories.Add(info);
                if (info.Exists)
                {
                    totalBytes += info.SizeBytes;
                    totalItems += info.ItemCount;
                }
            }

            // Xcode "installed" = any of the categories exist
            var xcodeInstalled = categories.Any(c => c.Exists);
            var report = new XcodeCleanerReport(xcodeInstalled, home, categories, totalBytes);
            LastReport = report;

            return new ScanResult(Id, true, totalItems, totalBytes);
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
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(home))
                return new OptimizationResult(Id, operationId, false, 0, 0, DateTime.UtcNow - start);

            var items = plan.SelectedItemIds?.ToList() ?? new List<string>();
            int totalSteps = Math.Max(items.Count, 1);

            for (int i = 0; i < items.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var itemId = items[i];
                progress?.Report(new TaskProgress(Id, (double)i / totalSteps * 100, $"Cleaning {itemId}..."));

                long itemFreed = 0;
                bool ok = false;

                if (itemId == "all")
                {
                    // Clean everything except archives and device-support (safer)
                    foreach (var def in CategoryDefs)
                    {
                        if (def.Id is "archives" or "ios-device-support" or "watchos-device-support" or "tvos-device-support")
                            continue; // skip risky
                        var path = Path.Combine(home, def.RelPath);
                        itemFreed += await SafeDeleteContentsAsync(path, ct);
                    }
                    // Also run simctl to clean unavailable simulators
                    await ProcessRunner.RunAsync("xcrun", "simctl delete unavailable", ct, timeoutSeconds: 120);
                    ok = true;
                }
                else if (itemId == "unavailable-simulators")
                {
                    var result = await ProcessRunner.RunAsync("xcrun", "simctl delete unavailable", ct, timeoutSeconds: 120);
                    ok = result.Success;
                }
                else
                {
                    // Specific category id
                    var def = CategoryDefs.FirstOrDefault(d => d.Id == itemId);
                    if (!string.IsNullOrEmpty(def.Id))
                    {
                        var path = Path.Combine(home, def.RelPath);
                        itemFreed = await SafeDeleteContentsAsync(path, ct);
                        ok = true;
                    }
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

    private static async Task<XcodeCacheCategory> MeasureCategoryAsync(string id, string name, string path, CancellationToken ct)
    {
        if (!Directory.Exists(path))
            return new XcodeCacheCategory(id, name, path, 0, 0, null, Exists: false);

        // Size via du -sk
        long sizeBytes = 0;
        var duResult = await ProcessRunner.RunAsync("/bin/sh", $"-c \"du -sk '{path}' 2>/dev/null | awk '{{print $1}}'\"", ct);
        if (duResult.Success && long.TryParse(duResult.Stdout.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var kb))
            sizeBytes = kb * 1024;

        // Item count (immediate subdirs) + oldest
        int itemCount = 0;
        DateTime? oldest = null;
        try
        {
            var entries = Directory.EnumerateFileSystemEntries(path, "*", SearchOption.TopDirectoryOnly);
            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();
                itemCount++;
                try
                {
                    var writeTime = File.GetLastWriteTimeUtc(entry);
                    if (oldest == null || writeTime < oldest)
                        oldest = writeTime;
                }
                catch { /* skip inaccessible */ }
            }
        }
        catch { /* skip inaccessible dir */ }

        return new XcodeCacheCategory(id, name, path, sizeBytes, itemCount, oldest, Exists: true);
    }

    private static async Task<long> SafeDeleteContentsAsync(string dirPath, CancellationToken ct)
    {
        if (!Directory.Exists(dirPath)) return 0;

        // Measure before
        var before = await MeasureDirectoryBytesAsync(dirPath, ct);

        // Delete contents (NOT the directory itself)
        try
        {
            foreach (var subDir in Directory.EnumerateDirectories(dirPath))
            {
                ct.ThrowIfCancellationRequested();
                try { Directory.Delete(subDir, recursive: true); }
                catch { /* skip locked/permission denied */ }
            }
            foreach (var file in Directory.EnumerateFiles(dirPath))
            {
                ct.ThrowIfCancellationRequested();
                try { File.Delete(file); }
                catch { /* skip */ }
            }
        }
        catch { /* top-level skip */ }

        var after = await MeasureDirectoryBytesAsync(dirPath, ct);
        var delta = before - after;
        return delta > 0 ? delta : 0;
    }

    private static async Task<long> MeasureDirectoryBytesAsync(string path, CancellationToken ct)
    {
        if (!Directory.Exists(path)) return 0;
        var result = await ProcessRunner.RunAsync("/bin/sh", $"-c \"du -sk '{path}' 2>/dev/null | awk '{{print $1}}'\"", ct);
        if (!result.Success) return 0;
        if (long.TryParse(result.Stdout.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var kb))
            return kb * 1024;
        return 0;
    }
}

public static class XcodeCleanerRegistration
{
    public static IServiceCollection AddXcodeCleanerModule(this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, XcodeCleanerModule>();
}
