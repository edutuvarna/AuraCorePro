# Phase 6.12 — Security Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close four open security gaps from Phase 6.11 verification (IEmailService XSS, superadmin login tiered rate limit, BCrypt timing-attack defense, Cloudflare Turnstile CAPTCHA on four auth endpoints) with reversible three-phase deploy and +15-17 regression tests.

**Architecture:** No new DB tables, no migrations. Backend gains a Polly-wrapped `ICaptchaVerifier` service and three small auth-controller hardening passes. Four FE forms (one React + three vanilla HTML) gain Turnstile widgets. Tolerant-mode env flag enables backwards-compatible rollout.

**Tech Stack:** .NET 8 / EF Core 8 / xUnit / Microsoft.Extensions.Http.Resilience (Polly) / BCrypt.Net-Next / Cloudflare Turnstile / Next.js 14 / `@marsidev/react-turnstile` / vanilla CF api.js.

**Spec:** `docs/superpowers/specs/2026-04-24-phase-6-12-security-hardening-design.md`

**Branch setup (run before T1):**
```bash
cd /c/Users/Admin/Desktop/auracorepro/AuraCorePro
git checkout main
git pull origin main
git checkout -b phase-6-12-security-hardening
```

**One-time CF dashboard prerequisite (user, before T9):**
1. `dash.cloudflare.com` → Turnstile → Add site, name `AuraCorePro Auth`.
2. Domains: `auracore.pro` and `admin.auracore.pro` (or wildcard `*.auracore.pro`).
3. Widget mode: Managed.
4. Save → record **Site Key** + **Secret Key**. Site key goes in admin-panel `.env.production` and the three landing-page HTML files; secret key goes in `/etc/auracore-api.env` on origin and as `appsettings.Test.json` blank for tests.

---

## Wave 1 — Test infrastructure

### Task 1: TestWebAppFactory base + migrate existing fixtures

**Files:**
- Create: `tests/AuraCore.Tests.API/Support/TestWebAppFactory.cs`
- Create: `tests/AuraCore.Tests.API/Support/AlwaysAllowCaptchaVerifier.cs`
- Modify: `tests/AuraCore.Tests.API/SuperadminFoundation/SuperadminLoginEndpointTests.cs`
- Modify: `tests/AuraCore.Tests.API/SuperadminFoundation/TwoFactorEnforcementTests.cs`
- Modify: `tests/AuraCore.Tests.API/SuperadminFoundation/LoginSuspendedAccountTests.cs`
- Modify: `tests/AuraCore.Tests.API/SuperadminFoundation/AdminInvitationFlowTests.cs`

- [ ] **Step 1: Create the captcha test stub**

`tests/AuraCore.Tests.API/Support/AlwaysAllowCaptchaVerifier.cs`:

```csharp
using AuraCore.API.Application.Services.Security;

namespace AuraCore.Tests.API.Support;

/// <summary>
/// Test substitute for ICaptchaVerifier — always returns true so existing
/// auth-flow integration tests don't need to construct real Turnstile tokens.
/// Phase 6.12 introduced ICaptchaVerifier as a hard requirement on four auth
/// endpoints; this stub keeps pre-6.12 fixtures green.
/// </summary>
public sealed class AlwaysAllowCaptchaVerifier : ICaptchaVerifier
{
    public Task<bool> VerifyAsync(string token, string remoteIp, CancellationToken ct = default)
        => Task.FromResult(true);
}
```

- [ ] **Step 2: Create the test web-app factory base**

`tests/AuraCore.Tests.API/Support/TestWebAppFactory.cs`:

```csharp
using AuraCore.API.Application.Services.Security;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.Tests.API.Support;

/// <summary>
/// Shared WebApplicationFactory configuration for all auth-touching tests.
/// Replaces the production DbContext with InMemory + a shared root, suppresses
/// the EF service-provider warning that fires when many fixtures coexist, and
/// substitutes ICaptchaVerifier with the always-allow stub.
/// </summary>
public sealed class TestWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName;
    private readonly InMemoryDatabaseRoot _dbRoot;

    public TestWebAppFactory()
    {
        Environment.SetEnvironmentVariable("JWT_SECRET", "test-secret-at-least-32-characters-long!!");
        _dbName = $"test-{Guid.NewGuid()}";
        _dbRoot = new InMemoryDatabaseRoot();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var dbDesc = services.Single(d => d.ServiceType == typeof(DbContextOptions<AuraCoreDbContext>));
            services.Remove(dbDesc);
            services.AddDbContext<AuraCoreDbContext>(o => o
                .UseInMemoryDatabase(_dbName, _dbRoot)
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));

            // Phase 6.12 — substitute Turnstile verifier with always-allow.
            var captchaDesc = services.SingleOrDefault(d => d.ServiceType == typeof(ICaptchaVerifier));
            if (captchaDesc is not null) services.Remove(captchaDesc);
            services.AddSingleton<ICaptchaVerifier, AlwaysAllowCaptchaVerifier>();
        });
    }

    public async Task SeedAsync(Action<AuraCoreDbContext> act)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        act(db);
        await db.SaveChangesAsync();
    }
}
```

- [ ] **Step 3: Verify the new files compile**

Note: `ICaptchaVerifier` doesn't exist yet (Task 2 creates it). For now the project will fail to compile — that's expected. Comment out the `ICaptchaVerifier` import + the `services.Remove`/`AddSingleton` block temporarily by wrapping them in a `#if false` block, then uncomment after Task 2.

Replace the captcha block with:

```csharp
            // Phase 6.12 — captcha substitution wired in Task 2 once
            // ICaptchaVerifier is created. Until then, this is dead.
            #if PHASE_6_12_CAPTCHA_READY
            var captchaDesc = services.SingleOrDefault(d => d.ServiceType == typeof(ICaptchaVerifier));
            if (captchaDesc is not null) services.Remove(captchaDesc);
            services.AddSingleton<ICaptchaVerifier, AlwaysAllowCaptchaVerifier>();
            #endif
```

Same for the `using AuraCore.API.Application.Services.Security;` line — remove temporarily, restore in Task 2.

```bash
cd /c/Users/Admin/Desktop/auracorepro/AuraCorePro
dotnet build tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --no-restore 2>&1 | tail -5
```

Expected: build succeeds.

- [ ] **Step 4: Migrate `SuperadminLoginEndpointTests.cs` to derive from base**

Open the file. Replace:

```csharp
public class SuperadminLoginEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SuperadminLoginEndpointTests(WebApplicationFactory<Program> factory)
    {
        Environment.SetEnvironmentVariable("JWT_SECRET", "test-secret-at-least-32-characters-long!!");
        var dbName = $"int-{Guid.NewGuid()}";
        var dbRoot = new InMemoryDatabaseRoot();
        _factory = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            var dbDesc = s.Single(d => d.ServiceType == typeof(DbContextOptions<AuraCoreDbContext>));
            s.Remove(dbDesc);
            s.AddDbContext<AuraCoreDbContext>(o => o.UseInMemoryDatabase(dbName, dbRoot));
        }));
    }
```

with:

```csharp
public class SuperadminLoginEndpointTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public SuperadminLoginEndpointTests(TestWebAppFactory factory) => _factory = factory;
```

Add `using AuraCore.Tests.API.Support;` to the using block. Remove unused `using Microsoft.EntityFrameworkCore.Storage;`, `using Microsoft.EntityFrameworkCore;` if only the InMemory references are gone.

Inside any private `Seed*` helper, replace the local-scope CreateScope/SaveChanges block with `await _factory.SeedAsync(db => { /* same body */ });`.

- [ ] **Step 5: Migrate `TwoFactorEnforcementTests.cs`**

Same pattern. Replace ctor block + helper. The class previously also added `.ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))` — that's now centralized in the base, so remove it.

- [ ] **Step 6: Migrate `LoginSuspendedAccountTests.cs`**

Same pattern.

- [ ] **Step 7: Migrate `AdminInvitationFlowTests.cs`**

Same pattern.

- [ ] **Step 8: Run the full API test suite**

```bash
cd /c/Users/Admin/Desktop/auracorepro/AuraCorePro
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --no-restore 2>&1 | tail -5
```

Expected: 183/183 passing (no regressions). If any other test class uses the old pattern and breaks, migrate it identically.

- [ ] **Step 9: Commit**

```bash
git add tests/AuraCore.Tests.API/Support/ tests/AuraCore.Tests.API/SuperadminFoundation/SuperadminLoginEndpointTests.cs tests/AuraCore.Tests.API/SuperadminFoundation/TwoFactorEnforcementTests.cs tests/AuraCore.Tests.API/SuperadminFoundation/LoginSuspendedAccountTests.cs tests/AuraCore.Tests.API/SuperadminFoundation/AdminInvitationFlowTests.cs
git commit -m "test(6.12.W1.T1): TestWebAppFactory base + AlwaysAllowCaptchaVerifier stub

Centralize the WebApplicationFactory<Program> configuration that 4+
test classes were duplicating: shared InMemoryDatabaseRoot, suppress
ManyServiceProvidersCreatedWarning, register an always-allow stub for
the upcoming ICaptchaVerifier. Migrating the four existing fixtures
unblocks the Phase 6.12 CAPTCHA enforcement work."
```

---

## Wave 2 — Backend infrastructure

### Task 2: ICaptchaVerifier interface

**Files:**
- Create: `src/Backend/AuraCore.API.Application/Services/Security/ICaptchaVerifier.cs`
- Modify: `tests/AuraCore.Tests.API/Support/TestWebAppFactory.cs` (uncomment captcha block)
- Modify: `tests/AuraCore.Tests.API/Support/AlwaysAllowCaptchaVerifier.cs` (uncomment using)

- [ ] **Step 1: Create the interface**

`src/Backend/AuraCore.API.Application/Services/Security/ICaptchaVerifier.cs`:

```csharp
namespace AuraCore.API.Application.Services.Security;

/// <summary>
/// Verifies a CAPTCHA challenge token, returning true when the token is
/// valid and false otherwise. Implementations may emit a fail-open response
/// (returning true with a warning log) when the upstream provider is
/// unreachable — see TurnstileVerifier for the Polly circuit-breaker policy.
/// </summary>
public interface ICaptchaVerifier
{
    Task<bool> VerifyAsync(string token, string remoteIp, CancellationToken ct = default);
}
```

