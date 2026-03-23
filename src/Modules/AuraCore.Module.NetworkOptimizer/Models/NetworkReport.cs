namespace AuraCore.Module.NetworkOptimizer.Models;

public sealed record NetworkReport
{
    public List<NetworkAdapterInfo> Adapters { get; init; } = new();
    public DnsInfo CurrentDns { get; init; } = new();
    public List<PingResult> PingResults { get; init; } = new();
    public List<DnsPreset> AvailableDnsPresets { get; init; } = new();
    public int IssuesFound { get; init; }
}

public sealed record NetworkAdapterInfo
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Status { get; init; } = "";
    public string Speed { get; init; } = "";
    public string IpAddress { get; init; } = "";
    public string MacAddress { get; init; } = "";
    public string AdapterType { get; init; } = "";
}

public sealed record DnsInfo
{
    public string Primary { get; init; } = "";
    public string Secondary { get; init; } = "";
    public string ProviderName { get; init; } = "";
    public double ResponseTimeMs { get; init; }
}

public sealed record PingResult
{
    public string Host { get; init; } = "";
    public string Label { get; init; } = "";
    public double LatencyMs { get; init; }
    public bool Success { get; init; }
}

public sealed record DnsPreset
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Primary { get; init; } = "";
    public string Secondary { get; init; } = "";
    public string Category { get; init; } = "";
    public bool IsCurrentlyActive { get; set; }
    public double LatencyMs { get; set; } = -1;
}

public sealed record DnsBenchmarkResult
{
    public List<DnsPreset> Rankings { get; init; } = new();
    public DnsPreset? Recommended { get; init; }
    public DnsPreset? Current { get; init; }
    public double? ImprovementMs { get; init; }
}

public sealed record SpeedTestResult
{
    public double DownloadMbps { get; init; }
    public double LatencyMs { get; init; }
    public long BytesDownloaded { get; init; }
    public double DurationSeconds { get; init; }
    public bool Success { get; init; }
    public string Error { get; init; } = "";
    public string ServerUsed { get; init; } = "";
}
