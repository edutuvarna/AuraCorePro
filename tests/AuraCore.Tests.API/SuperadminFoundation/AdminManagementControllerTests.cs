using System.Net;
using System.Net.Http.Json;
using AuraCore.API.Application.Interfaces;
using AuraCore.API.Application.Services.Email;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Helpers;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class AdminManagementControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _f;

    public AdminManagementControllerTests(WebApplicationFactory<Program> f)
    {
        Environment.SetEnvironmentVariable("JWT_SECRET", "test-secret-at-least-32-characters-long!!");
        // Shared InMemoryDatabaseRoot so seeded data is visible across scopes.
        var dbName = $"amc-{Guid.NewGuid()}";
        var dbRoot = new InMemoryDatabaseRoot();
        _f = f.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            var d = s.Single(x => x.ServiceType == typeof(DbContextOptions<AuraCoreDbContext>));
            s.Remove(d);
            s.AddDbContext<AuraCoreDbContext>(o => o.UseInMemoryDatabase(dbName, dbRoot));

            // Override IEmailService with a no-op so controller code doesn't hit
            // the real Resend HTTPS API.
            var emailDesc = s.SingleOrDefault(x => x.ServiceType == typeof(IEmailService));
            if (emailDesc != null) s.Remove(emailDesc);
            s.AddScoped<IEmailService, NullEmailService>();
        }));
    }

    private async Task<(HttpClient c, Guid superId)> SuperClient()
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var su = new User { Id = Guid.NewGuid(), Email = $"super-{Guid.NewGuid():N}@x.com", PasswordHash = "x", Role = "superadmin", TotpEnabled = true };
        db.Users.Add(su);
        await db.SaveChangesAsync();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var token = auth.GenerateAccessToken(su);
        var c = _f.CreateClient();
        c.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return (c, su.Id);
    }

    [Fact]
    public async Task Create_admin_with_Trusted_template_emits_6_grants()
    {
        var (c, _) = await SuperClient();
        var res = await c.PostAsJsonAsync("/api/superadmin/admins", new {
            email = "new-admin@x.com",
            sendInvitation = false,
            initialPassword = "Abcdefghij12",
            forcePasswordChange = "on_first_login",
            template = "Trusted",
            require2fa = true,
        });
        res.EnsureSuccessStatusCode();

        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var user = await db.Users.FirstAsync(u => u.Email == "new-admin@x.com");
        Assert.Equal("admin", user.Role);
        Assert.True(user.ForcePasswordChange);
        Assert.True(user.Require2fa);
        var grants = await db.PermissionGrants.Where(g => g.AdminUserId == user.Id).ToListAsync();
        Assert.Equal(PermissionKeys.AllTier2.Count, grants.Count);
    }

    [Fact]
    public async Task Create_admin_without_invitation_or_password_fails_validation()
    {
        var (c, _) = await SuperClient();
        var res = await c.PostAsJsonAsync("/api/superadmin/admins", new {
            email = "foo@x.com",
            sendInvitation = false,
            template = "Default",
            forcePasswordChange = "never",
            require2fa = false,
            // initialPassword missing + sendInvitation=false → 400.
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Suspend_admin_sets_is_active_false_and_revokes_tokens()
    {
        var (c, _) = await SuperClient();
        Guid targetId;
        using (var scope = _f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
            var target = new User { Id = Guid.NewGuid(), Email = "target@x.com", PasswordHash = "x", Role = "admin", IsActive = true };
            db.Users.Add(target);
            db.RefreshTokens.Add(new RefreshToken { Id = Guid.NewGuid(), UserId = target.Id, Token = "t1", ExpiresAt = DateTimeOffset.UtcNow.AddDays(5) });
            await db.SaveChangesAsync();
            targetId = target.Id;
        }

        var res = await c.PostAsync($"/api/superadmin/admins/{targetId}/suspend", null);
        res.EnsureSuccessStatusCode();

        using var scope2 = _f.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var reloaded = await db2.Users.FirstAsync(u => u.Id == targetId);
        Assert.False(reloaded.IsActive);
        var refreshes = await db2.RefreshTokens.Where(r => r.UserId == targetId).ToListAsync();
        Assert.All(refreshes, r => Assert.True(r.IsRevoked));
    }

    [Fact]
    public async Task Promote_existing_user_changes_role_to_admin_and_applies_template()
    {
        var (c, _) = await SuperClient();
        Guid userId;
        using (var scope = _f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
            var user = new User { Id = Guid.NewGuid(), Email = "u@x.com", PasswordHash = "x", Role = "user" };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            userId = user.Id;
        }

        var res = await c.PostAsJsonAsync($"/api/superadmin/users/{userId}/promote", new {
            template = "Trusted",
            forcePasswordChange = "never",
            require2fa = false,
        });
        res.EnsureSuccessStatusCode();

        using var scope2 = _f.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var reloaded = await db2.Users.FirstAsync(u => u.Id == userId);
        Assert.Equal("admin", reloaded.Role);
        Assert.Equal(PermissionKeys.AllTier2.Count, await db2.PermissionGrants.CountAsync(g => g.AdminUserId == userId));
    }

    [Fact]
    public async Task Delete_admin_cascades_permission_grants_and_requests()
    {
        var (c, _) = await SuperClient();
        Guid targetId;
        using (var scope = _f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
            var target = new User { Id = Guid.NewGuid(), Email = "t@x.com", PasswordHash = "x", Role = "admin" };
            db.Users.Add(target);
            db.PermissionGrants.Add(new PermissionGrant { AdminUserId = target.Id, PermissionKey = "tab:updates", GrantedBy = target.Id });
            db.PermissionRequests.Add(new PermissionRequest { AdminUserId = target.Id, PermissionKey = "tab:updates", Reason = "test reason long enough to pass 50 char minimum length" });
            await db.SaveChangesAsync();
            targetId = target.Id;
        }

        var res = await c.DeleteAsync($"/api/superadmin/admins/{targetId}");
        res.EnsureSuccessStatusCode();

        using var scope2 = _f.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        Assert.Equal(0, await db2.Users.CountAsync(u => u.Id == targetId));
        Assert.Equal(0, await db2.PermissionGrants.CountAsync(g => g.AdminUserId == targetId));
        Assert.Equal(0, await db2.PermissionRequests.CountAsync(r => r.AdminUserId == targetId));
    }

    private sealed class NullEmailService : IEmailService
    {
        public Task<EmailSendResult> SendAsync(string to, string subject, string html, CancellationToken ct = default)
            => Task.FromResult(new EmailSendResult(true, "test-null", null));

        public Task<EmailSendResult> SendFromTemplateAsync(EmailTemplate template, object data, CancellationToken ct = default)
            => Task.FromResult(new EmailSendResult(true, "test-null", null));
    }
}
