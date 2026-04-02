using System.Text.Json;
using AuraCore.Module.JunkCleaner.Models;

namespace AuraCore.Module.JunkCleaner;

/// <summary>
/// Provides exclude-list persistence, cleanup history logging,
/// and disk-pressure detection for the Junk Cleaner module.
/// Also used by CategoryCleanView for the enhanced UI features.
/// </summary>
public static class JunkCleanerService
{
    private static readonly string AppDataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AuraCorePro");

    private static readonly string ExcludeFilePath = Path.Combine(AppDataDir, "junk_exclude.json");
    private static readonly string HistoryFilePath = Path.Combine(AppDataDir, "junk_history.json");

    private const int MaxHistoryEntries = 50;

    // ── Exclude List ──────────────────────────────────────────

    public static List<string> LoadExcludeList()
    {
        try
        {
            if (!File.Exists(ExcludeFilePath)) return new();
            var json = File.ReadAllText(ExcludeFilePath);
            return JsonSerializer.Deserialize<List<string>>(json) ?? new();
        }
        catch { return new(); }
    }

    public static void SaveExcludeList(List<string> excludes)
    {
        try
        {
            Directory.CreateDirectory(AppDataDir);
            var json = JsonSerializer.Serialize(excludes, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ExcludeFilePath, json);
        }
        catch { }
    }

    public static void AddToExcludeList(string path)
    {
        var list = LoadExcludeList();
        var normalized = path.Replace('/', '\\').TrimEnd('\\');
        if (!list.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            list.Add(normalized);
            SaveExcludeList(list);
        }
    }

    public static void RemoveFromExcludeList(string path)
    {
        var list = LoadExcludeList();
        var normalized = path.Replace('/', '\\').TrimEnd('\\');
        list.RemoveAll(p => string.Equals(p, normalized, StringComparison.OrdinalIgnoreCase));
        SaveExcludeList(list);
    }

    public static bool IsExcluded(string filePath, List<string> excludeList)
    {
        var normalized = filePath.Replace('/', '\\');
        foreach (var ex in excludeList)
        {
            if (string.Equals(normalized, ex, StringComparison.OrdinalIgnoreCase))
                return true;
            // Also exclude if file is under an excluded directory
            if (normalized.StartsWith(ex + "\\", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // ── History Log ───────────────────────────────────────────

    public static List<CleanHistoryEntry> LoadHistory()
    {
        try
        {
            if (!File.Exists(HistoryFilePath)) return new();
            var json = File.ReadAllText(HistoryFilePath);
            return JsonSerializer.Deserialize<List<CleanHistoryEntry>>(json) ?? new();
        }
        catch { return new(); }
    }

    public static void AddHistoryEntry(CleanHistoryEntry entry)
    {
        try
        {
            var history = LoadHistory();
            history.Insert(0, entry);
            if (history.Count > MaxHistoryEntries)
                history.RemoveRange(MaxHistoryEntries, history.Count - MaxHistoryEntries);

            Directory.CreateDirectory(AppDataDir);
            var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(HistoryFilePath, json);
        }
        catch { }
    }

    // ── Disk Pressure ─────────────────────────────────────────

    /// <summary>
    /// Returns (usedPercent, freeBytes, totalBytes) for the system drive.
    /// Returns null if detection fails.
    /// </summary>
    public static DiskPressureInfo? GetDiskPressure()
    {
        try
        {
            var systemDrive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? "C:\\";
            var driveInfo = new DriveInfo(systemDrive);
            if (!driveInfo.IsReady) return null;

            var total = driveInfo.TotalSize;
            var free = driveInfo.AvailableFreeSpace;
            var used = total - free;
            var usedPercent = total > 0 ? (double)used / total * 100.0 : 0;

            return new DiskPressureInfo(usedPercent, free, total);
        }
        catch { return null; }
    }
}

public sealed record CleanHistoryEntry
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string ModuleId { get; init; } = "";
    public int ItemsCleaned { get; init; }
    public long BytesFreed { get; init; }
    public List<string> Categories { get; init; } = new();

    public string BytesFreedDisplay => BytesFreed switch
    {
        < 1024 => $"{BytesFreed} B",
        < 1024 * 1024 => $"{BytesFreed / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{BytesFreed / (1024.0 * 1024):F1} MB",
        _ => $"{BytesFreed / (1024.0 * 1024 * 1024):F2} GB"
    };
}

public sealed record DiskPressureInfo(double UsedPercent, long FreeBytes, long TotalBytes)
{
    public bool IsHighPressure => UsedPercent >= 85.0;

    public string FreeDisplay => FreeBytes switch
    {
        < 1024L * 1024 * 1024 => $"{FreeBytes / (1024.0 * 1024):F1} MB",
        _ => $"{FreeBytes / (1024.0 * 1024 * 1024):F1} GB"
    };
}
