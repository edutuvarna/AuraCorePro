using AuraCore.API.Application.Interfaces;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using AuraCore.API.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly AuraCoreDbContext _db;
    private static readonly Dictionary<string, (int Count, DateTime ResetAt)> _regAttempts = new();

    public AuthController(IAuthService auth, AuraCoreDbContext db)
    {
        _auth = auth;
        _db = db;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { error = "Email is required" });

        // Strict email validation - no HTML, no special chars except standard email chars
        var email = request.Email.Trim().ToLowerInvariant();
        if (email.Length > 254)
            return BadRequest(new { error = "Email too long" });

        // Regex: standard email format only
        if (!System.Text.RegularExpressions.Regex.IsMatch(email,
            @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
            return BadRequest(new { error = "Invalid email format" });

        // Anti-XSS: reject any HTML characters
        if (email.Contains('<') || email.Contains('>') || email.Contains('"') || email.Contains('\''))
            return BadRequest(new { error = "Invalid characters in email" });

        // Rate limit registration: max 3 per IP per hour
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        lock (_regAttempts)
        {
            // Clean old entries
            var expired = _regAttempts.Where(x => x.Value.ResetAt <= DateTime.UtcNow).Select(x => x.Key).ToList();
            foreach (var key in expired) _regAttempts.Remove(key);

            if (_regAttempts.TryGetValue(ip, out var regAttempt))
            {
                if (regAttempt.Count >= 3)
                    return StatusCode(429, new { error = "Too many registration attempts. Try again later." });
                _regAttempts[ip] = (regAttempt.Count + 1, regAttempt.ResetAt);
            }
            else
            {
                _regAttempts[ip] = (1, DateTime.UtcNow.AddHours(1));
            }
        }

        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Email and password are required" });

        if (request.Password.Length < 10)
            return BadRequest(new { error = "Password must be at least 10 characters" });

        // Check for at least 2 character types
        int types = 0;
        if (request.Password.Any(char.IsUpper)) types++;
        if (request.Password.Any(char.IsLower)) types++;
        if (request.Password.Any(char.IsDigit)) types++;
        if (request.Password.Any(c => !char.IsLetterOrDigit(c))) types++;
        if (types < 2)
            return BadRequest(new { error = "Password must include at least 2 of: uppercase, lowercase, numbers, symbols" });

        var result = await _auth.RegisterAsync(email, request.Password, ct);
        if (!result.Success)
        {
            // Return same HTTP status to prevent account enumeration
            // Add artificial delay to match registration time
            await Task.Delay(Random.Shared.Next(100, 300), ct);
            return BadRequest(new { error = "Registration failed. Please try a different email or contact support." });
        }

        return Created("/api/auth/me", new
        {
            accessToken = result.AccessToken,
            refreshToken = result.RefreshToken,
            user = result.User
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Email and password are required" });

        var email = request.Email.Trim().ToLowerInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(email,
            @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
            return BadRequest(new { error = "Invalid email format" });

        var ip = GetClientIp();

        // ── Rate Limiting: 3 failed attempts in 30 min → block ──
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-30);
        var recentFails = await _db.LoginAttempts
            .CountAsync(a => a.IpAddress == ip && !a.Success && a.CreatedAt > cutoff, ct);

        if (recentFails >= 3)
            return StatusCode(429, new { error = "Too many failed attempts. Try again in 30 minutes." });

        // Per-email rate limiting
        var normalizedEmail = request.Email?.Trim().ToLowerInvariant() ?? "";
        var emailFails = await _db.LoginAttempts
            .CountAsync(a => a.Email == normalizedEmail && !a.Success && a.CreatedAt > cutoff, ct);
        if (emailFails >= 5)
            return StatusCode(429, new { error = "Account temporarily locked. Try again later." });

        // ── Authenticate ──
        var result = await _auth.LoginAsync(request.Email, request.Password, ct);

        // Log attempt
        _db.LoginAttempts.Add(new LoginAttempt
        {
            Email = request.Email.ToLowerInvariant().Trim(),
            IpAddress = ip,
            Success = result.Success
        });
        await _db.SaveChangesAsync(ct);

        if (!result.Success)
            return Unauthorized(new { error = result.Error });

        // ── 2FA Check ──
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant().Trim(), ct);
        if (user is not null && user.TotpEnabled)
        {
            // If 2FA code provided in request, validate it
            if (!string.IsNullOrEmpty(request.TotpCode))
            {
                if (!TotpService.ValidateCode(user.TotpSecret!, request.TotpCode))
                    return Unauthorized(new { error = "Invalid 2FA code" });
            }
            else
            {
                // Return partial response — client must provide 2FA code
                return Ok(new
                {
                    requires2fa = true,
                    message = "Enter your 2FA code from Google Authenticator"
                });
            }
        }

        return Ok(new
        {
            accessToken = result.AccessToken,
            refreshToken = result.RefreshToken,
            user = result.User
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var result = await _auth.RefreshTokenAsync(request.RefreshToken, ct);
        if (!result.Success)
            return Unauthorized(new { error = result.Error });

        return Ok(new
        {
            accessToken = result.AccessToken,
            refreshToken = result.RefreshToken,
            user = result.User
        });
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var userId = User.FindFirst("sub")?.Value;
        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        return Ok(new { id = userId, email, role });
    }

    private string GetClientIp()
    {
        // Only use RemoteIpAddress - don't trust X-Forwarded-For unless behind known proxy
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

public sealed record RegisterRequest(string Email, string Password);
public sealed record LoginRequest(string Email, string Password, string? TotpCode = null);
public sealed record RefreshRequest(string RefreshToken);
