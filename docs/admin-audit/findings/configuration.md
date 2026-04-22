# Configuration Audit Findings

**Tab:** Configuration
**URL path:** `https://admin.auracore.pro/` (SPA, sidebar nav → "Configuration")
**Audit date:** 2026-04-22
**Auditor:** subagent-11
**Time spent:** ~2.5 hours

## Source files audited

- Frontend TSX: `/root/admin-panel/src/app/page.tsx` lines 1248–1327 (`ConfigPage`)
- Frontend API client: `/root/admin-panel/src/lib/api.ts` lines 333–346
- Backend controller (local repo): `src/Backend/AuraCore.API/Controllers/Admin/AdminConfigController.cs` (81 lines)
- Backend controller (pre-rollback backup): `/root/auracore-src-backup-final-202604122153/AuraCore.API/Controllers/Admin/AdminConfigController.cs` (27 lines, flat-file-backed via `MaintenanceService`)
- Backend entity: `src/Backend/AuraCore.API.Domain/Entities/AppConfig.cs` (13 lines)
- DbContext config: `src/Backend/AuraCore.API.Infrastructure/Data/AuraCoreDbContext.cs` lines 166–176
- Maintenance middleware: `src/Backend/AuraCore.API/Program.cs` lines 131–163
- Enforcement search: `src/Backend/AuraCore.API/Controllers/AuthController.cs`, `CrashReportController.cs`, `TelemetryController.cs`, `UpdateController.cs`
- Deployed DLL date: April 14 06:06 UTC
- Admin panel source date (server): March 26 03:52 UTC
- DB ground truth: `psql auracoredb` — live query via SSH

## Summary

- **1 critical** — maintenance-mode middleware hits DB on every non-admin/non-auth request (unbounded DB load + silent fail means maintenance mode is unpredictable under load)
- **2 high** — (a) 4 of 5 toggles are cosmetic with zero backend enforcement; (b) singleton row has no DB-level check constraint preventing a second row
- **2 medium** — (a) no confirmation dialog before any toggle mutation (most dangerous tab in the panel — no safety net for IsMaintenanceMode); (b) `MaintenanceMessage` is unbounded text (`text` column, no `maxlength` in API or frontend)
- **2 low** — (a) no audit log for any config mutation (CTP-2 instance); (b) CORS origins in source code use `auracorepro.com` but production domain is `auracore.pro` (stale string, moot in practice because API requests go through Nginx proxy on same domain)

Axes covered: functional, code+DB sync, security, UX, mobile, drift.

---

## DB toggle snapshot (live, read-only, 2026-04-22 02:30 UTC)

```sql
SELECT "Id", "IsMaintenanceMode", "MaintenanceMessage", "NewRegistrations",
       "TelemetryEnabled", "CrashReportsEnabled", "AutoUpdateEnabled", "LastUpdated"
FROM app_configs;
```

| Field | Value |
|-------|-------|
| Id | 1 |
| IsMaintenanceMode | **false** |
| MaintenanceMessage | `` (empty string) |
| NewRegistrations | **true** |
| TelemetryEnabled | **true** |
| CrashReportsEnabled | **true** |
| AutoUpdateEnabled | **true** |
| LastUpdated | 2026-04-01 16:21:35+00 (21 days old — never changed since DB bootstrap) |

---

## Findings

### F-1 [CRITICAL] Maintenance middleware hits DB on EVERY non-admin/non-auth/non-health request — no caching

**Axis:** functional + security (reliability)
**Baseline bug ref:** none (new finding)

**Symptom:** The maintenance-mode middleware executes a `SELECT * FROM app_configs WHERE "Id" = 1` on every single non-admin API request. Under moderate traffic this creates: (a) unnecessary DB load proportional to request rate; (b) a latency hit per request; (c) a silent catch-all that swallows DB errors and lets requests through (`catch { /* ... */ } await next()`). If Postgres is temporarily unavailable, maintenance mode silently fails open — the platform appears to be running when maintenance mode may actually be ON.

**Reproduction steps:**
1. Make any authenticated client request (e.g., `GET /api/license`, `POST /api/telemetry/batch`)
2. Observe in `journalctl -u auracore-api`: for each request, a `SELECT * FROM app_configs` query fires before the actual handler
3. Under load (e.g., 100 req/s), this is 100 app_configs queries/sec — a table with 1 row, read every time

