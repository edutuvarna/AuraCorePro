using System.Security.Cryptography;
using AuraCore.API.Application.Services.Email;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Filters;
using AuraCore.API.Helpers;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Superadmin;

[ApiController]
[Route("api/superadmin")]
[Authorize(Roles = "superadmin")]
public sealed class AdminManagementController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    private readonly IEmailService _email;

    public AdminManagementController(AuraCoreDbContext db, IEmailService email)
    {
        _db = db;
        _email = email;
    }

    [HttpGet("admins")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await _db.Users
            .Where(u => u.Role == "admin" || u.Role == "superadmin")
            .OrderBy(u => u.Email)
            .Select(u => new {
                id = u.Id,
                email = u.Email,
                role = u.Role,
                isActive = u.IsActive,
                isReadonly = u.IsReadonly,
                totpEnabled = u.TotpEnabled,
                require2fa = u.Require2fa,
                createdAt = u.CreatedAt,
                createdVia = u.CreatedVia,
                createdByEmail = _db.Users.Where(cu => cu.Id == u.CreatedByUserId).Select(cu => cu.Email).FirstOrDefault(),
            })
            .ToListAsync(ct);
        return Ok(new { items });
    }

    [HttpPost("admins")]
    [AuditAction("CreateAdmin", "User")]
    public async Task<IActionResult> Create([FromBody] CreateAdminDto dto, CancellationToken ct)
    {
        var superId = User.GetUserId()!.Value;

        if (string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest(new { error = "email_required" });
        var email = dto.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            return BadRequest(new { error = "email_exists" });

        if (!dto.SendInvitation && string.IsNullOrWhiteSpace(dto.InitialPassword))
            return BadRequest(new { error = "password_or_invitation_required" });
        if (!PermissionTemplates.IsValidTemplate(dto.Template))
            return BadRequest(new { error = "unknown_template" });

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = dto.SendInvitation
                ? BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")) // throwaway; replaced on redemption
                : BCrypt.Net.BCrypt.HashPassword(dto.InitialPassword!),
            Role = "admin",
            IsActive = true,
            IsReadonly = PermissionTemplates.RequiresIsReadonlyFlag(dto.Template),
            CreatedByUserId = superId,
            CreatedVia = "superadmin_create",
            Require2fa = dto.Require2fa,
            ForcePasswordChange = dto.ForcePasswordChange != "never",
            ForcePasswordChangeBy = ForceChangeDeadline(dto.ForcePasswordChange),
        };
        _db.Users.Add(user);

        // Apply template grants
        if (dto.Template == PermissionTemplates.Custom)
        {
            foreach (var ck in dto.CustomKeys ?? new List<CustomKey>())
            {
                if (!PermissionKeys.IsValidKey(ck.PermissionKey)) continue;
                _db.PermissionGrants.Add(new PermissionGrant
                {
                    AdminUserId = user.Id,
                    PermissionKey = ck.PermissionKey,
                    GrantedBy = superId,
                    ExpiresAt = ck.ExpiresAt,
                });
            }
        }
        else
        {
            foreach (var key in PermissionTemplates.GetPermissionsForTemplate(dto.Template))
            {
                _db.PermissionGrants.Add(new PermissionGrant
                {
                    AdminUserId = user.Id,
                    PermissionKey = key,
                    GrantedBy = superId,
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        if (dto.SendInvitation)
        {
            var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace("+", "-").Replace("/", "_").TrimEnd('=');
            var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
            var inv = new AdminInvitation
            {
                TokenHash = hash,
                AdminUserId = user.Id,
                CreatedBy = superId,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            };
            _db.AdminInvitations.Add(inv);
            await _db.SaveChangesAsync(ct);

            var setupLink = $"https://admin.auracore.pro/#/invite?token={raw}&email={Uri.EscapeDataString(email)}";
            await _email.SendFromTemplateAsync(EmailTemplate.AdminInvitation, new
            {
                to = email,
                adminEmail = email,
                invitedBy = User.GetEmail() ?? "superadmin",
                setupLink,
                expiresAt = inv.ExpiresAt.ToString("u"),
            }, ct);
        }
        else
        {
            // Let the superadmin know the account exists + has a manually-set password
            // they must share out-of-band (password manager, etc.).
            await _email.SendFromTemplateAsync(EmailTemplate.AdminCreatedWithoutEmail, new
            {
                to = User.GetEmail() ?? email,
                adminEmail = email,
                note = "The initial password must be shared with the admin out-of-band (e.g. via password manager).",
            }, ct);
        }

        return Ok(new { id = user.Id, email = user.Email, template = dto.Template });
    }

    [HttpPost("admins/{id:guid}/suspend")]
    [AuditAction("SuspendAdmin", "User", TargetIdFromRouteKey = "id")]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken ct)
    {
        var u = await _db.Users.FindAsync(new object[] { id }, ct);
        if (u is null) return NotFound();
        if (u.Role != "admin") return BadRequest(new { error = "only_admin_can_be_suspended" });
        u.IsActive = false;

        // Revoke all outstanding refresh tokens. Access-token jtis that are still
        // valid are handled separately — we don't have their jtis here, but the
        // access-token expiry is 15min so effective lockout is ≤15min.
        var refreshes = await _db.RefreshTokens.Where(r => r.UserId == id && !r.IsRevoked).ToListAsync(ct);
        foreach (var r in refreshes) r.IsRevoked = true;
        await _db.SaveChangesAsync(ct);
        return Ok(new { id, isActive = false });
    }

    [HttpPost("admins/{id:guid}/restore")]
    [AuditAction("RestoreAdmin", "User", TargetIdFromRouteKey = "id")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct)
    {
        var u = await _db.Users.FindAsync(new object[] { id }, ct);
        if (u is null) return NotFound();
        u.IsActive = true;
        await _db.SaveChangesAsync(ct);
        return Ok(new { id, isActive = true });
    }

    [HttpPost("admins/{id:guid}/reset-password")]
    [AuditAction("ResetAdminPassword", "User", TargetIdFromRouteKey = "id")]
    public async Task<IActionResult> ResetPassword(Guid id, CancellationToken ct)
    {
        var u = await _db.Users.FindAsync(new object[] { id }, ct);
        if (u is null) return NotFound();

        // Issue a single-use invitation-style token for password reset.
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');
        var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(3);
        _db.AdminInvitations.Add(new AdminInvitation
        {
            TokenHash = hash,
            AdminUserId = u.Id,
            CreatedBy = User.GetUserId()!.Value,
            ExpiresAt = expiresAt,
        });
        await _db.SaveChangesAsync(ct);

        var setupLink = $"https://admin.auracore.pro/#/invite?token={raw}&email={Uri.EscapeDataString(u.Email)}";
        await _email.SendFromTemplateAsync(EmailTemplate.AdminInvitation, new
        {
            to = u.Email,
            adminEmail = u.Email,
            invitedBy = User.GetEmail() ?? "superadmin",
            setupLink,
            expiresAt = expiresAt.ToString("u"),
        }, ct);
        return Ok(new { id = u.Id, reset = true });
    }

    [HttpDelete("admins/{id:guid}")]
    [RequiresPermission(PermissionKeys.ActionUsersDelete)]
    [AuditAction("DeleteAdmin", "User", TargetIdFromRouteKey = "id")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var u = await _db.Users.FindAsync(new object[] { id }, ct);
        if (u is null) return NotFound();
        if (u.Role == "superadmin") return BadRequest(new { error = "cannot_delete_superadmin" });

        // Delete dependents explicitly. Production Postgres has ON DELETE CASCADE
        // on these FKs (see Task 2), but EF's InMemory provider used in tests does
        // not emulate DB-level cascades. Doing it here keeps both paths in sync.
        var grants = await _db.PermissionGrants.Where(g => g.AdminUserId == id).ToListAsync(ct);
        _db.PermissionGrants.RemoveRange(grants);
        var requests = await _db.PermissionRequests.Where(r => r.AdminUserId == id).ToListAsync(ct);
        _db.PermissionRequests.RemoveRange(requests);
        var invitations = await _db.AdminInvitations.Where(i => i.AdminUserId == id).ToListAsync(ct);
        _db.AdminInvitations.RemoveRange(invitations);
        var refreshes = await _db.RefreshTokens.Where(r => r.UserId == id).ToListAsync(ct);
        _db.RefreshTokens.RemoveRange(refreshes);

        _db.Users.Remove(u);
        await _db.SaveChangesAsync(ct);
        return Ok(new { id });
    }

    [HttpPost("users/{userId:guid}/promote")]
    [AuditAction("PromoteUser", "User", TargetIdFromRouteKey = "userId")]
    public async Task<IActionResult> Promote(Guid userId, [FromBody] PromoteDto dto, CancellationToken ct)
    {
        var superId = User.GetUserId()!.Value;
        var u = await _db.Users.FindAsync(new object[] { userId }, ct);
        if (u is null) return NotFound();
        if (u.Role != "user") return BadRequest(new { error = "only_user_role_can_be_promoted" });
        if (!PermissionTemplates.IsValidTemplate(dto.Template))
            return BadRequest(new { error = "unknown_template" });

        u.Role = "admin";
        u.CreatedVia = "admin_promote";
        u.CreatedByUserId = superId;
        u.Require2fa = dto.Require2fa;
        u.IsReadonly = PermissionTemplates.RequiresIsReadonlyFlag(dto.Template);
        u.ForcePasswordChange = dto.ForcePasswordChange != "never";
        u.ForcePasswordChangeBy = ForceChangeDeadline(dto.ForcePasswordChange);

        if (dto.Template == PermissionTemplates.Custom)
        {
            foreach (var ck in dto.CustomKeys ?? new List<CustomKey>())
            {
                if (!PermissionKeys.IsValidKey(ck.PermissionKey)) continue;
                _db.PermissionGrants.Add(new PermissionGrant
                {
                    AdminUserId = u.Id,
                    PermissionKey = ck.PermissionKey,
                    GrantedBy = superId,
                    ExpiresAt = ck.ExpiresAt,
                });
            }
        }
        else
        {
            foreach (var key in PermissionTemplates.GetPermissionsForTemplate(dto.Template))
            {
                _db.PermissionGrants.Add(new PermissionGrant
                {
                    AdminUserId = u.Id,
                    PermissionKey = key,
                    GrantedBy = superId,
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { id = u.Id, role = u.Role });
    }

    [HttpPost("admins/{id:guid}/demote")]
    [AuditAction("DemoteAdmin", "User", TargetIdFromRouteKey = "id")]
    public async Task<IActionResult> Demote(Guid id, CancellationToken ct)
    {
        var u = await _db.Users.FindAsync(new object[] { id }, ct);
        if (u is null) return NotFound();
        if (u.Role != "admin") return BadRequest(new { error = "only_admin_can_be_demoted" });
        u.Role = "user";
        u.IsReadonly = false;

        // Revoke all outstanding grants as "demoted".
        var grants = await _db.PermissionGrants
            .Where(g => g.AdminUserId == id && g.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var g in grants)
        {
            g.RevokedAt = DateTimeOffset.UtcNow;
            g.RevokeReason = "demoted";
        }
        await _db.SaveChangesAsync(ct);
        return Ok(new { id, role = u.Role });
    }

    [HttpPost("admins/bulk-promote")]
    [AuditAction("BulkPromoteUsers", "User")]
    public async Task<IActionResult> BulkPromote([FromBody] BulkPromoteDto dto, CancellationToken ct)
    {
        if (dto?.UserIds == null || dto.UserIds.Length == 0)
            return BadRequest(new { error = "no_users_selected" });
        if (!PermissionTemplates.IsValidTemplate(dto.Template) || dto.Template == PermissionTemplates.Custom)
            return BadRequest(new { error = "invalid_template_for_bulk" });

        var users = await _db.Users.Where(u => dto.UserIds.Contains(u.Id)).ToListAsync(ct);
        if (users.Count != dto.UserIds.Length)
            return BadRequest(new { error = "one_or_more_user_ids_not_found" });
        if (users.Any(u => u.Role != "user"))
            return BadRequest(new { error = "some_users_not_in_user_role" });

        // Tolerate missing claims for direct controller-instantiation tests; in
        // production [Authorize(Roles="superadmin")] guarantees a ClaimsPrincipal.
        var superId = HttpContext?.User?.GetUserId();

        // Match the ApplyTemplate transaction-gating pattern: gate the tx on a relational
        // provider (production Postgres). InMemory (tests) returns false here so we
        // perform a best-effort SaveChanges without a transaction.
        await using var tx = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(ct)
            : null;
        try
        {
            var keys = PermissionTemplates.GetPermissionsForTemplate(dto.Template);
            var deadline = ForceChangeDeadline(dto.ForcePasswordChange);
            foreach (var u in users)
            {
                u.Role = "admin";
                u.CreatedVia = "admin_bulk_promote";
                u.CreatedByUserId = superId;
                u.IsReadonly = PermissionTemplates.RequiresIsReadonlyFlag(dto.Template);
                u.Require2fa = dto.Require2fa;
                u.ForcePasswordChange = dto.ForcePasswordChange != "never";
                u.ForcePasswordChangeBy = deadline;

                foreach (var key in keys)
                {
                    _db.PermissionGrants.Add(new PermissionGrant
                    {
                        AdminUserId = u.Id,
                        PermissionKey = key,
                        GrantedBy = superId ?? Guid.Empty,
                    });
                }
            }
            await _db.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);

            return Ok(new
            {
                succeeded = users.Count,
                failed = 0,
                promoted = users.Select(u => new { u.Id, u.Email, template = dto.Template }),
            });
        }
        catch (Exception ex)
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            return StatusCode(500, new { error = "bulk_promote_failed", detail = ex.Message });
        }
    }

    [HttpPost("admins/bulk-demote")]
    [AuditAction("BulkDemoteAdmins", "User")]
    public async Task<IActionResult> BulkDemote([FromBody] BulkDemoteDto dto, CancellationToken ct)
    {
        if (dto?.AdminIds == null || dto.AdminIds.Length == 0)
            return BadRequest(new { error = "no_admins_selected" });

        var admins = await _db.Users
            .Where(u => dto.AdminIds.Contains(u.Id) && u.Role == "admin")
            .ToListAsync(ct);
        if (admins.Count != dto.AdminIds.Length)
            return BadRequest(new { error = "some_ids_not_active_admins" });

        await using var tx = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(ct)
            : null;
        try
        {
            var ids = admins.Select(a => a.Id).ToHashSet();
            foreach (var u in admins)
            {
                u.Role = "user";
                u.IsReadonly = false;
            }
            var grants = await _db.PermissionGrants
                .Where(g => ids.Contains(g.AdminUserId) && g.RevokedAt == null)
                .ToListAsync(ct);
            var now = DateTimeOffset.UtcNow;
            foreach (var g in grants)
            {
                g.RevokedAt = now;
                g.RevokeReason = "bulk_demoted";
            }
            await _db.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);

            return Ok(new { succeeded = admins.Count, failed = 0 });
        }
        catch (Exception ex)
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            return StatusCode(500, new { error = "bulk_demote_failed", detail = ex.Message });
        }
    }

    [HttpPut("admins/{id:guid}/require-2fa")]
    public async Task<IActionResult> SetRequire2fa(Guid id, [FromBody] SetRequire2faDto dto, CancellationToken ct)
    {
        var u = await _db.Users.FindAsync(new object[] { id }, ct);
        if (u is null) return NotFound();
        u.Require2fa = dto.Require2fa;
        await _db.SaveChangesAsync(ct);
        return Ok(new { id, require2fa = u.Require2fa });
    }

    [HttpPost("admins/{id:guid}/apply-template")]
    [AuditAction("ApplyPermissionTemplate", "User", TargetIdFromRouteKey = "id")]
    public async Task<IActionResult> ApplyTemplate(Guid id, [FromBody] ApplyTemplateDto dto, CancellationToken ct)
    {
        var superId = User.GetUserId()!.Value;
        var target = await _db.Users.FindAsync(new object[] { id }, ct);
        if (target is null) return NotFound();
        if (target.Role != "admin") return BadRequest(new { error = "template_applies_to_admin_only" });
        if (!PermissionTemplates.IsValidTemplate(dto.Template))
            return BadRequest(new { error = "unknown_template" });

        // Atomic: revoke all active grants, then insert new ones, then flip is_readonly.
        // Production is always Postgres/Relational where BeginTransactionAsync gives
        // real atomicity. InMemory (tests) throws TransactionIgnoredWarning-as-error,
        // so we gate the transaction on a relational provider.
        await using var tx = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(ct)
            : null;

        var active = await _db.PermissionGrants
            .Where(g => g.AdminUserId == id && g.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var g in active)
        {
            g.RevokedAt = DateTimeOffset.UtcNow;
            g.RevokedBy = superId;
            g.RevokeReason = "template_swap";
        }

        if (dto.Template == PermissionTemplates.Custom)
        {
            foreach (var ck in dto.CustomKeys ?? new List<CustomKey>())
            {
                if (!PermissionKeys.IsValidKey(ck.PermissionKey)) continue;
                _db.PermissionGrants.Add(new PermissionGrant
                {
                    AdminUserId = id,
                    PermissionKey = ck.PermissionKey,
                    GrantedBy = superId,
                    ExpiresAt = ck.ExpiresAt,
                });
            }
        }
        else
        {
            foreach (var key in PermissionTemplates.GetPermissionsForTemplate(dto.Template))
                _db.PermissionGrants.Add(new PermissionGrant
                {
                    AdminUserId = id,
                    PermissionKey = key,
                    GrantedBy = superId,
                });
        }

        target.IsReadonly = PermissionTemplates.RequiresIsReadonlyFlag(dto.Template);
        await _db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return Ok(new { id, template = dto.Template, isReadonly = target.IsReadonly });
    }

    public sealed record ApplyTemplateDto(string Template, List<CustomKey>? CustomKeys);

    private static DateTimeOffset? ForceChangeDeadline(string? policy) => policy switch
    {
        "on_first_login" => DateTimeOffset.UtcNow,                 // already due
        "within_7_days"  => DateTimeOffset.UtcNow.AddDays(7),
        "within_30_days" => DateTimeOffset.UtcNow.AddDays(30),
        "never"          => null,
        _                => null,
    };

    public sealed record CreateAdminDto(
        string Email,
        bool SendInvitation,
        string? InitialPassword,
        string ForcePasswordChange,
        string Template,
        List<CustomKey>? CustomKeys,
        bool Require2fa);

    public sealed record PromoteDto(
        string Template,
        string ForcePasswordChange,
        bool Require2fa,
        List<CustomKey>? CustomKeys);

    public sealed record CustomKey(string PermissionKey, DateTimeOffset? ExpiresAt);

    public sealed record SetRequire2faDto(bool Require2fa);

    public sealed record BulkPromoteDto(Guid[] UserIds, string Template, string ForcePasswordChange, bool Require2fa);

    public sealed record BulkDemoteDto(Guid[] AdminIds);
}
