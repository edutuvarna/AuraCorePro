using AuraCore.API.Application.Interfaces;
using AuraCore.API.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuraCore.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class TelemetryController : ControllerBase
{
    private readonly ITelemetryRepository _telemetry;
    public TelemetryController(ITelemetryRepository telemetry) => _telemetry = telemetry;

    [HttpPost("batch")]
    public async Task<IActionResult> ReceiveBatch([FromBody] TelemetryBatchRequest request, CancellationToken ct)
    {
        if (request.Events == null || request.Events.Count == 0)
            return BadRequest(new { error = "No events provided" });
        if (request.Events.Count > 500)
            return BadRequest(new { error = "Too many events in batch (max 500)" });
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
