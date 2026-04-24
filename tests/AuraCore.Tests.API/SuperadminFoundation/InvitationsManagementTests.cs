using System.Security.Cryptography;
using AuraCore.API.Application.Interfaces;
using AuraCore.API.Application.Services.Email;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class InvitationsManagementTests : IClassFixture<WebApplicationFactory<Program>>
{
    // Shared in-memory root so _f.Services and _f.CreateClient() see the same DB
    // (per Task 30.1 discovery — EF InMemory provider cap).
    private static readonly InMemoryDatabaseRoot DbRoot = new();

    private readonly WebApplicationFactory<Program> _f;
    private readonly string _dbName;

    public InvitationsManagementTests(WebApplicationFactory<Program> f)
    {
        Environment.SetEnvironmentVariable("JWT_SECRET", "test-secret-at-least-32-characters-long!!");
        _dbName = $"invmgmt-{Guid.NewGuid()}";
        _f = f.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            var d = s.Single(x => x.ServiceType == typeof(DbContextOptions<AuraCoreDbContext>));
            s.Remove(d);
            s.AddDbContext<AuraCoreDbContext>(o => o.UseInMemoryDatabase(_dbName, DbRoot));

            // Override IEmailService with a null impl so Resend isn't hit during tests.
            var emailDesc = s.SingleOrDefault(x => x.ServiceType == typeof(IEmailService));
            if (emailDesc != null) s.Remove(emailDesc);
            s.AddScoped<IEmailService, NullEmailService>();
        }));
    }

    private async Task<(HttpClient c, Guid id)> SuperClient()
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var u = new User { Id = Guid.NewGuid(), Email = $"s-{Guid.NewGuid():N}@x.com", PasswordHash = "x", Role = "superadmin", TotpEnabled = true };
        db.Users.Add(u);
        await db.SaveChangesAsync();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var c = _f.CreateClient();
        c.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.GenerateAccessToken(u));
        return (c, u.Id);
    }

    private static string Sha256(string s) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(s))).ToLowerInvariant();

    [Fact]
    public async Task List_returns_pending_invitations()
    {
        var (c, superId) = await SuperClient();
        using (var scope = _f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
            var admin = new User { Id = Guid.NewGuid(), Email = "new@x.com", PasswordHash = "x", Role = "admin" };
            db.Users.Add(admin);
            db.AdminInvitations.Add(new AdminInvitation
            {
                TokenHash = Sha256("tok1"),
                AdminUserId = admin.Id,
                CreatedBy = superId,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            });
            await db.SaveChangesAsync();
        }

        var res = await c.GetAsync("/api/superadmin/invitations");
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("new@x.com", body);
    }

    [Fact]
    public async Task Revoke_deletes_invitation_row()
    {
        var (c, superId) = await SuperClient();
        var hash = Sha256($"revoke-me-{Guid.NewGuid():N}");
        using (var scope = _f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
            var admin = new User { Id = Guid.NewGuid(), Email = $"r-{Guid.NewGuid():N}@x.com", PasswordHash = "x", Role = "admin" };
            db.Users.Add(admin);
            db.AdminInvitations.Add(new AdminInvitation
            {
                TokenHash = hash,
                AdminUserId = admin.Id,
                CreatedBy = superId,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            });
            await db.SaveChangesAsync();
        }

        var res = await c.DeleteAsync($"/api/superadmin/invitations/{hash}");
        res.EnsureSuccessStatusCode();

        using var vs = _f.Services.CreateScope();
        var db2 = vs.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        Assert.Equal(0, await db2.AdminInvitations.CountAsync(i => i.TokenHash == hash));
    }

    [Fact]
    public async Task Resend_creates_new_token_invalidates_old_and_keeps_user_id()
    {
        var (c, superId) = await SuperClient();
        var oldHash = Sha256($"old-token-{Guid.NewGuid():N}");
        Guid adminId;
        using (var scope = _f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
            var admin = new User { Id = Guid.NewGuid(), Email = $"re-{Guid.NewGuid():N}@x.com", PasswordHash = "x", Role = "admin" };
            adminId = admin.Id;
            db.Users.Add(admin);
            db.AdminInvitations.Add(new AdminInvitation
            {
                TokenHash = oldHash,
                AdminUserId = admin.Id,
                CreatedBy = superId,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            });
            await db.SaveChangesAsync();
        }

        var res = await c.PostAsync($"/api/superadmin/invitations/{oldHash}/resend", null);
        res.EnsureSuccessStatusCode();

        using var vs = _f.Services.CreateScope();
        var db2 = vs.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        Assert.Equal(0, await db2.AdminInvitations.CountAsync(i => i.TokenHash == oldHash));
        var newInv = await db2.AdminInvitations.FirstOrDefaultAsync(i => i.AdminUserId == adminId);
        Assert.NotNull(newInv);
        Assert.NotEqual(oldHash, newInv!.TokenHash);
    }

    private sealed class NullEmailService : IEmailService
    {
        public Task<EmailSendResult> SendAsync(string to, string subject, string html, CancellationToken ct = default)
            => Task.FromResult(new EmailSendResult(true, "test-null", null));

        public Task<EmailSendResult> SendFromTemplateAsync(EmailTemplate template, object data, CancellationToken ct = default)
            => Task.FromResult(new EmailSendResult(true, "test-null", null));
    }
}
