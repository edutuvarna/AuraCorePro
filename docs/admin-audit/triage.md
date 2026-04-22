# Admin Panel Audit — Triage for Fix Phase

**Audit commit:** `23c46b1` (HEAD of `phase-6-admin-audit` at consolidation)
**Tabs audited:** 12
**Total findings:** 96 (+ 6 informational)
**Breakdown:** 20 Critical / 27 High / 30 Medium / 19 Low
**Cross-tab patterns:** 13 (CTP-1 through CTP-13)

---

## Executive Summary

The 12-tab deep audit revealed three dominant failure categories. First, the April 2026 security rollback (B-4) silently gutted multiple backend controllers: `AdminLicenseController` was stripped from 121 lines to a 26-line Create-only stub, `AdminDeviceController` lost `GetById` and `Delete`, `CryptoController` lost `AdminRejectPayment`, and `AdminChartController` was removed entirely — all confirmed via DLL string inspection. These rollback artifacts make four tabs partially or completely non-functional in production today.

Second, systematic migration drift: `__EFMigrationsHistory` has zero rows, meaning every EF-declared index and schema change since the raw-DDL bootstrap has never reached production. This means `ip_whitelists` table does not exist (IP Whitelist tab returns 500 on every request), `Platform`/`GitHubReleaseId` columns are absent from `app_updates` (Updates tab upload flow broken end-to-end), and performance indexes on `crash_reports`, `telemetry_events`, `login_attempts`, `payments`, and `devices` are all missing.

Third, zero audit logging: no `admin_audit_log` table exists anywhere in the database. Every admin mutation — grant subscription, delete user, revoke license, approve crypto payment, change config, publish update — happens with no record of who did what or when. The "Audit Log" sidebar tab is mislabeled and only shows login attempts.

Additional critical findings include a 2FA brute-force bypass (correct password + unlimited TOTP guesses, no lockout), an empty-password acceptance in `ResetPassword`, and the entire whitelist enforcement layer having been silently removed during a backend refactor.

---

## Tier 0 — Critical (fix IMMEDIATELY, before any other work)

Data-integrity / security / revenue-loss bugs. Fix-first candidates.

| ID | Tab | Title | Severity | Est fix effort |
|---|---|---|---|---|
| T0.1 | Security (2FA) | 2FA brute-force bypass — login logs TOTP fail as Success=true, bypasses rate limit | Critical | M |
| T0.2 | Users | ResetPassword accepts empty string — any user can be locked out | Critical | S |
| T0.3 | IP Whitelist | Whitelist enforcement removed — whitelisted IPs have zero effect at rate limit | Critical | M |
| T0.4 | Payments | Stripe webhook NullReferenceException on missing Signature header (500 + stack trace leak) | Critical | S |
| T0.5 | Payments | Missing idempotency check in webhook — Stripe retries create duplicate payment records + double-license credits | Critical | M |
| T0.6 | Payments | Crypto Reject endpoint missing from DLL — rejected payments stuck in `confirming` forever | Critical | S |
| T0.7 | Audit Log | No `admin_audit_log` table — zero traceability of any admin action across all tabs | Critical | L |
| T0.8 | IP Whitelist | `ip_whitelists` table does not exist — entire tab returns 500 on every interaction | Critical | S |
| T0.9 | Licenses | Entire Licenses tab non-functional — GET returns 405, Revoke/Activate return 404 | Critical | M |
| T0.10 | Licenses | Create license returns 500 — no user-existence check, unhandled FK exception | Critical | S |
| T0.11 | Configuration | Maintenance middleware hits DB on every request with silent fail-open on DB error | Critical | M |
| T0.12 | Audit Log | Route + key mismatch — Audit Log tab always shows empty (419 rows exist) | Critical | S |
| T0.13 | IP Whitelist | 4-way API contract drift — all CRUD ops silently fail even if table existed | Critical | M |
| T0.14 | Security (2FA) | Source rewrite broke QR enrollment — next deploy kills 2FA setup permanently | Critical | S |
| T0.15 | Subscriptions | Tier badge always shows "free" — all 4 pro users display as free | Critical | S |
| T0.16 | Payments | No AdminPaymentController — Payments tab uses Dashboard endpoints, no pagination beyond 50 | Critical | S |
| T0.17 | Devices | KPI stats all show 0 — stats field name mismatch (totalDevices vs total, etc.) | Critical | S |
| T0.18 | Devices | Pagination permanently broken — `pages` field absent from Devices GetAll response | Critical | S |
| T0.19 | Crash Reports | Pagination permanently broken — `pages` field absent from Crash Reports response | Critical | S |
| T0.20 | Updates | 6.6.E backend never deployed — PrepareUpload (405), RetryGitHubMirror (404) | Critical | L |

