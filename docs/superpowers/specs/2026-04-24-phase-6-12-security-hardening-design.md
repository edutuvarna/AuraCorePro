# Phase 6.12 — Security Hardening — Design

**Date:** 2026-04-24
**Author:** Brainstormed in collaborative dialogue (Phase 6.11 closeout session)
**Status:** Draft, awaiting user review before plan generation
**Successor to:** [Phase 6.11 Superadmin Foundation (merged at `fb47778`)](../../.../memory/project_phase_6_item_11_superadmin_foundation_complete.md)

## Goal

Close the four open security gaps surfaced in Phase 6.11 verification + add a Cloudflare Turnstile CAPTCHA layer to the four highest-value auth endpoints, while keeping the deploy ritual reversible and the test suite expanded with regression guards.

## Non-goals

- **No deep audit re-run** — Phase 6.13 will own a full post-6.11 audit sweep across 12 admin tabs + new controllers.
- **No QR rendering on Enable2FAPage** — deferred to Phase 6.13 (per user direction during Phase 6.11 post-deploy).
- **No new authentication primitives** (no passwordless, no WebAuthn, no SSO) — those are out of scope.
- **No replacement of the BCrypt password hashing scheme** — the dummy-hash defense complements existing BCrypt, doesn't replace it.

## Scope

Four atomic security items + supporting infrastructure:

| ID | Item | Effort |
|---|---|---|
| **6.12.A** | IEmailService `ApplyPlaceholders` XSS — HTML-encode placeholder values | XS |
| **6.12.B** | Tiered rate limit on `/api/auth/superadmin/login` | S |
| **6.12.C** | BCrypt timing-attack defense (dummy hash) on superadmin login + regular login | S |
| **6.12.D** | Cloudflare Turnstile CAPTCHA on 4 auth endpoints with circuit-breaker fallback | L |

Total estimated effort: 4-6 days for a solo dev. Item D dominates.

## Locked design decisions

These were resolved during the brainstorming dialogue:

- **D1: Phase theme.** Security-first hardening pass. Other 6.11 carry-forward (UX polish, feature expansion, observability) deferred to 6.13+.
- **D2: Scope inclusion.** Three confirmed-open items (6.12.A/B/C) plus CAPTCHA (6.12.D). Full-audit re-run rejected — defer to 6.13.
- **D3: CAPTCHA provider.** Cloudflare Turnstile. Self-hosted premium stack (Altcha + FingerprintJS + Spamhaus/AbuseIPDB + behavioral) recorded as future work for Phase 6.14+.
- **D4: CAPTCHA placement.** Four endpoints — `/api/auth/login`, `/api/auth/superadmin/login`, `/api/auth/register`, `/api/auth/forgot-password`. `/api/auth/redeem-invitation` excluded (token-gated, 32-char random hash, brute-force infeasible).
- **D5: CAPTCHA trigger mode.** Always-on with Turnstile in Managed mode (CF auto-decides invisible vs challenge). No progressive trigger; uniform across all four endpoints.
- **D6: XSS fix technique.** Apply `WebUtility.HtmlEncode(...)` uniformly to every placeholder value in `ResendEmailService.ApplyPlaceholders`. Razor-style templating engine (Scriban / Fluid / Razor) deferred to Phase 6.13+ as future work.
- **D7: Rate limit threshold.** Tiered three-layer counter on superadmin login: existing 3 fails per email per 60 min + new 10 fails per IP per 60 min + new 30 fails per IP per 24 h. Whitelisted IPs bypass all three layers (existing pattern).
- **D8: BCrypt timing defense.** Precomputed dummy hash; always run `BCrypt.Net.BCrypt.Verify(...)` regardless of user existence. Apply to both `AuthController.SuperadminLogin` and `IAuthService.LoginAsync`.
- **D9: Turnstile-down fallback.** Fail-open with circuit breaker. Five consecutive verifier failures within a sliding window → 60-second bypass mode → re-probe → recover. Implemented via Polly (`Microsoft.Extensions.Http.Resilience`).

