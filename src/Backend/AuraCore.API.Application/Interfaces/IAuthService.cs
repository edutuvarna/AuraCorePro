using AuraCore.API.Domain.Entities;

namespace AuraCore.API.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string email, string password, CancellationToken ct = default);
    Task<AuthResult> LoginAsync(string email, string password, CancellationToken ct = default);
    Task<AuthResult> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
}

public sealed record AuthResult(
    bool Success,
    string? AccessToken = null,
    string? RefreshToken = null,
    string? Error = null,
    UserDto? User = null);

public sealed record UserDto(
    Guid Id,
    string Email,
    string Role,
    string Tier = "free");
