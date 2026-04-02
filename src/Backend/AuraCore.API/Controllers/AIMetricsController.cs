using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;

namespace AuraCore.API.Controllers;

[ApiController]
[Route("api/telemetry/ai-metrics")]
public sealed class AIMetricsController : ControllerBase
{
    // ── In-memory store (beta) ────────────────────────────────────────
    private static readonly ConcurrentBag<AIMetricEntry> _metrics = new();
    private static readonly ConcurrentDictionary<string, DateOnly> _rateLimits = new();

    // ── POST /api/telemetry/ai-metrics ────────────────────────────────
    [HttpPost]
    public IActionResult Submit([FromBody] AIMetricRequest req)
    {
        // --- Validation ---
        if (string.IsNullOrWhiteSpace(req.DeviceId) || req.DeviceId.Length > 128)
            return BadRequest(new { error = "Invalid deviceId (required, max 128 chars)" });

        if (req.Date == default)
            return BadRequest(new { error = "Date is required" });

        if (req.Date > DateOnly.FromDateTime(DateTime.UtcNow))
            return BadRequest(new { error = "Date cannot be in the future" });

        if (req.AvgCpu < 0 || req.AvgCpu > 100)
            return BadRequest(new { error = "AvgCpu must be between 0 and 100" });

        if (req.AvgRam < 0 || req.AvgRam > 100)
            return BadRequest(new { error = "AvgRam must be between 0 and 100" });

        if (req.DiskUsedPct < 0 || req.DiskUsedPct > 100)
            return BadRequest(new { error = "DiskUsedPct must be between 0 and 100" });

        if (req.AnomalyCount < 0 || req.AnomalyCount > 10000)
            return BadRequest(new { error = "AnomalyCount must be between 0 and 10000" });

        if (req.CpuCores < 1 || req.CpuCores > 256)
            return BadRequest(new { error = "CpuCores must be between 1 and 256" });

        if (req.RamTotalGb < 0.1 || req.RamTotalGb > 2048)
            return BadRequest(new { error = "RamTotalGb must be between 0.1 and 2048" });

        if (req.OsVersion is { Length: > 200 })
            return BadRequest(new { error = "OsVersion max 200 chars" });

        // --- Rate limit: 1 POST per device per day ---
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var key = req.DeviceId;

        if (_rateLimits.TryGetValue(key, out var lastDate) && lastDate == today)
            return StatusCode(429, new { error = "Rate limit: 1 submission per device per day" });

        _rateLimits[key] = today;

        // --- Store ---
        var entry = new AIMetricEntry(
            req.DeviceId, req.Date, req.AvgCpu, req.AvgRam,
            req.DiskUsedPct, req.AnomalyCount,
            req.OsVersion, req.CpuCores, req.RamTotalGb);

        _metrics.Add(entry);

        // --- Compute response ---
        return Ok(BuildResponse(entry));
    }

    // ── GET /api/telemetry/ai-metrics/global ──────────────────────────
    [HttpGet("global")]
    public IActionResult GetGlobal()
    {
        var all = _metrics.ToArray();
        if (all.Length == 0)
            return Ok(new { globalAvgCpu = 0.0, globalAvgRam = 0.0, globalAvgDiskUsed = 0.0, totalEntries = 0 });

        return Ok(new
        {
            globalAvgCpu = Math.Round(all.Average(m => m.AvgCpu), 1),
            globalAvgRam = Math.Round(all.Average(m => m.AvgRam), 1),
            globalAvgDiskUsed = Math.Round(all.Average(m => m.DiskUsedPct), 1),
            totalEntries = all.Length
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────
    private static object BuildResponse(AIMetricEntry current)
    {
        var all = _metrics.ToArray();

        double globalAvgCpu = all.Length > 0 ? Math.Round(all.Average(m => m.AvgCpu), 1) : 0;
        double globalAvgRam = all.Length > 0 ? Math.Round(all.Average(m => m.AvgRam), 1) : 0;
        double globalAvgDisk = all.Length > 0 ? Math.Round(all.Average(m => m.DiskUsedPct), 1) : 0;

        int cpuPct = Percentile(all.Select(m => m.AvgCpu), current.AvgCpu);
        int ramPct = Percentile(all.Select(m => m.AvgRam), current.AvgRam);
        int diskPct = Percentile(all.Select(m => m.DiskUsedPct), current.DiskUsedPct);

        return new
        {
            globalAvgCpu,
            globalAvgRam,
            globalAvgDiskUsed = globalAvgDisk,
            percentileRank = new { cpu = cpuPct, ram = ramPct, disk = diskPct }
        };
    }

    private static int Percentile(IEnumerable<double> values, double current)
    {
        var arr = values.ToArray();
        if (arr.Length <= 1) return 50;
        int lower = arr.Count(v => v < current);
        return (int)Math.Round(100.0 * lower / arr.Length);
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────
public sealed record AIMetricRequest(
    string DeviceId,
    DateOnly Date,
    double AvgCpu,
    double AvgRam,
    double DiskUsedPct,
    int AnomalyCount,
    string? OsVersion,
    int CpuCores,
    double RamTotalGb);

public sealed record AIMetricEntry(
    string DeviceId,
    DateOnly Date,
    double AvgCpu,
    double AvgRam,
    double DiskUsedPct,
    int AnomalyCount,
    string? OsVersion,
    int CpuCores,
    double RamTotalGb);
