using System.Net.Http.Json;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class SuperadminLoginEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SuperadminLoginEndpointTests(WebApplicationFactory<Program> factory)
    {
        Environment.SetEnvironmentVariable("JWT_SECRET", "test-secret-at-least-32-characters-long!!");
        // One InMemoryDatabaseRoot shared across all scopes so seeded data is
        // visible to the HTTP request's DbContext. Without this, EF's default
        // scoped-lifetime DbContextOptions would hand each scope a private store.
        var dbName = $"int-{Guid.NewGuid()}";
        var dbRoot = new InMemoryDatabaseRoot();
        _factory = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            var dbDesc = s.Single(d => d.ServiceType == typeof(DbContextOptions<AuraCoreDbContext>));
            s.Remove(dbDesc);
            s.AddDbContext<AuraCoreDbContext>(o => o.UseInMemoryDatabase(dbName, dbRoot));
        }));
    }

    private HttpClient Client() => _factory.CreateClient();

    private async Task SeedSuperadmin(string email, string password)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        db.Users.Add(new User {
            Id = Guid.NewGuid(),
            Email = email.ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = "superadmin",
            TotpEnabled = true,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Returns_401_for_nonexistent_email()
    {
        var c = Client();
        var res = await c.PostAsJsonAsync("/api/auth/superadmin/login",
            new { email = "none@x.com", password = "whatever12", totpCode = "123456" });
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Returns_401_when_user_is_not_superadmin_even_with_correct_password()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
            db.Users.Add(new User {
                Id = Guid.NewGuid(),
                Email = "plain@x.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("GoodPass12"),
                Role = "admin",
            });
            await db.SaveChangesAsync();
        }

        var c = Client();
        var res = await c.PostAsJsonAsync("/api/auth/superadmin/login",
            new { email = "plain@x.com", password = "GoodPass12" });
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Returns_ok_and_token_for_valid_superadmin_with_totp()
    {
        await SeedSuperadmin("boss@x.com", "GoodPass12");
        // Password correct + no TOTP code supplied → requires2fa: true (same contract as /login).
        var c = Client();
        var res = await c.PostAsJsonAsync("/api/auth/superadmin/login",
            new { email = "boss@x.com", password = "GoodPass12" });
        Assert.Equal(System.Net.HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("requires2fa", body);
    }

    [Fact]
    public async Task Returns_scope_limited_token_when_totp_not_enabled()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
            db.Users.Add(new User {
                Id = Guid.NewGuid(),
                Email = "fresh@x.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("GoodPass12"),
                Role = "superadmin",
                TotpEnabled = false,
            });
            await db.SaveChangesAsync();
        }

        var c = Client();
        var res = await c.PostAsJsonAsync("/api/auth/superadmin/login",
            new { email = "fresh@x.com", password = "GoodPass12" });
        Assert.Equal(System.Net.HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("requiresTwoFactorSetup", body);
        Assert.Contains("accessToken", body);
    }
}
