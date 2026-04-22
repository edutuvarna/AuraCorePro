# Admin Panel Deep Audit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. This is an AUDIT plan — deliverables are findings markdown files, not code.

**Goal:** Produce a deep, exhaustive bug/security/UX audit of all 12 admin panel tabs at `https://admin.auracore.pro`, with each tab's findings documented in a standalone markdown file, ready to feed the subsequent batch-fix phase (Phase 6 Item 8).

**Architecture:** Sequential subagent dispatch, one subagent per tab. Each subagent: reads authoritative source at `/root/admin-panel/src/` on the origin server via SSH, reads backend controller in this repo, hits the live admin panel with Chrome MCP + device emulation, runs read-only psql queries against prod DB for state verification, produces `docs/admin-audit/findings/{tab}.md` per the spec's template.

**Tech Stack:** SSH (`~/.ssh/id_ed25519`), Postgres psql via ssh-tunnel (port-forward 5432, read-only session), `mcp__Claude_in_Chrome__*` for live UI + viewport emulation, Next.js 13+ App Router (TSX source at `/root/admin-panel/src/app/page.tsx`), ASP.NET Core 8 backend controllers at `src/Backend/AuraCore.API/Controllers/Admin/`.

**Spec:** `docs/superpowers/specs/2026-04-21-admin-audit-design.md`

---

## Pre-flight — resolve BEFORE Task 0

1. **Credentials** (NEVER commit to repo):
   - Admin login: `admin@auracore.pro` / `<password-from-user-turn>`
   - Nginx basic auth: `auracore_admin` / `<same-password>`
   - Postgres: try `auracorepro2026` first, fallback `auracore2026`
2. **SSH target:** `root@165.227.170.3`, key at `~/.ssh/id_ed25519`
3. **Authoritative source path on origin:** `/root/admin-panel/src/`
4. **Deployed static export on origin:** `/var/www/admin-panel/`
5. **Postgres on origin:** 127.0.0.1:5432 (bound to loopback; requires SSH tunnel)

---

## File structure

**Created by this plan:**
- `docs/admin-audit/findings/subscriptions.md`
- `docs/admin-audit/findings/users.md`
- `docs/admin-audit/findings/licenses.md`
- `docs/admin-audit/findings/payments.md`
- `docs/admin-audit/findings/devices.md`
- `docs/admin-audit/findings/updates.md`
- `docs/admin-audit/findings/crash-reports.md`
- `docs/admin-audit/findings/telemetry.md`
- `docs/admin-audit/findings/audit-log.md`
- `docs/admin-audit/findings/ip-whitelist.md`
- `docs/admin-audit/findings/configuration.md`
- `docs/admin-audit/findings/security-2fa.md`

**Modified by this plan:**
- `docs/superpowers/specs/2026-04-21-admin-audit-design.md` — findings matrix (lines ~220-240) updated after each tab audit with counts + cross-tab patterns (lines ~245-260) updated when patterns emerge.

**NOT created:** no code changes, no test changes, no build artifact changes. This is a read-only audit.

---

## Shared audit protocol (applies to Tasks 1-12)

Every per-tab audit task uses this protocol. Do NOT duplicate this protocol into each task — each task references it with "Follow the Shared Audit Protocol" plus tab-specific additions.

### Protocol — each subagent MUST do all of these

#### A. Context load (before touching any files)

Read in order:
1. `docs/superpowers/specs/2026-04-21-admin-audit-design.md` — full methodology + severity rubric + baseline known bugs B-1..B-4
2. All prior `docs/admin-audit/findings/*.md` files in commit order (so cross-tab patterns accumulate)
3. The `Cross-tab patterns` section of the spec (auto-updated by main session after each tab)

#### B. Source code read

Via SSH to `root@165.227.170.3`:

```bash
ssh -i ~/.ssh/id_ed25519 -o StrictHostKeyChecking=no root@165.227.170.3
cd /root/admin-panel/src/app
# Read page.tsx — find the specific tab component section for your assigned tab
# Record: line range of tab function, all imports, all state hooks, all api calls
cat page.tsx | sed -n '<FROM>,<TO>p'
# Also read api.ts for the tab's methods
cat /root/admin-panel/src/lib/api.ts | grep -A 5 -B 1 '<tab-method-pattern>'
```

Also read (locally in this repo):
- `src/Backend/AuraCore.API/Controllers/Admin/Admin{Tab}Controller.cs`
- Relevant entity: `src/Backend/AuraCore.API.Domain/Entities/{Entity}.cs`
- DbContext config: `src/Backend/AuraCore.API.Infrastructure/Data/AuraCoreDbContext.cs` for the entity's table

#### C. Deployment drift check

Compare source vs live:
```bash
ssh ... root@165.227.170.3 "ls -la /var/www/admin-panel/_next/static/chunks/ | head -20"
ssh ... root@165.227.170.3 "diff -r /root/admin-panel/out/ /var/www/admin-panel/ | head -20"
```
Note any divergence. If `out/` doesn't exist on server, note it (means no recent rebuild; deployed version may be stale vs source).

Also:
```bash
# Check git log on the /root/admin-panel/ directory if it's a git repo
ssh ... root@165.227.170.3 "cd /root/admin-panel && git log --oneline -5 2>/dev/null || echo 'Not a git repo — rollback history only in bak dirs'"
ssh ... root@165.227.170.3 "ls /root/ | grep admin-backup"
```

