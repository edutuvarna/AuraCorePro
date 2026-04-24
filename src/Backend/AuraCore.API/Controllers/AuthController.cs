using AuraCore.API.Application.Interfaces;
using AuraCore.API.Application.Services.Security;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Hubs;
using AuraCore.API.Infrastructure.Data;
using AuraCore.API.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly AuraCoreDbContext _db;
    private readonly IWhitelistService _whitelist;
    private readonly ITotpEncryption _totpEnc;
    private readonly IHubContext<AdminHub> _hub;
    private static readonly Dictionary<string, (int Count, DateTime ResetAt)> _regAttempts = new();

    public AuthController(IAuthService auth, AuraCoreDbContext db, IWhitelistService whitelist, ITotpEncryption totpEnc, IHubContext<AdminHub> hub)
    {
        _auth = auth;
        _db = db;
        _whitelist = whitelist;
        _totpEnc = totpEnc;
        _hub = hub;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        // T1.25 NewRegistrations enforcement — rejects signups when admin disabled them
        var cache = HttpContext?.RequestServices?.GetService<IMemoryCache>();
        if (cache is not null
            && cache.TryGetValue<AppConfig>("maintenance-config", out var cachedCfg)
            && cachedCfg is not null
            && cachedCfg.NewRegistrations == false)
            return StatusCode(503, new { error = "New registrations are temporarily disabled" });

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

        // Phase 6.10 Task 19: broadcast new registration to admin dashboard
        if (result.User is not null)
        {
            await _hub.Clients.Group("admins").SendAsync("UserRegistered", new
            {
                email = result.User.Email,
                id = result.User.Id,
                createdAt = DateTimeOffset.UtcNow
            }, ct);
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

        // ── Rate Limiting: bypass for whitelisted operational IPs (ip-whitelist.md F-3) ──
        var whitelisted = await _whitelist.IsWhitelistedAsync(ip, ct);
        if (!whitelisted)
        {
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
        }

        // ── Authenticate ──
        var result = await _auth.LoginAsync(request.Email, request.Password, ct);

        // Helper for one-line LoginAttempt writes. Deferred until we know the true outcome
        // so 2FA failures count against rate limits (security-2fa.md F-1).
        async Task LogAttemptAsync(bool success)
        {
            _db.LoginAttempts.Add(new LoginAttempt
            {
                Email = request.Email.ToLowerInvariant().Trim(),
                IpAddress = ip,
                Success = success
            });
            await _db.SaveChangesAsync(ct);
        }

        if (!result.Success)
        {
            await LogAttemptAsync(false);
            await EmitLoginAsync(email, success: false, ip);
            return Unauthorized(new { error = result.Error });
        }

        // ── 2FA Check ──
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant().Trim(), ct);
        if (user is not null && user.TotpEnabled)
        {
            if (string.IsNullOrEmpty(request.TotpCode))
            {
                // Password was correct, but we haven't seen the TOTP code yet. Do not log
                // an attempt — client must re-submit with TotpCode. The next call will log
                // either Success=true or Success=false based on the TOTP verdict.
                return Ok(new
                {
                    requires2fa = true,
                    message = "Enter your 2FA code from Google Authenticator"
                });
            }

            var plaintextTotpSecret = _totpEnc.Decrypt(user.TotpSecret!);
            if (!TotpService.ValidateCode(plaintextTotpSecret, request.TotpCode))
            {
                // Password OK but TOTP wrong — count as failed attempt so brute-forcing
                // the TOTP also hits the rate limit (security-2fa.md F-1).
                await LogAttemptAsync(false);
                await EmitLoginAsync(email, success: false, ip);
                return Unauthorized(new { error = "Invalid 2FA code" });
            }
        }

        // Password OK + (TOTP OK or not required).
        await LogAttemptAsync(true);
        await EmitLoginAsync(email, success: true, ip);

        return Ok(new
        {
            accessToken = result.AccessToken,
            refreshToken = result.RefreshToken,
            user = result.User
        });
    }

    // Phase 6.11.W2.T13: Superadmin-only login.
    // Stricter than /login: 3 fails / 60 min (vs. /login's 3 / 30 min), always
    // audit-logs to audit_log (Action='SuperadminLoginAttempt') in addition to
    // login_attempts, never reveals whether the email exists, and 2FA is
    // mandatory — a superadmin with totp_enabled=false gets a scope-limited
    // JWT ('2fa-setup-only', 15 min) so the frontend can redirect to
    // /enable-2fa. role='superadmin' is enforced; admins trying this endpoint
    // are rejected with the same 'Invalid credentials' the enumeration attacker
    // would get for a nonexistent email.
    [HttpPost("superadmin/login")]
    public async Task<IActionResult> SuperadminLogin([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Email and password are required" });

        var email = request.Email.Trim().ToLowerInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(email,
            @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
            return BadRequest(new { error = "Invalid email format" });

        var ip = GetClientIp();

        // Stricter rate limit: 3 failed attempts / 60 min (vs. /login's 3 / 30).
        // Uses the same login_attempts table; scoped to this email with a longer window.
        var whitelisted = await _whitelist.IsWhitelistedAsync(ip, ct);
        if (!whitelisted)
        {
            var cutoff = DateTimeOffset.UtcNow.AddMinutes(-60);
            var recent = await _db.LoginAttempts.CountAsync(a =>
                a.Email == email && !a.Success && a.CreatedAt > cutoff, ct);
            if (recent >= 3)
                return StatusCode(429, new { error = "Too many failed attempts. Try again in 60 minutes." });
        }

        // Resolve the user without revealing whether the email exists — every
        // failure path below returns the same 'Invalid credentials'. Always
        // write both a login_attempts row and an audit_log entry.
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        async Task LogAttempt(bool success, Guid? userId = null)
        {
            _db.LoginAttempts.Add(new LoginAttempt
            {
                Email = email,
                IpAddress = ip,
                Success = success,
            });
            _db.AuditLogs.Add(new AuditLogEntry
            {
                ActorId = userId,
                ActorEmail = email,
                Action = "SuperadminLoginAttempt",
                TargetType = "User",
                TargetId = userId?.ToString(),
                AfterData = $"{{\"success\":{(success ? "true" : "false")}}}",
                IpAddress = ip,
            });
            await _db.SaveChangesAsync(ct);
        }

        if (user is null
            || user.Role != "superadmin"
            || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            await LogAttempt(false, user?.Id);
            return Unauthorized(new { error = "Invalid credentials" });
        }

        if (!user.IsActive)
        {
            await LogAttempt(false, user.Id);
            return Unauthorized(new { error = "account_suspended" });
        }

        // 2FA is mandatory for superadmin. totp_enabled=false → scope-limited JWT
        // that only lets the frontend hit /enable-2fa; no refresh token is issued.
        if (!user.TotpEnabled)
        {
            await LogAttempt(true, user.Id);
            var scopedToken = _auth.GenerateAccessToken(user, scope: "2fa-setup-only", lifetime: TimeSpan.FromMinutes(15));
            return Ok(new
            {
                requiresTwoFactorSetup = true,
                accessToken = scopedToken,
                user = new { user.Id, user.Email, user.Role },
                message = "Superadmin accounts require 2FA. Complete setup to continue."
            });
        }

        if (string.IsNullOrEmpty(request.TotpCode))
        {
            // Two-step: client re-submits with TotpCode. Don't log yet — the
            // next call decides success/failure based on the TOTP verdict.
            return Ok(new
            {
                requires2fa = true,
                message = "Enter your 2FA code from your authenticator app"
            });
        }

        var totpPlaintext = _totpEnc.Decrypt(user.TotpSecret!);
        if (!TotpService.ValidateCode(totpPlaintext, request.TotpCode))
        {
            await LogAttempt(false, user.Id);
            return Unauthorized(new { error = "Invalid 2FA code" });
        }

        await LogAttempt(true, user.Id);

        var refresh = _auth.GenerateRefreshToken();
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refresh,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
        });
        await _db.SaveChangesAsync(ct);

        var access = _auth.GenerateAccessToken(user);
        return Ok(new
        {
            accessToken = access,
            refreshToken = refresh,
            user = new { user.Id, user.Email, user.Role },
        });
    }

    // Phase 6.10 Task 19: helper to broadcast login outcome to admin dashboard.
    // Called AFTER LogAttemptAsync so the audit_log row is written first.
    // Strategy A: emit only at the 3 LogAttemptAsync sites (success + auth-fail +
    // 2FA-fail). Pre-validation rejects (empty/format/rate-limit) do not emit —
    // the dashboard activity feed isn't trying to capture every micro-failure.
    private async Task EmitLoginAsync(string email, bool success, string ip)
    {
        await _hub.Clients.Group("admins").SendAsync("UserLogin", new
        {
            email,
            success,
            ipAddress = ip,
            createdAt = DateTimeOffset.UtcNow
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