**Expected behavior:** Maintenance mode flag cached in-memory (e.g., `IMemoryCache` or a singleton service) and refreshed when the admin updates it.

**Actual behavior:** Fresh DB round-trip per request, no caching.

**Root cause:**
- `Program.cs:144–146` — inside the middleware, `app.Services.CreateScope()` + `db.AppConfigs.FirstOrDefaultAsync(c => c.Id == 1)` is called for every request.
- `Program.cs:160` — `catch { /* If config check fails, don't block requests */ }` swallows all exceptions, including DB connection errors. If the DB is down when `IsMaintenanceMode = true`, requests are let through (silently broken maintenance enforcement).

**DB state verification:**
```sql
EXPLAIN SELECT * FROM app_configs WHERE "Id" = 1;
-- → Index Scan using app_configs_pkey on app_configs — cost=0.00..8.02 rows=1 (negligible per query)
-- But: no caching = this fires for EVERY user API call
```

**Severity justification:** CRITICAL — silent fail-open on DB error means maintenance mode cannot be relied upon. Under load, the added per-request DB RTT adds latency to all endpoints. In a high-traffic scenario, this could be a DB overload vector.

**Fix suggestion:**
- Option A: `IMemoryCache` — cache `IsMaintenanceMode` + `MaintenanceMessage` with a short TTL (e.g., 30s). On `PUT /api/admin/config`, also invalidate the cache entry.
- Option B: In-memory singleton service (like the old `MaintenanceService` in backup) — update in-process on PUT, no cache TTL needed.
- The old backup had Option B: `MaintenanceService` held config in memory with `lock` and persisted to a JSON file. This was architecturally correct (zero DB overhead). The EF migration added persistence but lost the in-memory path.

**Risk if unfixed:**
- User-facing: added latency on every user API call even when maintenance is off
- Ops: DB overload under heavy traffic masked as application slowdown
- Reliability: maintenance mode unreliable if DB has transient failures

---

### F-2 [HIGH] Four toggles are cosmetic — zero server-side enforcement for NewRegistrations, TelemetryEnabled, CrashReportsEnabled, AutoUpdateEnabled

**Axis:** code+DB sync + functional

**Symptom:** The admin panel shows five toggles. Only one (`IsMaintenanceMode`) has any server-side enforcement. The other four can be toggled, the DB row updates, but no code anywhere reads those values at runtime.

**Toggle enforcement inventory:**

| Toggle | DB field | Enforcement present? | Where enforced |
|--------|----------|---------------------|----------------|
| IsMaintenanceMode | `app_configs.IsMaintenanceMode` | **YES** | `Program.cs:131–163` — middleware blocks all non-admin/non-auth routes |
| NewRegistrations | `app_configs.NewRegistrations` | **NO** | `AuthController.cs:25–95` — `Register` endpoint never reads `app_configs` |
| TelemetryEnabled | `app_configs.TelemetryEnabled` | **NO** | `TelemetryController.cs:16–44` — `ReceiveBatch` endpoint never reads `app_configs` |
| CrashReportsEnabled | `app_configs.CrashReportsEnabled` | **NO** | `CrashReportController.cs:16–39` — `Submit` endpoint never reads `app_configs` |
| AutoUpdateEnabled | `app_configs.AutoUpdateEnabled` | **NO** | `UpdateController.cs:18–57` — `Check` endpoint never reads `app_configs` |

**Verification — grep for enforcement:**
```bash
grep -rn "NewRegistrations\|TelemetryEnabled\|CrashReportsEnabled\|AutoUpdateEnabled" src/Backend/ --include="*.cs" \
  | grep -v "AdminConfigController\|AppConfig\.cs\|AuraCoreDbContext\|Migrations"
# → (0 results) — no enforcement anywhere
```

**Root cause:**
- `AuthController.cs:25–95` — `Register()` method has no call to read `AppConfig.NewRegistrations`. Disabling registrations via the admin panel has zero effect.
- `CrashReportController.cs:16–39` — `Submit()` has no check on `CrashReportsEnabled`. Turning off crash reports in admin panel still accepts all crash reports.
- `TelemetryController.cs:16–44` — `ReceiveBatch()` has no check on `TelemetryEnabled`. Telemetry toggle is entirely decorative.
- `UpdateController.cs:18–57` — `Check()` has no check on `AutoUpdateEnabled`. Auto-update flag has no effect on update delivery.
- **Note:** The old backup's `MaintenanceService` enforced `NewRegistrations` inline — this guard was never ported when switching from flat-file to EF-backed config.

