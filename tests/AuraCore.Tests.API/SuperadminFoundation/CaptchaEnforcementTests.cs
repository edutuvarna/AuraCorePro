using System.Net;
using System.Net.Http.Json;
using AuraCore.API.Application.Services.Security;
using AuraCore.API.Domain.Entities;
using AuraCore.Tests.API.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

/// <summary>
/// Phase 6.12.W6.T10-T12 — verifies that every covered auth endpoint
/// rejects requests with missing or invalid Turnstile tokens (when not in
/// tolerant mode) and proceeds normally with valid tokens.
/// </summary>
public class CaptchaEnforcementTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _f;
    public CaptchaEnforcementTests(TestWebAppFactory f) => _f = f;

    private sealed class AlwaysDenyCaptchaVerifier : ICaptchaVerifier
    {
        public Task<bool> VerifyAsync(string token, string remoteIp, CancellationToken ct = default)
            => Task.FromResult(false);
    }

    private WebApplicationFactory<Program> WithDenyVerifier() =>
        _f.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            var d = s.SingleOrDefault(x => x.ServiceType == typeof(ICaptchaVerifier));
            if (d is not null) s.Remove(d);
            s.AddSingleton<ICaptchaVerifier, AlwaysDenyCaptchaVerifier>();
        }));

    [Fact]
    public async Task Login_returns_400_when_token_missing_in_strict_mode()
    {
        Environment.SetEnvironmentVariable("TURNSTILE_TOLERANT_MODE", "false");
        Environment.SetEnvironmentVariable("CAPTCHA_ENABLED", "true");
        var c = _f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email = "x@y.com", password = "wrong" });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("captcha_required", await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_returns_400_when_token_invalid()
    {
        Environment.SetEnvironmentVariable("TURNSTILE_TOLERANT_MODE", "false");
        Environment.SetEnvironmentVariable("CAPTCHA_ENABLED", "true");
        var deny = WithDenyVerifier();
        var c = deny.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login",
            new { email = "x@y.com", password = "wrong", turnstileToken = "anything" });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("captcha_invalid", await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SuperadminLogin_returns_400_when_token_missing()
    {
        Environment.SetEnvironmentVariable("TURNSTILE_TOLERANT_MODE", "false");
        Environment.SetEnvironmentVariable("CAPTCHA_ENABLED", "true");
        var c = _f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/superadmin/login",
            new { email = "x@y.com", password = "wrong", totpCode = "000000" });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Register_returns_400_when_token_missing()
    {
        Environment.SetEnvironmentVariable("TURNSTILE_TOLERANT_MODE", "false");
        Environment.SetEnvironmentVariable("CAPTCHA_ENABLED", "true");
        var c = _f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/register",
            new { email = "newuser@x.com", password = "GoodPassword12" });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Login_proceeds_with_valid_token()
    {
        Environment.SetEnvironmentVariable("TURNSTILE_TOLERANT_MODE", "false");
        Environment.SetEnvironmentVariable("CAPTCHA_ENABLED", "true");
        await _f.SeedAsync(db => db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "valid@x.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("GoodPass12"),
            Role = "admin",
            TotpEnabled = false,
            IsActive = true,
        }));
        var c = _f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login",
            new { email = "valid@x.com", password = "GoodPass12", turnstileToken = "any-token-stub-allows" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_returns_400_when_token_missing()
    {
        Environment.SetEnvironmentVariable("TURNSTILE_TOLERANT_MODE", "false");
        Environment.SetEnvironmentVariable("CAPTCHA_ENABLED", "true");
        var c = _f.CreateClient();
        // Note: actual route is /api/auth/password/forgot (per existing
        // [Route("api/auth/password")] + [HttpPost("forgot")]). The plan
        // T11 step 4 referenced a hypothetical /api/auth/forgot-password
        // route, but the live endpoint (used by the landing page) is the
        // path below — not changing the route avoids breaking consumers.
        var r = await c.PostAsJsonAsync("/api/auth/password/forgot", new { email = "x@y.com" });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("captcha_required", await r.Content.ReadAsStringAsync());
    }
}
