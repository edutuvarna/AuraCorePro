using Xunit;
using AuraCore.Engine.AIAnalyzer;
using AuraCore.Engine.AIAnalyzer.Profile;

namespace AuraCore.Tests.AIEngine;

public class ProfileLearnerTests : IDisposable
{
    private readonly string _dbPath;
    private readonly UserProfileStore _profile;
    private readonly MetricBuffer _buffer;
    private readonly ProfileLearner _learner;

    public ProfileLearnerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"aura_learner_{Guid.NewGuid():N}.db");
        _profile = new UserProfileStore(_dbPath);
        _buffer = new MetricBuffer(capacity: 100);
        _learner = new ProfileLearner(_profile, _buffer);
    }

    public void Dispose()
    {
        _profile.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public void LearnFromBuffer_UpdatesRamRange()
    {
        for (int i = 0; i < 10; i++)
            _buffer.Push(new MetricSample(
                DateTimeOffset.UtcNow.AddSeconds(i * 2), 30f, 50f + i * 2, 60f,
                Array.Empty<ProcessMetric>()));
        _learner.LearnFromBuffer();
        var entry = _profile.Get("normal_ram_range");
        Assert.NotNull(entry);
        Assert.True(entry!.SampleCount >= 10);
    }

    [Fact]
    public void LearnFromBuffer_UpdatesCpuRange()
    {
        for (int i = 0; i < 10; i++)
            _buffer.Push(new MetricSample(
                DateTimeOffset.UtcNow.AddSeconds(i * 2), 20f + i * 3, 60f, 70f,
                Array.Empty<ProcessMetric>()));
        _learner.LearnFromBuffer();
        var entry = _profile.Get("normal_cpu_range");
        Assert.NotNull(entry);
    }

    [Fact]
    public void LearnFromBuffer_TracksTypicalApps()
    {
        var procs = new ProcessMetric[]
        {
            new("Chrome", 2_000_000_000),
            new("Code", 800_000_000),
        };
        for (int i = 0; i < 5; i++)
            _buffer.Push(new MetricSample(
                DateTimeOffset.UtcNow.AddSeconds(i * 2), 30f, 60f, 70f, procs));
        _learner.LearnFromBuffer();
        var entry = _profile.Get("typical_apps");
        Assert.NotNull(entry);
        Assert.Contains("Chrome", entry!.Value);
        Assert.Contains("Code", entry.Value);
    }

    [Fact]
    public void LearnFromBuffer_ConfidenceIncreasesWithMoreData()
    {
        for (int i = 0; i < 5; i++)
            _buffer.Push(new MetricSample(DateTimeOffset.UtcNow.AddSeconds(i * 2), 30f, 60f, 70f, Array.Empty<ProcessMetric>()));
        _learner.LearnFromBuffer();
        var conf1 = _profile.Get("normal_ram_range")!.Confidence;

        for (int i = 0; i < 20; i++)
            _buffer.Push(new MetricSample(DateTimeOffset.UtcNow.AddSeconds(100 + i * 2), 35f, 65f, 72f, Array.Empty<ProcessMetric>()));
        _learner.LearnFromBuffer();
        var conf2 = _profile.Get("normal_ram_range")!.Confidence;

        Assert.True(conf2 >= conf1, $"Confidence should increase: {conf1} -> {conf2}");
    }
}
