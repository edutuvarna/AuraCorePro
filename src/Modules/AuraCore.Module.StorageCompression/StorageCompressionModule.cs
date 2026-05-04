using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using AuraCore.Module.StorageCompression.Models;

namespace AuraCore.Module.StorageCompression;

[SupportedOSPlatform("windows")]
public sealed class StorageCompressionModule : IOptimizationModule
{
    public string Id => "storage-compression";
    public string DisplayName => "Storage Compression";
    public OptimizationCategory Category => OptimizationCategory.StorageCompression;
    public RiskLevel Risk => RiskLevel.Medium;

    public StorageCompressionReport? LastReport { get; private set; }

    // Folders worth compressing — big, rarely-written, performance not critical
    private static readonly (string subPath, string name, string desc, string risk, CompressionType algo)[] CompressTargets =
    {
        (@"Windows\Installer", "Windows Installer Cache", "MSI installer backups — large, rarely accessed", "Safe", CompressionType.LZX),
        (@"Windows\SoftwareDistribution\Download", "Windows Update Cache", "Downloaded update files", "Safe", CompressionType.LZX),
        (@"Windows\Logs", "Windows Logs", "System log files", "Safe", CompressionType.XPRESS8K),
        (@"Windows\WinSxS\Backup", "WinSxS Backup", "Component store backups", "Caution", CompressionType.LZX),
        (@"Program Files\dotnet", ".NET Runtime", "Shared .NET framework files", "Safe", CompressionType.XPRESS8K),
        (@"ProgramData\Package Cache", "Package Cache", "Visual Studio and installer caches", "Safe", CompressionType.LZX),
        (@"Windows\System32\DriverStore\FileRepository", "Driver Store", "Cached driver packages", "Caution", CompressionType.XPRESS8K),
    };

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        var report = await Task.Run(async () =>
        {
            var folders = new List<CompressibleFolder>();
            var systemDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";

            foreach (var (subPath, name, desc, risk, algo) in CompressTargets)
            {
                ct.ThrowIfCancellationRequested();
                var fullPath = Path.Combine(systemDrive, subPath);
                if (!Directory.Exists(fullPath)) continue;

                var (size, compressed) = GetFolderStats(fullPath);
                if (size < 1024 * 1024) continue; // Skip folders < 1MB

                // Estimate savings based on algorithm
                double ratio = algo switch
                {
                    CompressionType.LZX => 0.45,
                    CompressionType.XPRESS16K => 0.30,
                    CompressionType.XPRESS8K => 0.25,
                    CompressionType.XPRESS4K => 0.18,
                    _ => 0.25
                };

                var savings = compressed ? 0 : (long)(size * ratio);

                folders.Add(new CompressibleFolder
                {
                    Path = fullPath,
                    DisplayName = name,
                    Description = desc,
                    SizeBytes = size,
                    EstimatedSavings = savings,
                    Risk = risk,
                    IsAlreadyCompressed = compressed,
                    RecommendedAlgorithm = algo
                });
            }

            // Also scan user profile large folders
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var userTargets = new[]
            {
                (Path.Combine(userProfile, ".nuget"), "NuGet Cache", "NuGet package cache", "Safe", CompressionType.LZX),
                (Path.Combine(userProfile, ".gradle"), "Gradle Cache", "Gradle build cache", "Safe", CompressionType.LZX),
                (Path.Combine(userProfile, ".m2"), "Maven Cache", "Maven repository cache", "Safe", CompressionType.LZX),
                (Path.Combine(userProfile, "AppData", "Local", "npm-cache"), "NPM Cache", "Node.js package cache", "Safe", CompressionType.LZX),
            };

            foreach (var (path, name, desc, risk, algo) in userTargets)
            {
                if (!Directory.Exists(path)) continue;
                var (size, compressed) = GetFolderStats(path);
                if (size < 10 * 1024 * 1024) continue; // Skip < 10MB for user folders

                double ratio = algo == CompressionType.LZX ? 0.45 : 0.25;
                folders.Add(new CompressibleFolder
                {
                    Path = path,
                    DisplayName = name,
                    Description = desc,
                    SizeBytes = size,
                    EstimatedSavings = compressed ? 0 : (long)(size * ratio),
                    Risk = risk,
                    IsAlreadyCompressed = compressed,
                    RecommendedAlgorithm = algo
                });
            }

            // Check CompactOS state
            var compactOsState = await GetCompactOsStateAsync();

            // Detect SSD vs HDD
            var driveType = DetectDriveType();

            folders.Sort((a, b) => b.EstimatedSavings.CompareTo(a.EstimatedSavings));

            return new StorageCompressionReport
            {
                Folders = folders,
                TotalCurrentBytes = folders.Sum(f => f.SizeBytes),
                TotalSavingsEstimate = folders.Where(f => !f.IsAlreadyCompressed).Sum(f => f.EstimatedSavings),
                CompactOsEnabled = compactOsState.Contains("Compact"),
                CompactOsState = compactOsState,
                SystemDriveType = driveType
            };
        }, ct);