- [ ] **Step 2: Re-enable captcha registration in TestWebAppFactory**

Open `tests/AuraCore.Tests.API/Support/TestWebAppFactory.cs`. Add `using AuraCore.API.Application.Services.Security;` and remove the `#if PHASE_6_12_CAPTCHA_READY` wrapper (or replace `#if false` with `#if true`):

```csharp
using AuraCore.API.Application.Services.Security;
// ... existing usings ...

protected override void ConfigureWebHost(IWebHostBuilder builder)
{
    builder.ConfigureServices(services =>
    {
        // ... existing DbContext substitution ...

        // Phase 6.12 — substitute Turnstile verifier with always-allow stub.
        var captchaDesc = services.SingleOrDefault(d => d.ServiceType == typeof(ICaptchaVerifier));
        if (captchaDesc is not null) services.Remove(captchaDesc);
        services.AddSingleton<ICaptchaVerifier, AlwaysAllowCaptchaVerifier>();
    });
}
```

- [ ] **Step 3: Re-enable AlwaysAllowCaptchaVerifier**

Same — remove any conditional compile guards. The `using AuraCore.API.Application.Services.Security;` should resolve now.

- [ ] **Step 4: Build + run full API tests**

```bash
dotnet build tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --no-restore 2>&1 | tail -5
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --no-restore 2>&1 | tail -5
```

Expected: build clean, 183/183 tests still passing (interface alone has no behavior).

- [ ] **Step 5: Commit**

```bash
git add src/Backend/AuraCore.API.Application/Services/Security/ICaptchaVerifier.cs tests/AuraCore.Tests.API/Support/TestWebAppFactory.cs tests/AuraCore.Tests.API/Support/AlwaysAllowCaptchaVerifier.cs
git commit -m "feat(6.12.W2.T2): ICaptchaVerifier interface + test stub registration

Application-layer interface for CAPTCHA verification. Defines the contract
TurnstileVerifier (T3) implements and AlwaysAllowCaptchaVerifier (T1)
substitutes for tests. No behavior change yet; auth controllers wire it
in W6 (T9-T12)."
```

### Task 3: TurnstileVerifier with Polly circuit breaker

**Files:**
- Create: `src/Backend/AuraCore.API.Infrastructure/Services/Security/TurnstileVerifier.cs`
- Modify: `src/Backend/AuraCore.API.Infrastructure/AuraCore.API.Infrastructure.csproj`
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/TurnstileVerifierTests.cs`

- [ ] **Step 1: Add the Polly resilience NuGet package**

Open `src/Backend/AuraCore.API.Infrastructure/AuraCore.API.Infrastructure.csproj`. Add to the `<ItemGroup>` containing `PackageReference` entries:

```xml
<PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="8.10.0" />
```

If this version doesn't exist on NuGet at install time, use the latest 8.x. Run:

```bash
dotnet restore src/Backend/AuraCore.API.Infrastructure/AuraCore.API.Infrastructure.csproj 2>&1 | tail -5
```

Expected: package restored without errors.

- [ ] **Step 2: Write the failing test**

`tests/AuraCore.Tests.API/SuperadminFoundation/TurnstileVerifierTests.cs`:

```csharp
using System.Net;
using System.Net.Http;
using AuraCore.API.Infrastructure.Services.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

/// <summary>
/// Verifier-only tests — Polly resilience pipeline tested separately in
/// CaptchaCircuitBreakerTests via DI integration.
/// </summary>
public class TurnstileVerifierTests
{
    private static HttpClient ClientThatReturns(HttpStatusCode status, string body)
        => new(new StubHandler(status, body)) { BaseAddress = new Uri("https://challenges.cloudflare.com/") };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        public HttpRequestMessage? LastRequest;
        public string? LastBody;
        public StubHandler(HttpStatusCode status, string body) { _status = status; _body = body; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            LastRequest = req;
            if (req.Content is not null) LastBody = await req.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(_status) { Content = new StringContent(_body) };
        }
    }

    [Fact]
    public async Task Verify_returns_true_when_CF_responds_success_true()
    {
        var http = ClientThatReturns(HttpStatusCode.OK, "{\"success\":true}");
        Environment.SetEnvironmentVariable("TURNSTILE_SECRET_KEY", "test-secret");
        var v = new TurnstileVerifier(http, NullLogger<TurnstileVerifier>.Instance);
        var ok = await v.VerifyAsync("user-token", "1.2.3.4");
        Assert.True(ok);
    }

    [Fact]
    public async Task Verify_returns_false_when_CF_responds_success_false()
    {
        var http = ClientThatReturns(HttpStatusCode.OK, "{\"success\":false,\"error-codes\":[\"invalid-input-response\"]}");
        Environment.SetEnvironmentVariable("TURNSTILE_SECRET_KEY", "test-secret");
        var v = new TurnstileVerifier(http, NullLogger<TurnstileVerifier>.Instance);
        var ok = await v.VerifyAsync("bad-token", "1.2.3.4");
        Assert.False(ok);
    }

    [Fact]
    public async Task Verify_sends_form_data_with_secret_response_remoteip()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{\"success\":true}");
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://challenges.cloudflare.com/") };
        Environment.SetEnvironmentVariable("TURNSTILE_SECRET_KEY", "the-secret");
        var v = new TurnstileVerifier(http, NullLogger<TurnstileVerifier>.Instance);
        await v.VerifyAsync("user-token", "1.2.3.4");
        Assert.NotNull(handler.LastBody);
        Assert.Contains("secret=the-secret", handler.LastBody);
        Assert.Contains("response=user-token", handler.LastBody);
        Assert.Contains("remoteip=1.2.3.4", handler.LastBody);
    }

    [Fact]
    public async Task Verify_returns_false_on_HTTP_error_status()
    {
        var http = ClientThatReturns(HttpStatusCode.InternalServerError, "");
        Environment.SetEnvironmentVariable("TURNSTILE_SECRET_KEY", "test-secret");
        var v = new TurnstileVerifier(http, NullLogger<TurnstileVerifier>.Instance);
        var ok = await v.VerifyAsync("user-token", "1.2.3.4");
        Assert.False(ok);
    }
}
```

- [ ] **Step 3: Run failing test**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter "FullyQualifiedName~TurnstileVerifierTests" --no-restore 2>&1 | tail -10
```

Expected: CS0246 — `TurnstileVerifier` not found.

- [ ] **Step 4: Create TurnstileVerifier**

`src/Backend/AuraCore.API.Infrastructure/Services/Security/TurnstileVerifier.cs`:

```csharp
using System.Text.Json;
using AuraCore.API.Application.Services.Security;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;

namespace AuraCore.API.Infrastructure.Services.Security;

/// <summary>
/// Cloudflare Turnstile CAPTCHA verifier. Posts to
/// challenges.cloudflare.com/turnstile/v0/siteverify and returns the
/// upstream's "success" boolean.
///
/// The HttpClient is configured with a Polly resilience pipeline in
/// Program.cs (T4) — circuit-breaker opens after 5 consecutive failures
/// in a 60s window and stays open for 60s. When the breaker is open,
/// VerifyAsync catches BrokenCircuitException and returns true (fail-open)
/// plus emits a Warning-level log. Rate-limit + BCrypt timing defense
/// continue to protect the auth path during bypass mode.
/// </summary>
public sealed class TurnstileVerifier : ICaptchaVerifier
{
    private const string Endpoint = "turnstile/v0/siteverify";
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;
    private readonly ILogger<TurnstileVerifier> _logger;

    public TurnstileVerifier(HttpClient http, ILogger<TurnstileVerifier> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<bool> VerifyAsync(string token, string remoteIp, CancellationToken ct = default)
    {
        var secret = Environment.GetEnvironmentVariable("TURNSTILE_SECRET_KEY");
        if (string.IsNullOrEmpty(secret))
        {
            _logger.LogError("TURNSTILE_SECRET_KEY env var is missing — failing the verify (closed)");
            return false;
        }

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("secret", secret),
            new KeyValuePair<string, string>("response", token ?? ""),
            new KeyValuePair<string, string>("remoteip", remoteIp ?? ""),
        });

        try
        {
            using var resp = await _http.PostAsync(Endpoint, form, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Turnstile verify returned non-success status {Status}", resp.StatusCode);
                return false;
            }
            var json = await resp.Content.ReadAsStringAsync(ct);
            var parsed = JsonSerializer.Deserialize<TurnstileResponse>(json, JsonOpts);
            return parsed?.Success ?? false;
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning("Turnstile circuit open — bypass mode (fail-open). " +
                               "Rate limit + BCrypt + 2FA still active.");
            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Turnstile verify HTTP failure — closed");
            return false;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Turnstile verify timeout — closed");
            return false;
        }
    }

    private sealed record TurnstileResponse(bool Success);
}
```

- [ ] **Step 5: Run tests, expect pass**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter "FullyQualifiedName~TurnstileVerifierTests" --no-restore 2>&1 | tail -5
```

Expected: 4/4 passing.

- [ ] **Step 6: Run full API suite — confirm no regression**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --no-restore 2>&1 | tail -5
```

Expected: 187/187 passing (183 pre-Phase-6.12 + 4 new TurnstileVerifierTests).

- [ ] **Step 7: Commit**

```bash
git add src/Backend/AuraCore.API.Infrastructure/AuraCore.API.Infrastructure.csproj src/Backend/AuraCore.API.Infrastructure/Services/Security/TurnstileVerifier.cs tests/AuraCore.Tests.API/SuperadminFoundation/TurnstileVerifierTests.cs
git commit -m "feat(6.12.W2.T3): TurnstileVerifier — Cloudflare CAPTCHA backend with circuit-breaker fallback

POSTs to challenges.cloudflare.com/turnstile/v0/siteverify with
secret/response/remoteip form data, parses {success: bool} response.
Catches BrokenCircuitException → fail-open + warning log. Catches
HttpRequestException + TaskCanceledException → fail-closed (treats
the verify as a definite no).

The Polly resilience pipeline (5-fail circuit breaker, 60s break) is
wired in Program.cs (T4) on the HttpClient registration — verifier
itself is provider-agnostic.

Adds Microsoft.Extensions.Http.Resilience 8.10.0 NuGet package."
```

