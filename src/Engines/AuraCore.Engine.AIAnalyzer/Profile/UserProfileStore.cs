using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace AuraCore.Engine.AIAnalyzer.Profile;

public sealed record ProfileEntry(
    string Key, string Value, double Confidence, int SampleCount,
    DateTimeOffset FirstSeen, DateTimeOffset LastUpdated);

public sealed record ProfileSnapshot(string Week, string ProfileData, DateTimeOffset CreatedAt);

public sealed class UserProfileStore : IDisposable
{
    private readonly SqliteConnection _conn;

    public UserProfileStore(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        _conn = new SqliteConnection($"Data Source={dbPath}");
        _conn.Open();
        InitTables();
    }

    private void InitTables()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS user_profile (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL,
                confidence REAL DEFAULT 0.0,
                sample_count INTEGER DEFAULT 0,
                first_seen TEXT NOT NULL,
                last_updated TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS profile_snapshots (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                week TEXT NOT NULL,
                profile_data TEXT NOT NULL,
                created_at TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public void Set(string key, string value, double confidence, int sampleCount)
    {
        var now = DateTimeOffset.UtcNow.ToString("o");
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO user_profile (key, value, confidence, sample_count, first_seen, last_updated)
            VALUES ($key, $val, $conf, $cnt, $now, $now)
            ON CONFLICT(key) DO UPDATE SET
                value=$val, confidence=$conf, sample_count=$cnt, last_updated=$now
            """;
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$val", value);
        cmd.Parameters.AddWithValue("$conf", confidence);
        cmd.Parameters.AddWithValue("$cnt", sampleCount);
        cmd.Parameters.AddWithValue("$now", now);
        cmd.ExecuteNonQuery();
    }

    public ProfileEntry? Get(string key)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT key, value, confidence, sample_count, first_seen, last_updated FROM user_profile WHERE key = $key";
        cmd.Parameters.AddWithValue("$key", key);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new ProfileEntry(r.GetString(0), r.GetString(1), r.GetDouble(2), r.GetInt32(3),
            DateTimeOffset.Parse(r.GetString(4)), DateTimeOffset.Parse(r.GetString(5)));
    }

    public IReadOnlyList<ProfileEntry> GetAll()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT key, value, confidence, sample_count, first_seen, last_updated FROM user_profile ORDER BY key";
        using var r = cmd.ExecuteReader();
        var result = new List<ProfileEntry>();
        while (r.Read())
            result.Add(new ProfileEntry(r.GetString(0), r.GetString(1), r.GetDouble(2), r.GetInt32(3),
                DateTimeOffset.Parse(r.GetString(4)), DateTimeOffset.Parse(r.GetString(5))));
        return result;
    }

    public double GetOverallConfidence()
    {
        var all = GetAll();
        if (all.Count == 0) return 0.0;
        return all.Average(e => e.Confidence);
    }

    public void SaveSnapshot()
    {
        var all = GetAll();
        var data = JsonSerializer.Serialize(all.Select(e => new { e.Key, e.Value, e.Confidence }));
        var week = $"{DateTime.UtcNow:yyyy}-W{System.Globalization.ISOWeek.GetWeekOfYear(DateTime.UtcNow):D2}";
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT INTO profile_snapshots (week, profile_data, created_at) VALUES ($week, $data, $now)";
        cmd.Parameters.AddWithValue("$week", week);
        cmd.Parameters.AddWithValue("$data", data);
        cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<ProfileSnapshot> GetSnapshots(int limit = 10)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT week, profile_data, created_at FROM profile_snapshots ORDER BY id DESC LIMIT $limit";
        cmd.Parameters.AddWithValue("$limit", limit);
        using var r = cmd.ExecuteReader();
        var result = new List<ProfileSnapshot>();
        while (r.Read())
            result.Add(new ProfileSnapshot(r.GetString(0), r.GetString(1), DateTimeOffset.Parse(r.GetString(2))));
        return result;
    }

    public void Dispose() => _conn.Dispose();
}
