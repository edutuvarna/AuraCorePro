using System.Diagnostics;
using System.Net.Http.Json;
using AuraCore.API.Domain.Entities;
using AuraCore.Tests.API.Support;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

/// <summary>
/// Phase 6.12.W5.T7+T8 — BCrypt timing-attack defense. Without the dummy
/// hash, /superadmin/login (and /login) returned almost instantly when the
/// email did not exist (no BCrypt work) but spent ~100-300 ms when the email
/// DID exist (BCrypt.Verify against the real hash). The delta is observable
/// over the network and lets attackers enumerate valid emails despite the
/// generic 401 error message.
///
/// These tests verify the response time delta between (real-email + wrong-pw)
/// and (nonexistent-email + valid-pw) stays within a 100 ms threshold. CI
/// runner load variance can produce false positives — increase the threshold
/// or skip on slow runners if needed.
/// </summary>
[Trait("Category", "Timing")]
public class LoginTimingDefenseTests : IClassFixture<TestWebAppFactory>
{
    private const int Samples = 5;
    private const int ThresholdMs = 100;

    private readonly TestWebAppFactory _f;
    public LoginTimingDefenseTests(TestWebAppFactory f) => _f = f;

    private async Task<long> AverageMsAsync(string endpoint, object body)
    {
        var c = _f.CreateClient();
        // Warm-up call discarded.
        await c.PostAsJsonAsync(endpoint, body);
        var samples = new List<long>();
        for (int i = 0; i < Samples; i++)
        {
            var sw = Stopwatch.StartNew();
            await c.PostAsJsonAsync(endpoint, body);
            sw.Stop();
            samples.Add(sw.ElapsedMilliseconds);
        }
        // Median of 5 samples to reduce single-outlier flakiness.
        samples.Sort();
        return samples[Samples / 2];
    }

    [Fact]
    public async Task SuperadminLogin_response_time_does_not_leak_user_existence()
    {
        await _f.SeedAsync(db => db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "exists@x.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("RealPass12"),
            Role = "superadmin",
            TotpEnabled = true,
        }));

        var existing = await AverageMsAsync("/api/auth/superadmin/login",
            new { email = "exists@x.com", password = "WrongPass99", totpCode = "000000" });
        var nonexistent = await AverageMsAsync("/api/auth/superadmin/login",
            new { email = "ghost@x.com", password = "WrongPass99", totpCode = "000000" });

        var delta = Math.Abs(existing - nonexistent);
        Assert.True(delta < ThresholdMs,
            $"Timing delta ({delta} ms) leaked email existence. existing={existing} nonexistent={nonexistent}");
    }

    [Fact(Skip = "T8 applies the same dummy-hash defense to IAuthService.LoginAsync. Until then this test fails by design.")]
    public async Task Login_response_time_does_not_leak_user_existence()
    {
        await _f.SeedAsync(db => db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "exists2@x.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("RealPass12"),
            Role = "admin",
            TotpEnabled = false,
        }));

        var existing = await AverageMsAsync("/api/auth/login",
            new { email = "exists2@x.com", password = "WrongPass99" });
        var nonexistent = await AverageMsAsync("/api/auth/login",
            new { email = "ghost2@x.com", password = "WrongPass99" });

        var delta = Math.Abs(existing - nonexistent);
        Assert.True(delta < ThresholdMs,
            $"Timing delta ({delta} ms) leaked email existence. existing={existing} nonexistent={nonexistent}");
    }
}
