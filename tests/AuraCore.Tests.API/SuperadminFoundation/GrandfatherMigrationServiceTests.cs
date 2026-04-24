using AuraCore.API.Domain.Entities;
using AuraCore.API.Helpers;
using AuraCore.API.Infrastructure.Data;
using AuraCore.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class GrandfatherMigrationServiceTests
{
    private static AuraCoreDbContext BuildDb()
    {
        var options = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"gf-{Guid.NewGuid()}")
            .Options;
        return new AuraCoreDbContext(options);
    }

    [Fact]
    public async Task RunAsync_grants_Trusted_template_to_existing_admin_with_zero_grants()
    {
        var db = BuildDb();
        var adminId = Guid.NewGuid();
        db.Users.Add(new User { Id = adminId, Email = "admin@x.com", PasswordHash = "x", Role = "admin" });
        await db.SaveChangesAsync();

        var svc = new GrandfatherMigrationService(db, NullLogger<GrandfatherMigrationService>.Instance);
        await svc.RunAsync();

        var grants = await db.PermissionGrants.Where(g => g.AdminUserId == adminId).ToListAsync();
        Assert.Equal(PermissionKeys.AllTier2.Count, grants.Count);
        Assert.All(PermissionKeys.AllTier2, key => Assert.Contains(grants, g => g.PermissionKey == key));
    }

    [Fact]
    public async Task RunAsync_is_noop_when_admin_already_has_any_active_grant()
    {
        var db = BuildDb();
        var adminId = Guid.NewGuid();
        db.Users.Add(new User { Id = adminId, Email = "admin@x.com", PasswordHash = "x", Role = "admin" });
        db.PermissionGrants.Add(new PermissionGrant { AdminUserId = adminId, PermissionKey = "tab:configuration", GrantedBy = adminId });
        await db.SaveChangesAsync();

        var svc = new GrandfatherMigrationService(db, NullLogger<GrandfatherMigrationService>.Instance);
        await svc.RunAsync();

        Assert.Equal(1, await db.PermissionGrants.CountAsync(g => g.AdminUserId == adminId));
    }

    [Fact]
    public async Task RunAsync_skips_regular_users_and_superadmins()
    {
        var db = BuildDb();
        db.Users.Add(new User { Id = Guid.NewGuid(), Email = "u@x.com", PasswordHash = "x", Role = "user" });
        db.Users.Add(new User { Id = Guid.NewGuid(), Email = "sa@x.com", PasswordHash = "x", Role = "superadmin" });
        await db.SaveChangesAsync();

        var svc = new GrandfatherMigrationService(db, NullLogger<GrandfatherMigrationService>.Instance);
        await svc.RunAsync();

        Assert.Equal(0, await db.PermissionGrants.CountAsync());
    }

    [Fact]
    public async Task RunAsync_attributes_grants_to_first_superadmin_when_available()
    {
        var db = BuildDb();
        var superId = Guid.NewGuid();
        db.Users.Add(new User { Id = superId, Email = "sa@x.com", PasswordHash = "x", Role = "superadmin" });
        db.Users.Add(new User { Id = Guid.NewGuid(), Email = "admin@x.com", PasswordHash = "x", Role = "admin" });
        await db.SaveChangesAsync();

        var svc = new GrandfatherMigrationService(db, NullLogger<GrandfatherMigrationService>.Instance);
        await svc.RunAsync();

        var grants = await db.PermissionGrants.ToListAsync();
        Assert.All(grants, g => Assert.Equal(superId, g.GrantedBy));
    }

    [Fact]
    public async Task RunAsync_idempotent_on_repeated_invocations()
    {
        var db = BuildDb();
        db.Users.Add(new User { Id = Guid.NewGuid(), Email = "admin@x.com", PasswordHash = "x", Role = "admin" });
        await db.SaveChangesAsync();

        var svc = new GrandfatherMigrationService(db, NullLogger<GrandfatherMigrationService>.Instance);
        await svc.RunAsync();
        await svc.RunAsync();
        await svc.RunAsync();

        Assert.Equal(PermissionKeys.AllTier2.Count, await db.PermissionGrants.CountAsync());
    }
}
