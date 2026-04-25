using System.Net.Http.Json;
using AuraCore.API.Domain.Entities;
using AuraCore.Tests.API.Support;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class TwoFactorEnforcementTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _f;

    public TwoFactorEnforcementTests(TestWebAppFactory f) => _f = f;

    [Fact]
    public async Task Admin_without_require_2fa_and_global_off_does_not_require_setup()
    {
        await _f.SeedAsync(db => {
            db.Users.Add(new User {
                Id = Guid.NewGuid(), Email = "a@x.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("GoodPass12"),
                Role = "admin", TotpEnabled = false, Require2fa = false,
            });
        });
        var c = _f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email = "a@x.com", password = "GoodPass12", turnstileToken = "stub" });
        var body = await r.Content.ReadAsStringAsync();
        Assert.DoesNotContain("requiresTwoFactorSetup", body);
    }

    [Fact]
    public async Task Admin_with_per_account_require_2fa_returns_setup_token()
    {
        await _f.SeedAsync(db => {
            db.Users.Add(new User {
                Id = Guid.NewGuid(), Email = "b@x.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("GoodPass12"),
                Role = "admin", TotpEnabled = false, Require2fa = true,
            });
        });
        var c = _f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email = "b@x.com", password = "GoodPass12", turnstileToken = "stub" });
        var body = await r.Content.ReadAsStringAsync();
        Assert.Contains("requiresTwoFactorSetup", body);
    }

    [Fact]
    public async Task Admin_when_global_2fa_on_returns_setup_token()
    {
        await _f.SeedAsync(db => {
            db.Users.Add(new User {
                Id = Guid.NewGuid(), Email = "c@x.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("GoodPass12"),
                Role = "admin", TotpEnabled = false, Require2fa = false,
            });
            db.SystemSettings.Add(new SystemSetting { Key = "require_2fa_for_all_admins", Value = "true" });
        });
        var c = _f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email = "c@x.com", password = "GoodPass12", turnstileToken = "stub" });
        var body = await r.Content.ReadAsStringAsync();
        Assert.Contains("requiresTwoFactorSetup", body);
    }
}