#### D. Live behavior test

Via `mcp__Claude_in_Chrome__*`:

1. Navigate to `https://admin.auracore.pro` (Chrome MCP will prompt for basic auth — enter `auracore_admin` + password)
2. App-level login: email `admin@auracore.pro` + same password
3. Navigate to the assigned tab in the sidebar
4. Exercise every action: list refresh, search, pagination, sort, each CRUD operation
5. Capture screenshots at: 1024 (desktop baseline), 768 (tablet), 375 (mobile), 320 (iPhone SE)
6. Observe network tab in devtools for each mutation — record: endpoint hit, payload, response status

#### E. DB state verification (read-only)

Set up the SSH tunnel:
```bash
# In a backgroundable shell:
ssh -i ~/.ssh/id_ed25519 -L 5432:localhost:5432 -N root@165.227.170.3 &
# Wait a few seconds for tunnel
# Connect as read-only (or plain user if no read-only role exists)
PGPASSWORD='auracorepro2026' psql -h localhost -U auracore -d auracore -c "SET default_transaction_read_only = on; SELECT version();"
```

(If `auracorepro2026` fails, try `auracore2026`. If both fail, report BLOCKED and ask user.)

Run queries to verify UI-displayed data matches DB truth. Example for Subscriptions tab:
```sql
SELECT u."Email", u."Tier" AS user_tier, l."Tier" AS license_tier
FROM users u
LEFT JOIN licenses l ON l."UserId" = u."Id" AND l."Status" = 'active'
WHERE u."Email" = 'baconungabunga@gmail.com';
```

Compare `user_tier` vs `license_tier`. Mismatch = B-1 pattern confirmed.

**WRITE GATE:** subagents cannot talk to the user directly. If a finding requires a DB WRITE (`INSERT/UPDATE/DELETE/TRUNCATE/ALTER/DROP`) to reproduce or verify, subagent MUST:

1. NOT execute the write
2. Report back to main session with: exact SQL, reason, rollback plan
3. Main session surfaces the request to the user
4. User approves or denies in their next turn
5. Main session re-dispatches (or inline-executes) only if approved

Read queries are always fine with no approval flow.

#### F. 6-axis analysis

Apply the full checklist from spec § "Depth of audit":
1. Functional
2. Code + DB sync
3. Security (auth, IDOR, CSRF, XSS, SQL injection, rate limit, audit log)
4. UX (loading/error/empty/confirmation states + Bug 3 refresh test)
5. Mobile (5 breakpoints)
6. Deployment drift

For each axis, document findings OR explicitly write "No findings for this axis".

#### G. Write findings file

File path: `docs/admin-audit/findings/{tab-slug}.md`

Template (verbatim — copy this structure, fill in content):

```markdown
# {Tab} Audit Findings

**Tab:** {Tab display name}
**URL path:** `https://admin.auracore.pro/{path}`
**Audit date:** 2026-04-{DD}
**Auditor:** subagent-{id}
**Time spent:** {N} hours

## Source files audited

- Frontend TSX: `/root/admin-panel/src/app/page.tsx` lines {from}-{to}
- Frontend API client: `/root/admin-panel/src/lib/api.ts` lines {from}-{to}
- Backend controller: `src/Backend/AuraCore.API/Controllers/Admin/Admin{Tab}Controller.cs`
- Backend entity: `src/Backend/AuraCore.API.Domain/Entities/{Entity}.cs`
- DbContext config: `src/Backend/AuraCore.API.Infrastructure/Data/AuraCoreDbContext.cs` lines {from}-{to}

## Summary

- **{N} critical** — {one-line summary}
- **{N} high** — {one-line summary}
- **{N} medium** — {one-line summary}
- **{N} low** — {one-line summary}

Axes covered: functional, code+DB sync, security, UX, mobile, drift.

## Findings

### F-1 [CRITICAL|HIGH|MEDIUM|LOW] {Short title}

**Axis:** functional | code-db-sync | security | ux | mobile | drift
**Baseline bug ref (if any):** B-1 / B-2 / B-3 / B-4
**Symptom:** what the user experiences (1-2 sentences)

**Reproduction steps:**
1. Log in as `admin@auracore.pro`
2. Navigate to ...
3. Click ...
4. Observe: ...

**Expected behavior:** ...

**Actual behavior:** ...

**Root cause:** (file:line anchored)
- `Admin{Tab}Controller.cs:{line}` — {explanation}
- `/root/admin-panel/src/app/page.tsx:{line}` — {explanation}

**DB state verification** (if DB involved):
```sql
-- Query used (read-only)
SELECT ... FROM ... WHERE ...;
```
- Actual result: `...`
- Expected result (if feature worked): `...`

**Fix suggestion:** (concrete approach, not implementation — fix phase will decide)
- Option A: ...
- Option B: ...

**Risk if unfixed:**
- User-facing: ...
- Data integrity: ...
- Support burden: ...

---

### F-2 ...

{Repeat the F-1 structure for each finding}

---

## Axis-by-axis coverage

### 1. Functional
- List/search/pagination/sort: {status}
- Create action: {status}
- Update action: {status}
- Delete action: {status}
- Empty state: {status}

