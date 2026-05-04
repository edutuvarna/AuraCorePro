using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.Domain.Enums;
using AuraCore.Module.JunkCleaner.Models;

namespace AuraCore.Module.JunkCleaner;

public sealed class JunkCleanerModule : IOptimizationModule, IOperationModule
{
    public string Id => "junk-cleaner";
    public string DisplayName => "AI Junk Cleaner";
    public OptimizationCategory Category => OptimizationCategory.DiskCleanup;
    public RiskLevel Risk => RiskLevel.Medium;
    public SupportedPlatform Platform => SupportedPlatform.All;

    public JunkScanReport? LastReport { get; private set; }

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        var categories = new List<JunkCategory>();

        await Task.Run(() =>
        {
            // 1. System/User Temp — all platforms
            categories.Add(ScanDirectory(
                "System Temp",
                "Temporary files created by the OS and applications",
                Path.GetTempPath(),
                scanAll: true));

            if (OperatingSystem.IsWindows())
            {
                // 2. Prefetch (Windows only)
                var prefetch = @"C:\Windows\Prefetch";
                if (Directory.Exists(prefetch))
                    categories.Add(ScanDirectory("Prefetch Cache", "Application launch optimization cache", prefetch, extensions: new[] { ".pf" }));

                // 3. Windows Logs
                var winLogs = @"C:\Windows\Logs";
                if (Directory.Exists(winLogs))
                    categories.Add(ScanDirectory("Windows Logs", "System and application log files", winLogs, extensions: new[] { ".log", ".etl" }));

                // 4. Thumbnail Cache
                var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var thumbCache = Path.Combine(localApp, @"Microsoft\Windows\Explorer");
                if (Directory.Exists(thumbCache))
                    categories.Add(ScanDirectory("Thumbnail Cache", "Windows Explorer thumbnail database files", thumbCache, extensions: new[] { ".db" }));

                // 5. Recent Shortcuts
                var recent = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
                if (Directory.Exists(recent))
                    categories.Add(ScanDirectory("Recent Shortcuts", "Shortcuts to recently opened files", recent, extensions: new[] { ".lnk" }));

                // 6. Recycle Bin
                categories.Add(ScanRecycleBin());
            }

            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                // Linux/macOS: ~/.cache
                var userCache = Path.Combine(home, ".cache");
                if (Directory.Exists(userCache))
                    categories.Add(ScanDirectory("User Cache", "Application cache files (~/.cache)", userCache, scanAll: true));

                // Linux: /var/log (user-readable portions)
                if (OperatingSystem.IsLinux() && Directory.Exists("/var/log"))
                    categories.Add(ScanDirectory("System Logs", "Log files in /var/log", "/var/log", extensions: new[] { ".log", ".gz", ".old" }));

                // Linux/macOS: /tmp (if different from GetTempPath)
                var tmp = "/tmp";
                if (Directory.Exists(tmp) && tmp != Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar))
                    categories.Add(ScanDirectory("Global Temp", "Shared temporary files (/tmp)", tmp, scanAll: true));

                // Trash
                var trash = Path.Combine(home, ".local", "share", "Trash", "files");
                if (Directory.Exists(trash))
                    categories.Add(ScanDirectory("Trash", "Deleted files in Trash", trash, scanAll: true));

                // Package manager caches
                var aptCache = "/var/cache/apt/archives";
                if (Directory.Exists(aptCache))
                    categories.Add(ScanDirectory("APT Cache", "Downloaded package files", aptCache, extensions: new[] { ".deb" }));

                var pipCache = Path.Combine(home, ".cache", "pip");
                if (Directory.Exists(pipCache))
                    categories.Add(ScanDirectory("Pip Cache", "Python package cache", pipCache, scanAll: true));
            }

