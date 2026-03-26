using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using AuraCore.Module.DiskCleanup.Models;
using System.Security.Cryptography;

namespace AuraCore.Module.DiskCleanup;

/// <summary>
/// Disk Cleanup Pro - Deep system cleanup beyond what Windows built-in tool covers.
/// Scans: WinUpdate cache, Delivery Optimization, Error Reports, Crash Dumps,
/// Shader Cache, Font Cache, Installer Cache, Old Windows, Empty Folders, Duplicates, and more.
/// </summary>
public sealed class DiskCleanupModule : IOptimizationModule
{
    public string Id => "disk-cleanup";
    public string DisplayName => "Disk Cleanup Pro";
    public OptimizationCategory Category => OptimizationCategory.DiskCleanup;
    public RiskLevel Risk => RiskLevel.Medium;

    public CleanupScanReport? LastReport { get; private set; }

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        var categories = new List<CleanupCategory>();

        await Task.Run(() =>
        {
            var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

            // 1. Windows Update Cache
            var swDist = Path.Combine(winDir, @"SoftwareDistribution\Download");
            if (Directory.Exists(swDist))
            {
                categories.Add(ScanDir("Windows Update Cache",
                    "Downloaded update packages - safe to remove after updates are installed",
                    swDist, risk: "Medium", admin: true));
            }

            // 2. Delivery Optimization
            var deliveryOpt = Path.Combine(winDir, @"ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache");
            if (Directory.Exists(deliveryOpt))
            {
                categories.Add(ScanDir("Delivery Optimization",
                    "Peer-to-peer update delivery cache files",
                    deliveryOpt, risk: "Safe", admin: true));
            }
            var doUser = Path.Combine(winDir, @"SoftwareDistribution\DeliveryOptimization");
            if (Directory.Exists(doUser))
            {
                var existing = categories.FirstOrDefault(c => c.Name == "Delivery Optimization");
                if (existing != null && existing.FileCount == 0)
                    categories.Remove(existing);
                categories.Add(ScanDir("Delivery Optimization",
                    "Peer-to-peer update delivery cache files",
                    doUser, risk: "Safe", admin: true));
            }

            // 3. Windows Error Reports
            var wer = Path.Combine(localApp, @"Microsoft\Windows\WER\ReportArchive");
            if (Directory.Exists(wer))
            {
                categories.Add(ScanDir("Windows Error Reports",
                    "Archived crash and error reports sent to Microsoft",
                    wer, risk: "Safe"));
            }
            var werQueue = Path.Combine(localApp, @"Microsoft\Windows\WER\ReportQueue");
            if (Directory.Exists(werQueue))
            {
                var werCat = categories.FirstOrDefault(c => c.Name == "Windows Error Reports");
                if (werCat != null)
                {
                    var extra = ScanDir("temp", "", werQueue, risk: "Safe");
                    categories.Remove(werCat);
                    var merged = new CleanupCategory
                    {
                        Name = "Windows Error Reports",
                        Description = "Archived crash and error reports sent to Microsoft",
                        RiskLevel = "Safe",
                        Files = werCat.Files.Concat(extra.Files).ToList()
                    };
                    categories.Add(merged);
                }
            }

            // 4. Memory Dump Files
            var dumps = new List<CleanupItem>();
            var miniDump = Path.Combine(winDir, "Minidump");
            if (Directory.Exists(miniDump))
                dumps.AddRange(ScanDir("temp", "", miniDump, risk: "Medium").Files);
            var fullDump = Path.Combine(winDir, "MEMORY.DMP");
            if (File.Exists(fullDump))
            {
                try
                {
                    var fi = new FileInfo(fullDump);
                    dumps.Add(new CleanupItem(fullDump, fi.Length, "Memory Dumps", fi.LastWriteTimeUtc));
                }
                catch { }
            }
            if (dumps.Count > 0)
            {
                categories.Add(new CleanupCategory
                {
                    Name = "Memory Dumps",
                    Description = "System crash dump files - can be very large",
                    RiskLevel = "Medium",
                    RequiresAdmin = true,
                    Files = dumps
                });
            }

            // 5. DirectX Shader Cache
            var shaderCache = Path.Combine(localApp, @"D3DSCache");
            if (Directory.Exists(shaderCache))
            {
                categories.Add(ScanDir("DirectX Shader Cache",
                    "Compiled shader cache - will be rebuilt automatically",
                    shaderCache, risk: "Safe"));
            }
            var amdCache = Path.Combine(localApp, @"AMD\DxCache");
            if (Directory.Exists(amdCache))
            {
                var cat = ScanDir("GPU Shader Cache (AMD)", "AMD GPU shader cache", amdCache, risk: "Safe");
                if (cat.FileCount > 0) categories.Add(cat);
            }
            var nvidiaCache = Path.Combine(localApp, @"NVIDIA\DXCache");
            if (Directory.Exists(nvidiaCache))
            {
                var cat = ScanDir("GPU Shader Cache (NVIDIA)", "NVIDIA GPU shader cache", nvidiaCache, risk: "Safe");
                if (cat.FileCount > 0) categories.Add(cat);
            }

            // 6. Font Cache
            var fontCache = Path.Combine(winDir, @"ServiceProfiles\LocalService\AppData\Local\FontCache");
            if (Directory.Exists(fontCache))
            {
                categories.Add(ScanDir("Font Cache",
                    "Windows font rendering cache - rebuilds on restart",
                    fontCache, risk: "Low", admin: true));
            }

            // 7. Windows Installer Patch Cache
            var patchCache = @"C:\Windows\Installer\$PatchCache$";
            if (Directory.Exists(patchCache))
            {
                categories.Add(ScanDir("Installer Patch Cache",
                    "Cached installer patches for rollback - often very large",
                    patchCache, risk: "Medium", admin: true));
            }

            // 8. Windows Temp (System level)
            var winTemp = Path.Combine(winDir, "Temp");
            if (Directory.Exists(winTemp))
            {
                categories.Add(ScanDir("System Temp Files",
                    "Windows system-level temporary files",
                    winTemp, risk: "Safe", admin: true));
            }

            // 9. Old Windows Installation
            var windowsOld = @"C:\Windows.old";
            if (Directory.Exists(windowsOld))
            {
                categories.Add(ScanDir("Previous Windows Installation",
                    "Old Windows files from a previous upgrade - very large!",
                    windowsOld, risk: "High", admin: true));
            }

            // 10. Windows Search Index temp
            var searchTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                @"Microsoft\Search\Data\Applications\Windows\GatherLogs");
            if (Directory.Exists(searchTemp))
            {
                categories.Add(ScanDir("Search Index Logs",
                    "Windows Search indexer log files",
                    searchTemp, risk: "Safe", admin: true,
                    extensions: new[] { ".log", ".etl" }));
            }

            // 11. Temp Internet Files (Legacy IE/Edge)
            var inetCache = Path.Combine(localApp, @"Microsoft\Windows\INetCache");
            if (Directory.Exists(inetCache))
            {
                categories.Add(ScanDir("Internet Cache (Legacy)",
                    "Cached internet files from IE/Legacy Edge",
                    inetCache, risk: "Safe"));
            }

            // 12. Event Trace Logs
            var etlLogs = Path.Combine(winDir, @"System32\LogFiles\WMI");
            if (Directory.Exists(etlLogs))
            {
                categories.Add(ScanDir("ETW Trace Logs",
                    "Event Tracing for Windows log files",
                    etlLogs, risk: "Low", admin: true,
                    extensions: new[] { ".etl" }));
            }

            // ═══════════════════════════════════════════════════════════
            // 13. Empty Folder Scanner (NEW)
            // ═══════════════════════════════════════════════════════════
            var emptyFolders = ScanEmptyFolders(ct);
            if (emptyFolders.FileCount > 0)
                categories.Add(emptyFolders);

            // ═══════════════════════════════════════════════════════════
            // 14. Duplicate File Finder (NEW)
            // ═══════════════════════════════════════════════════════════
            var duplicates = ScanDuplicateFiles(ct);
            if (duplicates.FileCount > 0)
                categories.Add(duplicates);

        }, ct);

        categories.RemoveAll(c => c.FileCount == 0);

        LastReport = new CleanupScanReport { Categories = categories };
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

        var selectedCategories = plan.SelectedItemIds?.Count > 0
            ? new HashSet<string>(plan.SelectedItemIds)
            : new HashSet<string>(LastReport.Categories.Select(c => c.Name));

        await Task.Run(() =>
        {
            foreach (var category in LastReport.Categories)
            {
                if (!selectedCategories.Contains(category.Name)) continue;

                // Special handling for empty folders - delete deepest first
                if (category.Name == "Empty Folders")
                {
                    var sorted = category.Files
                        .OrderByDescending(f => f.FullPath.Length)
                        .ToList();
                    foreach (var folder in sorted)
                    {
                        ct.ThrowIfCancellationRequested();
                        try
                        {
                            if (Directory.Exists(folder.FullPath) && !Directory.EnumerateFileSystemEntries(folder.FullPath).Any())
                            {
                                Directory.Delete(folder.FullPath);
                                deleted++;
                            }
                        }
                        catch { }
                        processed++;
                        progress?.Report(new TaskProgress(Id,
                            total > 0 ? (double)processed / total * 100 : 100,
                            $"Removing empty folders: {processed}/{total}"));
                    }
                    continue;
                }

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
                        else if (Directory.Exists(file.FullPath))
                        {
                            var dirSize = file.SizeBytes;
                            Directory.Delete(file.FullPath, true);
                            freedBytes += dirSize;
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

    // ═══════════════════════════════════════════════════════════
    // Empty Folder Scanner
    // ═══════════════════════════════════════════════════════════
    private static CleanupCategory ScanEmptyFolders(CancellationToken ct)
    {
        var emptyDirs = new List<CleanupItem>();
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Scan common user directories for empty folders
        var scanRoots = new[]
        {
            Path.Combine(userProfile, "Downloads"),
            Path.Combine(userProfile, "Documents"),
            Path.Combine(userProfile, "Desktop"),
            Path.Combine(userProfile, "Pictures"),
            Path.Combine(userProfile, "Videos"),
            Path.Combine(userProfile, "Music"),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        };

        // Protected dirs that should never be removed even if empty
        var protectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git", ".vs", ".vscode", "node_modules", "__pycache__",
            "bin", "obj", "Debug", "Release", ".nuget", ".npm",
            "AppData", "Local", "Roaming", "LocalLow",
            "Microsoft", "Packages", "WindowsApps",
            "Desktop", "Documents", "Downloads", "Pictures", "Videos", "Music"
        };

        foreach (var root in scanRoots)
        {
            if (!Directory.Exists(root)) continue;
            ct.ThrowIfCancellationRequested();

            try
            {
                foreach (var dir in Directory.EnumerateDirectories(root, "*", new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = true,
                    AttributesToSkip = FileAttributes.System | FileAttributes.Hidden
                }))
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        var dirName = Path.GetFileName(dir);
                        if (protectedNames.Contains(dirName)) continue;

                        // Check if truly empty (no files, no subdirs)
                        if (!Directory.EnumerateFileSystemEntries(dir).Any())
                        {
                            var info = new DirectoryInfo(dir);
                            emptyDirs.Add(new CleanupItem(
                                dir, 0, "Empty Folders", info.LastWriteTimeUtc));
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        return new CleanupCategory
        {
            Name = "Empty Folders",
            Description = $"Empty directories in user folders - {emptyDirs.Count} found across Downloads, Documents, Desktop, etc.",
            RiskLevel = "Safe",
            RequiresAdmin = false,
            Files = emptyDirs
        };
    }

    // ═══════════════════════════════════════════════════════════
    // Duplicate File Finder
    // ═══════════════════════════════════════════════════════════
    private static CleanupCategory ScanDuplicateFiles(CancellationToken ct)
    {
        var duplicateFiles = new List<CleanupItem>();
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var scanRoots = new[]
        {
            Path.Combine(userProfile, "Downloads"),
            Path.Combine(userProfile, "Documents"),
            Path.Combine(userProfile, "Desktop"),
            Path.Combine(userProfile, "Pictures"),
            Path.Combine(userProfile, "Videos"),
        };

        // Phase 1: Group files by size (fast pre-filter)
        var sizeGroups = new Dictionary<long, List<string>>();
        long minSize = 100 * 1024;     // Ignore files < 100 KB
        long maxSize = 500L * 1024 * 1024; // Ignore files > 500 MB (too slow to hash)
        int maxFilesPerRoot = 10000;    // Safety limit per directory

        foreach (var root in scanRoots)
        {
            if (!Directory.Exists(root)) continue;
            ct.ThrowIfCancellationRequested();
            int count = 0;

            try
            {
                foreach (var file in Directory.EnumerateFiles(root, "*.*", new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = true,
                    AttributesToSkip = FileAttributes.System | FileAttributes.Hidden
                }))
                {
                    ct.ThrowIfCancellationRequested();
                    if (++count > maxFilesPerRoot) break;

                    try
                    {
                        var info = new FileInfo(file);
                        if (info.Length < minSize || info.Length > maxSize) continue;

                        if (!sizeGroups.TryGetValue(info.Length, out var list))
                        {
                            list = new List<string>();
                            sizeGroups[info.Length] = list;
                        }
                        list.Add(file);
                    }
                    catch { }
                }
            }
            catch { }
        }

        // Phase 2: Hash files that share the same size
        var hashGroups = new Dictionary<string, List<(string path, long size, DateTimeOffset modified)>>();

        foreach (var group in sizeGroups.Where(g => g.Value.Count > 1))
        {
            foreach (var filePath in group.Value)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var hash = ComputePartialHash(filePath);
                    if (hash is null) continue;

                    if (!hashGroups.TryGetValue(hash, out var list))
                    {
                        list = new List<(string, long, DateTimeOffset)>();
                        hashGroups[hash] = list;
                    }
                    var fi = new FileInfo(filePath);
                    list.Add((filePath, fi.Length, fi.LastWriteTimeUtc));
                }
                catch { }
            }
        }

        // Phase 3: For each group of duplicates, keep the oldest, mark others for removal
        foreach (var group in hashGroups.Where(g => g.Value.Count > 1))
        {
            // Keep the file that was modified earliest (original)
            var sorted = group.Value.OrderBy(f => f.modified).ToList();
            // Skip first (keep it), add rest as duplicates
            for (int i = 1; i < sorted.Count; i++)
            {
                duplicateFiles.Add(new CleanupItem(
                    sorted[i].path,
                    sorted[i].size,
                    "Duplicate Files",
                    sorted[i].modified));
            }
        }

        var totalWaste = duplicateFiles.Sum(f => f.SizeBytes);
        var wasteDisplay = totalWaste switch
        {
            < 1024 * 1024 => $"{totalWaste / 1024.0:F0} KB",
            < 1024L * 1024 * 1024 => $"{totalWaste / (1024.0 * 1024):F1} MB",
            _ => $"{totalWaste / (1024.0 * 1024 * 1024):F2} GB"
        };

        return new CleanupCategory
        {
            Name = "Duplicate Files",
            Description = $"Duplicate files in user folders - {duplicateFiles.Count} copies found ({wasteDisplay} wasted)",
            RiskLevel = "Low",
            RequiresAdmin = false,
            Files = duplicateFiles
        };
    }

    /// <summary>
    /// Computes a partial hash (first 8KB + last 8KB) for fast duplicate detection.
    /// Full hash only for files smaller than 16KB.
    /// </summary>
    private static string? ComputePartialHash(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var hasher = SHA256.Create();
            var bufferSize = 8192;

            if (stream.Length <= bufferSize * 2)
            {
                // Small file - hash entire content
                var hash = hasher.ComputeHash(stream);
                return $"{stream.Length}:{Convert.ToHexString(hash)}";
            }

            // Large file - hash first 8KB + last 8KB
            var buffer = new byte[bufferSize * 2];
            stream.Read(buffer, 0, bufferSize);
            stream.Seek(-bufferSize, SeekOrigin.End);
            stream.Read(buffer, bufferSize, bufferSize);

            var partialHash = hasher.ComputeHash(buffer);
            return $"{stream.Length}:{Convert.ToHexString(partialHash)}";
        }
        catch
        {
            return null;
        }
    }

    private static CleanupCategory ScanDir(string name, string description, string path,
        string risk = "Safe", bool admin = false, string[]? extensions = null)
    {
        var files = new List<CleanupItem>();
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
                    if (extensions != null)
                    {
                        var ext = Path.GetExtension(file).ToLowerInvariant();
                        if (!extensions.Contains(ext)) continue;
                    }

                    var info = new FileInfo(file);
                    if (!info.Exists || info.Length == 0) continue;

                    files.Add(new CleanupItem(file, info.Length, name, info.LastWriteTimeUtc));
                }
                catch { }
            }
        }
        catch { }

        return new CleanupCategory
        {
            Name = name,
            Description = description,
            RiskLevel = risk,
            RequiresAdmin = admin,
            Files = files
        };
    }
}

public static class DiskCleanupRegistration
{
    public static IServiceCollection AddDiskCleanupModule(
        this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, DiskCleanupModule>();
}
