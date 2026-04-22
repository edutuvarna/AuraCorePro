# Crash Reports Audit Findings

**Tab:** Crash Reports
**URL path:** `https://admin.auracore.pro/` (SPA, sidebar nav → "Crash Reports" / `case 'crashes'`)
**Audit date:** 2026-04-22
**Auditor:** subagent-7
**Time spent:** ~2.5 hours

## Source files audited

- Frontend TSX (source on origin, March 27 snapshot): `/root/admin-panel/src/app/page.tsx` lines 949–1022
- Frontend API client: `/root/admin-panel/src/lib/api.ts` (getCrashReports, getCrashReport, getCrashStats, deleteCrashReport)
- Backend controller (local repo): `src/Backend/AuraCore.API/Controllers/Admin/AdminCrashReportController.cs` (104 lines)
- Backend controller (backup, April 12): `/root/auracore-src-backup-final-202604122153/AuraCore.API/Controllers/Admin/AdminCrashReportController.cs` (90 lines)
- Public submit controller: `src/Backend/AuraCore.API/Controllers/CrashReportController.cs` (43 lines)
- Backend entity: `src/Backend/AuraCore.API.Domain/Entities/CrashReport.cs`
- DbContext config: `src/Backend/AuraCore.API.Infrastructure/Data/AuraCoreDbContext.cs` lines 85–95
- Deployed DLL: `/var/www/auracore-api/AuraCore.API.dll` (built 2026-04-14)

## Summary

- **1 critical** — Pagination permanently broken: `pages` field absent from API response; all crash reports unreachable beyond page 1 at scale
- **1 high** — Stats KPI mismatch: 3 of 4 KPI cards always show 0 due to field name divergence between backend (`last24h`, `last7d`) and frontend (`today`, `thisWeek`, `uniqueTypes`)
- **2 high** — Delete action has no confirmation dialog (CTP-4); version filter silently broken due to query param name mismatch
- **2 medium** — Schema drift (DB has `Message` column + wrong varchar lengths vs EF config); `CreatedAt` index absent from prod DB (CTP-9)
- **1 medium** — No audit log for Delete action (CTP-2)
- **1 low** — `stackTracePreview` truncation present only in backup controller, not deployed

Axes covered: functional, code+DB sync, security, UX, mobile, drift.

---

## Findings

### F-1 [CRITICAL] Pagination permanently broken — API returns no `pages` field, Pagination component always hidden

**Axis:** functional, code-db-sync

**Symptom:** Crash reports beyond page 1 are completely inaccessible via the admin panel. With a high crash volume (hundreds or thousands of reports), admin can only see the first 50. The pagination buttons never render.

**Reproduction steps:**
1. Log in as `admin@auracore.pro`, navigate to Crash Reports tab
2. Observe: no Previous/Next pagination buttons rendered (even with > 50 crashes)
3. Inspect network: `GET /api/admin/crash-reports?page=1&pageSize=50` returns `{ total, page, pageSize, items }` — no `pages` key
4. Frontend state initializes with `{ items: [], total: 0, page: 1, pages: 0 }` and reads `data.pages || 0` → passes `pages=0` to `<Pagination>`
5. `Pagination` component at `page.tsx:276`: `if (pages <= 1) return null;` — always returns null since `pages` is always 0

**Expected behavior:** Pagination buttons render when `total > pageSize`. Pages = `Math.ceil(total / pageSize)` on the backend, returned as `pages` field.

**Actual behavior:** `<Pagination page={data.page || 1} pages={data.pages || 0} onChange={setPage} />` at `page.tsx:1018` always receives `pages=0` → renders nothing.

**Root cause:**
- `AdminCrashReportController.cs:49` returns `Ok(new { total, page, pageSize, items })` — includes `pageSize` but NOT `pages`.
- Backup controller (`/root/auracore-src-backup-final-202604122153/AuraCore.API/Controllers/Admin/AdminCrashReportController.cs:44`) returned `Ok(new { items, total, page, pages = (int)Math.Ceiling(total / (double)pageSize) })` — the rollback removed this computation.
- The frontend contracts on `pages` (backup pattern). The local repo introduced `pageSize` in the response but removed `pages`.