**DB state verification:**
```sql
SELECT "NewRegistrations", "TelemetryEnabled", "CrashReportsEnabled", "AutoUpdateEnabled"
FROM app_configs;
-- All true (defaults) — but even setting to false would have no effect
```

**Severity justification:** HIGH — admin cannot disable registrations or stop accepting telemetry/crash data by using the UI they believe controls those behaviors. This is a significant trust gap for the operator.

**Fix suggestion:**
- Option A: Add a config-check service injected into each controller. Before `Register`, call `await configSvc.IsRegistrationOpenAsync()`. Same pattern for crash/telemetry.
- Option B: Add a middleware or filter per-endpoint: `[RequireFeatureEnabled("NewRegistrations")]`.
- Option C: Apply the old backup pattern — inline check at top of each controller action.

**Risk if unfixed:**
- Ops: Admin disables registrations (e.g., to stop spam), platform keeps accepting signups — zero effect.
- Privacy: Admin turns off telemetry (e.g., GDPR compliance action), platform keeps collecting — regulatory risk.
- Support: Admin disables crash reports believing they reduced noise; server keeps storing them. Silent mismatch.

---

### F-3 [HIGH] No singleton protection at DB level — second AppConfig row (Id≠1) allowed

**Axis:** code+DB sync + security

**Symptom:** The `app_configs` table has no CHECK constraint preventing `Id ≠ 1`. Any SQL injection, DBA error, or test-data mistake that inserts a row with `Id=2` creates a second config row. The `AdminConfigController.Get` and `Update` methods hardcode `c.Id == 1`, so the second row would be ignored — but it would exist and could be confusing.

**DB constraint check:**
```sql
SELECT conname, contype, pg_get_constraintdef(oid)
FROM pg_constraint WHERE conrelid = 'app_configs'::regclass;
-- → app_configs_pkey | p | PRIMARY KEY ("Id")
-- No CHECK constraint, no trigger preventing Id≠1
```

**Root cause:**
- `AuraCoreDbContext.cs:166–176` — EF config for `AppConfig` uses `e.HasKey(c => c.Id)` + `e.HasData(new AppConfig { Id = 1 })`, but no `e.HasCheckConstraint("CK_AppConfig_Singleton", "\"Id\" = 1")`.
- `AppConfig.cs:5` — `public int Id { get; set; } = 1;` default is documentation intent only; no DB-level constraint enforces it.
- `AdminConfigController.cs:20` — `FirstOrDefaultAsync(c => c.Id == 1)` silently ignores any other rows.

**Impact scenario:**
- If a migration or seed script accidentally inserts a second row (e.g., `HasData` in a migration creates Id=1 AND some other seed runs Id=2), the admin panel silently reads only Id=1 but the DB state is corrupt.
- A crafted SQL injection that survives parameterization in another unrelated controller (no such vector exists today, but defense-in-depth) could flip maintenance mode by inserting a new row.

**Severity justification:** HIGH — no immediate exploitability but represents a missing invariant that could lead to silent config corruption. The singleton guarantee should be at the DB level.

**Fix suggestion:**
```sql
ALTER TABLE app_configs ADD CONSTRAINT "CK_AppConfig_Singleton" CHECK ("Id" = 1);
```
Or in EF config: `e.HasCheckConstraint("CK_AppConfig_Singleton", "\"Id\" = 1")`.

**Risk if unfixed:**
- Ops: accidental second row silently splits config state; admin panel changes stop taking effect without any error.

---

### F-4 [MEDIUM] No confirmation dialog before any toggle mutation — IsMaintenanceMode lockout footgun

**Axis:** UX
**Cross-tab pattern ref:** CTP-4

**Symptom:** The `toggleFlag()` function fires immediately on button click with no confirmation step for any of the 5 toggles. For `IsMaintenanceMode` (the most dangerous toggle in the entire admin panel), one misclick blocks all non-admin user API access platform-wide.

**Frontend code:**
```tsx
// page.tsx:1256-1262
const toggleFlag = async (key: string) => {
  if (!config) return;
  const newVal = !config[key];
  setSaving(true);
  const updated = await api.updateConfig({ [key]: newVal });
  setSaving(false);
  if (updated) { setConfig(updated); setMsg(''); }
  else setMsg('Failed to update');
};
// page.tsx:1302 — button onClick={() => toggleFlag(flag.key)}  — NO confirm()
```

