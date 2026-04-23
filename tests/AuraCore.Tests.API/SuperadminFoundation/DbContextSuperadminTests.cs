using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class DbContextSuperadminTests
{
    private static AuraCoreDbContext BuildDb()
    {
        var options = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"sa-{Guid.NewGuid()}")
            .Options;
        return new AuraCoreDbContext(options);
    }

    [Fact]
    public async Task DbContext_exposes_five_new_DbSets()
    {
        var db = BuildDb();
        db.PermissionGrants.Add(new PermissionGrant { AdminUserId = Guid.NewGuid(), PermissionKey = "tab:updates", GrantedBy = Guid.NewGuid() });
        db.PermissionRequests.Add(new PermissionRequest { AdminUserId = Guid.NewGuid(), PermissionKey = "tab:updates", Reason = "testing" });
        db.RevokedTokens.Add(new RevokedToken { Jti = "abc", UserId = Guid.NewGuid(), RevokeReason = "logout" });
        db.AdminInvitations.Add(new AdminInvitation { TokenHash = "hash123", AdminUserId = Guid.NewGuid(), CreatedBy = Guid.NewGuid(), ExpiresAt = DateTimeOffset.UtcNow.AddDays(7) });
        db.SystemSettings.Add(new SystemSetting { Key = "k", Value = "v" });
        await db.SaveChangesAsync();

        Assert.Equal(1, await db.PermissionGrants.CountAsync());
        Assert.Equal(1, await db.PermissionRequests.CountAsync());
        Assert.Equal(1, await db.RevokedTokens.CountAsync());
        Assert.Equal(1, await db.AdminInvitations.CountAsync());
        Assert.Equal(1, await db.SystemSettings.CountAsync());
    }

    [Fact]
    public async Task User_new_fields_persist_round_trip()
    {
        var db = BuildDb();
        var uid = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = uid, Email = "a@b.com", PasswordHash = "x", Role = "admin",
            IsActive = false, IsReadonly = true, Require2fa = true, CreatedVia = "superadmin_create",
        });
        await db.SaveChangesAsync();

        var back = await db.Users.FirstAsync(u => u.Id == uid);
        Assert.False(back.IsActive);
        Assert.True(back.IsReadonly);
        Assert.True(back.Require2fa);
        Assert.Equal("superadmin_create", back.CreatedVia);
    }
}
