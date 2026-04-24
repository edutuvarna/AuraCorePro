using System.Net.Http.Json;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class TwoFactorEnforcementTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _f;

    public TwoFactorEnforcementTests(WebApplicationFactory<Program> f)
    {
        Environment.SetEnvironmentVariable("JWT_SECRET", "test-secret-at-least-32-characters-long!!");
        // Shared InMemoryDatabaseRoot so seeded data is visible across scopes
        // (the Seed helper's scope and the HTTP request's scope). Without it,
        // EF's scoped DbContextOptions hand each scope a private store.
        var dbName = $"2fa-{Guid.NewGuid()}";
        var dbRoot = new InMemoryDatabaseRoot();
        _f = f.WithWebHostBuilder(b => b.ConfigureServices(s => {
            var d = s.Single(x => x.ServiceType == typeof(DbContextOptions<AuraCoreDbContext>));
            s.Remove(d);
            s.AddDbContext<AuraCoreDbContext>(o => o.UseInMemoryDatabase(dbName, dbRoot));
        }));
    }

    private async Task Seed(Action<AuraCoreDbContext> act)
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        act(db);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Admin_without_require_2fa_and_global_off_does_not_require_setup()
    {
        await Seed(db => {
            db.Users.Add(new User {
                Id = Guid.NewGuid(), Email = "a@x.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("GoodPass12"),
                Role = "admin", TotpEnabled = false, Require2fa = false,
            });
        });
        var c = _f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email = "a@x.com", password = "GoodPass12" });
        var body = await r.Content.ReadAsStringAsync();
        Assert.DoesNotContain("requiresTwoFactorSetup", body);
    }

    [Fact]
    public async Task Admin_with_per_account_require_2fa_returns_setup_token()
    {
        await Seed(db => {
            db.Users.Add(new User {
                Id = Guid.NewGuid(), Email = "b@x.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("GoodPass12"),
                Role = "admin", TotpEnabled = false, Require2fa = true,
            });
        });
        var c = _f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email = "b@x.com", password = "GoodPass12" });
        var body = await r.Content.ReadAsStringAsync();
        Assert.Contains("requiresTwoFactorSetup", body);
    }

    [Fact]
    public async Task Admin_when_global_2fa_on_returns_setup_token()
    {
        await Seed(db => {
            db.Users.Add(new User {
                Id = Guid.NewGuid(), Email = "c@x.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("GoodPass12"),
                Role = "admin", TotpEnabled = false, Require2fa = false,
            });
            db.SystemSettings.Add(new SystemSetting { Key = "require_2fa_for_all_admins", Value = "true" });
        });
        var c = _f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email = "c@x.com", password = "GoodPass12" });
        var body = await r.Content.ReadAsStringAsync();
        Assert.Contains("requiresTwoFactorSetup", body);
    }
}
