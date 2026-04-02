using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Admin;

[ApiController]
[Route("api/admin/telemetry")]
[Authorize(Roles = "admin")]
public sealed class AdminTelemetryController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    public AdminTelemetryController(AuraCoreDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? eventType = null,
        [FromQuery] Guid? deviceId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (pageSize > 100) pageSize = 100;
        if (pageSize < 1) pageSize = 10;
        if (page < 1) page = 1;

        var query = _db.TelemetryEvents.AsQueryable();

        if (!string.IsNullOrWhiteSpace(eventType))
            query = query.Where(t => t.EventType == eventType);

        if (deviceId.HasValue)
            query = query.Where(t => t.DeviceId == deviceId.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id, t.DeviceId, t.EventType, t.EventData,
                t.SessionId, t.CreatedAt,
                deviceName = t.Device.MachineName
            })
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var total = await _db.TelemetryEvents.CountAsync(ct);
        var last24h = await _db.TelemetryEvents.CountAsync(t => t.CreatedAt > DateTimeOffset.UtcNow.AddHours(-24), ct);
        var last7d = await _db.TelemetryEvents.CountAsync(t => t.CreatedAt > DateTimeOffset.UtcNow.AddDays(-7), ct);
        var last30d = await _db.TelemetryEvents.CountAsync(t => t.CreatedAt > DateTimeOffset.UtcNow.AddDays(-30), ct);

        var topEventTypes = await _db.TelemetryEvents
            .GroupBy(t => t.EventType)
            .Select(g => new { eventType = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .Take(10)
            .ToListAsync(ct);

        return Ok(new { total, last24h, last7d, last30d, topEventTypes });
    }

    [HttpGet("event-types")]
    public async Task<IActionResult> GetEventTypes(CancellationToken ct)
    {
        var types = await _db.TelemetryEvents
            .GroupBy(t => t.EventType)
            .Select(g => new { eventType = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToListAsync(ct);

        return Ok(types);
    }
}