### 2. Code + DB sync
- {status / findings refs}

### 3. Security
- Authorization check on every endpoint: {status}
- IDOR: {status}
- CSRF: {status}
- XSS: {status}
- SQL injection: {status}
- Rate limit: {status}
- Audit log: {status}
- Nginx basic auth bypass: {status}

### 4. UX
- Loading states: {status}
- Error states: {status}
- Empty states: {status}
- Destructive confirmation: {status}
- Refresh survival (Bug 3): {status}

### 5. Mobile
- 1024px (desktop baseline): {screenshot reference} {findings}
- 768px (tablet): {screenshot reference} {findings}
- 414px: {findings}
- 375px: {findings}
- 320px (iPhone SE): {findings}

### 6. Deployment drift
- Source vs deployed diff: {summary}
- Rollback artifact evidence: {summary}

## Questions for user (if any)

- {Any decisions that require user input before a fix can be designed}
```

#### H. Self-check before reporting

Before sending the report back, subagent verifies:
- [ ] All 6 axes have a section (even if content is "No findings")
- [ ] Every finding has reproduction steps + root cause file:line + fix suggestion + risk
- [ ] Severity matches rubric (no inflation, no deflation)
- [ ] If any write query was needed, it was NOT executed — user gate triggered instead
- [ ] Findings file committed to branch
- [ ] Report back ≤ 400 words with: critical/high/medium/low counts, 1-2 standout findings, any BLOCKED items

---

## Task 0: Pre-flight verification (main session inline, NOT a subagent task)

**Goal:** Before dispatching any audit subagent, verify every tool, credential, and connection works.

**Files:**
- Create: `docs/admin-audit/` directory
- Modify: none

- [ ] **Step 1: Verify SSH access + get server state**

```bash
ssh -i C:/Users/Admin/.ssh/id_ed25519 -o StrictHostKeyChecking=no root@165.227.170.3 "hostname && uptime && ls /root/admin-panel/src/app/page.tsx"
```
Expected: hostname, uptime line, page.tsx lists. If ls fails → source path assumption wrong; STOP.

- [ ] **Step 2: Verify Nginx basic auth + app-level admin login**

Via `mcp__Claude_in_Chrome__navigate`:
```
URL: https://admin.auracore.pro
# Browser prompts for basic auth: auracore_admin + <password>
# Then the Next.js app loads; log in with admin@auracore.pro + <password>
```

Confirm: sidebar with all 12 tabs visible. If login fails → credential assumption wrong; STOP and ask user.

- [ ] **Step 3: Set up read-only Postgres tunnel + verify connection**

In a background shell:
```bash
ssh -i C:/Users/Admin/.ssh/id_ed25519 -L 5432:localhost:5432 -N -f root@165.227.170.3
PGPASSWORD='auracorepro2026' psql -h localhost -U auracore -d auracore -c "SELECT COUNT(*) FROM users;"
```
Expected: returns a row count. If password wrong, try `auracore2026`. If both fail, STOP and ask user.

- [ ] **Step 4: Create findings directory**

```bash
cd "C:/Users/Admin/Desktop/AuraCorePro/AuraCorePro"
mkdir -p docs/admin-audit/findings
touch docs/admin-audit/findings/.gitkeep
```

- [ ] **Step 5: Capture baseline screenshot of each tab at 1024px (desktop)**

Via `mcp__Claude_in_Chrome__resize_window` (1024×768) + navigate each tab + `mcp__Claude_in_Chrome__screenshot` stored to `docs/admin-audit/baseline-screenshots/{tab}-desktop.png`. Gives us a before-picture for drift detection later.

(If baseline screenshots are too heavy to store, skip this step and rely on subagents' per-tab screenshots. Decide inline.)

- [ ] **Step 6: Commit directory scaffolding**

```bash
git add docs/admin-audit/
git commit -m "chore(audit): scaffold docs/admin-audit/findings/ directory for Phase 6 Item 7"
```

---

## Task 1: Audit Subscriptions tab (primary pain — Bug 2)

**Files:**
- Create: `docs/admin-audit/findings/subscriptions.md`
- Modify: `docs/superpowers/specs/2026-04-21-admin-audit-design.md` (findings matrix row)

**Tab location:** `/root/admin-panel/src/app/page.tsx` — search for `SubscriptionsPage` function (or similar). Per 6.6 Explore report, was at lines 536-607 or adjacent — exact range may have drifted.

**Backend:** `src/Backend/AuraCore.API/Controllers/Admin/AdminSubscriptionController.cs`

**Entity:** `src/Backend/AuraCore.API.Domain/Entities/Subscription.cs` + `License.cs` (dual-source-of-truth axis)

**Known applicable bugs:** B-1 (tier sync), B-2 (refresh data-loss), B-3 (stale after mutation)

- [ ] **Step 1: Follow Shared Audit Protocol sections A through G**

Subagent executes the full protocol. Specific things this tab MUST verify:
- B-1 reproduction: grant Pro to a test user, watch both `users.Tier` and `licenses.Tier` in DB. Document which updates and which doesn't. File:line where the divergence happens.
- Revoke subscription path: does it also have the same dual-source issue (license status changes but user.Tier stays Pro)?
- Grant form: `UserId` field is a GUID text input — is there a user search/lookup? Or must admin manually type the GUID? (If manual, cross-ref to 6.6.E's UsersPage GUID column — is the workflow admin → Users tab → click-to-copy → paste into Subscriptions form? Or is there a better path?)

- [ ] **Step 2: Main session reviews subscriptions.md**

Main session verifies:
- B-1 is documented with file:line and DB verification
- Reproduction steps work (if unsure, re-run steps manually via Chrome MCP)
- Severity rubric matches impact

- [ ] **Step 3: Update findings matrix in spec**

Main session edits `docs/superpowers/specs/2026-04-21-admin-audit-design.md` matrix table:

```
| Subscriptions | subagent-1 | done | N | N | N | N | docs/admin-audit/findings/subscriptions.md |
```

- [ ] **Step 4: Update cross-tab patterns (if applicable)**

If B-1 confirmed as a dual-source pattern, add to `Cross-tab patterns` section:

```
### CTP-1: Dual-source-of-truth tier display
First surfaced: subscriptions tab (F-X). Expected to also appear in: users tab, licenses tab.
Pattern: {Users.Tier} rendered for admin UI, but {Licenses.Tier} is the source of truth.
```

- [ ] **Step 5: Commit**

```bash
git add docs/admin-audit/findings/subscriptions.md docs/superpowers/specs/2026-04-21-admin-audit-design.md
git commit -m "audit(subscriptions): deep audit — {N} findings ({critical/high/medium/low})