**DB state verification:**
```sql
SELECT COUNT(*) FROM crash_reports;
-- Result: 0 (no crash reports currently)
-- At scale (100+ rows), pagination would be broken
```

**Fix suggestion:**
- Option A: Add `pages = (int)Math.Ceiling(total / (double)pageSize)` to the `GetAll` response (mirrors backup behavior).
- Option B: Frontend computes `pages` as `Math.ceil(data.total / data.pageSize)` (matches Users tab pattern at `page.tsx:606`).

**Risk if unfixed:**
- User-facing: admin cannot view any crash report beyond the first 50 entries — hidden reports could mask a widespread crash bug.
- Data integrity: false sense of "no crash issues" when the table is large.
- Support: crash investigation blocked at scale.

---

### F-2 [HIGH] Stats KPI mismatch — 3 of 4 KPI cards always show 0

**Axis:** functional, code-db-sync

**Symptom:** The "Today", "This Week", and "Unique Types" KPI cards always display 0, regardless of actual crash volume. Only "Total Crashes" displays correctly.

**Reproduction steps:**
1. Navigate to Crash Reports tab
2. Observe: KPI row shows `Total Crashes: N, Today: 0, This Week: 0, Unique Types: 0`
3. If crashes exist, query DB: `SELECT COUNT(*) FROM crash_reports WHERE "CreatedAt" > NOW() - INTERVAL '24 hours';` — will return non-zero for active apps
4. The mismatch is consistent regardless of data

**Expected behavior:** "Today" shows crashes in the last 24h, "This Week" shows last 7d, "Unique Types" shows count of distinct `ExceptionType` values.

**Actual behavior:** All three always show 0.

**Root cause:**
- Frontend (`page.tsx:971–974`) reads: `stats?.today`, `stats?.thisWeek`, `stats?.uniqueTypes`
- Backend `AdminCrashReportController.GetStats` (`cs:72–91`) returns: `{ total, last24h, last7d, last30d, topExceptions, topVersions }`
- Field name divergence:
  - `stats?.today` → backend field is `last24h` → **undefined → 0**
  - `stats?.thisWeek` → backend field is `last7d` → **undefined → 0**
  - `stats?.uniqueTypes` → **not returned at all** → **0**
- The backup controller (`/root/auracore-src-backup-final-202604122153`) returned `{ total, last24h, last7d, byVersion, byException }` — same field names as local, but the frontend was updated to expect different keys without corresponding backend update.

**DB state verification:**
```sql
SELECT COUNT(*) FROM crash_reports;
-- Result: 0 (table is empty — no crashes to miscount right now)
-- At scale the mismatches become observable
```

**Fix suggestion:**
- Option A (backend): Rename `last24h` → `today`, `last7d` → `thisWeek`, add `uniqueTypes = db.CrashReports.Select(c => c.ExceptionType).Distinct().Count()`.
- Option B (frontend): Change field reads to match backend: `stats?.last24h`, `stats?.last7d`, `stats?.topExceptions?.length` for unique types.

**Risk if unfixed:**
- User-facing: admin sees "Today: 0, This Week: 0" for a product actively crashing. Masks crash severity entirely.

---

### F-3 [HIGH] Delete action has no confirmation dialog (CTP-4)

**Axis:** UX, security

**Symptom:** Clicking the trash icon on a crash report row immediately deletes it with no "Are you sure?" prompt. A mis-click permanently destroys a crash report that may be needed for debugging.

**Reproduction steps:**
1. Navigate to Crash Reports tab
2. Click any row's trash icon (🗑 button)
3. Observe: `api.deleteCrashReport(c.id)` fires immediately, then `load()` re-fetches. No `window.confirm()`, no modal, no undo.

