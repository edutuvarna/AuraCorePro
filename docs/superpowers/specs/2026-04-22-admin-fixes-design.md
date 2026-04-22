# Admin Panel Must-Fix Phase — Design Spec

**Status:** Spec approved (user, 2026-04-22). Next: writing-plans.
**Branch:** `phase-6-admin-fixes` (to be created from `phase-6-admin-audit` HEAD `f982179` so audit findings + triage.md travel with fixes).
**Phase ref:** Phase 6 Item 8 (must-fix; polish + frontend rebuild = Phase 6.9; real-time sync = Phase 6.10).

## Context

Phase 6 Item 7 (admin audit) surfaced **96 findings across 12 tabs + 13 cross-tab patterns**, documented in `docs/admin-audit/findings/*.md` + `docs/admin-audit/triage.md`. User's hypothesis about rollback artifacts (B-4) concretely confirmed: 5 controllers stripped during security refactor (Nginx basic auth addition), Phase 6.6.E backend never deployed, `__EFMigrationsHistory` empty.

Phase 6.8 (this spec) is **must-fix only** — handles the 20 Critical findings + systemic CTP cascades that unblock the platform. Non-urgent polish (High-severity per-tab bugs, UX improvements, mobile responsive, frontend rebuild) deferred to Phase 6.9.

## Scope

### In scope — Phase 6.8 Must-Fix (4 sub-phases)

1. **6.8.A Foundation** — ops prerequisites + audit log infrastructure
2. **6.8.B Controller Restoration** — restore 5 rollback-stripped controllers from backup
3. **6.8.C Critical Security Batch** — 20 Critical findings (security + systemic leftovers after CTP-6 restore)
4. **6.8.D Systemic Contract-Drift** — additive backend fixes for CTP-1/10/11/12 (frontend rebuild NOT in this phase)

### Out of scope — deferred

- **Phase 6.9** (polish): 27 High findings (per-tab functional bugs), 49 Medium+Low findings (UX + validation + error handling), CTP-3 (mobile responsive), frontend rebuild + deploy
- **Phase 6.10**: Real-time sync infrastructure (SignalR / SSE / polling)
- **Phase 6 Item 11+**: features beyond bugs (new admin panel features, dashboard enhancements, new tabs)

### Out of audit scope (not touched)

- macOS notarization (Phase 6 Item 6 — user-hardware blocked)
- Multi-arch release pipeline
- Delta / incremental updates
- Authenticode signing
- Beta/canary channel UI
- User opt-out from auto-update

## Design decisions

### D1 — Audit log infrastructure (6.8.A)

**New table `audit_log`** (dedicated, not repurposed `login_attempts`):

```sql
CREATE TABLE audit_log (
    id BIGSERIAL PRIMARY KEY,
    actor_id UUID REFERENCES users(id) ON DELETE SET NULL,
    actor_email VARCHAR(256) NOT NULL,
    action VARCHAR(64) NOT NULL,
    target_type VARCHAR(32) NOT NULL,
    target_id VARCHAR(128),
    before_data JSONB,
    after_data JSONB,
    ip_address VARCHAR(45),
    created_at TIMESTAMPTZ DEFAULT NOW() NOT NULL
);
CREATE INDEX idx_audit_actor_created ON audit_log(actor_id, created_at DESC);
CREATE INDEX idx_audit_action_created ON audit_log(action, created_at DESC);
CREATE INDEX idx_audit_target ON audit_log(target_type, target_id);
```

**Enforcement:** attribute-based interceptor. Controller actions declare `[AuditAction("GrantSubscription")]`; middleware captures actor (from JWT claims), target (from action result), before/after data (shallow diff of returned entity).

**19 actions tracked** (all admin mutations): GrantSubscription / RevokeSubscription / CreateLicense / DeleteLicense / DisableUser / DeleteUser / ResetPassword / UpdateRole / CreateDevice / DeleteDevice / AddIpWhitelist / RemoveIpWhitelist / UpdateAppConfig / PublishUpdate / DeleteUpdate / RetryGitHubMirror / RejectCryptoPayment / ApproveCryptoPayment / EnableTotp / DisableTotp.

**Retention:** forward-only, no auto-purge (revisit at 1M rows or 1 year).
**Backfill:** skip (no events before table exists).
**UI update:** `AdminAuditLogController` SELECT changes from `login_attempts` to `audit_log`; frontend rename "Audit Log" display stays (now accurate).