## Architecture

### Backend components (`src/Backend/AuraCore.API`)

#### 1. `ResendEmailService.ApplyPlaceholders` — XSS encode

File: `src/Backend/AuraCore.API.Infrastructure/Services/Email/ResendEmailService.cs`, line ~80.

Two-line change inside the existing private method:

```csharp
private static string ApplyPlaceholders(string template, object data)
{
    var props = data.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
    foreach (var p in props)
    {
        var raw = p.GetValue(data)?.ToString() ?? "";
        var encoded = System.Net.WebUtility.HtmlEncode(raw);
        template = template.Replace("{{" + p.Name + "}}", encoded);
    }
    return template;
}
```

Edge case: existing placeholders are all plain text (admin email, permission key, reason text, review note). No template currently relies on a placeholder containing raw HTML, so no double-encoding regressions expected. Future placeholders that need raw HTML (e.g., a clickable button) must be inserted as static template content, not as a placeholder.

#### 2. `AuthController.SuperadminLogin` — tiered rate limit

File: `src/Backend/AuraCore.API/Controllers/AuthController.cs`, around line 285-290 (existing email-only block).

Replace the single `CountAsync` with three sequential checks, all gated behind `whitelisted == false`:

```csharp
var whitelisted = await _whitelist.IsWhitelistedAsync(ip, ct);
if (!whitelisted)
{
    var now = DateTimeOffset.UtcNow;

    // Layer 1 — email scope, 60 min (existing behavior).
    var emailFails = await _db.LoginAttempts.CountAsync(a =>
        a.Email == email && !a.Success && a.CreatedAt > now.AddMinutes(-60), ct);
    if (emailFails >= 3)
        return StatusCode(429, new { error = "Too many failed attempts for this email. Try again in 60 minutes." });

    // Layer 2 — IP scope, 60 min (new).
    var ipFailsShort = await _db.LoginAttempts.CountAsync(a =>
        a.IpAddress == ip && !a.Success && a.CreatedAt > now.AddMinutes(-60), ct);
    if (ipFailsShort >= 10)
        return StatusCode(429, new { error = "Too many failed attempts from this IP. Try again in 60 minutes." });

    // Layer 3 — IP scope, 24 h (new) — slow-drip distributed attack guard.
    var ipFailsLong = await _db.LoginAttempts.CountAsync(a =>
        a.IpAddress == ip && !a.Success && a.CreatedAt > now.AddHours(-24), ct);
    if (ipFailsLong >= 30)
        return StatusCode(429, new { error = "Too many failed attempts from this IP today. Try again later." });
}
```

Three EF queries per superadmin login attempt; superadmin login traffic is low (single-digit attempts/day in normal ops), so cost is negligible. Indices on `login_attempts(IpAddress, CreatedAt)` and `login_attempts(Email, CreatedAt)` are required — verify they exist or add them in the EF migration generated by the implementation plan.

#### 3. `AuthController.SuperadminLogin` + `IAuthService.LoginAsync` — dummy-hash defense

Files:
- `src/Backend/AuraCore.API/Controllers/AuthController.cs` (`SuperadminLogin`, around line 300)
- `src/Backend/AuraCore.API.Infrastructure/Services/Auth/AuthService.cs` (`LoginAsync` — implementation of `IAuthService`)

Pattern (apply to both call sites):

```csharp
// One-time at class init — module-static field.
private static readonly string _dummyHash =
    BCrypt.Net.BCrypt.HashPassword("dummy-password-never-matches-anything", workFactor: 12);

// Inside the method:
var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
var hashToVerify = user?.PasswordHash ?? _dummyHash;
var passwordValid = BCrypt.Net.BCrypt.Verify(request.Password, hashToVerify);

if (user is null || !passwordValid /* || other-condition for SuperadminLogin */)
{
    await LogAttempt(success: false);
    return Unauthorized(new { error = "Invalid credentials" });
}
```

