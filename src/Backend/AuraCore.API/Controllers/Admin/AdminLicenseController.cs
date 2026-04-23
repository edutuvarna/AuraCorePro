using AuraCore.API.Domain.Entities;
using AuraCore.API.Filters;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Admin;

[ApiController]
[Route("api/admin/licenses")]
[Authorize(Roles = "admin")]
public sealed class AdminLicenseController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    public AdminLicenseController(AuraCoreDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] string? tier = null, [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var q = _db.Licenses.AsNoTracking().Include(l => l.User).AsQueryable();
        if (!string.IsNullOrEmpty(tier))   q = q.Where(l => l.Tier == tier);
        if (!string.IsNullOrEmpty(status)) q = q.Where(l => l.Status == status);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(l => new {
                l.Id, l.Key, l.Tier, l.Status, l.MaxDevices, l.CreatedAt, l.ExpiresAt,
                userId = l.UserId, userEmail = l.User != null ? l.User.Email : null,
                // Phase 6.10 W5.T25: dropped Phase 6.8 `activeDevices` alias —
                // frontend rebuild converged on `deviceCount` as the canonical name.
                deviceCount = l.Devices.Count()
            })
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, pages = (int)Math.Ceiling((double)total / pageSize), items });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var l = await _db.Licenses.AsNoTracking()
            .Include(x => x.User).Include(x => x.Devices)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (l is null) return NotFound(new { error = "License not found" });

        return Ok(new {
            l.Id, l.Key, l.Tier, l.Status, l.MaxDevices, l.CreatedAt, l.ExpiresAt,
            user = l.User is null ? null : new { l.User.Id, l.User.Email },
            devices = l.Devices.Select(d => new { d.Id, d.MachineName, d.LastSeenAt })
        });
    }

    [HttpPut("{id:guid}/revoke")]
    [AuditAction("RevokeLicense", "License", TargetIdFromRouteKey = "id")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        var l = await _db.Licenses.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (l is null) return NotFound(new { error = "License not found" });

        l.Status = "revoked";
        l.Tier = "free";  // fully-revoke: both status AND tier (fixes audit F-5 in licenses.md)
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "License revoked", l.Id, l.Status, l.Tier });
    }

    [HttpPut("{id:guid}/activate")]
    [AuditAction("ActivateLicense", "License", TargetIdFromRouteKey = "id")]
    public async Task<IActionResult> Activate(Guid id, [FromBody] ActivateLicenseRequest req, CancellationToken ct)
    {
        var l = await _db.Licenses.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (l is null) return NotFound(new { error = "License not found" });

        l.Status = "active";
        if (!string.IsNullOrEmpty(req.Tier)) l.Tier = req.Tier;
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "License activated", l.Id, l.Status, l.Tier });
    }
}

public sealed record ActivateLicenseRequest(string? Tier);