### D2 — CTP-12 fix direction (6.8.D): **backend adapts additively**

Backend responses become SUPERSETS (old + new fields) — non-breaking for deployed frontend; frontend rebuild in Phase 6.9.D removes compatibility layer.

Specifically:

- **CTP-10 pagination:** add `pages = Math.Ceiling(total/pageSize)` to every list endpoint response (keep existing `{total, page, pageSize, items}`, ADD `pages`)
- **CTP-11 stats KPI names:** backend returns BOTH time-window names (`last24h`) AND semantic names (`today` = same value). Dual aliasing.
- **CTP-12 route drift:** backend adds ALIAS routes (e.g., `/api/admin/audit/login-attempts` → redirects 308 to `/api/admin/audit-log`). Frontend rebuild later removes alias need.
- **POST body field names:** case-insensitive Model Binder registration globally; `IpAddress`/`ip`, `Label`/`label` both work.

Rationale: backend deploys are independent, low-risk, and unblock admin panel functionality without waiting for frontend rebuild.

### D3 — Branch strategy: **single branch**

`phase-6-admin-fixes` — all 4 sub-phases in one branch, ceremonial close + `--no-ff` merge at end (prior phase precedent: Phase 6.6 27 commits in one merge).

Base: `phase-6-admin-audit` HEAD `f982179` (findings + triage travel with fixes).

### D4 — Test strategy: **surgical regression + integration**

**New tests mandatory:**
- 2FA brute-force bypass (TOTP order on login)
- Empty password ResetPassword (validation)
- Webhook idempotency (Stripe ExternalId guard)
- Webhook signature null check
- Whitelist enforcement integration

**Integration tests** (restored controllers):
- AdminLicense GET/Revoke/Activate paths
- CryptoController RejectPayment
- AdminChart revenue query
- AdminDevice GetById + Delete
- StripeController full webhook flow

**Skip tests for:**
- Pure frontend fixes (CTP-1 `u.tier` — manual verification via screenshots in 6.9.D)
- CSS/mobile changes (Phase 6.9.C)
- Audit log interceptor attribute binding (mocked verification sufficient)

**Baseline:** 2303 tests on main. **Target post-6.8:** ~2340 (+~35 new tests).

### D5 — Scope boundary (findings distribution)

| Severity | Count | Phase |
|---|---|---|
| Critical | 20 | 6.8.B/C/D |
| High | 27 | 6.9.A |
| Medium | 30 | 6.9.B |
| Low | 19 | 6.9.B |

Cross-tab patterns:
- **CTP-2** Audit log — 6.8.A
- **CTP-6** Stripped controllers — 6.8.B
- **CTP-1** u.tier — 6.8.D
- **CTP-10/11/12** Contract drift — 6.8.D
- **CTP-3** Mobile responsive — 6.9.C
- **CTP-4** Destructive confirmations — 6.9.B
- **CTP-5** EF cascade bug (AdminUserController only) — 6.8.C
- **CTP-7** Webhook idempotency — folded into 6.8.B (Stripe restore)
- **CTP-8** Hardcoded `$` currency — 6.9.D (frontend rebuild)
- **CTP-9** EF index migration gap — 6.8.A (migrations apply covers)
- **CTP-13** (per security-2fa.md) — 6.8.C

### D6 — v1.6.0 backfill hash fix

Phase 6.6.A migration seed row has `SignatureHash = ''`. Desktop client's `UpdateDownloader` fail-fast rejects empty hash → v1.6.0 users cannot self-update. Fix in 6.8.A:

```bash
# Manual ops step (user approval required — WRITE GATE):
curl -sL 'https://github.com/edutuvarna/AuraCorePro/releases/download/v1.6.0/AuraCorePro-Setup.exe' \
    | sha256sum | cut -d' ' -f1
# Copy the 64-char hex, then:
PGPASSWORD='auracorepro2026' psql -h localhost -U postgres -d auracoredb -c \
    "UPDATE app_updates SET \"SignatureHash\" = '<HASH>' WHERE \"Version\" = '1.6.0';"
```

### D7 — Password UpdatedAt propagation

Audit found `users.UpdatedAt` NOT bumped on password change (admin cannot see who had password changed recently — GDPR/forensic concern). Fix in 6.8.C:

- Add `users.UpdatedAt = NOW()` to `AdminUserController.ResetPassword`
- Optionally add Postgres trigger (belt-and-suspenders)

## Architecture — Sub-phase breakdown