**Expected behavior:** "Delete crash report? This cannot be undone." confirmation before firing the DELETE request.

**Root cause:**
- `page.tsx:1010`: `<button onClick={async () => { await api.deleteCrashReport(c.id); load(); }}` — inline async handler with no guard.
- Compare to Users tab Delete (`page.tsx:594`): `if(confirm('Delete this user...'))` — inconsistent. Crash Reports was missed.

**Cross-tab pattern ref:** CTP-4 (inconsistent destructive confirmation — same pattern as Licenses Revoke/Activate, Users Revoke).

**Fix suggestion:**
- Add `if (!confirm('Delete this crash report? This action cannot be undone.')) return;` before the `await api.deleteCrashReport(c.id)` call.
- Longer term: replace `confirm()` with a shared `ConfirmModal` component for consistent UX (CTP-4 fix recommendation).

**Risk if unfixed:**
- Data: accidental deletion of crash reports during investigation — forensic evidence permanently lost.
- UX: no feedback distinguishes "click to view detail" (Eye button) from "click to delete" (Trash button) — buttons are adjacent at `page.tsx:1008–1011`.

---

### F-4 [HIGH] Version filter silently broken — query param name mismatch (`version` vs `appVersion`)

**Axis:** functional, code-db-sync

**Symptom:** The frontend sends `?version=<value>` but the backend reads `[FromQuery] string? appVersion`. The filter is silently ignored — all crash reports are returned regardless of version input. There is no version filter input visible in the current UI, but the API client supports it and any future UI addition would inherit this bug.

**Reproduction steps:**
1. `GET https://api.auracore.pro/api/admin/crash-reports?version=1.5.0` → returns all crashes (ignores version filter)
2. The backend `GetAll` reads `appVersion` from query string, not `version`
3. With data in the table: `SELECT COUNT(*) FROM crash_reports WHERE "AppVersion" = '1.5.0'` would return N rows; the API returns all rows instead

**Root cause:**
- `AdminCrashReportController.cs:19`: `[FromQuery] string? appVersion = null` — parameter named `appVersion`.
- `api.ts getCrashReports`: `params.set('version', version)` — sends `version`.
- Backup controller used `[FromQuery] string? version = null` (matched the frontend). The rollback renamed it to `appVersion` to match the entity property naming convention, breaking the frontend contract.

**Fix suggestion:**
- Option A: Rename controller parameter back to `version` to match frontend: `[FromQuery] string? version = null`.
- Option B: Update frontend api.ts to send `appVersion`: `params.set('appVersion', version)`.

**Risk if unfixed:**
- Admin filtering by version is impossible. When a bad release ships, admin cannot isolate crash reports to that version alone — must visually scan all crash reports manually.

---

### F-5 [MEDIUM] Schema drift — DB has `Message` column and wrong varchar lengths vs EF config

**Axis:** code-db-sync, drift

**Symptom:** The `crash_reports` table in production has a `Message` column and different varchar length constraints that do not match the EF config or the entity class. The `Message` column is invisible to the ORM — it exists in the DB but is never populated or read.

**Reproduction steps:**
```sql
-- DB schema (from \d crash_reports):
-- ExceptionType: character varying(255)  ← EF config declares HasMaxLength(512)
-- AppVersion: character varying(50)       ← EF config declares HasMaxLength(32)
-- Message: text                           ← NOT in CrashReport.cs entity at all
-- StackTrace: text                        ← matches EF config (no maxLength = text)
-- DeviceId: nullable uuid                 ← entity declares Guid DeviceId (non-nullable), DB allows NULL
```

**Root cause:**
- DB was bootstrapped via raw DDL (not `dotnet ef database update`), using an older schema that had a separate `Message` column and different varchar lengths.
- `CrashReport.cs` entity has no `Message` property — the DB column is an orphan.
- `ExceptionType` was 255 in the DDL; EF config later changed it to 512. DB was never migrated.
- `AppVersion` was 50 in the DDL; EF config later changed it to 32 (tighter). DB allows 33-50 char versions that EF would reject at insert time — model validation vs DB constraint inversion.

