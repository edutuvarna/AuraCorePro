using AuraCore.API.Filters;
using AuraCore.API.Helpers;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Admin;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "admin")]
public sealed class AdminUserController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    public AdminUserController(AuraCoreDbContext db) => _db = db;

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

        var query = _db.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u => u.Email.Contains(search));

        var total = await query.CountAsync(ct);

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.Id, u.Email, u.Role, u.CreatedAt,
                // CTP-1: top-level tier for frontend compatibility (u.tier).
                // Mirrors AdminUserController.GetById's projection pattern.
                tier = _db.Licenses
                    .Where(l => l.UserId == u.Id && l.Status == "active")
                    .Select(l => l.Tier)
                    .FirstOrDefault() ?? "free",
                // Nested license object retained for callers that read u.license.tier.
                license = _db.Licenses
                    .Where(l => l.UserId == u.Id && l.Status == "active")
                    .Select(l => new { l.Tier, l.ExpiresAt })
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, pages = (int)Math.Ceiling((double)total / pageSize), users });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync(new object[] { id }, ct);
        if (user is null) return NotFound();

        var license = await _db.Licenses
            .FirstOrDefaultAsync(l => l.UserId == id && l.Status == "active", ct);

        return Ok(new
        {
            user.Id, user.Email, user.Role, user.CreatedAt,
            tier = license?.Tier ?? "free",
            expiresAt = license?.ExpiresAt
        });
    }

    [HttpPost("reset-password")]
    [AuraCore.API.Filters.AuditAction("ResetPassword", "User")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email, ct);
        if (user is null)
        {
            // Always return success to prevent email enumeration
            return Ok(new { message = "If this email is registered, password has been reset." });
        }

        // Phase 6.13 hotfix: silently no-op on admin/superadmin targets so this
        // endpoint cannot be used as an account-takeover vector. Admin password
        // resets must go through the superadmin-gated AdminManagementController
        // which issues single-use invitation-style tokens. The opaque response
        // mirrors the email-enumeration protection above.
        if (user.Role == "admin" || user.Role == "superadmin")
            return Ok(new { message = "If this email is registered, password has been reset." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new { message = "If this email is registered, password has been reset." });
    }

    [HttpDelete("{id:guid}")]
    [RequiresPermission(PermissionKeys.ActionUsersDelete)]
    [AuraCore.API.Filters.AuditAction("DeleteUser", "User", TargetIdFromRouteKey = "id")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync(new object[] { id }, ct);
        if (user is null) return NotFound();

        // Prevent deleting yourself
        var callerId = User.FindFirst("sub")?.Value;
        if (callerId == id.ToString())
            return BadRequest(new { error = "Cannot delete your own account" });

        // Phase 6.13 hotfix: prevent admin/superadmin deletion via this endpoint.
        // Admin lifecycle (delete/promote/demote) is gated behind the
        // superadmin-only AdminManagementController. Without this guard, any
        // admin holding the ActionUsersDelete permission could nuke a peer
        // admin — or the superadmin — through the regular users API.
        if (user.Role == "admin" || user.Role == "superadmin")
            return BadRequest(new { error = "Admin accounts must be removed via the superadmin-only AdminManagement endpoint." });

        // Delete related records first (foreign key constraints)
        var refreshTokens = await _db.RefreshTokens.Where(r => r.UserId == id).ToListAsync(ct);
        _db.RefreshTokens.RemoveRange(refreshTokens);

        var loginAttempts = await _db.LoginAttempts.Where(a => a.Email == user.Email).ToListAsync(ct);
        _db.LoginAttempts.RemoveRange(loginAttempts);

        // Load licenses with their devices eagerly, then capture deviceIds BEFORE
        // any RemoveRange. Collecting deviceIds AFTER RemoveRange (the old pattern)
        // returned empty because EF's change tracker excludes Deleted-state entities
        // from subsequent queries — CTP-5 / users.md F-3.
        var licenses = await _db.Licenses
            .Where(l => l.UserId == id)
            .Include(l => l.Devices)
            .ToListAsync(ct);
        var deviceIds = licenses.SelectMany(l => l.Devices).Select(d => d.Id).ToList();

        if (deviceIds.Count > 0)
        {
            var crashReports = await _db.CrashReports.Where(c => deviceIds.Contains(c.DeviceId)).ToListAsync(ct);
            _db.CrashReports.RemoveRange(crashReports);

            var telemetry = await _db.TelemetryEvents.Where(t => deviceIds.Contains(t.DeviceId)).ToListAsync(ct);
            _db.TelemetryEvents.RemoveRange(telemetry);
        }

        foreach (var lic in licenses)
            _db.Devices.RemoveRange(lic.Devices);
        _db.Licenses.RemoveRange(licenses);

        var payments = await _db.Payments.Where(p => p.UserId == id).ToListAsync(ct);
        _db.Payments.RemoveRange(payments);

        var subscriptions = await _db.Subscriptions.Where(s => s.UserId == id).ToListAsync(ct);
        _db.Subscriptions.RemoveRange(subscriptions);

        _db.Users.Remove(user);
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = $"User {user.Email} deleted" });
    }

    [HttpPost("{id:guid}/ban")]
    [RequiresPermission(PermissionKeys.ActionUsersBan)]
    [AuraCore.API.Filters.AuditAction("BanUser", "User", TargetIdFromRouteKey = "id")]
    public async Task<IActionResult> BanUser(Guid id, [FromBody] BanUserRequest req, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync(new object[] { id }, ct);
        if (user is null) return NotFound();

        // Prevent banning yourself / other admins through this endpoint.
        if (user.Role == "admin" || user.Role == "superadmin")
            return BadRequest(new { error = "Admin accounts cannot be banned via this endpoint; use Suspend instead." });

        user.IsActive = !req.Banned ? true : false;
        await _db.SaveChangesAsync(ct);
        return Ok(new { id = user.Id, email = user.Email, isActive = user.IsActive });
    }
}

public sealed class ResetPasswordRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    public string Email { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
    [System.ComponentModel.DataAnnotations.MaxLength(128, ErrorMessage = "Password too long")]
    public string NewPassword { get; set; } = string.Empty;
}

public sealed record BanUserRequest(bool Banned);