**T0.1 detail — 2FA brute-force:** `AuthController.Login` logs the LoginAttempt as `Success=result.Success` (password-check result) BEFORE the TOTP check at line 129–135. With a correct password + wrong TOTP, attempt is logged as `Success=true`. Rate limiter counts only `!Success` rows — unlimited TOTP guesses possible. Fix: move LoginAttempt write to after TOTP validation; do not persist RefreshToken until after TOTP is verified.

**T0.2 detail — empty password:** `AdminUserController.ResetPassword` accepts `newPassword=""`, BCrypt-hashes it, saves. Confirmed live: `prouser99@mailnull.com` password was set to `""` during audit. Fix: add `[MinLength(8)]` guard.

**T0.3 detail — whitelist enforcement removed:** `AuthController.cs` in local repo has no `IsWhitelisted` call; the backup's enforcement at lines 77–85 was dropped when switching from flat-file `WhitelistService` to EF-backed approach. Whitelisted IPs are stored but never consulted at login.

**T0.5 detail — webhook idempotency:** `HandleCheckoutCompleted` calls `_db.Payments.Add(...)` unconditionally. Backup had `AnyAsync(p => p.ExternalId == session.Id && p.Status == "completed")` guard at lines 183–184 — stripped by rollback. `ExternalId` unique index also absent from DB (CTP-9). Stripe retries every non-2xx response for up to 3 days.

**T0.7 detail — no audit table:** SQL confirmed — `SELECT table_name ... WHERE table_name IN ('audit_log','admin_audit_log',...)` returns 0 rows. Every admin mutation across all 12 tabs is completely untracked. Requires new table, service injection, and middleware.

---

## Tier 1 — High (Wave 1 of fix phase)

Feature broken / data displayed wrong / sync gap. User-visible but not catastrophic.

| ID | Tab | Title | Severity | Est fix effort |
|---|---|---|---|---|
| T1.1 | Subscriptions | `subscriptions` table entirely unused — Grant writes to `licenses` not `subscriptions` | High | S |
| T1.2 | Subscriptions | Nginx basic auth does not protect API endpoints — bypass via api.auracore.pro with JWT | High | S |
| T1.3 | Subscriptions | Admin actions not recorded in any audit log (CTP-2 Subscriptions instance) | High | L |
| T1.4 | Users | Tier badge always shows "free" on Users tab — downstream of CTP-1 | High | S |
| T1.5 | Users | Delete cascade silently orphans CrashReports + TelemetryEvents (EF Core tracked-entity bug CTP-5) | High | S |
| T1.6 | Users | Action buttons below 44px tap target — Revoke/Delete/GUID all ~28px tall | High | S |
| T1.7 | Licenses | Deployed controller is stripped stub — GET/Revoke/Activate endpoints missing (CTP-6) | High | M |
| T1.8 | Licenses | `activeDevices` vs `DeviceCount` field name mismatch — device count always shows 0 | High | S |
| T1.9 | Licenses | Revoke sets Status=revoked but leaves Tier=pro — inconsistent with Subscriptions Revoke | High | S |
| T1.10 | Payments | Revenue chart API 404 — AdminChartController missing from DLL | High | M |
| T1.11 | Payments | Hardcoded `$` currency — TRY/EUR payments appear as `$X.XX` | High | S |
| T1.12 | Payments | HandleCheckoutCompleted hardcodes Currency="USD" — multi-currency payments stored wrong | High | S |
| T1.13 | Devices | GetById and Delete stripped from DLL (CTP-6) — admin cannot view detail or delete device | High | M |
| T1.14 | Devices | HardwareFingerprint returned in every GetAll response — unnecessary privacy exposure | High | S |
| T1.15 | Devices | crashCount/telemetryCount columns always show 0 — fields missing from GetAll projection | High | S |
| T1.16 | Updates | `AddPlatformToAppUpdate` migration not applied — Platform/GitHubReleaseId columns absent | High | M |
| T1.17 | Crash Reports | Stats KPI mismatch — 3 of 4 KPI cards always show 0 (last24h vs today, etc.) | High | S |
| T1.18 | Crash Reports | Delete has no confirmation (CTP-4 instance) | High | S |
| T1.19 | Crash Reports | Version filter silently broken — `?version=` sent, `?appVersion=` read by backend | High | S |
| T1.20 | Telemetry | No rate limit or batch-size cap on client telemetry POST — unbounded DB write amplification | High | M |
| T1.21 | Telemetry | Stats KPI mismatch — Total Events and Today always show 0 (CTP-11) | High | S |
| T1.22 | Telemetry | Pagination broken — `pages` field absent from telemetry GetAll response (CTP-10) | High | S |
| T1.23 | Audit Log | Stats KPI all show 0 — shape mismatch (successful24h vs last24h, etc.) | High | S |
| T1.24 | IP Whitelist | No IP format validation — any string accepted as IP address | High | S |
| T1.25 | Configuration | 4 of 5 toggles are cosmetic — NewRegistrations/Telemetry/CrashReports/AutoUpdate have zero enforcement | High | M |
| T1.26 | Configuration | No singleton protection at DB level — second AppConfig row allowed | High | S |
| T1.27 | Security (2FA) | TOTP secret stored plaintext in database — DB compromise = 2FA bypass for all users | High | M |