### Task 4: Program.cs DI + Polly pipeline + env var

**Files:**
- Modify: `src/Backend/AuraCore.API/Program.cs`

- [ ] **Step 1: Locate existing AddScoped registrations in Program.cs**

```bash
grep -n "AddScoped<.*Security\|AddHttpClient\|AddResilience" src/Backend/AuraCore.API/Program.cs
```

Find the cluster of security-related `AddScoped` calls (around line 41-77, near `IWhitelistService` and `ITotpEncryption`).

- [ ] **Step 2: Add the HttpClient + resilience pipeline registration**

Insert after the existing `IWhitelistService` registration (around line 44):

```csharp
// Phase 6.12.W2.T4 — Cloudflare Turnstile CAPTCHA verifier with Polly
// circuit breaker (5 consecutive fails in 60s sampling window → 60s break).
// HttpClient timeout is 5s; verifier returns true (fail-open) when the
// breaker is open so a CF outage does not take down the auth path.
builder.Services.AddHttpClient<
    AuraCore.API.Application.Services.Security.ICaptchaVerifier,
    AuraCore.API.Infrastructure.Services.Security.TurnstileVerifier>(client =>
{
    client.BaseAddress = new Uri("https://challenges.cloudflare.com/");
    client.Timeout = TimeSpan.FromSeconds(5);
})
.AddResilienceHandler("captcha-verify", pipeline =>
{
    pipeline.AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions<HttpResponseMessage>
    {
        FailureRatio = 1.0,
        MinimumThroughput = 5,
        SamplingDuration = TimeSpan.FromMinutes(1),
        BreakDuration = TimeSpan.FromSeconds(60),
        ShouldHandle = new Polly.PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .Handle<TaskCanceledException>()
            .HandleResult(r => (int)r.StatusCode >= 500),
    });
});
```

The `using` block at the top of Program.cs may need `using Polly;` and `using Polly.CircuitBreaker;` — add if compile errors point there.

- [ ] **Step 3: Confirm build**

```bash
dotnet build src/Backend/AuraCore.API/AuraCore.API.csproj --no-restore 2>&1 | tail -5
```

Expected: build clean. If `Polly.PredicateBuilder` or `CircuitBreakerStrategyOptions` not found, the resilience package isn't installed in the API project — add `Microsoft.Extensions.Http.Resilience 8.10.0` to `src/Backend/AuraCore.API/AuraCore.API.csproj` `<PackageReference>` block.

- [ ] **Step 4: Run full API suite**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --no-restore 2>&1 | tail -5
```

Expected: 187/187 passing. The DI replacement in TestWebAppFactory means tests use `AlwaysAllowCaptchaVerifier`, not the real Polly-wrapped TurnstileVerifier.

- [ ] **Step 5: Commit**

```bash
git add src/Backend/AuraCore.API/Program.cs src/Backend/AuraCore.API/AuraCore.API.csproj
git commit -m "feat(6.12.W2.T4): Program.cs wires TurnstileVerifier with Polly circuit breaker

AddHttpClient binds ICaptchaVerifier → TurnstileVerifier with a 5s timeout
and a circuit-breaker pipeline: 5 consecutive failures in a 60s sampling
window opens the breaker for 60s. ShouldHandle catches HttpRequestException,
TaskCanceledException, and any 5xx response. After break duration the
breaker half-opens; first success closes it, first failure re-opens for
another 60s.

Tests substitute AlwaysAllowCaptchaVerifier via TestWebAppFactory so this
production DI registration is bypassed in xUnit."
```

---

## Wave 3 — IEmailService XSS

### Task 5: ResendEmailService.ApplyPlaceholders HTML encoding

**Files:**
- Modify: `src/Backend/AuraCore.API.Infrastructure/Services/Email/ResendEmailService.cs`
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/EmailServiceXssTests.cs`

- [ ] **Step 1: Write failing test**

`tests/AuraCore.Tests.API/SuperadminFoundation/EmailServiceXssTests.cs`:

```csharp
using System.Reflection;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

/// <summary>
/// Phase 6.12.W3.T5 — verifies that ResendEmailService.ApplyPlaceholders
/// HTML-encodes every placeholder value before substituting into the
/// template, preventing XSS via free-text admin inputs (reason, reviewNote)
/// that flow into permission-event email bodies.
///
/// ApplyPlaceholders is private; test reaches it via reflection. This is
/// acceptable for a single-method security-critical helper — the alternative
/// (extract to a public utility class) would over-expand surface area for
/// one test.
/// </summary>
public class EmailServiceXssTests
{
    private static string Apply(string template, object data)
    {
        var t = typeof(AuraCore.API.Infrastructure.Services.Email.ResendEmailService);
        var m = t.GetMethod("ApplyPlaceholders", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ApplyPlaceholders not found");
        return (string)m.Invoke(null, new object[] { template, data })!;
    }

    public sealed record Data(string reason);

    [Fact]
    public void Encodes_script_tag_payload()
    {
        var output = Apply("body: {{reason}}", new Data("<script>alert(1)</script>"));
        Assert.Equal("body: &lt;script&gt;alert(1)&lt;/script&gt;", output);
        Assert.DoesNotContain("<script>", output);
    }

    [Fact]
    public void Encodes_all_reserved_chars()
    {
        var output = Apply("{{reason}}", new Data("a & b < c > d \" e ' f"));
        Assert.Contains("&amp;", output);
        Assert.Contains("&lt;", output);
        Assert.Contains("&gt;", output);
        Assert.Contains("&quot;", output);
        Assert.Contains("&#39;", output);
    }

    [Fact]
    public void Plain_text_passes_through_unchanged()
    {
        var output = Apply("Hello {{reason}}", new Data("admin needs access"));
        Assert.Equal("Hello admin needs access", output);
    }

    [Fact]
    public void Null_value_renders_empty_string()
    {
        var output = Apply("[{{reason}}]", new Data(null!));
        Assert.Equal("[]", output);
    }
}
```

- [ ] **Step 2: Run test, expect 3 of 4 to fail**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter "FullyQualifiedName~EmailServiceXssTests" --no-restore 2>&1 | tail -10
```

Expected: `Plain_text_passes_through_unchanged` and `Null_value_renders_empty_string` PASS, the encoding tests FAIL because the current implementation does raw `.ToString()` without HtmlEncode.

- [ ] **Step 3: Apply the fix**

Open `src/Backend/AuraCore.API.Infrastructure/Services/Email/ResendEmailService.cs`. Find `ApplyPlaceholders` (around line 80):

```csharp
    private static string ApplyPlaceholders(string template, object data)
    {
        var props = data.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var p in props)
            template = template.Replace("{{" + p.Name + "}}", p.GetValue(data)?.ToString() ?? "");
        return template;
    }
```

Replace with:

```csharp
    private static string ApplyPlaceholders(string template, object data)
    {
        var props = data.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var p in props)
        {
            var raw = p.GetValue(data)?.ToString() ?? "";
            // Phase 6.12.W3.T5 — HTML-encode every placeholder value.
            // Free-text admin inputs (permission-request reason, review note)
            // flow into HTML email templates and were previously embedded raw.
            // No current template embeds a placeholder inside a <script> or
            // url(...) context, so HtmlEncode (which is correct for HTML body
            // text + most attribute values) suffices.
            var encoded = System.Net.WebUtility.HtmlEncode(raw);
            template = template.Replace("{{" + p.Name + "}}", encoded);
        }
        return template;
    }
```

- [ ] **Step 4: Run XSS tests — expect all green**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter "FullyQualifiedName~EmailServiceXssTests" --no-restore 2>&1 | tail -5
```

Expected: 4/4 passing.

- [ ] **Step 5: Run full API suite**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --no-restore 2>&1 | tail -5
```

Expected: 191/191 passing (187 + 4 new). No regression in pre-existing tests — none of the 6 email templates depend on raw-HTML placeholders.

- [ ] **Step 6: Commit**

```bash
git add src/Backend/AuraCore.API.Infrastructure/Services/Email/ResendEmailService.cs tests/AuraCore.Tests.API/SuperadminFoundation/EmailServiceXssTests.cs
git commit -m "fix(6.12.W3.T5): HTML-encode email template placeholders

ResendEmailService.ApplyPlaceholders previously concatenated each
placeholder value via raw .ToString() into the template HTML, allowing
free-text admin inputs (permission-request reason, review note) to
inject script tags or attributes into the email body delivered to
admin/superadmin recipients.

Wrap each value in System.Net.WebUtility.HtmlEncode before substitution.
Existing 6 templates do not embed placeholders inside script or url()
contexts, so HtmlEncode is a sound default. Future raw-HTML placeholders
must use static template content, not data fields.

+4 regression tests in EmailServiceXssTests.cs (script payload, reserved
chars, plain-text passthrough, null-safe)."
```

---

## Wave 4 — Tiered rate limit on superadmin login

### Task 6: SuperadminLogin three-layer rate limit

**Files:**
- Modify: `src/Backend/AuraCore.API/Controllers/AuthController.cs`
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/LoginRateLimitTieredTests.cs`

- [ ] **Step 1: Inspect the existing rate-limit block**

```bash
sed -n '280,300p' src/Backend/AuraCore.API/Controllers/AuthController.cs
```

You should see the existing single email-scoped `CountAsync` block (around line 285-293). Note the exact line numbers; you'll replace this block.

- [ ] **Step 2: Write failing tests**

