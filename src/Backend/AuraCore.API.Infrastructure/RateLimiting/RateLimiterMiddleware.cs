using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace AuraCore.API.Infrastructure.RateLimiting;

/// <summary>
/// Phase 6.15.3 — convention-based middleware. Reads <see cref="RateLimitedAttribute"/>
/// from the matched endpoint, hashes the caller's remote IP into a bucket key,
/// and consults <see cref="IAuraCoreRateLimiter"/> (method-injected). When the
/// limiter denies, returns 429 with a <c>Retry-After</c> header.
/// </summary>
public sealed class RateLimiterMiddleware
{
    private readonly RequestDelegate _next;

    public RateLimiterMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext ctx, IAuraCoreRateLimiter limiter)
    {
        var endpoint = ctx.GetEndpoint();
        var attr = endpoint?.Metadata.GetMetadata<RateLimitedAttribute>();
        if (attr is null)
        {
            await _next(ctx);
            return;
        }

        var bucketKey = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = await limiter.TryAcquireAsync(attr.PolicyName, bucketKey, ctx.RequestAborted);
        if (!result.Allowed)
        {
            ctx.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            var retrySeconds = (int)Math.Ceiling(result.RetryAfter.TotalSeconds);
            ctx.Response.Headers["Retry-After"] = retrySeconds.ToString(CultureInfo.InvariantCulture);
            await ctx.Response.WriteAsync($"Rate limit exceeded. Retry after {retrySeconds} seconds.");
            return;
        }

        await _next(ctx);
    }
}

public static class RateLimiterMiddlewareExtensions
{
    public static IApplicationBuilder UseAuraCoreRateLimiter(this IApplicationBuilder app)
        => app.UseMiddleware<RateLimiterMiddleware>();
}
