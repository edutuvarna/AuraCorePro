using AuraCore.API.Application.Interfaces;
using AuraCore.API.Application.Services.Telemetry;
using AuraCore.API.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class TelemetryController : ControllerBase
{
    private readonly ITelemetryRepository _telemetry;
    private readonly ITelemetryRateLimiter _rateLimiter;

    public TelemetryController(ITelemetryRepository telemetry, ITelemetryRateLimiter rateLimiter)
    {
        _telemetry = telemetry;
        _rateLimiter = rateLimiter;
    }

    [HttpPost("batch")]
    public async Task<IActionResult> ReceiveBatch([FromBody] TelemetryBatchRequest request, CancellationToken ct)
    {
        // T1.25 TelemetryEnabled enforcement
        var cache = HttpContext?.RequestServices?.GetService<IMemoryCache>();
        if (cache is not null
            && cache.TryGetValue<AppConfig>("maintenance-config", out var cachedCfg)
            && cachedCfg is not null
            && cachedCfg.TelemetryEnabled == false)
            return StatusCode(503, new { error = "Telemetry collection is currently disabled" });

        if (request.Events == null || request.Events.Count == 0)
            return BadRequest(new { error = "No events provided" });

        var eventsCount = request.Events.Count;

        // T1.20: batch-size cap
        if (eventsCount > 100)
            return BadRequest(new { error = "Batch too large (max 100 events per request)" });

        // T1.20: rate limit
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
        if (!_rateLimiter.TryAdmit(ip, eventsCount))
            return StatusCode(429, new { error = "Rate limit exceeded. Try again in 1 minute." });

        foreach (var e in request.Events)
        {
            if (string.IsNullOrEmpty(e.EventType) || e.EventType.Length > 128)
                return BadRequest(new { error = "Invalid EventType" });
            if (e.EventData?.Length > 10000)
                return BadRequest(new { error = "EventData too long (max 10KB)" });
            if (e.SessionId?.Length > 128)
                return BadRequest(new { error = "SessionId too long" });
        }

        var events = request.Events.Select(e => new TelemetryEvent
        {
            DeviceId = request.DeviceId,
            EventType = e.EventType,
            EventData = e.EventData ?? "{}",
            SessionId = e.SessionId,
            CreatedAt = e.Timestamp
        });
        await _telemetry.InsertBatchAsync(events, ct);
        return Accepted(new { received = request.Events.Count });
    }
}

public sealed record TelemetryBatchRequest(Guid DeviceId, List<TelemetryEventDto> Events);
public sealed record TelemetryEventDto(string EventType, string? EventData, string? SessionId, DateTimeOffset Timestamp);