`tests/AuraCore.Tests.API/SuperadminFoundation/LoginRateLimitTieredTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using AuraCore.API.Domain.Entities;
using AuraCore.Tests.API.Support;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

/// <summary>
/// Phase 6.12.W4.T6 — tiered rate limit on /api/auth/superadmin/login:
///   layer 1 — 3 fails/email/60min (existing)
///   layer 2 — 10 fails/IP/60min (new — defends against email rotation)
///   layer 3 — 30 fails/IP/24h (new — defends against slow-drip distributed attack)
/// All three gated behind whitelisted-IP bypass.
/// </summary>
public class LoginRateLimitTieredTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _f;
    public LoginRateLimitTieredTests(TestWebAppFactory f) => _f = f;

    private static LoginAttempt Fail(string email, string ip, DateTimeOffset when)
        => new() { Email = email, IpAddress = ip, Success = false, CreatedAt = when };

    private async Task<HttpStatusCode> AttemptAsync(string email)
    {
        var c = _f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/superadmin/login", new { email, password = "wrong-pass-12", totpCode = "000000" });
        return r.StatusCode;
    }

    [Fact]
    public async Task Returns_429_after_3_email_fails_in_60min()
    {
        var ip = "9.9.9.9";
        await _f.SeedAsync(db =>
        {
            for (int i = 0; i < 3; i++)
                db.LoginAttempts.Add(Fail("victim@x.com", ip, DateTimeOffset.UtcNow.AddMinutes(-5)));
        });
        var status = await AttemptAsync("victim@x.com");
        Assert.Equal(HttpStatusCode.TooManyRequests, status);
    }

    [Fact]
    public async Task Returns_429_after_10_IP_fails_in_60min()
    {
        var ip = "9.9.9.10";
        await _f.SeedAsync(db =>
        {
            for (int i = 0; i < 10; i++)
                db.LoginAttempts.Add(Fail($"u{i}@x.com", ip, DateTimeOffset.UtcNow.AddMinutes(-5)));
        });
        // 11th attempt from a brand-new email but same IP must still be blocked
        var status = await AttemptAsync("never-tried-before@x.com");
        Assert.Equal(HttpStatusCode.TooManyRequests, status);
    }

    [Fact]
    public async Task Returns_429_after_30_IP_fails_in_24h_but_under_60min_threshold()
    {
        var ip = "9.9.9.11";
        await _f.SeedAsync(db =>
        {
            // 30 fails spread across the last 12 hours (none within 60 min)
            for (int i = 0; i < 30; i++)
                db.LoginAttempts.Add(Fail($"slow{i}@x.com", ip, DateTimeOffset.UtcNow.AddHours(-1).AddMinutes(-i * 20)));
        });
        var status = await AttemptAsync("yet-another@x.com");
        Assert.Equal(HttpStatusCode.TooManyRequests, status);
    }

    [Fact]
    public async Task Whitelisted_IP_bypasses_all_three_caps()
    {
        var ip = "127.0.0.1"; // see whether prod adds this — replace with a stub-friendly value otherwise
        await _f.SeedAsync(db =>
        {
            db.IpWhitelists.Add(new IpWhitelist { IpAddress = ip, AddedBy = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow });
            for (int i = 0; i < 50; i++)
                db.LoginAttempts.Add(Fail($"u{i}@x.com", ip, DateTimeOffset.UtcNow.AddMinutes(-5)));
        });
        // Whitelisted IP should reach the auth flow (returning 401 because the user does not exist)
        var c = _f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/superadmin/login", new { email = "ghost@x.com", password = "wrong", totpCode = "000000" });
        Assert.NotEqual(HttpStatusCode.TooManyRequests, r.StatusCode);
    }
}
```

Note about the whitelist test: the integration test client uses `127.0.0.1` as the remote address by default. If `IpWhitelist` table requires fields not shown above, fill them in to match the entity's actual NOT-NULL columns — inspect `src/Backend/AuraCore.API.Domain/Entities/IpWhitelist.cs`.

- [ ] **Step 3: Run failing tests**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter "FullyQualifiedName~LoginRateLimitTieredTests" --no-restore 2>&1 | tail -10
```

Expected: `Returns_429_after_3_email_fails_in_60min` and `Whitelisted_IP_bypasses_all_three_caps` PASS (existing behavior). `Returns_429_after_10_IP_fails_in_60min` and `Returns_429_after_30_IP_fails_in_24h_but_under_60min_threshold` FAIL (no IP cap yet).

- [ ] **Step 4: Replace the rate-limit block in AuthController.SuperadminLogin**

Open `src/Backend/AuraCore.API/Controllers/AuthController.cs`. Find the SuperadminLogin block around line 280-300. Replace the existing single-`CountAsync` block (whatever it currently looks like — the spec describes it) with:

```csharp
        // Phase 6.12.W4.T6 — tiered rate limit:
        //   layer 1 — 3 fails per email per 60 min (catches password guessing)
        //   layer 2 — 10 fails per IP per 60 min (catches email rotation)
        //   layer 3 — 30 fails per IP per 24 h (catches slow-drip distributed)
        // All three gated behind whitelisted-IP bypass.
        var whitelisted = await _whitelist.IsWhitelistedAsync(ip, ct);
        if (!whitelisted)
        {
            var now = DateTimeOffset.UtcNow;

            var emailFails = await _db.LoginAttempts.CountAsync(a =>
                a.Email == email && !a.Success && a.CreatedAt > now.AddMinutes(-60), ct);
            if (emailFails >= 3)
                return StatusCode(429, new { error = "Too many failed attempts for this email. Try again in 60 minutes." });

            var ipFailsShort = await _db.LoginAttempts.CountAsync(a =>
                a.IpAddress == ip && !a.Success && a.CreatedAt > now.AddMinutes(-60), ct);
            if (ipFailsShort >= 10)
                return StatusCode(429, new { error = "Too many failed attempts from this IP. Try again in 60 minutes." });

            var ipFailsLong = await _db.LoginAttempts.CountAsync(a =>
                a.IpAddress == ip && !a.Success && a.CreatedAt > now.AddHours(-24), ct);
            if (ipFailsLong >= 30)
                return StatusCode(429, new { error = "Too many failed attempts from this IP today. Try again later." });
        }
```

- [ ] **Step 5: Run failing tests, expect green**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter "FullyQualifiedName~LoginRateLimitTieredTests" --no-restore 2>&1 | tail -5
```

Expected: 4/4 passing.

- [ ] **Step 6: Run full suite — confirm no regression**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --no-restore 2>&1 | tail -5
```

Expected: 195/195 passing (191 + 4 new). Existing `SuperadminLoginEndpointTests` still pass because their rate-limit assertions stay within the new bounds.

- [ ] **Step 7: Commit**

```bash
git add src/Backend/AuraCore.API/Controllers/AuthController.cs tests/AuraCore.Tests.API/SuperadminFoundation/LoginRateLimitTieredTests.cs
git commit -m "feat(6.12.W4.T6): tiered rate limit on /api/auth/superadmin/login

Pre-6.12 the endpoint counted only fails per email per 60 min. An attacker
rotating across N emails from one IP hit the email cap N times but never
any IP cap. Now three layers stack:
  L1 3 fails/email/60min — existing, password-guessing attack
  L2 10 fails/IP/60min   — new, email-rotation attack
  L3 30 fails/IP/24h     — new, slow-drip distributed attack

All three gated behind whitelisted-IP bypass. Three EF queries per attempt
is cheap given low superadmin login traffic.

+4 regression tests covering each layer's threshold + whitelist bypass."
```

---

## Wave 5 — BCrypt timing-attack defense

### Task 7: SuperadminLogin dummy hash defense

**Files:**
- Modify: `src/Backend/AuraCore.API/Controllers/AuthController.cs`
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/LoginTimingDefenseTests.cs`

- [ ] **Step 1: Determine the production BCrypt work factor**

```bash
grep -rn "BCrypt.Net.BCrypt.HashPassword\|HashPassword(" src/Backend --include="*.cs" | grep -v "/bin/\|/obj/" | head -10
```

Look for the registration / password-set call site (likely in `AuthService` or `AdminUserController.ResetPassword` or `AuthController.Register`). Identify whether `workFactor:` is passed as an argument.

- If a `workFactor: N` is explicit, use that N for the dummy hash.
- If no work factor is passed, BCrypt.Net-Next default is `11`. Use `11`.
- Spot-check a real prod password hash if accessible (the cost factor is the second-segment digit in `$2a$NN$...`). On a development machine, try: `psql ... -c "SELECT \"PasswordHash\" FROM users LIMIT 1;"` or examine a test fixture's BCrypt.HashPassword default behavior.

Record the determined work factor — call it `WF`. Default to `11` if unable to spot-check.

- [ ] **Step 2: Write failing test**

`tests/AuraCore.Tests.API/SuperadminFoundation/LoginTimingDefenseTests.cs`:

```csharp
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

    [Fact]
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
```

- [ ] **Step 3: Run tests, confirm initial state**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter "FullyQualifiedName~LoginTimingDefenseTests" --no-restore 2>&1 | tail -10
```

Expected: `SuperadminLogin_response_time_does_not_leak_user_existence` FAILS (delta exceeds 100 ms because nonexistent-email returns instantly and existing-email runs full BCrypt).

`Login_response_time_does_not_leak_user_existence` may pass or fail depending on whether `IAuthService.LoginAsync` already has the defense. T8 covers it explicitly.

- [ ] **Step 4: Apply dummy-hash defense to SuperadminLogin**

Open `src/Backend/AuraCore.API/Controllers/AuthController.cs`. Add a static field to the class (next to other private static fields, e.g., near `_regAttempts` declaration around line 26):

```csharp
    // Phase 6.12.W5.T7 — precomputed dummy hash for BCrypt timing-attack
    // defense. Work factor MUST match production hashing (see W5.T7
    // resolution step). Static so the BCrypt.HashPassword cost is paid
    // once at app startup, not per login attempt.
    private static readonly string _dummyHashWf11 =
        BCrypt.Net.BCrypt.HashPassword("dummy-password-never-matches-anything", workFactor: 11);
```

Adjust `workFactor: 11` to `WF` from Step 1 if different.

Find the SuperadminLogin block where the user is fetched (around line 300, after the rate-limit block). The current code reads roughly:

```csharp
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        // ... password verification logic somewhere ...
```

Wrap the user fetch + verify with the dummy-hash pattern:

```csharp
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        // Phase 6.12.W5.T7 — always run BCrypt.Verify against either the real
        // user's hash or the dummy hash so the response time does not depend
        // on email existence. Work factor matches new-registration path.
        var hashToVerify = user?.PasswordHash ?? _dummyHashWf11;
        var passwordValid = BCrypt.Net.BCrypt.Verify(request.Password, hashToVerify);

        if (user is null || user.Role != "superadmin" || !passwordValid)
        {
            await LogAttempt(success: false);
            return Unauthorized(new { error = "Invalid credentials" });
        }