### 6.8.A Foundation (estimated 6-10h)

**Ops prerequisites (manual user steps via SSH):**
1. `dotnet publish -c Release` of backend on local
2. scp publish output to origin `/var/www/auracore-api/`
3. SSH to origin, `sudo systemctl stop auracore-api`
4. Apply migrations manually on prod DB:
   - INSERT InitialCreate + AddPlatformToAppUpdate rows into `__EFMigrationsHistory`
   - OR run `dotnet ef database update` from origin (requires tool install on prod)
5. Add R2 + GitHub env vars to `/etc/auracore-api.env`
6. `sudo systemctl start auracore-api`
7. Verify: `curl -u ... /api/admin/updates/prepare-upload` → 400 (not 405, not 500 — means endpoint exists + auth gate)

**Audit log schema:**
- New migration `AddAuditLogTable` (table + 3 indexes)
- New entity `AuditLogEntry.cs` + DbContext entry
- New attribute `[AuditAction(action, targetType)]` + `IActionFilter`
- Registration: `services.AddScoped<IAuditLogService>()` + MVC filter

**v1.6.0 hash fix:** user-approved SQL WRITE.

### 6.8.B Controller Restoration (estimated 8-14h)

Restore 5 stripped controllers using backup at `/root/auracore-src-backup-final-202604122153/AuraCore.API/Controllers/` as reference (copy + modernize):

1. **AdminLicenseController** (26 → ~125 lines): GET list/by-id, Revoke, Activate endpoints
2. **StripeController** (276 → ~410 lines): `alreadyProcessed` idempotency guard + signature null handling + webhook event type expansion
3. **CryptoController** (145 → ~165 lines): AdminRejectPayment endpoint + notification
4. **AdminChartController** (missing → ~75 lines): new revenue chart endpoint at `/api/admin/charts/revenue` (matches frontend expectation, OR add route alias in 6.8.D)
5. **AdminDeviceController** (67 → ~95 lines): GetById + Delete endpoints

All restored endpoints get `[AuditAction(...)]` attributes from 6.8.A for logging.

### 6.8.C Critical Security Batch (estimated 10-18h)

20 Critical findings addressed:

**Security:**
- F-1 (2FA brute-force bypass): swap order in `AuthController.Login` — TOTP check before logging Success=true
- F-2 (empty password ResetPassword): add `[Required, MinLength(8)]` to `ResetPasswordRequest`
- F-3 (webhook 500 null signature): defensive null check + `catch NullReferenceException`
- F-4 (whitelist enforcement removed): restore `WhitelistService` (or rewrite stateless), integrate into `AuthController.Login` rate-limit bypass + `IpGate` middleware

**Data integrity:**
- F-5 (webhook idempotency): confirmed working after CTP-6 restore in 6.8.B (integration test added here)
- F-6 (CTP-5 EF cascade bug): fix `AdminUserController.DeleteUser` — collect deviceIds FIRST, then RemoveRange
- F-7 (CTP-13 secret storage): TOTP secret encryption at rest via `IDataProtector`

**Config + drift:**
- F-8..F-20: per-tab Critical findings (IP Whitelist table creation, AppConfig cache, Maintenance middleware fail-fast, etc.)

**UpdatedAt trigger:** D7 fix.

### 6.8.D Systemic Contract-Drift (estimated 6-10h)

Backend additive fixes:

- **CTP-1 u.tier:** `AdminUserController.GetAll` projection adds top-level `tier = license?.Tier ?? "free"` (matches `GetById` pattern). One-line fix.
- **CTP-10 pagination:** add `pages = (int)Math.Ceiling((double)total / pageSize)` to every list endpoint response in `Admin*Controller.cs`. 5-6 files, 1 line each.
- **CTP-11 stats KPI names:** `GetStats` methods return anonymous object with BOTH old + new field names (aliased). Backend-only, frontend unchanged.
- **CTP-12 routes:** backend attribute aliases (`[Route("api/admin/audit-log")]` + `[Route("api/admin/audit/login-attempts")]` both on same controller method). Backend binder case-insensitivity for POST bodies.

Test coverage: pick 2-3 per CTP for regression.

## Testing strategy

- **xUnit backend tests** at `tests/AuraCore.Tests.API/AdminFixes/`:
  - `AuditLogAttributeTests.cs`
  - `SecurityFixesTests.cs` (2FA + ResetPassword + webhook + whitelist)
  - `ControllerRestorationTests.cs`
  - `ContractDriftTests.cs` (CTP-1/10/11/12)
