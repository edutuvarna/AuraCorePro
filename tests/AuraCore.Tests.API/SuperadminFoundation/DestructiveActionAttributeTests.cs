using System.Security.Claims;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Filters;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class DestructiveActionAttributeTests
{
    private static (AuraCoreDbContext db, AuthorizationFilterContext ctx) BuildCtx(string role, bool isReadonly)
    {
        var opt = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"da-{Guid.NewGuid()}").Options;
        var db = new AuraCoreDbContext(opt);
        var id = Guid.NewGuid();
        db.Users.Add(new User { Id = id, Email = "a@x.com", PasswordHash = "x", Role = role, IsReadonly = isReadonly });
        db.SaveChanges();

        var services = new ServiceCollection().AddSingleton(db).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Response.Body = new MemoryStream();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.Role, role), new Claim("sub", id.ToString()),
        }, "Bearer"));
        var actionCtx = new ActionContext(http, new RouteData(), new ActionDescriptor());
        return (db, new AuthorizationFilterContext(actionCtx, Array.Empty<IFilterMetadata>()));
    }

    [Fact]
    public async Task Normal_admin_passes()
    {
        var (_, ctx) = BuildCtx("admin", isReadonly: false);
        await new DestructiveActionAttribute().OnAuthorizationAsync(ctx);
        Assert.Null(ctx.Result);
    }

    [Fact]
    public async Task Superadmin_passes()
    {
        var (_, ctx) = BuildCtx("superadmin", isReadonly: false);
        await new DestructiveActionAttribute().OnAuthorizationAsync(ctx);
        Assert.Null(ctx.Result);
    }

    [Fact]
    public async Task Readonly_admin_is_403_readonly_account()
    {
        var (_, ctx) = BuildCtx("admin", isReadonly: true);
        await new DestructiveActionAttribute().OnAuthorizationAsync(ctx);
        Assert.Equal(403, ctx.HttpContext.Response.StatusCode);
        ctx.HttpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(ctx.HttpContext.Response.Body).ReadToEndAsync();
        Assert.Contains("readonly_account", body);
    }
}
