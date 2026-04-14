using AuraCore.Engine.AIAnalyzer.Config;
using Xunit;

namespace AuraCore.Tests.AIEngine;

public class AIConfigProviderTests
{
    [Fact]
    public void LoadConfig_ValidJson_ReturnsParams()
    {
        var tmpFile = Path.GetTempFileName();
        try
        {
            var json = """
            {
                "anomalyDetector": {
                    "windowSize": 128,
                    "threshold": 0.75,
                    "sensitivity": 90,
                    "alertThreshold": 0.6
                },
                "diskForecaster": {
                    "windowSize": 21,
                    "seriesLength": 90,
                    "confidenceLevel": 99,
                    "forecastHorizon": 60
                },
                "memoryLeakDetector": {
                    "confidence": 0.99,
                    "changeHistoryLength": 30,
                    "growthRateThreshold": 2.0,
                    "minSamples": 20
                }
            }
            """;
            File.WriteAllText(tmpFile, json);

            var config = AIConfigProvider.LoadFromFile(tmpFile);

            Assert.Equal(128, config.AnomalyDetector.WindowSize);
            Assert.Equal(0.75, config.AnomalyDetector.Threshold);
            Assert.Equal(90, config.AnomalyDetector.Sensitivity);
            Assert.Equal(0.6, config.AnomalyDetector.AlertThreshold);

            Assert.Equal(21, config.DiskForecaster.WindowSize);
            Assert.Equal(90, config.DiskForecaster.SeriesLength);
            Assert.Equal(99, config.DiskForecaster.ConfidenceLevel);
            Assert.Equal(60, config.DiskForecaster.ForecastHorizon);

            Assert.Equal(0.99, config.MemoryLeakDetector.Confidence);
            Assert.Equal(30, config.MemoryLeakDetector.ChangeHistoryLength);
            Assert.Equal(2.0, config.MemoryLeakDetector.GrowthRateThreshold);
            Assert.Equal(20, config.MemoryLeakDetector.MinSamples);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    [Fact]
    public void LoadConfig_MissingFile_ReturnsDefaults()
    {
        var config = AIConfigProvider.LoadFromFile("/nonexistent/path/config.json");

        Assert.Equal(64, config.AnomalyDetector.WindowSize);
        Assert.Equal(0.5, config.AnomalyDetector.Threshold);
        Assert.Equal(80, config.AnomalyDetector.Sensitivity);
        Assert.Equal(0.4, config.AnomalyDetector.AlertThreshold);

        Assert.Equal(14, config.DiskForecaster.WindowSize);
        Assert.Equal(60, config.DiskForecaster.SeriesLength);
        Assert.Equal(95, config.DiskForecaster.ConfidenceLevel);
        Assert.Equal(30, config.DiskForecaster.ForecastHorizon);

        Assert.Equal(0.95, config.MemoryLeakDetector.Confidence);
        Assert.Equal(20, config.MemoryLeakDetector.ChangeHistoryLength);
        Assert.Equal(1.0, config.MemoryLeakDetector.GrowthRateThreshold);
        Assert.Equal(12, config.MemoryLeakDetector.MinSamples);
    }
}
