using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AuraCore.API.Helpers;

public static class ClaimsExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal user)
    {
        var raw = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public static string? GetEmail(this ClaimsPrincipal user)
        => user.FindFirst(ClaimTypes.Email)?.Value;

    /// <summary>Primary role (the first one emitted by AuthService). For superadmin
    /// accounts this is "superadmin"; both "admin" and "superadmin" claims exist but
    /// the primary one determines authorization semantics.</summary>
    public static string? GetPrimaryRole(this ClaimsPrincipal user)
    {
        // Prefer superadmin if both exist
        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
        if (roles.Contains("superadmin")) return "superadmin";
        if (roles.Contains("admin")) return "admin";
        return roles.FirstOrDefault();
    }

    public static string? GetJti(this ClaimsPrincipal user)
        => user.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

    public static string? GetScope(this ClaimsPrincipal user)
        => user.FindFirst("scope")?.Value;

    public static bool IsScopeLimited(this ClaimsPrincipal user)
        => !string.IsNullOrEmpty(user.GetScope());
}
