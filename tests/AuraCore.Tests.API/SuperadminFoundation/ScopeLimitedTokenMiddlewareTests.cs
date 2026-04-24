using System.Security.Claims;
using AuraCore.API.Middleware;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class ScopeLimitedTokenMiddlewareTests
{
    private static DefaultHttpContext BuildCtx(string? scope, string path)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        ctx.Response.Body = new MemoryStream();
        var claims = new List<Claim>();
        if (scope != null) claims.Add(new Claim("scope", scope));
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        return ctx;
    }

    [Fact]
    public async Task Passes_through_when_no_scope_claim()
    {
        var ctx = BuildCtx(null, "/api/admin/users");
        var called = false;
        var mw = new ScopeLimitedTokenMiddleware(_ => { called = true; return Task.CompletedTask; });
        await mw.InvokeAsync(ctx);
        Assert.True(called);
    }

    [Fact]
    public async Task Returns_403_when_scope_limited_token_hits_admin_endpoint()
    {
        var ctx = BuildCtx("2fa-setup-only", "/api/admin/users");
        var called = false;
        var mw = new ScopeLimitedTokenMiddleware(_ => { called = true; return Task.CompletedTask; });
        await mw.InvokeAsync(ctx);
        Assert.False(called);
        Assert.Equal(403, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task Allows_scope_limited_token_on_enable_2fa()
    {
        var ctx = BuildCtx("2fa-setup-only", "/api/auth/enable-2fa");
        var called = false;
        var mw = new ScopeLimitedTokenMiddleware(_ => { called = true; return Task.CompletedTask; });
        await mw.InvokeAsync(ctx);
        Assert.True(called);
    }

    [Fact]
    public async Task Allows_scope_limited_token_on_logout()
    {
        var ctx = BuildCtx("2fa-setup-only", "/api/auth/logout");
        var called = false;
        var mw = new ScopeLimitedTokenMiddleware(_ => { called = true; return Task.CompletedTask; });
        await mw.InvokeAsync(ctx);
        Assert.True(called);
    }
}