- **Integration smoke test** in 6.8.D final: end-to-end grant-subscription → audit log row written → tier shown correctly in Users tab UI probe
- **No frontend tests added** in 6.8 (frontend rebuild + tests = 6.9.D)

## Deployment flow

1. **6.8.A deploy first** (backend binary + migrations + audit_log table) — everything else depends on it
2. **6.8.B deploy** (restored controllers). Tested isolation: new controllers return 200 with test data via curl before declaring done
3. **6.8.C deploy** (security fixes)
4. **6.8.D deploy** (additive contract fixes). Safe — no breaking changes to existing responses.

All deploys via:
```bash
# On origin:
sudo systemctl stop auracore-api
# scp new DLLs + dependencies
sudo systemctl start auracore-api
# curl smoke test
```

## Open questions / known risks

- **TOTP secret encryption** (D7): adds `IDataProtector` dependency. If app restarts without keyring persistence, all existing TOTP secrets become unreadable. Mitigation: persist DataProtection keys to disk at `/var/www/auracore-api/.dataprotection-keys/` (user-owned, chmod 600). Document in `docs/ops/`.
- **CTP-5 EF cascade fix scope**: audit found the pattern only in `AdminUserController.DeleteUser`. But Phase 6.8.B restores 5 controllers — potential to introduce new occurrences. Add grep check at the end of 6.8.B: `grep -n "RemoveRange" src/Backend/AuraCore.API/Controllers/Admin/*.cs` — flag any `RemoveRange` before corresponding `.Where(...).ToList()` capture.
- **AdminChartController** vs frontend route: frontend hits `/api/admin/charts/revenue` (plural "charts"). Backup AdminRevenueController at `/api/admin/revenue/chart-data`. Creating AdminChartController adds new route but doesn't break backup — both can coexist (compat layer).
- **v1.6.0 empty hash**: if user declines the WRITE_GATE SQL update, v1.6.0 clients remain stuck (cannot auto-update). Fallback: add an empty-hash-allowed escape hatch in `UpdateDownloader` gated by app setting `AllowUnsignedLegacyUpdates=true`. Document as optional.

## Decision log

| Decision | Chosen | Rejected | Why |
|---|---|---|---|
| Phase split | 6.8 must-fix + 6.9 polish | All-in-one phase | User felt healthier boundary ("daha sağlıklı olucakmı gibi hissiyat") |
| 6.8 ordering | A/B/C/D | Triage's A/B/C/D (different contents) | Audit showed 3/5 Critical security fixes depend on CTP-6 restore; reordered to fix dependency |
| Audit log in 6.8.A | 6.8.A foundation | 6.8.D separate | Logging middleware lands early → every subsequent fix auto-logged |
| CTP-12 fix direction | Backend additive | Frontend rewrite | Backend deploys are independent + low-risk; decouples from frontend rebuild timeline |
| Branch strategy | Single `phase-6-admin-fixes` | Multi-branch merges | Prior phase precedent (6.6 = 27 commits, one merge) |
| Test scope | Regression + integration on Criticals | Full TDD coverage | Audit-derived fixes are mostly bug repair, not new features — targeted tests more valuable |
| Info-level findings | Include in 6.8/6.9 | Defer entirely | User confirmed "tüm bu session'da keşfedilenler faz 6.8'de yapılıcak" |

## Non-goals

- Rewriting admin panel to different framework (rollback-drift means restore + fix, not rewrite)
- Adding new admin features beyond bug fixes
- Changing database schema for reasons other than audit_log creation + migration catchup
- Multi-region deploys
- Performance optimization beyond what's needed to fix Critical findings

## Success criteria

Phase 6.8 is DONE when:
- All 20 Critical findings resolved (close each via commit message reference)
- All CTP-1/2/6/10/11/12 cross-tab patterns resolved or documented as cascade-closed
- Audit log table exists in prod + captures mutations end-to-end (verify via one admin action)
- Backend DLL deployed matches branch HEAD (no drift)
- Migrations applied in prod (`__EFMigrationsHistory` non-empty)
- R2 + GitHub env vars set; Phase 6.6.E release pipeline functional end-to-end (Task 33 smoke test from release-pipeline-setup.md)
- All new tests passing: ~2340 total, 0 failed, 0 skipped
- Memory file written + MEMORY.md pointer updated
- Branch merged to main via `--no-ff`

**Spec end.** Writing-plans skill invoked next.