---

## Tier 2 — Medium (Wave 2 of fix phase)

UX pain / missing validation / error-handling gaps.

| ID | Tab | Title | Severity | Est fix effort |
|---|---|---|---|---|
| T2.1 | Subscriptions | Days field has no validation — negative values/zero accepted (immediately-expired licenses) | Medium | S |
| T2.2 | Subscriptions | No confirmation dialog on Grant — accidental grant, no undo path | Medium | S |
| T2.3 | Subscriptions | Revoke button practically unreachable due to F-1 cascade (post-polish, was Critical) | Medium | S |
| T2.4 | Subscriptions | Toast color determined by `!` in message string — fragile error styling | Medium | S |
| T2.5 | Users | No "Showing X–Y of N" pagination label | Medium | S |
| T2.6 | Users | No search debounce — every keystroke fires API call | Medium | S |
| T2.7 | Users | No role-change UI despite visible Role column | Medium | M |
| T2.8 | Licenses | No input validation on Create — negative MaxDevices, arbitrary Tier strings | Medium | S |
| T2.9 | Licenses | No confirmation on Revoke/Activate (CTP-4) | Medium | S |
| T2.10 | Licenses | License mutations not logged (CTP-2 Licenses instance) | Medium | L |
| T2.11 | Payments | No confirmation on crypto Approve/Reject (CTP-4) | Medium | S |
| T2.12 | Payments | No audit log for payment mutations (CTP-2 Payments instance) | Medium | L |
| T2.13 | Payments | ExternalId non-unique index — DB-level duplicate payment records allowed | Medium | S |
| T2.14 | Payments | Floating-point double division `/ 100.0` on invoice amounts (post-polish, was High) | Medium | S |
| T2.15 | Devices | No audit log for device deletions (CTP-2 Devices instance) | Medium | L |
| T2.16 | Devices | No UI mechanism to delete devices — `deleteDevice()` is dead code in api.ts | Medium | S |
| T2.17 | Updates | Delete confirmation shows "(undefined)" for platform field | Medium | S |
| T2.18 | Updates | No frontend validation when all platforms unchecked before Publish | Medium | S |
| T2.19 | Crash Reports | Schema drift — DB has `Message` column + wrong varchar lengths vs EF config | Medium | S |
| T2.20 | Crash Reports | `CreatedAt` index absent from crash_reports (CTP-9) | Medium | S |
| T2.21 | Crash Reports | No audit log for Delete (CTP-2 Crash Reports instance) | Medium | L |
| T2.22 | Telemetry | CTP-9 — `CreatedAt` and `EventType` indexes absent from telemetry_events | Medium | S |
| T2.23 | Audit Log | Composite indexes on login_attempts missing from prod (CTP-9) | Medium | S |
| T2.24 | Audit Log | Unbounded login_attempts growth — no retention/purge policy | Medium | M |
| T2.25 | IP Whitelist | `my-ip` self-whitelisting endpoint missing from deployed DLL | Medium | S |
| T2.26 | IP Whitelist | No confirmation on IP delete (CTP-4) | Medium | S |
| T2.27 | Configuration | No confirmation before any toggle — IsMaintenanceMode is a platform-outage footgun | Medium | S |
| T2.28 | Configuration | MaintenanceMessage stored as unbounded text — no server-side length limit | Medium | S |
| T2.29 | Security (2FA) | `/api/2fa/validate` is AllowAnonymous — leaks email existence + 2FA enrollment status | Medium | S |
| T2.30 | Security (2FA) | In-memory rate-limit dictionary not thread-safe, resets on app restart | Medium | S |

---

## Tier 3 — Low (defer or include if Wave 2 has capacity)

Code smells / cosmetic / minor UX.

