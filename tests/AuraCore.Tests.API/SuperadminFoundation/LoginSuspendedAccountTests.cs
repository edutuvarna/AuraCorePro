using System.Net;
using System.Net.Http.Json;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

/// <summary>
/// Regression guard for the Phase 6.11 post-deploy bug where
/// <see cref="AuraCore.API.Controllers.AuthController.Login"/> accepted valid
/// credentials for users with <c>IsActive=false</c>. SuperadminLogin already
/// gated this at line ~315; the regular endpoint was missing the check, so an
/// admin suspended via the Admin Management tab could still authenticate.
/// </summary>
public class LoginSuspendedAccountTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public LoginSuspendedAccountTests(WebApplicationFactory<Program> factory)
    {
        Environment.SetEnvironmentVariable("JWT_SECRET", "test-secret-at-least-32-characters-long!!");
        var dbName = $"susp-{Guid.NewGuid()}";
        var dbRoot = new InMemoryDatabaseRoot();
        _factory = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            var dbDesc = s.Single(d => d.ServiceType == typeof(DbContextOptions<AuraCoreDbContext>));
            s.Remove(dbDesc);
            s.AddDbContext<AuraCoreDbContext>(o => o
                .UseInMemoryDatabase(dbName, dbRoot)
                // Suppress ManyServiceProvidersCreatedWarning — each xUnit test class
                // that uses IClassFixture<WebApplicationFactory<Program>> + a per-ctor
                // WithWebHostBuilder contributes one more IServiceProvider to EF's
                // cache, and the suite now has enough such classes to trip the >20
                // threshold. Irrelevant to prod behavior.
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));
        }));
    }

    private async Task Seed(Action<AuraCoreDbContext> act)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        act(db);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Returns_401_account_suspended_when_IsActive_is_false()
    {
        await Seed(db => db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "suspended@x.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("GoodPass12"),
            Role = "admin",
            TotpEnabled = false,
            IsActive = false,
        }));

        var c = _factory.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login",
            new { email = "suspended@x.com", password = "GoodPass12" });
        var body = await r.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        Assert.Contains("account_suspended", body);
    }

    [Fact]
    public async Task Active_account_with_same_credentials_still_logs_in()
    {
        await Seed(db => db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "active@x.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("GoodPass12"),
            Role = "admin",
            TotpEnabled = false,
            IsActive = true,
        }));

        var c = _factory.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login",
            new { email = "active@x.com", password = "GoodPass12" });
        var body = await r.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.DoesNotContain("account_suspended", body);
        Assert.Contains("accessToken", body);
    }
}
