namespace AuraCore.API.Application.Services.Telemetry;

public interface ITelemetryRateLimiter
{
    /// <summary>
    /// Try to record N events for a given client IP. Returns true if within quota,
    /// false if rate-limited. Quota = 60 events/minute per IP (sliding window).
    /// Empty IP (loopback/dev) always admits.
    /// </summary>
    bool TryAdmit(string ipAddress, int eventCount);
}
