using System.Collections.Concurrent;
using System.Text.Json;
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

    private static readonly ConcurrentDictionary<string, (int Count, DateTime ResetAt)> _forgotAttempts = new();
    private static readonly ConcurrentDictionary<string, (int Count, DateTime ResetAt)> _resetAttempts = new();

    public PasswordResetController(AuraCoreDbContext db)
    {
        _db = db;
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
            await SendResetEmailAsync(email, code);
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

    private static async Task SendResetEmailAsync(string email, string code)
    {
        var apiKey = Environment.GetEnvironmentVariable("RESEND_API_KEY");
        if (string.IsNullOrEmpty(apiKey)) return;

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        var body = new
        {
            from = "AuraCore Pro <noreply@auracore.pro>",
            to = new[] { email },
            subject = "Password Reset Code",
            html = $"<div style='font-family:sans-serif;max-width:400px;margin:0 auto;padding:20px'>" +
                   $"<h2 style='color:#00D4AA'>AuraCore Pro</h2>" +
                   $"<p>Your password reset code is:</p>" +
                   $"<div style='font-size:32px;font-weight:bold;letter-spacing:8px;text-align:center;padding:20px;background:#f5f5f5;border-radius:8px;margin:16px 0'>{code}</div>" +
                   $"<p style='color:#666;font-size:14px'>This code expires in 10 minutes. If you didn't request this, ignore this email.</p></div>"
        };

        await client.PostAsync("https://api.resend.com/emails",
            new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json"));
    }
}

public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Email, string Code, string NewPassword);