| ID | Tab | Title | Severity | Est fix effort |
|---|---|---|---|---|
| T3.1 | Subscriptions | Deployment drift — `/root/admin-panel/out/` is 26 days stale vs live | Low | S |
| T3.2 | Subscriptions | admin@auracore.pro has license.Tier='free' with ExpiresAt=2126 — anomalous data | Low | S |
| T3.3 | Users | Deployment drift — source 26 days stale, GUID column in live not in source | Low | S |
| T3.4 | Users | No confirmation dialog on Revoke (CTP-4 Users instance) | Low | S |
| T3.5 | Licenses | Deployment drift — 26 lines local vs 121 lines backup (CTP-6 evidence) | Low | S |
| T3.6 | Licenses | License key format is 32-char hex — not user-friendly `AC-XXXX-XXXX-XXXX` format | Low | S |
| T3.7 | Payments | StatusBadge doesn't handle `awaiting_payment`, `confirming`, `disputed` statuses | Low | S |
| T3.8 | Payments | No blockchain explorer link for crypto TX hash | Low | S |
| T3.9 | Devices | No "online/offline" status indicator — Last Seen only shows date | Low | S |
| T3.10 | Devices | No max-devices-exceeded warning for over-quota licenses | Low | S |
| T3.11 | Updates | No audit log for Publish/Delete/RetryGitHubMirror (CTP-2 Updates instance) | Low | L |
| T3.12 | Updates | R2 and GitHub token env vars absent from /etc/auracore-api.env | Low | S |
| T3.13 | Crash Reports | `stackTracePreview` truncation removed from list response (backup had it) | Low | S |
| T3.14 | Telemetry | Telemetry table missing `overflow-x-auto` — horizontal overflow on narrow viewports | Low | S |
| T3.15 | Audit Log | PII (email + IP) in login_attempts with no right-to-erasure mechanism | Low | M |
| T3.16 | Configuration | No audit log for config mutations (CTP-2 Config instance) | Low | L |
| T3.17 | Configuration | CORS origins use `auracorepro.com` — stale domain (production is `auracore.pro`) | Low | S |
| T3.18 | Security (2FA) | No backup codes or recovery flow — authenticator loss = permanent admin lockout | Low | M |
| T3.19 | Security (2FA) | No same-device enrollment warning — same-device TOTP defeats second factor | Low | S |

---

## Cross-tab patterns (fix ONCE, applies to many tabs)

### CTP-1: TSX reads `u.tier` but API sends `u.license.tier`

**Description:** `AdminUserController.GetAll` returns `{ ..., license: { tier, expiresAt } }`. UsersPage reads `u.tier` (undefined) → all tier badges show "free". Subscriptions tab Revoke button visibility also affected.

**Tabs affected:** Subscriptions (F-1), Users (F-1)

**Single fix location:** `AdminUserController.cs:38–46` — add `tier = license?.Tier ?? "free"` to `GetAll` projection (matches `GetById` behavior at line 63).

**Effort estimate:** Small (1-line change in one controller method)

---

### CTP-2: Missing audit log for all admin mutations

**Description:** No `admin_audit_log` table exists. All admin mutations (grant, revoke, delete, configure, publish update) happen with zero traceability. The "Audit Log" tab only shows `login_attempts` — not admin actions.

**Tabs affected:** All 12 tabs — Subscriptions, Users, Licenses, Payments, Devices, Updates, Crash Reports, Telemetry, Audit Log, IP Whitelist, Configuration, Security

**Single fix location:** New `admin_audit_log` table + `IAuditWriter` service + DI registration + wire-in to each Admin controller mutation endpoint.

**Effort estimate:** Large (new table migration + service + 12 controller touch-points)

---

### CTP-3: No mobile responsive layout

**Description:** Root layout `page.tsx:1460` is `flex h-screen overflow-hidden` with no breakpoints. Sidebar fixed at 260px with no hamburger toggle. At ≤375px, content area is ≤115px — completely unusable.

**Tabs affected:** All 12 tabs

**Single fix location:** Root layout in `page.tsx` (sidebar + main container responsive classes). Affects all tabs with one change.

**Effort estimate:** Medium (sidebar hamburger toggle + breakpoint CSS; must test all 12 tab layouts)

---

### CTP-4: Inconsistent destructive confirmation

**Description:** Some destructive actions have `confirm()` dialogs, others don't. Users Delete has one; Users Revoke doesn't. Licenses Revoke/Activate have none. Crash Reports Delete has none. Configuration toggles have none.

**Tabs affected:** Subscriptions (F-7), Users (F-9 = T3.4), Licenses (F-7), Payments (F-8), Crash Reports (F-3), IP Whitelist (F-6), Configuration (F-4)

**Single fix location:** Standardize on a shared `ConfirmModal` component; replace all `onClick` immediate-fire handlers with a guarded pattern. Centralized fix in a new component + 7 call-sites.

**Effort estimate:** Medium (new component + 7 touch-points)

---

### CTP-5: EF Core tracked-entity cascade bug — RemoveRange before ID collection

**Description:** `AdminUserController.cs:106–118`: devices added to EF change tracker as Deleted via `RemoveRange`, then subsequent `Where()` query on same `_db` context excludes them. `deviceIds` is always empty → CrashReports and TelemetryEvents silently orphaned on user delete.

**Tabs affected:** Users (F-3)

**Single fix location:** `AdminUserController.cs:105-118` — collect `deviceIds` BEFORE calling `RemoveRange`. 3-line reorder.

**Effort estimate:** Small

---

### CTP-6: Security rollback stripped controller endpoints

**Description:** The April 2026 security rollback gutted multiple backend controllers. Confirmed stripped: `AdminLicenseController` (121→26 lines), `AdminDeviceController` (90→67 lines, 2 endpoints removed), `CryptoController` (163→145 lines, `AdminRejectPayment` removed), `AdminChartController` (73 lines, entire controller missing), `StripeController` (408→276 lines, idempotency check + event handlers removed).

