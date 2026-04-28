using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.API.Filters;
using AuraCore.API.Infrastructure.Data;
using AuraCore.API.Infrastructure.Services.Background;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AuraCore.API.Controllers.Superadmin;

[ApiController]
[Route("api/superadmin/audit-retention")]
[Authorize(Roles = "superadmin")]
public sealed class AuditRetentionController : ControllerBase
{
    private readonly AuraCoreDbContext _db;

    public AuditRetentionController(AuraCoreDbContext db) { _db = db; }

    [HttpGet("policy")]
    public async Task<IActionResult> GetPolicy(CancellationToken ct)
    {
        var settings = await _db.SystemSettings
            .Where(s => s.Key.StartsWith("audit_retention."))
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        var totalRows = await _db.AuditLogs.CountAsync(ct);
        var oldestAt = totalRows > 0
            ? await _db.AuditLogs.MinAsync(a => (DateTimeOffset?)a.CreatedAt, ct)
            : (DateTimeOffset?)null;

        return Ok(new
        {
            retentionDays = settings.TryGetValue("audit_retention.retentionDays", out var d) && int.TryParse(d, out var dn) ? dn : 365,
            lastRunAt = settings.TryGetValue("audit_retention.lastRunAt", out var l) && DateTimeOffset.TryParse(l, out var lr) ? (DateTimeOffset?)lr : null,
            lastRunDeletedRows = settings.TryGetValue("audit_retention.lastRunDeletedRows", out var n) && int.TryParse(n, out var nn) ? nn : 0,
            totalRows,
            oldestAt,
        });
    }

    [HttpPost("policy")]
    [AuditAction("AuditRetentionPolicySet", "System")]
    public async Task<IActionResult> SetPolicy([FromBody] SetRetentionDto dto, CancellationToken ct)
    {
        if (dto.RetentionDays < 30 || dto.RetentionDays > 3650)
            return BadRequest(new { error = "Retention must be 30-3650 days" });

        var existing = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == "audit_retention.retentionDays", ct);
        if (existing == null)
            _db.SystemSettings.Add(new Domain.Entities.SystemSetting { Key = "audit_retention.retentionDays", Value = dto.RetentionDays.ToString(), UpdatedAt = DateTimeOffset.UtcNow });
        else
        {
            existing.Value = dto.RetentionDays.ToString();
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        return Ok(new { retentionDays = dto.RetentionDays });
    }

    [HttpPost("run-now")]
    [AuditAction("AuditRetentionRunNow", "System")]
    public async Task<IActionResult> RunNow(CancellationToken ct)
    {
        var deleted = await AuditLogCleanupService.RunCleanupAsync(_db, NullLogger.Instance, ct);
        return Ok(new { deleted });
    }
}

public record SetRetentionDto(int RetentionDays);
