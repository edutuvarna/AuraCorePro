using Microsoft.ML;

namespace AuraCore.Engine.AIAnalyzer.Models;

public sealed record ProcessMemorySeries(string Name, IReadOnlyList<long> WorkingSetBytes);

public sealed class MemoryLeakDetector
{
    private const int MinSamples = 12;
    private const double GrowthThresholdMbPerMin = 1.0;
    private readonly MLContext _ml = new(seed: 42);

    public IReadOnlyList<LeakResult> Detect(IReadOnlyList<ProcessMemorySeries> processes)
    {
        var leaks = new List<LeakResult>();
        foreach (var proc in processes)
        {
            if (proc.WorkingSetBytes.Count < MinSamples) continue;
            var leak = AnalyzeProcess(proc);
            if (leak is not null) leaks.Add(leak);
        }
        return leaks.OrderByDescending(l => l.GrowthRateMbPerMin).ToList();
    }

    private LeakResult? AnalyzeProcess(ProcessMemorySeries proc)
    {
        var mbSeries = proc.WorkingSetBytes
            .Select(b => new MemPoint { Value = (float)(b / (1024.0 * 1024.0)) })
            .ToList();

        var dataView = _ml.Data.LoadFromEnumerable(mbSeries);

        var changeHistoryLength = Math.Max(4, Math.Min(mbSeries.Count / 4, 20));

        var pipeline = _ml.Transforms.DetectIidChangePoint(
            outputColumnName: "Prediction",
            inputColumnName: nameof(MemPoint.Value),
            confidence: 90,
            changeHistoryLength: changeHistoryLength);

        var model = pipeline.Fit(dataView);
        var transformed = model.Transform(dataView);
        var results = _ml.Data.CreateEnumerable<ChangePointOutput>(transformed, reuseRowObject: false).ToList();

        var changePoints = results.Select((r, i) => (Index: i, IsChange: r.Prediction[0] != 0, Score: r.Prediction[1]))
            .Where(r => r.IsChange)
            .ToList();

        var firstMb = mbSeries[0].Value;
        var lastMb = mbSeries[^1].Value;
        var totalGrowthMb = lastMb - firstMb;
        var durationMin = proc.WorkingSetBytes.Count * 2.0 / 60.0;
        var growthRateMbPerMin = durationMin > 0 ? totalGrowthMb / durationMin : 0;

        if (growthRateMbPerMin < GrowthThresholdMbPerMin) return null;
        if (changePoints.Count == 0 && growthRateMbPerMin < 5.0) return null;

        var maxScore = changePoints.Count > 0 ? changePoints.Max(c => c.Score) : growthRateMbPerMin / 10.0;
        return new LeakResult(proc.Name, Math.Round(growthRateMbPerMin, 2), Math.Round(maxScore, 3));
    }

    private sealed class MemPoint { public float Value { get; set; } }
    private sealed class ChangePointOutput { public double[] Prediction { get; set; } = Array.Empty<double>(); }
}
