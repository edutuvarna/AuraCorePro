using System.Security.Claims;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class LicenseController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    public LicenseController(AuraCoreDbContext db) => _db = db;

    [HttpGet("validate")]
    [Authorize]
    public async Task<IActionResult> Validate(
        [FromQuery] string key, [FromQuery] string device, CancellationToken ct)
    {
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(sub, out var userId))
            return Unauthorized(new { error = "Invalid token" });

        var role = User.FindFirst("role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value;

        // Admin always gets full access
        if (role == "admin")
            return Ok(new { valid = true, tier = "admin", maxDevices = 999 });

        // Look up active license for this user
        var license = await _db.Licenses
            .FirstOrDefaultAsync(l => l.UserId == userId && l.Status == "active", ct);

        if (license is null)
            return Ok(new { valid = true, tier = "free", maxDevices = 1 });

        // Check expiry
        if (license.ExpiresAt.HasValue && license.ExpiresAt.Value < DateTimeOffset.UtcNow)
            return Ok(new { valid = true, tier = "free", maxDevices = 1, expired = true });

        return Ok(new { valid = true, tier = license.Tier, maxDevices = license.MaxDevices });
    }
}
