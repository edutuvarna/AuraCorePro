using AuraCore.API.Application.Services.Email;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Helpers;
using AuraCore.API.Hubs;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Superadmin;

[ApiController]
[Route("api/superadmin")]
[Authorize(Roles = "superadmin")]
public sealed class PermissionGrantsController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    private readonly IEmailService _email;
    private readonly IHubContext<AdminHub> _hub;

    public PermissionGrantsController(AuraCoreDbContext db, IEmailService email, IHubContext<AdminHub> hub)
    {
        _db = db; _email = email; _hub = hub;
    }

    [HttpGet("permission-requests")]
    public async Task<IActionResult> ListRequests([FromQuery] string? status = "pending", CancellationToken ct = default)
    {
        IQueryable<PermissionRequest> q = _db.PermissionRequests;
        if (!string.IsNullOrEmpty(status)) q = q.Where(r => r.Status == status);
        var items = await q
            .OrderByDescending(r => r.RequestedAt)
            .Take(200)
            .Select(r => new {
                r.Id, r.PermissionKey, r.Reason, r.Status, r.RequestedAt, r.ReviewedAt, r.ReviewNote,
                adminEmail = _db.Users.Where(u => u.Id == r.AdminUserId).Select(u => u.Email).FirstOrDefault(),
            })
            .ToListAsync(ct);
        return Ok(new { items });
    }

    [HttpPost("permission-requests/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveDto dto, CancellationToken ct)
    {
        var superId = User.GetUserId();
        if (superId is null) return Unauthorized();

        var req = await _db.PermissionRequests.FirstOrDefaultAsync(r => r.Id == id && r.Status == "pending", ct);
        if (req is null) return NotFound();

        req.Status = "approved";
        req.ReviewedBy = superId;
        req.ReviewedAt = DateTimeOffset.UtcNow;
        req.ReviewNote = dto?.ReviewNote;

        _db.PermissionGrants.Add(new PermissionGrant {
            AdminUserId = req.AdminUserId,
            PermissionKey = req.PermissionKey,
            GrantedBy = superId.Value,
            GrantedAt = DateTimeOffset.UtcNow,
            ExpiresAt = dto?.ExpiresAt,
            SourceRequestId = req.Id,
        });
        await _db.SaveChangesAsync(ct);

        var admin = await _db.Users.FirstAsync(u => u.Id == req.AdminUserId, ct);
        await _hub.Clients.User(admin.Id.ToString()).SendAsync("PermissionApproved",
            new { permissionKey = req.PermissionKey, expiresAt = dto?.ExpiresAt }, ct);
        await _email.SendFromTemplateAsync(EmailTemplate.PermissionApproved, new {
            to = admin.Email, permissionKey = req.PermissionKey,
            approvedBy = User.GetEmail() ?? "superadmin",
            expiresNote = dto?.ExpiresAt is null ? "This grant does not expire." : $"Expires at {dto.ExpiresAt:u}.",
        }, ct);
        return Ok(new { id = req.Id, status = req.Status });
    }

    [HttpPost("permission-requests/{id:guid}/deny")]
    public async Task<IActionResult> Deny(Guid id, [FromBody] DenyDto dto, CancellationToken ct)
    {
        var superId = User.GetUserId();
        if (superId is null) return Unauthorized();

        var req = await _db.PermissionRequests.FirstOrDefaultAsync(r => r.Id == id && r.Status == "pending", ct);
        if (req is null) return NotFound();

        req.Status = "denied";
        req.ReviewedBy = superId;
        req.ReviewedAt = DateTimeOffset.UtcNow;
        req.ReviewNote = dto?.ReviewNote;
        await _db.SaveChangesAsync(ct);

        var admin = await _db.Users.FirstAsync(u => u.Id == req.AdminUserId, ct);
        await _hub.Clients.User(admin.Id.ToString()).SendAsync("PermissionDenied",
            new { permissionKey = req.PermissionKey, reviewNote = dto?.ReviewNote }, ct);
        await _email.SendFromTemplateAsync(EmailTemplate.PermissionDenied, new {
            to = admin.Email, permissionKey = req.PermissionKey,
            deniedBy = User.GetEmail() ?? "superadmin",
            reviewNote = dto?.ReviewNote ?? "(no note provided)",
        }, ct);
        return Ok(new { id = req.Id, status = req.Status });
    }

    [HttpPost("permission-requests/bulk/approve")]
    public async Task<IActionResult> BulkApprove([FromBody] BulkIdsDto dto, CancellationToken ct)
    {
        var results = new List<object>();
        foreach (var id in dto.Ids)
        {
            var r = await Approve(id, new ApproveDto(null, null), ct);
            results.Add(new { id, ok = r is OkObjectResult });
        }
        return Ok(new { results });
    }

    [HttpPost("permission-requests/bulk/deny")]
    public async Task<IActionResult> BulkDeny([FromBody] BulkIdsDto dto, CancellationToken ct)
    {
        var results = new List<object>();
        foreach (var id in dto.Ids)
        {
            var r = await Deny(id, new DenyDto(null), ct);
            results.Add(new { id, ok = r is OkObjectResult });
        }
        return Ok(new { results });
    }

    [HttpPost("permission-grants/revoke")]
    public async Task<IActionResult> Revoke([FromBody] RevokeDto dto, CancellationToken ct)
    {
        var superId = User.GetUserId();
        if (superId is null) return Unauthorized();

        var grant = await _db.PermissionGrants
            .Where(g => g.AdminUserId == dto.AdminUserId && g.PermissionKey == dto.PermissionKey && g.RevokedAt == null)
            .FirstOrDefaultAsync(ct);
        if (grant is null) return NotFound();

        grant.RevokedAt = DateTimeOffset.UtcNow;
        grant.RevokedBy = superId;
        grant.RevokeReason = dto.Reason;
        await _db.SaveChangesAsync(ct);

        await _hub.Clients.User(dto.AdminUserId.ToString()).SendAsync("PermissionRevoked",
            new { permissionKey = dto.PermissionKey, reason = dto.Reason }, ct);
        return Ok(new { id = grant.Id, revokedAt = grant.RevokedAt });
    }

    public sealed record ApproveDto(DateTimeOffset? ExpiresAt, string? ReviewNote);
    public sealed record DenyDto(string? ReviewNote);
    public sealed record BulkIdsDto(List<Guid> Ids);
    public sealed record RevokeDto(Guid AdminUserId, string PermissionKey, string Reason);
}
