using System.Collections.Concurrent;
using AuraCore.API.Application.Services.Telemetry;

namespace AuraCore.API.Infrastructure.Services.Telemetry;

/// <summary>
/// Simple in-memory sliding-window rate limiter. 60 events/min per IP.
/// State is ephemeral — resets on app restart (accepted tradeoff;
/// abuse-observed → upgrade to Redis in Phase 6.11).
/// </summary>
public sealed class TelemetryRateLimiter : ITelemetryRateLimiter
{
    private const int MaxEventsPerMinute = 60;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, (DateTimeOffset WindowStart, int Count)> _state
        = new();

    public bool TryAdmit(string ipAddress, int eventCount)
    {
        if (string.IsNullOrEmpty(ipAddress) || eventCount <= 0) return true;
        var now = DateTimeOffset.UtcNow;

        while (true)
        {
            var current = _state.GetOrAdd(ipAddress, _ => (now, 0));
            var (start, count) = current;
            // Reset window if expired
            if (now - start > Window)
            {
                var reset = (now, eventCount);
                if (_state.TryUpdate(ipAddress, reset, current))
                    return true;
                continue;  // Retry on CAS fail
            }

            if (count + eventCount > MaxEventsPerMinute) return false;
            var updated = (start, count + eventCount);
            if (_state.TryUpdate(ipAddress, updated, current))
                return true;
            // else retry
        }
    }
}
