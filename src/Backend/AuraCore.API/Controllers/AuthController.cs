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

    public AuthController(IAuthService auth, AuraCoreDbContext db)
    {
        _auth = auth;
        _db = db;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Email and password are required" });

        if (request.Password.Length < 8)
            return BadRequest(new { error = "Password must be at least 8 characters" });

        var result = await _auth.RegisterAsync(request.Email, request.Password, ct);
        if (!result.Success)
            return Conflict(new { error = result.Error });

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
        var ip = GetClientIp();

        // ── Rate Limiting: 5 failed attempts in 15 min → block ──
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-15);
        var recentFails = await _db.LoginAttempts
            .CountAsync(a => a.IpAddress == ip && !a.Success && a.CreatedAt > cutoff, ct);

        if (recentFails >= 5)
        {
            return StatusCode(429, new
            {
                error = "Too many failed login attempts. Please try again in 15 minutes.",
                retryAfterMinutes = 15
            });
        }

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
        // Check forwarded headers (behind proxy/load balancer)
        var forwarded = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
            return forwarded.Split(',')[0].Trim();
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

public sealed record RegisterRequest(string Email, string Password);
public sealed record LoginRequest(string Email, string Password, string? TotpCode = null);
public sealed record RefreshRequest(string RefreshToken);