Primary finding: B-1 tier sync dual-source-of-truth confirmed at
AdminSubscriptionController.cs:{line}. Grant endpoint updates
Licenses.Tier but not Users.Tier; UsersPage reads from Users.Tier.
{Other key findings summarized in 1-2 bullets}."
```

---

## Task 2: Audit Users tab

**Files:**
- Create: `docs/admin-audit/findings/users.md`
- Modify: `docs/superpowers/specs/2026-04-21-admin-audit-design.md` (matrix + patterns if needed)

**Tab location:** `/root/admin-panel/src/app/page.tsx` — search for `UsersPage` function (was at lines 536-607 per 6.6 Explore + the GUID column added in 6.6.E)

**Backend:** `src/Backend/AuraCore.API/Controllers/Admin/AdminUserController.cs`

**Entity:** `src/Backend/AuraCore.API.Domain/Entities/User.cs`

**Known applicable bugs:** B-1 (tier display), B-2 (refresh), B-3 (stale)

- [ ] **Step 1: Follow Shared Audit Protocol sections A through G**

Tab-specific emphasis:
- **Tier column** — confirm it reads `Users.Tier` directly (not joined Licenses). If so, this is the "wrong side" of Bug 2.
- **Role column** — `user` vs `admin` — can admin edit role? If yes, is it safe (self-demotion lockout)?
- **Disable / Delete actions** — destructive, verify confirmation + audit log.
- **Search by email** — SQL injection via search box? EF parameterized?
- **GUID column (6.6.E addition)** — clipboard copy works at all viewport sizes? Tap target size on mobile?
- **Pagination** — 25 per page per 6.6 Explore; verify edge cases (page N+1 with <25 users).

- [ ] **Step 2: Main session reviews users.md**

Verify:
- Tier column source is explicitly documented (confirm B-1 pattern from other side)
- Role mutation flow audited — self-demotion prevented?
- Pagination edge cases tested

- [ ] **Step 3: Update matrix + patterns**

If B-1 confirmed, mark `CTP-1: Dual-source-of-truth tier display` as "Observed in subscriptions + users" in the spec.

- [ ] **Step 4: Commit**

```bash
git add docs/admin-audit/findings/users.md docs/superpowers/specs/2026-04-21-admin-audit-design.md
git commit -m "audit(users): deep audit — {N} findings ({breakdown})

