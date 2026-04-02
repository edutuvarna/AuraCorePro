using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuraCore.Engine.AIAnalyzer.Sync;

public static class MetricSyncService
{
    private static readonly string ConsentPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AuraCorePro", "ai_consent.json");

    private static readonly string ResponseCachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AuraCorePro", "ai", "global_metrics.json");

    private static readonly string DefaultApiBase = "https://api.auracore.pro";

    /// <summary>
    /// Runs once per app launch. If AI telemetry consent is given, sends
    /// yesterday's daily metrics to the backend and caches the global response.
    /// </summary>
    public static async Task TrySyncAsync(LocalMetricDb db, CancellationToken ct = default)
    {
        try
        {
            // 1. Check consent
            if (!IsConsentGiven())
            {
                Debug.WriteLine("[MetricSync] No AI telemetry consent — skipping sync.");
                return;
            }

            // 2. Get yesterday's metrics
            var yesterday = DateOnly.FromDateTime(DateTime.Now.AddDays(-1));
            var metrics = db.GetDailyMetricsRange(yesterday, yesterday);
            if (metrics.Count == 0)
            {
                Debug.WriteLine("[MetricSync] No metrics for yesterday — skipping sync.");
                return;
            }

            var daily = metrics[0];

            // 3. Build request
            var deviceHash = ComputeDeviceHash();
            var payload = new MetricSyncPayload
            {
                DeviceId = deviceHash,
                Date = yesterday,
                AvgCpu = Math.Round(daily.AvgCpu, 1),
                AvgRam = Math.Round(daily.AvgRam, 1),
                DiskUsedPct = Math.Round(daily.DiskUsedPct, 1),
                AnomalyCount = daily.AnomalyCount,
                OsVersion = Environment.OSVersion.ToString(),
                CpuCores = Environment.ProcessorCount,
                RamTotalGb = Math.Round(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024.0 * 1024 * 1024), 1)
            };

            // 4. POST to backend
            var apiBase = GetApiBaseUrl();
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var response = await http.PostAsJsonAsync(
                $"{apiBase}/api/telemetry/ai-metrics", payload, ct);

            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"[MetricSync] Backend returned {response.StatusCode}");
                return;
            }

            // 5. Save response locally
            var json = await response.Content.ReadAsStringAsync(ct);
            var dir = Path.GetDirectoryName(ResponseCachePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(ResponseCachePath, json, ct);

            Debug.WriteLine("[MetricSync] Sync completed successfully.");
        }
        catch (Exception ex)
        {
            // Never crash the app — log and move on
            Debug.WriteLine($"[MetricSync] Sync failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Reads cached global metrics from the last successful sync, or null if none.
    /// </summary>
    public static GlobalMetricsResponse? GetCachedGlobalMetrics()
    {
        try
        {
            if (!File.Exists(ResponseCachePath)) return null;
            var json = File.ReadAllText(ResponseCachePath);
            return JsonSerializer.Deserialize<GlobalMetricsResponse>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }

    // ── Private helpers ──────────────────────────────────────────────

    private static bool IsConsentGiven()
    {
        try
        {
            if (!File.Exists(ConsentPath)) return false;
            var json = File.ReadAllText(ConsentPath);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("telemetryConsent", out var val)
                   && val.GetBoolean();
        }
        catch
        {
            return false;
        }
    }

    private static string ComputeDeviceHash()
    {
        var raw = $"{Environment.MachineName}|{Environment.ProcessorCount}|" +
                  $"{GC.GetGCMemoryInfo().TotalAvailableMemoryBytes}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string GetApiBaseUrl()
    {
        // Allow override via environment variable for dev/staging
        var envUrl = Environment.GetEnvironmentVariable("AURACORE_API_URL");
        return string.IsNullOrWhiteSpace(envUrl) ? DefaultApiBase : envUrl.TrimEnd('/');
    }
}

// ── Serialization DTOs ───────────────────────────────────────────────

internal sealed class MetricSyncPayload
{
    [JsonPropertyName("deviceId")] public string DeviceId { get; set; } = "";
    [JsonPropertyName("date")] public DateOnly Date { get; set; }
    [JsonPropertyName("avgCpu")] public double AvgCpu { get; set; }
    [JsonPropertyName("avgRam")] public double AvgRam { get; set; }
    [JsonPropertyName("diskUsedPct")] public double DiskUsedPct { get; set; }
    [JsonPropertyName("anomalyCount")] public int AnomalyCount { get; set; }
    [JsonPropertyName("osVersion")] public string? OsVersion { get; set; }
    [JsonPropertyName("cpuCores")] public int CpuCores { get; set; }
    [JsonPropertyName("ramTotalGb")] public double RamTotalGb { get; set; }
}

public sealed class GlobalMetricsResponse
{
    [JsonPropertyName("globalAvgCpu")] public double GlobalAvgCpu { get; set; }
    [JsonPropertyName("globalAvgRam")] public double GlobalAvgRam { get; set; }
    [JsonPropertyName("globalAvgDiskUsed")] public double GlobalAvgDiskUsed { get; set; }
    [JsonPropertyName("percentileRank")] public PercentileRank? PercentileRank { get; set; }
}

public sealed class PercentileRank
{
    [JsonPropertyName("cpu")] public int Cpu { get; set; }
    [JsonPropertyName("ram")] public int Ram { get; set; }
    [JsonPropertyName("disk")] public int Disk { get; set; }
}
