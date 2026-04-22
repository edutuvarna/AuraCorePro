using System.Security.Claims;
using AuraCore.API.Application.Services.Audit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AuraCore.API.Filters;

[AttributeUsage(AttributeTargets.Method)]
public sealed class AuditActionAttribute : Attribute, IAsyncActionFilter
{
    public string Action { get; }
    public string TargetType { get; }
    public string? TargetIdFromRouteKey { get; init; }

    public AuditActionAttribute(string action, string targetType)
    {
        Action = action;
        TargetType = targetType;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();

        // Only log successful mutations (2xx results)
        if (executed.Result is not ObjectResult objResult || objResult.StatusCode is null || objResult.StatusCode < 200 || objResult.StatusCode >= 300)
            return;

        var actorIdStr = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? context.HttpContext.User.FindFirstValue("sub");
        var actorEmail = context.HttpContext.User.FindFirstValue(ClaimTypes.Email) ?? "unknown";

        Guid? actorId = Guid.TryParse(actorIdStr, out var g) ? g : null;
        string? targetId = null;

        if (TargetIdFromRouteKey is not null && context.RouteData.Values.TryGetValue(TargetIdFromRouteKey, out var rv))
            targetId = rv?.ToString();

        var afterData = objResult.Value is not null
            ? System.Text.Json.JsonSerializer.Serialize(objResult.Value)
            : null;

        var audit = context.HttpContext.RequestServices.GetService(typeof(IAuditLogService)) as IAuditLogService;
        if (audit is not null)
        {
            await audit.LogAsync(
                actorId, actorEmail, Action, TargetType, targetId,
                beforeData: null,
                afterData: afterData,
                ipAddress: context.HttpContext.Connection.RemoteIpAddress?.ToString(),
                ct: context.HttpContext.RequestAborted);
        }
    }
}
