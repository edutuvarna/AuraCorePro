using AuraCore.API.Helpers;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.API.Filters;

/// <summary>
/// Marker filter for Tier 3 destructive operations (Licenses.Revoke/Activate,
/// Devices.Revoke, CrashReports.Delete per spec D1). Blocks ONLY ReadOnly
/// admins. Tier 3 is open for normal admins + superadmin.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class DestructiveActionAttribute : Attribute, IAsyncAuthorizationFilter
{
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
        var isReadonly = await db.Users
            .Where(u => u.Id == userId.Value)
            .Select(u => u.IsReadonly)
            .FirstOrDefaultAsync();

        if (isReadonly)
        {
            context.HttpContext.Response.StatusCode = 403;
            context.HttpContext.Response.ContentType = "application/json";
            await context.HttpContext.Response.WriteAsync(
                "{\"error\":\"readonly_account\",\"hint\":\"This account is read-only; destructive actions blocked.\"}");
            context.Result = new EmptyResult();
        }
    }
}
