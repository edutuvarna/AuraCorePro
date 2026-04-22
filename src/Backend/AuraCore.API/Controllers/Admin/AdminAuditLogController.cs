using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Admin;

[ApiController]
[Authorize(Roles = "admin")]
public sealed class AdminAuditLogController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    public AdminAuditLogController(AuraCoreDbContext db) => _db = db;

    // New primary route — reads from audit_log
    [HttpGet("api/admin/audit-log")]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? action = null,
        [FromQuery] string? actorEmail = null,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var q = _db.AuditLogs.AsQueryable();
        if (!string.IsNullOrEmpty(action))       q = q.Where(a => a.Action == action);
        if (!string.IsNullOrEmpty(actorEmail))   q = q.Where(a => a.ActorEmail.Contains(actorEmail));

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new {
                a.Id, a.ActorEmail, a.Action, a.TargetType, a.TargetId,
                a.CreatedAt, a.IpAddress,
                actorId = a.ActorId
            })
            .ToListAsync(ct);

        return Ok(new {
            total, page, pageSize,
            pages = (int)Math.Ceiling((double)total / pageSize),
            items
        });
    }

    // Legacy alias — frontend sends here per audit F-2; redirect to new route
    [HttpGet("api/admin/audit/login-attempts")]
    public IActionResult LegacyAlias(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] string? action = null, [FromQuery] string? actorEmail = null)
    {
        var qs = $"?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(action)) qs += $"&action={Uri.EscapeDataString(action)}";
        if (!string.IsNullOrEmpty(actorEmail)) qs += $"&actorEmail={Uri.EscapeDataString(actorEmail)}";
        return RedirectPreserveMethod($"/api/admin/audit-log{qs}");
    }

    [HttpGet("api/admin/audit-log/stats")]
    public async Task<IActionResult> Stats(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var last24 = now.AddHours(-24);
        var last7d = now.AddDays(-7);

        var total = await _db.AuditLogs.CountAsync(ct);
        var last24hCount = await _db.AuditLogs.CountAsync(a => a.CreatedAt >= last24, ct);
        var last7dCount = await _db.AuditLogs.CountAsync(a => a.CreatedAt >= last7d, ct);
        var topActions = await _db.AuditLogs
            .GroupBy(a => a.Action)
            .Select(g => new { action = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count).Take(5)
            .ToListAsync(ct);

        // CTP-11 dual aliasing: both time-window names AND semantic names
        return Ok(new {
            total,
            last24h = last24hCount,
            today = last24hCount,       // semantic alias
            last7d = last7dCount,
            thisWeek = last7dCount,      // semantic alias
            topActions
        });
    }
}
