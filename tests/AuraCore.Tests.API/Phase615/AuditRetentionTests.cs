using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.API.Controllers.Superadmin;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using AuraCore.API.Infrastructure.Services.Background;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AuraCore.Tests.API.Phase615;

public sealed class AuditRetentionTests
{
    private static AuraCoreDbContext NewDb()
    {
        var opt = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"audit-retention-{Guid.NewGuid()}").Options;
        return new AuraCoreDbContext(opt);
    }

    [Fact]
    public async Task RunCleanup_DeletesOnlyOldRows()
    {
        await using var db = NewDb();
        db.SystemSettings.Add(new SystemSetting { Key = "audit_retention.retentionDays", Value = "30" });
        var oldDate = DateTimeOffset.UtcNow.AddDays(-60);
        var newDate = DateTimeOffset.UtcNow.AddDays(-5);
        db.AuditLogs.AddRange(
            new AuditLogEntry { ActorEmail = "a", Action = "X", TargetType = "T", CreatedAt = oldDate },
            new AuditLogEntry { ActorEmail = "a", Action = "X", TargetType = "T", CreatedAt = oldDate },
            new AuditLogEntry { ActorEmail = "a", Action = "X", TargetType = "T", CreatedAt = newDate });
        await db.SaveChangesAsync();

        var deleted = await AuditLogCleanupService.RunCleanupAsync(db, NullLogger.Instance, CancellationToken.None);

        Assert.Equal(2, deleted);
        // After cleanup: 1 surviving original (newDate) + 1 retention_run audit_log row added by the cleanup itself
        var remaining = await db.AuditLogs.CountAsync();
        Assert.Equal(2, remaining);
        Assert.Contains(await db.AuditLogs.ToListAsync(), e => e.Action == "AuditRetentionRun");
    }

    [Fact]
    public async Task RunCleanup_WritesRetentionRunAuditRow()
    {
        await using var db = NewDb();
        db.SystemSettings.Add(new SystemSetting { Key = "audit_retention.retentionDays", Value = "30" });
        await db.SaveChangesAsync();

        await AuditLogCleanupService.RunCleanupAsync(db, NullLogger.Instance, CancellationToken.None);

        var run = await db.AuditLogs.FirstOrDefaultAsync(e => e.Action == "AuditRetentionRun");
        Assert.NotNull(run);
        Assert.Equal("System", run!.TargetType);
        Assert.Contains("\"deleted\":0", run.AfterData ?? "");
        Assert.Contains("\"retentionDays\":30", run.AfterData ?? "");
    }

    [Fact]
    public async Task PolicyEndpoint_ReturnsCurrentSettings()
    {
        await using var db = NewDb();
        db.SystemSettings.AddRange(
            new SystemSetting { Key = "audit_retention.retentionDays", Value = "180" },
            new SystemSetting { Key = "audit_retention.lastRunAt", Value = "2026-04-28T03:00:00+00:00" },
            new SystemSetting { Key = "audit_retention.lastRunDeletedRows", Value = "1234" });
        db.AuditLogs.Add(new AuditLogEntry
        {
            ActorEmail = "a",
            Action = "X",
            TargetType = "T",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-100)
        });
        await db.SaveChangesAsync();

        var ctrl = new AuditRetentionController(db);
        var result = await ctrl.GetPolicy(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"retentionDays\":180", json);
        Assert.Contains("\"lastRunDeletedRows\":1234", json);
    }

    [Fact]
    public async Task PolicyEndpoint_PostUpdatesRetentionDays()
    {
        await using var db = NewDb();
        db.SystemSettings.Add(new SystemSetting { Key = "audit_retention.retentionDays", Value = "365" });
        await db.SaveChangesAsync();

        var ctrl = new AuditRetentionController(db);
        var result = await ctrl.SetPolicy(new SetRetentionDto(90), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var stored = await db.SystemSettings.FirstAsync(s => s.Key == "audit_retention.retentionDays");
        Assert.Equal("90", stored.Value);
    }

    [Fact]
    public async Task SetPolicy_RejectsOutOfRange()
    {
        await using var db = NewDb();
        var ctrl = new AuditRetentionController(db);
        var resultLow = await ctrl.SetPolicy(new SetRetentionDto(29), CancellationToken.None);
        var resultHigh = await ctrl.SetPolicy(new SetRetentionDto(3651), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(resultLow);
        Assert.IsType<BadRequestObjectResult>(resultHigh);
    }
}