```

If the existing code has additional shape (e.g., it sets `user.Role` checks separately), preserve those checks but route them all into the same generic-401 branch — the goal is one error path with constant time.

- [ ] **Step 5: Run timing test, expect pass**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter "SuperadminLogin_response_time_does_not_leak_user_existence" --no-restore 2>&1 | tail -5
```

Expected: PASS (delta < 100 ms).

- [ ] **Step 6: Run full suite, confirm no regression**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --no-restore 2>&1 | tail -5
```

Expected: 196/196 (195 + 1 new SuperadminLogin timing test passing). The `Login_response_time_does_not_leak_user_existence` test may still fail — T8 fixes it.

- [ ] **Step 7: Commit**

```bash
git add src/Backend/AuraCore.API/Controllers/AuthController.cs tests/AuraCore.Tests.API/SuperadminFoundation/LoginTimingDefenseTests.cs
git commit -m "feat(6.12.W5.T7): SuperadminLogin BCrypt timing-attack defense via dummy hash

Pre-fix: nonexistent-email returned instantly (no BCrypt.Verify ran),
existing-email returned after ~100-300 ms BCrypt work — observable
delta let attackers enumerate valid emails despite the generic 401.

Add a precomputed _dummyHashWf11 (work factor 11 matching new-registration
path) and always run BCrypt.Verify against either the real or dummy hash.
Generic-401 branch now covers (user null) | (wrong role) | (wrong password)
in constant time.

+1 regression test (5-sample median, 100 ms threshold). T8 covers regular
/login analogously."
```

### Task 8: IAuthService.LoginAsync dummy hash defense

**Files:**
- Modify: probably `src/Backend/AuraCore.API.Infrastructure/Services/Auth/AuthService.cs` (verify location)

- [ ] **Step 1: Locate LoginAsync implementation**

```bash
find src/Backend -name "AuthService.cs" -not -path "*/bin/*" -not -path "*/obj/*"
grep -n "LoginAsync\|public.*Login\|FirstOrDefaultAsync.*Email" src/Backend/AuraCore.API.Infrastructure/Services/Auth/AuthService.cs 2>&1 | head
```

If the file is somewhere else, locate it. The interface lives in `AuraCore.API.Application/Interfaces/IAuthService.cs` per Phase 6.11.

- [ ] **Step 2: Inspect the existing implementation**

Open the file. Look at the `LoginAsync` method body. Note whether it:
- Fetches the user via `FirstOrDefaultAsync` (probably yes)
- Calls `BCrypt.Net.BCrypt.Verify(...)` against `user.PasswordHash` (probably yes)
- Returns early when `user is null` (this is the bug)

If `LoginAsync` is already constant-time (e.g., it always verifies regardless of user existence), T8 is a no-op — verify by running the timing test and seeing it pass. Skip to Step 5.

- [ ] **Step 3: Apply dummy-hash defense**

Add a static field to the class (top of class, alongside any existing static state):

```csharp
    private static readonly string _dummyHashWf11 =
        BCrypt.Net.BCrypt.HashPassword("dummy-password-never-matches-anything", workFactor: 11);
```

Modify the user-fetch section so the verify step always runs:

```csharp
    var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    // Phase 6.12.W5.T8 — constant-time email-existence defense.
    var hashToVerify = user?.PasswordHash ?? _dummyHashWf11;
    var passwordValid = BCrypt.Net.BCrypt.Verify(password, hashToVerify);

    if (user is null || !passwordValid)
        return new LoginResult { Success = false, Error = "Invalid email or password" };
```

Adjust the exact return shape to match `LoginResult` or whatever type `IAuthService.LoginAsync` returns. The shape may differ — preserve the existing surface while routing through one constant-time branch.

If the method already has a `user is null` early return that bypasses BCrypt, REMOVE that early return.

- [ ] **Step 4: Run timing test, expect pass**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter "Login_response_time_does_not_leak_user_existence" --no-restore 2>&1 | tail -5
```

Expected: PASS.

- [ ] **Step 5: Run full suite**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --no-restore 2>&1 | tail -5
```

Expected: 197/197 passing (196 + 1 new Login timing test passing). All pre-existing tests stable.

- [ ] **Step 6: Commit**

```bash
git add src/Backend/AuraCore.API.Infrastructure/Services/Auth/AuthService.cs
git commit -m "feat(6.12.W5.T8): IAuthService.LoginAsync BCrypt timing-attack defense

Mirrors the SuperadminLogin defense from T7 — dummy hash with the same
work factor, BCrypt.Verify always runs regardless of user existence,
generic-fail branch covers null-user + wrong-password in constant time.

If LoginAsync was already constant-time, this is a no-op verification.
Otherwise the earlier null-user fast-return is removed."
```

---

## Wave 6 — CAPTCHA wiring on four auth endpoints

### Task 9: Login + Register + ForgotPassword + SuperadminLogin DTOs gain TurnstileToken

**Files:**
- Modify: `src/Backend/AuraCore.API/Controllers/AuthController.cs` (DTO records at file bottom)
- Modify: `src/Backend/AuraCore.API/Controllers/Auth/PasswordResetController.cs` (DTO record)

- [ ] **Step 1: Locate the DTO records**

In `AuthController.cs`, find the `public sealed record LoginRequest(...)` and `public sealed record RegisterRequest(...)` lines (around line 619-621). In `PasswordResetController.cs`, find the forgot-password request DTO.

- [ ] **Step 2: Add `TurnstileToken` property to each DTO**

`LoginRequest` (used by both `/login` and `/superadmin/login`):

```csharp
public sealed record LoginRequest(string Email, string Password, string? TotpCode = null, string? TurnstileToken = null);
```

`RegisterRequest`:

```csharp
public sealed record RegisterRequest(string Email, string Password, string? TurnstileToken = null);
```

`ForgotPasswordRequest` (or whatever the existing name is in PasswordResetController.cs — inspect first):

```csharp
public sealed record ForgotPasswordRequest(string Email, string? TurnstileToken = null);
```

If the existing DTO name differs, keep the existing name and add `TurnstileToken` as a new optional parameter.

- [ ] **Step 3: Verify build**

```bash
dotnet build src/Backend/AuraCore.API/AuraCore.API.csproj --no-restore 2>&1 | tail -5
```

Expected: clean build. Tests still pass (DTOs are additive — existing test bodies that don't pass TurnstileToken remain valid because the field is optional).

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --no-restore 2>&1 | tail -5
```

Expected: 197/197 passing.

- [ ] **Step 4: Commit**

```bash
git add src/Backend/AuraCore.API/Controllers/AuthController.cs src/Backend/AuraCore.API/Controllers/Auth/PasswordResetController.cs
git commit -m "feat(6.12.W6.T9): add TurnstileToken to four auth DTOs

Optional field on LoginRequest, RegisterRequest, ForgotPasswordRequest.
Optional so existing tests that omit the field remain valid; the
controllers (T10-T12) read it as the first action and require it once
TURNSTILE_TOLERANT_MODE=false."
```

### Task 10: AuthController CAPTCHA enforcement (Login + SuperadminLogin + Register)

**Files:**
- Modify: `src/Backend/AuraCore.API/Controllers/AuthController.cs`
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/CaptchaEnforcementTests.cs`

- [ ] **Step 1: Inject ICaptchaVerifier into the controller**

In `AuthController.cs`, find the constructor (around line 28) and field declarations (around line 19-26).

Add the field:

```csharp
    private readonly AuraCore.API.Application.Services.Security.ICaptchaVerifier _captcha;
    private static readonly bool _captchaEnabled =
        !string.Equals(Environment.GetEnvironmentVariable("CAPTCHA_ENABLED"), "false", StringComparison.OrdinalIgnoreCase);
    private static readonly bool _captchaTolerant =
        string.Equals(Environment.GetEnvironmentVariable("TURNSTILE_TOLERANT_MODE"), "true", StringComparison.OrdinalIgnoreCase);
```

Update the constructor to accept and assign it. The current ctor signature is:

```csharp
public AuthController(IAuthService auth, AuraCoreDbContext db, IWhitelistService whitelist, ITotpEncryption totpEnc, IHubContext<AdminHub> hub)
```

Add `ICaptchaVerifier captcha` as the last parameter:

```csharp
public AuthController(IAuthService auth, AuraCoreDbContext db, IWhitelistService whitelist, ITotpEncryption totpEnc, IHubContext<AdminHub> hub,
    AuraCore.API.Application.Services.Security.ICaptchaVerifier captcha)
{
    _auth = auth;
    _db = db;
    _whitelist = whitelist;
    _totpEnc = totpEnc;
    _hub = hub;
    _captcha = captcha;
}
```

- [ ] **Step 2: Add a private CAPTCHA gate helper at the bottom of the controller class**

Inside the `AuthController` class, add a private helper method (just above the closing brace of the class):

```csharp
    /// <summary>
    /// Phase 6.12.W6.T10 — gate every covered auth endpoint with a Turnstile
    /// verification step. Returns null on success (caller proceeds), or an
    /// IActionResult to return immediately on failure. CAPTCHA_ENABLED=false
    /// disables the check entirely (emergency escape hatch).
    /// TURNSTILE_TOLERANT_MODE=true allows missing tokens through during
    /// the rollout window between backend and frontend deploys.
    /// </summary>
    private async Task<IActionResult?> CheckCaptchaAsync(string? token, string ip, CancellationToken ct)
    {
        if (!_captchaEnabled) return null;
        if (string.IsNullOrEmpty(token))
        {
            if (_captchaTolerant) return null;
            return BadRequest(new { error = "captcha_required" });
        }
        var ok = await _captcha.VerifyAsync(token, ip, ct);
        return ok ? null : BadRequest(new { error = "captcha_invalid" });
    }
```

- [ ] **Step 3: Call the gate at the top of `Login`, `SuperadminLogin`, `Register`**

In `Login` (around line 129), after the basic empty-input check but before any DB work:

```csharp
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Email and password are required" });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Phase 6.12.W6.T10 — CAPTCHA gate.
        var captchaFail = await CheckCaptchaAsync(request.TurnstileToken, ip, ct);
        if (captchaFail is not null) return captchaFail;

        // ... existing flow continues (rate limit, password check, 2FA, etc.) ...
    }
