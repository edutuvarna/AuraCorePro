using System.Security.Claims;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using AuraCore.API.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class ForcePasswordChangeMiddlewareTests
{
    private static AuraCoreDbContext BuildDb()
    {
        var opt = new DbContextOptionsBuilder<AuraCoreDbContext>().UseInMemoryDatabase($"fp-{Guid.NewGuid()}").Options;
        return new AuraCoreDbContext(opt);
    }

    private static DefaultHttpContext BuildCtx(AuraCoreDbContext db, Guid userId, string path)
    {
        var ctx = new DefaultHttpContext();
        ctx.RequestServices = new ServiceCollection().AddSingleton(db).BuildServiceProvider();
        ctx.Request.Path = path;
        ctx.Response.Body = new MemoryStream();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", userId.ToString()) }, "Bearer"));
        return ctx;
    }

    [Fact]
    public async Task Passes_through_when_user_has_no_flag()
    {
        var db = BuildDb();
        var uid = Guid.NewGuid();
        db.Users.Add(new User { Id = uid, Email = "a@x.com", PasswordHash = "x", Role = "admin" });
        await db.SaveChangesAsync();

        var called = false;
        var mw = new ForcePasswordChangeMiddleware(_ => { called = true; return Task.CompletedTask; });
        await mw.InvokeAsync(BuildCtx(db, uid, "/api/admin/users"), db);
        Assert.True(called);
    }

    [Fact]
    public async Task Returns_403_when_deadline_has_passed()
    {
        var db = BuildDb();
        var uid = Guid.NewGuid();
        db.Users.Add(new User {
            Id = uid, Email = "a@x.com", PasswordHash = "x", Role = "admin",
            ForcePasswordChange = true,
            ForcePasswordChangeBy = DateTimeOffset.UtcNow.AddMinutes(-1),
        });
        await db.SaveChangesAsync();

        var ctx = BuildCtx(db, uid, "/api/admin/users");
        var called = false;
        var mw = new ForcePasswordChangeMiddleware(_ => { called = true; return Task.CompletedTask; });
        await mw.InvokeAsync(ctx, db);

        Assert.False(called);
        Assert.Equal(403, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task Allows_change_password_endpoint_even_after_deadline()
    {
        var db = BuildDb();
        var uid = Guid.NewGuid();
        db.Users.Add(new User {
            Id = uid, Email = "a@x.com", PasswordHash = "x", Role = "admin",
            ForcePasswordChange = true,
            ForcePasswordChangeBy = DateTimeOffset.UtcNow.AddMinutes(-1),
        });
        await db.SaveChangesAsync();

        var ctx = BuildCtx(db, uid, "/api/auth/change-password");
        var called = false;
        var mw = new ForcePasswordChangeMiddleware(_ => { called = true; return Task.CompletedTask; });
        await mw.InvokeAsync(ctx, db);
        Assert.True(called);
    }

    [Fact]
    public async Task Passes_through_when_deadline_is_in_the_future()
    {
        var db = BuildDb();
        var uid = Guid.NewGuid();
        db.Users.Add(new User {
            Id = uid, Email = "a@x.com", PasswordHash = "x", Role = "admin",
            ForcePasswordChange = true,
            ForcePasswordChangeBy = DateTimeOffset.UtcNow.AddHours(1),
        });
        await db.SaveChangesAsync();

        var ctx = BuildCtx(db, uid, "/api/admin/users");
        var called = false;
        var mw = new ForcePasswordChangeMiddleware(_ => { called = true; return Task.CompletedTask; });
        await mw.InvokeAsync(ctx, db);

        Assert.True(called);
        Assert.NotEqual(403, ctx.Response.StatusCode);
    }
}
