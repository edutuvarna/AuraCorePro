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