The `_dummyHash` work factor must match the production hashing work factor (`12` is the BCrypt.Net default). If `IAuthService.LoginAsync` already implements this pattern (verify during implementation), 6.12.C for the regular login is a no-op.

#### 4. `ICaptchaVerifier` interface + `TurnstileVerifier` implementation

New files:
- `src/Backend/AuraCore.API.Application/Services/Security/ICaptchaVerifier.cs`
- `src/Backend/AuraCore.API.Infrastructure/Services/Security/TurnstileVerifier.cs`

Interface:

```csharp
namespace AuraCore.API.Application.Services.Security;

public interface ICaptchaVerifier
{
    Task<bool> VerifyAsync(string token, string remoteIp, CancellationToken ct = default);
}
```

Implementation calls `https://challenges.cloudflare.com/turnstile/v0/siteverify` via `IHttpClientFactory`. Form-data fields: `secret` (from env), `response` (the token), `remoteip` (best-effort client IP). Response JSON's `success` field is the boolean returned.

Polly resilience pipeline wraps the HttpClient. Configuration in `Program.cs`:

```csharp
builder.Services.AddHttpClient<ICaptchaVerifier, TurnstileVerifier>(c =>
{
    c.BaseAddress = new Uri("https://challenges.cloudflare.com/");
    c.Timeout = TimeSpan.FromSeconds(5);
})
.AddResilienceHandler("captcha", b =>
{
    b.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
    {
        FailureRatio = 1.0,                       // any failure counts
        MinimumThroughput = 5,                    // require 5 attempts before tripping
        SamplingDuration = TimeSpan.FromMinutes(1),
        BreakDuration = TimeSpan.FromSeconds(60),
    });
});
```

When the circuit is open, `TurnstileVerifier.VerifyAsync` catches `BrokenCircuitException` and returns `true` (fail-open) plus emits a `Warning`-level log line. When the breaker re-probes after 60 s and the next request succeeds, normal mode resumes automatically (Polly state machine, no manual reset).

#### 5. CAPTCHA wiring on four auth endpoints

Files:
- `src/Backend/AuraCore.API/Controllers/AuthController.cs` (`Login`, `SuperadminLogin`, `Register`)
- `src/Backend/AuraCore.API/Controllers/Auth/PasswordResetController.cs` (`ForgotPassword`)

DTOs gain a `TurnstileToken` string property. Each endpoint, as the very first action after the basic empty-input check:

```csharp
if (string.IsNullOrEmpty(request.TurnstileToken))
    return BadRequest(new { error = "captcha_required" });

var captchaOk = await _captcha.VerifyAsync(request.TurnstileToken, ip, ct);
if (!captchaOk)
    return BadRequest(new { error = "captcha_invalid" });
```

In tolerant mode (rollout window), the `IsNullOrEmpty` branch returns `true` instead of `BadRequest` — controlled by an env var `TURNSTILE_TOLERANT_MODE` read once at app startup.

### Frontend components

#### admin-panel (React, Next.js)

- New npm dep: `@marsidev/react-turnstile@^0.4`.
- File: `admin-panel/src/components/LoginScreen.tsx` — wrap submit with `<Turnstile siteKey={process.env.NEXT_PUBLIC_TURNSTILE_SITE_KEY!} onSuccess={setToken} />`. Submit button disabled until `token` is set. Both `submit('admin')` and `submit('superadmin')` paths include the token.
- Env var: `admin-panel/.env.production` gains `NEXT_PUBLIC_TURNSTILE_SITE_KEY=0x...`.

#### landing-page (vanilla HTML, deployed via scp from local mirror)

Three files (`sign-in.html`, `register.html`, `forgot-password.html`) gain:

