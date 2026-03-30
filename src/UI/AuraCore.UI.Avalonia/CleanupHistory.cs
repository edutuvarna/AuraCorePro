using System.Text.Json;

namespace AuraCore.UI.Avalonia;

public sealed record CleanupRecord
{
    public DateTimeOffset Timestamp { get; init; }
    public string Module { get; init; } = "";
    public string Action { get; init; } = "";
    public long BytesFreed { get; init; }
    public int ItemsProcessed { get; init; }
    public double DurationSeconds { get; init; }

    public string BytesDisplay => BytesFreed switch
    {
        < 1024 * 1024 => $"{BytesFreed / 1024.0:F0} KB",
        < 1024L * 1024 * 1024 => $"{BytesFreed / (1024.0 * 1024):F1} MB",
        _ => $"{BytesFreed / (1024.0 * 1024 * 1024):F2} GB"
    };
}

public static class CleanupHistory
{
    private static readonly string FilePath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AuraCorePro", "cleanup_history.json");

    private static List<CleanupRecord>? _cache;

    public static List<CleanupRecord> Load()
    {
        if (_cache is not null) return _cache;
        try
        {
            if (File.Exists(FilePath))
            {
                _cache = JsonSerializer.Deserialize<List<CleanupRecord>>(File.ReadAllText(FilePath)) ?? new();
                return _cache;
            }
        }
        catch { }
        _cache = new();
        return _cache;
    }

    public static void Add(string module, string action, long bytesFreed, int items, double seconds)
    {
        var records = Load();
        records.Insert(0, new CleanupRecord
        {
            Timestamp = DateTimeOffset.Now,
            Module = module,
            Action = action,
            BytesFreed = bytesFreed,
            ItemsProcessed = items,
            DurationSeconds = seconds
        });
        if (records.Count > 100) records.RemoveRange(100, records.Count - 100);
        _cache = records;
        Save(records);
    }

    public static (long totalFreed, int totalActions, string sinceDate) GetStats()
    {
        var records = Load();
        if (records.Count == 0) return (0, 0, "");
        var totalFreed = records.Sum(r => r.BytesFreed);
        var oldest = records.Min(r => r.Timestamp);
        return (totalFreed, records.Count, oldest.LocalDateTime.ToString("MMM d, yyyy"));
    }

    private static void Save(List<CleanupRecord> records)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
