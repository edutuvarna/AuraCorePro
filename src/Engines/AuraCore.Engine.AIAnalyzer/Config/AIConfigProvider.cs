using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuraCore.Engine.AIAnalyzer.Config;

public static class AIConfigProvider
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static AIModelConfig LoadFromFile(string path)
    {
        try
        {
            if (!File.Exists(path))
                return AIModelConfig.Default;

            var json = File.ReadAllText(path);
            var cfg = JsonSerializer.Deserialize<AIModelConfig>(json, JsonOpts);
            return cfg ?? AIModelConfig.Default;
        }
        catch
        {
            return AIModelConfig.Default;
        }
    }
}

public sealed record AIModelConfig(
    AnomalyDetectorConfig AnomalyDetector,
    DiskForecasterConfig DiskForecaster,
    MemoryLeakDetectorConfig MemoryLeakDetector)
{
    public static readonly AIModelConfig Default = new(
        AnomalyDetectorConfig.Default,
        DiskForecasterConfig.Default,
        MemoryLeakDetectorConfig.Default);
}

public sealed record AnomalyDetectorConfig(
    int WindowSize,
    double Threshold,
    int Sensitivity,
    double AlertThreshold)
{
    public static readonly AnomalyDetectorConfig Default = new(
        WindowSize: 64,
        Threshold: 0.5,
        Sensitivity: 80,
        AlertThreshold: 0.4);
}

public sealed record DiskForecasterConfig(
    int WindowSize,
    int SeriesLength,
    int ConfidenceLevel,
    int ForecastHorizon)
{
    public static readonly DiskForecasterConfig Default = new(
        WindowSize: 14,
        SeriesLength: 60,
        ConfidenceLevel: 95,
        ForecastHorizon: 30);
}

public sealed record MemoryLeakDetectorConfig(
    double Confidence,
    int ChangeHistoryLength,
    double GrowthRateThreshold,
    int MinSamples)
{
    public static readonly MemoryLeakDetectorConfig Default = new(
        Confidence: 0.95,
        ChangeHistoryLength: 20,
        GrowthRateThreshold: 1.0,
        MinSamples: 12);
}
