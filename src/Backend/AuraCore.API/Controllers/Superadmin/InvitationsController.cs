using System.Security.Cryptography;
using AuraCore.API.Application.Services.Email;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Helpers;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Superadmin;

[ApiController]
[Route("api/superadmin/invitations")]
[Authorize(Roles = "superadmin")]
public sealed class InvitationsController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    private readonly IEmailService _email;

    public InvitationsController(AuraCoreDbContext db, IEmailService email)
    {
        _db = db;
        _email = email;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var items = await _db.AdminInvitations
            .OrderByDescending(i => i.CreatedAt)
            .Take(200)
            .Select(i => new
            {
                tokenHash = i.TokenHash,
                adminEmail = _db.Users.Where(u => u.Id == i.AdminUserId).Select(u => u.Email).FirstOrDefault(),
                createdByEmail = _db.Users.Where(u => u.Id == i.CreatedBy).Select(u => u.Email).FirstOrDefault(),
                createdAt = i.CreatedAt,
                expiresAt = i.ExpiresAt,
                consumedAt = i.ConsumedAt,
                status = i.ConsumedAt != null ? "accepted"
                       : i.ExpiresAt < now ? "expired"
                       : "pending",
            })
            .ToListAsync(ct);
        return Ok(new { items });
    }

    [HttpDelete("{tokenHash}")]
    public async Task<IActionResult> Revoke(string tokenHash, CancellationToken ct)
    {
        var inv = await _db.AdminInvitations.FirstOrDefaultAsync(i => i.TokenHash == tokenHash, ct);
        if (inv is null) return NotFound();
        if (inv.ConsumedAt != null) return BadRequest(new { error = "already_accepted" });
        _db.AdminInvitations.Remove(inv);
        await _db.SaveChangesAsync(ct);
        return Ok(new { revoked = true });
    }

    [HttpPost("{tokenHash}/resend")]
    public async Task<IActionResult> Resend(string tokenHash, CancellationToken ct)
    {
        var inv = await _db.AdminInvitations.FirstOrDefaultAsync(i => i.TokenHash == tokenHash, ct);
        if (inv is null) return NotFound();
        if (inv.ConsumedAt != null) return BadRequest(new { error = "already_accepted" });

        var admin = await _db.Users.FirstOrDefaultAsync(u => u.Id == inv.AdminUserId, ct);
        if (admin is null) return NotFound(new { error = "admin_user_missing" });

        // Generate a fresh token (base64url, 32 random bytes).
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');
        var newHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

        _db.AdminInvitations.Remove(inv);
        _db.AdminInvitations.Add(new AdminInvitation
        {
            TokenHash = newHash,
            AdminUserId = admin.Id,
            CreatedBy = User.GetUserId()!.Value,
            ExpiresAt = expiresAt,
        });
        await _db.SaveChangesAsync(ct);

        var setupLink = $"https://admin.auracore.pro/#/invite?token={raw}&email={Uri.EscapeDataString(admin.Email)}";
        await _email.SendFromTemplateAsync(EmailTemplate.AdminInvitation, new
        {
            to = admin.Email,
            adminEmail = admin.Email,
            invitedBy = User.GetEmail() ?? "superadmin",
            setupLink,
            expiresAt = expiresAt.ToString("u"),
        }, ct);

        return Ok(new { resent = true, newTokenHash = newHash });
    }
}