{1-2 line standout summary}"
```

---

## Task 3: Audit Licenses tab

**Files:**
- Create: `docs/admin-audit/findings/licenses.md`

**Tab location:** `/root/admin-panel/src/app/page.tsx` — search for `LicensesPage` function

**Backend:** `src/Backend/AuraCore.API/Controllers/Admin/AdminLicenseController.cs`

**Entity:** `src/Backend/AuraCore.API.Domain/Entities/License.cs` — this IS the source of truth for tier per 6.6 findings

**Known applicable bugs:** B-1 (source side), B-2, B-3

- [ ] **Step 1: Follow Shared Audit Protocol sections A through G**

Tab-specific emphasis:
- **License key column** — is the full key rendered or masked? (Security: full license keys visible in admin panel = data-at-rest concern if admin panel is compromised)
- **Tier column** — confirm this reads `Licenses.Tier` (the source of truth)
- **Status column** — active/revoked/expired — toggling flow works?
- **Max devices** — editable? DB validation on negative values?
- **User FK** — click-through to user detail? Does link navigate correctly?

- [ ] **Step 2: Main session reviews licenses.md**

- [ ] **Step 3: Update matrix + patterns**

- [ ] **Step 4: Commit**

```bash
git add docs/admin-audit/findings/licenses.md docs/superpowers/specs/2026-04-21-admin-audit-design.md
git commit -m "audit(licenses): deep audit — {N} findings"
```

---

## Task 4: Audit Payments tab

**Files:**
- Create: `docs/admin-audit/findings/payments.md`

**Tab location:** `/root/admin-panel/src/app/page.tsx` — search for `PaymentsPage` function

**Backend:** `src/Backend/AuraCore.API/Controllers/Admin/AdminPaymentController.cs` (may exist — verify) + Stripe webhook endpoints + crypto payment flow

**Entity:** `src/Backend/AuraCore.API.Domain/Entities/Payment.cs`

**Known applicable bugs:** B-2, B-3 (no specific payment bug known pre-audit — but financial data = highest risk)

- [ ] **Step 1: Follow Shared Audit Protocol sections A through G**

Tab-specific emphasis (financial caution):
- **Amount column** — displayed currency matches stored currency? Rounding?
- **Status column** — pending/succeeded/failed/refunded — all renderable? What about disputes?
- **Provider column** — Stripe vs crypto — both flows rendered consistently?
- **Crypto tx hash** — displayed full? link to blockchain explorer?
- **Refund action** (if present) — destructive, irreversible; confirmation flow?
- **Manual grant after crypto payment** — any admin action that could double-credit a user?
- **Reconciliation** — admin can verify Stripe webhook didn't miss a payment?
- **Data mask** — is full crypto address / card-last-4 / customer email displayed to admin? Sensitive.

- [ ] **Step 2: Main session reviews payments.md — EXTRA SCRUTINY for financial**

- [ ] **Step 3: Update matrix + patterns**

- [ ] **Step 4: Commit**

```bash
git add docs/admin-audit/findings/payments.md docs/superpowers/specs/2026-04-21-admin-audit-design.md
git commit -m "audit(payments): deep audit — {N} findings"
```

---

## Task 5: Audit Devices tab

**Files:**
- Create: `docs/admin-audit/findings/devices.md`

**Tab location:** search for `DevicesPage`

**Backend:** `src/Backend/AuraCore.API/Controllers/Admin/AdminDeviceController.cs`

**Entity:** `src/Backend/AuraCore.API.Domain/Entities/Device.cs`

- [ ] **Step 1: Follow Shared Audit Protocol sections A through G**

Tab-specific emphasis:
- **HardwareFingerprint column** — rendered full or hashed?
- **MachineName + OsVersion** — user-supplied data; XSS vector if rendered with `dangerouslySetInnerHTML`
- **LastSeenAt** — how is "stale" defined? (>30 days offline?)
- **Revoke action** — does it cascade (device removed from license's device count)?
- **Max-devices-exceeded cases** — how does admin see over-count? Is there remediation UI?

- [ ] **Step 2: Main session reviews devices.md**

- [ ] **Step 3: Update matrix + patterns**

- [ ] **Step 4: Commit**

```bash
git add docs/admin-audit/findings/devices.md docs/superpowers/specs/2026-04-21-admin-audit-design.md
git commit -m "audit(devices): deep audit — {N} findings"
```

---

## Task 6: Audit Updates tab (6.6.E implementation — light pass)

**Files:**
- Create: `docs/admin-audit/findings/updates.md`

**Tab location:** `/root/admin-panel/src/app/page.tsx` — `UpdatesPage` function (rewritten in 6.6.E commit `1eb42d8`)

**Backend:** `src/Backend/AuraCore.API/Controllers/Admin/AdminUpdateController.cs` (6.6.C)

**Entity:** `src/Backend/AuraCore.API.Domain/Entities/AppUpdate.cs` (6.6.A)

- [ ] **Step 1: Follow Shared Audit Protocol sections A through G — but LIGHT PASS**

This tab was just shipped. Audit is for bugs I might have missed, not design critique. Focus:
- **R2 upload flow** — prepare-upload + PUT + publish sequence; any failure mode that leaves orphaned `pending/` files?
- **GitHub mirror retry button** — actually works? What error message on failure?
- **Invalid Date fix (fmtDate)** — confirm it renders legitimate dates correctly AND handles edge cases (null, empty string, malformed)?
- **Mandatory toggle** — warning text rendering correctly? does admin understand the implication?
- **macOS "Coming Soon" disabled checkbox** — does state persist if admin accidentally clicks it?
- **Cross-check with 6.6.H ops doc** — are the Known Runtime Gaps in production? (especially: signatureHash DB invariant, are there any v1.x rows with empty hash causing real update failures?)

- [ ] **Step 2: Main session reviews updates.md**

- [ ] **Step 3: Update matrix + patterns**

- [ ] **Step 4: Commit**

```bash
git add docs/admin-audit/findings/updates.md docs/superpowers/specs/2026-04-21-admin-audit-design.md
git commit -m "audit(updates): light audit of 6.6.E implementation — {N} findings"
```

---

## Task 7: Audit Crash Reports tab

**Files:**
- Create: `docs/admin-audit/findings/crash-reports.md`

**Tab location:** search for `CrashReportsPage`

**Backend:** `src/Backend/AuraCore.API/Controllers/Admin/AdminCrashReportController.cs`

**Entity:** `src/Backend/AuraCore.API.Domain/Entities/CrashReport.cs`

- [ ] **Step 1: Follow Shared Audit Protocol sections A through G**

Tab-specific:
- **StackTrace column** — rendered with `<pre>` (preserving formatting) or unescaped HTML?
- **SystemInfo jsonb** — parsed to key-value display or raw JSON dump?
- **Pagination** — crash reports accumulate fast; how many per page?
- **Filter by AppVersion / ExceptionType** — are filters present? SQL injection vector?
- **Delete / bulk-delete** — does the UI support housekeeping? (long-term storage concern)
- **Large stack traces** — does the UI truncate or blow up the layout?

- [ ] **Step 2: Main session reviews crash-reports.md**

- [ ] **Step 3: Update matrix + patterns**

- [ ] **Step 4: Commit**

```bash
git add docs/admin-audit/findings/crash-reports.md docs/superpowers/specs/2026-04-21-admin-audit-design.md
git commit -m "audit(crash-reports): deep audit — {N} findings"
```

---

## Task 8: Audit Telemetry tab

**Files:**
- Create: `docs/admin-audit/findings/telemetry.md`

**Tab location:** search for `TelemetryPage`

**Backend:** `src/Backend/AuraCore.API/Controllers/Admin/AdminTelemetryController.cs`

**Entity:** `src/Backend/AuraCore.API.Domain/Entities/TelemetryEvent.cs`

- [ ] **Step 1: Follow Shared Audit Protocol sections A through G**

Tab-specific:
- **Dashboard vs tabular** — which is it? Charts rendered by Recharts?
- **Date range selector** — present? default range (last 7 days)?
- **Event type aggregation** — top-N events displayed?
- **Per-device telemetry** — is it exposed in admin view or only aggregated?
- **Event data jsonb** — rendered or hidden (privacy)?
- **Large-volume performance** — does the tab lag when telemetry table has millions of rows?

- [ ] **Step 2: Main session reviews telemetry.md**

- [ ] **Step 3: Update matrix + patterns**

- [ ] **Step 4: Commit**

```bash
git add docs/admin-audit/findings/telemetry.md docs/superpowers/specs/2026-04-21-admin-audit-design.md
git commit -m "audit(telemetry): deep audit — {N} findings"
```

---

## Task 9: Audit Audit Log tab

**Files:**
- Create: `docs/admin-audit/findings/audit-log.md`

**Tab location:** search for `AuditLogPage`

**Backend:** find whatever controller serves audit log (maybe `AdminAuditLogController` — verify existence)

**Entity:** may or may not exist as a dedicated table. If LoginAttempt is repurposed as audit log, that's worth flagging. If there's no audit log table at all, that's a critical finding (admin actions not logged).

- [ ] **Step 1: Follow Shared Audit Protocol sections A through G**

Tab-specific:
- **Does the audit log table actually exist?** If not → CRITICAL finding (admin actions unloggable)
- **Are admin actions from other tabs (tier grant, user disable, etc.) actually written to audit log?** Cross-check by doing an action then querying the log.
- **Tampering** — can admin delete their own audit log entries?
- **Filter + search** — by actor / by action type / by date range
- **Retention policy** — how long are entries kept?

- [ ] **Step 2: Main session reviews audit-log.md — EXTRA SCRUTINY for auditability**

- [ ] **Step 3: Update matrix + patterns**

If audit logging is widely missing, add to `Cross-tab patterns` section:
```
### CTP-N: Missing audit log coverage
{summary of which admin actions are NOT logged}
```

- [ ] **Step 4: Commit**

```bash
git add docs/admin-audit/findings/audit-log.md docs/superpowers/specs/2026-04-21-admin-audit-design.md
git commit -m "audit(audit-log): deep audit — {N} findings"
```

---

## Task 10: Audit IP Whitelist tab

**Files:**
- Create: `docs/admin-audit/findings/ip-whitelist.md`

**Tab location:** search for `IpWhitelistPage`

**Backend:** `src/Backend/AuraCore.API/Controllers/Admin/AdminIpWhitelistController.cs`

**Entity:** `src/Backend/AuraCore.API.Domain/Entities/IpWhitelist.cs`

- [ ] **Step 1: Follow Shared Audit Protocol sections A through G**

Tab-specific:
- **IP format validation** — IPv4, IPv6, CIDR?
- **Add own IP button** — detects caller IP via `X-Forwarded-For`? Spoofable?
- **Lockout protection** — if admin removes own IP from whitelist while whitelist is enforced, are they locked out of admin panel? Safeguard?
- **Label field** — free-text, XSS vector on render?
- **Duplicate prevention** — unique index on IP (per entity)?

- [ ] **Step 2: Main session reviews ip-whitelist.md**

- [ ] **Step 3: Update matrix + patterns**

- [ ] **Step 4: Commit**

```bash
git add docs/admin-audit/findings/ip-whitelist.md docs/superpowers/specs/2026-04-21-admin-audit-design.md
git commit -m "audit(ip-whitelist): deep audit — {N} findings"
```

---

## Task 11: Audit Configuration tab

**Files:**
- Create: `docs/admin-audit/findings/configuration.md`

**Tab location:** search for `ConfigurationPage`

**Backend:** `src/Backend/AuraCore.API/Controllers/Admin/AdminConfigController.cs`

**Entity:** `src/Backend/AuraCore.API.Domain/Entities/AppConfig.cs`

- [ ] **Step 1: Follow Shared Audit Protocol sections A through G**

Tab-specific:
- **Feature flags** — each flag clearly labeled? toggling has real effect (test one)?
- **Maintenance mode toggle** — when ON, does API actually block non-admin requests? Test via curl with non-admin JWT.
- **Maintenance message** — rendered to end-users? XSS vector if not escaped?
- **New registrations toggle** — when OFF, does registration endpoint actually reject?
- **Telemetry enabled / Crash reports enabled** — respected server-side?
- **Auto-update enabled** — respected by desktop client? (cross-cuts with 6.6.G UpdateChecker)
- **Singleton row** — DB has only one AppConfig row (Id=1). What if someone INSERTs a second? Frontend behavior?
- **Accidental-maintenance-mode footgun** — is there a confirmation dialog before enabling? Rollback path if admin locks themselves out?

- [ ] **Step 2: Main session reviews configuration.md — EXTRA SCRUTINY for lockout footguns**

- [ ] **Step 3: Update matrix + patterns**

- [ ] **Step 4: Commit**

```bash
git add docs/admin-audit/findings/configuration.md docs/superpowers/specs/2026-04-21-admin-audit-design.md
git commit -m "audit(configuration): deep audit — {N} findings"
```

---

## Task 12: Audit Security (2FA) tab

**Files:**
- Create: `docs/admin-audit/findings/security-2fa.md`

**Tab location:** search for `SecurityPage` or `TwoFactorPage`

**Backend:** likely `AuthController` or a dedicated `TotpController` (find via grep)

**Entity:** User entity has `TotpSecret` + `TotpEnabled` columns already (per DbContext line 33)

- [ ] **Step 1: Follow Shared Audit Protocol sections A through G**

Tab-specific (narrow scope per spec — QR code 2FA activation ONLY):
- **QR code generation** — library used? secret randomness source?
- **Backup codes** — generated and displayed? admin-printable?
- **Recovery flow** — if admin loses device, how to disable 2FA? (DB manual? support ticket?)
- **Enforcement** — does login actually REQUIRE TOTP when `TotpEnabled = true`? Test with correct password + wrong TOTP.
- **Brute force** — TOTP endpoint rate-limited?
- **Secret storage** — `TotpSecret` column — encrypted at rest? Plaintext in DB?
- **Same-device enrollment** — can admin enroll AND use TOTP on same device? That defeats the second-factor purpose. Is there a warning?

- [ ] **Step 2: Main session reviews security-2fa.md — EXTRA SCRUTINY**

- [ ] **Step 3: Update matrix + patterns**

- [ ] **Step 4: Commit**

```bash
git add docs/admin-audit/findings/security-2fa.md docs/superpowers/specs/2026-04-21-admin-audit-design.md
git commit -m "audit(security-2fa): deep audit — {N} findings"
```

---

## Task 13: Cross-tab pattern consolidation + findings triage

**Goal:** With all 12 tabs audited, synthesize patterns and produce a triage-ready summary.

**Files:**
- Modify: `docs/superpowers/specs/2026-04-21-admin-audit-design.md` (Cross-tab patterns section becomes comprehensive; Success criteria checklist ticked off)
- Create: `docs/admin-audit/triage.md` — prioritized bug list for the batch fix phase

- [ ] **Step 1: Subagent reads all 12 findings files + the spec**

Dispatched subagent task: "Read all 12 finding files. Identify cross-tab patterns. Produce a triage document ranking all findings by (severity × impact × user-pain) for the fix phase."

- [ ] **Step 2: Subagent writes `docs/admin-audit/triage.md`**

Template:

```markdown
# Admin Audit Triage — Fix Priority Order

