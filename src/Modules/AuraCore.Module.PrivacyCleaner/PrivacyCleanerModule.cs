using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using AuraCore.Module.PrivacyCleaner.Models;

namespace AuraCore.Module.PrivacyCleaner;

/// <summary>
/// Privacy Cleaner - Remove privacy-sensitive data traces from your system.
/// Scans: Browser data (Chrome, Edge, Firefox), Recent files, Jump Lists,
/// Thumbnail cache, Prefetch, Clipboard history, and more.
/// </summary>
public sealed class PrivacyCleanerModule : IOptimizationModule
{
    public string Id => "privacy-cleaner";
    public string DisplayName => "Privacy Cleaner";
    public OptimizationCategory Category => OptimizationCategory.Privacy;
    public RiskLevel Risk => RiskLevel.Medium;

    public PrivacyScanReport? LastReport { get; private set; }

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        var categories = new List<PrivacyCategory>();

        await Task.Run(() =>
        {
            var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

            // ── 1. Chrome Browser Data ──
            var chromeBase = Path.Combine(localApp, @"Google\Chrome\User Data\Default");
            if (Directory.Exists(chromeBase))
            {
                var items = new List<PrivacyItem>();
                ScanDirItems(items, Path.Combine(chromeBase, "Cache"), "Chrome Cache");
                ScanDirItems(items, Path.Combine(chromeBase, "Code Cache"), "Chrome Cache");
                ScanDirItems(items, Path.Combine(chromeBase, "Service Worker", "CacheStorage"), "Chrome Cache");
                ScanDirItems(items, Path.Combine(chromeBase, "GPUCache"), "Chrome Cache");
                if (items.Count > 0)
                    categories.Add(new PrivacyCategory
                    {
                        Name = "Chrome - Cache",
                        Description = "Cached web pages, images, and scripts from Google Chrome",
                        Icon = "Chrome", RiskLevel = "Safe", Items = items
                    });

                var histItems = new List<PrivacyItem>();
                AddFileIfExists(histItems, Path.Combine(chromeBase, "History"), "Chrome History");
                AddFileIfExists(histItems, Path.Combine(chromeBase, "History-journal"), "Chrome History");
                AddFileIfExists(histItems, Path.Combine(chromeBase, "Visited Links"), "Chrome History");
                AddFileIfExists(histItems, Path.Combine(chromeBase, "Top Sites"), "Chrome History");
                if (histItems.Count > 0)
                    categories.Add(new PrivacyCategory
                    {
                        Name = "Chrome - History",
                        Description = "Browsing history, visited links, and top sites",
                        Icon = "Chrome", RiskLevel = "Medium", Items = histItems
                    });

                var cookieItems = new List<PrivacyItem>();
                AddFileIfExists(cookieItems, Path.Combine(chromeBase, "Cookies"), "Chrome Cookies");
                AddFileIfExists(cookieItems, Path.Combine(chromeBase, "Cookies-journal"), "Chrome Cookies");
                if (cookieItems.Count > 0)
                    categories.Add(new PrivacyCategory
                    {
                        Name = "Chrome - Cookies",
                        Description = "Website cookies and tracking data - deleting will log you out of sites",
                        Icon = "Chrome", RiskLevel = "Medium", Items = cookieItems
                    });
            }

            // ── 2. Edge Browser Data ──
            var edgeBase = Path.Combine(localApp, @"Microsoft\Edge\User Data\Default");
            if (Directory.Exists(edgeBase))
            {
                var items = new List<PrivacyItem>();
                ScanDirItems(items, Path.Combine(edgeBase, "Cache"), "Edge Cache");
                ScanDirItems(items, Path.Combine(edgeBase, "Code Cache"), "Edge Cache");
                ScanDirItems(items, Path.Combine(edgeBase, "Service Worker", "CacheStorage"), "Edge Cache");
                if (items.Count > 0)
                    categories.Add(new PrivacyCategory
                    {
                        Name = "Edge - Cache",
                        Description = "Cached data from Microsoft Edge browser",
                        Icon = "Edge", RiskLevel = "Safe", Items = items
                    });

                var histItems = new List<PrivacyItem>();
                AddFileIfExists(histItems, Path.Combine(edgeBase, "History"), "Edge History");
                AddFileIfExists(histItems, Path.Combine(edgeBase, "History-journal"), "Edge History");
                if (histItems.Count > 0)
                    categories.Add(new PrivacyCategory
                    {
                        Name = "Edge - History",
                        Description = "Microsoft Edge browsing history and visited links",
                        Icon = "Edge", RiskLevel = "Medium", Items = histItems
                    });
            }

            // ── 3. Firefox Browser Data ──
            var ffProfiles = Path.Combine(appData, @"Mozilla\Firefox\Profiles");
            if (Directory.Exists(ffProfiles))
            {
                var items = new List<PrivacyItem>();
                foreach (var profile in Directory.GetDirectories(ffProfiles))
                {
                    ScanDirItems(items, Path.Combine(profile, "cache2"), "Firefox Cache");
                    ScanDirItems(items, Path.Combine(profile, "shader-cache"), "Firefox Cache");
                }
                if (items.Count > 0)
                    categories.Add(new PrivacyCategory
                    {
                        Name = "Firefox - Cache",
                        Description = "Cached data from Mozilla Firefox browser",
                        Icon = "Firefox", RiskLevel = "Safe", Items = items
                    });
            }

            // ── 4. Windows Recent Files ──
            var recent = Path.Combine(appData, @"Microsoft\Windows\Recent");
            if (Directory.Exists(recent))
            {
                var items = new List<PrivacyItem>();
                ScanDirItems(items, recent, "Recent Files", recurse: false, extensions: new[] { ".lnk" });
                if (items.Count > 0)
                    categories.Add(new PrivacyCategory
                    {
                        Name = "Recent Documents",
                        Description = "Shortcuts to recently opened files - reveals your file activity",
                        Icon = "Recent", RiskLevel = "Safe", Items = items
                    });
            }

            // ── 5. Jump Lists ──
            var jumpRecent = Path.Combine(appData, @"Microsoft\Windows\Recent\AutomaticDestinations");
            var jumpCustom = Path.Combine(appData, @"Microsoft\Windows\Recent\CustomDestinations");
            var jumpItems = new List<PrivacyItem>();
            ScanDirItems(jumpItems, jumpRecent, "Jump Lists");
            ScanDirItems(jumpItems, jumpCustom, "Jump Lists");
            if (jumpItems.Count > 0)
                categories.Add(new PrivacyCategory
                {
                    Name = "Jump Lists",
                    Description = "Taskbar and Start menu recent/pinned file lists per application",
                    Icon = "JumpList", RiskLevel = "Low", Items = jumpItems
                });

            // ── 6. Thumbnail Cache ──
            var thumbCache = Path.Combine(localApp, @"Microsoft\Windows\Explorer");
            if (Directory.Exists(thumbCache))
            {
                var items = new List<PrivacyItem>();
                ScanDirItems(items, thumbCache, "Thumbnail Cache", recurse: false,
                    extensions: new[] { ".db" });
                if (items.Count > 0)
                    categories.Add(new PrivacyCategory
                    {
                        Name = "Thumbnail Cache",
                        Description = "Cached image thumbnails - reveals which images/folders you viewed",
                        Icon = "Thumbnail", RiskLevel = "Safe", Items = items
                    });
            }

            // ── 7. Windows Prefetch ──
            var prefetch = Path.Combine(winDir, "Prefetch");
            if (Directory.Exists(prefetch))
            {
                var items = new List<PrivacyItem>();
                ScanDirItems(items, prefetch, "Prefetch", recurse: false,
                    extensions: new[] { ".pf" });
                if (items.Count > 0)
                    categories.Add(new PrivacyCategory
                    {
                        Name = "Prefetch Files",
                        Description = "Windows app launch optimization data - reveals which apps you run",
                        Icon = "Prefetch", RiskLevel = "Low", Items = items
                    });
            }

            // ── 8. Windows Activity Timeline ──
            var activityDir = Path.Combine(localApp, @"ConnectedDevicesPlatform");
            if (Directory.Exists(activityDir))
            {
                var items = new List<PrivacyItem>();
                ScanDirItems(items, activityDir, "Activity Timeline",
                    extensions: new[] { ".db", ".db-wal", ".db-shm" });
                if (items.Count > 0)
                    categories.Add(new PrivacyCategory
                    {
                        Name = "Activity Timeline",
                        Description = "Windows Timeline and activity history database",
                        Icon = "Timeline", RiskLevel = "Medium", Items = items
                    });
            }

            // ── 9. Temp Files (User) ──
            var userTemp = Path.GetTempPath();
            if (Directory.Exists(userTemp))
            {
                var items = new List<PrivacyItem>();
                ScanDirItems(items, userTemp, "User Temp Files");
                if (items.Count > 0)
                    categories.Add(new PrivacyCategory
                    {
                        Name = "User Temp Files",
                        Description = "Temporary files from various applications",
                        Icon = "Temp", RiskLevel = "Safe", Items = items
                    });
            }

            // ── 10. Windows Clipboard History ──
            var clipboardDir = Path.Combine(localApp, @"Microsoft\Windows\Clipboard");
            if (Directory.Exists(clipboardDir))
            {
                var items = new List<PrivacyItem>();
                ScanDirItems(items, clipboardDir, "Clipboard History");
                if (items.Count > 0)
                    categories.Add(new PrivacyCategory
                    {
                        Name = "Clipboard History",
                        Description = "Windows clipboard history - may contain copied passwords or sensitive text",
                        Icon = "Clipboard", RiskLevel = "Medium", Items = items
                    });
            }

            // ── 11. DNS Cache (info only, cleaned via command) ──
            // Represented as a virtual category
            categories.Add(new PrivacyCategory
            {
                Name = "DNS Cache",
                Description = "Cached DNS lookups - reveals which websites you visited recently",
                Icon = "DNS", RiskLevel = "Safe",
                Items = new List<PrivacyItem>
                {
                    new PrivacyItem("DNS_CACHE_VIRTUAL", 0, "DNS Cache", DateTimeOffset.UtcNow)
                }
            });

        }, ct);

