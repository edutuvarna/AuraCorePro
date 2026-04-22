# Devices Audit Findings

**Tab:** Devices
**URL path:** `https://admin.auracore.pro/` (SPA, sidebar nav → "Devices")
**Audit date:** 2026-04-22
**Auditor:** subagent-5
**Time spent:** ~2.5 hours

## Source files audited

- Frontend TSX: `/root/admin-panel/src/app/page.tsx` lines 889–942 (`DevicesPage` function)
- Frontend API client: `/root/admin-panel/src/lib/api.ts` lines 285–310 (device methods)
- Backend controller (local): `src/Backend/AuraCore.API/Controllers/Admin/AdminDeviceController.cs` (67 lines, 2 endpoints)
- Backend controller (backup): `/root/auracore-src-backup-final-202604122153/AuraCore.API/Controllers/Admin/AdminDeviceController.cs` (90 lines, 4 endpoints)
- Backend entity: `src/Backend/AuraCore.API.Domain/Entities/Device.cs`
- DbContext config: `src/Backend/AuraCore.API.Infrastructure/Data/AuraCoreDbContext.cs` lines 51–61
- Deployed DLL: `/var/www/auracore-api/AuraCore.API.dll` (built 2026-04-14)

## Summary

- **2 critical** — KPI stats all show 0 due to field name mismatch; pagination silently broken (pages field absent)
- **3 high** — `GetById` and `Delete` endpoints stripped from DLL (CTP-6); `HardwareFingerprint` (64-char hash) exposed in every `GetAll` response; `crashCount`/`telemetryCount` columns always show 0
- **2 medium** — No admin audit log for device deletions (CTP-2); admin `deleteDevice` function exists in api.ts but no UI trigger (dead code — admin cannot revoke any device)
- **2 low** — No "Last Active" status indicator (online/offline); no max-devices-exceeded warning for over-quota licenses

Axes covered: functional, code+DB sync, security, UX, mobile, drift.

---

## Findings

### F-1 [CRITICAL] All four KPI cards always display 0 — stats field name mismatch between backend and frontend

**Axis:** functional, code-db-sync
**Baseline bug ref:** (new — Devices-specific)

**Symptom:** The four KPI cards ("Total Devices", "Active Today", "Active This Week", "New This Week") all permanently display 0, regardless of actual device data in the DB. With 1 device registered (confirmed in production DB), all cards show 0.

**Reproduction steps:**
1. Log in as `admin@auracore.pro`, navigate to Devices tab
2. Observe four KPI cards — all show 0
3. Check DB: `SELECT COUNT(*) FROM devices;` → 1 row

**Expected behavior:** KPI cards reflect actual device count and activity statistics from the DB.

**Actual behavior:** All KPI cards frozen at 0.

**Root cause:**
- `AdminDeviceController.cs:GetStats` (local repo, lines 51–66) returns JSON fields:
  `{ total, activeLastDay, activeLastWeek, activeLastMonth, topOs }`
