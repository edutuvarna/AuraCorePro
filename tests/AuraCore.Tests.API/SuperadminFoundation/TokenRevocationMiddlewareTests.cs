using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using AuraCore.API.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class TokenRevocationMiddlewareTests
{
    private static AuraCoreDbContext BuildDb()
    {
        var opt = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"rev-{Guid.NewGuid()}").Options;
        return new AuraCoreDbContext(opt);
    }

    private static DefaultHttpContext BuildCtx(AuraCoreDbContext db, string? jti, bool authenticated = true)
    {
        var ctx = new DefaultHttpContext();
        ctx.RequestServices = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddSingleton(db)
            .BuildServiceProvider();
        ctx.Response.Body = new MemoryStream();
        if (authenticated)
        {
            var claims = new List<Claim>();
            if (jti != null) claims.Add(new Claim(JwtRegisteredClaimNames.Jti, jti));
            ctx.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        }
        return ctx;
    }

    [Fact]
    public async Task Passes_through_when_unauthenticated()
    {
        var db = BuildDb();
        var ctx = BuildCtx(db, null, authenticated: false);
        var called = false;
        var mw = new TokenRevocationMiddleware(_ => { called = true; return Task.CompletedTask; });
        await mw.InvokeAsync(ctx, db);
        Assert.True(called);
    }

    [Fact]
    public async Task Passes_through_when_jti_not_revoked()
    {
        var db = BuildDb();
        var ctx = BuildCtx(db, "fresh-jti-123");
        var called = false;
        var mw = new TokenRevocationMiddleware(_ => { called = true; return Task.CompletedTask; });
        await mw.InvokeAsync(ctx, db);
        Assert.True(called);
    }

    [Fact]
    public async Task Returns_401_when_jti_revoked()
    {
        var db = BuildDb();
        db.RevokedTokens.Add(new RevokedToken { Jti = "bad-jti", UserId = Guid.NewGuid(), RevokeReason = "suspend" });
        await db.SaveChangesAsync();
        var ctx = BuildCtx(db, "bad-jti");
        var called = false;
        var mw = new TokenRevocationMiddleware(_ => { called = true; return Task.CompletedTask; });
        await mw.InvokeAsync(ctx, db);
        Assert.False(called);
        Assert.Equal(401, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task Passes_through_when_jti_claim_missing()
    {
        var db = BuildDb();
        var ctx = BuildCtx(db, jti: null);
        var called = false;
        var mw = new TokenRevocationMiddleware(_ => { called = true; return Task.CompletedTask; });
        await mw.InvokeAsync(ctx, db);
        Assert.True(called);
    }
}
