using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using AuraCore.Module.JunkCleaner.Models;

namespace AuraCore.Module.JunkCleaner;

public sealed class JunkCleanerModule : IOptimizationModule
{
    public string Id => "junk-cleaner";
    public string DisplayName => "AI Junk Cleaner";
    public OptimizationCategory Category => OptimizationCategory.DiskCleanup;
    public RiskLevel Risk => RiskLevel.Medium;

    public JunkScanReport? LastReport { get; private set; }

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        var categories = new List<JunkCategory>();

        await Task.Run(() =>
        {
            // 1. Windows Temp — all files
            categories.Add(ScanDirectory(
                "Windows Temp",
                "Temporary files created by Windows and applications",
                Path.GetTempPath(),
                scanAll: true));

            // 2. Prefetch
            var prefetch = @"C:\Windows\Prefetch";
            if (Directory.Exists(prefetch))
            {
                categories.Add(ScanDirectory(
                    "Prefetch Cache",
                    "Application launch optimization cache",
                    prefetch,
                    extensions: new[] { ".pf" }));
            }

            // 3. Windows Logs
            var winLogs = @"C:\Windows\Logs";
            if (Directory.Exists(winLogs))
            {
                categories.Add(ScanDirectory(
                    "Windows Logs",
                    "System and application log files",
                    winLogs,
                    extensions: new[] { ".log", ".etl" }));
            }

            // 4. Chrome Cache
            var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var chromeCache = Path.Combine(localApp, @"Google\Chrome\User Data\Default\Cache\Cache_Data");
            if (Directory.Exists(chromeCache))
            {
                categories.Add(ScanDirectory("Chrome Cache", "Google Chrome browser cache", chromeCache, scanAll: true));
            }

            // 5. Edge Cache
            var edgeCache = Path.Combine(localApp, @"Microsoft\Edge\User Data\Default\Cache\Cache_Data");
            if (Directory.Exists(edgeCache))
            {
                categories.Add(ScanDirectory("Edge Cache", "Microsoft Edge browser cache", edgeCache, scanAll: true));
            }

            // 6. Thumbnail Cache
            var thumbCache = Path.Combine(localApp, @"Microsoft\Windows\Explorer");
            if (Directory.Exists(thumbCache))
            {
                categories.Add(ScanDirectory(
                    "Thumbnail Cache",
                    "Windows Explorer thumbnail database files",
                    thumbCache,
                    extensions: new[] { ".db" }));
            }

            // 7. Recent Shortcuts
            var recent = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
            if (Directory.Exists(recent))
            {
                categories.Add(ScanDirectory(
                    "Recent Shortcuts",
                    "Shortcuts to recently opened files",
                    recent,
                    extensions: new[] { ".lnk" }));
            }

            // 8. Recycle Bin
            categories.Add(ScanRecycleBin());

        }, ct);

        categories.RemoveAll(c => c.FileCount == 0);

        LastReport = new JunkScanReport { Categories = categories };
        return new ScanResult(Id, true, LastReport.TotalFiles, LastReport.TotalBytes);
    }

    public async Task<OptimizationResult> OptimizeAsync(
        OptimizationPlan plan, IProgress<TaskProgress>? progress = null, CancellationToken ct = default)
    {
        if (LastReport is null)
            return new OptimizationResult(Id, "", false, 0, 0, TimeSpan.Zero);

        var start = DateTime.UtcNow;
        long freedBytes = 0;
        int deleted = 0;
        int total = LastReport.TotalFiles;
        int processed = 0;

        await Task.Run(() =>
        {
            foreach (var category in LastReport.Categories)
            {
                // Skip Recycle Bin — don't delete $Recycle.Bin directly
                if (category.Name == "Recycle Bin") continue;

                foreach (var file in category.Files)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        if (File.Exists(file.FullPath))
                        {
                            File.Delete(file.FullPath);
                            freedBytes += file.SizeBytes;
                            deleted++;
                        }
                    }
                    catch { }

                    processed++;
                    progress?.Report(new TaskProgress(Id,
                        total > 0 ? (double)processed / total * 100 : 100,
                        $"Cleaning: {processed}/{total}"));
                }
            }
        }, ct);

        return new OptimizationResult(Id, Guid.NewGuid().ToString(), true, deleted, freedBytes, DateTime.UtcNow - start);
    }

    public Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default)
        => Task.FromResult(false);

    public Task RollbackAsync(string operationId, CancellationToken ct = default)
        => Task.CompletedTask;

    private static JunkCategory ScanDirectory(string name, string description, string path,
        string[]? extensions = null, bool scanAll = false)
    {
        var files = new List<JunkItem>();
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*.*", new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.System
            }))
            {
                try
                {
                    if (!scanAll && extensions is not null)
                    {
                        var ext = Path.GetExtension(file).ToLowerInvariant();
                        if (!extensions.Contains(ext)) continue;
                    }

                    var info = new FileInfo(file);
                    if (!info.Exists || info.Length == 0) continue;

                    files.Add(new JunkItem(file, info.Length, name, info.LastWriteTimeUtc));
                }
                catch { }
            }
        }
        catch { }

        return new JunkCategory { Name = name, Description = description, Files = files };
    }

    private static JunkCategory ScanRecycleBin()
    {
        long totalSize = 0;
        try
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
            {
                var recyclePath = Path.Combine(drive.Name, "$Recycle.Bin");
                if (!Directory.Exists(recyclePath)) continue;
                try
                {
                    foreach (var file in Directory.EnumerateFiles(recyclePath, "*.*", new EnumerationOptions
                    {
                        IgnoreInaccessible = true,
                        RecurseSubdirectories = true
                    }))
                    {
                        try { totalSize += new FileInfo(file).Length; } catch { }
                    }
                }
                catch { }
            }
        }
        catch { }

        var files = new List<JunkItem>();
        if (totalSize > 0)
            files.Add(new JunkItem("$Recycle.Bin", totalSize, "Recycle Bin", DateTimeOffset.UtcNow));

        return new JunkCategory { Name = "Recycle Bin", Description = "Deleted files in the Recycle Bin", Files = files };
    }
}

public static class JunkCleanerRegistration
{
    public static IServiceCollection AddJunkCleanerModule(
        this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, JunkCleanerModule>();
}