            // Browser caches — cross-platform paths
            {
                var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                // Chrome
                var chromeCachePaths = new[]
                {
                    Path.Combine(localApp, @"Google\Chrome\User Data\Default\Cache\Cache_Data"),        // Windows
                    Path.Combine(home, ".config/google-chrome/Default/Cache/Cache_Data"),                // Linux
                    Path.Combine(home, "Library/Caches/Google/Chrome/Default/Cache/Cache_Data"),         // macOS
                };
                foreach (var p in chromeCachePaths)
                    if (Directory.Exists(p)) { categories.Add(ScanDirectory("Chrome Cache", "Google Chrome browser cache", p, scanAll: true)); break; }

                // Edge
                var edgeCachePaths = new[]
                {
                    Path.Combine(localApp, @"Microsoft\Edge\User Data\Default\Cache\Cache_Data"),
                    Path.Combine(home, ".config/microsoft-edge/Default/Cache/Cache_Data"),
                };
                foreach (var p in edgeCachePaths)
                    if (Directory.Exists(p)) { categories.Add(ScanDirectory("Edge Cache", "Microsoft Edge browser cache", p, scanAll: true)); break; }

                // Firefox
                var firefoxPaths = new[]
                {
                    Path.Combine(localApp, @"Mozilla\Firefox\Profiles"),
                    Path.Combine(home, ".mozilla/firefox"),
                    Path.Combine(home, "Library/Caches/Firefox/Profiles"),
                };
                foreach (var ffDir in firefoxPaths)
                {
                    if (!Directory.Exists(ffDir)) continue;
                    foreach (var profile in Directory.GetDirectories(ffDir, "*.default*"))
                    {
                        var cache2 = Path.Combine(profile, "cache2");
                        if (Directory.Exists(cache2))
                        { categories.Add(ScanDirectory("Firefox Cache", "Mozilla Firefox cache", cache2, scanAll: true)); break; }
                    }
                    break;
                }
            }

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

        // Support category-level selection
        var selectedCategories = plan.SelectedItemIds?.Count > 0 && !plan.SelectedItemIds.Contains("all")
            ? new HashSet<string>(plan.SelectedItemIds)
            : null; // null = all categories

        // Load exclude list for filtering
        var excludes = JunkCleanerService.LoadExcludeList();

        await Task.Run(() =>
        {
            foreach (var category in LastReport.Categories)
            {
                // Skip Recycle Bin — don't delete $Recycle.Bin directly
                if (category.Name == "Recycle Bin") continue;

                // Skip if not in selected categories
                if (selectedCategories is not null && !selectedCategories.Contains(category.Name))
                    continue;

                foreach (var file in category.Files)
                {
                    ct.ThrowIfCancellationRequested();

                    // Skip excluded files
                    if (JunkCleanerService.IsExcluded(file.FullPath, excludes))
                    {
                        processed++;
                        continue;
                    }

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

    /// <summary>
    /// Phase 6.17 Wave F: rich-result wrapper with privilege guard for Linux package-cache cleanup.
    /// Windows path doesn't need helper for tmp folder cleanup.
    /// </summary>
    public async Task<OperationResult> RunOperationAsync(
        OptimizationPlan plan,
        IPrivilegedActionGuard guard,
        IProgress<TaskProgress>? progress = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        if (OperatingSystem.IsLinux())
        {
            if (!await guard.TryGuardAsync(
                    actionDescription: "Delete cached package files and temporary data",
                    remediationCommandOverride: null,
                    ct: ct))
            {
                sw.Stop();
                return OperationResult.Skipped(
                    "Privilege helper required",
                    "sudo bash /opt/auracorepro/install-privhelper.sh");
            }
        }

        try
        {
            var legacy = await OptimizeAsync(plan, progress, ct);
            sw.Stop();
            if (!legacy.Success)
                return OperationResult.Failed("Junk cleanup did not complete successfully", sw.Elapsed);
            return OperationResult.Success(legacy.BytesFreed, legacy.ItemsProcessed, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return OperationResult.Failed($"{ex.GetType().Name}: {ex.Message}", sw.Elapsed);
        }
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
