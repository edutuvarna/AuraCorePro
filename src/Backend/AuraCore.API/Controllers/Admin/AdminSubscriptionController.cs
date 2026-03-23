using AuraCore.API.Infrastructure.Data;
using AuraCore.API.Domain.Entities;
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
    public async Task<IActionResult> Grant([FromBody] GrantRequest req, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync(new object[] { req.UserId }, ct);
        if (user is null) return NotFound("User not found");

        var license = await _db.Licenses.FirstOrDefaultAsync(l => l.UserId == req.UserId && l.Status == "active", ct);
        if (license is not null)
        {
            license.Tier = req.Tier;
            license.ExpiresAt = DateTimeOffset.UtcNow.AddDays(req.Days);
        }
        else
        {
            _db.Licenses.Add(new License
            {
                UserId = req.UserId,
                Key = Guid.NewGuid().ToString("N"),
                Tier = req.Tier,
                MaxDevices = req.Tier == "enterprise" ? 5 : 1,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(req.Days)
            });
        }
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = $"Granted {req.Tier} for {req.Days} days to {user.Email}" });
    }

    [HttpPost("revoke/{userId:guid}")]
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
