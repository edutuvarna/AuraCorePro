using System.Net.Http.Json;
using System.Security.Cryptography;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using AuraCore.Tests.API.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class AdminInvitationFlowTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _f;

    public AdminInvitationFlowTests(TestWebAppFactory f) => _f = f;

    private static string Sha256(string s)
        => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(s))).ToLowerInvariant();

    [Fact]
    public async Task Valid_token_redeems_password_and_returns_token()
    {
        var userId = Guid.NewGuid();
        var raw = "abcd1234";
        await _f.SeedAsync(db =>
        {
            var user = new User { Id = userId, Email = "new@x.com", PasswordHash = "temp", Role = "admin" };
            db.Users.Add(user);
            db.AdminInvitations.Add(new AdminInvitation {
                TokenHash = Sha256(raw), AdminUserId = user.Id, CreatedBy = user.Id,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            });
        });

        var c = _f.CreateClient();
        var res = await c.PostAsJsonAsync("/api/auth/redeem-invitation", new {
            token = raw, email = "new@x.com", newPassword = "NewSecurePass12!",
        });
        res.EnsureSuccessStatusCode();
        Assert.Contains("accessToken", await res.Content.ReadAsStringAsync());

        using var scope2 = _f.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var reloaded = await db2.Users.FirstAsync(u => u.Id == userId);
        Assert.True(BCrypt.Net.BCrypt.Verify("NewSecurePass12!", reloaded.PasswordHash));
        var inv = await db2.AdminInvitations.FirstAsync();
        Assert.NotNull(inv.ConsumedAt);
    }

    [Fact]
    public async Task Consumed_token_returns_410()
    {
        var raw = "consumed-token";
        await _f.SeedAsync(db =>
        {
            var user = new User { Id = Guid.NewGuid(), Email = "used@x.com", PasswordHash = "x", Role = "admin" };
            db.Users.Add(user);
            db.AdminInvitations.Add(new AdminInvitation {
                TokenHash = Sha256(raw), AdminUserId = user.Id, CreatedBy = user.Id,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(1), ConsumedAt = DateTimeOffset.UtcNow,
            });
        });

        var c = _f.CreateClient();
        var res = await c.PostAsJsonAsync("/api/auth/redeem-invitation", new {
            token = raw, email = "used@x.com", newPassword = "NewSecurePass12!",
        });
        Assert.Equal(System.Net.HttpStatusCode.Gone, res.StatusCode);
    }

    [Fact]
    public async Task Expired_token_returns_410()
    {
        var raw = "expired-token";
        await _f.SeedAsync(db =>
        {
            var user = new User { Id = Guid.NewGuid(), Email = "exp@x.com", PasswordHash = "x", Role = "admin" };
            db.Users.Add(user);
            db.AdminInvitations.Add(new AdminInvitation {
                TokenHash = Sha256(raw), AdminUserId = user.Id, CreatedBy = user.Id,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            });
        });

        var c = _f.CreateClient();
        var res = await c.PostAsJsonAsync("/api/auth/redeem-invitation", new {
            token = raw, email = "exp@x.com", newPassword = "NewSecurePass12!",
        });
        Assert.Equal(System.Net.HttpStatusCode.Gone, res.StatusCode);
    }
}
