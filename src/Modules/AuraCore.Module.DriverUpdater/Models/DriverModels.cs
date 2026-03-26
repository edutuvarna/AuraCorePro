namespace AuraCore.Module.DriverUpdater.Models;

public sealed class DriverInfo
{
    public string DeviceName { get; set; } = "";
    public string DeviceClass { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public string DriverVersion { get; set; } = "";
    public DateTimeOffset DriverDate { get; set; }
    public string InfName { get; set; } = "";
    public string PublishedName { get; set; } = "";
    public string HardwareId { get; set; } = "";
    public string Status { get; set; } = "OK";
    public bool HasProblem { get; set; }
    public int ProblemCode { get; set; }

    /// <summary>Days since driver was last updated</summary>
    public int AgeDays => (int)(DateTimeOffset.UtcNow - DriverDate).TotalDays;

    /// <summary>Driver age category for UI</summary>
    public string AgeCategory => AgeDays switch
    {
        < 90 => "Current",
        < 365 => "Recent",
        < 730 => "Aging",
        _ => "Outdated"
    };

    /// <summary>Risk color for UI</summary>
    public string AgeColor => AgeCategory switch
    {
        "Current" => "Green",
        "Recent" => "Blue",
        "Aging" => "Amber",
        "Outdated" => "Red",
        _ => "Blue"
    };

    public string DriverDateDisplay => DriverDate != DateTimeOffset.MinValue
        ? DriverDate.ToString("yyyy-MM-dd") : "Unknown";

    /// <summary>Icon glyph based on device class</summary>
    public string ClassIcon => DeviceClass.ToLowerInvariant() switch
    {
        "display" or "display adapters" => "\uE7F8",
        "net" or "network" or "network adapters" => "\uE968",
        "media" or "sound" or "audio" or "sound, video and game controllers" => "\uE995",
        "usb" or "universal serial bus controllers" => "\uE88E",
        "hid" or "human interface devices" => "\uE765",
        "diskdrive" or "disk drives" => "\uEDA2",
        "monitor" => "\uE7F4",
        "processor" or "processors" => "\uE950",
        "bluetooth" => "\uE702",
        "camera" or "imaging devices" => "\uE722",
        "keyboard" => "\uE765",
        "mouse" or "mice and other pointing devices" => "\uE962",
        "printer" or "printers" => "\uE749",
        "firmware" => "\uE74C",
        _ => "\uE964"
    };
}

public sealed class DriverScanReport
{
    public List<DriverInfo> Drivers { get; set; } = new();
    public int TotalCount => Drivers.Count;
    public int OutdatedCount => Drivers.Count(d => d.AgeCategory is "Aging" or "Outdated");
    public int ProblemCount => Drivers.Count(d => d.HasProblem);
    public int CurrentCount => Drivers.Count(d => d.AgeCategory == "Current");
    public DateTimeOffset ScanTime { get; set; } = DateTimeOffset.UtcNow;

    public string HealthSummary => ProblemCount > 0
        ? $"{ProblemCount} driver(s) with problems"
        : OutdatedCount > 5
            ? $"{OutdatedCount} outdated drivers found"
            : OutdatedCount > 0
                ? $"{OutdatedCount} aging driver(s), {CurrentCount} current"
                : "All drivers are up to date";
}

public sealed class DriverBackupResult
{
    public string BackupPath { get; set; } = "";
    public int DriversExported { get; set; }
    public long SizeBytes { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }

    public string SizeDisplay => SizeBytes switch
    {
        < 1024 * 1024 => $"{SizeBytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{SizeBytes / (1024.0 * 1024):F1} MB",
        _ => $"{SizeBytes / (1024.0 * 1024 * 1024):F2} GB"
    };
}
