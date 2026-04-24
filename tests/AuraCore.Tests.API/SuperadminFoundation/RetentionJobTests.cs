using AuraCore.API.Domain.Entities;
using AuraCore.API.HostedServices;
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class RetentionJobTests
{
    private static AuraCoreDbContext BuildDb()
    {
        var opt = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"ret-{Guid.NewGuid()}").Options;
        return new AuraCoreDbContext(opt);
    }

    [Fact]
    public async Task Sweep_deletes_old_revoked_tokens()
    {
        var db = BuildDb();
        var u = new User { Id = Guid.NewGuid(), Email = "a@x.com", PasswordHash = "x", Role = "admin" };
        db.Users.Add(u);
        db.RevokedTokens.Add(new RevokedToken { Jti = "old", UserId = u.Id, RevokeReason = "logout", RevokedAt = DateTime.UtcNow.AddHours(-3) });
        db.RevokedTokens.Add(new RevokedToken { Jti = "fresh", UserId = u.Id, RevokeReason = "logout", RevokedAt = DateTime.UtcNow.AddMinutes(-30) });
        await db.SaveChangesAsync();

        await RetentionJob.SweepAsync(db, NullLogger.Instance);

        var remaining = await db.RevokedTokens.Select(r => r.Jti).ToListAsync();
        Assert.DoesNotContain("old", remaining);
        Assert.Contains("fresh", remaining);
    }

    [Fact]
    public async Task Sweep_deletes_expired_unconsumed_invitations()
    {
        var db = BuildDb();
        var u = new User { Id = Guid.NewGuid(), Email = "a@x.com", PasswordHash = "x", Role = "admin" };
        var su = new User { Id = Guid.NewGuid(), Email = "s@x.com", PasswordHash = "x", Role = "superadmin" };
        db.Users.AddRange(u, su);
        db.AdminInvitations.Add(new AdminInvitation {
            TokenHash = "old-inv", AdminUserId = u.Id, CreatedBy = su.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(-31),
        });
        db.AdminInvitations.Add(new AdminInvitation {
            TokenHash = "accepted", AdminUserId = u.Id, CreatedBy = su.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(-31), ConsumedAt = DateTime.UtcNow.AddDays(-35),
        });
        db.AdminInvitations.Add(new AdminInvitation {
            TokenHash = "fresh-inv", AdminUserId = u.Id, CreatedBy = su.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(5),
        });
        await db.SaveChangesAsync();

        await RetentionJob.SweepAsync(db, NullLogger.Instance);

        var remaining = await db.AdminInvitations.Select(i => i.TokenHash).ToListAsync();
        Assert.DoesNotContain("old-inv", remaining);
        Assert.Contains("accepted", remaining);   // consumed — keep for audit
        Assert.Contains("fresh-inv", remaining);  // not yet expired
    }

    [Fact]
    public async Task Sweep_does_not_touch_audit_log()
    {
        var db = BuildDb();
        db.AuditLogs.Add(new AuditLogEntry { ActorEmail = "old@x.com", Action = "X", TargetType = "Y", CreatedAt = DateTimeOffset.UtcNow.AddYears(-5) });
        await db.SaveChangesAsync();

        await RetentionJob.SweepAsync(db, NullLogger.Instance);

        // Spec: audit_log retention deferred to 6.12
        Assert.Equal(1, await db.AuditLogs.CountAsync());
    }
}