        categories.RemoveAll(c => c.ItemCount == 0);

        LastReport = new PrivacyScanReport { Categories = categories };
        return new ScanResult(Id, true, LastReport.TotalItems, LastReport.TotalBytes);
    }

    public async Task<OptimizationResult> OptimizeAsync(
        OptimizationPlan plan, IProgress<TaskProgress>? progress = null, CancellationToken ct = default)
    {
        if (LastReport is null)
            return new OptimizationResult(Id, "", false, 0, 0, TimeSpan.Zero);

        var start = DateTime.UtcNow;
        long freedBytes = 0;
        int deleted = 0;
        int total = LastReport.TotalItems;
        int processed = 0;

        var selectedCategories = plan.SelectedItemIds?.Count > 0
            ? new HashSet<string>(plan.SelectedItemIds)
            : new HashSet<string>(LastReport.Categories.Select(c => c.Name));

        await Task.Run(() =>
        {
            foreach (var category in LastReport.Categories)
            {
                if (!selectedCategories.Contains(category.Name)) continue;

                // DNS cache: flush via command
                if (category.Name == "DNS Cache")
                {
                    try
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "ipconfig",
                            Arguments = "/flushdns",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true
                        };
                        using var proc = System.Diagnostics.Process.Start(psi);
                        proc?.WaitForExit(5000);
                        deleted++;
                    }
                    catch { }
                    processed++;
                    progress?.Report(new TaskProgress(Id, (double)processed / total * 100, "Flushed DNS cache"));
                    continue;
                }

                foreach (var item in category.Items)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        if (File.Exists(item.FullPath))
                        {
                            File.Delete(item.FullPath);
                            freedBytes += item.SizeBytes;
                            deleted++;
                        }
                    }
                    catch { }

                    processed++;
                    progress?.Report(new TaskProgress(Id,
                        total > 0 ? (double)processed / total * 100 : 100,
                        $"Cleaning privacy data: {processed}/{total}"));
                }
            }
        }, ct);

        return new OptimizationResult(Id, Guid.NewGuid().ToString(), true, deleted, freedBytes, DateTime.UtcNow - start);
    }

    public Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default)
        => Task.FromResult(false);

    public Task RollbackAsync(string operationId, CancellationToken ct = default)
        => Task.CompletedTask;

    // ── Helpers ──
    private static void ScanDirItems(List<PrivacyItem> items, string path, string category,
        bool recurse = true, string[]? extensions = null)
    {
        if (!Directory.Exists(path)) return;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*.*", new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = recurse,
                AttributesToSkip = FileAttributes.System
            }))
            {
                try
                {
                    if (extensions != null)
                    {
                        var ext = Path.GetExtension(file).ToLowerInvariant();
                        if (!extensions.Contains(ext)) continue;
                    }
                    var info = new FileInfo(file);
                    if (!info.Exists || info.Length == 0) continue;
                    items.Add(new PrivacyItem(file, info.Length, category, info.LastWriteTimeUtc));
                }
                catch { }
            }
        }
        catch { }
    }

    private static void AddFileIfExists(List<PrivacyItem> items, string path, string category)
    {
        try
        {
            if (!File.Exists(path)) return;
            var info = new FileInfo(path);
            items.Add(new PrivacyItem(path, info.Length, category, info.LastWriteTimeUtc));
        }
        catch { }
    }
}

public static class PrivacyCleanerRegistration
{
    public static IServiceCollection AddPrivacyCleanerModule(
        this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, PrivacyCleanerModule>();
}
