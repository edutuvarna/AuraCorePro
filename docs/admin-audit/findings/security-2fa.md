# Security Tab Audit Findings

**Tab:** Security
**URL path:** `https://admin.auracore.pro/` (SPA, sidebar nav → "Security")
**Audit date:** 2026-04-22
**Auditor:** subagent-12 (Task 12 — FINAL per-tab audit)
**Source files audited:**
- Frontend TSX: `/root/admin-panel/src/app/page.tsx` lines 1331–1430 (`SecurityPage`)
- Frontend API client: `/root/admin-panel/src/lib/api.ts` lines 122–160
- Backend controller (local repo): `src/Backend/AuraCore.API/Controllers/TotpController.cs` (142 lines)
- Backend service (local repo): `src/Backend/AuraCore.API.Infrastructure/Services/TotpService.cs` (122 lines)
- Auth controller (login 2FA path): `src/Backend/AuraCore.API/Controllers/AuthController.cs` lines 140–166
- Auth service (token generation): `src/Backend/AuraCore.API.Infrastructure/Services/AuthService.cs` lines 60–94
- DB entity: `src/Backend/AuraCore.API.Domain/Entities/User.cs`
- DbContext config: `src/Backend/AuraCore.API.Infrastructure/Data/AuraCoreDbContext.cs` lines 33–34
- Deployed chunk: `/var/www/admin-panel/_next/static/chunks/app/page-9bf9edb4333e55cf.js` (April 21 03:42 UTC)
**Live test URL:** `https://admin.auracore.pro/` → Security sidebar item
**Time spent:** ~2.5 hours

## Scope verification

Security tab contains **only** QR-code 2FA activation, as stated. No other features found. The `SecurityPage` component (lines 1331–1430) renders exactly: status badge, Enable 2FA / Verify+Enable / Disable 2FA flow, and a message area. No unrelated features.

## Summary

- **2 critical** — (1) 2FA brute-force at `/api/auth/login` bypasses all rate limiting (login attempt logged as Success=true when password correct + 2FA wrong → never counted as failure). (2) Deployment drift: source rewrite broke the QR code display — when next deploy runs, 2FA enrollment will be completely non-functional.
- **1 high** — TOTP secret stored plaintext in `users.totp_secret` column with no encryption at rest; any DB compromise exposes all 2FA secrets, enabling 2FA bypass for all users.
- **2 medium** — (1) `/api/2fa/validate` is `[AllowAnonymous]` and leaks two bits of information: valid email existence (NotFound vs BadRequest) and whether that email has 2FA enabled. (2) Static rate-limit `Dictionary<string, ...>` is not thread-safe (no lock) and resets on app restart.
- **2 low** — (1) No backup codes or recovery flow — admin loses authenticator → permanent lockout, no self-service escape. (2) No warning about same-device enrollment (enrolling from the same browser/laptop used to log in defeats the second factor).
- **1 info** — TOTP code comparison uses string `==` (not constant-time `CryptographicOperations.FixedTimeEquals`); theoretical timing oracle.
- **CTP-2:** TOTP setup/verify/disable actions not audit-logged anywhere (no audit_log table exists per prior audits).
- **CTP-12:** Confirmed — deployed bundle and source have diverged on the Security tab (see F-1 / F-7).
- **Bug 3:** Not applicable — SecurityPage has no Refresh button. Token restore from localStorage confirmed working on browser refresh.

Axes covered: functional, code+DB sync, security, UX, mobile, drift.

---

## Controller location

`src/Backend/AuraCore.API/Controllers/TotpController.cs` — route prefix `[Route("api/2fa")]`, class-level `[Authorize]`.

Note: `TotpController` is NOT in the `/Controllers/Admin/` folder. It has `[Authorize]` (any authenticated user), not `[Authorize(Roles = "admin")]`. This is intentional — 2FA is a self-service user endpoint that operates on the caller's own account. All 12 Admin folder controllers use `[Authorize(Roles = "admin")]`.

---

## Findings

### F-1 [CRITICAL] Source rewrite broke QR enrollment — next deploy will kill 2FA setup entirely (CTP-12)

