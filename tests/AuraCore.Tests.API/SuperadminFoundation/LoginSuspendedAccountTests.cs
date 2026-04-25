using System.Net;
using System.Net.Http.Json;
using AuraCore.API.Domain.Entities;
using AuraCore.Tests.API.Support;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

/// <summary>
/// Regression guard for the Phase 6.11 post-deploy bug where
/// <see cref="AuraCore.API.Controllers.AuthController.Login"/> accepted valid
/// credentials for users with <c>IsActive=false</c>. SuperadminLogin already
/// gated this at line ~315; the regular endpoint was missing the check, so an
/// admin suspended via the Admin Management tab could still authenticate.
/// </summary>
public class LoginSuspendedAccountTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public LoginSuspendedAccountTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Returns_401_account_suspended_when_IsActive_is_false()
    {
        await _factory.SeedAsync(db => db.Users.Add(new User
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
            new { email = "suspended@x.com", password = "GoodPass12", turnstileToken = "stub" });
        var body = await r.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        Assert.Contains("account_suspended", body);
    }

    [Fact]
    public async Task Active_account_with_same_credentials_still_logs_in()
    {
        await _factory.SeedAsync(db => db.Users.Add(new User
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
            new { email = "active@x.com", password = "GoodPass12", turnstileToken = "stub" });
        var body = await r.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.DoesNotContain("account_suspended", body);
        Assert.Contains("accessToken", body);
    }
}