        LastReport = report;
        return new ScanResult(Id, true, report.Folders.Count, report.TotalSavingsEstimate);
    }

    public async Task<OptimizationResult> OptimizeAsync(
        OptimizationPlan plan, IProgress<TaskProgress>? progress = null, CancellationToken ct = default)
    {
        if (plan.SelectedItemIds.Count == 0)
            return new OptimizationResult(Id, "", false, 0, 0, TimeSpan.Zero);

        var start = DateTime.UtcNow;
        int compressed = 0;
        long totalSaved = 0;
        int total = plan.SelectedItemIds.Count;

        for (int i = 0; i < total; i++)
        {
            ct.ThrowIfCancellationRequested();
            var path = plan.SelectedItemIds[i];

            // Check for special CompactOS action
            if (path == "compact-os-enable" || path == "compact-os-disable")
            {
                progress?.Report(new TaskProgress(Id, (double)(i + 1) / total * 100,
                    path == "compact-os-enable" ? "Enabling CompactOS (this may take a few minutes)..." : "Disabling CompactOS..."));
                var ok = await SetCompactOsAsync(path == "compact-os-enable");
                if (ok) compressed++;
                continue;
            }

            // Decompress action
            if (path.StartsWith("decompress:"))
            {
                var decompPath = path[11..];
                var decompFolder = LastReport?.Folders.FirstOrDefault(f => f.Path == decompPath);
                progress?.Report(new TaskProgress(Id, (double)(i + 1) / total * 100,
                    $"Decompressing: {decompFolder?.DisplayName ?? decompPath}..."));
                var ok = await RunCompactAsync($"/u /s /a /i \"{decompPath}\\*\"");
                if (ok) compressed++;
                continue;
            }

            // Find the folder info
            var folder = LastReport?.Folders.FirstOrDefault(f => f.Path == path);
            if (folder is null) continue;

            var algoArg = folder.RecommendedAlgorithm switch
            {
                CompressionType.LZX => "/exe:lzx",
                CompressionType.XPRESS16K => "/exe:xpress16k",
                CompressionType.XPRESS8K => "/exe:xpress8k",
                _ => "/exe:xpress4k"
            };

            progress?.Report(new TaskProgress(Id, (double)(i + 1) / total * 100,
                $"Compressing: {folder.DisplayName}..."));

            var sizeBefore = folder.SizeBytes;
            var success = await RunCompactAsync($"/c /s /a /i {algoArg} \"{path}\\*\"");

            if (success)
            {
                compressed++;
                totalSaved += folder.EstimatedSavings;
            }
        }

        return new OptimizationResult(Id, Guid.NewGuid().ToString(), true, compressed, totalSaved, DateTime.UtcNow - start);
    }

    public async Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default)
        => true; // Compression is fully reversible

    public async Task RollbackAsync(string operationId, CancellationToken ct = default)
    {
        // Uncompress all folders that were in the last report
        if (LastReport is null) return;
        foreach (var folder in LastReport.Folders.Where(f => f.IsAlreadyCompressed))
        {
            await RunCompactAsync($"/u /s /a /i \"{folder.Path}\\*\"");
        }
    }

    private static (long size, bool isCompressed) GetFolderStats(string path)
    {
        long totalSize = 0;
        int compressedFiles = 0;
        int totalFiles = 0;

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
                    var info = new FileInfo(file);
                    if (!info.Exists) continue;
                    totalSize += info.Length;
                    totalFiles++;
                    if ((info.Attributes & FileAttributes.Compressed) != 0)
                        compressedFiles++;
                }
                catch { }
            }
        }
        catch { }

        // Consider "compressed" if >80% of files are already compressed
        var isCompressed = totalFiles > 0 && (double)compressedFiles / totalFiles > 0.8;
        return (totalSize, isCompressed);
    }

    private static async Task<string> GetCompactOsStateAsync()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "compact.exe",
                Arguments = "/compactos:query",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc is null) return "Unknown";
            var output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
            return output.Trim();
        }
        catch { return "Unknown"; }
    }

    private static async Task<bool> SetCompactOsAsync(bool enable)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "compact.exe",
                Arguments = enable ? "/compactos:always" : "/compactos:never",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc is null) return false;
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0;
        }
        catch { return false; }
    }

    private static async Task<bool> RunCompactAsync(string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "compact.exe",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc is null) return false;
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0;
        }
        catch { return false; }
    }

    private static StorageDriveType DetectDriveType()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-NoProfile -Command \"Get-PhysicalDisk | Where-Object DeviceId -eq 0 | Select-Object -ExpandProperty MediaType\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc is null) return StorageDriveType.Unknown;
            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit();
            return output.Contains("SSD", StringComparison.OrdinalIgnoreCase) ? StorageDriveType.SSD
                : output.Contains("HDD", StringComparison.OrdinalIgnoreCase) ? StorageDriveType.HDD
                : StorageDriveType.Unknown;
        }
        catch { return StorageDriveType.Unknown; }
    }
}

[SupportedOSPlatform("windows")]
public static class StorageCompressionRegistration
{
    public static IServiceCollection AddStorageCompressionModule(
        this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, StorageCompressionModule>();
}
