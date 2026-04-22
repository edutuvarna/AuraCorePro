# Admin Panel Deep Audit — Design Spec

**Status:** Spec approved (user, 2026-04-21). Next: writing-plans.
**Branch:** `phase-6-admin-audit` (separated from `phase-6-release-pipeline` ceremonially closed at main `b774b96`).
**Phase ref:** Phase 6 Item 7 (audit-only; fixes = Phase 6 Item 8; real-time sync = Phase 6 Item 9).

## Context

Admin panel (`https://admin.auracore.pro`) has accumulated issues over a 9-month build. Known pain points:

- **Bug 2 — Grant Subscription tier sync:** admin grants Pro tier via Subscriptions tab, DB row updates correctly, but Users tab still shows FREE. Root cause hypothesis (confirmed by 6.6 spec code read): `AdminSubscriptionController.Grant` only writes `Licenses.Tier`; Users list reads `Users.Tier`. Dual-source-of-truth.
- **Bug 3 — Refresh data-loss (NEW, cross-tab):** clicking any "Refresh" button across any tab clears all data panel-wide and data does not come back. Likely state/auth invalidation — Refresh forces hard page reload, in-memory JWT + api client reset, session state loss.
- **Rollback artifact risk:** user recalls breaking admin panel while adding security (Nginx basic auth), then rolled back to an older version. Unknown whether features present in source compile-time still function at runtime, or whether the rollback left deployment drift.
- **Mobile access required:** admin panel must be usable from a phone (current responsiveness unknown).
- **Updates tab:** shipped in Phase 6.6.E (commit `1eb42d8`) — new, worth an audit pass but scope should exclude re-spec'ing the design.

## Scope

### In scope — deep audit of 12 tabs

| Group | Tabs |
|---|---|
| MANAGEMENT | Users, Payments, Subscriptions, Licenses, Updates *(Phase 6.6.E)*, Devices |
| ANALYTICS | Crash Reports, Telemetry, Audit Log |
| SYSTEM | IP Whitelist, Configuration, Security *(QR-code 2FA only)* |

### Out of scope — intentionally deferred

- **Bug fixes** — this spec is audit-only. Every finding goes into `docs/admin-audit/findings/{tab}.md`. Batch fix phase is a separate Phase 6 Item 8.
- **Real-time sync infrastructure** (SignalR / SSE / polling) — Phase 6 Item 9, after fix phase.
- **Admin panel rewrite / migration to different framework** — if audit concludes the codebase is beyond saving, that's a user-escalation moment, not in-scope for this audit.
- **Creating new features** — audit maps current state; feature expansion is separate work.
- **Backend API hardening beyond bugs audit surfaces** — audit flags security issues it sees; full pen test is separate.

## Depth of audit (6-axis checklist applied per tab)

Every per-tab audit must cover these six axes explicitly. A section per axis in the findings file; if an axis has zero findings for a tab, mark as `"No findings"`.

### 1. Functional
- List view: search, pagination, sort, filter — each works?
- Create action: form validation, submit path, success state, DB write confirmation
- Update action: form pre-fills, submit path, UI reflects new state without manual refresh
- Delete action: confirmation dialog, DB delete, UI removes row
- Empty state: what renders when list is empty?

### 2. Code + DB sync
- Compare UI-displayed data to actual DB state (psql read-only query)
- After a mutation, does UI re-fetch (invalidate cache) or does it mutate local state optimistically?
- If optimistic: what happens on API error — rollback or stuck?
- Cross-tab impact: if Tab X mutates, does Tab Y reflect when visited?
- Bug 2 style dual-source-of-truth scan: any field rendered from A but updated via B?

### 3. Security
- Every backend endpoint under this tab has `[Authorize(Roles = "admin")]`?
- IDOR: parameters that could be mutated to access other records (tab-specific; single-tenant app so mainly moot, but verify)
- CSRF: mutation endpoints protected (ASP.NET Core antiforgery or stateless JWT-only — document which)
- XSS: user-provided content rendered with React's default escaping? any `dangerouslySetInnerHTML` in this tab?
- SQL injection: any raw SQL in the admin controller? EF parameterized-only?
- Rate limit: mutation endpoints (grant subscription, delete user, ban IP) rate-limited?
- Audit log: admin actions logged with actor + timestamp + before/after state?
- Nginx basic auth bypass: does the tab's API endpoint require basic auth at Nginx layer? What happens with direct `curl` hitting `api.auracore.pro/api/admin/...` without basic auth (it passes because API is on a different vhost — verify)?

