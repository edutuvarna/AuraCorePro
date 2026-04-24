using System.Runtime.CompilerServices;
using System.Text;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Superadmin;

[ApiController]
[Route("api/superadmin/admin-actions")]
[Authorize(Roles = "superadmin")]
public sealed class AdminActionLogController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    public AdminActionLogController(AuraCoreDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? actorEmail, [FromQuery] string? action,
        [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (pageSize > 200) pageSize = 200;
        var adminActorIds = _db.Users.Where(u => u.Role == "admin").Select(u => u.Id);
        var q = _db.AuditLogs.Where(a => a.ActorId != null && adminActorIds.Contains(a.ActorId.Value));

        if (!string.IsNullOrEmpty(actorEmail)) q = q.Where(a => a.ActorEmail.Contains(actorEmail));
        if (!string.IsNullOrEmpty(action)) q = q.Where(a => a.Action == action);
        if (dateFrom.HasValue) q = q.Where(a => a.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue) q = q.Where(a => a.CreatedAt <= dateTo.Value);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new { a.Id, a.ActorEmail, a.ActorId, a.Action, a.TargetType, a.TargetId, a.IpAddress, a.CreatedAt })
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct)
    {
        var adminActorIds = _db.Users.Where(u => u.Role == "admin").Select(u => u.Id);
        var baseQ = _db.AuditLogs.Where(a => a.ActorId != null && adminActorIds.Contains(a.ActorId.Value));

        var total = await baseQ.CountAsync(ct);
        var cutoff24 = DateTimeOffset.UtcNow.AddDays(-1);
        var cutoff7  = DateTimeOffset.UtcNow.AddDays(-7);
        var last24h = await baseQ.CountAsync(a => a.CreatedAt > cutoff24, ct);
        var last7d  = await baseQ.CountAsync(a => a.CreatedAt > cutoff7, ct);

        var topAdmins = await baseQ.GroupBy(a => a.ActorEmail)
            .Select(g => new { email = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count).Take(5).ToListAsync(ct);
        var topActions = await baseQ.GroupBy(a => a.Action)
            .Select(g => new { action = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count).Take(5).ToListAsync(ct);

        return Ok(new { total, last24h, last7d, topAdmins, topActions });
    }

    [HttpGet("export.csv")]
    public async Task ExportCsv(
        [FromQuery] string? actorEmail, [FromQuery] string? action,
        [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo,
        CancellationToken ct = default)
    {
        var adminActorIds = _db.Users.Where(u => u.Role == "admin").Select(u => u.Id);
        IQueryable<AuditLogEntry> q = _db.AuditLogs.Where(a => a.ActorId != null && adminActorIds.Contains(a.ActorId.Value));
        if (!string.IsNullOrEmpty(actorEmail)) q = q.Where(a => a.ActorEmail.Contains(actorEmail));
        if (!string.IsNullOrEmpty(action))     q = q.Where(a => a.Action == action);
        if (dateFrom.HasValue) q = q.Where(a => a.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue)   q = q.Where(a => a.CreatedAt <= dateTo.Value);

        Response.ContentType = "text/csv; charset=utf-8";
        Response.Headers["Content-Disposition"] = $"attachment; filename=\"admin-actions-{DateTime.UtcNow:yyyyMMddHHmmss}.csv\"";
        await WriteCsvAsync(q.AsAsyncEnumerable(), Response.Body, ct);
    }

    internal static async Task WriteCsvAsync(IAsyncEnumerable<AuditLogEntry> rows, Stream output, CancellationToken ct)
    {
        await using var writer = new StreamWriter(output, Encoding.UTF8, leaveOpen: true);
        await writer.WriteLineAsync("\"id\",\"actor_email\",\"actor_id\",\"action\",\"target_type\",\"target_id\",\"ip_address\",\"created_at_utc\"");
        await foreach (var r in rows.WithCancellation(ct))
        {
            var line =
                $"\"{r.Id}\"," +
                $"\"{Esc(r.ActorEmail)}\"," +
                $"\"{r.ActorId?.ToString() ?? ""}\"," +
                $"\"{Esc(r.Action)}\"," +
                $"\"{Esc(r.TargetType)}\"," +
                $"\"{Esc(r.TargetId ?? "")}\"," +
                $"\"{Esc(r.IpAddress ?? "")}\"," +
                $"\"{r.CreatedAt.UtcDateTime:o}\"";
            await writer.WriteLineAsync(line);
        }
    }

    internal static string Esc(string s) => s?.Replace("\"", "\"\"") ?? "";
}
