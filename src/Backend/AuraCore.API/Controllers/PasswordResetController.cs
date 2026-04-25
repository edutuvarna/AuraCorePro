using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers;

[ApiController]
[Route("api/auth/password")]
public sealed class PasswordResetController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    private readonly AuraCore.API.Application.Services.Email.IEmailService _email;
    private readonly AuraCore.API.Application.Services.Security.ICaptchaVerifier _captcha;

    private static readonly ConcurrentDictionary<string, (int Count, DateTime ResetAt)> _forgotAttempts = new();
    private static readonly ConcurrentDictionary<string, (int Count, DateTime ResetAt)> _resetAttempts = new();

    // Phase 6.12.W6.T11 — env-flag-driven CAPTCHA enforcement. Use static
    // PROPERTIES (not static readonly fields) so each request re-reads the env;
    // tests can toggle the flags via Environment.SetEnvironmentVariable at runtime.
    private static bool CaptchaEnabled =>
        !string.Equals(Environment.GetEnvironmentVariable("CAPTCHA_ENABLED"), "false", StringComparison.OrdinalIgnoreCase);
    private static bool CaptchaTolerant =>
        string.Equals(Environment.GetEnvironmentVariable("TURNSTILE_TOLERANT_MODE"), "true", StringComparison.OrdinalIgnoreCase);

    public PasswordResetController(
        AuraCoreDbContext db,
        AuraCore.API.Application.Services.Email.IEmailService email,
        AuraCore.API.Application.Services.Security.ICaptchaVerifier captcha)
    {
        _db = db;
        _email = email;
        _captcha = captcha;
    }

    [HttpPost("forgot")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        var okResponse = Ok(new { message = "If this email is registered, a reset code has been sent." });

        if (string.IsNullOrWhiteSpace(request.Email))
            return okResponse;

        var email = request.Email.Trim().ToLowerInvariant();

        if (email.Length > 254)
            return okResponse;

        if (!Regex.IsMatch(email, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
            return okResponse;

        // Rate limit: 3 requests/hour per IP
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Phase 6.12.W6.T11 — CAPTCHA gate. Runs after empty-input + email-format
        // validation (those return the generic okResponse to avoid email
        // enumeration) but before rate-limit + DB writes + email send.
        var captchaFail = await CheckCaptchaAsync(request.TurnstileToken, ip, ct);
        if (captchaFail is not null) return captchaFail;

        if (!CheckRateLimit(_forgotAttempts, ip, 3))
            return StatusCode(429, new { error = "Too many requests. Try again later." });

        // Generate 6-digit code
        var code = Random.Shared.Next(100000, 999999).ToString();

        // Invalidate previous unused codes for this email
        var previousCodes = await _db.PasswordResetCodes
            .Where(c => c.Email == email && !c.Used)
            .ToListAsync(ct);
        foreach (var prev in previousCodes)
            prev.Used = true;

        // Save new code with 10-minute TTL
        _db.PasswordResetCodes.Add(new PasswordResetCode
        {
            Email = email,
            Code = code,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
            Used = false
        });
        await _db.SaveChangesAsync(ct);

        // Send email (only if user exists — but always return same response)
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is not null)
        {
            await _email.SendFromTemplateAsync(
                AuraCore.API.Application.Services.Email.EmailTemplate.PasswordReset,
                new { to = email, code, expiresMinutes = 10 },
                ct);
        }

        return okResponse;
    }

    [HttpPost("reset")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Code) ||
            string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(new { error = "Invalid or expired code." });

        var email = request.Email.Trim().ToLowerInvariant();

        // Rate limit: 5 attempts/hour per IP
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (!CheckRateLimit(_resetAttempts, ip, 5))
            return StatusCode(429, new { error = "Too many attempts. Try again later." });

        // Find matching unused, non-expired code
        var resetCode = await _db.PasswordResetCodes
            .Where(c => c.Email == email && c.Code == request.Code && !c.Used && c.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (resetCode is null)
            return BadRequest(new { error = "Invalid or expired code." });

        // Validate password strength: min 10 chars, 2+ character types
        if (request.NewPassword.Length < 10)
            return BadRequest(new { error = "Password must be at least 10 characters." });

        int types = 0;
        if (request.NewPassword.Any(char.IsUpper)) types++;
        if (request.NewPassword.Any(char.IsLower)) types++;
        if (request.NewPassword.Any(char.IsDigit)) types++;
        if (request.NewPassword.Any(c => !char.IsLetterOrDigit(c))) types++;
        if (types < 2)
            return BadRequest(new { error = "Password must include at least 2 of: uppercase, lowercase, numbers, symbols." });

        // Update user password
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
            return BadRequest(new { error = "Invalid or expired code." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        resetCode.Used = true;

        await _db.SaveChangesAsync(ct);

        return Ok(new { message = "Password reset successful." });
    }

    private static bool CheckRateLimit(ConcurrentDictionary<string, (int Count, DateTime ResetAt)> store, string key, int maxAttempts)
    {
        var now = DateTime.UtcNow;

        // Clean expired entries periodically
        foreach (var expired in store.Where(x => x.Value.ResetAt <= now).Select(x => x.Key).ToList())
            store.TryRemove(expired, out _);

        var entry = store.AddOrUpdate(
            key,
            _ => (1, now.AddHours(1)),
            (_, existing) =>
            {
                if (existing.ResetAt <= now)
                    return (1, now.AddHours(1));
                return (existing.Count + 1, existing.ResetAt);
            });

        return entry.Count <= maxAttempts;
    }

    /// <summary>
    /// Phase 6.12.W6.T11 — CAPTCHA gate helper. Mirrors AuthController.CheckCaptchaAsync.
    /// Returns null on success (caller continues), or an IActionResult to return
    /// immediately on failure. CAPTCHA_ENABLED=false disables the check entirely
    /// (emergency escape hatch). TURNSTILE_TOLERANT_MODE=true allows missing tokens
    /// through during the rollout window between backend and frontend deploys.
    /// </summary>
    private async Task<IActionResult?> CheckCaptchaAsync(string? token, string ip, CancellationToken ct)
    {
        if (!CaptchaEnabled) return null;
        if (string.IsNullOrEmpty(token))
        {
            if (CaptchaTolerant) return null;
            return BadRequest(new { error = "captcha_required" });
        }
        var ok = await _captcha.VerifyAsync(token, ip, ct);
        return ok ? null : BadRequest(new { error = "captcha_invalid" });
    }
}

public sealed record ForgotPasswordRequest(string Email, string? TurnstileToken = null);
public sealed record ResetPasswordRequest(string Email, string Code, string NewPassword);
