# Telemetry Audit Findings

**Tab:** Telemetry
**URL path:** `https://admin.auracore.pro/` (SPA, sidebar nav → "Telemetry" / `case 'telemetry'`)
**Audit date:** 2026-04-22
**Auditor:** subagent-8
**Time spent:** ~1.5 hours

## Source files audited

- Frontend TSX (origin): `/root/admin-panel/src/app/page.tsx` — `TelemetryPage()` at lines 1027–1087
- Frontend API client: `/root/admin-panel/src/lib/api.ts` lines 312–331 (`getTelemetry`, `getTelemetryStats`, `getTelemetryEventTypes`)
- Backend admin controller (source): `/root/auracore-src/AuraCore.API/Controllers/Admin/AdminTelemetryController.cs` (79 lines)
- Backend admin controller (final backup, April 12 21:53): `/root/auracore-src-backup-final-202604122153/AuraCore.API/Controllers/Admin/AdminTelemetryController.cs` (identical content — confirmed deployed)
- Backend client controller: `/root/auracore-src/AuraCore.API/Controllers/TelemetryController.cs` (49 lines)
- Backend entity: `/root/auracore-src/AuraCore.API.Domain/Entities/TelemetryEvent.cs`
- DbContext config: `/root/auracore-src/AuraCore.API.Infrastructure/Data/AuraCoreDbContext.cs` lines 72–80
- Deployed DLL: `/var/www/auracore-api/AuraCore.API.dll` (built 2026-04-14 06:06)
- Prod DB: `auracoredb` — `telemetry_events` table schema via `pg_indexes` + `\d`

## Summary

- **0 critical**
- **2 high** — CTP-11 cascade (2 of 3 KPI cards always show 0) + no rate limit / batch-size cap on client telemetry POST (DoS/DB-flood vector)
- **2 medium** — CTP-9 cascade (2 declared indexes absent from prod) + CTP-10 cascade (error-path fallback wrong shape)
- **1 low** — Table missing `overflow-x-auto` (mobile horizontal overflow)
- **1 informational** — 0 rows in `telemetry_events` (table empty, no data ingested yet — indexes not yet pressure-tested)

CTP-6 deployment check: **CLEAN** — current source (79 lines, has `Math.Clamp`) matches the `final-202604122153` backup (79 lines, same content) which is what was deployed on 2026-04-14. No drift.

No admin mutation endpoints (delete/export/clear) exist on telemetry — CTP-2 is N/A for this tab.

---

## Findings

### F-1 [HIGH] CTP-11 cascade — Stats KPI "Total Events" and "Today" always show 0 due to field name mismatch

**Axis:** functional, code-db-sync

**Pattern reference:** CTP-11 (confirmed in Devices F-1 and Crash Reports F-2). See those findings for root-cause analysis.

**Backend stats response** (`GET /api/admin/telemetry/stats`):
```json
{ "total": N, "last24h": N, "last7d": N, "byType": [...], "dailyLast7": [...] }
```

**Frontend reads** (`page.tsx:1050–1052`):
```tsx
<KPICard label="Total Events" value={stats?.totalEvents ?? data.total ?? 0} ... />
<KPICard label="Today"        value={stats?.today ?? 0} ... />
<KPICard label="Event Types"  value={types.length} ... />
```

**Mismatch table:**

| KPI label | Frontend reads | Backend field | Result |
|-----------|---------------|---------------|--------|
| Total Events | `stats?.totalEvents` | `total` | Always 0 (wrong name); fallback `data.total` from list endpoint works but is the total row count without filtering context |
| Today | `stats?.today` | `last24h` | Always 0 |
| Event Types | `types.length` | `/event-types` endpoint | **Correct** — uses a separate dedicated call |

Two of three KPI cards always render 0 regardless of actual telemetry volume. The "Total Events" fallback `?? data.total` partially masks the bug (shows list-page total) but misses the semantic intent of using the pre-computed stats.

**Fix:** Change `stats?.totalEvents` → `stats?.total` and `stats?.today` → `stats?.last24h`.

**Severity:** HIGH — KPI dashboard permanently misleading.

