using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Admin;

[ApiController]
[Route("api/admin/audit-log")]
[Authorize(Roles = "admin")]
public sealed class AuditLogExportController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    public AuditLogExportController(AuraCoreDbContext db) => _db = db;

    [HttpGet("export.csv")]
    public async Task ExportCsv(
        [FromQuery] string? actorEmail, [FromQuery] string? action,
        [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo,
        CancellationToken ct = default)
    {
        IQueryable<AuditLogEntry> q = _db.AuditLogs;
        if (!string.IsNullOrEmpty(actorEmail)) q = q.Where(a => a.ActorEmail.Contains(actorEmail));
        if (!string.IsNullOrEmpty(action))     q = q.Where(a => a.Action == action);
        if (dateFrom.HasValue) q = q.Where(a => a.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue)   q = q.Where(a => a.CreatedAt <= dateTo.Value);

        Response.ContentType = "text/csv; charset=utf-8";
        Response.Headers["Content-Disposition"] = $"attachment; filename=\"audit-log-{DateTime.UtcNow:yyyyMMddHHmmss}.csv\"";
        await Superadmin.AdminActionLogController.WriteCsvAsync(q.AsAsyncEnumerable(), Response.Body, ct);
    }
}
