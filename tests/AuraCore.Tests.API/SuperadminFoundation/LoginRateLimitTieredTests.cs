using System.Net;
using System.Net.Http.Json;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using AuraCore.Tests.API.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

/// <summary>
/// Phase 6.12.W4.T6 — tiered rate limit on /api/auth/superadmin/login:
///   layer 1 — 3 fails/email/60min (existing)
///   layer 2 — 10 fails/IP/60min (new — defends against email rotation)
///   layer 3 — 30 fails/IP/24h (new — defends against slow-drip distributed attack)
/// All three gated behind whitelisted-IP bypass.
///
/// Each test instantiates its own TestWebAppFactory so InMemory DB rows do not
/// leak across cases (the IP-cap layers count rows by IpAddress regardless of
/// email, so a shared-fixture seed from one test would skew the next).
/// The IP value used in seeds is the inbound request IP that TestServer
/// reports — discovered at runtime via a probe attempt that records a
/// LoginAttempt row, since TestServer's RemoteIpAddress behavior differs
/// across runtime versions.
/// </summary>
public class LoginRateLimitTieredTests
{
    private static LoginAttempt Fail(string email, string ip, DateTimeOffset when)
        => new() { Email = email, IpAddress = ip, Success = false, CreatedAt = when };

    private static async Task<string> ProbeRequestIpAsync(TestWebAppFactory f)
    {
        // One probe attempt with a unique email creates a LoginAttempt row whose
        // IpAddress column is exactly what GetClientIp() returned for the test
        // request — that is the value we must use when seeding rows the rate
        // limit will count against.
        var probeEmail = $"probe-{Guid.NewGuid():N}@x.com";
        var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/auth/superadmin/login",
            new { email = probeEmail, password = "wrong-pass-12", totpCode = "000000" });

        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var row = await db.LoginAttempts.FirstAsync(a => a.Email == probeEmail);
        // Clean up the probe row so it does not count against later layer-2/3 caps.
        db.LoginAttempts.Remove(row);
        await db.SaveChangesAsync();
        return row.IpAddress;
    }

    private static async Task<HttpStatusCode> AttemptAsync(TestWebAppFactory f, string email)
    {
        var c = f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/superadmin/login",
            new { email, password = "wrong-pass-12", totpCode = "000000" });
        return r.StatusCode;
    }

    [Fact]
    public async Task Returns_429_after_3_email_fails_in_60min()
    {
        using var f = new TestWebAppFactory();
        var ip = await ProbeRequestIpAsync(f);
        await f.SeedAsync(db =>
        {
            for (int i = 0; i < 3; i++)
                db.LoginAttempts.Add(Fail("victim@x.com", ip, DateTimeOffset.UtcNow.AddMinutes(-5)));
        });
        var status = await AttemptAsync(f, "victim@x.com");
        Assert.Equal(HttpStatusCode.TooManyRequests, status);
    }

    [Fact]
    public async Task Returns_429_after_10_IP_fails_in_60min()
    {
        using var f = new TestWebAppFactory();
        var ip = await ProbeRequestIpAsync(f);
        await f.SeedAsync(db =>
        {
            for (int i = 0; i < 10; i++)
                db.LoginAttempts.Add(Fail($"u{i}@x.com", ip, DateTimeOffset.UtcNow.AddMinutes(-5)));
        });
        // 11th attempt from a brand-new email but same IP must still be blocked.
        var status = await AttemptAsync(f, "never-tried-before@x.com");
        Assert.Equal(HttpStatusCode.TooManyRequests, status);
    }

    [Fact]
    public async Task Returns_429_after_30_IP_fails_in_24h_but_under_60min_threshold()
    {
        using var f = new TestWebAppFactory();
        var ip = await ProbeRequestIpAsync(f);
        await f.SeedAsync(db =>
        {
            // 30 fails spread across the last 24 hours, all OUTSIDE the 60-min
            // window — so layer 2 (60-min IP cap) sees zero, but layer 3
            // (24-h IP cap) sees 30 and trips.
            for (int i = 0; i < 30; i++)
                db.LoginAttempts.Add(Fail($"slow{i}@x.com", ip,
                    DateTimeOffset.UtcNow.AddMinutes(-65).AddMinutes(-i * 20)));
        });
        var status = await AttemptAsync(f, "yet-another@x.com");
        Assert.Equal(HttpStatusCode.TooManyRequests, status);
    }

    [Fact]
    public async Task Whitelisted_IP_bypasses_all_three_caps()
    {
        using var f = new TestWebAppFactory();
        var ip = await ProbeRequestIpAsync(f);
        await f.SeedAsync(db =>
        {
            db.IpWhitelists.Add(new IpWhitelist
            {
                Id = Guid.NewGuid(),
                IpAddress = ip,
                Label = "test-bypass",
                CreatedAt = DateTimeOffset.UtcNow,
            });
            for (int i = 0; i < 50; i++)
                db.LoginAttempts.Add(Fail($"u{i}@x.com", ip, DateTimeOffset.UtcNow.AddMinutes(-5)));
        });
        // Whitelisted IP should reach the auth flow (returning 401 because the
        // user does not exist). The key assertion is "not 429".
        var c = f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/superadmin/login",
            new { email = "ghost@x.com", password = "wrong", totpCode = "000000" });
        Assert.NotEqual(HttpStatusCode.TooManyRequests, r.StatusCode);
    }
}
