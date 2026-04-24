using System.Net.Http.Json;
using System.Security.Cryptography;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class AdminInvitationFlowTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _f;

    public AdminInvitationFlowTests(WebApplicationFactory<Program> f)
    {
        Environment.SetEnvironmentVariable("JWT_SECRET", "test-secret-at-least-32-characters-long!!");
        // Shared InMemoryDatabaseRoot so seeded data is visible across scopes
        // (the HTTP request's DbContext scope would otherwise see an empty store).
        var dbName = $"inv-{Guid.NewGuid()}";
        var dbRoot = new InMemoryDatabaseRoot();
        _f = f.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            var d = s.Single(x => x.ServiceType == typeof(DbContextOptions<AuraCoreDbContext>));
            s.Remove(d);
            s.AddDbContext<AuraCoreDbContext>(o => o.UseInMemoryDatabase(dbName, dbRoot));
        }));
    }

    private static string Sha256(string s)
        => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(s))).ToLowerInvariant();

    [Fact]
    public async Task Valid_token_redeems_password_and_returns_token()
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var user = new User { Id = Guid.NewGuid(), Email = "new@x.com", PasswordHash = "temp", Role = "admin" };
        db.Users.Add(user);
        var raw = "abcd1234";
        db.AdminInvitations.Add(new AdminInvitation {
            TokenHash = Sha256(raw), AdminUserId = user.Id, CreatedBy = user.Id,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
        });
        await db.SaveChangesAsync();

        var c = _f.CreateClient();
        var res = await c.PostAsJsonAsync("/api/auth/redeem-invitation", new {
            token = raw, email = "new@x.com", newPassword = "NewSecurePass12!",
        });
        res.EnsureSuccessStatusCode();
        Assert.Contains("accessToken", await res.Content.ReadAsStringAsync());

        using var scope2 = _f.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var reloaded = await db2.Users.FirstAsync(u => u.Id == user.Id);
        Assert.True(BCrypt.Net.BCrypt.Verify("NewSecurePass12!", reloaded.PasswordHash));
        var inv = await db2.AdminInvitations.FirstAsync();
        Assert.NotNull(inv.ConsumedAt);
    }

    [Fact]
    public async Task Consumed_token_returns_410()
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var user = new User { Id = Guid.NewGuid(), Email = "used@x.com", PasswordHash = "x", Role = "admin" };
        db.Users.Add(user);
        var raw = "consumed-token";
        db.AdminInvitations.Add(new AdminInvitation {
            TokenHash = Sha256(raw), AdminUserId = user.Id, CreatedBy = user.Id,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1), ConsumedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var c = _f.CreateClient();
        var res = await c.PostAsJsonAsync("/api/auth/redeem-invitation", new {
            token = raw, email = "used@x.com", newPassword = "NewSecurePass12!",
        });
        Assert.Equal(System.Net.HttpStatusCode.Gone, res.StatusCode);
    }

    [Fact]
    public async Task Expired_token_returns_410()
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var user = new User { Id = Guid.NewGuid(), Email = "exp@x.com", PasswordHash = "x", Role = "admin" };
        db.Users.Add(user);
        var raw = "expired-token";
        db.AdminInvitations.Add(new AdminInvitation {
            TokenHash = Sha256(raw), AdminUserId = user.Id, CreatedBy = user.Id,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        });
        await db.SaveChangesAsync();

        var c = _f.CreateClient();
        var res = await c.PostAsJsonAsync("/api/auth/redeem-invitation", new {
            token = raw, email = "exp@x.com", newPassword = "NewSecurePass12!",
        });
        Assert.Equal(System.Net.HttpStatusCode.Gone, res.StatusCode);
    }
}
