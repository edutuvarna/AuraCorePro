namespace AuraCore.API.Middleware;

/// <summary>
/// Rejects scope-limited JWTs (claim <c>scope="2fa-setup-only"</c>) on every
/// endpoint except <c>/api/auth/enable-2fa</c>, <c>/api/auth/logout</c>, and
/// <c>/api/auth/me</c> (spec D — 2FA enforcement; T33). Runs AFTER
/// <see cref="TokenRevocationMiddleware"/> so a revoked scope-limited token
/// is rejected for revocation first (401 over 403).
/// Tokens without a scope claim pass through unchanged.
/// </summary>
public class ScopeLimitedTokenMiddleware
{
    private static readonly HashSet<string> AllowedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/enable-2fa",
        "/api/auth/logout",
        "/api/auth/me",
    };

    private readonly RequestDelegate _next;
    public ScopeLimitedTokenMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        var scope = ctx.User.FindFirst("scope")?.Value;
        if (string.IsNullOrEmpty(scope))
        {
            await _next(ctx);
            return;
        }

        var path = ctx.Request.Path.Value ?? "";
        if (AllowedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(ctx);
            return;
        }

        ctx.Response.StatusCode = 403;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync("{\"error\":\"scope_limited_token\",\"scope\":\"" + scope + "\"}");
    }
}
