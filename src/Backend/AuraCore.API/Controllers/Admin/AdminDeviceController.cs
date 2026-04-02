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

        var query = _db.Devices.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(d => d.MachineName.Contains(search) || d.OsVersion.Contains(search));

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(d => d.LastSeenAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new
            {
                d.Id, d.LicenseId, d.MachineName, d.OsVersion,
                d.HardwareFingerprint, d.RegisteredAt, d.LastSeenAt,
                licenseTier = d.License.Tier,
                userEmail = d.License.User.Email
            })
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var total = await _db.Devices.CountAsync(ct);
        var activeLastDay = await _db.Devices.CountAsync(d => d.LastSeenAt > DateTimeOffset.UtcNow.AddHours(-24), ct);
        var activeLastWeek = await _db.Devices.CountAsync(d => d.LastSeenAt > DateTimeOffset.UtcNow.AddDays(-7), ct);
        var activeLastMonth = await _db.Devices.CountAsync(d => d.LastSeenAt > DateTimeOffset.UtcNow.AddDays(-30), ct);

        var topOs = await _db.Devices
            .GroupBy(d => d.OsVersion)
            .Select(g => new { osVersion = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .Take(10)
            .ToListAsync(ct);

        return Ok(new { total, activeLastDay, activeLastWeek, activeLastMonth, topOs });
    }
}