**Generated:** 2026-04-NN
**Source:** 12 findings files + cross-tab patterns

## Tier 0 (Critical, fix first)

Blockers or data-integrity/security issues. Fix before anything else.

1. {Finding ref} — {Tab}.F-N — {Title}
   - Severity: Critical
   - Cross-tab: yes/no
   - Estimated fix: {S/M/L}

## Tier 1 (High impact, fix in wave 1)

User-visible bugs affecting core workflows.

...

## Tier 2 (Medium, fix in wave 2)

UX pain, missing validation, error handling.

...

## Tier 3 (Low, fix as capacity allows or defer)

Code smells, cosmetic.

...

## Cross-tab patterns (fix once, applies to many)

- CTP-1: {pattern name} — affects {tabs}. Single fix at {location} resolves all.
- CTP-2: ...

## Unblocked vs blocked by dependencies

- Independent (can fix in parallel): {list}
- Blocked (needs another fix first): {list with deps}

## Estimated fix-phase effort

- Tier 0: {N findings} × {avg size} = {hours}
- Tier 1: ...
- Total: {hours}

This triage feeds the Phase 6 Item 8 implementation plan.
```

- [ ] **Step 3: Main session reviews triage.md**

Verify: every finding from all 12 tabs appears SOMEWHERE in the triage (no dropped findings). Every CTP maps back to specific findings.

- [ ] **Step 4: Commit**

```bash
git add docs/admin-audit/triage.md docs/superpowers/specs/2026-04-21-admin-audit-design.md
git commit -m "audit: cross-tab pattern consolidation + triage doc

