using AuraCore.API.Filters;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Admin;

[ApiController]
[Route("api/admin/devices")]
[Authorize(Roles = "admin")]
public sealed class AdminDeviceController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    public AdminDeviceController(AuraCoreDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (pageSize > 100) pageSize = 100;
        if (pageSize < 1) pageSize = 10;
        if (page < 1) page = 1;

        var query = _db.Devices.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(d => (d.MachineName != null && d.MachineName.Contains(search))
                                  || (d.OsVersion != null && d.OsVersion.Contains(search)));

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(d => d.LastSeenAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new
            {
                d.Id, d.LicenseId, d.MachineName, d.OsVersion,
                // T1.14: HardwareFingerprint removed from list view (privacy).
                // GetById still returns it (detail view is authorized-admin context).
                d.RegisteredAt, d.LastSeenAt,
                licenseTier = d.License.Tier,
                userEmail = d.License.User.Email,
                // T1.15: count fields so frontend 'Crashes' and 'Telemetry Events'
                // columns populate.
                crashCount = d.CrashReports.Count(),
                telemetryCount = d.TelemetryEvents.Count()
            })
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, pages = (int)Math.Ceiling((double)total / pageSize), items });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var d = await _db.Devices.AsNoTracking()
            .Include(x => x.License).ThenInclude(l => l!.User)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (d is null) return NotFound(new { error = "Device not found" });

        return Ok(new {
            d.Id, d.HardwareFingerprint, d.MachineName, d.OsVersion,
            d.RegisteredAt, d.LastSeenAt,
            licenseId = d.LicenseId,
            licenseTier = d.License != null ? d.License.Tier : null,
            userEmail = d.License != null && d.License.User != null ? d.License.User.Email : null
        });
    }

    [HttpDelete("{id:guid}")]
    [AuditAction("DeleteDevice", "Device", TargetIdFromRouteKey = "id")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        // Use ExecuteDeleteAsync to bypass EF tracking (CTP-5 safe)
        var affected = await _db.Devices.Where(d => d.Id == id).ExecuteDeleteAsync(ct);
        if (affected == 0) return NotFound(new { error = "Device not found" });
        return Ok(new { message = "Device revoked", id });
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var total = await _db.Devices.CountAsync(ct);
        var activeLastDay = await _db.Devices.CountAsync(d => d.LastSeenAt >= now.AddDays(-1), ct);
        var activeLastWeek = await _db.Devices.CountAsync(d => d.LastSeenAt >= now.AddDays(-7), ct);
        var activeLastMonth = await _db.Devices.CountAsync(d => d.LastSeenAt >= now.AddDays(-30), ct);
        var topOs = await _db.Devices
            .Where(d => !string.IsNullOrEmpty(d.OsVersion))
            .GroupBy(d => d.OsVersion).Select(g => new { os = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count).Take(5).ToListAsync(ct);

        // CTP-11 dual aliasing: both time-window names AND semantic names
        return Ok(new {
            total,
            totalDevices = total,            // semantic alias
            activeLastDay,
            activeToday = activeLastDay,     // semantic alias
            activeLastWeek,
            activeThisWeek = activeLastWeek, // semantic alias
            activeLastMonth,
            newThisWeek = activeLastMonth,   // approximate semantic alias
            topOs
        });
    }
}
