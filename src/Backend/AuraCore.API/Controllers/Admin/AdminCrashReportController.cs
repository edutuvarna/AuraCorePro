using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Admin;

[ApiController]
[Route("api/admin/crash-reports")]
[Authorize(Roles = "admin")]
public sealed class AdminCrashReportController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    public AdminCrashReportController(AuraCoreDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search = null,
        [FromQuery] string? appVersion = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (pageSize > 100) pageSize = 100;
        if (pageSize < 1) pageSize = 10;
        if (page < 1) page = 1;

        var query = _db.CrashReports.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.ExceptionType.Contains(search));

        if (!string.IsNullOrWhiteSpace(appVersion))
            query = query.Where(c => c.AppVersion == appVersion);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id, c.DeviceId, c.AppVersion, c.ExceptionType, c.CreatedAt,
                deviceName = c.Device.MachineName
            })
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, pages = (int)Math.Ceiling((double)total / pageSize), items });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var report = await _db.CrashReports.Include(c => c.Device)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (report is null) return NotFound();

        return Ok(new
        {
            report.Id, report.DeviceId, report.AppVersion,
            report.ExceptionType, report.StackTrace, report.SystemInfo,
            report.CreatedAt,
            deviceName = report.Device.MachineName,
            osVersion = report.Device.OsVersion
        });
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var total = await _db.CrashReports.CountAsync(ct);
        var last24h = await _db.CrashReports.CountAsync(c => c.CreatedAt > DateTimeOffset.UtcNow.AddHours(-24), ct);
        var last7d = await _db.CrashReports.CountAsync(c => c.CreatedAt > DateTimeOffset.UtcNow.AddDays(-7), ct);
        var last30d = await _db.CrashReports.CountAsync(c => c.CreatedAt > DateTimeOffset.UtcNow.AddDays(-30), ct);

        var topExceptions = await _db.CrashReports
            .GroupBy(c => c.ExceptionType)
            .Select(g => new { exceptionType = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .Take(10)
            .ToListAsync(ct);

        var topVersions = await _db.CrashReports
            .GroupBy(c => c.AppVersion)
            .Select(g => new { appVersion = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .Take(10)
            .ToListAsync(ct);

        return Ok(new {
            total,
            last24h, today = last24h,         // CTP-11 semantic alias
            last7d, thisWeek = last7d,         // CTP-11 semantic alias
            last30d, thisMonth = last30d,      // CTP-11 semantic alias
            topExceptions, topVersions
        });
    }

    [HttpDelete("{id:guid}")]
    [AuraCore.API.Filters.AuditAction("DeleteCrashReport", "CrashReport", TargetIdFromRouteKey = "id")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var report = await _db.CrashReports.FindAsync(new object[] { id }, ct);
        if (report is null) return NotFound();

        _db.CrashReports.Remove(report);
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Crash report deleted" });
    }
}