**Tabs affected:** Licenses (F-1/F-3), Devices (F-3), Payments (F-1/F-3/F-4/F-5)

**Fix locations:** Restore from backup at `/root/auracore-src-backup-final-202604122153/AuraCore.API/Controllers/Admin/`. Rebuild + redeploy backend DLL after each restore. Fix API contract mismatches between backup shapes and current frontend expectations.

**Effort estimate:** Large (5 controllers, each needs restore + contract validation + redeploy test)

---

### CTP-7: Webhook idempotency — payment handlers missing ExternalId dedup check

**Description:** `StripeController.HandleCheckoutCompleted` and `HandleInvoicePaid` call `_db.Payments.Add(...)` unconditionally. Stripe guaranteed-delivery retries create duplicate payment records. Backup had the guard at lines 183–184.

**Tabs affected:** Payments (F-5)

**Single fix location:** `StripeController.cs:HandleCheckoutCompleted` and `HandleInvoicePaid` — restore `AnyAsync(ExternalId == sessionId)` check from backup. Also add `e.HasIndex(p => p.ExternalId).IsUnique()` in DbContext.

**Effort estimate:** Small (2 method insertions + 1 EF config line + migration)

---

### CTP-8: Frontend hardcoded `$` currency symbol

**Description:** `page.tsx:669` — `${ (p.amount ?? 0).toFixed(2) }` — hardcoded `$` prefix. Backend returns `currency` field (USD/TRY/EUR/BTC/USDT); frontend never reads it.

**Tabs affected:** Payments (F-7), Dashboard recent payments panel

**Single fix location:** `page.tsx:669` — replace with `Intl.NumberFormat` currency-aware format. 1–3 lines.

**Effort estimate:** Small

---

### CTP-9: EF migration gap — declared indexes absent from production DB

**Description:** `__EFMigrationsHistory` has 0 rows. All EF-declared indexes (`HasIndex(...)`) across all tables are absent from the production PostgreSQL database. Tables bootstrapped via raw DDL; `dotnet ef database update` was never run.

**Tabs affected:** Payments (ExternalId), Devices (LicenseId+HardwareFingerprint composite), Updates (Version+Channel+Platform composite), Crash Reports (CreatedAt), Telemetry (CreatedAt, EventType), Audit Log (Email+CreatedAt, IpAddress+CreatedAt), IP Whitelist (IpAddress unique — table itself missing)

**Single fix location:** Apply EF migrations: `dotnet ef database update` from API project root, OR manually CREATE INDEX per table using the SQL from each finding.

**Effort estimate:** Small per table (SQL commands); coordinating with CTP-6 DLL redeploy is the complexity.

---

### CTP-10: Pagination shape mismatch — `pages` field absent from backend responses

**Description:** Multiple backends return `{total, page, pageSize, items}` but omit the `pages` computed field. The `<Pagination>` component in `page.tsx:276` returns `null` when `pages <= 1`. Admin can only see first 50 records of any tab with this bug.

**Tabs affected:** Devices (F-2), Crash Reports (F-1), Telemetry (F-4), Audit Log (part of F-2)

**Single fix location (per controller):** Add `pages = (int)Math.Ceiling(total / (double)pageSize)` to each affected `GetAll`/`List` return. 4 controllers, 1 line each.

**Effort estimate:** Small

---

### CTP-11: Stats KPI shape mismatch — field names diverge between backend and frontend

**Description:** Multiple stats endpoints return field names that don't match what the frontend reads. Pattern: backend returns `last24h`, frontend reads `today`; backend returns `total`, frontend reads `totalEvents`. Result: KPI dashboard cards all show 0.

**Tabs affected:** Devices (F-1 — `activeLastDay` vs `activeToday`), Crash Reports (F-2 — `last24h` vs `today`), Telemetry (F-1 — `total` vs `totalEvents`, `last24h` vs `today`), Audit Log (F-3 — all 4 KPI fields mismatched)

**Single fix approach:** Option A — rename backend fields to match frontend expectations (1 line per field, 4 controllers). Option B — update frontend reads. Option A preferred: frontend is the stable contract; backends align to it.

**Effort estimate:** Small (4 controllers, 2–4 field renames each)

---

### CTP-12: API contract drift — backend refactor without coordinated frontend rebuild

**Description:** When backend controllers were refactored (route rename, field name changes, endpoint removals), the deployed frontend bundle (built March 26) was never rebuilt. Result: route, field names, response shape, and endpoint set all diverge.

**Tabs affected:** IP Whitelist (F-2 — full 4-way drift: route + POST body + GET shape + DELETE key), Audit Log (F-2 — route `audit-log` vs `audit/login-attempts` + key `items` vs `attempts`), Security/2FA (F-1 — `qrUri` vs `qrCodeDataUrl`)

**Fix approach:** Establish a coordinated deploy policy: backend route/shape changes must trigger a frontend rebuild. Immediate fix: rebuild admin-panel frontend (`npm run build && deploy`) after all backend fixes are stabilized.

