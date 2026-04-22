namespace AuraCore.API.Application.Services.Audit;

public interface IAuditLogService
{
    Task LogAsync(
        Guid? actorId,
        string actorEmail,
        string action,
        string targetType,
        string? targetId,
        string? beforeData,
        string? afterData,
        string? ipAddress,
        CancellationToken ct);
}
