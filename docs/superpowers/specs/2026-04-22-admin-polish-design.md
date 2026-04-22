# Admin Panel Polish Phase — Design Spec

**Status:** Design approved (user, 2026-04-22, preemptive section approval). Next: writing-plans.
**Branch:** `phase-6-admin-polish` (created from `main` HEAD `7c4e32f`, the Phase 6.8 ceremonial seal).
**Phase ref:** Phase 6 Item 9 (polish: High + Medium + Low cherry-picks + 4 remaining CTPs). Admin panel UI rebuild (CTP-3 mobile responsive + full frontend rewrite) split out to Phase 6.10.

## Context

Phase 6 Item 7 (audit) surfaced **96 findings across 12 admin panel tabs + 13 cross-tab patterns**, documented in `docs/admin-audit/findings/*.md` + `docs/admin-audit/triage.md`.

Phase 6.8 (must-fix, merged at `ab41a09`, ceremonial `7c4e32f`) closed:
- All 20 Critical findings
- 12 High findings (cascade-closed by Critical fixes)
- 4 Medium findings (cascade)
- CTP-1/2/5/6/9/10/11/12 (partial — patterns instantiated in 6.8's scope)

Phase 6.9 (this spec) is **admin panel polish** — handles the remaining High, Medium, and 4 Low cherry-pick findings plus 4 cross-tab patterns (CTP-2 extension, CTP-4, CTP-8, CTP-9) that don't require admin panel UI rewrite. Frontend rebuild and mobile responsive deferred to Phase 6.10.

## Scope

### In scope — Phase 6.9 (~45 findings + 4 CTPs)

- **15 High findings** (remaining after Phase 6.8 cascade)
- **26 Medium findings**
- **4 Low cherry-picks**: T3.2 (admin@auracore.pro license data anomaly), T3.7 (StatusBadge missing statuses), T3.13 (stackTracePreview truncation restore), T3.17 (CORS stale domain)
- **CTP-2 extension** — `[AuditAction]` attribute on remaining mutation controllers (CrashReport, Telemetry, Config, IpWhitelist, AdminUser)
- **CTP-4** — destructive action confirmation dialog as shared React component, applied across 6 admin tabs
- **CTP-8** — hardcoded `$` currency swap: central `formatCurrency(amount, currency)` helper + replacement across admin panel
- **CTP-9** — prod DB index catch-up via idempotent SQL DDL (`crash_reports.CreatedAt`, `telemetry_events.CreatedAt/EventType`, `login_attempts` composite, `payments.ExternalId` unique index)

### Out of scope — deferred

- **Phase 6.10**: Admin panel UI rebuild — CTP-3 mobile responsive, Next.js/React migration, visual redesign, license key format (T3.6) prettification, destructive-confirmation component primitive promotion
- **Phase 6.11+**: Feature work not in audit — TOTP backup codes (T3.18), PII/GDPR right-to-erasure (T3.15), role-change UI (T2.7), blockchain explorer links (T3.8), online/offline device indicator (T3.9)

### Cherry-picked Low tier (4 items)

Per D5 decision (brainstorm Q3): 4 of 19 Low findings included because they are real-runtime impact despite Low severity:

| ID | Summary | Why 6.9 scope | Effort |
|---|---|---|---|
| T3.2 | `admin@auracore.pro` has Tier=free with ExpiresAt=2126 | Data anomaly; SQL one-liner fix | S (SQL) |
| T3.7 | StatusBadge missing `awaiting_payment`, `confirming`, `disputed` | Visible in Payments tab today | S (frontend) |
| T3.13 | `stackTracePreview` truncation stripped from list response | Restorable from backup, UX improvement | S (backend) |
| T3.17 | CORS origins use stale `auracorepro.com` (prod is `auracore.pro`) | Runtime CORS reject risk | S (Program.cs) |

### Deferred Low (15 items)

Never-priority cosmetic + code smell:
- T3.1, T3.3, T3.5, T3.14 (deployment drift / horizontal overflow — cosmetic)
- T3.4 (Revoke confirmation — cascade-closes via CTP-4 in 6.9, but this is the Users-tab-specific Low duplicate)
- T3.6 (license key format — Phase 6.10 rebuild)
- T3.8, T3.9, T3.10 (device/payment feature adds — Phase 6.11)
- T3.11, T3.16 (audit log for Updates + Config Low instances — already cascade-closed when CTP-2 extension lands in Wave 1)
- T3.15 (PII/GDPR erasure — compliance phase)
- T3.18 (TOTP backup codes — feature work)
- T3.19 (same-device TOTP enrollment warning — UX, low-impact)

## Design decisions

### D1 — Single branch, per-pattern execution (brainstorm Q1+Q2)

One branch `phase-6-admin-polish` off `main` HEAD `7c4e32f`. All waves merged with one `--no-ff` ceremonial commit at end. Phase 6.6/6.8 precedent.

Tasks organized **per-pattern** (Wave 1 closes cross-tab patterns first; Wave 3 handles tab-specific leftovers), not per-tab, because cross-tab patterns avoid code duplication (e.g., shared `<ConfirmDialog>` component written once, applied across 6 tabs).

### D2 — Two-point deploy (brainstorm Q4)

- **Midway deploy (end of Wave 2):** Backend DLL + CTP-9 SQL DDL + CTP-2 audit log extension. Backend-only, no frontend touch. Audit log starts capturing admin mutations immediately.
- **Final deploy (end of Wave 4):** Admin panel frontend rebuild (CTP-4 confirmation component + CTP-8 currency + T3.7 StatusBadge + T2.17 "undefined" platform label fix + other per-tab UX). Frontend-only.

Rationale: backend audit log should start accumulating real admin action data as early as possible; frontend change decoupled so UI regressions don't block backend fixes. Each deploy independently rollbackable.

### D3 — Admin panel frontend source stays on origin (brainstorm Q5)

Source remains at `/root/admin-panel/src/` on origin (165.227.170.3). Local scp pattern from Phase 6.8 landing-page work: pull to `admin-panel-work/` in local repo (gitignored), edit, rebuild, scp `out/` back to `/var/www/admin-panel/` with `.bak-YYYYMMDDHHMM` timestamp rollback.

Phase 6.10 rebuild will restructure this (bring into repo, CI/CD-deployable). Not 6.9's job.

### D4 — Surgical regression test strategy (brainstorm Q5)

Same pattern as Phase 6.8: write regression tests ONLY for:
- Cross-tab pattern contracts (e.g., CTP-4 ConfirmDialog `onConfirm` callback, `onCancel` non-mutation)
- Behavioral fixes (e.g., IP format validation accepting/rejecting specific inputs)
- Audit log extension per-controller (e.g., `[AuditAction("DeleteCrashReport", "CrashReport")]` wires correctly)
- Any fix whose regression vector is subtle (e.g., search debounce timing)

**Skip tests for:**
- Trivial field rename / projection shape fix (e.g., `activeDevices` → `DeviceCount`)
- Frontend CSS-only changes (mobile polish touches, StatusBadge color adds)
- Confirmation dialog presence test (covered by CTP-4 shared component test)

**Target:** 2323 → ~2348 (+25 tests, bulk additive).

### D5 — Findings distribution

| Severity | Total | 6.8 cascade-closed | 6.9 scope | Deferred |
|---|---|---|---|---|
| Critical | 20 | 20 | 0 | 0 |
| High | 27 | 12 | **15** | 0 |
| Medium | 30 | 4 | **26** | 0 |
| Low | 19 | 0 | **4** | 15 |
| **Total** | **96** | **36** | **45** | **15** |

Cross-tab patterns:
- CTP-1, CTP-5, CTP-6, CTP-10, CTP-11, CTP-12, CTP-13 — closed in 6.8
- CTP-2 — partially closed in 6.8 (audit_log infrastructure + [AuditAction] on AdminLicense/AdminDevice/Stripe/Crypto); extension in 6.9 Wave 1
- CTP-3 — **deferred to Phase 6.10** (mobile responsive admin)
- CTP-4 — **6.9 Wave 1** (shared ConfirmDialog + apply to 6 tabs)
- CTP-7 — closed in 6.8 (Stripe idempotency)
- CTP-8 — **6.9 Wave 1** (central formatCurrency helper + swap)
- CTP-9 — **6.9 Wave 1** (missing EF indexes — SQL DDL)

### D6 — Wave structure

**Wave 1 — Cross-tab pattern work (backend-leaning):**
1. CTP-9 missing EF indexes (idempotent SQL DDL, applied as `Wave1Indexes.sql`)
2. CTP-2 extension: `[AuditAction]` on AdminCrashReport.Delete, AdminTelemetry.Delete (if exists), AdminConfig.Update, AdminIpWhitelist.Add/Delete, AdminUser.DeleteUser/UpdateRole — all mutation endpoints
3. CTP-8 backend preparation: ensure `Payment.Currency` is reliably set on all webhook/create paths (audit found T1.12 "HandleCheckoutCompleted hardcodes USD"), already partially backend-additive

**Wave 2 — Backend bugs (tab-specific remaining High/Medium):**
Per-tab backend fixes:
- Subscriptions: T1.1 (subscriptions table unused — Grant writes to licenses, fix semantics), T1.2 (nginx basic auth bypass check), T2.1 (Days validation), T2.4 (toast color logic)
- Users: T1.6 (tap target ≥44px — frontend? actually mobile UX; frontend in Wave 4), T2.5 (pagination label — frontend Wave 4), T2.6 (search debounce — frontend Wave 4), **backend: T2.25 my-ip self-whitelisting endpoint restore**
- Licenses: T1.8 (activeDevices vs DeviceCount rename), T2.8 (Create input validation)
- Payments: T1.11/T1.12 (currency hardcode finish — backend webhook Currency assignment), T2.13 (ExternalId unique index — already CTP-9), T2.14 (float `/100.0` → decimal)
- Devices: T1.14 (HardwareFingerprint privacy — remove from GetAll projection), T1.15 (crashCount/telemetryCount fields)
- Crash Reports: T1.19 (version vs appVersion query param), T2.19 (Message column drift), T2.20 (CreatedAt index — CTP-9), T3.13 (stackTracePreview truncation restore)
- Telemetry: T1.20 (rate limit + batch-size cap on client POST)
- Audit Log: T2.24 (retention/purge policy — cron or trigger), **T1.23 cascade-closed in 6.8 via stats rewrite**
- IP Whitelist: T1.24 (IP format validation)
- Configuration: T1.25 (4 cosmetic toggles — make functional), T1.26 (singleton DB-level protection), T2.28 (length limit on MaintenanceMessage)
- Security 2FA: T1.27 closed in 6.8, T2.29 (`/api/2fa/validate` AllowAnonymous leak), T2.30 (in-memory rate-limit thread safety)
- T3.2 admin user data anomaly SQL fix
- T3.17 CORS stale domain fix

**Wave 3 — Midway deploy checkpoint:**
- Backend build Release + scp + service restart
- Apply Wave 1 SQL DDL (idempotent CREATE INDEX + ON CONFLICT DO NOTHING seeds)
- Smoke test (admin JWT + hit 3-4 endpoints, verify audit_log rows appear)
- Commit ops marker

**Wave 4 — Frontend patches (admin-panel-work/ scp pattern):**
- CTP-4 ConfirmDialog component (React) + wire into 6 action sites: SubscriptionsGrant, LicensesRevoke/Activate, UsersDelete, CrashReportsDelete, IpWhitelistDelete, ConfigToggles (especially IsMaintenanceMode)
- CTP-8 formatCurrency helper + swap hardcoded `$` in PaymentsPage, SubscriptionsPage, dashboard
- T1.6 tap targets (quick CSS: action buttons min-height 44px)
- T2.5 "Showing X–Y of N" pagination label shared component
- T2.6 search debounce (existing search inputs, useDebouncedValue hook)
- T2.17 Delete confirmation "(undefined)" platform field fix
- T2.18 Publish form all-platforms-unchecked validation
- T2.26 IP delete confirmation (cascade to CTP-4)
- T2.27 config toggle confirmations (cascade to CTP-4)
- T3.7 StatusBadge missing statuses (awaiting_payment, confirming, disputed)

**Wave 5 — Final deploy + ceremonial:**
- Admin panel rebuild (next build) + scp `out/` → `/var/www/admin-panel/` with timestamped backup
- Full test suite green verification (~2348)
- Memory file write
- MEMORY.md pointer update (Phase 6.8 → 6.9)
- `--no-ff` merge to main + ceremonial commit + push (user-gated)

## Architecture — Per-wave components

### Wave 1 Components (cross-tab, backend)

- **`src/Backend/AuraCore.API.Infrastructure/Migrations/` idempotent DDL patch file** OR inline SQL via psql heredoc (preferred — matches Phase 6.8 `_deploy_6_8_a.sql` pattern; no new EF migration needed since indexes are database-only optimization)
- **`[AuditAction]` attribute additions** — existing filter from Phase 6.8 (`src/Backend/AuraCore.API/Filters/AuditActionAttribute.cs`), just applied to more methods
- **`Currency` field normalization** in webhook flows — `StripeController.HandleCheckoutCompleted` already gets metadata.currency, ensure `Payment.Currency` stored as-is (not hardcoded "USD")

### Wave 2 Components (backend per-tab)

- `src/Backend/AuraCore.API/Controllers/Admin/*.cs` — surgical edits per tab
- `src/Backend/AuraCore.API/Controllers/CryptoController.cs` — `my-ip` endpoint restoration if absent
- `src/Backend/AuraCore.API/Program.cs` — CORS origins fix (T3.17), optional singleton AppConfig enforcement
- `src/Backend/AuraCore.API.Infrastructure/Data/AuraCoreDbContext.cs` — singleton check-constraint for app_configs

### Wave 4 Components (frontend admin panel)

Operating on `admin-panel-work/` (scp'd from origin `/root/admin-panel/src/`):
- `src/components/ConfirmDialog.tsx` — new shared component (props: `title`, `message`, `confirmLabel`, `onConfirm`, `onCancel`, `destructive` flag for red styling)
- `src/lib/format.ts` — new `formatCurrency(amount, currency)` with Intl.NumberFormat
- `src/components/StatusBadge.tsx` — add `awaiting_payment`, `confirming`, `disputed` status-to-color mappings
- `src/components/PaginationLabel.tsx` — new shared "Showing X–Y of N" component
- `src/hooks/useDebouncedValue.ts` — new React hook
- `src/app/page.tsx` (or per-page subcomponents) — swap `$` for `formatCurrency()` call sites, add `<ConfirmDialog>` wrappers to destructive actions

## Testing strategy

### Backend (xUnit)

New test sub-directory: `tests/AuraCore.Tests.API/AdminPolish/`
- `AuditLogExtensionTests.cs` — verifies `[AuditAction]` applied to each new mutation method (regression tests, follow Phase 6.8 `AuditLogAttributeTests` pattern — mock controller action context, assert audit_log row written)
- `BackendBugFixTests.cs` — behavioral tests: IP format validation, Days negative rejection, webhook Currency preservation, etc. (~10-12 tests)
- `IndexPresenceTests.cs` — integration-light test confirming expected indexes exist (optional; not run in unit test scope if DB isn't available)

### Frontend (no new tests in 6.9)

Admin panel currently has no test harness. Phase 6.10 rebuild adds Playwright / React Testing Library setup. For 6.9, frontend changes are manually verified via admin.auracore.pro smoke test after Wave 5 deploy.

### Target

- Baseline post-6.8: 2323
- +25 surgical regression tests
- Target: ~2348 tests, 0 failed, 0 skipped

## Deployment flow

Per D2 two-point strategy:

**Midway (Wave 3):**
```bash
# Backend-only, lowest risk
ssh root@165.227.170.3 "TS=$(date -u +%Y%m%d%H%M); cp -r /var/www/auracore-api /var/www/auracore-api.bak-${TS} && sudo systemctl stop auracore-api"
scp -r publish-output/. root@165.227.170.3:/var/www/auracore-api/
ssh root@165.227.170.3 "PGPASSWORD='auracorepro2026' psql -h localhost -U postgres -d auracoredb -f /tmp/wave1_indexes.sql"
ssh root@165.227.170.3 "systemctl start auracore-api && sleep 4 && systemctl is-active auracore-api"
# Smoke test: curl admin JWT + few endpoint probes + psql audit_log SELECT COUNT
```

**Final (Wave 5):**
```bash
# Frontend-only, backend untouched
ssh root@165.227.170.3 "cd /root/admin-panel && npm run build"
# (OR local rebuild if admin-panel-work/ is newer)
ssh root@165.227.170.3 "TS=$(date -u +%Y%m%d%H%M); cp -r /var/www/admin-panel /var/www/admin-panel.bak-${TS}"
scp -r admin-panel-work/out/. root@165.227.170.3:/var/www/admin-panel/
# No service restart needed (static files served by nginx directly)
# Smoke test: open admin.auracore.pro, verify 6 tabs load + confirmations work + currency shows native code
```

## Open questions / known risks

- **CTP-4 confirmation dialog UX shape:** plan assumes a modal overlay. If the current admin panel has toast-based UX for some actions, uniform confirmation dialog might feel heavy. Mitigation: implement as low-visual-weight "confirm in a banner" pattern; revisit in Phase 6.10 rebuild if out of place.
- **T1.20 telemetry rate limit design:** no current rate limit → unbounded DB writes risk. Plan uses simple in-memory counter (IP + endpoint + 1-minute sliding window, e.g. 60/min cap). In-memory state lost on restart (same tradeoff as current 2FA rate limiter per T2.30). Revisit for Redis-backed solution in Phase 6.11 if abuse observed.
- **T1.25 4 config toggles non-functional:** NewRegistrations, Telemetry, CrashReports, AutoUpdate are stored but never read. Each needs enforcement logic in corresponding endpoints. Scope-creep risk: "make toggle functional" = small to medium per toggle. Plan includes only simple gate checks (return 503 / skip operation when toggle false), not rich config-driven behavior.
- **Config singleton enforcement (T1.26):** adding CHECK constraint `id=1` via migration. Must verify prod data has exactly 1 row first (expected) before applying constraint.
- **Frontend ConfirmDialog + CTP-8 currency swap done together** in Wave 4 → single frontend deploy minimizes rollback granularity. If a UI bug is traced to one or the other, rollback is "frontend as a whole" not per-change. Acceptable risk; both changes are visual and easy to identify.
- **Admin panel source on origin not in repo:** Phase 6.9 continues the "source on origin" debt. If anything goes wrong with scp'd files (corruption, wrong version), recovery is `.bak-<timestamp>` restore. Documented limitation, resolved in Phase 6.10.

## Decision log

| Decision | Chosen | Rejected | Why |
|---|---|---|---|
| Phase split | 6.9 polish + 6.10 UI rebuild | Single 6.9 covering all of A/B/C/D | Brainstorm Q1: frontend rebuild's visual-design prep would slow backend fixes; user picked B |
| Execution | Per-pattern (cross-tab first) | Per-tab (vertical) or severity-flat | Brainstorm Q2: shared ConfirmDialog written once, applied to 6 tabs; avoids code dup; user picked B |
| Low tier | 4 cherry-picks + defer 15 | All 19 Low OR zero Low | Brainstorm Q3: cherry-pick real-runtime impact items; T3.18/T3.15 are feature/compliance work not polish; user picked B |
| Deploy | Two-point (mid backend + final frontend) | Single big deploy or per-task incremental | Brainstorm Q4: audit log value accrues from deploy time; frontend-backend decoupling allows independent rollback; user picked B |
| Test | Surgical +25 (6.8 pattern) | +45 or +5 | Brainstorm Q5: trivial rename fixes don't warrant test; behavioral + cross-pattern do; user picked A |
| Frontend source | Origin-only scp pattern | Pull into main repo now | Brainstorm Q5 bonus: Phase 6.10 rebuild is natural moment to restructure; user chose server-side |

## Non-goals

- Admin panel visual redesign (color system, typography, layout — 6.10)
- New admin features (role management UI, PII erasure, TOTP backup codes)
- Mobile responsive admin panel (CTP-3 — 6.10)
- Nginx config changes (basic auth layer stays as-is)
- Database schema changes beyond index catch-up and optional singleton constraint
- Auth provider migration (still JWT + TOTP)
- Payment gateway additions

## Success criteria

Phase 6.9 is DONE when:
- All 15 High findings resolved (close each via commit message reference or cascade via CTP)
- All 26 Medium findings resolved (or documented as cascade-closed)
- 4 Low cherry-picks resolved (T3.2, T3.7, T3.13, T3.17)
- CTP-2 extension live: all admin mutation endpoints emit audit_log rows
- CTP-4 live: destructive actions in 6 tabs gated by ConfirmDialog
- CTP-8 live: all `$` instances in admin panel replaced with `formatCurrency()`
- CTP-9 live: 4-6 missing indexes created in prod DB
- Two deploys landed: backend midway + frontend final
- audit_log captures >20 rows in the first day post-deploy (smoke verification)
- All new tests passing: ~2348 total, 0 failed, 0 skipped
- Memory file written + MEMORY.md pointer updated
- Branch merged to main via `--no-ff` (ceremonial) + pushed to origin (user-gated)

**Spec end.** Writing-plans skill invoked next.
