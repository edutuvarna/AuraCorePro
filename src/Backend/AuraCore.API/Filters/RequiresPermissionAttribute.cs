using AuraCore.API.Helpers;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.API.Filters;

/// <summary>
/// Authorization filter that enforces per-permission grants on admin users
/// (spec D5). Superadmin bypasses. Non-admin is 403. ReadOnly admin is 403
/// on any non-tab key. Otherwise checks permission_grants for an active,
/// non-revoked, non-expired grant.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class RequiresPermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    public string Permission { get; }
    public RequiresPermissionAttribute(string permission) { Permission = permission; }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        var role = user.GetPrimaryRole();

        if (role == "superadmin") return;
        if (role != "admin")
        {
            context.Result = new ForbidResult();
            return;
        }

        var userId = user.GetUserId();
        if (userId is null)
        {
            context.Result = new ForbidResult();
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<AuraCoreDbContext>();

        // ReadOnly admins fail for any non-tab permission (Tier 2 actions blocked).
        if (PermissionKeys.IsActionKey(Permission))
        {
            var isReadonly = await db.Users
                .Where(u => u.Id == userId.Value)
                .Select(u => u.IsReadonly)
                .FirstOrDefaultAsync();
            if (isReadonly)
            {
                await Write403(context, "readonly_account");
                return;
            }
        }

        var hasGrant = await db.PermissionGrants.AnyAsync(g =>
            g.AdminUserId == userId.Value
            && g.PermissionKey == Permission
            && g.RevokedAt == null
            && (g.ExpiresAt == null || g.ExpiresAt > DateTimeOffset.UtcNow));

        if (!hasGrant)
            await Write403(context, "permission_required");
    }

    private async Task Write403(AuthorizationFilterContext ctx, string errorCode)
    {
        ctx.HttpContext.Response.StatusCode = 403;
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsync(
            $"{{\"error\":\"{errorCode}\",\"permission\":\"{Permission}\"}}");
        ctx.Result = new EmptyResult();
    }
}