```

Same pattern for `SuperadminLogin` (around line 257) and `Register` (around line 38). For `Register`, the `ip` variable may already exist or be computed differently — match the existing pattern.

- [ ] **Step 4: Write CAPTCHA enforcement tests**

`tests/AuraCore.Tests.API/SuperadminFoundation/CaptchaEnforcementTests.cs`:

```csharp
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
}
```

Note: Environment variable manipulation in tests has cross-test leak risk because static initializers in `AuthController` (`_captchaEnabled`, `_captchaTolerant`) read at class load time, not per-request. To make these tests reliable, set the env vars BEFORE `WebApplicationFactory<Program>` is first instantiated. The TestWebAppFactory constructor already runs once; subsequent env changes are observed only on first use.

A more reliable approach: wrap `_captchaEnabled` and `_captchaTolerant` in instance methods that re-read the env each call. Apply this fix:

In `AuthController.cs`, replace the two static fields:

```csharp
    private static readonly bool _captchaEnabled = ...
    private static readonly bool _captchaTolerant = ...
```

With instance properties (no readonly cache):

```csharp
    private static bool CaptchaEnabled =>
        !string.Equals(Environment.GetEnvironmentVariable("CAPTCHA_ENABLED"), "false", StringComparison.OrdinalIgnoreCase);
    private static bool CaptchaTolerant =>
        string.Equals(Environment.GetEnvironmentVariable("TURNSTILE_TOLERANT_MODE"), "true", StringComparison.OrdinalIgnoreCase);
```

And use `CaptchaEnabled` / `CaptchaTolerant` in the helper instead of the underscore fields. This adds 1 env-var read per CAPTCHA gate (negligible cost) and lets tests toggle the values at runtime.

- [ ] **Step 5: Run CAPTCHA tests, expect green**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter "FullyQualifiedName~CaptchaEnforcementTests" --no-restore 2>&1 | tail -10
```

Expected: 5/5 passing.

- [ ] **Step 6: Run full suite**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --no-restore 2>&1 | tail -5
```

Expected: 202/202 (197 + 5). Existing tests still green because TestWebAppFactory always registers `AlwaysAllowCaptchaVerifier` and the env var defaults align with strict mode but the always-allow verifier passes the gate when a token IS present. Tests without token bodies have not added one yet — re-check whether any pre-Phase-6.12 fixture relies on /login, /superadmin/login, or /register without a token. If so, those tests need a `turnstileToken = "x"` field added to their request bodies (the always-allow stub returns true regardless of token content).

If pre-existing tests now fail with `captcha_required`, fix each by adding the token field. Likely candidates: `SuperadminLoginEndpointTests`, `LoginSuspendedAccountTests`, `TwoFactorEnforcementTests`. Quick search:

```bash
grep -rn "PostAsJsonAsync.*\"/api/auth/" tests/AuraCore.Tests.API/ | head -20
```

Add `turnstileToken = "stub"` to each request body that POSTs to `/api/auth/login`, `/api/auth/superadmin/login`, or `/api/auth/register` (NOT to `/api/auth/redeem-invitation` — out of CAPTCHA scope).

- [ ] **Step 7: Commit**

```bash
git add src/Backend/AuraCore.API/Controllers/AuthController.cs tests/AuraCore.Tests.API/SuperadminFoundation/CaptchaEnforcementTests.cs tests/AuraCore.Tests.API/SuperadminFoundation/SuperadminLoginEndpointTests.cs tests/AuraCore.Tests.API/SuperadminFoundation/LoginSuspendedAccountTests.cs tests/AuraCore.Tests.API/SuperadminFoundation/TwoFactorEnforcementTests.cs
git commit -m "feat(6.12.W6.T10): CAPTCHA enforcement on Login + SuperadminLogin + Register

Inject ICaptchaVerifier; add CheckCaptchaAsync helper called as the first
action in each endpoint after empty-input checks. Two env vars control
behavior: CAPTCHA_ENABLED=false disables the check entirely (emergency
escape), TURNSTILE_TOLERANT_MODE=true permits missing tokens during the
rollout window.

+5 regression tests (CaptchaEnforcementTests). Pre-existing fixtures now
include turnstileToken='stub' fields where they POST to covered endpoints
— always-allow verifier stub keeps them green."
```

### Task 11: PasswordResetController.ForgotPassword CAPTCHA

**Files:**
- Modify: `src/Backend/AuraCore.API/Controllers/Auth/PasswordResetController.cs`

- [ ] **Step 1: Locate the controller**

```bash
find src/Backend -name "PasswordResetController*" -not -path "*/bin/*" -not -path "*/obj/*"
grep -n "forgot\|Forgot\|HttpPost\|public.*Async" src/Backend/AuraCore.API/Controllers/Auth/PasswordResetController.cs
```

- [ ] **Step 2: Inject ICaptchaVerifier**

Add field + ctor param identical to T10. The captcha gate helper from `AuthController` should ideally be shared — extract to a base class or a static helper. For minimum code, copy the helper inline (3-line method) — preferred for one extra controller.

Add identical static `CaptchaEnabled`/`CaptchaTolerant` properties + identical `CheckCaptchaAsync` helper method in `PasswordResetController`. (Future cleanup: extract to a shared base or `[ServiceFilter]` attribute. Out of scope for 6.12.)

- [ ] **Step 3: Call the gate in ForgotPassword**

Find the ForgotPassword action method. Add the gate as the first action after empty-input validation:

```csharp
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { error = "Email is required" });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Phase 6.12.W6.T11 — CAPTCHA gate.
        var captchaFail = await CheckCaptchaAsync(request.TurnstileToken, ip, ct);
        if (captchaFail is not null) return captchaFail;

        // ... existing flow ...
    }
```

If the existing action signature differs (e.g., uses a different DTO name), adapt accordingly. The `request.TurnstileToken` reference matches the field added in T9.

- [ ] **Step 4: Add a regression test in CaptchaEnforcementTests**

In `tests/AuraCore.Tests.API/SuperadminFoundation/CaptchaEnforcementTests.cs`, add:

```csharp
    [Fact]
    public async Task ForgotPassword_returns_400_when_token_missing()
    {
        Environment.SetEnvironmentVariable("TURNSTILE_TOLERANT_MODE", "false");
        Environment.SetEnvironmentVariable("CAPTCHA_ENABLED", "true");
        var c = _f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/forgot-password", new { email = "x@y.com" });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("captcha_required", await r.Content.ReadAsStringAsync());
    }
```

- [ ] **Step 5: Run tests**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter "FullyQualifiedName~CaptchaEnforcementTests" --no-restore 2>&1 | tail -5
```

Expected: 6/6 passing.

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --no-restore 2>&1 | tail -5
```

Expected: 203/203. Pre-existing PasswordResetController tests may need a `turnstileToken = "stub"` field added to their request bodies — same pattern as T10 Step 6.

- [ ] **Step 6: Commit**

```bash
git add src/Backend/AuraCore.API/Controllers/Auth/PasswordResetController.cs tests/AuraCore.Tests.API/SuperadminFoundation/CaptchaEnforcementTests.cs
git commit -m "feat(6.12.W6.T11): CAPTCHA enforcement on /api/auth/forgot-password

Mirrors T10 pattern in PasswordResetController. The CheckCaptchaAsync
helper is duplicated (3 lines) rather than extracted to a shared base —
acceptable for one extra controller. Future refactor: shared filter
attribute or base class. Same env var semantics (CAPTCHA_ENABLED,
TURNSTILE_TOLERANT_MODE). +1 regression test."
```

---

## Wave 7 — Frontend

### Task 12: admin-panel LoginScreen Turnstile widget

**Files:**
- Modify: `admin-panel/package.json`
- Modify: `admin-panel/src/components/LoginScreen.tsx`
- Modify: `admin-panel/.env.production` (or `.env.local` for dev)

- [ ] **Step 1: Install the React Turnstile dependency**

```bash
cd /c/Users/Admin/Desktop/auracorepro/AuraCorePro/admin-panel
npm install @marsidev/react-turnstile@^0.4 --save 2>&1 | tail -3
cd ..
```

Confirm `package.json` and `package-lock.json` updated.

- [ ] **Step 2: Add the env var**

`admin-panel/.env.production` (create if missing):

```
NEXT_PUBLIC_TURNSTILE_SITE_KEY=PUT_REAL_SITE_KEY_HERE
```

`admin-panel/.env.local` (for dev — NOT committed):

```
NEXT_PUBLIC_TURNSTILE_SITE_KEY=1x00000000000000000000AA
```

`1x00000000000000000000AA` is Cloudflare's official always-passes test site key. Use it for local dev so you don't need a real CF account during development.

`.env.production` and `.env.local` should NOT be committed if they're in `.gitignore`. If `.env.production` is currently committed, rotate the value at deploy time — don't commit a real site key.

- [ ] **Step 3: Update LoginScreen**

Open `admin-panel/src/components/LoginScreen.tsx`. Add to imports:

```typescript
import { Turnstile } from '@marsidev/react-turnstile';
```

Add state for the token next to other useState calls:

```typescript
const [turnstileToken, setTurnstileToken] = useState<string | null>(null);
```

Modify the `submit(...)` function (or wherever the API call is made) to include `turnstileToken` in the body. If the function signature is `submit(role: 'admin' | 'superadmin')`, the body construction passes through `api.login(email, password, totpCode, turnstileToken)` or similar — adapt to the existing pattern.

If `api.login` does not currently accept a `turnstileToken` parameter, modify `admin-panel/src/lib/api.ts` to accept and forward it:

Locate `async login(email, password, totpCode?)` in `api.ts`. Update:

```typescript
async login(email: string, password: string, totpCode?: string, turnstileToken?: string) {
  // ...existing body...
  const res = await request('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify({ email, password, totpCode, turnstileToken }),
  });
  // ...rest unchanged...
}
```

Add a similar `turnstileToken` argument to `superadminLogin` if that's a separate function.

In LoginScreen.tsx, render the widget INSIDE the `<form>`:

```tsx
<Turnstile
  siteKey={process.env.NEXT_PUBLIC_TURNSTILE_SITE_KEY!}
  onSuccess={(token) => setTurnstileToken(token)}
  onError={() => setTurnstileToken(null)}
  onExpire={() => setTurnstileToken(null)}
  options={{ theme: 'dark' }}
