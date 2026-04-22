using System.Security.Claims;
using AuraCore.API.Application.Services.Security;
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
    private readonly ITotpEncryption _totpEnc;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (int Count, DateTime ResetAt)> _totpAttempts = new();

    public TotpController(AuraCoreDbContext db, ITotpEncryption totpEnc)
    {
        _db = db;
        _totpEnc = totpEnc;
    }

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
        user.TotpSecret = _totpEnc.Encrypt(secret);
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

        var plaintextSecret = _totpEnc.Decrypt(user.TotpSecret!);
        if (!TotpService.ValidateCode(plaintextSecret, req.Code))
            return Unauthorized(new { error = "Invalid code. Check your authenticator app." });

        user.TotpEnabled = true;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new { message = "2FA enabled successfully!", enabled = true });
    }

    // T2.29: [AllowAnonymous] removed — the previous implementation accepted an arbitrary email in the
    // request body and returned 2FA enrollment status for that address, enabling account enumeration
    // and revealing which accounts lack 2FA (credential-stuffing intelligence). The endpoint now
    // requires a valid JWT; the caller's own identity is resolved from the token, and no email is
    // accepted from the body.
    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] TotpValidateRequest req, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new { error = "Invalid token" });

        var userKey = userId.Value.ToString();
        var now = DateTime.UtcNow;

        // T2.30: Rate limiting via ConcurrentDictionary (thread-safe, atomic AddOrUpdate).
        // Check BEFORE the DB hit so we don't do unnecessary work under brute-force.
        if (_totpAttempts.TryGetValue(userKey, out var existing))
        {
            if (existing.ResetAt > now && existing.Count >= 5)
                return StatusCode(429, new { error = "Too many attempts. Try again later." });
            if (existing.ResetAt <= now)
                _totpAttempts.TryRemove(userKey, out _); // Window expired — clean up stale entry
        }

        var user = await _db.Users.FindAsync(new object[] { userId.Value }, ct);
        if (user is null) return NotFound();
        if (!user.TotpEnabled || string.IsNullOrEmpty(user.TotpSecret))
            return BadRequest(new { error = "2FA not enabled for this account" });

        var plaintextSecretValidate = _totpEnc.Decrypt(user.TotpSecret!);
        if (!TotpService.ValidateCode(plaintextSecretValidate, req.Code))
        {
            // Atomic increment: window carries over if still active, resets if expired.
            _totpAttempts.AddOrUpdate(
                userKey,
                _ => (1, now.AddMinutes(15)),
                (_, prev) => prev.ResetAt > now
                    ? (prev.Count + 1, prev.ResetAt)
                    : (1, now.AddMinutes(15)));
            return Unauthorized(new { error = "Invalid 2FA code" });
        }

        // Clear rate-limit entry on success.
        _totpAttempts.TryRemove(userKey, out _);
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

        var plaintextSecretDisable = _totpEnc.Decrypt(user.TotpSecret!);
        if (!TotpService.ValidateCode(plaintextSecretDisable, req.Code))
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
// TotpValidateRequest: Code-only; email previously leaked account existence — see T2.29 fix above.
public sealed record TotpValidateRequest(string Code);
// TotpLoginRequest kept for any in-flight callers; superseded by TotpValidateRequest.
public sealed record TotpLoginRequest(string Email, string Code);
