using Xunit;
using AuraCore.Engine.AIAnalyzer;

namespace AuraCore.Tests.AIEngine;

public class LocalMetricDbTests : IDisposable
{
    private readonly string _dbPath;
    private readonly LocalMetricDb _db;

    public LocalMetricDbTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"aura_test_{Guid.NewGuid():N}.db");
        _db = new LocalMetricDb(_dbPath);
    }

    public void Dispose()
    {
        _db.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public void Constructor_CreatesTablesAutomatically()
    {
        var metrics = _db.GetDailyMetrics(DateOnly.FromDateTime(DateTime.Today));
        Assert.Null(metrics);
    }

    [Fact]
    public void SaveDailyMetrics_ThenRetrieve()
    {
        var date = new DateOnly(2026, 4, 2);
        var m = new DailyMetrics(date, AvgCpu: 34.5, MaxCpu: 82.1, AvgRam: 62.3, MaxRam: 91.0,
            DiskUsedPct: 72.0, DiskFreeGb: 120.5, TopRamProcess: "Chrome", AnomalyCount: 3, SessionDurationMin: 480);
        _db.SaveDailyMetrics(m);
        var loaded = _db.GetDailyMetrics(date);
        Assert.NotNull(loaded);
        Assert.Equal(34.5, loaded!.AvgCpu, 1);
        Assert.Equal("Chrome", loaded.TopRamProcess);
    }

    [Fact]
    public void SaveDailyMetrics_Upsert_UpdatesExisting()
    {
        var date = new DateOnly(2026, 4, 2);
        _db.SaveDailyMetrics(new DailyMetrics(date, 30, 80, 60, 90, 70, 100, "Chrome", 1, 100));
        _db.SaveDailyMetrics(new DailyMetrics(date, 40, 90, 70, 95, 75, 95, "Firefox", 5, 200));
        var loaded = _db.GetDailyMetrics(date);
        Assert.Equal(40, loaded!.AvgCpu, 1);
        Assert.Equal("Firefox", loaded.TopRamProcess);
    }

    [Fact]
    public void GetDailyMetricsRange_ReturnsOrderedByDate()
    {
        _db.SaveDailyMetrics(new DailyMetrics(new DateOnly(2026, 4, 3), 30, 80, 60, 90, 70, 100, "A", 0, 100));
        _db.SaveDailyMetrics(new DailyMetrics(new DateOnly(2026, 4, 1), 10, 50, 40, 70, 60, 200, "B", 0, 200));
        _db.SaveDailyMetrics(new DailyMetrics(new DateOnly(2026, 4, 2), 20, 60, 50, 80, 65, 150, "C", 0, 150));
        var range = _db.GetDailyMetricsRange(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 3));
        Assert.Equal(3, range.Count);
        Assert.Equal(new DateOnly(2026, 4, 1), range[0].Date);
        Assert.Equal(new DateOnly(2026, 4, 3), range[2].Date);
    }

    [Fact]
    public void SaveAIEvent_ThenRetrieveRecent()
    {
        _db.SaveAIEvent(new AIEvent("anomaly_cpu", "warning", "CPU spike", "CPU hit 95%", "{\"score\":0.9}"));
        _db.SaveAIEvent(new AIEvent("memory_leak", "critical", "Chrome leak", "3MB/min growth", "{\"process\":\"chrome\"}"));
        var events = _db.GetRecentEvents(limit: 10);
        Assert.Equal(2, events.Count);
        Assert.Equal("memory_leak", events[0].EventType);
    }
}