/>
```

Place it between the password input and the submit buttons.

Disable submit buttons when `turnstileToken` is null:

```tsx
<button type="submit" disabled={loading !== null || !turnstileToken}
  className="btn-primary w-full ...">...</button>
<button type="button" disabled={loading !== null || !turnstileToken}
  onClick={() => submit('superadmin')}
  className="...">...</button>
```

- [ ] **Step 4: Build the admin-panel**

```bash
cd admin-panel && npm run build 2>&1 | tail -8 && cd ..
```

Expected: clean build. Bundle size grows by ~3-5 KB (React Turnstile wrapper + script tag injection).

- [ ] **Step 5: Run admin-panel tests**

```bash
cd admin-panel && npx vitest run 2>&1 | tail -8 && cd ..
```

Expected: 59/59 passing. Existing LoginScreen tests may break if they assert a specific button-disabled state — fix by stubbing the Turnstile widget. Add to the relevant test file (`LoginScreen.superadmin.test.tsx`):

```typescript
vi.mock('@marsidev/react-turnstile', () => ({
  Turnstile: ({ onSuccess }: any) => {
    // Auto-trigger success in tests so submit button enables
    onSuccess?.('stub-token');
    return null;
  },
}));
```

Add this `vi.mock` near the top of any test file that renders `LoginScreen`. Do not commit a global mock unless the user prefers it.

- [ ] **Step 6: Commit**

```bash
git add admin-panel/package.json admin-panel/package-lock.json admin-panel/src/components/LoginScreen.tsx admin-panel/src/lib/api.ts admin-panel/src/__tests__/views/LoginScreen.superadmin.test.tsx admin-panel/.env.production
git commit -m "feat(6.12.W7.T12): admin-panel LoginScreen integrates Turnstile widget

Adds @marsidev/react-turnstile dep. Widget rendered inside the form,
token stored in local state, both submit buttons disabled until token
arrives. api.login + api.superadminLogin signatures gain optional
turnstileToken parameter that flows into the request body.

NEXT_PUBLIC_TURNSTILE_SITE_KEY env var read at build time. Tests stub
the widget via vi.mock to auto-resolve onSuccess so the submit button
enables in xUnit-equivalent runs."
```

### Task 13: landing-page (sign-in.html + register.html + forgot-password.html) Turnstile

**Files:**
- Modify (via local mirror): `landing-page-work/sign-in.html`
- Modify (via local mirror): `landing-page-work/register.html`
- Modify (via local mirror): `landing-page-work/forgot-password.html`

**Pre-step:** sync local mirror from origin (Phase 6.5 ritual):

```bash
mkdir -p ./landing-page-work
scp -i ~/.ssh/id_ed25519 -r root@165.227.170.3:/var/www/landing-page/* ./landing-page-work/
```

- [ ] **Step 1: Identify which forms exist**

```bash
ls landing-page-work/*.html | head
grep -l "/api/auth/" landing-page-work/*.html | head
```

If `register.html` does not exist (Resend's free tier limit + `NewRegistrations` toggle suggest user signups may be currently disabled), confirm with user before adding it. Skip register.html in this task if it's truly absent — note as deferred.

- [ ] **Step 2: Add CF Turnstile script + widget to sign-in.html**

In `<head>`, add:

```html
<script src="https://challenges.cloudflare.com/turnstile/v0/api.js" async defer></script>
```

Inside the `<form>` (just before the submit button), add:

```html
<div class="cf-turnstile" data-sitekey="PUT_REAL_SITE_KEY_HERE" data-callback="onTurnstileToken" data-theme="dark"></div>
<input type="hidden" name="turnstileToken" id="turnstile-response">
```

If the form already submits via JavaScript (preferred for SPA-style), add the JS callback near the existing form-handler script:

```html
<script>
  function onTurnstileToken(token) {
    document.getElementById('turnstile-response').value = token;
    document.getElementById('signin-submit').disabled = false;
  }
</script>
```

If the form submits via standard `<form action method>`, the hidden field already gets included. The `onTurnstileToken` callback enables the submit button.

The submit button:

```html
<button type="submit" id="signin-submit" disabled>Sign in</button>
```

If the existing form submits via `fetch()`, ensure the existing JavaScript reads the `turnstileToken` value from the hidden input (or the form's FormData), and includes it in the `JSON.stringify` body. Inspect the existing `sign-in.html` JavaScript and add the field to the request payload.

- [ ] **Step 3: Repeat for register.html (if exists)**

Same pattern. Field name in the request body must be `turnstileToken` to match the backend DTO.

- [ ] **Step 4: Repeat for forgot-password.html**

Same pattern.

- [ ] **Step 5: Smoke locally (optional)**

If you have a local landing-page dev server (e.g., `python -m http.server 8000` from the work directory), open `http://localhost:8000/sign-in.html` and verify the widget renders. Submit button should be initially disabled, then enable after the widget completes.

For deployment-only validation, skip to Wave 8.

- [ ] **Step 6: Commit the landing-page work directory**

Landing-page is NOT in the main repo per the user's setup. Commit to a separate location if there's a landing-page git repo, or skip the commit step. The change is captured in the deployed `/var/www/landing-page/` files via Wave 8.

If you want a local commit for tracking:

```bash
cd ./landing-page-work
git init 2>/dev/null || true
git add sign-in.html register.html forgot-password.html
git commit -m "feat(6.12.W7.T13): landing-page Turnstile widget on three auth forms"
cd ..
```

Otherwise, mark the work-directory as ready for Wave 8 deploy.

---

## Wave 8 — Three-phase deploy + ops

### Task 14: Deploy phase 1 — backend tolerant mode

**Files:** none (operational task).

- [ ] **Step 1: Set environment variables on origin**

```bash
ssh -i ~/.ssh/id_ed25519 root@165.227.170.3 "cat >> /etc/auracore-api.env <<EOF
TURNSTILE_SECRET_KEY=PASTE_REAL_SECRET_KEY_HERE
TURNSTILE_TOLERANT_MODE=true
CAPTCHA_ENABLED=true
EOF
chmod 600 /etc/auracore-api.env
echo 'env updated'"
```

Verify the values are in:

```bash
ssh -i ~/.ssh/id_ed25519 root@165.227.170.3 "grep -E 'TURNSTILE|CAPTCHA' /etc/auracore-api.env"
```

- [ ] **Step 2: Publish backend**

```bash
cd src/Backend/AuraCore.API
dotnet publish -c Release -r linux-x64 --self-contained false -o ./publish 2>&1 | tail -5
cd ../../..
```

- [ ] **Step 3: Backup + deploy**

```bash
STAMP=$(date +%Y%m%d%H%M%S)
ssh -i ~/.ssh/id_ed25519 root@165.227.170.3 "cp -a /var/www/auracore-api /var/www/auracore-api.bak-$STAMP && echo backup_$STAMP"
scp -i ~/.ssh/id_ed25519 -r src/Backend/AuraCore.API/publish/* root@165.227.170.3:/var/www/auracore-api/ 2>&1 | tail -3
ssh -i ~/.ssh/id_ed25519 root@165.227.170.3 "chown -R www-data:www-data /var/www/auracore-api && systemctl restart auracore-api && sleep 5 && systemctl is-active auracore-api && curl -fsS http://localhost:5000/health"
```

Expected: `active`, `{"status":"healthy","database":"connected"}`.

- [ ] **Step 4: Smoke — token-less request still authenticates (tolerant mode)**

```bash
curl -X POST https://api.auracore.pro/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"ozgurdeniz807@gmail.com","password":"WRONG"}' \
  -i 2>&1 | head -10
```

Expected: 401 Unauthorized (wrong password) — NOT 400 captcha_required (because tolerant mode).

This confirms the backend is deployed in tolerant mode and existing FE bundles still authenticate.

- [ ] **Step 5: Commit ops marker (no code change, just document)**

```bash
git commit --allow-empty -m "ops(6.12.W8.T14): deploy backend phase 1 — tolerant mode

Backend deployed to /var/www/auracore-api (backup .bak-$STAMP).
TURNSTILE_TOLERANT_MODE=true so old FE bundles without turnstileToken
field continue to authenticate. Phase 2 (T15) deploys FE; Phase 3 (T16)
flips to strict mode."
```

(Replace `$STAMP` with the actual timestamp from Step 3.)

### Task 15: Deploy phase 2 — frontend

**Files:** none (operational task).

- [ ] **Step 1: Build admin-panel**

```bash
cd admin-panel && npm run build 2>&1 | tail -5 && cd ..
```

- [ ] **Step 2: Backup + deploy admin-panel**

```bash
STAMP=$(date +%Y%m%d%H%M%S)
ssh -i ~/.ssh/id_ed25519 root@165.227.170.3 "cp -a /var/www/admin-panel /var/www/admin-panel.bak-$STAMP && echo admin_panel_backup_$STAMP"
scp -i ~/.ssh/id_ed25519 -r admin-panel/out/* root@165.227.170.3:/var/www/admin-panel/ 2>&1 | tail -3
ssh -i ~/.ssh/id_ed25519 root@165.227.170.3 "chown -R deploy:deploy /var/www/admin-panel"
```

- [ ] **Step 3: Backup + deploy landing-page**

```bash
STAMP_LP=$(date +%Y%m%d%H%M%S)
ssh -i ~/.ssh/id_ed25519 root@165.227.170.3 "cp -a /var/www/landing-page /var/www/landing-page.bak-$STAMP_LP && echo landing_page_backup_$STAMP_LP"
scp -i ~/.ssh/id_ed25519 -r ./landing-page-work/* root@165.227.170.3:/var/www/landing-page/ 2>&1 | tail -3
```

- [ ] **Step 4: Smoke — verify widgets render**

```bash
for path in sign-in.html register.html forgot-password.html; do
  curl -s "https://auracore.pro/$path" | grep -q "cf-turnstile" && echo "$path OK" || echo "$path MISSING"
done
curl -s https://admin.auracore.pro/ | grep -q "challenges.cloudflare.com" && echo "admin-panel OK" || echo "admin-panel MISSING"
```

Expected: all four lines say "OK". If any say "MISSING", check the build output and the file contents on origin.

- [ ] **Step 5: Browser smoke (manual)**

Visit each form in a real browser. Confirm:
1. Page loads without console errors.
2. Turnstile widget initializes (visible in browser dev tools Network tab — request to `challenges.cloudflare.com`).
3. Submit button transitions from disabled → enabled after the widget completes.
4. Submitting a form sends the `turnstileToken` field in the request body (visible in Network tab).
5. Backend returns 200 (login success) or appropriate auth-error response, not 400 captcha_required.

- [ ] **Step 6: Commit ops marker**

```bash
git commit --allow-empty -m "ops(6.12.W8.T15): deploy frontend phase 2

admin-panel deployed to /var/www/admin-panel (backup .bak-$STAMP).
landing-page deployed to /var/www/landing-page (backup .bak-$STAMP_LP).
Backend still in tolerant mode (T14). Browser smoke verified: widgets
render, tokens included in requests, end-to-end auth flow works."
```

### Task 16: Deploy phase 3 — backend strict mode

**Files:** none (operational task).

- [ ] **Step 1: Wait for FE cache clear (24-48h ideal)**

The `Cache-Control: no-cache` header on admin-panel HTML should make this near-instant. Landing-page caching depends on nginx + Cloudflare CDN settings. Wait at least 1 hour, ideally 24 hours, before flipping strict mode — or check Cloudflare Cache-Tag analytics.

If the user is impatient and tolerates a brief "users with very-old cached HTML get a 400 captcha_required and have to refresh" UX, the wait can be skipped.

- [ ] **Step 2: Flip TURNSTILE_TOLERANT_MODE=false**

```bash
ssh -i ~/.ssh/id_ed25519 root@165.227.170.3 "sed -i 's/^TURNSTILE_TOLERANT_MODE=.*/TURNSTILE_TOLERANT_MODE=false/' /etc/auracore-api.env && grep TURNSTILE_TOLERANT_MODE /etc/auracore-api.env && systemctl restart auracore-api && sleep 4 && systemctl is-active auracore-api && curl -fsS http://localhost:5000/health"
```

Expected: `TURNSTILE_TOLERANT_MODE=false`, `active`, `healthy`.

- [ ] **Step 3: Smoke — token-less request now rejected**

```bash
curl -X POST https://api.auracore.pro/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"x@y.com","password":"x"}' \
  -i 2>&1 | head -10
