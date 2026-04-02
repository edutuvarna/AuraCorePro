using System.IO;
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

        try
        {
            using var alter = _conn.CreateCommand();
            alter.CommandText = "ALTER TABLE daily_metrics ADD COLUMN is_synthetic INTEGER DEFAULT 0";
            alter.ExecuteNonQuery();
        }
        catch { /* column already exists */ }
    }

    public void SeedIfEmpty()
    {
        using var countCmd = _conn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM daily_metrics";
        var count = Convert.ToInt64(countCmd.ExecuteScalar());
        if (count > 0) return;

        var root = OperatingSystem.IsWindows()
            ? (DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady)?.Name ?? "C:\\")
            : "/";
        var drive = new DriveInfo(root);
        var currentDiskPct = (drive.TotalSize - drive.AvailableFreeSpace) * 100.0 / drive.TotalSize;
        var currentDiskFreeGb = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);

        var rng = new Random();
        for (int i = 7; i >= 1; i--)
        {
            var date = DateTime.Today.AddDays(-i);
            var diskPct = currentDiskPct - (i * (0.15 + rng.NextDouble() * 0.1));
            var diskFreeGb = currentDiskFreeGb + (i * (0.15 + rng.NextDouble() * 0.1) * drive.TotalSize / (100.0 * 1024 * 1024 * 1024));
            var avgCpu = 20 + rng.NextDouble() * 15;
            var avgRam = 50 + rng.NextDouble() * 20;

            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO daily_metrics (date, avg_cpu, max_cpu, avg_ram, max_ram, disk_used_pct, disk_free_gb, top_ram_process, anomaly_count, session_duration_min, is_synthetic)
                VALUES ($date, $avgCpu, $maxCpu, $avgRam, $maxRam, $diskPct, $diskFree, $topProc, $anomalies, $session, 1)
                ON CONFLICT(date) DO NOTHING
                """;
            cmd.Parameters.AddWithValue("$date", DateOnly.FromDateTime(date).ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("$avgCpu", avgCpu);
            cmd.Parameters.AddWithValue("$maxCpu", avgCpu + rng.NextDouble() * 20);
            cmd.Parameters.AddWithValue("$avgRam", avgRam);
            cmd.Parameters.AddWithValue("$maxRam", avgRam + rng.NextDouble() * 15);
            cmd.Parameters.AddWithValue("$diskPct", diskPct);
            cmd.Parameters.AddWithValue("$diskFree", diskFreeGb);
            cmd.Parameters.AddWithValue("$topProc", "synthetic");
            cmd.Parameters.AddWithValue("$anomalies", 0);
            cmd.Parameters.AddWithValue("$session", 60 + rng.Next(120));
            cmd.ExecuteNonQuery();
        }
    }

    public void CleanupSynthetic()
    {
        using var countCmd = _conn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM daily_metrics WHERE is_synthetic = 0";
        var realCount = Convert.ToInt64(countCmd.ExecuteScalar());
        if (realCount >= 7)
        {
            using var delCmd = _conn.CreateCommand();
            delCmd.CommandText = "DELETE FROM daily_metrics WHERE is_synthetic = 1";
            delCmd.ExecuteNonQuery();
        }
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