**DB state verification:**
```sql
SELECT column_name, data_type, character_maximum_length
FROM information_schema.columns 
WHERE table_name = 'crash_reports' ORDER BY ordinal_position;
-- ExceptionType: varchar(255) [EF says 512]
-- AppVersion: varchar(50) [EF says 32]
-- Message: text [entity has no such field]
```

**Fix suggestion:**
- Apply pending EF migrations to align DB schema with EF config.
- Decide on `Message` column: drop it (it's never populated) or add it to the entity.
- Align varchar lengths: either update EF config to match DB (255/50) or migrate DB to match EF (512/32).

**Risk if unfixed:**
- `DeviceId` nullable in DB but non-nullable in entity — submitting a crash report without a DeviceId will succeed at the DB level but EF will raise a validation error first. Edge case but a silent inconsistency.
- `Message` column accumulates confusion for future developers.

---

### F-6 [MEDIUM] `CreatedAt` index absent from production DB (CTP-9 confirmed for crash_reports)

**Axis:** code-db-sync, functional

**Symptom:** All crash report queries use `ORDER BY "CreatedAt" DESC` (the primary sort order for `GetAll` and the filter for `GetStats` time windows). Without an index on `CreatedAt`, these queries perform full table sequential scans. As the crash_reports table grows, every admin page load and every stats refresh degrades linearly.

**Verification:**
```sql
SELECT tablename, indexname, indexdef
FROM pg_indexes WHERE tablename='crash_reports' ORDER BY indexname;
-- Result: only crash_reports_pkey (Id)
-- IX_CrashReports_CreatedAt is ABSENT
```

**Root cause:**
- `AuraCoreDbContext.cs:93`: `e.HasIndex(c => c.CreatedAt)` declares this index in EF config.
- `__EFMigrationsHistory` has 0 rows — no EF migrations have ever been applied.
- The raw DDL bootstrap did not create this index.

**Cross-tab pattern ref:** CTP-9 (EF index migration gap — confirmed in payments, devices, app_updates, and now crash_reports).

**GetStats is especially vulnerable:**
- 4 COUNT queries with `WHERE "CreatedAt" > ...` — all sequential scans at scale.
- The `GROUP BY ExceptionType` aggregate query for topExceptions — no index assists this either.

**Fix suggestion:**
- `CREATE INDEX "IX_CrashReports_CreatedAt" ON crash_reports ("CreatedAt" DESC);`
- Apply as part of the global CTP-9 fix (batch migration application in Phase 6 Item 8).

**Risk if unfixed:**
- Crash Reports tab becomes the slowest page in the admin panel as the table grows. Stats endpoint runs 4 sequential count queries on every tab visit.

---

### F-7 [MEDIUM] No audit log for Delete action (CTP-2 confirmed for Crash Reports)

**Axis:** security
**Cross-tab pattern ref:** CTP-2 (missing audit log — confirmed across all prior tabs)

**Symptom:** Admin can permanently delete individual crash reports with no record of who deleted what or when.

**Root cause:**
- No `admin_audit_log` table exists in production DB (established by CTP-2).
- `AdminCrashReportController.Delete` (`cs:94–103`) has no audit log write after `_db.SaveChangesAsync()`.
- Crash reports are forensic data. Deleting a report tied to a reported user issue with no audit trail means support incidents become untraceable.

**Fix suggestion:**
- Part of global CTP-2 fix: once `admin_audit_log` table exists, wire into Delete endpoint: log `entity=crash_report, entityId={id}, action=delete, actorId={currentUserId}, timestamp=now`.

**Risk if unfixed:**
- Admin deletes a crash report that was evidence of a security incident. No way to reconstruct who deleted it or when.

---

### F-8 [LOW] `stackTracePreview` truncation absent from deployed controller (backup had it, local repo removed it)

**Axis:** drift, functional

**Symptom:** The backup `List` endpoint truncated StackTrace to 300 characters in the list response to avoid sending large payloads for the crash list view. The local repo's `GetAll` returns only `{ Id, DeviceId, AppVersion, ExceptionType, CreatedAt, deviceName }` — StackTrace is NOT in the list response at all. The detail view (`GetById`) loads the full StackTrace. This is actually a slight improvement (less data in list), but the detail panel renders the full raw StackTrace in a `<pre>` element.

**Detail panel overflow check:**
- `page.tsx:983`: `<pre className="bg-surface-950 rounded-xl p-4 text-xs font-mono text-white/60 overflow-x-auto max-h-60">{detail.stackTrace}</pre>`
- `overflow-x-auto` + `max-h-60` (240px): StackTrace rendering is safely contained — horizontal scroll for wide lines, vertical scroll for long traces. No layout explosion risk.
- This is a low finding because the CSS contains the StackTrace correctly. The backup's 300-char truncation was unnecessary given the detail panel is separate from the list.

**Risk if unfixed:** None — current behavior is correct. Noting as informational.

---

## Axis-by-axis coverage

### 1. Functional

- **List view:** Renders. Search by ExceptionType works (EF `.Contains(search)` → PostgreSQL `LIKE` via EF translation). Version filter **broken** (F-4 — `version` vs `appVersion` param mismatch). Sort: always `ORDER BY CreatedAt DESC`, no user-configurable sort.
- **Detail action (view):** Eye button fetches `GET /api/admin/crash-reports/{id}` → GetById loads StackTrace + SystemInfo. Rendered in a collapsible detail panel. XSS safe (see Security axis). SystemInfo (jsonb) is returned as a raw JSON string in the response — rendered as `{detail.exceptionType}`, `{detail.appVersion}`. SystemInfo field is NOT rendered in the detail panel at all (only ExceptionType and AppVersion shown in the 2-col grid + StackTrace in `<pre>`).
- **Delete action:** Fires immediately, no confirmation (F-3).
- **Pagination:** Permanently broken (F-1) — `pages` field absent from API response.
- **Stats:** 3 of 4 KPI cards always 0 (F-2).
- **Refresh button:** Present. `onClick={load}` calls `load()` (soft refetch — Bug 3 B-2 NOT confirmed for Crash Reports tab; identical to Devices and Updates pattern).
- **Empty state:** `{(data.items || []).length === 0 && <EmptyState icon={Bug} title="No crash reports" subtitle="Great news - no crashes recorded!" />}` — renders correctly at line ~1019. DB confirmed 0 rows → empty state shows.

### 2. Code + DB sync

- **Pagination shape mismatch:** `{ total, page, pageSize, items }` returned; `{ total, page, pages, items }` expected (F-1).
- **Stats field name mismatch:** `last24h`/`last7d`/`last30d`/`topExceptions`/`topVersions` returned; `today`/`thisWeek`/`uniqueTypes` expected (F-2).
- **Version filter param mismatch:** `?version=` sent; `?appVersion=` read (F-4).
- **Schema drift:** `Message` column in DB not in entity; varchar lengths diverged (F-5).
- **CreatedAt index missing:** CTP-9 (F-6).
- **DeviceId FK nullable in DB:** Entity has `Guid DeviceId` (non-nullable value type), DB column allows NULL. EF will fail with NULL DeviceId at insert.
- **CrashReport.SystemInfo:** Stored as `jsonb`, returned as a JSON string from the API. Frontend does not parse or render it in the detail panel. The `GetById` response includes `report.SystemInfo` which is whatever jsonb string is stored. No structured key-value display.

**DB read queries run:**
```sql
-- CTP-9 check
SELECT tablename, indexname, indexdef
FROM pg_indexes WHERE tablename='crash_reports' ORDER BY indexname;
-- Result: crash_reports_pkey only

-- Schema check
SELECT column_name, data_type, character_maximum_length
FROM information_schema.columns WHERE table_name = 'crash_reports' ORDER BY ordinal_position;
-- Id uuid, DeviceId uuid (nullable), AppVersion varchar(50), ExceptionType varchar(255),
-- StackTrace text, Message text (orphan), SystemInfo jsonb, CreatedAt timestamptz

-- Count
SELECT COUNT(*) FROM crash_reports;
-- Result: 0
```

### 3. Security

- **Authorization:** `[Authorize(Roles = "admin")]` at `AdminCrashReportController` class level (line 10). All 4 endpoints (GetAll, GetById, GetStats, Delete) require admin JWT. Confirmed by DLL string check. No bypass path from admin panel.
- **Public POST endpoint auth:** `CrashReportController` (the client-submit endpoint) has `[Authorize]` at class level (line 10) — requires a valid user JWT. NOT open to anonymous submission. This is a deliberate design choice: only authenticated clients (with a valid device JWT) can submit crash reports. Prevents unauthenticated spam. **No rate limiting** on this endpoint (consistent with all prior tabs' finding of no rate limiting).
- **Request size limit:** `Program.cs:95` sets `MaxRequestBodySize = 5_000_000` (5 MB) globally. The controller also validates: StackTrace max 50,000 chars (50KB), SystemInfo max 10,000 chars (10KB), ExceptionType max 512 chars. Defense-in-depth at both Kestrel and application layers.
- **XSS:** `ExceptionType` rendered as `{c.exceptionType}` (React text node, escaped) in list view. StackTrace in detail rendered in `<pre>{detail.stackTrace}</pre>` (React text node, NOT dangerouslySetInnerHTML) — **safe**. No `dangerouslySetInnerHTML` found in CrashReportsPage. React's default escaping protects all user-supplied fields.
- **SQL injection:** All filters use EF Core LINQ (`query.Where(c => c.ExceptionType.Contains(search))`) — EF parameterizes the value. No raw SQL in `AdminCrashReportController`. No injection risk.
- **IDOR:** GUIDs are used for crash report IDs. Single-tenant. No user-scoped access control needed beyond admin role.
- **CSRF:** Stateless JWT — no CSRF surface.
- **Audit log:** Missing for Delete (F-7, CTP-2).
- **Nginx basic auth bypass:** Same as all prior tabs — `api.auracore.pro` is a separate vhost with no Nginx basic auth. Admin JWT is the only guard. Direct `curl /api/admin/crash-reports` without JWT → 401.
- **PII in StackTrace:** Potential. Exception messages in .NET can embed values from local variables (e.g., `NullReferenceException: Object reference not set to an instance of an object at X.Y(String email = "user@example.com")`). This is a documentation note — not an application bug, but admin should be aware that StackTrace data may contain user-identifiable values.

### 4. UX

- **Loading state:** No spinner while `Promise.all([getCrashReports, getCrashStats])` fetches. Same pattern as all prior tabs.
- **Error state:** API error falls back to `{ items: [], total: 0, page: 1, pages: 0 }` on catch — empty state shown. No error toast. Silent failure pattern (consistent across all tabs).
- **Empty state:** Renders correctly (`EmptyState` component with friendly message). DB confirmed 0 rows → visible.
- **Destructive confirmation:** Delete has NO confirmation (F-3, CTP-4).
- **Refresh survival (Bug 3):** Refresh button (`onClick={load}`) calls `load()` — soft refetch only, NOT `window.location.reload()`. **Bug 3 B-2 NOT confirmed for Crash Reports tab.** Auth state preserved.
- **Detail panel:** Collapsible (close button visible). StackTrace in `<pre>` with `overflow-x-auto max-h-60` — safely contained at all viewport sizes.

### 5. Mobile

CTP-3 (root layout — fixed 260px sidebar, no responsive breakpoints) applies to Crash Reports tab identically to all other tabs. At 320px:
- Sidebar 260px + content 60px — table unusable.
- The 4-column KPI grid (`grid-cols-2 lg:grid-cols-4`) collapses to 2-col at small screens (has `lg:` breakpoint) — partial mobile awareness.
- Table: 4 columns (Exception, Version, Date, Actions). Exception column is `font-mono text-xs` — long exception class names will overflow horizontally even on 768px.
- At 320px: main horizontal overflow confirmed by the root layout pattern established in CTP-3. No Crash Reports-specific mobile breakpoint handling.
- The detail panel `<pre>` with `overflow-x-auto` handles long StackTrace lines at all viewport sizes — this is the one mobile-aware element on the page.

No CrashReports-specific mobile finding beyond CTP-3.

### 6. Deployment drift

**Controller comparison:**

| Feature | Local repo (104 lines) | Backup (90 lines) | Deployed DLL |
|---|---|---|---|
| List method name | `GetAll` | `List` | `GetAll` (confirmed in DLL strings) |
| Response shape | `{total, page, pageSize, items}` | `{items, total, page, pages}` | `{total, page, pageSize, items}` (no `pages`) |
| Stats fields | `{total, last24h, last7d, last30d, topExceptions, topVersions}` | `{total, last24h, last7d, byVersion, byException}` | `{total, last24h, ...}` (confirmed `last24h` in DLL strings) |
| Version filter param | `appVersion` | `version` | `appVersion` (DLL strings confirm `appVersion` field) |
| StackTrace preview | Not in list response | 300-char truncation in list | Not in list |
| pageSize cap | 100 | 500 | 100 |
| Delete impl | `FindAsync` + `Remove` + `SaveChanges` | `ExecuteDeleteAsync` | `FindAsync` pattern (local) |

**Key drift finding:** The local repo's CrashReport controller is deployed (DLL built 2026-04-14 matches local repo, not backup). However the frontend was rebuilt on 2026-04-21 with expectations from the backup (uses `data.pages`, reads `version` filter). The frontend expects the backup's API contract; the deployed backend has the local repo's contract. The result:
- Pagination silently broken (F-1)
- Version filter silently broken (F-4)
- Stats KPI mismatch (F-2)

**CTP-6 status:** Local repo (104 lines) > backup (90 lines). The local repo has MORE functionality than the backup (added `GetStats` endpoint, `GetById` with Device join, `deviceName` in list). This is NOT a rollback strip — CTP-6 does not apply to Crash Reports. The controller was enhanced post-rollback but the API contract was not aligned with the frontend.

**Source vs deployed frontend:**
- `/root/admin-panel/src/app/page.tsx` modified: 2026-03-27 (old source)
- `/var/www/admin-panel/_next/static/chunks/app/page-9bf9edb4333e55cf.js` modified: 2026-04-21 (deployed)
- 26-day gap. Deployed frontend is newer than the source on origin. Same drift pattern as all prior tabs.

---

## Questions for user

1. **SystemInfo rendering:** The detail panel currently shows only `exceptionType` and `appVersion` from a crash report. The `systemInfo` jsonb (which may contain OS version, RAM, CPU, etc.) is returned by `GetById` but not rendered. Should the detail panel parse and display `systemInfo` key-value pairs? This is a UX enhancement question for the fix phase.

2. **Bulk delete:** There is no bulk-delete action for crash reports. As the table grows, admin will need housekeeping tools. Should a "Select all + Delete selected" or "Delete all before date X" action be added? This is a feature request for Phase 6 Item 8 scope consideration.

3. **Version filter UI:** The frontend API client supports `version` filtering but the Crash Reports UI has no version dropdown or input. The `getCrashStats` endpoint returns `topVersions`. Should the fix phase add a version filter dropdown populated from `topVersions`?