**Axis:** drift + functional
**Symptom:** 2FA setup works today (deployed bundle), but the next `npm run build && deploy` will leave the QR code blank and the manual entry key invisible — admin cannot enroll any authenticator app.

**Root cause:**

Deployed bundle (`page-9bf9edb4333e55cf.js`, built April 21): reads `a.qrUri` from the API response and passes it to an `eD` QRCode canvas component (loaded from dynamic chunk 819, which is the `qrcode` npm package).

Source TSX (`/root/admin-panel/src/app/page.tsx` line 1396–1397):
```tsx
{setupData.qrCodeDataUrl && <img src={setupData.qrCodeDataUrl} ... />}
{setupData.manualEntryKey && <code>...</code>}
```

Backend response (`TotpController.cs:44–49`):
```csharp
return Ok(new { secret, qrUri, message = "..." });
```

| Field | Backend returns | Deployed reads | Source expects |
|---|---|---|---|
| QR display | `qrUri` (otpauth:// string) | `a.qrUri` → canvas render | `setupData.qrCodeDataUrl` (never defined) → `<img>` not rendered |
| Manual key | `secret` (base32 string) | `a.secret` → inline code | `setupData.manualEntryKey` (never defined) → not rendered |

Additionally, `qrcode` npm package was removed from `package.json`; any rebuild would fail to ship the QR renderer.

**Reproduction (current state — works):** Admin → Security → Enable 2FA → QR appears in canvas → scan → enter 6-digit code → verified.

**Reproduction (after next rebuild):** Same flow → QR area blank → manual key area blank → cannot enroll.

**Expected:** QR code and manual entry key both visible after clicking Enable 2FA.
**Actual (post-rebuild):** No QR, no manual key; the `<img>` tag has `src={undefined}` (conditional never renders).

**Fix suggestion:**
- Option A (frontend fix): Revert source to use `setupData.qrUri` for the canvas-based QR renderer, restore `qrcode` to `package.json`. Restore `setupData.secret` for manual key display.
- Option B (backend fix): Add server-side QR PNG generation (e.g. `QRCoder` NuGet) and return `qrCodeDataUrl` as a base64 `data:image/png;base64,...` — then source is correct as-is.

**Risk if unfixed:** Next deploy disables 2FA enrollment permanently until manually patched.

---

### F-2 [CRITICAL] 2FA brute-force at `/api/auth/login` bypasses all rate limiting

**Axis:** security
**Symptom:** An attacker who knows an admin's password can try all 1,000,000 possible 6-digit TOTP codes without triggering any lockout, because the login rate limiter counts only `Success=false` attempts but the 2FA-fail path logs `Success=true`.

**Root cause:**

`AuthController.Login` flow:
1. `_auth.LoginAsync(email, password)` → if password correct → `AuthResult { Success = true, AccessToken, RefreshToken }`
2. Log attempt: `{ Success = result.Success }` → **`Success = true`** saved to `login_attempts`
3. Check `TotpEnabled` → if TOTP code wrong → return `401 Invalid 2FA code`

Rate limit check (lines 111–123) counts `!a.Success` rows. Since the 2FA-fail attempt is logged as `Success = true`, it never counts toward the 3-IP or 5-email lockout threshold.

The `/api/2fa/validate` endpoint has its own in-memory rate limit (5 attempts per 15 min), but the login endpoint's built-in 2FA path has none.

**Reproduction:**
```bash
# With correct password, iterate all TOTP codes — no lockout
for code in $(seq -w 0 999999); do
  curl -s -X POST https://api.auracore.pro/api/auth/login \
    -H 'Content-Type: application/json' \
    -d "{\"email\":\"admin@auracore.pro\",\"password\":\"CORRECT_PW\",\"totpCode\":\"$code\"}" &
done
```

**Expected:** After N failed 2FA attempts, account locked or 429 returned.
**Actual:** Unlimited 2FA code attempts; 300 seconds covers all 1M codes with parallelism.

**Additional finding:** Each correct-password attempt also creates an orphaned `RefreshToken` row in DB (AuthService lines 83–88: token generated and saved before the 2FA check in `AuthController`). Not directly exploitable (token not returned), but causes unbounded DB growth under attack.

**Fix suggestion:**
- Log TOTP fail attempts separately (or log `Success=false` when TOTP validation fails even if password was right).
- Apply the existing email/IP rate limit at the TOTP validation step, not just at the password step.
- On 2FA challenge path: do not generate/persist the RefreshToken until after TOTP is also verified.

**Risk if unfixed:** An attacker with a stolen admin password can bypass 2FA in under 10 minutes with parallelized requests; 2FA provides no real protection.

---

### F-3 [HIGH] TOTP secret stored plaintext in database — DB compromise = 2FA bypass for all users

**Axis:** security + code-db-sync
**Symptom:** Anyone with read access to `auracoredb` (DB backup, SQL injection, etc.) can extract all TOTP secrets and generate valid 2FA codes for any user who has enabled 2FA.

**Root cause:**

`User.cs` line 9: `public string? TotpSecret { get; set; }` — no encryption attribute.

`AuraCoreDbContext.cs` line 33: `e.Property(u => u.TotpSecret).HasMaxLength(64)` — no value converter, no column encryption.

`TotpController.Setup` line 36–37: `var secret = TotpService.GenerateSecret(); user.TotpSecret = secret;` — raw base32 string stored.

DB schema: `totp_secret character varying(64)` — plaintext column.

No `IDataProtector` (ASP.NET Core Data Protection), no AES wrapper, no column-level encryption in `TotpService` or `AuthService`.

**DB verification query:**
```sql
-- Would reveal all secrets if any users had TotpEnabled=true
SELECT id, email, totp_secret, totp_enabled FROM users WHERE totp_enabled = true;
-- Currently 0 rows (all 6 users have TotpEnabled=false — no active 2FA to expose today)
```

**Expected:** `totp_secret` encrypted at rest (e.g., via ASP.NET Core Data Protection `IDataProtector.Protect()`).
**Actual:** Raw base32 TOTP seed stored in plaintext.

**Fix suggestion:** Wrap `TotpService.GenerateSecret()` result with `IDataProtector.Protect()` before saving; wrap `TotpService.ValidateCode()` call with `IDataProtector.Unprotect()` before validation. Key stored in key ring (separate from DB).

**Risk if unfixed:** DB compromise (backup leak, SQL injection, insider) exposes 2FA secrets — the second factor becomes worthless retroactively.

---

### F-4 [MEDIUM] `/api/2fa/validate` is AllowAnonymous and leaks email existence + 2FA enrollment status

**Axis:** security
**Symptom:** Any unauthenticated internet user can call `POST /api/2fa/validate` to determine (a) whether an email address is registered, and (b) whether that registered user has 2FA enabled.

**Root cause:**

`TotpController.Validate` (line 74): `[AllowAnonymous]`

Response distinguishes three states:
- Email not found: `404 { error: "User not found" }`
- Email found, 2FA disabled: `400 { error: "2FA not enabled for this user" }`
- Email found, 2FA enabled, wrong code: `401 { error: "Invalid 2FA code" }`

**Reproduction:**
```bash
# Enumerate email existence
curl -X POST https://api.auracore.pro/api/2fa/validate \
  -H 'Content-Type: application/json' \
  -d '{"email":"admin@auracore.pro","code":"000000"}'
# 400 → email exists, no 2FA
# 401 → email exists, 2FA active (guessed code wrong)
# 404 → email not registered
```

**Expected:** Consistent response regardless of whether email exists; requires authentication or at minimum indistinguishable error codes.
**Actual:** Three distinct HTTP status codes reveal account enumeration and 2FA enrollment state.

**Fix suggestion:** Collapse NotFound and BadRequest into the same 401 response for the AllowAnonymous endpoint. Or require authentication (`[Authorize]`) for this endpoint — the admin panel always has a JWT when calling it.

**Risk if unfixed:** Targeted phishing (attacker knows which admins have 2FA); recon for the F-2 brute-force attack.

---

### F-5 [MEDIUM] In-memory rate-limit dictionary is not thread-safe and resets on restart

**Axis:** security + code-db-sync
**Symptom:** Under concurrent requests the `_totpAttempts` dictionary can corrupt (race condition), silently under-count failed attempts, or panic. App restart (deploy, crash) wipes all attempt counters — an attacker can simply wait for a restart to reset their lockout.

**Root cause:**

`TotpController.cs` line 16:
```csharp
private static readonly Dictionary<string, (int Count, DateTime ResetAt)> _totpAttempts = new();
```

No `lock` statement or `ConcurrentDictionary` anywhere in the file. The `Validate` endpoint reads and writes this dictionary in an async method — two concurrent requests for the same email can race on `_totpAttempts[email] = ...`.

App restart: `Dictionary` is in-process memory; ephemeral containers / k8s deployments / IIS recycles / crashes all reset counters to zero.

**Fix suggestion:** Replace with `ConcurrentDictionary<string, (int, DateTime)>` for thread safety; persist attempt counts to the `login_attempts` DB table (which already exists and survives restarts) instead of in-memory.

**Risk if unfixed:** Rate limiting on `/api/2fa/validate` is less reliable than it appears; parallelized attack reduces effective window.

---

### F-6 [LOW] No backup codes or recovery flow — authenticator loss = permanent admin lockout

**Axis:** functional + ux
**Symptom:** If the admin loses access to their authenticator app (phone theft, factory reset, app uninstall), there is no self-service recovery path. The only escape is manual DB intervention.

**Root cause:**

No backup codes generated at enrollment (`TotpController.Verify` line 66–68: only sets `TotpEnabled=true`, no backup codes table).
No recovery endpoint in `TotpController` or any other controller.
No email-based recovery link.
No admin-override endpoint (e.g., `POST /api/admin/users/{id}/disable-2fa`) that a support agent could use.

**DB verification:**
```sql
-- No backup_codes table exists
SELECT table_name FROM information_schema.tables
WHERE table_schema='public' AND table_name LIKE '%backup%';
-- 0 rows
```

**Expected:** 8–10 single-use backup codes generated and displayed once at enrollment.
**Actual:** No backup codes, no recovery flow. Lockout requires direct `UPDATE users SET totp_enabled=false, totp_secret=null WHERE email='...'` on the DB.

**Fix suggestion:** Generate 8 random backup codes at enrollment (`TotpController.Verify`); hash and store in a `backup_codes` table; display once (plaintext, one-time reveal). Accept backup code as an alternative to TOTP at login.

**Risk if unfixed:** Admin loses authenticator → cannot log into admin panel → requires DB access to unlock → operational outage.

---

### F-7 [LOW] No same-device enrollment warning

**Axis:** ux + security
**Symptom:** Admin can enroll TOTP using an authenticator app (e.g., browser-based or desktop app) on the same device and same browser session used to log in, defeating the "second factor" entirely.

**Root cause:** `SecurityPage` (lines 1331–1430): no warning text about using a separate physical device. The instructional text only says "You will need an authenticator app like Google Authenticator or Authy."

**Expected:** UI warning: "Use a separate device (e.g., your phone) to scan this QR code. Using the same device as your login reduces the security benefit."
**Actual:** No warning.

**Fix suggestion:** Add a one-sentence advisory below the QR code.

**Risk if unfixed:** Admin enrolls TOTP on the same laptop; if laptop is stolen, both factors are compromised simultaneously.

---

### F-8 [INFO] TOTP code comparison uses string == (not constant-time)

**Axis:** security
**Symptom:** Theoretical timing side-channel on TOTP code comparison.

**Root cause:**

`TotpService.ValidateCode` line 47: `if (expected == code) return true;`

`string.==` in .NET uses short-circuit comparison (stops at first differing character). An attacker could theoretically time API responses to extract partial information about the expected TOTP code. In practice, network jitter makes this impractical for a 6-digit code with 30s validity, but it is a deviation from cryptographic best practice.

**Fix suggestion:** Replace with `CryptographicOperations.FixedTimeEquals(System.Text.Encoding.ASCII.GetBytes(expected), System.Text.Encoding.ASCII.GetBytes(code))` — available in `System.Security.Cryptography` since .NET Core 2.1.

**Risk if unfixed:** Negligible in practice; flagged for completeness.

---

## Axis summary

### 1. Functional
- Setup → Verify → Enable flow: logically correct on backend; BROKEN in source (F-1 drift), works in deployed.
- Disable flow: requires current TOTP code — correct.
- Status endpoint: returns `{ enabled: bool }` — correct.
- Empty state: when `status` is null (initial load), no spinner shown — minor (status loads quickly from `useEffect`).

### 2. Code + DB sync
- `TotpEnabled` and `TotpSecret` are on a single `users` row — no dual-source-of-truth issue.
- After `verify`: `TotpEnabled=true` written, UI updates `status` via `setStatus({ enabled: true })` — correct local state update.
- After `disable`: `TotpEnabled=false`, `TotpSecret=null` — correct.
- No stale-after-mutation issue; SecurityPage uses local state mutation, not refetch.

### 3. Security
- F-2: 2FA brute-force bypass (critical).
- F-3: Plaintext secret at rest (high).
- F-4: AllowAnonymous validate + user enumeration (medium).
- F-5: Non-thread-safe rate limiter (medium).
- F-8: Non-constant-time comparison (info).
- CTP-2: No audit log for 2FA setup/enable/disable events (confirmed — no audit_log table exists per Task 9 findings).
- `[Authorize]` (not `[Roles="admin"]`) on TotpController is correct — self-service endpoint, not admin-only action.

### 4. UX
- Loading spinner present on all three actions (Enable/Verify/Disable) via `loading` state.
- Error messages displayed via `msg` state.
- Success messages displayed and state transitions cleanly.
- No Refresh button (Bug 3 not applicable to this tab).
- No confirmation dialog before Disable — disabling 2FA is a destructive security action but there is no "Are you sure?" (low-priority: not data-destructive, easily re-enabled).
- F-6: No backup codes.
- F-7: No same-device warning.

### 5. Mobile responsiveness
Security tab renders a single `max-w-xl glass-card` — inherently mobile-friendly. CTP-3 (table overflow) does not apply (no table). The card is constrained width, not full-width overflow. QR image is `w-48 h-48` — fits on 320px screen. Input is `w-full`. Buttons are `w-full` on verify/disable. No horizontal scroll risk identified.

### 6. Deployment drift
- F-1 (critical): Source SecurityPage rewrote QR display from `qrUri+canvas` to `qrCodeDataUrl+img` without updating backend or keeping `qrcode` npm package. Next rebuild breaks 2FA setup entirely.
- Source last committed: `/root/admin-panel` source is newer than deployed bundle (deployed April 21, source contains changes beyond that).
- Deployed chunk still works because it reads `qrUri` (backend field) correctly.

---

## CTP carry-forward status

| CTP | Status on Security tab |
|---|---|
| CTP-2 (no audit log) | Confirmed — TOTP events not logged anywhere |
| CTP-6 (rollback stripped) | Not applicable — TotpController is fully implemented, 4 endpoints intact |
| CTP-12 (API contract drift) | Confirmed — F-1 details source/deployed divergence on `qrCodeDataUrl` vs `qrUri` |
| Bug 3 (refresh data-loss) | Not applicable — no Refresh button in SecurityPage; localStorage token restore working |

---

## TOTP implementation quality assessment

The custom `TotpService` implementation (RFC 6238, HMAC-SHA1, 6-digit, 30s step, ±1 window) is architecturally correct:
- Secret generation: `RandomNumberGenerator.Fill(bytes)` (20 bytes = 160 bits) — strong entropy.
- Base32 encoding: correct RFC 4648 alphabet.
- Dynamic truncation: correct RFC 4226 §5.4 implementation.
- Clock window: ±1 step (±30s) — industry standard.
- No external TOTP NuGet needed — implementation is clean.

The quality issues are at the storage (F-3) and login-flow (F-2) layers, not the TOTP math itself.