```html
<head>
  <!-- ... existing ... -->
  <script src="https://challenges.cloudflare.com/turnstile/v0/api.js" async defer></script>
</head>

<form id="signin-form" method="post" action="...">
  <!-- existing fields -->
  <div class="cf-turnstile" data-sitekey="0xYOUR_SITE_KEY" data-callback="onTurnstileToken"></div>
  <input type="hidden" id="cf-turnstile-response" name="turnstileToken">
  <button type="submit" id="submit-btn" disabled>Sign in</button>
</form>

<script>
  function onTurnstileToken(t) {
    document.getElementById('cf-turnstile-response').value = t;
    document.getElementById('submit-btn').disabled = false;
  }
</script>
```

The site key is hardcoded in the HTML attribute — landing-page is plain HTML, no env-var step. Update the site key in three files when you rotate.

### Data flow (example: consumer login from landing-page sign-in.html)

1. Browser loads `sign-in.html`. Cloudflare's `api.js` initializes the Turnstile widget (rendered as an invisible overlay in Managed mode).
2. User fills email + password.
3. Cloudflare's widget produces a one-shot token and calls the page's `onTurnstileToken(t)` callback, which fills the hidden `turnstileToken` input and enables the submit button.
4. Form submits to `https://api.auracore.pro/api/auth/login` with body `{ email, password, turnstileToken }`.
5. Backend `AuthController.Login`:
   - Reads `TurnstileToken` from request DTO.
   - Calls `_captcha.VerifyAsync(token, ip, ct)`. Polly pipeline wraps the HTTP call to `challenges.cloudflare.com/turnstile/v0/siteverify`.
   - On success → proceeds to existing rate limit + BCrypt.Verify + 2FA flow.
   - On verifier failure (false response) → returns `400 { "error": "captcha_invalid" }`.
   - On verifier exception (CF unreachable) → Polly's circuit breaker tracks; if breaker is open, `VerifyAsync` returns `true` (fail-open) with a warning log.
6. Successful auth → JWT returned as before.

## Error handling

| Condition | HTTP | Response body | Client behavior |
|---|---|---|---|
| `TurnstileToken` field missing | 400 | `{"error":"captcha_required"}` | FE: button stays disabled until widget ready; on this error, refresh page. |
| Token format invalid or expired (>300 s old, already-used) | 400 | `{"error":"captcha_invalid"}` | FE: widget auto-resets, user retries. |
| CF API success=false | 400 | `{"error":"captcha_invalid"}` | Same as above. |
| CF API unreachable / timeout | Logged as warning; `VerifyAsync` returns `true` (fail-open via Polly) | n/a | Auth proceeds with rate limit + BCrypt dummy + 2FA defenses still active. |
| Polly circuit breaker open | Auth proceeds; warning log on each bypass | n/a | After 60 s break duration, Polly half-opens and re-probes the next request. |
| `TURNSTILE_TOLERANT_MODE=true` env (rollout window) | Token-less requests proceed without verify | n/a | Backwards-compatible window for old FE bundles. |

Emergency disable: set `CAPTCHA_ENABLED=false` env var → `ICaptchaVerifier` no-op returns `true`. Use only if Turnstile causes a prod incident.

## Testing strategy

Backend test count grows from 183 → ~198-200 (+15-17 tests).

### Unit + integration tests

| File (new) | Count | Purpose |
|---|---:|---|
| `EmailServiceXssTests.cs` | 4 | `ApplyPlaceholders` encodes script payload, all 5 reserved chars, idempotent for plain text, all 6 templates render without exception |
| `LoginRateLimitTieredTests.cs` | 4 | Email cap, IP-short cap (10/60min), IP-long cap (30/24h), whitelisted-IP bypass |
| `LoginTimingDefenseTests.cs` | 2 | Nonexistent-email vs wrong-password response time delta < 50 ms statistical bound, separately for `/superadmin/login` and `/login` |
| `TurnstileVerifierTests.cs` | 4 | CF success=true → returns true; success=false → returns false; network failure → throws; correct form-data shape |
| `CaptchaEnforcementTests.cs` | 5 | All four endpoints: missing-token 400, invalid-token 400, valid-token proceeds; substituted-mock infrastructure |
| `CaptchaCircuitBreakerTests.cs` | 3 | Opens after 5 consecutive failures, re-probes and recovers after 60 s, bypass mode logs warning |