{N} total findings across 12 tabs ({critical/high/medium/low breakdown}).
{M} cross-tab patterns identified. Triage ranked by severity × impact.
Feeds Phase 6 Item 8 (batch fix) implementation plan."
```

---

## Task 14: Ceremonial close of audit phase

**Goal:** Close Phase 6 Item 7 cleanly. Does NOT merge to main yet — batch fix phase (6 Item 8) will consume these findings on a new branch based on this audit's HEAD.

**Files:**
- Create: `C:\Users\Admin\.claude\projects\C--\memory\project_phase_6_item_7_admin_audit_complete.md`
- Modify: `C:\Users\Admin\.claude\projects\C--\memory\MEMORY.md` (add pointer)

- [ ] **Step 1: Write memory file**

```markdown
---
name: Phase 6 Item 7 Admin Audit COMPLETE
description: Exhaustive deep audit of 12 admin panel tabs at admin.auracore.pro. Per-tab findings in docs/admin-audit/findings/*.md, triage + cross-tab patterns in docs/admin-audit/triage.md. Baseline for Phase 6 Item 8 (batch fix).
type: project
---

# Phase 6 Item 7 — Admin Panel Deep Audit COMPLETE

**Branch:** `phase-6-admin-audit` (audit-only — no fixes. Fixes = Item 8 on a branch based off this audit's HEAD). HEAD at {sha}.
**Spec:** `docs/superpowers/specs/2026-04-21-admin-audit-design.md`
**Plan:** `docs/superpowers/plans/2026-04-21-admin-audit.md`

## Findings summary

- **{N} total** findings across 12 tabs
- **Critical:** {N} — {short list}
- **High:** {N}
- **Medium:** {N}
- **Low:** {N}
- **Cross-tab patterns:** {N} — {short list}

## Per-tab summary

| Tab | Critical | High | Medium | Low |
|---|---|---|---|---|
| Subscriptions | ... |
...

## Top findings to fix first

1. ...
2. ...
3. ...

## Next: Phase 6 Item 8 (batch fix)

Triage at `docs/admin-audit/triage.md` feeds the fix-phase implementation plan.

## Merge status

Branch NOT merged to main. Fix phase's branch will be based on this one so findings documents travel with fix commits.
```

- [ ] **Step 2: Update MEMORY.md pointer**

Insert a new bullet in MEMORY.md:

```
- [Phase 6 Item 7 Admin Audit COMPLETE](project_phase_6_item_7_admin_audit_complete.md) — Exhaustive 12-tab deep audit. {N} findings ({breakdown}). Per-tab findings at docs/admin-audit/findings/*.md. Branch `phase-6-admin-audit` HEAD {sha}. **Not merged** — fix phase (6 Item 8) branches from this. **NEW CURRENT STATE (supersedes Phase 6.6).**
```

Also update the prior `Release Pipeline COMPLETE (Phase 6.6)` entry to remove "NEW CURRENT STATE" since this audit now holds that slot.

- [ ] **Step 3: No git commit — memory file is outside the repo**

Memory lives at `C:\Users\Admin\.claude\projects\C--\memory\` which is not tracked by this repo. No commit.

- [ ] **Step 4: Empty ceremonial commit in repo**

```bash
cd "C:/Users/Admin/Desktop/AuraCorePro/AuraCorePro"
git commit --allow-empty -m "ceremonial: Phase 6 Item 7 (Admin Audit) sealed on phase-6-admin-audit

Audit complete. 12 tabs audited, {N} findings, triage produced.
See docs/admin-audit/triage.md for fix-phase priority order.
Branch NOT merged to main — Phase 6 Item 8 will branch from this head
so findings travel with fixes."
```

---

## Self-review checklist (writing-plans skill requirement)

**1. Spec coverage:**
- ✅ 6-axis methodology — shared protocol section + Task 1-12 each reference + tab-specific additions
- ✅ 12 tabs in spec order (Subscriptions → Security) — 12 tasks one per tab
- ✅ Per-tab findings format — shared protocol section G template
- ✅ Severity rubric — referenced in every task
- ✅ Baseline known bugs B-1..B-4 — Task 1 (Subscriptions), Task 2 (Users) explicitly mention
- ✅ Findings matrix update — every task has Step 3 for matrix update
- ✅ Cross-tab patterns — every task has Step 4 for pattern update + Task 13 consolidation
- ✅ DB write gate — protocol section E
- ✅ Mobile breakpoints (320/375/414/768/1024) — protocol section D + F
- ✅ Deployment drift axis — protocol section C
- ✅ Success criteria — Task 14 verifies all checklist items

**2. Placeholder scan:** no "TBD" / "implement later" in task steps. Some {placeholders} in commit message templates and finding matrix rows — these are legitimate parameterization, not incomplete design.

**3. Type consistency:** tab names use consistent slug format across file paths (subscriptions, users, licenses, payments, devices, updates, crash-reports, telemetry, audit-log, ip-whitelist, configuration, security-2fa). Severity rubric terms consistent (Critical/High/Medium/Low). Finding ID format consistent (F-N).

---

## Execution handoff

Per user preference (`feedback_subagent_driven_default.md`): **subagent-driven-development** without asking. Per `feedback_supervisor_mode.md`: inline supervisor verification after each task.

**Plan complete and saved to `docs/superpowers/plans/2026-04-21-admin-audit.md`.**
