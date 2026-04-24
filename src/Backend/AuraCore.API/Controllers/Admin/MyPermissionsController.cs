using AuraCore.API.Helpers;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Admin;

[ApiController]
[Route("api/admin/my-permissions")]
[Authorize(Roles = "admin")]
public sealed class MyPermissionsController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    public MyPermissionsController(AuraCoreDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();

        var grants = await _db.PermissionGrants
            .Where(g => g.AdminUserId == userId.Value && g.RevokedAt == null)
            .Select(g => new {
                g.PermissionKey, g.GrantedAt, g.ExpiresAt,
                grantedByEmail = _db.Users.Where(u => u.Id == g.GrantedBy).Select(u => u.Email).FirstOrDefault(),
                g.SourceRequestId,
            })
            .ToListAsync(ct);

        var pending = await _db.PermissionRequests
            .Where(r => r.AdminUserId == userId.Value && r.Status == "pending")
            .Select(r => new { r.Id, r.PermissionKey, r.Reason, r.RequestedAt })
            .ToListAsync(ct);

        var denied = await _db.PermissionRequests
            .Where(r => r.AdminUserId == userId.Value && r.Status == "denied")
            .OrderByDescending(r => r.ReviewedAt)
            .Take(10)
            .Select(r => new { r.PermissionKey, r.ReviewNote, r.ReviewedAt })
            .ToListAsync(ct);

        var totalRestricted = PermissionKeys.AllKeys.Count;
        return Ok(new {
            totalRestricted,
            activeGrantsCount = grants.Count,
            grants,
            pending,
            recentDenials = denied,
        });
    }
}