- `page.tsx:909–912` reads:
  - `stats?.totalDevices` — API returns `total` not `totalDevices` → `undefined` → 0
  - `stats?.activeToday` — API returns `activeLastDay` not `activeToday` → `undefined` → 0
  - `stats?.activeThisWeek` — API returns `activeLastWeek` not `activeThisWeek` → `undefined` → 0
  - `stats?.newThisWeek` — API returns no `newThisWeek` at all (local repo doesn't compute new-this-week; backup did) → `undefined` → 0
- The Total Devices card has a partial fallback: `stats?.totalDevices ?? data.total ?? 0` — so once `getDevices()` resolves, `data.total` shows the correct count. But the 3 other KPI cards have no fallback and remain 0 permanently.
- The backup's `Stats` endpoint returned `{ total, activeToday, activeWeek, newThisWeek, osDist }` — matching the frontend. The local repo's renamed fields broke the contract.

**DB state verification:**
```sql
SET default_transaction_read_only = on;
SELECT COUNT(*) AS total_devices,
       COUNT(*) FILTER (WHERE "LastSeenAt" > NOW() - INTERVAL '24 hours') AS active_today,
       COUNT(*) FILTER (WHERE "LastSeenAt" > NOW() - INTERVAL '7 days') AS active_week,
       COUNT(*) FILTER (WHERE "RegisteredAt" > NOW() - INTERVAL '7 days') AS new_this_week
FROM devices;
-- Actual: total=1, active_today=0, active_week=0, new_this_week=0
-- (Device registered 2026-03-27, last seen 2026-03-31 — all time-based stats are 0 for current week)
-- Despite actual data existing, all stats show 0 in the UI due to field name mismatch
```

**Fix suggestion:**
- Option A (recommended): Update `GetStats` to return field names matching what the frontend expects: rename `activeLastDay → activeToday`, `activeLastWeek → activeThisWeek`, add `newThisWeek`, rename `activeLastMonth → activeLastMonth` (unused by frontend; can keep or drop).
- Option B: Update `page.tsx:909–912` to read `stats?.total`, `stats?.activeLastDay`, `stats?.activeLastWeek`. But the backup's naming (`activeToday`, `activeThisWeek`) was already correct for the frontend — restoring backup naming is simpler.

**Risk if unfixed:**
- Admin has no visibility into device activity trends. All "active" metrics are misleading zeroes.
- "Total Devices" partially correct (`data.total` fallback) — but only after device list loads.

---

### F-2 [CRITICAL] Pagination silently broken — `pages` field absent from `GetAll` response, Pagination component hidden

**Axis:** functional, code-db-sync

**Symptom:** The Pagination component at the bottom of the Devices table is never rendered, even when there are more than `pageSize` devices. Admin cannot navigate past page 1.

**Reproduction steps:**
1. Navigate to Devices tab
2. Observe Pagination component at bottom of table (`page.tsx:940`)
3. `Pagination` is rendered with `pages={data.pages || 0}` — `data.pages` is `undefined`
4. `Pagination` component returns `null` when `pages <= 1` (`page.tsx:276`) — so it renders nothing

**Expected behavior:** Pagination appears when total devices exceed `pageSize` (50).

**Actual behavior:** Pagination never renders. Admin is stuck on page 1.

**Root cause:**
- `AdminDeviceController.cs:GetAll` (local, line 47) returns:
  `{ total, page, pageSize, items }` — no `pages` field.
- `page.tsx:940`: `<Pagination page={data.page || 1} pages={data.pages || 0} onChange={setPage} />`
- `data.pages` → `undefined` → `|| 0` → `Pagination` renders `null` because `pages <= 1`.
- The backup's `List` endpoint (line 43) returns `{ items, total, page, pages = (int)Math.Ceiling(total / (double)pageSize) }` — correctly computed `pages` field.
- The local repo removed the `pages` field computation from the response.
- Additionally, the local repo returns `pageSize` in the response but the frontend ignores it.

**DB state verification:**
```sql
SELECT COUNT(*) FROM devices;
-- Currently: 1 device. Pagination not observable with 1 device.
-- When device count > 50: pagination would silently fail (no next-page button ever shown).
```

**Fix suggestion:**
- Add `pages = (int)Math.Ceiling(total / (double)pageSize)` to the `GetAll` return: `return Ok(new { total, page, pageSize, items, pages = (int)Math.Ceiling(total / (double)pageSize) });`

**Risk if unfixed:**
- Admin can only see the first 50 devices. Any deployment with 51+ devices has invisible devices in the admin panel with no way to discover them.

---

### F-3 [HIGH] `GetById` and `Delete` endpoints stripped from deployed DLL (CTP-6 confirmed)

**Axis:** drift, functional
**Baseline bug ref:** B-4 (rollback artifact), CTP-6

**Symptom:** `GET /api/admin/devices/{id}` returns HTTP 404 (endpoint not in DLL). `DELETE /api/admin/devices/{id}` returns HTTP 404. Admin cannot view device detail or delete any device.

**Reproduction steps:**
1. `curl -H 'Authorization: Bearer <valid-admin-jwt>' https://api.auracore.pro/api/admin/devices/{id}` → HTTP 404
2. `curl -X DELETE -H 'Authorization: Bearer <valid-admin-jwt>' https://api.auracore.pro/api/admin/devices/{id}` → HTTP 404
3. (With invalid JWT): `GET /api/admin/devices` → 401, `GET /api/admin/devices/{id}` → 404 — confirms the route doesn't exist in the DLL, not an auth gap.

**Expected behavior:** Both endpoints should return 401 for invalid auth (route exists) or 200/204 for valid auth.

**Actual behavior:** 404 (route not registered) — method bodies stripped from deployed DLL.

**Root cause — controller diff:**

| Method | Backup (90 lines) | Local repo (67 lines) | Deployed DLL |
|---|---|---|---|
| `List` / `GetAll` | `[HttpGet] List(...)` returns `{items,total,page,pages}` | `[HttpGet] GetAll(...)` returns `{total,page,pageSize,items}` | `GetAll` present |
| `GetById` | `[HttpGet("{id}")] GetById(Guid id)` | **ABSENT** | **ABSENT** |
| `Stats` | `[HttpGet("stats")] Stats(...)` | `[HttpGet("stats")] GetStats(...)` | `GetStats` present |
| `Delete` | `[HttpDelete("{id}")] Delete(Guid id)` uses `ExecuteDeleteAsync` | **ABSENT** | **ABSENT** |

DLL confirmation:
- `strings /var/www/auracore-api/AuraCore.API.dll | grep AdminDevice` → only `GetAll d__2` and `GetStats d__3`. No `GetById` or `Delete` state machines.

Additionally, the frontend API client (`api.ts:296–308`) has `getDevice(id)` and `deleteDevice(id)` methods that call these stripped endpoints — both are dead code at runtime. The UI (DevicesPage) never calls either of them (no row click handler, no delete button in the TSX), so this is currently invisible to the admin but the API contract is broken.

**Fix suggestion:**
- Restore `GetById` and `Delete` from the backup into the local repo's `AdminDeviceController.cs`.
- Also restore the backup's `List` response shape (`pages` field — see F-2).
- Consider whether the Devices tab should expose a per-device detail view (linked from Machine name column) — the frontend api.ts already has `getDevice()` ready to call.

**Risk if unfixed:**
- Admin cannot delete a rogue or stale device. License's device slot remains occupied, blocking the user's next registration if they're at max devices.
- `getDevice()` always returns `null` (404) — any future UI work that tries to show device detail will silently fail.

---

### F-4 [HIGH] `HardwareFingerprint` (64-char unique identifier) returned in every `GetAll` response — privacy and tracking concern

**Axis:** security, code-db-sync

**Symptom:** Every call to `GET /api/admin/devices` includes the `HardwareFingerprint` field for each device (a 64-character hex hash unique to the user's hardware). This field is not displayed in the DevicesPage UI but is present in every paginated API response.

**Root cause:**
- `AdminDeviceController.cs:GetAll` (local, line 40–44): explicitly selects `d.HardwareFingerprint` in the projection.
- The backup's `List` endpoint deliberately excluded `HardwareFingerprint` from the list projection (only in `GetById` detail view). The local repo added this field to the list response without a corresponding UI column.
- DB confirmed: `HardwareFingerprint = 'b35e4617208dbe121118f16c4821ccf18074dcd53baf8eb1d060cab373e97dde'` (64 hex chars) — a hardware-derived unique hash per device.
- The fingerprint is effectively a permanent hardware ID. Exposing it in a list endpoint (even admin-only) is unnecessary for the list view. If admin panel auth is ever compromised, all user hardware fingerprints leak.
- DbContext config (`AuraCoreDbContext.cs:54`): `HasMaxLength(512)` — the field is designed to hold up to 512 characters.

**DB state verification:**
```sql
SELECT LENGTH("HardwareFingerprint"), LEFT("HardwareFingerprint", 16) || '...' AS fingerprint_prefix
FROM devices LIMIT 5;
-- Result: 64 chars, 'b35e4617208dbe12...'
```

**Fix suggestion:**
- Remove `d.HardwareFingerprint` from `GetAll`'s projection — it is not rendered in the UI anyway.
- Keep it in `GetById` only (which is also stripped — see F-3) for per-device detail views.
- If an admin needs to see/compare fingerprints for duplicate detection, it should be an explicit action on a per-device detail page, not bulk-returned in list responses.

**Risk if unfixed:**
- Any admin panel session compromise leaks permanent hardware fingerprints for all registered users.
- Hardware fingerprints are unique identifiers — their exposure creates a data-at-rest privacy concern under GDPR/CCPA.

---

### F-5 [HIGH] `crashCount` and `telemetryCount` columns always show 0 — fields missing from `GetAll` response

**Axis:** functional, code-db-sync

**Symptom:** The Devices table has "Crashes" and "Telemetry" columns (`page.tsx:922–923`) that display `{d.crashCount ?? 0}` and `{d.telemetryCount ?? 0}`. Both always show 0 regardless of actual crash/telemetry records linked to the device.

**Root cause:**
- `AdminDeviceController.cs:GetAll` (local, lines 38–44) projects: `{ d.Id, d.LicenseId, d.MachineName, d.OsVersion, d.HardwareFingerprint, d.RegisteredAt, d.LastSeenAt, licenseTier, userEmail }` — no `crashCount`, no `telemetryCount`.
- Backup's `List` endpoint (lines 35–39) projected: `{ d.Id, d.MachineName, d.OsVersion, d.RegisteredAt, d.LastSeenAt, d.LicenseId, crashCount = d.CrashReports.Count, telemetryCount = d.TelemetryEvents.Count }` — both subaggregate counts present.
- Frontend `page.tsx:922`: `<td className="py-3 px-4 text-white/50">{d.crashCount ?? 0}</td>` — `d.crashCount` is `undefined` → `?? 0` → always 0.
- The local repo traded crash/telemetry counts for `HardwareFingerprint` and tier/email data in the projection — the wrong tradeoff.

**DB state verification:**
```sql
SELECT d."MachineName",
       (SELECT COUNT(*) FROM crash_reports c WHERE c."DeviceId" = d."Id") AS crash_count,
       (SELECT COUNT(*) FROM telemetry_events t WHERE t."DeviceId" = d."Id") AS telemetry_count
FROM devices d;
-- Result: DESKTOP-GQN87MV, crash_count=0, telemetry_count=0
-- (Both are actually 0 in prod, so not currently observable in UI — but will misreport as data accumulates)
```

**Fix suggestion:**
- Restore `crashCount = d.CrashReports.Count` and `telemetryCount = d.TelemetryEvents.Count` in the `GetAll` projection.
- EF Core can translate these Count() calls to efficient SQL COUNT subqueries when projecting from the navigational property.

**Risk if unfixed:**
- Admin sees incorrect 0 crash/telemetry counts for all devices. A device with 200 crashes appears identical to a healthy device — admin cannot use the Devices tab for triage.

---

### F-6 [MEDIUM] No admin audit log for device delete operations (CTP-2 confirmed for Devices)

**Axis:** security
**Cross-tab pattern ref:** CTP-2 (confirmed in Subscriptions, Users, Licenses, Payments — now Devices)

**Symptom:** When `Delete /api/admin/devices/{id}` is eventually restored, the deletion will not be logged to any audit table. Admin can delete device registrations with no evidence trail.

**Root cause:**
- Backup's `Delete` method (lines 82–88): `ExecuteDeleteAsync(ct)` — no audit log write before or after.
- No `admin_audit_log` table exists (confirmed across all prior audits — CTP-2 established).
- Device deletion removes a user's hardware registration permanently — this is an admin-initiated action affecting user's subscription slot count.

**Fix suggestion:**
- Part of the global CTP-2 fix: add `admin_audit_log` table and wire all mutation controllers. Device Delete should log: actor (admin email), device ID, device machine name, license ID, timestamp.

**Risk if unfixed:**
- Admin can delete user devices (freeing license slots) with no audit trail. Potential insider abuse.

---

### F-7 [MEDIUM] Admin has no UI mechanism to revoke/delete any device — `deleteDevice()` is dead code

**Axis:** functional, UX

**Symptom:** The Devices tab is entirely read-only. There is no "Revoke", "Delete", or "Remove" button on any device row. A user who registers a device that should be removed (stolen laptop, compromised machine) cannot be remediated from the admin panel.

**Root cause:**
- `DevicesPage` TSX (lines 919–937): table rows render only Machine, OS, Crashes, Telemetry, Last Seen — no action column.
- `api.ts:306–309` has `deleteDevice(id)` method: `DELETE /api/admin/devices/${id}`. This method is never called from any TSX component.
- Compounding: even if the UI had a delete button, the backend endpoint is stripped (F-3 — 404).
- The backup controller had `Delete` working; the backup + frontend combination would support device revocation. Both were removed.

**Fix suggestion:**
- Add an "Action" column to the Devices table with a "Remove" button per row.
- Wire the button to `api.deleteDevice(device.id)` with a `confirm()` guard (CTP-4 pattern).
- Restore the backend `Delete` endpoint (part of F-3 fix).
- Consider: should device removal cascade to linked CrashReports and TelemetryEvents? DbContext.cs:82,94 — both have `OnDelete(DeleteBehavior.Cascade)` at the DB level, so deleting a device removes its crash reports and telemetry events automatically. Admin should be warned of this cascade.

**Risk if unfixed:**
- Admin cannot remove stale, stolen, or compromised device registrations.
- Users at max-device limit cannot free a slot via admin intervention — they'd need a DB-level write (write-gate required, undocumented).

---

### F-8 [LOW] No "online" / "offline" status indicator — "Last Seen" only shows date, not staleness

**Axis:** UX, functional

**Symptom:** The "Last Seen" column in the Devices table shows a date only (`new Date(d.lastSeenAt).toLocaleDateString()` — `page.tsx:928`). There is no color-coding, badge, or "inactive" flag to indicate whether a device is actively connected or hasn't been seen in weeks/months.

**Root cause:**
- The backend provides `LastSeenAt` (a timestamp). There is no computed `isOnline` field.
- The frontend does not compute staleness client-side — it only formats the date as a locale string.
- The `GetStats` endpoint provides `activeLastDay` / `activeLastWeek` counts but not per-device status.
- By comparison, the Users tab has `StatusBadge` with online/offline states — the Devices tab has no equivalent.

**Fix suggestion:**
- Add a frontend staleness check: if `lastSeenAt` > 30 days ago → show amber "Stale" badge; if > 90 days → show red "Inactive" badge.
- Or: add a computed `isOnline` field in the backend projection (`isOnline = d.LastSeenAt > DateTimeOffset.UtcNow.AddMinutes(-30)`) for a "currently connected" indicator.

**Risk if unfixed:**
- Admin cannot visually distinguish an active device from an abandoned one at a glance.

---

### F-9 [LOW] No max-devices-exceeded warning — admin has no signal when a license has more devices than `MaxDevices`

**Axis:** functional, UX

**Symptom:** If a license has `MaxDevices = 2` but has 3 active device registrations (e.g., due to a registration race or manual DB manipulation), the Devices tab shows no warning. There is no column showing `devicesUsed / maxDevices` per row.

**Root cause:**
- The Devices tab groups by device rows, not by license. There is no per-license device count visible.
- `LicensesPage` (`page.tsx:775`) shows `l.activeDevices ?? 0}/{l.maxDevices ?? 1}` — but this requires navigating to a different tab.
- The `GetAll` projection includes `d.LicenseId` but not the license's `MaxDevices` or sibling device count.
- An over-quota scenario can occur if the API's device registration guard races (two simultaneous registrations when at max-1).

**Fix suggestion:**
- Add `maxDevices = d.License.MaxDevices` and `licenseDeviceCount = d.License.Devices.Count()` to the `GetAll` projection.
- Add a warning badge in the Machine column when `licenseDeviceCount > maxDevices`.
- Or: add a dashboard card "Over-quota licenses" to the Devices tab KPI row.

**Risk if unfixed:**
- Admin cannot detect over-quota license situations from the Devices tab. A license with 3 devices and `MaxDevices = 2` is invisible until the user reports a registration failure.

---

## Axis-by-axis coverage

### 1. Functional

- **List view:** Partially functional — device rows render (Machine, OS columns display correctly). Search by MachineName/OsVersion works at the backend (EF `.Contains()`). Pagination: BROKEN (F-2 — `pages` field absent). Sorting: ascending-only by `LastSeenAt DESC` (hardcoded, no sort control).
- **Create action:** Not applicable — devices are registered by the desktop app, not created from admin panel.
- **Update action:** Not applicable — no device fields are admin-editable.
- **Delete action:** BROKEN — Delete endpoint missing from DLL (F-3); no UI trigger in DevicesPage (F-7).
- **Detail view:** BROKEN — `GetById` endpoint missing from DLL (F-3); `api.getDevice()` always returns null.
- **Empty state:** Correct — `EmptyState` with Monitor icon and "No devices registered yet" renders when items array is empty.
- **KPI cards:** ALL BROKEN — field name mismatch means all 4 KPI cards show 0 (F-1). Partial recovery: "Total Devices" shows correct value once device list loads via `data.total` fallback.
- **Crash/Telemetry columns:** BROKEN — both always show 0 (F-5).

### 2. Code + DB sync

- **KPI stats mismatch (F-1):** Backend returns `activeLastDay/activeLastWeek/activeLastMonth/topOs`; frontend reads `activeToday/activeThisWeek/newThisWeek` → all zero. Field contract broken in local repo.
- **Pagination field mismatch (F-2):** Backend omits `pages`; frontend expects it → pagination never shows.
- **crashCount/telemetryCount mismatch (F-5):** Backend omits these fields; frontend expects them → always 0.
- **HardwareFingerprint over-returned (F-4):** Backend returns sensitive field not needed for list view.
- **CTP-1 check:** Not applicable — Devices tab does not display tier. Backend returns `licenseTier` but the TSX never renders it (no Tier column in DevicesPage). Dead data in response.
- **DB cascade:** `OnDelete(DeleteBehavior.Cascade)` for both `TelemetryEvents` and `CrashReports` on `DeviceId` FK — deleting a device removes all associated telemetry and crash reports at the DB level. This is correct behavior but must be documented for the admin (F-7 fix note).
- **Unique index:** `HasIndex(d => new { d.LicenseId, d.HardwareFingerprint }).IsUnique()` — confirmed in DbContext. DB verification:

```sql
SELECT indexname, indexdef FROM pg_indexes WHERE tablename='devices';
-- Result: devices_pkey (Id) only. The composite unique index is NOT in the DB — migration did not apply it.
```

The `(LicenseId, HardwareFingerprint)` unique index exists in EF config but NOT in the production DB. DB-level deduplication enforcement is absent — a second registration with the same fingerprint on the same license would insert a duplicate row rather than returning the existing device.

### 3. Security

- **Authorization:** `[Authorize(Roles = "admin")]` at `AdminDeviceController` class level (line 10) ✓. All routes under this controller require admin JWT. Verified: `GET /api/admin/devices` with invalid JWT → 401.
- **IDOR:** Device IDs are GUIDs. Single-tenant admin panel — IDOR risk is minimal. `GetAll` returns all devices across all users (correct for admin view). No user-ID-scoped endpoint exposed.
- **CSRF:** Stateless JWT — no CSRF risk.
- **XSS:** `{d.machineName}` and `{d.osVersion}` rendered as React text nodes (page.tsx:921, 922). React escapes by default. No `dangerouslySetInnerHTML`. Both fields are user-supplied from the desktop app (`MachineName = Environment.MachineName`, `OsVersion = RuntimeInformation.OSDescription` at registration time). Safe under React's default escaping.
- **SQL injection:** Search query (`page.tsx:search` → `AdminDeviceController.GetAll:30`): `query.Where(d => d.MachineName.Contains(search) || d.OsVersion.Contains(search))` — EF Core translates to `WHERE MachineName LIKE '%search%'` with parameterized query. No SQL injection risk.
- **Rate limit:** No rate limiting on admin endpoints (consistent with all prior audits).
- **Audit log:** Missing — CTP-2 confirmed (F-6).
- **Nginx basic auth bypass:** `api.auracore.pro` vhost has no basic auth — same as all prior tabs. Admin endpoints rely on JWT `[Authorize(Roles = "admin")]` only. Direct `curl` to `https://api.auracore.pro/api/admin/devices` without basic auth → 401 (JWT gate active). No bypass possible without valid admin JWT.
- **Unique index gap:** The `(LicenseId, HardwareFingerprint)` composite unique index in EF config is NOT in the DB (migration not applied — confirmed via `pg_indexes`). A race condition could allow two simultaneous device registrations with the same fingerprint on the same license, creating a duplicate. The device registration endpoint (`DeviceController.Register`) would need to handle this gracefully.

### 4. UX

- **Loading state:** No loading spinner while `getDevices()` and `getDeviceStats()` load in parallel. Both fetch in `useEffect` via `useCallback`. Brief empty table flash on mount.
- **Error state:** `getDevices` catch block returns `{ items: [], total: 0, page: 1, pages: 0 }` on error — silent failure, shows empty state. No error toast or error message shown to admin. `getDeviceStats` catch returns `null` — KPI cards use `stats?.totalDevices ?? data.total ?? 0` fallback.
- **Empty state:** Correct — `EmptyState` with Monitor icon + "No devices registered yet" + "Devices will appear after users login from the desktop app".
- **Destructive confirmation:** Not applicable — no destructive actions are accessible in the current UI (F-7).
- **Refresh button:** Present in `PageHeader` (`page.tsx:905`): `onClick={load}` — calls `load()` directly (a `useCallback` that calls `getDevices()` and `getDeviceStats()` again). This is a soft refresh (no page reload). **Bug 3 (B-2) does NOT affect Devices tab** — Refresh button is `load()` not `window.location.reload()`. Data correctly re-fetches without state loss.

### 5. Mobile

CTP-3 applies (same root layout as all other tabs — fixed 260px sidebar, no breakpoints):

- **1024px:** Usable. 5-column table (Machine, OS, Crashes, Telemetry, Last Seen) fits at this width.
- **768px:** Sidebar 260px, content 508px — table starts to compress. "Machine" column with Monitor icon + text overflows. Horizontal scroll required.
- **414px:** Sidebar 260px + main 154px — table severely constrained. Column headers visible but values truncated/wrapped.
- **375px:** Sidebar 260px + main 115px — essentially unusable. Same root layout CTP-3 finding applies.
- **320px:** Sidebar 260px + main 60px — completely broken.
- **KPI grid:** `grid grid-cols-2 lg:grid-cols-4` — correctly stacks to 2-column on mobile. This is the only responsive element in DevicesPage.

### 6. Deployment drift

- **Local repo vs backup controller:**
  - Backup: 90 lines, 4 endpoints (`List`, `GetById`, `Stats`, `Delete`), returns `{items,total,page,pages}`, no `HardwareFingerprint` in list, includes `crashCount`/`telemetryCount`.
  - Local repo: 67 lines (-26%), 2 endpoints (`GetAll`, `GetStats`), returns `{total,page,pageSize,items}` (missing `pages`), exposes `HardwareFingerprint`, lacks `crashCount`/`telemetryCount`, `GetStats` renamed fields breaking frontend contract.
  - CTP-6 CONFIRMED for Devices tab: `-26%` line count matches the prediction from the CTP-6 note in the spec.
- **DLL vs local repo:** DLL contains `GetAll` and `GetStats` — exactly matching local repo. The local repo IS the deployed code. This is not a compile/deploy mismatch — the rollback occurred at source level and the DLL was rebuilt from the rolled-back source.
- **Frontend source vs deployed:** Admin panel source at `/root/admin-panel` matches deployed `/var/www/admin-panel`. Same 26-day deploy gap noted in prior tabs — last deployed ~March 27, source updated with minor changes since but `out/` not regenerated.

---

## CTP-6 Impact for Devices Tab

CTP-6 (security rollback stripped controller endpoints) is confirmed at `-26%` for `AdminDeviceController`. The stripped endpoints are:
1. `[HttpGet("{id}")] GetById` — device detail with recent crashes + telemetry (404)
2. `[HttpDelete("{id}")] Delete` — device revocation via `ExecuteDeleteAsync` (404)

Additionally, the local repo's `GetAll` and `GetStats` have different response shapes than the backup, breaking the frontend data contract in 3 ways (F-1, F-2, F-5).

---

## CTP-5 Hunt (EF Core cascade bug)

**CTP-5 does NOT apply to the Devices tab** in its current form (local repo). The local repo has no `RemoveRange` call — it uses `ExecuteDeleteAsync` (backup) which bypasses EF Core change tracking entirely and issues a direct SQL `DELETE`. EF Core's tracked-entity exclusion bug (CTP-5, surfaced in Users tab) does not affect `ExecuteDeleteAsync`.

However, the cascade behavior is worth noting: deleting a device via `ExecuteDeleteAsync` triggers `ON DELETE CASCADE` at the DB level for `TelemetryEvents` and `CrashReports`. Since the composite unique index `(LicenseId, HardwareFingerprint)` is NOT in the DB (migration gap — confirmed via `pg_indexes`), deleting a device would also permanently remove all crash and telemetry history for that device — admin should be warned before deletion.

---

## Axis-by-axis coverage matrix

| Axis | Status | Key findings |
|---|---|---|
| 1. Functional | Partially broken | F-1 (KPI 0), F-2 (pagination), F-3 (GetById/Delete 404), F-5 (counts 0), F-7 (no delete action) |
| 2. Code+DB sync | Multiple gaps | F-1 (stats field mismatch), F-2 (pages field missing), F-4 (fingerprint over-returned), F-5 (count fields missing), unique index migration gap |
| 3. Security | Acceptable (no critical auth gaps) | F-4 (fingerprint exposure), F-6 (no audit log), unique index not in DB |
| 4. UX | Mostly OK | F-7 (no delete action), F-8 (no staleness indicator), F-9 (no quota warning); Bug 3 NOT reproducible |
| 5. Mobile | Broken below 768px | CTP-3 applies — fixed 260px sidebar |
| 6. Deployment drift | CTP-6 confirmed | 67 vs 90 lines; GetById + Delete stripped; response shape diverged |

---

## Questions for user

1. **Device detail view:** Should admins be able to click a device row and see a detail panel (recent crashes, telemetry events)? The backup had `GetById` implementing this. If yes, this is a priority for the fix phase (F-3 includes the backend restore).

2. **Device deletion cascade:** When an admin deletes a device, all linked crash reports and telemetry events are also deleted (DB cascade). Should there be an option to preserve crash reports even when the device is removed? Or is the cascade acceptable?

3. **HardwareFingerprint retention:** The fingerprint is exposed in `GetAll` responses. Is there a business reason to display it in the admin panel? Or should it be restricted to `GetById` only (or masked entirely)?
