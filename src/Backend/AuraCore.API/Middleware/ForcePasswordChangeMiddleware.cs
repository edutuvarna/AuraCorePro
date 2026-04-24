using AuraCore.API.Helpers;
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Middleware;

/// <summary>
/// Blocks authenticated requests when the user has <c>ForcePasswordChange=true</c>
/// AND <c>(ForcePasswordChangeBy ?? UtcNow) &lt;= UtcNow</c>, except on the
/// allow-list (<c>/api/auth/change-password</c>, <c>/api/auth/logout</c>,
/// <c>/api/auth/me</c>). Denied requests receive 403 with JSON body
/// <c>{"error":"password_change_required"}</c>. Runs AFTER
/// <see cref="ScopeLimitedTokenMiddleware"/> so scope-limited tokens are
/// handled first (spec D — password reset enforcement; T34).
/// </summary>
public class ForcePasswordChangeMiddleware
{
    private static readonly HashSet<string> AllowedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/change-password",
        "/api/auth/logout",
        "/api/auth/me",
    };

    private readonly RequestDelegate _next;
    public ForcePasswordChangeMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, AuraCoreDbContext db)
    {
        if (ctx.User.Identity?.IsAuthenticated != true) { await _next(ctx); return; }

        var userId = ctx.User.GetUserId();
        if (userId is null) { await _next(ctx); return; }

        var path = ctx.Request.Path.Value ?? "";
        if (AllowedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase))) { await _next(ctx); return; }

        var user = await db.Users
            .Where(u => u.Id == userId.Value)
            .Select(u => new { u.ForcePasswordChange, u.ForcePasswordChangeBy })
            .FirstOrDefaultAsync(ctx.RequestAborted);
        if (user is null || !user.ForcePasswordChange) { await _next(ctx); return; }

        var deadline = user.ForcePasswordChangeBy ?? DateTimeOffset.UtcNow; // null deadline = due now
        if (deadline <= DateTimeOffset.UtcNow)
        {
            ctx.Response.StatusCode = 403;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync("{\"error\":\"password_change_required\"}");
            return;
        }

        await _next(ctx);
    }
}