**Effort estimate:** Medium (requires all backend API contracts to stabilize before a single rebuild; the rebuild itself is S)

---

### CTP-13: 2FA brute-force bypass via login-attempt success-log gap

**Description:** `AuthController.Login` logs `Success=result.Success` (password check) BEFORE the TOTP validation step. A correct password + wrong TOTP is logged as `Success=true`. Rate limiter counts only `!Success` rows → unlimited TOTP guesses at the login endpoint. Additionally, `RefreshToken` is persisted before TOTP is verified → orphaned tokens accumulate on every attack attempt.

**Tabs affected:** Security/2FA (F-2)

**Single fix location:** `AuthController.cs:129–135` — move `LoginAttempt` write to after TOTP validation (log `Success=false` when TOTP fails). `AuthService.cs:83–88` — do not `SaveChanges` the RefreshToken until TOTP is also verified.

**Effort estimate:** Small–Medium (logic change in critical auth path; requires careful regression testing)

---

## Independent vs blocked findings

### Independent (can fix in parallel)

The following groups are fully independent of each other and can be worked in parallel:

- **Security group (T0.1, T0.2, CTP-13):** Auth/2FA fixes touch `AuthController` and `TotpController` only. No DB migration required (except logging 2FA fail as `Success=false`).
- **CTP-1 tier display:** Single 1-line change in `AdminUserController.GetAll`. No migration, no other controllers affected.
- **CTP-10 pagination:** Add `pages` field to 4 GetAll responses. Each controller is independent.
- **CTP-11 stats rename:** Backend field renames in 4 stats endpoints. Independent per controller.
- **CTP-8 currency display:** Frontend-only change in `page.tsx`. No backend required.
- **CTP-4 confirmation dialogs:** Frontend-only. Each tab's confirm() can be added independently.
- **T2.28 MaintenanceMessage length:** Additive change, no migration conflict.
- **T1.15 Devices crashCount/telemetryCount projection:** Backend-only projection change.
- **T1.14 HardwareFingerprint removal from GetAll:** Backend-only projection change.

### Blocked (must wait for another fix first)

| Finding | Blocked by | Reason |
|---|---|---|
| IP Whitelist tab CTP-12 drift (T0.13) | `ip_whitelists` table creation (T0.8, via CTP-9 migration) | Tab is broken at DB level; fixing contract drift before table exists adds no value |
| IP Whitelist enforcement re-wire (T0.3) | `ip_whitelists` table creation (T0.8) | Need the table to exist before enforcement code can read from it |
| Updates tab upload flow (T0.20) | Backend DLL redeploy (T0.20 is ops prerequisite) | Cannot test until DLL is redeployed; also blocked on migration for `Platform` column (T1.16) |
| Updates delete confirm platform display (T2.17) | T0.20 (DLL redeploy) + T1.16 (migration) | Platform field undefined until both are resolved |
| Licenses Revoke/Activate UX (T2.9) | Licenses controller restore (T0.9/T1.7) | Buttons don't render (empty list) until GET endpoint is working |
| Licenses `activeDevices` field (T1.8) | Licenses controller restore (T0.9/T1.7) | Field is in the backup controller's projection; restore needed |
| Devices Delete endpoint UI (T2.16) | T1.13 (restore GetById/Delete from backup) | UI delete button would call a 404 endpoint until backend is restored |
| CTP-7 webhook idempotency unique index | Migration applied (CTP-9) | Index cannot be added until `dotnet ef database update` or manual SQL runs |
| Frontend rebuild (resolves CTP-12) | All backend API contracts stabilized | Rebuilding before all routes/shapes are final would require another rebuild |
| Admin audit log wiring (CTP-2) | `admin_audit_log` table creation (T0.7) | Controllers cannot log until the table exists |

---

## Fix-phase effort estimate

| Tier | Findings | Avg effort | Estimate |
|---|---|---|---|
| Tier 0 (Critical) | 20 findings | 0.5–2 hours each | 15–30 hours |
| Tier 1 (High) | 27 findings | 0.5–1.5 hours each | 15–25 hours |
| Tier 2 (Medium) | 30 findings | 0.25–1 hour each | 10–20 hours |
| Tier 3 (Low) | 19 findings | 0.25–0.5 hours each | 5–10 hours |
| Cross-tab pattern batch fixes | 13 CTPs (CTP-1 to CTP-13) | 1–8 hours each | 20–40 hours |

**Total estimated effort:** 65–125 developer-hours

Notes on effort concentration:
- CTP-2 (audit logging, L) dominates at ~8h: new table + service + 12 controller touch-points + tests
- CTP-6 (rollback restoration, L) at ~6h: 5 controllers, restore + validate + rebuild + redeploy
- T0.20 (Updates 6.6.E deploy, L) at ~4h: rebuild + apply migration + env vars + smoke test
- All remaining are S or M individually

---

## Non-audit recommendations (infrastructure / ops)