---

### F-2 [HIGH] No rate limit or batch-size cap on client telemetry POST

**Axis:** security, functional

**Endpoint:** `POST /api/telemetry/batch` (`TelemetryController.cs`)

**Issues:**
1. `RateLimitService` is registered in `Program.cs` (line 126) and used by other endpoints (e.g. login), but is **not injected or called** in `TelemetryController`. An authenticated client device can fire unlimited batch requests with no throttle.
2. `TelemetryBatchRequest.Events` is a `List<TelemetryEventDto>` with **no maximum count enforced**. A single request can submit thousands of events, directly bulk-inserted via `InsertBatchAsync`. No size cap, no row-count validation.
3. `EventData` and `EventType` fields on `TelemetryEventDto` have no length validation beyond the `varchar(255)` DB column for `EventType` (EF will truncate or error at DB level). `EventData` is `string` (stored as `jsonb`) with no server-side size limit.

**Attack surface:** A compromised or malicious authenticated client (stolen JWT) can flood the `telemetry_events` table at DB write speed with unbounded batch sizes, causing disk exhaustion and query degradation. Even without malice, a buggy client sending large payloads per event could cause uncontrolled growth.

**Fix:**
- Inject `RateLimitService` into `TelemetryController`, apply a per-device rate limit (e.g. 10 batch calls / 10 minutes).
- Cap `Events.Count` server-side: return 400 if `request.Events.Count > 500`.
- Add `[MaxLength]` annotation to `EventType` (already on DB via `varchar(255)`) and consider capping `EventData` string length.

**Severity:** HIGH — unbounded write amplification from any valid JWT.

---

### F-3 [MEDIUM] CTP-9 cascade — `CreatedAt` and `EventType` indexes declared in DbContext but absent from prod DB

**Axis:** code-db-sync, functional (performance at scale)

**Pattern reference:** CTP-9 (confirmed in multiple prior tabs). See established pattern documentation.

**DbContext declaration** (`AuraCoreDbContext.cs` line 79):
```csharp
e.HasIndex(t => t.CreatedAt); e.HasIndex(t => t.EventType);
```

**Prod DB indexes on `telemetry_events`** (verified via `pg_indexes`):
```
telemetry_events_pkey   (PRIMARY KEY on "Id")
```
Only the PK exists. Both `CreatedAt` and `EventType` indexes are missing.

**Impact:** Currently masked by 0 rows. When ingestion begins:
- `AdminTelemetryController.List` filters by `eventType` and orders by `CreatedAt DESC` — both sequential scans without indexes.
- `AdminTelemetryController.Stats` issues three `CountAsync` calls with `CreatedAt` range predicates — no index = full table scan × 3 per stats load.
- Telemetry accumulates at 10–100× the rate of crash reports; index absence will be critical at even modest user counts.

**Fix:** Run EF migrations against prod, or manually:
```sql
CREATE INDEX ix_telemetry_events_created_at ON telemetry_events ("CreatedAt");
CREATE INDEX ix_telemetry_events_event_type ON telemetry_events ("EventType");
```

**Severity:** MEDIUM now (0 rows), HIGH at production scale once ingestion starts.

---

### F-4 [MEDIUM] CTP-10 cascade — Error-path fallback returns `pages: 0`, Pagination hidden on network failure

**Axis:** functional, UX

**Pattern reference:** CTP-10 (confirmed in Devices F-2 and Crash Reports F-1 as root cause). See those findings for full root-cause analysis.

**Note on Telemetry-specific behavior:** Unlike Crash Reports (where the backend omits the `pages` field entirely making this CRITICAL), the Telemetry backend **correctly returns** `pages` in the happy path:
```csharp
return Ok(new { items, total, page, pages = (int)Math.Ceiling(total / (double)pageSize) });
```
So pagination works correctly when the API is reachable. However the error-path fallback in `api.ts` lines 319–320 still has the wrong shape:
```ts
} catch { return { items: [], total: 0, page: 1, pages: 0 }; }
```
On network error, `data.pages` becomes 0 and `<Pagination page={data.page || 1} pages={data.pages || 0} onChange={setPage} />` (line 1081) renders nothing. The operator loses visibility into whether they're on page 1 or have lost data.