### 4. UX
- Loading indicator while data fetches?
- Error state: network error, server error, permission denied — each handled?
- Empty state: friendly message or broken rendering?
- Destructive action confirmation: "Are you sure?" on delete / revoke / ban?
- Toast/inline feedback on success/failure?
- Bug 3 (Refresh data-loss): does this tab survive a manual browser refresh?

### 5. Mobile responsiveness
- Viewport breakpoints to test: 320 (iPhone SE), 375 (iPhone 14 mini), 414 (iPhone 14 Pro Max), 768 (iPad portrait), 1024 (iPad landscape)
- Table reflow: does it horizontal-scroll gracefully or overflow the viewport?
- Tap target size: all interactive elements ≥ 44×44px?
- Modal/form fits in small viewport?
- Sidebar collapses to hamburger below some breakpoint?

### 6. Deployment drift
- Compare `/root/admin-panel/src/app/page.tsx` (source) to `/var/www/admin-panel/index.html` + `_next/static/chunks/*.js` (deployed) — any behavior visible in source but not in live production?
- Any UI elements rendered in live admin panel that don't exist in source (stale deploy)?
- Any features shipped in recent commits but not observable in live UI?
- User recalls breaking admin panel and rolling back — any commits reverted in source that should be?

## Tab audit order (pain-first)

1. **Subscriptions** — primary pain point (Bug 2 tier sync)
2. **Users** — tightly coupled to Subscriptions (tier display consumer)
3. **Licenses** — tier stack foundation (the actual source-of-truth data)
4. **Payments** — financial stack, Stripe + crypto flows, audit carefully
5. **Devices** — device registration + revoke
6. **Updates** — shipped Phase 6.6.E, light audit pass (bugs not design)
7. **Crash Reports** — read-heavy tab, lower priority
8. **Telemetry** — dashboard-style, lower mutation surface
9. **Audit Log** — read-only, verify tab behaves and has useful filter
10. **IP Whitelist** — mutation tab; CRUD + validation focus
11. **Configuration** — feature flags + maintenance mode + environment toggles
12. **Security** — narrow scope: QR-code 2FA activation only

## Deliverables

### Main spec + findings index

`docs/superpowers/specs/2026-04-21-admin-audit-design.md` — THIS file (methodology, scope, tab order, baseline known bugs). Updated as audit progresses with a master findings matrix.

### Per-tab findings files

`docs/admin-audit/findings/{tab-slug}.md` — 12 files, one per tab. Each uses this template:

```markdown
# {Tab} Audit Findings

**Tab:** {Tab name}
**Audit date:** 2026-04-NN
**Auditor:** subagent-{id}
**Source files audited:**
- Frontend: `/root/admin-panel/src/app/page.tsx` L{nn}-L{nn}
- API client: `/root/admin-panel/src/lib/api.ts` L{nn}-L{nn}
- Backend: `src/Backend/AuraCore.API/Controllers/Admin/Admin{Tab}Controller.cs`
- DB entity: `src/Backend/AuraCore.API.Domain/Entities/{Entity}.cs`
**Live test URL:** `https://admin.auracore.pro/{path}`
**Time spent:** N hours

## Summary

- N critical findings
- N high findings
- N medium findings
- N low findings
- Axes covered: functional, code+DB sync, security, UX, mobile, drift

## Findings

### F-1 [SEVERITY] Short title

**Axis:** functional | code-db-sync | security | ux | mobile | drift
**Symptom:** what user experiences
**Reproduction steps:**
1. ...
2. ...
3. Observe: ...

**Expected:** ...
**Actual:** ...

**Root cause:** file:line explanation (e.g., `AdminSubscriptionController.cs:47 — UPDATE writes only Licenses table, misses Users.Tier`)

**DB state (if DB involved):**
```sql
-- Verification query (read-only)
SELECT * FROM users WHERE email = 'baconungabunga@gmail.com';
```
- Actual: `Tier = 'free'`
- Expected: `Tier = 'pro'` (matches licenses row)

