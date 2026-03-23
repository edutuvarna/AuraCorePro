namespace AuraCore.Module.RamOptimizer.Models;

public sealed record RamReport
{
    public double TotalGb { get; init; }
    public double UsedGb { get; init; }
    public double AvailableGb { get; init; }
    public int UsagePercent { get; init; }
    public List<ProcessMemoryInfo> TopProcesses { get; init; } = new();
    public long TotalReclaimableBytes { get; init; }

    public string UsedDisplay => $"{UsedGb:F1} GB";
    public string AvailableDisplay => $"{AvailableGb:F1} GB";
    public string TotalDisplay => $"{TotalGb:F1} GB";
    public string ReclaimableDisplay => TotalReclaimableBytes switch
    {
        < 1024 * 1024 => $"{TotalReclaimableBytes / 1024.0:F0} KB",
        < 1024 * 1024 * 1024 => $"{TotalReclaimableBytes / (1024.0 * 1024):F0} MB",
        _ => $"{TotalReclaimableBytes / (1024.0 * 1024 * 1024):F1} GB"
    };
}

public sealed record ProcessMemoryInfo
{
    public int Pid { get; init; }
    public string Name { get; init; } = "";
    public long WorkingSetBytes { get; init; }
    public long PrivateBytes { get; init; }
    public string Category { get; init; } = "Application";
    public bool IsEssential { get; init; }

    public string MemoryDisplay => WorkingSetBytes switch
    {
        < 1024 * 1024 => $"{WorkingSetBytes / 1024.0:F0} KB",
        < 1024 * 1024 * 1024 => $"{WorkingSetBytes / (1024.0 * 1024):F1} MB",
        _ => $"{WorkingSetBytes / (1024.0 * 1024 * 1024):F2} GB"
    };
}
