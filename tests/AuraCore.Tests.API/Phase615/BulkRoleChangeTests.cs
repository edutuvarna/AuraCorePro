using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.API.Application.Services.Email;
using AuraCore.API.Controllers.Superadmin;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Helpers;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuraCore.Tests.API.Phase615;

public sealed class BulkRoleChangeTests
{
    private static AuraCoreDbContext NewDb()
    {
        var opt = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"bulk-role-{Guid.NewGuid()}").Options;
        return new AuraCoreDbContext(opt);
    }

    private static AdminManagementController NewController(AuraCoreDbContext db)
        => new AdminManagementController(db, new NullEmailService());

    [Fact]
    public async Task BulkPromote_PromotesAllUsersAndAppliesTemplate()
    {
        await using var db = NewDb();
        var u1 = new User { Id = Guid.NewGuid(), Email = "a@x.com", Role = "user", IsActive = true };
        var u2 = new User { Id = Guid.NewGuid(), Email = "b@x.com", Role = "user", IsActive = true };
        db.Users.AddRange(u1, u2);
        await db.SaveChangesAsync();

        var ctrl = NewController(db);
        var dto = new AdminManagementController.BulkPromoteDto(
            new[] { u1.Id, u2.Id },
            PermissionTemplates.Trusted,
            "on_first_login",
            true);

        var result = await ctrl.BulkPromote(dto, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var promoted = await db.Users.Where(u => u.Role == "admin").CountAsync();
        Assert.Equal(2, promoted);
        // Trusted = all Tier 2 grants per user.
        var grants = await db.PermissionGrants.CountAsync();
        Assert.Equal(2 * PermissionKeys.AllTier2.Count, grants);
    }

    [Fact]
    public async Task BulkPromote_RejectsOnInvalidUserIds()
    {
        await using var db = NewDb();
        var u1 = new User { Id = Guid.NewGuid(), Email = "a@x.com", Role = "user", IsActive = true };
        db.Users.Add(u1);
        await db.SaveChangesAsync();

        var ctrl = NewController(db);
        // Include a non-existent ID — should bail out BEFORE any user is promoted.
        var dto = new AdminManagementController.BulkPromoteDto(
            new[] { u1.Id, Guid.NewGuid() },
            PermissionTemplates.Trusted,
            "never",
            false);

        var result = await ctrl.BulkPromote(dto, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        var promoted = await db.Users.Where(u => u.Role == "admin").CountAsync();
        Assert.Equal(0, promoted);
    }

    [Fact]
    public async Task BulkDemote_DemotesAllAndRevokesGrants()
    {
        await using var db = NewDb();
        var a1 = new User { Id = Guid.NewGuid(), Email = "a@x.com", Role = "admin", IsActive = true };
        var a2 = new User { Id = Guid.NewGuid(), Email = "b@x.com", Role = "admin", IsActive = true };
        db.Users.AddRange(a1, a2);
        db.PermissionGrants.AddRange(
            new PermissionGrant { Id = Guid.NewGuid(), AdminUserId = a1.Id, PermissionKey = PermissionKeys.ActionUsersDelete, GrantedAt = DateTimeOffset.UtcNow },
            new PermissionGrant { Id = Guid.NewGuid(), AdminUserId = a2.Id, PermissionKey = PermissionKeys.ActionUsersBan, GrantedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var ctrl = NewController(db);
        var dto = new AdminManagementController.BulkDemoteDto(new[] { a1.Id, a2.Id });

        var result = await ctrl.BulkDemote(dto, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var stillAdmin = await db.Users.Where(u => u.Role == "admin").CountAsync();
        Assert.Equal(0, stillAdmin);
        var activeGrants = await db.PermissionGrants.Where(g => g.RevokedAt == null).CountAsync();
        Assert.Equal(0, activeGrants);
    }

    [Fact]
    public async Task BulkPromote_RejectsCustomTemplate()
    {
        await using var db = NewDb();
        var u1 = new User { Id = Guid.NewGuid(), Email = "a@x.com", Role = "user", IsActive = true };
        db.Users.Add(u1);
        await db.SaveChangesAsync();

        var ctrl = NewController(db);
        var dto = new AdminManagementController.BulkPromoteDto(new[] { u1.Id }, PermissionTemplates.Custom, "never", false);

        var result = await ctrl.BulkPromote(dto, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private sealed class NullEmailService : IEmailService
    {
        public Task<EmailSendResult> SendAsync(string to, string subject, string html, CancellationToken ct = default)
            => Task.FromResult(new EmailSendResult(true, "test-null", null));

        public Task<EmailSendResult> SendFromTemplateAsync(EmailTemplate template, object data, CancellationToken ct = default)
            => Task.FromResult(new EmailSendResult(true, "test-null", null));
    }
}
