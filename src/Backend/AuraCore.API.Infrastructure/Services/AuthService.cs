using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AuraCore.API.Application.Interfaces;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AuraCore.API.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private readonly AuraCoreDbContext _db;
    private readonly IConfiguration _config;

    public AuthService(AuraCoreDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<AuthResult> RegisterAsync(string email, string password, CancellationToken ct = default)
    {
        var existing = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (existing is not null)
            return new AuthResult(false, Error: "Email already registered");

        var user = new User
        {
            Email = email.ToLowerInvariant().Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = "user"
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
        });
        await _db.SaveChangesAsync(ct);

        return new AuthResult(true, accessToken, refreshToken,
            User: new UserDto(user.Id, user.Email, user.Role));
    }

    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant().Trim(), ct);
        if (user is null)
            return new AuthResult(false, Error: "Invalid email or password");

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return new AuthResult(false, Error: "Invalid email or password");

        // Fetch active license tier
        var tier = "free";
        if (user.Role == "admin")
        {
            tier = "admin";
        }
        else
        {
            var license = await _db.Licenses
                .FirstOrDefaultAsync(l => l.UserId == user.Id && l.Status == "active"
                    && (!l.ExpiresAt.HasValue || l.ExpiresAt > DateTimeOffset.UtcNow), ct);
            if (license is not null) tier = license.Tier;
        }

        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
        });
        await _db.SaveChangesAsync(ct);

        return new AuthResult(true, accessToken, refreshToken,
            User: new UserDto(user.Id, user.Email, user.Role, tier));
    }

    public async Task<AuthResult> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var stored = await _db.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == refreshToken && !r.IsRevoked, ct);

        if (stored is null || stored.ExpiresAt < DateTimeOffset.UtcNow)
            return new AuthResult(false, Error: "Invalid or expired refresh token");

        stored.IsRevoked = true;

        var newAccessToken = GenerateAccessToken(stored.User);
        var newRefreshToken = GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = stored.UserId,
            Token = newRefreshToken,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
        });
        await _db.SaveChangesAsync(ct);

        // Fetch tier for refreshed session
        var tier = "free";
        if (stored.User.Role == "admin")
        {
            tier = "admin";
        }
        else
        {
            var license = await _db.Licenses
                .FirstOrDefaultAsync(l => l.UserId == stored.UserId && l.Status == "active"
                    && (!l.ExpiresAt.HasValue || l.ExpiresAt > DateTimeOffset.UtcNow), ct);
            if (license is not null) tier = license.Tier;
        }

        return new AuthResult(true, newAccessToken, newRefreshToken,
            User: new UserDto(stored.User.Id, stored.User.Email, stored.User.Role, tier));
    }

    public string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Secret"] ?? "AuraCorePro-Default-Secret-Key-Change-In-Production-Min32Chars!"));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("sub", user.Id.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "AuraCorePro",
            audience: _config["Jwt:Audience"] ?? "AuraCorePro",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}
