using System.Security.Claims;
using AuraCore.API.Infrastructure.Data;
using AuraCore.API.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers;

[ApiController]
[Route("api/2fa")]
[Authorize]
public sealed class TotpController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    public TotpController(AuraCoreDbContext db) => _db = db;

    private Guid? GetUserId()
    {
        var sub = User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    [HttpPost("setup")]
    public async Task<IActionResult> Setup(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new { error = "Invalid token" });

        var user = await _db.Users.FindAsync(new object[] { userId.Value }, ct);
        if (user is null) return NotFound();

        var secret = TotpService.GenerateSecret();
        user.TotpSecret = secret;
        user.TotpEnabled = false;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var qrUri = TotpService.GetQrUri(secret, user.Email);

        return Ok(new
        {
            secret,
            qrUri,
            message = "Scan the QR code with Google Authenticator, then call /verify with the 6-digit code."
        });
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] TotpVerifyRequest req, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new { error = "Invalid token" });

        var user = await _db.Users.FindAsync(new object[] { userId.Value }, ct);
        if (user is null) return NotFound();
        if (string.IsNullOrEmpty(user.TotpSecret))
            return BadRequest(new { error = "Call /setup first" });

        if (!TotpService.ValidateCode(user.TotpSecret, req.Code))
            return Unauthorized(new { error = "Invalid code. Check your authenticator app." });

        user.TotpEnabled = true;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new { message = "2FA enabled successfully!", enabled = true });
    }

    [HttpPost("validate")]
    [AllowAnonymous]
    public async Task<IActionResult> Validate([FromBody] TotpLoginRequest req, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email.ToLowerInvariant().Trim(), ct);
        if (user is null) return NotFound(new { error = "User not found" });
        if (!user.TotpEnabled || string.IsNullOrEmpty(user.TotpSecret))
            return BadRequest(new { error = "2FA not enabled for this user" });

        if (!TotpService.ValidateCode(user.TotpSecret, req.Code))
            return Unauthorized(new { error = "Invalid 2FA code" });

        return Ok(new { valid = true, message = "2FA verified" });
    }

    [HttpPost("disable")]
    public async Task<IActionResult> Disable([FromBody] TotpVerifyRequest req, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new { error = "Invalid token" });

        var user = await _db.Users.FindAsync(new object[] { userId.Value }, ct);
        if (user is null) return NotFound();
        if (!user.TotpEnabled) return Ok(new { message = "2FA already disabled" });

        if (!TotpService.ValidateCode(user.TotpSecret!, req.Code))
            return Unauthorized(new { error = "Invalid code" });

        user.TotpEnabled = false;
        user.TotpSecret = null;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new { message = "2FA disabled", enabled = false });
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new { error = "Invalid token" });

        var user = await _db.Users.FindAsync(new object[] { userId.Value }, ct);
        if (user is null) return NotFound();
        return Ok(new { enabled = user.TotpEnabled });
    }
}

public sealed record TotpVerifyRequest(string Code);
public sealed record TotpLoginRequest(string Email, string Code);
