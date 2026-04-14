using AuraCore.Engine.AIAnalyzer.Models;
using Xunit;

namespace AuraCore.Tests.AIEngine;

public class OnnxAnomalyDetectorTests
{
    [Fact]
    public void IsOnnxAvailable_NoModel_ReturnsFalse()
    {
        using var detector = new OnnxAnomalyDetector(null);
        Assert.False(detector.IsOnnxAvailable);
    }

    [Fact]
    public void Detect_WithoutOnnxModel_FallsBackToSrCnn()
    {
        using var detector = new OnnxAnomalyDetector(null);

        // Generate a stable series with enough samples for SR-CNN (min 12)
        var series = Enumerable.Range(0, 60)
            .Select(i => 50f + (float)Math.Sin(i * 0.1) * 2)
            .ToList();

        var results = detector.Detect(series);

        Assert.NotNull(results);
        Assert.Equal(series.Count, results.Count);
    }

    [Fact]
    public void Detect_StableSeries_NoAnomalies()
    {
        using var detector = new OnnxAnomalyDetector(null);

        // Perfectly stable series — all constant
        var series = Enumerable.Repeat(50f, 60).ToList();

        var results = detector.Detect(series);

        Assert.NotNull(results);
        Assert.Equal(60, results.Count);
        // A constant series should not produce anomalies
        Assert.All(results, r => Assert.False(r.IsAnomaly));
    }
}
