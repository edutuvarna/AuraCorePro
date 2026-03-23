using System.Text.Json;

namespace AuraCore.Desktop.Services.Scheduler;

public sealed record ScheduleEntry
{
    public string ModuleId { get; init; } = "";
    public string ModuleName { get; init; } = "";
    public bool Enabled { get; set; }
    public ScheduleInterval Interval { get; set; } = ScheduleInterval.Daily;
    public bool OnlyWhenIdle { get; set; } = true;
    public DateTimeOffset? LastRun { get; set; }
    public string? LastResult { get; set; }
}

public enum ScheduleInterval
{
    Every30Min,
    Hourly,
    Every6Hours,
    Daily,
    Weekly
}

public static class ScheduleIntervalExtensions
{
    public static TimeSpan ToTimeSpan(this ScheduleInterval interval) => interval switch
    {
        ScheduleInterval.Every30Min => TimeSpan.FromMinutes(30),
        ScheduleInterval.Hourly => TimeSpan.FromHours(1),
        ScheduleInterval.Every6Hours => TimeSpan.FromHours(6),
        ScheduleInterval.Daily => TimeSpan.FromHours(24),
        ScheduleInterval.Weekly => TimeSpan.FromDays(7),
        _ => TimeSpan.FromHours(24)
    };

    public static string ToDisplayString(this ScheduleInterval interval) => interval switch
    {
        ScheduleInterval.Every30Min => "Every 30 minutes",
        ScheduleInterval.Hourly => "Every hour",
        ScheduleInterval.Every6Hours => "Every 6 hours",
        ScheduleInterval.Daily => "Daily",
        ScheduleInterval.Weekly => "Weekly",
        _ => "Daily"
    };
}

public sealed class ScheduleStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AuraCorePro", "schedules.json");

    public static List<ScheduleEntry> Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<List<ScheduleEntry>>(File.ReadAllText(FilePath)) ?? GetDefaults();
        }
        catch { }
        return GetDefaults();
    }

    public static void Save(List<ScheduleEntry> entries)
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private static List<ScheduleEntry> GetDefaults() => new()
    {
        new() { ModuleId = "junk-cleaner", ModuleName = "Junk Cleaner", Enabled = false, Interval = ScheduleInterval.Daily },
        new() { ModuleId = "ram-optimizer", ModuleName = "RAM Optimizer", Enabled = false, Interval = ScheduleInterval.Hourly },
        new() { ModuleId = "system-health", ModuleName = "System Health Scan", Enabled = false, Interval = ScheduleInterval.Daily },
        new() { ModuleId = "registry-optimizer", ModuleName = "Registry Scan & Fix", Enabled = false, Interval = ScheduleInterval.Weekly },
        new() { ModuleId = "bloatware-removal", ModuleName = "Bloatware Scan", Enabled = false, Interval = ScheduleInterval.Weekly },
        new() { ModuleId = "storage-compression", ModuleName = "Storage Analysis", Enabled = false, Interval = ScheduleInterval.Weekly },
    };
}