Existing test fixtures that currently use `IClassFixture<WebApplicationFactory<Program>>` and hit auth endpoints (`SuperadminLoginEndpointTests`, `TwoFactorEnforcementTests`, `LoginSuspendedAccountTests`, `AdminInvitationFlowTests`, etc.) need either a shared `TestWebAppFactory` base helper or per-fixture `s.AddScoped<ICaptchaVerifier, AlwaysAllowCaptchaVerifier>()` registration so existing tests continue passing without producing real Turnstile traffic.

### Frontend tests

- `admin-panel/src/__tests__/components/LoginScreen.captcha.test.tsx` — submit disabled until token, token included in API call body, widget reset on auth failure.
- `landing-page` has no automated test framework; manual smoke check is the only gate. Spec ops checklist enforces this.

### Test reliability concerns

- **Timing tests** are inherently flaky on CI due to runner load variance. Mitigation: 5-sample average, 50 ms threshold (allowing > 30 ms of natural BCrypt jitter), `[Trait("Category", "Timing")]` to allow filtering.
- **Circuit breaker tests** require fresh `IServiceProvider` per test to isolate Polly state. Use `IClassFixture` with disposable factory.
- **24 h rate limit test** must not depend on real wall-clock. Inject `TimeProvider` (xUnit's `FakeTimeProvider`) into the rate-limit query.

## Operational steps

### One-time CF dashboard setup (user-driven, before code lands)

1. `dash.cloudflare.com` → Turnstile (sidebar).
2. **Add site:** name `AuraCorePro Auth`. Domains: `auracore.pro`, `admin.auracore.pro` (or wildcard `*.auracore.pro`).
3. Widget mode: **Managed** (CF decides invisible vs challenge per-request).
4. Save → record the **Site Key** (public) and **Secret Key** (server-only).

### Environment variable plumbing

| Var | Type | Location | Notes |
|---|---|---|---|
| `TURNSTILE_SECRET_KEY` | Server-only | `/etc/auracore-api.env` | New. `TurnstileVerifier` reads via `Environment.GetEnvironmentVariable`. |
| `NEXT_PUBLIC_TURNSTILE_SITE_KEY` | Public | `admin-panel/.env.production` | New. `next build` inlines at compile time. |
| `TURNSTILE_TOLERANT_MODE` | Server-only | `/etc/auracore-api.env` | New, transient. `true` during rollout window, `false` after FE deploy completes. |
| `CAPTCHA_ENABLED` | Server-only | `/etc/auracore-api.env` | New, emergency disable. Default `true`. |
| (landing-page site key) | Hardcoded HTML | `data-sitekey="0x..."` attribute | Vanilla HTML, no env var. |

### Three-phase rollout

**Phase 1 — Backend tolerant mode**
- Deploy backend with `TURNSTILE_TOLERANT_MODE=true`.
- Old FE bundles (no token in request) authenticate normally; new FE bundles work too.
- Smoke: existing login flows unchanged, `/health` green.

**Phase 2 — Frontend deploy**
- `npm run build` admin-panel + scp to `/var/www/admin-panel/`.
- Edit landing-page's three HTML files (Turnstile script + div + hidden input + JS callback), backup origin, scp.
- Smoke: each page now renders the Turnstile widget; submit produces a token visible in browser dev-tools network tab.

**Phase 3 — Backend strict mode**
- Set `TURNSTILE_TOLERANT_MODE=false` in `/etc/auracore-api.env`.
- `systemctl restart auracore-api`.
- Smoke: token-less curl returns `400 captcha_required`; token-bearing browser flow works.

Phase 1 to Phase 3 ideal interval: 24-48 hours. This window covers Cloudflare CDN cache TTL plus browser cache TTL for old admin-panel HTML/JS bundles. Shorter windows are safe if you've verified `Cache-Control: no-cache` on the admin-panel HTML entry point.

### Rollback path

| Failure | Action |
|---|---|
| Backend regression | `cp -a /var/www/auracore-api.bak-YYYYMMDDHHMMSS/* /var/www/auracore-api/ && systemctl restart auracore-api` |
| FE admin-panel regression | scp from local backup → `/var/www/admin-panel/` |
| FE landing-page regression | scp from origin's `.bak-YYYYMMDDHHMMSS` → `/var/www/landing-page/` |
| CF Turnstile extended outage (circuit breaker insufficient) | `TURNSTILE_TOLERANT_MODE=true` + `systemctl restart` |
| Total CAPTCHA disable (incident escape hatch) | `CAPTCHA_ENABLED=false` + `systemctl restart` |
| DB | Zero schema changes — no migration rollback needed. |

### Landing-page deploy ritual (scp from local mirror)

```bash
# 1. Sync local mirror from origin (Phase 6.5 ritual)
scp -i ~/.ssh/id_ed25519 -r root@165.227.170.3:/var/www/landing-page/* ./landing-page-work/

# 2. Edit sign-in.html + register.html + forgot-password.html
#    — add CF api.js script in <head>
#    — add <div class="cf-turnstile" data-sitekey="0x..." data-callback="onTurnstileToken">
#    — add <input type="hidden" name="turnstileToken">
#    — add <script>function onTurnstileToken(t) { ... }</script>

# 3. Backup origin
STAMP=$(date +%Y%m%d%H%M%S)
ssh -i ~/.ssh/id_ed25519 root@165.227.170.3 "cp -a /var/www/landing-page /var/www/landing-page.bak-$STAMP"

# 4. Deploy
scp -i ~/.ssh/id_ed25519 -r ./landing-page-work/* root@165.227.170.3:/var/www/landing-page/

# 5. Smoke
for path in sign-in.html register.html forgot-password.html; do
  curl -s "https://auracore.pro/$path" | grep -q "cf-turnstile" && echo "$path OK" || echo "$path MISSING"
done
```

### Post-deploy E2E smoke checklist

1. **admin.auracore.pro** sign-in: form loads, invisible widget initializes, valid creds + 2FA flow completes.
2. **admin.auracore.pro** Sign In as Superadmin: widget initializes on same page, superadmin path completes.
3. **api.auracore.pro** token-less curl: `curl -X POST .../api/auth/login -d '{"email":"x@y.com","password":"x"}'` returns `400 captcha_required` (after Phase 3).
4. **auracore.pro/sign-in.html**: vanilla widget renders, form submit completes.
5. **auracore.pro/register.html**: same.
6. **auracore.pro/forgot-password.html**: form submit triggers Resend email and the email arrives at the user's inbox.
7. **Circuit breaker controlled test** (local-only): patch `TurnstileVerifier` to throw 5 times in a row → 6th call enters bypass mode → log shows `"Turnstile circuit open, bypass active"` warn.

## Future work (deferred from this phase)

Items recorded as carry-forward during brainstorming, not implemented in 6.12:

- **Phase 6.13:** Self-hosted CAPTCHA premium stack (Altcha PoW + FingerprintJS + Spamhaus DROP + AbuseIPDB free tier + behavioral signals + progressive challenge). Migration target if AuraCorePro wants to remove the CF dependency from the auth path while keeping ~75-80 % of Turnstile's effectiveness.
- **Phase 6.13:** Razor-style templating engine for `IEmailService` (Scriban / Fluid / Razor) — auto-escape by default, replaces hand-rolled `ApplyPlaceholders` string replace, enables conditionals and partials.
- **Phase 6.13:** Full post-Phase-6.11 audit sweep — re-audit 12 admin tabs + new superadmin controllers + new permission system for any regression or new finding introduced by Phase 6.11's surface area.
- **Phase 6.13:** New Relic observability integration (free Pro plan via GitHub Student Pack). APM, distributed tracing, error tracking, alerting. Backend wiring + dashboard setup.
- **Phase 6.14+:** Astra Security managed bug bounty (free for students). Activate ahead of public launch / commercial release.
- **Phase 6.14+:** Doppler secrets management (free Team plan via GitHub Student Pack). Migrate `/etc/auracore-api.env` to Doppler with audit + rotation reminders.
- **Phase 6.13 polish queue carried from 6.11:** Duplicate toast fix (React 18 StrictMode `useEffect` de-dup in `PermissionNotificationsProvider`), QR rendering on `Enable2FAPage`, `RoleChangePage` permission gate restoration, scope-limited FE nav-lock, native `<select>` dark-theme styling, TOTP backup codes.

## Known risks

- **CF Turnstile site key domain binding.** Both `auracore.pro` and `admin.auracore.pro` must be listed in the dashboard, or use a wildcard `*.auracore.pro`. Misconfigured key produces silent verification failures.
- **Browser strict-tracking-protection rejection.** Tor Browser, Firefox ETP-strict, and similar block the CF widget script. Affected users (<1 % of traffic) cannot log in until they switch browsers. Workaround documented in user-facing error message: "If the verification doesn't load, try a different browser." Not a blocker for AuraCorePro's threat profile.
- **JWT vs Turnstile token confusion.** Code review must clearly distinguish `accessToken` (long-lived JWT, 15 min for scope-limited / 30 days for refresh) from `turnstileToken` (one-shot 300 s CF token, single-use per submission). Variable names and comments must reflect this.
- **Resend free-tier quota (100 emails/day).** CAPTCHA on register and forgot-password directly protects this quota from bot abuse. But genuine user load also counts; if AuraCorePro grows past ~50 daily registrations + password resets, paid Resend tier becomes necessary. Monitor in 6.13 once New Relic is in place.
- **Polly state isolation across xUnit runs.** Polly's `ResiliencePipelineRegistry` is process-scoped. CI test parallelism could leak circuit-breaker state between fixtures unless each fixture uses a fresh `IServiceProvider`.
- **Timing-defense statistical assertion flakiness.** CI runner load variance can produce false positives. Mitigation: 50 ms threshold + 5-sample average + `[Trait("Category","Timing")]` for retry-on-flake tagging.

## Self-review notes

- **Placeholders:** none; all sections complete.
- **Internal consistency check:** D7 (rate limit thresholds 3/email + 10/IP + 30/IP) consistent with the Tiered Rate Limit code block (3 sequential `CountAsync`). D8 (BCrypt timing defense, both endpoints) consistent with section "AuthController.SuperadminLogin + IAuthService.LoginAsync". D9 (5 consecutive failures, 60 s break) consistent with the Polly options block (`MinimumThroughput = 5`, `BreakDuration = 60s`).
- **Scope check:** Single-implementation-plan scope. No further decomposition needed.
- **Ambiguity check:** "Tolerant mode" rollout flag is a transient env var, only used for ~24-48 h between Phase 1 and Phase 3 deploy windows; the implementation plan must include "remove the flag" as a Phase 4 task once the rollout is stable, OR keep it permanently as the emergency escape hatch (decision deferred to writing-plans skill).

## Out-of-spec, decided-yet-unrecorded

- Both `/api/auth/login` (admin button in admin-panel) and `/api/auth/login` (consumer login in landing-page sign-in.html) hit the same backend endpoint. The CAPTCHA verification is identical. The Turnstile widget setup differs only in the FE tech (React vs vanilla).
- The admin panel `LoginScreen.tsx` covers BOTH `/api/auth/login` (admin button → role=admin auth) AND `/api/auth/superadmin/login` (superadmin button) on the same page; one Turnstile widget instance covers both buttons.
