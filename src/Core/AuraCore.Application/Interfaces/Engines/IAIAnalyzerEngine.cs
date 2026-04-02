namespace AuraCore.Application.Interfaces.Engines;

public interface IAIAnalyzerEngine
{
    void Push(AIMetricSample sample);
    Task<AIAnalysisResult> AnalyzeAsync(CancellationToken ct = default);
    AIAnalysisResult? LatestResult { get; }
    event Action<AIAnalysisResult>? AnalysisCompleted;
}

public sealed record AIMetricSample(
    DateTimeOffset Timestamp,
    float CpuPercent,
    float RamPercent,
    float DiskUsedPercent,
    IReadOnlyList<AIProcessMetric> TopProcesses);

public sealed record AIProcessMetric(string Name, long WorkingSetBytes);

public sealed record AIAnalysisResult(
    DateTimeOffset Timestamp,
    bool CpuAnomaly,
    double CpuAnomalyScore,
    bool RamAnomaly,
    double RamAnomalyScore,
    DiskPrediction? DiskPrediction,
    IReadOnlyList<MemoryLeakAlert> MemoryLeaks);

public sealed record DiskPrediction(
    int DaysUntilFull,
    double Confidence,
    string Trend);

public sealed record MemoryLeakAlert(
    string ProcessName,
    double GrowthRateMbPerMin,
    double ChangePointScore);
