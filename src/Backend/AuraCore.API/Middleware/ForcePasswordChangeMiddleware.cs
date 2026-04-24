using AuraCore.API.Helpers;
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Middleware;

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
            .FirstOrDefaultAsync();
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
