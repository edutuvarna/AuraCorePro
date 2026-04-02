namespace AuraCore.Engine.AIAnalyzer.Models;

/// <summary>Stub — full implementation in a future task (MemoryLeakDetector).</summary>
public sealed record ProcessMemorySeries(string ProcessName, IReadOnlyList<long> BytesSamples);

public sealed class MemoryLeakDetector
{
    private const double LeakThresholdMbPerMin = 1.0;

    public IReadOnlyList<LeakResult> Detect(IReadOnlyList<ProcessMemorySeries> series)
    {
        var results = new List<LeakResult>();
        foreach (var s in series)
        {
            if (s.BytesSamples.Count < 2) continue;
            var growthRate = ComputeGrowthRateMbPerMin(s.BytesSamples);
            if (growthRate > LeakThresholdMbPerMin)
                results.Add(new LeakResult(s.ProcessName, growthRate, growthRate / 10.0));
        }
        return results;
    }

    private static double ComputeGrowthRateMbPerMin(IReadOnlyList<long> bytes)
    {
        int n = bytes.Count;
        double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
        for (int i = 0; i < n; i++)
        {
            double mb = bytes[i] / (1024.0 * 1024.0);
            sumX += i; sumY += mb; sumXY += i * mb; sumX2 += (double)i * i;
        }
        return (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
    }
}
