using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Admin;

[ApiController]
[Route("api/admin/audit-log")]
[Authorize(Roles = "admin")]
public sealed class AdminAuditLogController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    public AdminAuditLogController(AuraCoreDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? email = null,
        [FromQuery] string? ipAddress = null,
        [FromQuery] bool? success = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (pageSize > 100) pageSize = 100;
        if (pageSize < 1) pageSize = 10;
        if (page < 1) page = 1;

        var query = _db.LoginAttempts.AsQueryable();

        if (!string.IsNullOrWhiteSpace(email))
            query = query.Where(a => a.Email.Contains(email));

        if (!string.IsNullOrWhiteSpace(ipAddress))
            query = query.Where(a => a.IpAddress == ipAddress);

        if (success.HasValue)
            query = query.Where(a => a.Success == success.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id, a.Email, a.IpAddress, a.Success, a.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var total = await _db.LoginAttempts.CountAsync(ct);
        var successful = await _db.LoginAttempts.CountAsync(a => a.Success, ct);
        var failed = await _db.LoginAttempts.CountAsync(a => !a.Success, ct);
        var last24h = await _db.LoginAttempts.CountAsync(a => a.CreatedAt > DateTimeOffset.UtcNow.AddHours(-24), ct);
        var failedLast24h = await _db.LoginAttempts.CountAsync(a => !a.Success && a.CreatedAt > DateTimeOffset.UtcNow.AddHours(-24), ct);

        var topFailedIps = await _db.LoginAttempts
            .Where(a => !a.Success && a.CreatedAt > DateTimeOffset.UtcNow.AddDays(-7))
            .GroupBy(a => a.IpAddress)
            .Select(g => new { ipAddress = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .Take(10)
            .ToListAsync(ct);

        var topFailedEmails = await _db.LoginAttempts
            .Where(a => !a.Success && a.CreatedAt > DateTimeOffset.UtcNow.AddDays(-7))
            .GroupBy(a => a.Email)
            .Select(g => new { email = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .Take(10)
            .ToListAsync(ct);

        return Ok(new { total, successful, failed, last24h, failedLast24h, topFailedIps, topFailedEmails });
    }
}