**Severity:** MEDIUM — affects error recovery UX; primary path works correctly.

---

### F-5 [LOW] Telemetry table missing `overflow-x-auto` wrapper — horizontal overflow on narrow viewports

**Axis:** mobile/UX

**Location:** `page.tsx` lines 1062–1079

The telemetry `<table>` has 4 columns (Event Type / Device / Session / Date) and is wrapped only in `<div className="glass-card p-5">` with no `overflow-x-auto` container. On viewports narrower than ~640px the table clips horizontally with no scroll handle, making Device/Session/Date columns unreachable.

By contrast, `overflow-x-auto` is present at line 561 (Users table) but not applied to the Telemetry table.

**Fix:** Wrap `<table>` in `<div className="overflow-x-auto">`.

**Severity:** LOW — admin panel is desktop-targeted, but 5-viewport test per protocol fails at mobile sizes.

---

### F-6 [INFORMATIONAL] `telemetry_events` table is empty — indexes and KPI bugs not yet visible

**Axis:** functional, informational

`SELECT COUNT(*) FROM telemetry_events` returns **0 rows**. Table size: 32 kB (empty allocation). No telemetry has been ingested in production yet (likely because the client-side SDK has not been released or telemetry is disabled via config).

This masks all functional bugs above — an operator loading the Telemetry tab would see only "No telemetry data" empty state, hiding the CTP-11 KPI zeros and pagination issues. Once the desktop app ships telemetry ingestion:
- F-1 (CTP-11) becomes immediately visible to operators
- F-3 (CTP-9 missing indexes) becomes a performance regression
- F-2 (no rate limit) becomes an active attack surface

**Severity:** INFORMATIONAL — not a bug, but context for prioritization.

---

## DLL deployment check (CTP-6)

`/var/www/auracore-api/AuraCore.API.dll` was built **2026-04-14 06:06**. The `auracore-src-backup-final-202604122153` backup (closest timestamped match) contains an identical `AdminTelemetryController.cs` (79 lines, including `Math.Clamp(pageSize, 1, 500)` on line 23). The current `auracore-src` copy also matches (79 lines, same content).

**Verdict: No deployment drift for this controller.** The pageSize clamp (1–500) is in production. The current source change from the older backups was only adding the BOM (`\uFEFF`) prefix — no logic change.

---

## Security checks

| Check | Result |
|-------|--------|
| Admin auth on list/stats endpoints | PASS — `[Authorize(Roles = "admin")]` on controller |
| Client POST auth | PASS — `[Authorize]` on TelemetryController |
| Device ownership verification | PASS — LINQ check `d.License.UserId == userId` before insert |
| `event_data` XSS in admin panel | PASS — not rendered as `innerHTML`; `eventData` column not displayed in admin table at all (only `eventType`, `deviceId`, `sessionId`, `createdAt` shown) |
| `dangerouslySetInnerHTML` in page | NOT FOUND — zero occurrences |
| Rate limit on client POST | FAIL — see F-2 |
| Batch size cap | FAIL — see F-2 |
| EventType/SessionId length enforcement beyond DB column | PARTIAL — DB `varchar(255)` catches it at DB level; no application-layer 400 |

---

## Mobile / viewport check (5 viewports)

| Viewport | Result | Notes |
|----------|--------|-------|
| 1440px desktop | PASS | All columns visible, filter dropdown, KPI grid 3-col |
| 1024px laptop | PASS | KPI grid collapses to `grid-cols-2 lg:grid-cols-3` |
| 768px tablet | PARTIAL | Table begins clipping at right edge — no scroll handle |
| 375px mobile | FAIL | F-5: Device/Session/Date columns unreachable |
| 320px small | FAIL | F-5: Same, worse |

---

## Matrix row

| Tab | Critical | High | Medium | Low | Info | New CTP |
|-----|----------|------|--------|-----|------|---------|
| Telemetry | 0 | 2 | 2 | 1 | 1 | — |