```

Expected: 400 Bad Request, body contains `captcha_required`. If still 401, the env var wasn't picked up — check sed ran successfully and systemd loaded the new env.

- [ ] **Step 4: Browser smoke — full auth flow with widget**

Sign in via admin.auracore.pro and via auracore.pro/sign-in.html. Verify both still complete successfully.

- [ ] **Step 5: Commit ops marker**

```bash
git commit --allow-empty -m "ops(6.12.W8.T16): deploy phase 3 — backend strict mode

TURNSTILE_TOLERANT_MODE=false. Token-less requests now rejected with
400 captcha_required. Browser smoke: admin-panel + landing-page sign-in,
register, forgot-password all complete with the widget enforcing the
gate. Phase 6.12 deploy complete."
```

---

## Wave 9 — Closeout

### Task 17: Full-suite regression + push

**Files:** none.

- [ ] **Step 1: Full backend suite**

```bash
cd /c/Users/Admin/Desktop/auracorepro/AuraCorePro
dotnet test AuraCorePro.sln --no-restore 2>&1 | grep -E "^(Passed|Failed)" | head -10
```

Expected per-assembly:
- AuraCore.Tests.API: 203/203
- All other assemblies unchanged from Phase 6.11 close (baseline 2453 - 181 = 2272 + 203 = ~2475 total)

- [ ] **Step 2: Full frontend suite**

```bash
cd admin-panel && npx vitest run 2>&1 | tail -5 && cd ..
```

Expected: 59/59 passing (no new FE tests in Phase 6.12; LoginScreen mock change preserved test count).

- [ ] **Step 3: Push branch**

```bash
git push origin phase-6-12-security-hardening 2>&1 | tail -5
```

### Task 18: Memory file + ceremonial merge

**Files:**
- Create: `C:\Users\Admin\.claude\projects\C--\memory\project_phase_6_item_12_security_hardening_complete.md`
- Modify: `C:\Users\Admin\.claude\projects\C--\memory\MEMORY.md`

- [ ] **Step 1: Draft the memory file**

`C:\Users\Admin\.claude\projects\C--\memory\project_phase_6_item_12_security_hardening_complete.md`:

```markdown
---
name: Phase 6.12 Security Hardening COMPLETE
description: Email XSS encode + tiered superadmin rate limit + BCrypt timing defense + Cloudflare Turnstile CAPTCHA on 4 auth endpoints with circuit-breaker fallback. Merged to main.
type: project
---

# Phase 6.12 — Security Hardening COMPLETE

**Branch:** `phase-6-12-security-hardening` merged to main at `<MERGE_COMMIT>`.
**Test counts:** Backend 203 API (+20 from 183 Phase 6.11 close) / 59 FE unchanged.
**Deploy:** 3-phase rollout completed at `<TIMESTAMP>`. Backups `.bak-<STAMP>`.

## Items shipped

- 6.12.A IEmailService XSS — `WebUtility.HtmlEncode` in `ApplyPlaceholders`. +4 regression tests.
- 6.12.B Tiered rate limit on /api/auth/superadmin/login — 3 fails/email/60min + 10 fails/IP/60min + 30 fails/IP/24h. +4 regression tests.
- 6.12.C BCrypt timing defense — precomputed dummy hash, BCrypt.Verify always runs. Applied to /superadmin/login (T7) and IAuthService.LoginAsync (T8). +2 regression tests.
- 6.12.D Cloudflare Turnstile CAPTCHA on 4 endpoints (/login, /superadmin/login, /register, /forgot-password) with Polly circuit-breaker fail-open fallback (5-fail / 60s break). +10 regression tests across TurnstileVerifier, CaptchaEnforcement, CaptchaCircuitBreaker (if implemented).

## Env vars added

- `TURNSTILE_SECRET_KEY` — server-only, /etc/auracore-api.env
- `TURNSTILE_TOLERANT_MODE` — rollout flag, set false post-deploy
- `CAPTCHA_ENABLED` — emergency disable, default true
- `NEXT_PUBLIC_TURNSTILE_SITE_KEY` — admin-panel build-time

## Out-of-scope carry-forward

(Same items from Phase 6.11 plus what 6.12 deferred — see spec at
docs/superpowers/specs/2026-04-24-phase-6-12-security-hardening-design.md
"Future work" section.)
```

Replace `<MERGE_COMMIT>`, `<TIMESTAMP>`, `<STAMP>` with actuals after Step 3.

- [ ] **Step 2: Update MEMORY.md index**

Open `C:\Users\Admin\.claude\projects\C--\memory\MEMORY.md`. Replace the current top entry (Phase 6.11 line) with:

```
- [Phase 6.12 Security Hardening COMPLETE](project_phase_6_item_12_security_hardening_complete.md) — **CURRENT STATE.** Branch merged to main at <MERGE_COMMIT>. Email XSS + tiered superadmin rate limit + BCrypt timing defense + Turnstile CAPTCHA on 4 endpoints. 203 API + 59 FE tests.
```

Mark the previous Phase 6.11 line as superseded.

- [ ] **Step 3: Merge + push**

User-gated step. Get explicit approval before running:

```bash
git checkout main
git pull origin main
git merge --no-ff phase-6-12-security-hardening -m "Merge branch 'phase-6-12-security-hardening' (Phase 6.12 Security Hardening)

Email XSS encode + tiered superadmin rate limit + BCrypt timing defense
+ Cloudflare Turnstile CAPTCHA on four auth endpoints with Polly circuit-
breaker fail-open fallback. 18 commits across 9 waves. See memory file
project_phase_6_item_12_security_hardening_complete.md for scope, test
numbers, and out-of-scope carry-forward."
git push origin main
```

- [ ] **Step 4: Final smoke after merge**

```bash
curl -fsS https://api.auracore.pro/health
curl -sI https://admin.auracore.pro/ | head -3
```

Expected: both 200. Phase 6.12 shipped.

---

## Self-Review

After completing the plan, the following checks were applied:

**Spec coverage:** Every item in `docs/superpowers/specs/2026-04-24-phase-6-12-security-hardening-design.md` maps to a task: D6 → T5, D7 → T6, D8 → T7+T8, D3+D4+D5+D9 → T2+T3+T4 (infra) + T9-T11 (wiring) + T12+T13 (FE). Locked design decisions D1+D2 are scope-only (no tasks). Tolerant-mode rollout (T14-T16). Memory file + merge (T17-T18).

**Placeholder scan:** No "TBD", "TODO", or "implement later" tokens. The few `<MERGE_COMMIT>` / `<STAMP>` placeholders in T18 are operational fill-ins, not code placeholders. The CF Site Key + Secret Key placeholders (`PUT_REAL_SITE_KEY_HERE`, `PASTE_REAL_SECRET_KEY_HERE`) are explicit user-action substitutions called out in the prerequisite section.

**Type consistency:** `ICaptchaVerifier.VerifyAsync(token, remoteIp, ct)` signature consistent across T2 (interface), T3 (implementation), T1 (test stub), T10/T11 (controller calls). DTO field name `TurnstileToken` consistent across T9 (DTOs), T10/T11 (controller reads), T12/T13 (FE writes). Env var names `TURNSTILE_SECRET_KEY`, `TURNSTILE_TOLERANT_MODE`, `CAPTCHA_ENABLED`, `NEXT_PUBLIC_TURNSTILE_SITE_KEY` consistent across T3/T4/T10/T12/T14/T16.

**Ambiguity check:** "Tolerant mode" lifecycle is explicit (T14 sets true, T16 flips false). BCrypt work factor determination is procedurally specified (T7 Step 1). Test fixture migration scope is explicit (T1 Step 4-7 lists exact files). One residual ambiguity: the spec leaves "remove the tolerant flag entirely after rollout, OR keep as escape hatch" as a writing-plans decision — this plan keeps the flag as an emergency escape hatch (alongside `CAPTCHA_ENABLED`) and does NOT remove it post-rollout.

No issues to fix inline.
