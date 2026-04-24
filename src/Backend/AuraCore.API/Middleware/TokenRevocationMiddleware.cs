using System.IdentityModel.Tokens.Jwt;
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Middleware;

/// <summary>
/// Rejects authenticated requests whose JWT 'jti' appears in revoked_tokens
/// (spec D13 — suspended admin, password reset, logout-all, admin-deleted).
/// Runs AFTER UseAuthentication so HttpContext.User has claims populated.
/// Unauthenticated requests and tokens without a jti claim pass through
/// (auth / login endpoints don't carry a jti until they mint one).
/// </summary>
public class TokenRevocationMiddleware
{
    private readonly RequestDelegate _next;

    public TokenRevocationMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, AuraCoreDbContext db)
    {
        if (ctx.User.Identity?.IsAuthenticated != true)
        {
            await _next(ctx);
            return;
        }

        var jti = ctx.User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        if (string.IsNullOrEmpty(jti))
        {
            await _next(ctx);
            return;
        }

        var revoked = await db.RevokedTokens.AnyAsync(r => r.Jti == jti);
        if (revoked)
        {
            ctx.Response.StatusCode = 401;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync("{\"error\":\"token_revoked\"}");
            return;
        }

        await _next(ctx);
    }
}
