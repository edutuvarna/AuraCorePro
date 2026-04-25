using System.Net.Http.Json;
using AuraCore.API.Domain.Entities;
using AuraCore.Tests.API.Support;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class SuperadminLoginEndpointTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public SuperadminLoginEndpointTests(TestWebAppFactory factory) => _factory = factory;

    private HttpClient Client() => _factory.CreateClient();

    private async Task SeedSuperadmin(string email, string password)
    {
        await _factory.SeedAsync(db => db.Users.Add(new User {
            Id = Guid.NewGuid(),
            Email = email.ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = "superadmin",
            TotpEnabled = true,
        }));
    }

    [Fact]
    public async Task Returns_401_for_nonexistent_email()
    {
        var c = Client();
        var res = await c.PostAsJsonAsync("/api/auth/superadmin/login",
            new { email = "none@x.com", password = "whatever12", totpCode = "123456", turnstileToken = "stub" });
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Returns_401_when_user_is_not_superadmin_even_with_correct_password()
    {
        await _factory.SeedAsync(db => db.Users.Add(new User {
            Id = Guid.NewGuid(),
            Email = "plain@x.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("GoodPass12"),
            Role = "admin",
        }));

        var c = Client();
        var res = await c.PostAsJsonAsync("/api/auth/superadmin/login",
            new { email = "plain@x.com", password = "GoodPass12", turnstileToken = "stub" });
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Returns_ok_and_token_for_valid_superadmin_with_totp()
    {
        await SeedSuperadmin("boss@x.com", "GoodPass12");
        // Password correct + no TOTP code supplied → requires2fa: true (same contract as /login).
        var c = Client();
        var res = await c.PostAsJsonAsync("/api/auth/superadmin/login",
            new { email = "boss@x.com", password = "GoodPass12", turnstileToken = "stub" });
        Assert.Equal(System.Net.HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("requires2fa", body);
    }

    [Fact]
    public async Task Returns_scope_limited_token_when_totp_not_enabled()
    {
        await _factory.SeedAsync(db => db.Users.Add(new User {
            Id = Guid.NewGuid(),
            Email = "fresh@x.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("GoodPass12"),
            Role = "superadmin",
            TotpEnabled = false,
        }));

        var c = Client();
        var res = await c.PostAsJsonAsync("/api/auth/superadmin/login",
            new { email = "fresh@x.com", password = "GoodPass12", turnstileToken = "stub" });
        Assert.Equal(System.Net.HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("requiresTwoFactorSetup", body);
        Assert.Contains("accessToken", body);
    }
}
