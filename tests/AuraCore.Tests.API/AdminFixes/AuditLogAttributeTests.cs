using AuraCore.API.Application.Services.Audit;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using AuditSvc = AuraCore.API.Infrastructure.Services.Audit.AuditLogService;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuraCore.Tests.API.AdminFixes;

public class AuditLogAttributeTests
{
    private static AuraCoreDbContext BuildDb()
    {
        var options = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"audit-{Guid.NewGuid()}").Options;
        return new AuraCoreDbContext(options);
    }

    [Fact]
    public async Task LogAsync_persists_entry_with_all_fields()
    {
        var db = BuildDb();
        var svc = new AuditSvc(db);
        var actorId = Guid.NewGuid();

        await svc.LogAsync(
            actorId: actorId,
            actorEmail: "admin@auracore.pro",
            action: "GrantSubscription",
            targetType: "License",
            targetId: "abc-123",
            beforeData: "{\"tier\":\"free\"}",
            afterData: "{\"tier\":\"pro\"}",
            ipAddress: "192.168.1.1",
            ct: CancellationToken.None);

        var rows = await db.AuditLogs.ToListAsync();
        Assert.Single(rows);
        var row = rows[0];
        Assert.Equal(actorId, row.ActorId);
        Assert.Equal("admin@auracore.pro", row.ActorEmail);
        Assert.Equal("GrantSubscription", row.Action);
        Assert.Equal("License", row.TargetType);
        Assert.Equal("abc-123", row.TargetId);
        Assert.Contains("pro", row.AfterData ?? "");
    }

    [Fact]
    public async Task LogAsync_accepts_null_actor_for_system_actions()
    {
        var db = BuildDb();
        var svc = new AuditSvc(db);

        await svc.LogAsync(
            actorId: null,
            actorEmail: "system@auracore.pro",
            action: "AutoUpdate",
            targetType: "System",
            targetId: null,
            beforeData: null,
            afterData: null,
            ipAddress: null,
            ct: CancellationToken.None);

        var rows = await db.AuditLogs.ToListAsync();
        Assert.Single(rows);
        Assert.Null(rows[0].ActorId);
    }
}