These are not tab-specific findings but should feed adjacent work before or alongside the fix phase:

1. **`__EFMigrationsHistory` is empty — systemic.** The production DB was bootstrapped via raw DDL and no `dotnet ef database update` has ever run. Apply all pending migrations before any fix that touches DB schema. Prerequisite for: `ip_whitelists` table, `Platform`/`GitHubReleaseId` columns on `app_updates`, all EF-declared indexes across 7+ tables.

2. **6.6.E backend never deployed — urgent ops step.** Rebuild backend DLL from current local repo + apply migration + restart `auracore-api` service. The Updates tab upload flow, CryptoController `AdminRejectPayment`, and `IR2Client`/`IGitHubReleaseMirror` are all missing from the April 14 DLL. This is a pure ops step (no code change needed for the DLL itself — code is already in local repo).

3. **R2 + GitHub PAT env vars not set in `/etc/auracore-api.env`.** Per `docs/ops/release-pipeline-setup.md`: `R2_ACCOUNT_ID`, `R2_ACCESS_KEY_ID`, `R2_SECRET_ACCESS_KEY`, `R2_BUCKET`, and `GITHUB_TOKEN` are all absent. Even after DLL redeploy, `PrepareUpload` will fail at IR2Client level until these are set.

4. **Admin panel has no git history.** `/root/admin-panel/` is not a git repo. Each rebuild/deploy overwrites files with no version history. Rollback relies on `/root/admin-backup-*` timestamped directories only. Recommend: `git init /root/admin-panel && git add -A && git commit -m "baseline"` + establish a build-and-commit deploy script.

5. **No `admin_audit_log` table — dedicated audit-logging infrastructure needed.** This is the canonical Phase 6 Item 8 sub-phase 1 candidate. Until this table and associated `IAuditWriter` service exist, every fix to individual tab mutations is incomplete from a security standpoint.

6. **Nginx basic auth does NOT protect API endpoints.** `api.auracore.pro` vhost has no `auth_basic` directive. Admin mutations rely solely on JWT `[Authorize(Roles="admin")]`. The `auth_basic off` in the `location /api/` block on `admin.auracore.pro` is intentional for the SPA, but it means a stolen admin JWT has unrestricted API access. Recommend: add rate limiting on `POST /api/auth/login` as the minimum mitigation (brute-force defense if no basic auth layer).

7. **Bug 3 (B-2) — Refresh data-loss.** This pre-audit known bug was NOT confirmed on any tab's Refresh button — all Refresh buttons are `onClick={load}` soft refetches, NOT `window.location.reload()`. However, a full browser page reload (F5/Cmd+R) does lose the JWT because it is stored in React state (`useState`), not `localStorage`. A proper fix: persist the JWT in `sessionStorage` or `localStorage` so hard reloads survive.

---

## Recommended fix-phase sub-phase breakdown

### Sub-phase 6.8.A: Ops prerequisites (no code changes — pure operations)
**Goal:** Unblock all other sub-phases.
1. Apply all EF migrations to production DB (`dotnet ef database update` from API root — or manual SQL per finding). Creates: `ip_whitelists` table, `Platform`/`GitHubReleaseId` on `app_updates`, all 7+ missing indexes.
2. Set R2 + GitHub PAT env vars in `/etc/auracore-api.env`.
3. Rebuild backend DLL from current local repo + restart `auracore-api`. This deploys 6.6.E Updates controller + restores any code already in local repo.
4. Smoke test: Updates tab upload, Licenses GET, Crypto Reject endpoint, IP Whitelist GET.

**Findings unblocked:** T0.8, T0.20, T1.16, T2.17, CTP-9 (all tabs), partial T0.9/T0.13

---

### Sub-phase 6.8.B: Critical security (highest attack surface)
**Goal:** Close the three active security holes before the product grows.
1. **CTP-13 (T0.1):** Move `LoginAttempt` write to after TOTP check in `AuthController`. Do not persist `RefreshToken` before TOTP is verified.
2. **T0.2:** Add `[MinLength(8)]` guard to `AdminUserController.ResetPassword`.
3. **T0.3:** Re-wire `IsWhitelisted` check in `AuthController` using the new EF-backed `IpWhitelistService`. Restore enforcement from backup `AuthController.cs:77–85`.
4. **T1.27:** Wrap TOTP secret with `IDataProtector.Protect/Unprotect` in `TotpService`.
5. **T2.29:** Collapse 404/400 responses in `/api/2fa/validate` to prevent email enumeration.

---

