using AuraCore.API.Application.Services.Email;
using AuraCore.API.Application.Services.Push;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Helpers;
using AuraCore.API.Hubs;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Admin;

[ApiController]
[Route("api/admin/permission-requests")]
[Authorize(Roles = "admin")]
public sealed class PermissionRequestsController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    private readonly IEmailService _email;
    private readonly IHubContext<AdminHub> _hub;
    private readonly IFcmService _fcm;

    public PermissionRequestsController(AuraCoreDbContext db, IEmailService email, IHubContext<AdminHub> hub, IFcmService fcm)
    {
        _db = db; _email = email; _hub = hub; _fcm = fcm;
    }

    [HttpGet]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();
        var list = await _db.PermissionRequests
            .Where(r => r.AdminUserId == userId.Value)
            .OrderByDescending(r => r.RequestedAt)
            .Take(100)
            .Select(r => new { r.Id, r.PermissionKey, r.Reason, r.Status, r.RequestedAt, r.ReviewedAt, r.ReviewNote })
            .ToListAsync(ct);
        return Ok(new { items = list });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRequestDto dto, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();

        if (!PermissionKeys.IsValidKey(dto.PermissionKey))
            return BadRequest(new { error = "unknown_permission_key" });
        if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Length < 50 || dto.Reason.Length > 500)
            return BadRequest(new { error = "reason_length", hint = "Reason must be 50-500 characters" });

        var hasPending = await _db.PermissionRequests.AnyAsync(r =>
            r.AdminUserId == userId.Value && r.PermissionKey == dto.PermissionKey && r.Status == "pending", ct);
        if (hasPending)
            return Conflict(new { error = "duplicate_pending_request" });

        var req = new PermissionRequest {
            AdminUserId = userId.Value,
            PermissionKey = dto.PermissionKey,
            Reason = dto.Reason.Trim(),
            Status = "pending",
        };
        _db.PermissionRequests.Add(req);
        await _db.SaveChangesAsync(ct);

        // Broadcast to superadmins
        var adminEmail = User.GetEmail() ?? "unknown";
        await _hub.Clients.Group("superadmins").SendAsync("PermissionRequested", new {
            requestId = req.Id, adminEmail, permissionKey = req.PermissionKey, reason = req.Reason, requestedAt = req.RequestedAt,
        }, ct);

        // Phase 6.14: FCM push to every superadmin device token (one push per token).
        var pushPayload = new FcmPayload(
            "Permission request",
            $"{adminEmail} requests {req.PermissionKey}",
            new Dictionary<string, string>
            {
                ["type"] = "permission-request",
                ["requestId"] = req.Id.ToString(),
            });
        await PermissionRequestPushTrigger.SendToSuperadminsAsync(_db, _fcm, pushPayload, ct);

        // Best-effort email to each superadmin
        var superadmins = await _db.Users.Where(u => u.Role == "superadmin" && u.IsActive).ToListAsync(ct);
        foreach (var sa in superadmins)
        {
            await _email.SendFromTemplateAsync(EmailTemplate.PermissionRequested, new {
                to = sa.Email, adminEmail, permissionKey = req.PermissionKey,
                reason = req.Reason, inboxLink = "https://admin.auracore.pro/#/permission-requests",
            }, ct);
        }

        return Ok(new { id = req.Id.ToString(), status = req.Status, requestedAt = req.RequestedAt });
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();

        var req = await _db.PermissionRequests.FirstOrDefaultAsync(r => r.Id == id && r.AdminUserId == userId.Value, ct);
        if (req is null) return NotFound();
        if (req.Status != "pending") return BadRequest(new { error = "cannot_cancel_non_pending" });
        req.Status = "cancelled";
        await _db.SaveChangesAsync(ct);
        return Ok(new { id = req.Id });
    }

    public sealed record CreateRequestDto(string PermissionKey, string Reason);
}

public static class PermissionRequestPushTrigger
{
    public static async Task SendToSuperadminsAsync(
        AuraCoreDbContext db,
        IFcmService fcm,
        FcmPayload payload,
        CancellationToken ct)
    {
        var tokens = await db.FcmDeviceTokens
            .Where(t => db.Users.Where(u => u.Role == "superadmin").Select(u => u.Id).Contains(t.UserId))
            .Select(t => t.Token)
            .ToListAsync(ct);
        foreach (var token in tokens)
        {
            try { await fcm.SendAsync(token, payload, ct); }
            catch (Exception) { /* log + continue: one bad token must not block others */ }
        }
    }
}
