using AuraCore.API.Application.Services.Audit;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;

namespace AuraCore.API.Infrastructure.Services.Audit;

public sealed class AuditLogService : IAuditLogService
{
    private readonly AuraCoreDbContext _db;
    public AuditLogService(AuraCoreDbContext db) => _db = db;

    public async Task LogAsync(
        Guid? actorId, string actorEmail, string action,
        string targetType, string? targetId,
        string? beforeData, string? afterData, string? ipAddress,
        CancellationToken ct)
    {
        _db.AuditLogs.Add(new AuditLogEntry
        {
            ActorId = actorId,
            ActorEmail = actorEmail,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            BeforeData = beforeData,
            AfterData = afterData,
            IpAddress = ipAddress,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
    }
}
