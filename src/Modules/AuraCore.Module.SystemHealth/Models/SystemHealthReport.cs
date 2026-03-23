namespace AuraCore.Module.SystemHealth.Models;

public sealed record SystemHealthReport
{
    public string OsName { get; init; } = "";
    public string OsVersion { get; init; } = "";
    public string Architecture { get; init; } = "";
    public string MachineName { get; init; } = "";
    public TimeSpan Uptime { get; init; }
    public int ProcessorCount { get; init; }
    public string ProcessorName { get; init; } = "";
    public double TotalRamGb { get; init; }
    public double AvailableRamGb { get; init; }
    public int RamUsagePercent { get; init; }
    public List<DriveReport> Drives { get; init; } = new();
    public List<GpuReport> Gpus { get; init; } = new();
    public BatteryReport? Battery { get; init; }
    public List<StartupEntry> StartupPrograms { get; init; } = new();
    public int RunningProcesses { get; init; }
    public int HealthScore { get; init; }
}

public sealed record DriveReport(
    string Name, string Label, string Format,
    double TotalGb, double FreeGb, int UsedPercent);

public sealed record GpuReport
{
    public string Name { get; init; } = "";
    public string DriverVersion { get; init; } = "";
    public string VideoMemory { get; init; } = "";
    public string Resolution { get; init; } = "";
}

public sealed record BatteryReport
{
    public bool HasBattery { get; init; }
    public int ChargePercent { get; init; }
    public string Status { get; init; } = "";
    public string EstimatedRuntime { get; init; } = "";
}

public sealed record StartupEntry
{
    public string Name { get; init; } = "";
    public string Command { get; init; } = "";
    public string Location { get; init; } = "";
    public string Impact { get; init; } = "Unknown";
}
