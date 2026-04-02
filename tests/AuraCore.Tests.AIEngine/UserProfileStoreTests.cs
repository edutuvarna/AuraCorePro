using Xunit;
using AuraCore.Engine.AIAnalyzer.Profile;

namespace AuraCore.Tests.AIEngine;

public class UserProfileStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly UserProfileStore _store;

    public UserProfileStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"aura_profile_{Guid.NewGuid():N}.db");
        _store = new UserProfileStore(_dbPath);
    }

    public void Dispose()
    {
        _store.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public void Set_ThenGet_ReturnsValue()
    {
        _store.Set("normal_ram_range", """{"min":45.2,"max":78.5,"mean":62.3}""", confidence: 0.85, sampleCount: 4320);
        var entry = _store.Get("normal_ram_range");
        Assert.NotNull(entry);
        Assert.Contains("62.3", entry!.Value);
        Assert.Equal(0.85, entry.Confidence, 2);
        Assert.Equal(4320, entry.SampleCount);
    }

    [Fact]
    public void Set_ExistingKey_UpdatesValue()
    {
        _store.Set("normal_ram_range", """{"mean":60}""", 0.5, 100);
        _store.Set("normal_ram_range", """{"mean":65}""", 0.8, 500);
        var entry = _store.Get("normal_ram_range");
        Assert.Contains("65", entry!.Value);
        Assert.Equal(0.8, entry.Confidence, 2);
        Assert.Equal(500, entry.SampleCount);
    }

    [Fact]
    public void Get_NonExistent_ReturnsNull()
    {
        var entry = _store.Get("does_not_exist");
        Assert.Null(entry);
    }

    [Fact]
    public void GetAll_ReturnsAllEntries()
    {
        _store.Set("key1", "val1", 0.5, 10);
        _store.Set("key2", "val2", 0.7, 20);
        _store.Set("key3", "val3", 0.9, 30);
        var all = _store.GetAll();
        Assert.Equal(3, all.Count);
    }

    [Fact]
    public void SaveSnapshot_ThenRetrieve()
    {
        _store.Set("key1", "val1", 0.5, 10);
        _store.SaveSnapshot();
        var snapshots = _store.GetSnapshots(limit: 10);
        Assert.Single(snapshots);
        Assert.Contains("key1", snapshots[0].ProfileData);
    }

    [Fact]
    public void GetOverallConfidence_AveragesAllEntries()
    {
        _store.Set("a", "v", 0.6, 100);
        _store.Set("b", "v", 0.8, 200);
        _store.Set("c", "v", 1.0, 300);
        var confidence = _store.GetOverallConfidence();
        Assert.Equal(0.8, confidence, 1);
    }
}
