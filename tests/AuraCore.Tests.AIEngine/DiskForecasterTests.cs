using Xunit;
using AuraCore.Engine.AIAnalyzer.Models;

namespace AuraCore.Tests.AIEngine;

public class DiskForecasterTests
{
    [Fact]
    public void Forecast_IncreasingTrend_PredictsFull()
    {
        var forecaster = new DiskForecaster();
        var dailyUsage = Enumerable.Range(0, 30)
            .Select(i => (float)(50.0 + i))
            .ToList();
        var result = forecaster.Forecast(dailyUsage);
        Assert.NotNull(result);
        Assert.True(result!.DaysUntilFull > 0 && result.DaysUntilFull < 60,
            $"Expected prediction within 60 days, got {result.DaysUntilFull}");
        Assert.Equal("increasing", result.Trend);
    }

    [Fact]
    public void Forecast_StableUsage_ReturnsSafePrediction()
    {
        var forecaster = new DiskForecaster();
        var dailyUsage = Enumerable.Range(0, 30)
            .Select(i => 50f + (float)(Math.Sin(i * 0.3) * 2))
            .ToList();
        var result = forecaster.Forecast(dailyUsage);
        Assert.NotNull(result);
        Assert.Equal("stable", result!.Trend);
    }

    [Fact]
    public void Forecast_TooFewDays_ReturnsNull()
    {
        var forecaster = new DiskForecaster();
        var dailyUsage = new List<float> { 50f, 52f, 53f };
        var result = forecaster.Forecast(dailyUsage);
        Assert.Null(result);
    }
}
