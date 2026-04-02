namespace AuraCore.Engine.AIAnalyzer;

public sealed record MetricSample(
    DateTimeOffset Timestamp,
    float CpuPercent,
    float RamPercent,
    float DiskUsedPercent,
    IReadOnlyList<ProcessMetric> TopProcesses);

public sealed record ProcessMetric(
    string Name,
    long WorkingSetBytes);
