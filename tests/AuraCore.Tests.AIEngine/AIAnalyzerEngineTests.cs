using Xunit;
using AuraCore.Engine.AIAnalyzer;
using AuraCore.Application.Interfaces.Engines;

namespace AuraCore.Tests.AIEngine;

public class AIAnalyzerEngineTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _profilePath;
    private readonly AIAnalyzerEngine _engine;

    public AIAnalyzerEngineTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"aura_engine_{Guid.NewGuid():N}.db");
        _profilePath = Path.Combine(Path.GetTempPath(), $"aura_profile_{Guid.NewGuid():N}.db");
        _engine = new AIAnalyzerEngine(_dbPath, _profilePath);
    }

    public void Dispose()
    {
        _engine.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
        if (File.Exists(_profilePath)) File.Delete(_profilePath);
    }

    [Fact]
    public void Push_AddsSamplesToBuffer()
    {
        var sample = new AIMetricSample(DateTimeOffset.UtcNow, 50f, 60f, 70f, Array.Empty<AIProcessMetric>());
        _engine.Push(sample);
        Assert.Null(_engine.LatestResult);
    }

    [Fact]
    public async Task AnalyzeAsync_WithSufficientData_ReturnsResult()
    {
        for (int i = 0; i < 200; i++)
        {
            _engine.Push(new AIMetricSample(
                DateTimeOffset.UtcNow.AddSeconds(i * 2),
                30f + (float)(Math.Sin(i * 0.1) * 5),
                60f + (float)(Math.Cos(i * 0.1) * 3),
                70f,
                new[] { new AIProcessMetric("TestApp", 500_000_000) }));
        }
        var result = await _engine.AnalyzeAsync();
        Assert.NotNull(result);
        Assert.Equal(result, _engine.LatestResult);
    }

    [Fact]
    public async Task AnalyzeAsync_WithTooFewSamples_ReturnsResultWithNoAnomalies()
    {
        _engine.Push(new AIMetricSample(DateTimeOffset.UtcNow, 50f, 60f, 70f, Array.Empty<AIProcessMetric>()));
        var result = await _engine.AnalyzeAsync();
        Assert.NotNull(result);
        Assert.False(result.CpuAnomaly);
        Assert.False(result.RamAnomaly);
    }

    [Fact]
    public async Task AnalyzeAsync_RaisesEvent()
    {
        AIAnalysisResult? received = null;
        _engine.AnalysisCompleted += r => received = r;
        for (int i = 0; i < 20; i++)
            _engine.Push(new AIMetricSample(DateTimeOffset.UtcNow.AddSeconds(i * 2), 50f, 60f, 70f, Array.Empty<AIProcessMetric>()));
        await _engine.AnalyzeAsync();
        Assert.NotNull(received);
    }
}