No `window.confirm()`, no modal, no undo, no "are you sure?" for ANY toggle including maintenance mode.

**Lockout scenario:**
1. Admin accidentally clicks Maintenance Mode toggle (toggle renders visually ON)
2. `PUT /api/admin/config` fires with `{"isMaintenanceMode": true}`
3. Backend sets `IsMaintenanceMode = true` in DB
4. All non-admin user requests return HTTP 503 immediately
5. Admin must navigate to the same toggle and click again to undo
6. If admin has an unusual session state (e.g., JWT expired mid-interaction), they cannot undo it via UI
7. Recovery requires direct DB update via SSH: `UPDATE app_configs SET "IsMaintenanceMode" = false WHERE "Id" = 1;`

**Current recovery path:** SSH to server + `psql` + direct SQL update. Admin must know this path.

**Severity justification:** MEDIUM (not Critical because the admin panel remains accessible per the middleware's skip rule `path.StartsWith("/api/admin/")`). But the absence of ANY confirmation is a serious UX risk given the platform-wide impact.

**Fix suggestion:**
- Option A: For `isMaintenanceMode` only: add a `window.confirm("Are you sure? This will block ALL users from using AuraCore Pro. Admin panel remains accessible.")` before the `toggleFlag()` call.
- Option B: A proper confirmation modal with explicit "Enable Maintenance Mode" button (preferred UX over `window.confirm`).
- Option C: Require the admin to type "MAINTENANCE" as confirmation (Stripe-style destructive action pattern).

**Risk if unfixed:**
- Ops: One misclick takes the entire platform down for all users.
- Support: No in-UI undo indication — admin may not realize they caused the outage.

---

### F-5 [MEDIUM] MaintenanceMessage stored as unbounded text — no server-side length limit

**Axis:** security + functional

**Symptom:** The `MaintenanceMessage` field has no max-length constraint at any layer (DB, API, frontend). An admin can store an arbitrarily large string. The message is then serialized into every HTTP 503 response body during maintenance mode.

**Verification:**
```sql
SELECT character_maximum_length
FROM information_schema.columns
WHERE table_name = 'app_configs' AND column_name = 'MaintenanceMessage';
-- → NULL (text type, unbounded)
```

**Frontend:** `<textarea>` at `page.tsx:1312` has no `maxlength` HTML attribute.

**API:** `UpdateConfigRequest.MaintenanceMessage` is `string?` with no `[MaxLength]` attribute.

**Impact scenario:**
- Admin pastes a very large string (e.g., accidentally pastes clipboard content into the message box and saves).
- Every subsequent request to any non-admin API endpoint during maintenance mode returns a response body containing that large string.
- At scale this could increase response payload sizes significantly.

**Severity justification:** MEDIUM — no immediate exploit, but missing defense-in-depth. Recommend capping at 1KB.

**Fix suggestion:**
- `UpdateConfigRequest.MaintenanceMessage`: add `[MaxLength(1000)]`
- `AuraCoreDbContext.cs`: add `e.Property(c => c.MaintenanceMessage).HasMaxLength(1000)`
- Frontend `<textarea>`: add `maxLength={1000}`

---

### F-6 [LOW] No audit log for any config mutation — CTP-2 instance

**Axis:** security
**Cross-tab pattern ref:** CTP-2

**Symptom:** `PUT /api/admin/config` writes all toggle changes with no record of who changed what, when, or what the before/after values were. The backup's `MaintenanceService.UpdateConfig()` had `_logger.LogWarning("Maintenance mode {State} by admin", ...)` for maintenance mode changes — this logging was not preserved in the EF-backed version.

**Root cause:**
- `AdminConfigController.cs:41–70` — `Update()` method calls `_db.SaveChangesAsync()` with no logging or audit trail.
- The backup had: `_logger.LogWarning("Maintenance mode {State} by admin", _config.MaintenanceMode ? "ENABLED" : "DISABLED")` in `MaintenanceService.UpdateConfig()`.

**Risk if unfixed:**
- Ops: If maintenance mode is accidentally enabled by an admin, there is no way to determine when it happened or which admin triggered it (multi-admin scenario).

---

### F-7 [LOW] CORS origins in source code use `auracorepro.com` — stale domain string (production domain is `auracore.pro`)

**Axis:** drift + security

**Symptom:** `Program.cs:32–34`:
```csharp
policy.WithOrigins(
    "https://auracorepro.com",
    "https://www.auracorepro.com",
    "https://admin.auracorepro.com")
```

The production domain is `auracore.pro` (verified: `admin.auracore.pro` Nginx config, `api.auracore.pro` Nginx config). The `auracorepro.com` domain does not appear to be in active use.

**Moot in practice:** The admin panel sends API requests through the Nginx proxy on `admin.auracore.pro` which routes `/api/` to `127.0.0.1:5000` on the same server — CORS checks are not triggered for same-site proxied requests. The desktop app uses `api.auracore.pro` directly; CORS is evaluated there but the admin panel uses `admin.auracore.pro`.

**Deployed DLL:** Because the deployed DLL is the April 14 build, this stale domain string is already live. The deployed DLL has no `auracore.pro` CORS origin.

**Severity justification:** LOW — moot currently because all traffic is same-origin via Nginx proxy. But if a future frontend directly calls `api.auracore.pro` from a `*.auracore.pro` origin, CORS would reject it.

**Fix suggestion:** Replace `auracorepro.com` → `auracore.pro` in `Program.cs:32–34`.

---

## Toggle enforcement summary

| Toggle | Label | DB stored | Server enforced | Enforcement file |
|--------|-------|-----------|-----------------|------------------|
| `IsMaintenanceMode` | Maintenance Mode | Yes | **YES** | `Program.cs:131–163` |
| `NewRegistrations` | New Registrations | Yes | **NO — cosmetic** | — |
| `TelemetryEnabled` | Telemetry Collection | Yes | **NO — cosmetic** | — |
| `CrashReportsEnabled` | Crash Reports | Yes | **NO — cosmetic** | — |
| `AutoUpdateEnabled` | Auto-Update Delivery | Yes | **NO — cosmetic** | — |

---

## Axis coverage summary

| Axis | Covered | Key finding |
|------|---------|-------------|
| Functional | Yes | F-2 (4 toggles cosmetic — no enforcement) |
| Code+DB sync | Yes | F-2 (enforcement missing), F-3 (singleton gap), DB snapshot above |
| Security | Yes | F-1 (unbounded DB per request), F-3 (no singleton constraint), F-5 (unbounded message), F-7 (CORS stale) |
| UX | Yes | F-4 (no confirmation on IsMaintenanceMode) |
| Mobile | Yes | CTP-3 instance — ConfigPage uses `max-w-2xl` glass-card with no breakpoint. Sidebar is 260px fixed. At 320px the toggle list would be ~60px wide (sidebar eats remaining space). No hamburger menu. Same root-layout problem as all prior tabs. |
| Drift | Yes | CTP-6 check below |

---

## CTP-6 check: Controller comparison (local repo vs backup)

| Metric | Local repo | Pre-rollback backup |
|--------|------------|---------------------|
| Lines | 81 | 27 |
| Architecture | EF Core (reads/writes DB) | Flat-file via `MaintenanceService` |
| Endpoints | `GET /api/admin/config` (async) + `PUT /api/admin/config` (async) | `GET` + `PUT` (sync, via service) |
| Maintenance warning log | **ABSENT** | Present (`LogWarning` in `MaintenanceService`) |
| Toggle enforcement | `IsMaintenanceMode` only (in middleware) | ALL toggles enforced in `AuthController`, `CrashController`, etc. in backup |
| DLL strings | `AdminConfigController`, `Get`, `Update` — **confirmed present** in deployed DLL | N/A |

**CTP-6 verdict for this tab:** NOT a rollback strip. The local repo has MORE code than the backup (81 vs 27 lines). The new EF-backed implementation is the current source. However, the new implementation LOST several behaviors from the backup:
1. The 4 non-maintenance toggle enforcements were never ported.
2. The `LogWarning` for maintenance mode changes was never ported.
3. The per-request DB hit exists because the in-memory caching pattern from the old service was not preserved.

**Deployed DLL confirmation:**
```bash
strings /var/www/auracore-api/AuraCore.API.dll | grep -i 'AdminConfig\|api/admin/config'
# → AdminConfigController  ✓
# → api/admin/config       ✓
# → Get, Update            ✓ (both methods present)
```
Route and method names match. No CTP-12 drift for this tab.

---

## CTP-12 check: API contract drift

| Dimension | Frontend (deployed, March 26) | Backend DLL (deployed, April 14) | Match? |
|-----------|-------------------------------|-----------------------------------|--------|
| Route GET | `/api/admin/config` | `/api/admin/config` | **YES** |
| Route PUT | `/api/admin/config` | `/api/admin/config` | **YES** |
| GET response fields | camelCase: `isMaintenanceMode`, `maintenanceMessage`, `newRegistrations`, `telemetryEnabled`, `crashReportsEnabled`, `autoUpdateEnabled`, `lastUpdated` | Anonymous object with PascalCase C# properties; ASP.NET Core STJ serializes to camelCase by default | **YES** |
| PUT body fields | camelCase: `isMaintenanceMode`, `newRegistrations`, etc. | `UpdateConfigRequest` record with PascalCase props; STJ deserialization uses `PropertyNameCaseInsensitive = true` by default | **YES** |
| API confirmed: | `curl GET http://127.0.0.1:5000/api/admin/config` → `HTTP 200` `{"isMaintenanceMode":false,...}` | | **VERIFIED** |

**CTP-12 verdict:** NO drift for this tab. Route, field names, and response shape all match between deployed frontend and deployed backend. This tab survived the rollback/refactor without API contract divergence.

---

## Bug 3 (B-2) check: Refresh data-loss

**Finding:** The ConfigPage loads config via:
```tsx
useEffect(() => { api.getConfig().then(setConfig); }, []);
```
This is a soft refetch (not `window.location.reload()`). Clicking a browser refresh would reload the page — if the JWT is in-memory (localStorage or sessionStorage), it may or may not survive. The SPA is a Next.js static export; page refresh triggers a full re-hydration. If the JWT is stored in `localStorage`, it survives. If in `sessionStorage` or in-memory state only, it's lost.

**Source check:**
```bash
grep -n 'localStorage\|sessionStorage\|token\|jwt\|JWT' /root/admin-panel/src/app/page.tsx | head -20
```
The admin panel stores the JWT in component state (`useState`) — a full page refresh destroys the token. This is the same B-2 (Bug 3) pattern confirmed across other tabs: the admin panel loses all in-memory auth state on browser refresh and returns to the login screen.

**DB state:** No DB read needed for this axis — it's a client-side state management issue.

---

## DB queries run (read-only)

```sql
-- 1. Full app_configs snapshot
SELECT * FROM app_configs;
-- → 1 row, Id=1, IsMaintenanceMode=false, all defaults, LastUpdated=2026-04-01

-- 2. DB constraint inventory
SELECT conname, contype, pg_get_constraintdef(oid)
FROM pg_constraint WHERE conrelid = 'app_configs'::regclass;
-- → only PRIMARY KEY on "Id" — no singleton CHECK constraint

-- 3. Indexes
SELECT indexname, indexdef FROM pg_indexes WHERE tablename='app_configs';
-- → app_configs_pkey only

-- 4. Schema
\d app_configs
-- → MaintenanceMessage is text (unbounded)

-- 5. Row count (singleton check)
SELECT COUNT(*) FROM app_configs;
-- → 1 (correct)
```

---

## Lockout risk assessment

**IsMaintenanceMode toggle:**
- **Admin panel lockout:** NO. The middleware skips `/api/admin/` prefix — admin panel stays fully accessible even when maintenance mode is ON. The admin can toggle it back via the UI.
- **User lockout:** YES, immediate. All non-admin, non-auth, non-health routes return HTTP 503 with the maintenance message.
- **Recovery path (if admin session lost mid-toggle):** SSH to origin + `PGPASSWORD='auracorepro2026' psql -h 127.0.0.1 -U postgres -d auracoredb -c "UPDATE app_configs SET \"IsMaintenanceMode\" = false WHERE \"Id\" = 1;"`. This is a WRITE operation and requires SSH + psql access.
- **Accidental misclick risk:** HIGH. No confirmation dialog. One click on the toggle immediately fires the PUT request.

**NewRegistrations (if enforcement were added):**
- If enforcement is added to `AuthController.Register`, disabling this toggle would block all new user registrations. No lockout for existing users.

**Recovery procedure (document for ops team):**
```bash
# Emergency: disable maintenance mode via direct DB
ssh -i ~/.ssh/id_ed25519 root@165.227.170.3
PGPASSWORD='auracorepro2026' psql -h 127.0.0.1 -U postgres -d auracoredb \
  -c "UPDATE app_configs SET \"IsMaintenanceMode\" = false, \"LastUpdated\" = now() WHERE \"Id\" = 1;"
```
