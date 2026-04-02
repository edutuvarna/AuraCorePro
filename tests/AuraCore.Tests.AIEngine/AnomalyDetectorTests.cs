using Xunit;
using AuraCore.Engine.AIAnalyzer.Models;

namespace AuraCore.Tests.AIEngine;

public class AnomalyDetectorTests
{
    [Fact]
    public void Detect_StableSignal_NoAnomaly()
    {
        var detector = new AnomalyDetector();
        var series = Enumerable.Range(0, 200)
            .Select(i => 50f + (float)(Math.Sin(i * 0.1) * 3))
            .ToList();
        var results = detector.Detect(series);
        Assert.Equal(series.Count, results.Count);
        var anomalyCount = results.Count(r => r.IsAnomaly);
        Assert.True(anomalyCount < series.Count * 0.1, $"Too many anomalies in stable signal: {anomalyCount}");
    }

    [Fact]
    public void Detect_SpikeInSignal_DetectsAnomaly()
    {
        var detector = new AnomalyDetector();
        var series = new List<float>();
        for (int i = 0; i < 150; i++) series.Add(30f + (float)(Math.Sin(i * 0.1) * 2));
        for (int i = 0; i < 20; i++) series.Add(95f);
        for (int i = 0; i < 30; i++) series.Add(30f + (float)(Math.Sin(i * 0.1) * 2));
        var results = detector.Detect(series);
        var spikeRegion = results.Skip(150).Take(20).ToList();
        var detectedInSpike = spikeRegion.Count(r => r.IsAnomaly);
        Assert.True(detectedInSpike > 0, "Spike should be detected as anomaly");
    }

    [Fact]
    public void Detect_TooFewSamples_ReturnsNoAnomalies()
    {
        var detector = new AnomalyDetector();
        var series = new List<float> { 50f, 60f, 55f };
        var results = detector.Detect(series);
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.False(r.IsAnomaly));
    }
}