**Fix suggestion:** (concrete approach, not implementation — that's fix phase's job)
- Option A: Denormalize — Grant endpoint writes both rows
- Option B: Single source of truth — drop Users.Tier column, always join Licenses

**Risk if unfixed:**
- User-facing: admin cannot tell who is actually Pro vs Free
- Revenue: Pro features may unlock for Free users or vice versa
- Support: repeated "I paid but it says Free" tickets
```

### Severity rubric

| Level | Criteria | Example |
|---|---|---|
| **Critical** | Data loss, auth bypass, payment bug, breaks core admin workflow | Bug 3 refresh data-loss, auth missing on mutation endpoint |
| **High** | Feature broken, data displayed wrong, sync gap | Bug 2 tier sync, delete button no-op |
| **Medium** | UX pain, missing validation, error-handling gap | No confirmation on destructive action, silent API failure |
| **Low** | Code smell, hardcoded string, cosmetic | Magic number, unreachable branch |

## Execution strategy

**Sequential, exhaustive.** User explicitly chose slow+thorough over fast+parallel ("en küçük bir hata/bug ve ya security zafiyetini dahi bulmamız gerek, yavaş olması sorun değil").

- One subagent per tab (12 total subagents across this phase)
- Each subagent reads:
  - THIS spec (methodology + severity rubric)
  - Master findings matrix in this spec (for cross-tab patterns discovered by prior subagents)
  - Prior tab finding files (to learn cross-tab patterns like dual-source-of-truth)
- Model: **Sonnet** (deep code read + security judgment + DB reasoning)

After each subagent:
- Main session reviews finding quality (does severity match impact, are reproduction steps clear, is root cause verified at file:line)
- Updates master findings matrix in this spec
- If finding involves a cross-tab pattern, adds to "Cross-tab patterns" section (so next subagent learns)

### Per-subagent task template

Each subagent gets a self-contained prompt including:
1. The tab name + location references
2. Auth credentials (Nginx basic + admin JWT login — passed via prompt, NOT committed to spec)
3. SSH to `165.227.170.3` with key `~/.ssh/id_ed25519`
4. DB access: read-only psql via ssh tunnel (connection string with `default_transaction_read_only=on` session param)
5. The 6-axis checklist
6. Live test URL + how to reach
7. Output file path: `docs/admin-audit/findings/{tab-slug}.md`

**Write gate:** subagents have no direct channel to the user. If a finding genuinely requires a DB WRITE (INSERT/UPDATE/DELETE/etc.) to reproduce or verify, the subagent must:

1. STOP — do not execute the write
2. Report back to main session with: the exact SQL it wants to execute, why it's necessary, what the rollback looks like if something goes wrong
3. Main session relays this to the user in their next turn
4. User explicitly approves or denies
5. If approved, main session re-dispatches the subagent with explicit permission to run that specific SQL (or runs it inline and shares the result)

Read queries are default and need no approval.

## Logistics

### Auth + access
- **Admin login:** `admin@auracore.pro` account (password in subagent prompt only). Existing account; no test admin creation needed.
- **Nginx basic auth:** `auracore_admin` / <password in subagent prompt>. Required to hit `admin.auracore.pro`.
- **SSH key:** `C:/Users/Admin/.ssh/id_ed25519`. Standing authorization to root@165.227.170.3.
- **Postgres password:** `auracorepro2026` (primary guess; fallback `auracore2026` per user recall).

### Mobile testing
- **Tool:** `mcp__Claude_in_Chrome__*` (Chrome MCP with device emulation).
- **Viewports:** 320 (iPhone SE), 375 (iPhone 14 mini), 414 (iPhone 14 Pro Max), 768 (iPad portrait), 1024 (iPad landscape).
- **Per tab:** subagent loads the tab at 320px first (worst case); finds any overflow / cramped / broken; then verifies at 768px (tablet) and notes whether desktop-to-mobile transition is clean.

### DB access guardrails
- Default: `SET default_transaction_read_only = on;` at session start (Postgres session-level safeguard).
- Any write: subagent STOPS and reports back. User's main-session turn approves or denies. Write command executed only after explicit `yes` in the turn.
- Read queries documented in finding file (reproducibility).

## Baseline known bugs (pre-audit starting list)

These are known before the audit begins. Audit will verify, characterize, and add file:line references. **Do NOT fix**; document only.

| ID | Title | Severity | Axis | Source |
|---|---|---|---|---|
| B-1 | Grant Subscription tier sync gap (Users.Tier not updated) | High | code-db-sync | Phase 6.6 spec § "Related future work" |
| B-2 | Refresh button data-loss (cross-tab) | Critical | ux, functional | User report 2026-04-21 brainstorm |
| B-3 | Stale admin panel after mutations (general) | Medium | code-db-sync | Phase 6.6 spec § "Bug 3" |
| B-4 | Rollback artifacts unknown — user recalls breaking panel adding security, rolled back | Unknown | drift | User report 2026-04-21 brainstorm |

## Findings matrix (live — updated as audit progresses)

| Tab | Auditor | Status | Critical | High | Medium | Low | Findings file |
|---|---|---|---|---|---|---|---|
| Subscriptions | subagent-1 | done | 2 | 3 | 3 | 2 | docs/admin-audit/findings/subscriptions.md |
| Users | subagent-2 | done | 1 | 3 | 3 | 2 | docs/admin-audit/findings/users.md |
| Licenses | subagent-3 | done | 2 | 3 | 3 | 2 | docs/admin-audit/findings/licenses.md |
| Payments | subagent-4 | done | 3 | 3 | 3 | 2 | docs/admin-audit/findings/payments.md |
| Devices | subagent-5 | done | 2 | 3 | 2 | 2 | docs/admin-audit/findings/devices.md |
| Updates | subagent-6 | done | 1 | 1 | 2 | 2 | docs/admin-audit/findings/updates.md |
| Crash Reports | subagent-7 | done | 1 | 3 | 3 | 1 | docs/admin-audit/findings/crash-reports.md |
| Telemetry | subagent-8 | done | 0 | 3 | 1 | 1 | docs/admin-audit/findings/telemetry.md |
| Audit Log | - | pending | - | - | - | - | - |
| IP Whitelist | subagent-10 | done | 3 | 1 | 2 | 1 | docs/admin-audit/findings/ip-whitelist.md |
| Configuration | subagent-11 | done | 1 | 2 | 2 | 2 | docs/admin-audit/findings/configuration.md |
| Security (2FA) | - | pending | - | - | - | - | - |

## Cross-tab patterns (live — updated as audit surfaces patterns)

### CTP-1: TSX reads `u.tier` but API sends `u.license.tier` — universal display bug
**First surfaced:** Subscriptions tab (F-1, F-2). Expected to also appear in: Users tab (confirmed same code), Licenses tab (may read license directly).
**Pattern:** `AdminUserController.GetAll` returns `{ ..., license: { tier, expiresAt } }` — tier is nested. UsersPage TSX reads `u.tier` (undefined) instead of `u.license?.tier`. All tier badges show "free". Revoke button visibility check (`u.tier !== 'free'`) is never true.
**File:line:** `page.tsx:582` (`TierBadge` render), `page.tsx:586` (Revoke button condition).
**Licenses tab check (subagent-3):** Licenses tab reads `l.tier` directly — NOT a second instance of CTP-1. Licenses tab would display tier correctly if the GET endpoint existed.
**Fix:** `AdminUserController.cs` should add a denormalized top-level `tier` field to the user projection (matches `GetById` behavior at line 63), OR frontend changes `u.tier` → `u.license?.tier`.

### CTP-2: Missing audit log for all admin mutations
**First surfaced:** Subscriptions tab (F-5). Expected to also appear in: Users, Licenses, Devices, IP Whitelist, Configuration tabs.
**Confirmed in:** Users tab (F-5 equivalent), Licenses tab (F-8) — 3 tabs confirmed, no exceptions found.
**Pattern:** No admin action is logged to any audit table. `AdminAuditLogController` only reads `login_attempts` — not admin mutations. There is no `admin_audit_log` table.
**Fix:** Add an `admin_audit_log` table + service. Wire into all mutation controllers as a cross-cutting concern (filter or service injection).

### CTP-3: No mobile responsive layout (all tabs affected)
**First surfaced:** Subscriptions tab (axis 5 findings). Expected to apply to ALL 12 tabs.
**Pattern:** Root layout (`page.tsx:1460`) is `flex h-screen overflow-hidden` with no breakpoints. Sidebar is fixed 260px or 72px (no auto-collapse on small screens, no hamburger menu). At ≤375px, content area is ≤115px — completely unusable.
**Fix:** Add a responsive sidebar (auto-collapse below 768px, hamburger toggle at ≤768px). Single fix in root layout applies to all tabs.

### CTP-4: Inconsistent destructive confirmation — Delete has confirm(), Revoke does not
**First surfaced:** Users tab (F-9). Likely applies to: Devices tab (revoke), Licenses tab (revoke/delete), IP Whitelist (delete IP).
**Confirmed in:** Licenses tab (F-7) — Revoke AND Activate have no confirmation. Licenses tab has ZERO confirm() anywhere — worse than Users tab (which at least has confirm on Delete).
**Pattern:** UsersPage Delete uses `if(confirm(...))` guard; UsersPage Revoke has no confirmation dialog. LicensesPage: both Revoke and Activate lack any confirmation. The pattern of "some destructive actions confirmed, others not" will likely repeat across tabs that have mixed CRUD.
**File:line:** `page.tsx:594` (Delete with confirm), `page.tsx:587–590` (Revoke without confirm), `page.tsx:790` (Licenses Revoke, no confirm), `page.tsx:793` (Licenses Activate, no confirm).
**Fix:** Standardize on a confirmation step for every destructive mutation (delete, revoke, ban, disable). Consider a shared `ConfirmModal` component rather than `window.confirm()` for better UX.

### CTP-5: EF Core tracked-entity cascade bug pattern — RemoveRange before ID collection
**First surfaced:** Users tab (F-3). May apply to: Devices tab (`AdminDeviceController.Revoke` if it also cascades), other controllers with cascade delete logic.
**Licenses tab hunt (subagent-3):** NOT found in `AdminLicenseController`. Local repo controller (Create-only) has no RemoveRange. Backup controller (Revoke/Activate) uses single-entity FindAsync + status update — no RemoveRange pattern. Licenses → Device cascade is DB-level (OnDelete.Cascade in EF config), not application-level — immune to this bug.
**Pattern:** `AdminUserController.cs:106–108` removes devices via `RemoveRange`, then line 118 tries to collect device IDs from the same DbContext. EF Core 8 excludes tracked-deleted entities from queries, so the ID list is always empty. CrashReports and TelemetryEvents are silently orphaned.
**Fix:** Collect IDs before calling `RemoveRange`, or configure DB-level `ON DELETE CASCADE` on FK constraints.

### CTP-6: Security rollback stripped controller endpoints — other controllers may also be stubs
**First surfaced:** Licenses tab (F-1, F-3, F-9). The security rollback event (B-4) is now confirmed to have gutted `AdminLicenseController.cs` from 121 lines to 26 lines (Create-only). This is the most visible rollback artifact.
**Risk for other tabs:** The same rollback may have stripped other controllers. Each subsequent subagent should compare the local repo controller to any backups found at `/root/auracore-src-backup-final-202604122153/AuraCore.API/Controllers/Admin/` and verify the live DLL strings contain the expected method names.
**Backup location:** `/root/auracore-src-backup-final-202604122153/AuraCore.API/Controllers/Admin/` — this backup (April 12) predates the rollback-based April 14 DLL. Use `strings /var/www/auracore-api/AuraCore.API.dll | grep AdminXController` to identify which methods survived the rollback.
**Fix:** For each gutted controller: restore the full implementation from the backup, test against the frontend's expected API contract, then rebuild and redeploy.

### CTP-7: Webhook idempotency — payment handlers must check ExternalId before inserting
**First surfaced:** Payments tab (F-5). May also affect: any future webhook-driven payment flow.
**Pattern:** `HandleCheckoutCompleted` and `HandleInvoicePaid` in `StripeController.cs` call `_db.Payments.Add(...)` unconditionally. Stripe guaranteed-delivery retries can fire the same event multiple times. Without an idempotency check, each retry creates a new payment record and re-updates the license. The server backup had this guard (`AnyAsync(p => p.ExternalId == sessionId && p.Status == "completed")`) at line 183–184 — it was lost in the rollback.
**DB constraint gap:** `ExternalId` index is non-unique in EF config AND not even created in the DB (migration did not apply). No DB-level deduplication protection.
**Fix:** Restore the `alreadyProcessed` check from backup. Add `e.HasIndex(p => p.ExternalId).IsUnique()` with partial index for NULLs.

### CTP-8: Frontend hardcoded `$` currency symbol — multi-currency amounts displayed incorrectly
**First surfaced:** Payments tab (F-7). Likely also affects: Dashboard recent payments panel.
**Pattern:** `page.tsx:669` — `${ (p.amount ?? 0).toFixed(2) }` — hardcoded `$` prefix. The API returns `currency` field (USD/TRY/EUR/BTC/USDT); the frontend never reads it. Turkish/European users who paid in TRY or EUR see their payment shown as `$X.XX`.
**Fix:** Replace hardcoded `$` with `Intl.NumberFormat` currency-aware formatting.

### CTP-6 UPDATE: Financial controllers also stripped by rollback
Payments audit confirms CTP-6 extends beyond `AdminLicenseController`. The rollback stripped:
- `StripeController.cs`: 408 lines (backup) → 276 lines (local). Lost: idempotency check, `invoice.payment_failed` handler, `charge.refunded` handler, dispute handlers, structured logging.
- `CryptoController.cs`: 162 lines (backup) → 144 lines (local). Lost: `AdminRejectPayment` endpoint.
- `AdminChartController.cs`: Entire controller missing from local repo and deployed DLL (backup has full implementation).
These are higher-severity than the Licenses rollback because they affect payment processing correctness.

### CTP-6 UPDATE 2: Devices controller also stripped + response shape diverged
Devices audit (subagent-5) confirms CTP-6 for `AdminDeviceController`:
- Backup: 90 lines, 4 endpoints (`List`, `GetById`, `Stats`, `Delete`)
- Local repo: 67 lines (-26%), 2 endpoints (`GetAll`, `GetStats`)
- Stripped from DLL: `GetById` (confirmed 404) + `Delete` (confirmed 404)
- Additionally, the local repo's remaining endpoints diverged in response shape: missing `pages` field (pagination broken), renamed stats fields (all KPI cards show 0), added `HardwareFingerprint` to list (security concern), removed `crashCount`/`telemetryCount` (columns always show 0).
- **Bug 3 (B-2) NOT confirmed on Devices tab** — Refresh button calls `load()` (soft refetch), not `window.location.reload()`.

### CTP-9 (NEW): EF unique index migration gap — composite indexes declared in EF config but absent from production DB
**First surfaced:** Payments tab (ExternalId index missing). Confirmed in Devices tab (`(LicenseId, HardwareFingerprint)` composite unique index missing from DB).
**Pattern:** EF Core `HasIndex(...)` and `HasIndex(...).IsUnique()` declarations exist in `AuraCoreDbContext.cs` but the corresponding migration never created these indexes in the production PostgreSQL database. `pg_indexes` shows only the primary key index for both `payments` and `devices` tables.
**Impact for Devices:** The `(LicenseId, HardwareFingerprint)` composite unique index (`AuraCoreDbContext.cs:59`) is not in the DB. A race condition during device registration allows duplicate device rows (same fingerprint, same license). The application-level guard in `DeviceController.Register` is the only deduplication protection.
**Check for all tabs:** Subsequent auditors should run `SELECT indexname FROM pg_indexes WHERE tablename='{table}';` to verify all EF-declared indexes are present in prod.
**Confirmed in:** app_updates (subagent-6) — `IX_app_updates_Version_Channel_Platform` absent. Root cause: `__EFMigrationsHistory` is empty (0 rows) — DB was bootstrapped via raw DDL, no `dotnet ef database update` was ever run. All EF-declared indexes across all tables are likely absent.
**Fix:** Apply missing migrations or manually create the indexes with `CREATE UNIQUE INDEX ... ON devices ("LicenseId","HardwareFingerprint");` and `CREATE UNIQUE INDEX "IX_app_updates_Version_Channel_Platform" ON app_updates ("Version","Channel","Platform");`

### CTP-6 UPDATE 3: Updates tab is GREENFIELD POST-ROLLBACK (not stripped)
Updates audit (subagent-6) confirms the rollback/deployment pattern for Updates is different from other tabs:
- Backup (April 12): 96-line pre-6.6.E `AdminUpdateController` — simple URL-based Publish + List only. No R2, no GitHub, no PrepareUpload.
- Local repo (6.6.E): 282-line new controller — entirely new code written post-rollback (PrepareUpload + Publish V2 + List + Delete + RetryGitHubMirror).
- Deployed DLL (April 14): pre-6.6.E state — only `Publish V1`, `List`, `Delete` in DLL strings. `IR2Client` and `IGitHubReleaseMirror` absent from DLL.
- The 6.6.E backend was NEVER deployed. The frontend was rebuilt (April 21 chunk). The result is a frontend/backend contract mismatch — the entire upload flow is broken in prod.
- This is NOT a CTP-6 rollback strip. It is a deployment gap: new feature implemented but backend redeploy step skipped.

### CTP-12 (NEW): API contract drift — backend refactor without coordinated frontend rebuild
**First surfaced:** IP Whitelist tab (subagent-10, F-2).
**Pattern:** When a backend controller is refactored (route rename, field name change, response shape change, endpoint additions/removals), the deployed frontend compiled bundle may be months behind. Result: route, body field names, response shape, and endpoint set all diverge simultaneously.
**IP Whitelist specific drift (4-dimensional):**
- Route: frontend sends to `/api/admin/whitelist`; backend serves `/api/admin/ip-whitelist` → 404
- POST body: frontend sends `{ip, label}`; backend expects `{IpAddress, Label}` → silent empty-string binding
- GET response: frontend expects `[{ip, label, addedAt}]` flat array; backend returns `{total, page, pageSize, items: [...]}` paginated envelope
- DELETE key: frontend uses IP string as path param; backend expects Guid → route mismatch
- Extra: `my-ip` endpoint in frontend, absent from backend DLL
**Detection method:** `strings /var/www/auracore-api/AuraCore.API.dll | grep routeHint` vs `grep route /root/admin-panel/src/lib/api.ts`.
**Risk for remaining tabs:** Configuration tab and Security tab (both audited after this) should cross-check their routes.
**Fix:** Frontend rebuild (next.js `npm run build` + deploy to `/var/www/admin-panel/`) after backend route/shape stabilizes. Until then, all four drift dimensions cause silent failures.

Section previously filled with expected categories (retained for reference):
- Dual-source-of-truth fields (Bug 2 pattern) → subsumed by CTP-1
- Stale-after-mutation UI (Bug 3 pattern, possibly >1 tab) — Bug 3 NOT confirmed on Users tab Refresh (soft refetch); NOT confirmed on Licenses tab Refresh (same soft refetch pattern); still to verify in other tabs
- Missing audit logging → CTP-2
- Inconsistent confirmation dialogs → CTP-4
- Deploy drift (source-vs-live divergence) — confirmed in Users (F-8, same 26-day gap as Subscriptions F-9)
- Mobile table overflow pattern → CTP-3
- EF Core cascade delete bug → CTP-5

## Non-goals

- No scope creep into fixes. Audit surfaces findings; batch fix phase (6 Item 8) consumes them.
- No new tests added during audit (audit adds findings, not test coverage).
- No refactor suggestions unless they're the only viable fix for a critical/high finding.
- No penetration testing of non-admin endpoints (this audit is scoped to admin panel).

## Success criteria (audit is DONE when)

- All 12 tabs have a completed `docs/admin-audit/findings/{tab}.md` file with at least the 6 axes covered (or "no findings" noted)
- Findings matrix in this spec is fully populated
- Cross-tab patterns section has ≥ 2 patterns documented (or note "no cross-tab patterns found")
- Baseline known bugs (B-1 through B-4) are cross-referenced to specific finding IDs in their tabs
- Main session has reviewed all 12 findings files for completeness, severity calibration, and reproducibility

**Spec end.** Implementation plan next — `writing-plans` skill invocation.
