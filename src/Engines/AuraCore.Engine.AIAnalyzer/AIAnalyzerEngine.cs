using AuraCore.Application.Interfaces.Engines;
using AuraCore.Engine.AIAnalyzer.Models;
using AuraCore.Engine.AIAnalyzer.Profile;

namespace AuraCore.Engine.AIAnalyzer;

public sealed class AIAnalyzerEngine : IAIAnalyzerEngine, IDisposable
{
    private readonly MetricBuffer _buffer = new(capacity: 900);
    private readonly LocalMetricDb _db;
    private readonly UserProfileStore _profile;
    private readonly ProfileLearner _learner;
    private readonly AIInsightsStore _store = new();
    private readonly AnomalyDetector _anomalyDetector = new();
    private readonly DiskForecaster _diskForecaster = new();
    private readonly MemoryLeakDetector _leakDetector = new();

    public AIAnalysisResult? LatestResult => _store.Latest;
    public event Action<AIAnalysisResult>? AnalysisCompleted;

    public AIAnalyzerEngine(string dbPath, string profileDbPath)
    {
        _db = new LocalMetricDb(dbPath);
        try { _db.SeedIfEmpty(); } catch { }
        _profile = new UserProfileStore(profileDbPath);
        _learner = new ProfileLearner(_profile, _buffer);
        _store.Updated += r => AnalysisCompleted?.Invoke(r);
    }

    public void Push(AIMetricSample sample)
    {
        _buffer.Push(new MetricSample(
            sample.Timestamp, sample.CpuPercent, sample.RamPercent, sample.DiskUsedPercent,
            sample.TopProcesses.Select(p => new ProcessMetric(p.Name, p.WorkingSetBytes)).ToList()));
    }

    public Task<AIAnalysisResult> AnalyzeAsync(CancellationToken ct = default)
    {
        _learner.LearnFromBuffer();

        var cpuSeries = _buffer.GetCpuSeries();
        var ramSeries = _buffer.GetRamSeries();

        var cpuAnomalies = _anomalyDetector.Detect(cpuSeries);
        var ramAnomalies = _anomalyDetector.Detect(ramSeries);

        bool cpuAnomaly = cpuAnomalies.Any(a => a.IsAnomaly);
        double cpuScore = cpuAnomalies.Where(a => a.IsAnomaly).Select(a => a.Score).DefaultIfEmpty(0).Max();
        bool ramAnomaly = ramAnomalies.Any(a => a.IsAnomaly);
        double ramScore = ramAnomalies.Where(a => a.IsAnomaly).Select(a => a.Score).DefaultIfEmpty(0).Max();

        try { _db.CleanupSynthetic(); } catch { }

        DiskPrediction? diskPrediction = null;
        var dailyMetrics = _db.GetDailyMetricsRange(
            DateOnly.FromDateTime(DateTime.Today.AddDays(-90)),
            DateOnly.FromDateTime(DateTime.Today));
        if (dailyMetrics.Count >= 7)
        {
            var diskSeries = dailyMetrics.Select(d => (float)d.DiskUsedPct).ToList();
            var forecast = _diskForecaster.Forecast(diskSeries);
            if (forecast is not null)
                diskPrediction = new DiskPrediction(forecast.DaysUntilFull, forecast.Confidence, forecast.Trend);
        }

        var snapshot = _buffer.GetSnapshot();
        var processGroups = snapshot
            .SelectMany(s => s.TopProcesses)
            .GroupBy(p => p.Name)
            .Where(g => g.Count() >= 12)
            .Select(g => new ProcessMemorySeries(g.Key, g.Select(p => p.WorkingSetBytes).ToList()))
            .ToList();

        var leakResults = _leakDetector.Detect(processGroups);
        var memoryLeaks = leakResults
            .Select(l => new MemoryLeakAlert(l.ProcessName, l.GrowthRateMbPerMin, l.Score))
            .ToList();

        var result = new AIAnalysisResult(
            DateTimeOffset.UtcNow,
            cpuAnomaly, cpuScore,
            ramAnomaly, ramScore,
            diskPrediction,
            memoryLeaks);

        _store.Update(result);

        if (cpuAnomaly)
            _db.SaveAIEvent(new AIEvent("anomaly_cpu", "warning", "CPU anomaly detected", $"Score: {cpuScore:F2}", "{}"));
        if (ramAnomaly)
            _db.SaveAIEvent(new AIEvent("anomaly_ram", "warning", "RAM anomaly detected", $"Score: {ramScore:F2}", "{}"));
        foreach (var leak in memoryLeaks)
            _db.SaveAIEvent(new AIEvent("memory_leak", "critical", $"{leak.ProcessName} potential leak",
                $"{leak.GrowthRateMbPerMin:F1} MB/min", "{}"));

        return Task.FromResult(result);
    }

    public void Dispose()
    {
        _db.Dispose();
        _profile.Dispose();
    }
}
