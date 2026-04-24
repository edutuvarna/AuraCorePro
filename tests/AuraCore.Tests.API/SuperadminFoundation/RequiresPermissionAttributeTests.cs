using System.Security.Claims;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Filters;
using AuraCore.API.Helpers;
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

public class RequiresPermissionAttributeTests
{
    private static AuraCoreDbContext BuildDb()
    {
        var opt = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"rp-{Guid.NewGuid()}").Options;
        return new AuraCoreDbContext(opt);
    }

    private static AuthorizationFilterContext BuildCtx(AuraCoreDbContext db, string? role, Guid? userId)
    {
        var services = new ServiceCollection().AddSingleton(db).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Response.Body = new MemoryStream();
        var claims = new List<Claim>();
        if (role != null) claims.Add(new Claim(ClaimTypes.Role, role));
        if (userId.HasValue) claims.Add(new Claim("sub", userId.Value.ToString()));
        http.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        var actionCtx = new ActionContext(http, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionCtx, Array.Empty<IFilterMetadata>());
    }

    [Fact]
    public async Task Superadmin_always_passes()
    {
        var db = BuildDb();
        var ctx = BuildCtx(db, "superadmin", Guid.NewGuid());
        var attr = new RequiresPermissionAttribute(PermissionKeys.ActionUsersDelete);
        await attr.OnAuthorizationAsync(ctx);
        Assert.Null(ctx.Result);
    }

    [Fact]
    public async Task NonAdmin_is_403()
    {
        var db = BuildDb();
        var ctx = BuildCtx(db, "user", Guid.NewGuid());
        var attr = new RequiresPermissionAttribute(PermissionKeys.ActionUsersDelete);
        await attr.OnAuthorizationAsync(ctx);
        Assert.IsType<ForbidResult>(ctx.Result);
    }

    [Fact]
    public async Task Admin_without_grant_is_403_with_permission_required_body()
    {
        var db = BuildDb();
        var adminId = Guid.NewGuid();
        db.Users.Add(new User { Id = adminId, Email = "a@x.com", PasswordHash = "x", Role = "admin" });
        await db.SaveChangesAsync();

        var ctx = BuildCtx(db, "admin", adminId);
        var attr = new RequiresPermissionAttribute(PermissionKeys.ActionUsersDelete);
        await attr.OnAuthorizationAsync(ctx);

        Assert.Equal(403, ctx.HttpContext.Response.StatusCode);
        ctx.HttpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(ctx.HttpContext.Response.Body).ReadToEndAsync();
        Assert.Contains("permission_required", body);
        Assert.Contains("action:users.delete", body);
    }

    [Fact]
    public async Task Admin_with_active_grant_passes()
    {
        var db = BuildDb();
        var adminId = Guid.NewGuid();
        db.Users.Add(new User { Id = adminId, Email = "a@x.com", PasswordHash = "x", Role = "admin" });
        db.PermissionGrants.Add(new PermissionGrant {
            AdminUserId = adminId, PermissionKey = PermissionKeys.ActionUsersDelete, GrantedBy = adminId,
        });
        await db.SaveChangesAsync();

        var ctx = BuildCtx(db, "admin", adminId);
        var attr = new RequiresPermissionAttribute(PermissionKeys.ActionUsersDelete);
        await attr.OnAuthorizationAsync(ctx);

        Assert.Null(ctx.Result);
    }

    [Fact]
    public async Task Admin_with_expired_grant_is_403()
    {
        var db = BuildDb();
        var adminId = Guid.NewGuid();
        db.Users.Add(new User { Id = adminId, Email = "a@x.com", PasswordHash = "x", Role = "admin" });
        db.PermissionGrants.Add(new PermissionGrant {
            AdminUserId = adminId, PermissionKey = PermissionKeys.ActionUsersDelete, GrantedBy = adminId,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        });
        await db.SaveChangesAsync();

        var ctx = BuildCtx(db, "admin", adminId);
        var attr = new RequiresPermissionAttribute(PermissionKeys.ActionUsersDelete);
        await attr.OnAuthorizationAsync(ctx);

        Assert.Equal(403, ctx.HttpContext.Response.StatusCode);
    }

    [Fact]
    public async Task Admin_with_revoked_grant_is_403()
    {
        var db = BuildDb();
        var adminId = Guid.NewGuid();
        db.Users.Add(new User { Id = adminId, Email = "a@x.com", PasswordHash = "x", Role = "admin" });
        db.PermissionGrants.Add(new PermissionGrant {
            AdminUserId = adminId, PermissionKey = PermissionKeys.ActionUsersDelete, GrantedBy = adminId,
            RevokedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var ctx = BuildCtx(db, "admin", adminId);
        var attr = new RequiresPermissionAttribute(PermissionKeys.ActionUsersDelete);
        await attr.OnAuthorizationAsync(ctx);

        Assert.Equal(403, ctx.HttpContext.Response.StatusCode);
    }

    [Fact]
    public async Task Readonly_admin_fails_on_action_permission_even_with_grant()
    {
        var db = BuildDb();
        var adminId = Guid.NewGuid();
        db.Users.Add(new User { Id = adminId, Email = "a@x.com", PasswordHash = "x", Role = "admin", IsReadonly = true });
        db.PermissionGrants.Add(new PermissionGrant {
            AdminUserId = adminId, PermissionKey = PermissionKeys.ActionUsersDelete, GrantedBy = adminId,
        });
        await db.SaveChangesAsync();

        var ctx = BuildCtx(db, "admin", adminId);
        var attr = new RequiresPermissionAttribute(PermissionKeys.ActionUsersDelete);
        await attr.OnAuthorizationAsync(ctx);

        Assert.Equal(403, ctx.HttpContext.Response.StatusCode);
        ctx.HttpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(ctx.HttpContext.Response.Body).ReadToEndAsync();
        Assert.Contains("readonly_account", body);
    }

    [Fact]
    public async Task Readonly_admin_passes_on_tab_permission_with_grant()
    {
        var db = BuildDb();
        var adminId = Guid.NewGuid();
        db.Users.Add(new User { Id = adminId, Email = "a@x.com", PasswordHash = "x", Role = "admin", IsReadonly = true });
        db.PermissionGrants.Add(new PermissionGrant {
            AdminUserId = adminId, PermissionKey = PermissionKeys.TabConfiguration, GrantedBy = adminId,
        });
        await db.SaveChangesAsync();

        var ctx = BuildCtx(db, "admin", adminId);
        var attr = new RequiresPermissionAttribute(PermissionKeys.TabConfiguration);
        await attr.OnAuthorizationAsync(ctx);

        Assert.Null(ctx.Result);
    }
}
