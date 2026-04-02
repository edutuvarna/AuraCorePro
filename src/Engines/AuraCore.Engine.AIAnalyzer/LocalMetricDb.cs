using Microsoft.Data.Sqlite;

namespace AuraCore.Engine.AIAnalyzer;

public sealed record DailyMetrics(
    DateOnly Date, double AvgCpu, double MaxCpu, double AvgRam, double MaxRam,
    double DiskUsedPct, double DiskFreeGb, string TopRamProcess,
    int AnomalyCount, int SessionDurationMin);

public sealed record AIEvent(
    string EventType, string Severity, string Title, string Description, string Data);

public sealed class LocalMetricDb : IDisposable
{
    private readonly SqliteConnection _conn;

    public LocalMetricDb(string dbPath)
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
            CREATE TABLE IF NOT EXISTS daily_metrics (
                date TEXT PRIMARY KEY,
                avg_cpu REAL, max_cpu REAL, avg_ram REAL, max_ram REAL,
                disk_used_pct REAL, disk_free_gb REAL, top_ram_process TEXT,
                anomaly_count INTEGER, session_duration_min INTEGER
            );
            CREATE TABLE IF NOT EXISTS ai_events (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp TEXT NOT NULL DEFAULT (datetime('now')),
                event_type TEXT NOT NULL, severity TEXT NOT NULL,
                title TEXT NOT NULL, description TEXT, data TEXT
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public void SaveDailyMetrics(DailyMetrics m)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO daily_metrics (date, avg_cpu, max_cpu, avg_ram, max_ram, disk_used_pct, disk_free_gb, top_ram_process, anomaly_count, session_duration_min)
            VALUES ($date, $avgCpu, $maxCpu, $avgRam, $maxRam, $diskPct, $diskFree, $topProc, $anomalies, $session)
            ON CONFLICT(date) DO UPDATE SET
                avg_cpu=$avgCpu, max_cpu=$maxCpu, avg_ram=$avgRam, max_ram=$maxRam,
                disk_used_pct=$diskPct, disk_free_gb=$diskFree, top_ram_process=$topProc,
                anomaly_count=$anomalies, session_duration_min=$session
            """;
        cmd.Parameters.AddWithValue("$date", m.Date.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$avgCpu", m.AvgCpu);
        cmd.Parameters.AddWithValue("$maxCpu", m.MaxCpu);
        cmd.Parameters.AddWithValue("$avgRam", m.AvgRam);
        cmd.Parameters.AddWithValue("$maxRam", m.MaxRam);
        cmd.Parameters.AddWithValue("$diskPct", m.DiskUsedPct);
        cmd.Parameters.AddWithValue("$diskFree", m.DiskFreeGb);
        cmd.Parameters.AddWithValue("$topProc", m.TopRamProcess);
        cmd.Parameters.AddWithValue("$anomalies", m.AnomalyCount);
        cmd.Parameters.AddWithValue("$session", m.SessionDurationMin);
        cmd.ExecuteNonQuery();
    }

    public DailyMetrics? GetDailyMetrics(DateOnly date)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM daily_metrics WHERE date = $date";
        cmd.Parameters.AddWithValue("$date", date.ToString("yyyy-MM-dd"));
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return ReadDailyMetrics(r);
    }

    public IReadOnlyList<DailyMetrics> GetDailyMetricsRange(DateOnly from, DateOnly to)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM daily_metrics WHERE date BETWEEN $from AND $to ORDER BY date";
        cmd.Parameters.AddWithValue("$from", from.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$to", to.ToString("yyyy-MM-dd"));
        using var r = cmd.ExecuteReader();
        var result = new List<DailyMetrics>();
        while (r.Read()) result.Add(ReadDailyMetrics(r));
        return result;
    }

    private static DailyMetrics ReadDailyMetrics(SqliteDataReader r) => new(
        DateOnly.Parse(r.GetString(0)), r.GetDouble(1), r.GetDouble(2),
        r.GetDouble(3), r.GetDouble(4), r.GetDouble(5), r.GetDouble(6),
        r.GetString(7), r.GetInt32(8), r.GetInt32(9));

    public void SaveAIEvent(AIEvent e)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ai_events (event_type, severity, title, description, data)
            VALUES ($type, $sev, $title, $desc, $data)
            """;
        cmd.Parameters.AddWithValue("$type", e.EventType);
        cmd.Parameters.AddWithValue("$sev", e.Severity);
        cmd.Parameters.AddWithValue("$title", e.Title);
        cmd.Parameters.AddWithValue("$desc", e.Description);
        cmd.Parameters.AddWithValue("$data", e.Data);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<AIEvent> GetRecentEvents(int limit = 50)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT event_type, severity, title, description, data FROM ai_events ORDER BY id DESC LIMIT $limit";
        cmd.Parameters.AddWithValue("$limit", limit);
        using var r = cmd.ExecuteReader();
        var result = new List<AIEvent>();
        while (r.Read())
            result.Add(new AIEvent(r.GetString(0), r.GetString(1), r.GetString(2),
                r.IsDBNull(3) ? "" : r.GetString(3), r.IsDBNull(4) ? "" : r.GetString(4)));
        return result;
    }

    public void Dispose() => _conn.Dispose();
}
