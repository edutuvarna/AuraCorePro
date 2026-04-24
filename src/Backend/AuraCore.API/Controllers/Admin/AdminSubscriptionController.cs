using AuraCore.API.Domain.Entities;
using AuraCore.API.Filters;
using AuraCore.API.Helpers;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Admin;

[ApiController]
[Route("api/admin/subscriptions")]
[Authorize(Roles = "admin")]
public sealed class AdminSubscriptionController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    public AdminSubscriptionController(AuraCoreDbContext db) => _db = db;

    [HttpPost("grant")]
    [RequiresPermission(PermissionKeys.ActionSubscriptionsGrant)]
    [AuraCore.API.Filters.AuditAction("GrantSubscription", "Subscription")]
    public async Task<IActionResult> Grant([FromBody] GrantRequest req, CancellationToken ct)
    {
        // T2.1: validate Days > 0 (immediately-expired licenses served no purpose)
        if (req.Days <= 0)
            return BadRequest(new { error = "Days must be positive" });
        if (req.Days > 3650)  // 10-year sanity cap
            return BadRequest(new { error = "Days must be <= 3650 (10 years)" });

        var user = await _db.Users.FindAsync(new object[] { req.UserId }, ct);
        if (user is null) return NotFound(new { error = "User not found" });

        var expiresAt = DateTimeOffset.UtcNow.AddDays(req.Days);

        // License side (existing behavior)
        var license = await _db.Licenses.FirstOrDefaultAsync(l => l.UserId == req.UserId && l.Status == "active", ct);
        if (license is not null)
        {
            license.Tier = req.Tier;
            license.ExpiresAt = expiresAt;
        }
        else
        {
            _db.Licenses.Add(new License
            {
                UserId = req.UserId,
                Key = LicenseKeyGenerator.Generate(),
                Tier = req.Tier,
                MaxDevices = req.Tier == "enterprise" ? 5 : 1,
                ExpiresAt = expiresAt
            });
        }

        // T1.1 fix: also write to Subscriptions so the Subscriptions tab has data.
        var subscription = await _db.Subscriptions.FirstOrDefaultAsync(s => s.UserId == req.UserId && s.Status == "active", ct);
        if (subscription is not null)
        {
            subscription.Plan = req.Tier;
            subscription.CurrentPeriodEnd = expiresAt;
        }
        else
        {
            subscription = new Subscription
            {
                UserId = req.UserId,
                Plan = req.Tier,
                Status = "active",
                CurrentPeriodEnd = expiresAt,
                StripeSubscriptionId = $"manual-{Guid.NewGuid():N}",  // Distinguish admin-granted from Stripe-backed
                StripeCustomerId = "",  // No Stripe customer for manually-granted subscriptions
            };
            _db.Subscriptions.Add(subscription);
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { message = $"Granted {req.Tier} for {req.Days} days to {user.Email}", subscriptionId = subscription.Id });
    }

    [HttpPost("revoke/{userId:guid}")]
    [RequiresPermission(PermissionKeys.ActionSubscriptionsRevoke)]
    [AuraCore.API.Filters.AuditAction("RevokeSubscription", "Subscription", TargetIdFromRouteKey = "userId")]
    public async Task<IActionResult> Revoke(Guid userId, CancellationToken ct)
    {
        var license = await _db.Licenses.FirstOrDefaultAsync(l => l.UserId == userId && l.Status == "active", ct);
        if (license is null) return NotFound();
        license.Tier = "free"; license.ExpiresAt = null;
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Subscription revoked" });
    }
}

public sealed record GrantRequest(Guid UserId, string Tier, int Days);