### Sub-phase 6.8.C: CTP batch fixes (systemic patterns — fix once, heal many tabs)
**Goal:** Address cross-tab patterns with maximum impact per change.
1. **CTP-6:** Restore gutted controllers from backup — `AdminLicenseController` (full CRUD), `AdminDeviceController` (GetById + Delete), `CryptoController` (`AdminRejectPayment`), `AdminChartController` (full controller). Validate API contract vs frontend expectations before committing.
2. **CTP-10:** Add `pages = Math.Ceiling(total / pageSize)` to Devices, Crash Reports, Telemetry, Audit Log GetAll responses.
3. **CTP-11:** Rename backend stats fields to match frontend reads across Devices, Crash Reports, Telemetry, Audit Log.
4. **CTP-1:** Add `tier = license?.Tier ?? "free"` to `AdminUserController.GetAll` projection.
5. **CTP-7:** Restore idempotency check in `StripeController.HandleCheckoutCompleted` and `HandleInvoicePaid`. Add `IsUnique()` on ExternalId.
6. **CTP-5:** Reorder `AdminUserController` delete logic — collect `deviceIds` BEFORE `RemoveRange`.
7. **CTP-8:** Replace hardcoded `$` with `Intl.NumberFormat` in `page.tsx`.

---

### Sub-phase 6.8.D: Admin audit log infrastructure
**Goal:** Add traceability to all admin actions (CTP-2 + T0.7).
1. Create `admin_audit_log` table migration: `(Id, AdminEmail, Action, TargetType, TargetId, Before jsonb, After jsonb, IpAddress, CreatedAt)`.
2. Create `IAuditWriter` service + DI registration.
3. Wire into all 12 Admin controllers at every mutation endpoint.
4. Update Audit Log tab: add `admin_audit_log` display alongside (or in place of) login attempts view. Fix route mismatch (T0.12) and key mismatch simultaneously.

---

### Sub-phase 6.8.E: Per-tab functional bugs (Tier 1 remaining)
**Goal:** Fix tab-level HIGH findings not covered by CTP batch.
1. Payments: restore `AdminChartController` (CTP-6 already covers this), fix `HandleCheckoutCompleted` USD hardcode (T1.12), fix `$` currency display (CTP-8 already covers this).
2. Updates: validate all 3 steps of 6.6.E upload flow post-6.8.A deployment. Fix `(undefined)` platform display in delete confirm (T2.17).
3. Configuration: add `IMemoryCache` for `IsMaintenanceMode` in middleware (T0.11). Add enforcement reads to `AuthController.Register`, `TelemetryController.ReceiveBatch`, `CrashReportController.Submit`, `UpdateController.Check` (T1.25). Add `CHECK ("Id" = 1)` constraint (T1.26).
4. Crash Reports: fix version filter param name (`version` → `appVersion` or vice versa, T1.19). Fix stats field names (CTP-11 in 6.8.C covers this).
5. Telemetry: add rate limit + batch-size cap to `TelemetryController.ReceiveBatch` (T1.20).
6. IP Whitelist: add `IPAddress.TryParse` validation to `AdminIpWhitelistController.Add` (T1.24). Fix CTP-12 drift (route + body fields).

---

### Sub-phase 6.8.F: UX Polish (Tier 2 + Tier 3 batch)
**Goal:** Address validation gaps, confirmation dialogs, mobile layout.
1. **CTP-4:** Implement shared `ConfirmModal` component. Replace all immediate-fire destructive handlers across 7 tabs.
2. **CTP-3:** Add hamburger-toggle sidebar (collapses at ≤768px). Single root-layout change.
3. Validation: Days field min/max (T2.1), MaxDevices range (T2.8), MaintenanceMessage maxLength (T2.28), tier allowlist validation.
4. UX: search debounce (T2.6), "Showing X–Y of N" pagination label (T2.5), role-change UI (T2.7 — scope TBD).
5. Minor: fix `toast color` logic (T2.4), `StatusBadge` for `confirming`/`awaiting_payment`/`disputed` statuses (T3.7), blockchain explorer link (T3.8).
6. Security tab: add backup codes generation at TOTP enrollment (T3.18), add same-device warning (T3.19).
7. Login attempts: add 90-day retention purge job (T2.24), add right-to-erasure on user delete (T3.15).

---

### Sub-phase 6.8.G: Frontend rebuild (coordinates CTP-12)
**Goal:** After all backend API contracts are stable (post 6.8.A–F), rebuild the admin panel frontend and redeploy.
1. Run `npm run build` from `/root/admin-panel/`.
2. Deploy output to `/var/www/admin-panel/`.
3. Initialize git in `/root/admin-panel/` for future change tracking.
4. Verify all routes, field names, and response shapes align between the new DLL and new bundle.

This sub-phase resolves: CTP-12 (IP Whitelist route drift, Audit Log key drift, Security 2FA QR drift), deployment drift across all tabs (T3.1, T3.3, T3.5).

---

## Triage summary

**Sub-phases:** 7 (6.8.A through 6.8.G)

**Critical path:** A (ops) → B (security) → C (CTP batch) → D (audit log) → E (per-tab) → F (UX polish) → G (frontend rebuild)

**Minimum viable fix:** Sub-phases A + B + C together restore functional parity across all tabs and close the three active security vulnerabilities, with an estimated effort of 25–40 hours.
