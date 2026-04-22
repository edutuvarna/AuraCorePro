# Audit Log Tab — Deep Audit Findings

**Tab:** Audit Log
**URL path:** `https://admin.auracore.pro/` (SPA, sidebar nav → "Audit Log")
**Audit date:** 2026-04-22
**Auditor:** subagent-9 (Task 9)
**Time spent:** ~2 hours

## Source files audited

- Frontend JS (deployed, minified): `/var/www/admin-panel/_next/static/chunks/app/page-9bf9edb4333e55cf.js` — `eP()` component (Audit Log), `getLoginAttempts()`, `getLoginAttemptStats()`, nav config
- Backend controller (local source): `src/Backend/AuraCore.API/Controllers/Admin/AdminAuditLogController.cs` (82 lines)
- Backend controller (backup, 2026-04-12): `/root/auracore-src-backup-final-202604122153/AuraCore.API/Controllers/Admin/AdminAuditController.cs` (82 lines) — different filename, different route, different response shape
- DbContext (backup): `/root/auracore-src-backup-final-202604122153/AuraCore.API.Infrastructure/Data/AuraCoreDbContext.cs` lines 135–142 (LoginAttempt entity + HasIndex declarations)
- Live DB: `auracoredb` on `165.227.170.3` — `pg_indexes` for `login_attempts`, all table names
- Live API: `http://localhost:5000/api/admin/audit-log` + `.../audit-log/stats`

## Summary

- **2 critical** — (1) No dedicated `audit_log` table: the entire Audit Log tab is a "Failed Logins" view mislabeled as an audit log; admin mutations are never recorded anywhere. (2) Route/key mismatch: frontend calls `/api/admin/audit/login-attempts` (404) while live API is at `/api/admin/audit-log`; even if route matched, key `items` vs expected `attempts` means the table always renders empty.
- **1 high** — Stats KPI shape mismatch (CTP-11): frontend reads `d.successful24h / d.failed24h / d.uniqueIps / d.suspiciousIps` but live API returns `{successful, failed, last24h, failedLast24h, topFailedIps, topFailedEmails}` — all 4 KPI cards show `0`.
- **2 medium** — (1) CTP-9: two composite indexes declared in DbContext (`{Email,CreatedAt}` and `{IpAddress,CreatedAt}`) are absent from prod (only PK index exists — `__EFMigrationsHistory` empty). (2) Unbounded `login_attempts` growth: no purge/retention policy — 419 rows since 2026-03-22 at current rate.
- **1 low** — PII (email + IP) stored indefinitely with no GDPR right-to-erasure mechanism.
- **1 info** — Frontend filter passes `&filter=true/false` but live controller parameter is `success=true/false` — filter toggle is silently ignored.

Axes covered: functional, code+DB sync, security, UX, mobile, deployment drift.

---

## Ground Truth: No Dedicated Audit Log Table (CTP-2 Canonical Evidence)

```sql
-- Run on auracoredb 2026-04-22
SELECT table_name FROM information_schema.tables
WHERE table_schema='public'
AND table_name IN ('audit_log','audit_logs','admin_audit_log','admin_logs');
-- Result: 0 rows
```

**All 14 public tables in `auracoredb`:**
`PasswordResets, __EFMigrationsHistory, app_configs, app_updates, crash_reports, devices, licenses, login_attempts, password_reset_codes, payments, refresh_tokens, subscriptions, telemetry_events, users`

No audit log table of any name exists. The Audit Log tab reads exclusively from `login_attempts` — a security/auth table written by the authentication middleware on every login attempt. It is NOT an admin action audit trail.

---

## Findings

### F-1 [CRITICAL] No dedicated audit log — tab is mislabeled "Audit Log" but shows only login attempts

**Axis:** code-db-sync / functional  
**CTP ref:** CTP-2 (admin mutations not logged — canonical instance)

**Symptom:** The "Audit Log" tab in the sidebar, and the page title "Audit Log / Login attempts and security events", imply a comprehensive audit trail of admin actions. In reality the tab displays only authentication events (login successes + failures) from the `login_attempts` table. Zero admin mutations (grant subscription, delete user, revoke license, ban IP, change config) are recorded anywhere.

**Ground truth DB verification:**
```sql
-- No audit_log table of any name exists (query above: 0 rows)
-- login_attempts columns: Id, Email, IpAddress, Success, CreatedAt
-- 419 rows, all from auth middleware, zero admin action rows
```

**Root cause:**
- `AdminAuditLogController.cs` route `api/admin/audit-log` queries `_db.LoginAttempts` exclusively.
- There is no `AuditLog` DbSet, no audit log migration, no interceptor/middleware writing admin actions.
- The page title "Audit Log" is accurate only if interpreted narrowly as "login attempt log" — but admins reasonably expect to see who granted subscriptions, deleted users, or changed config.

**CTP-2 scope:** this is the definitive evidence for CTP-2 (missing audit log coverage). Actions confirmed un-logged by cross-referencing all prior tabs:
- Users tab: `DELETE /api/admin/users/{id}` — not logged
- Users tab: `POST /api/admin/users/{id}/reset-password` — not logged
- Subscriptions tab: `POST /api/admin/subscriptions/grant` — not logged
- Licenses tab: `POST /api/admin/licenses/{id}/revoke` — not logged
- Licenses tab: `POST /api/admin/licenses/{id}/activate` — not logged
- IP Whitelist: `POST /api/admin/ip-whitelist` (add), `DELETE ./{id}` — not logged
- Configuration: any key change — not logged
- Updates: upload/publish/delete — not logged

**Fix suggestion (Phase 6 Item 8):**
1. Add `admin_audit_log` table: `(Id, AdminEmail, Action, TargetType, TargetId, Before, After, CreatedAt)`.
2. Inject `IAuditWriter` (EF savepoint pattern or dedicated service) into each Admin controller.
3. Rename tab to "Login Attempts" until actual admin audit log is implemented; or rename only when the table exists.

**Risk if unfixed:**
- Zero traceability: no record of who deleted a user, granted a subscription, or changed a config value.
- Compliance/GDPR concern: right to erasure requests against user data have no audit trail.
- Security incident response impossible: if admin account is compromised, there is no log of what was done.

---

### F-2 [CRITICAL] Route mismatch + payload key mismatch — Audit Log table always renders empty

**Axis:** functional / deployment drift  
**CTP ref:** CTP-10 (pagination shape), CTP-6 (controller divergence)

**Symptom:** The Audit Log tab in production always shows "No login attempts" even though `login_attempts` has 419 rows.

**Root cause — two-layer breakage:**

**Layer 1: Wrong API route (404)**
- Frontend JS `getLoginAttempts()` calls: `GET /api/admin/audit/login-attempts?page=1&pageSize=25`
- Live API route (from DLL strings + curl): `GET /api/admin/audit-log`
- Result: every call returns **HTTP 404**. Frontend catch block: `return {attempts:[], total:0}` — silent empty state, no error displayed.

**Layer 2: Key name mismatch (even if route matched)**
- Frontend reads `i.attempts` to render table rows: `(i.attempts||[]).map(...)`
- Live API `GetAll` returns: `{ total, page, pageSize, items: [...] }`
- Frontend default state: `{attempts:[], total:0}` — falls through to empty array even on 200.

**Evidence from live API (200 with correct direct call):**
```json
// curl GET /api/admin/audit-log?page=1&pageSize=3 → HTTP 200
{
  "total": 417,
  "page": 1,
  "pageSize": 3,
  "items": [
    {"id":713,"email":"admin@auracore.pro","ipAddress":"127.0.0.1","success":true,"createdAt":"..."},
    ...
  ]
}
```

**Controller divergence (CTP-6):**  
Backup `AdminAuditController.cs` (route `api/admin/audit`) was the frontend's source contract. The current source `AdminAuditLogController.cs` (route `api/admin/audit-log`) was written post-rollback with a different route and different list key (`items` vs `attempts`). The frontend was never updated to match.

**Fix suggestion:** Either:
- Option A: Change `AdminAuditLogController.cs` route to `api/admin/audit` and rename `items` → `attempts` in `GetAll` response.
- Option B: Update frontend `getLoginAttempts()` to call `/api/admin/audit-log` and read `.items` instead of `.attempts`.

**Risk if unfixed:**
- Audit Log tab is functionally broken in production — 0 rows displayed, 419 exist.

---

### F-3 [HIGH] Stats KPI cards all show 0 — shape mismatch (CTP-11)

**Axis:** functional / code-db-sync  
**CTP ref:** CTP-11 (stats shape mismatch)

**Symptom:** The 4 KPI stat cards (Successful 24h, Failed 24h, Unique IPs, Suspicious IPs) all show `0`.

**Root cause:**  
Frontend reads: `d.successful24h`, `d.failed24h`, `d.uniqueIps`, `d.suspiciousIps`  
Live API `GetStats` returns: `{ total, successful, failed, last24h, failedLast24h, topFailedIps, topFailedEmails }`

No field in the API response matches the frontend's expected field names. All four fall through to `?? 0`.

**Live API stats response (2026-04-22):**
```json
{
  "total": 417,
  "successful": 80,
  "failed": 337,
  "last24h": 34,
  "failedLast24h": 1,
  "topFailedIps": [{"ipAddress":"127.0.0.1","count":1}],
  "topFailedEmails": [{"email":"ozgurdeniz807@gmail.com","count":1}]
}
```

**Mapping needed:**
| Frontend expects | API provides | Fix |
|---|---|---|
| `successful24h` | `last24h - failedLast24h` (derived) | Add `successfulLast24h` field to API |
| `failed24h` | `failedLast24h` | Rename API field OR frontend reads `.failedLast24h` |
| `uniqueIps` | not present | Add computed field to API |
| `suspiciousIps` | not present (was in backup as count ≥3) | Add count field to API |

**Note:** Stats endpoint IS accessible (HTTP 200) via `GET /api/admin/audit-log/stats`. Only shape mismatch breaks the KPIs.

**Fix suggestion:** Align API `GetStats` response to match frontend field names, OR update frontend to read the correct field names.

---

### F-4 [MEDIUM] CTP-9: Composite indexes on `login_attempts` missing from prod DB

**Axis:** code-db-sync  
**CTP ref:** CTP-9 (migrations not run)

**Symptom:** With 419 rows and growing, the `WHERE email CONTAINS ? OR ipAddress CONTAINS ?` filter query in `GetAll` does a full sequential scan.

**DbContext declares (backup, confirmed same in current source):**
```csharp
e.HasIndex(a => new { a.Email, a.CreatedAt });
e.HasIndex(a => new { a.IpAddress, a.CreatedAt });
```

**Prod DB `pg_indexes` for `login_attempts`:**
```
login_attempts_pkey  (only index — primary key on "Id")
```

Both composite indexes are absent. EF migrations were never run (`__EFMigrationsHistory` is empty — CTP-9 uniform finding confirmed on this table).

**Impact:** Currently low (419 rows, sub-ms seq scan). Becomes relevant at 10K+ rows with brute-force attack traffic.

---

### F-5 [MEDIUM] Unbounded `login_attempts` table growth — no retention policy

**Axis:** security / functional

**Symptom:** `login_attempts` has 419 rows spanning 2026-03-22 to 2026-04-22 (31 days). No purge job, no TTL, no `DELETE` endpoint. At sustained brute-force rates, this table grows unboundedly.

**DB evidence:**
```sql
SELECT COUNT(*) as total_rows,
       MIN("CreatedAt") as oldest,
       MAX("CreatedAt") as newest
FROM login_attempts;
-- total_rows: 419 | oldest: 2026-03-22 04:28 | newest: 2026-04-22 01:31
```

**Admin panel writes:** no "Clear old records" button. No retention config in `app_configs`.

**Fix suggestion:** Add a background job (Hangfire/IHostedService) to delete `login_attempts WHERE CreatedAt < now() - interval '90 days'`. Or add a "Clear records older than N days" button in the Audit Log UI.

---

### F-6 [LOW] PII in `login_attempts` with no right-to-erasure mechanism

**Axis:** security

**Symptom:** Every row in `login_attempts` stores `Email` (PII) and `IpAddress` (PII under GDPR) indefinitely. No endpoint in the admin panel or user-facing API allows deleting a specific user's login attempt history.

**Evidence:** `login_attempts` columns: `Id, Email, IpAddress, Success, CreatedAt`. No foreign key to `users` table — cannot cascade delete when user is deleted.

**Fix suggestion:** When `AdminUserController.DeleteUser` is called, also `DELETE FROM login_attempts WHERE "Email" = user.Email`. Or add FK with ON DELETE CASCADE if email uniqueness can be guaranteed.

---

### F-7 [INFO] Filter query param mismatch — success filter silently ignored

**Axis:** functional

**Symptom:** Clicking "Success" or "Failed" filter button applies `&filter=true` or `&filter=false` to the request URL. The backend `GetAll` parameter is named `success`, not `filter`. The filter is silently ignored; all rows are returned regardless.

**Frontend JS:** `s += "&filter=".concat(t)` (where `t` is `true`/`false`)  
**Backend param:** `[FromQuery] bool? success = null`

ASP.NET Core model binding does not alias `filter` to `success`, so the parameter stays `null` and the filter clause is skipped.

**Fix suggestion:** Change frontend to `&success=` or add `[FromQuery(Name="filter")] bool? success` to the controller.

---

## Cross-Tab Pattern (CTP) Updates

### CTP-2: Missing audit log coverage — CANONICAL OCCURRENCE
- **Occurrence count:** confirmed across all 8 prior tabs (0 admin mutations logged anywhere)
- **Evidence:** No `audit_log` table. All Admin controllers write to their respective data tables only.
- **Severity uplift:** was documented as medium in prior tabs; this tab makes it **critical** — the dedicated "Audit Log" page confirms there is no audit trail infrastructure at all.

### CTP-6: Controller post-rollback divergence
- **This tab:** `AdminAuditController.cs` (backup, route `api/admin/audit`) → `AdminAuditLogController.cs` (current, route `api/admin/audit-log`) — filename, class name, route, and response key all changed; frontend not updated.

### CTP-9: Missing DB indexes (uniform)
- **This tab:** `login_attempts` — both composite indexes absent. Only PK exists. Confirms pattern.

### CTP-10: Pagination shape mismatch
- **This tab:** `items` (API) vs `attempts` (frontend). Confirmed — functionally breaks the table.

### CTP-11: Stats shape mismatch
- **This tab:** 4 KPI fields wrong — all show 0. Confirmed.

---

## Axis Summary

### 1. Functional
- List view: BROKEN (route 404 + key mismatch → always empty)
- Create/Update/Delete: N/A (read-only tab)
- Empty state: shows "No login attempts" even though 419 rows exist
- Filter (success/failed): BROKEN (param name mismatch — silently ignored)
- Search (email/IP): not testable (route 404 in frontend)
- Pagination: BROKEN (route 404; pagination shape also mismatched)

### 2. Code + DB sync
- Tab mislabeled: reads `login_attempts`, not a dedicated `audit_log` — no admin actions logged anywhere
- Route drift between backup/current source and frontend (see F-2)
- Key name drift (`attempts` vs `items`) between backup API contract and current source (see F-2)
- Stats field drift (`successful24h` etc vs `successful` etc) — see F-3

### 3. Security
- `[Authorize(Roles = "admin")]` present on `AdminAuditLogController` — auth guard OK
- No mutation endpoints → no CSRF surface on this tab
- No `dangerouslySetInnerHTML` in Audit Log component
- EF parameterized queries only (`.Contains()` → LIKE) — no raw SQL
- PII concern: emails + IPs stored indefinitely (F-6)
- Admin actions logged: NO — see F-1

### 4. UX
- Loading indicator: not observed (likely absent given 404 returns instantly)
- Error state: silent (catch block returns empty array, no toast)
- Empty state: renders "No login attempts" (misleading — tab is broken, not empty)
- Bug 3 (Refresh): button present, works correctly (re-calls `getLoginAttempts` + `getLoginAttemptStats` in Promise.all without page reload — does NOT trigger Bug 3 hard-reload)
- All 4 KPI cards show 0 (F-3)

### 5. Mobile responsiveness
- 320px: table overflows horizontally (4 columns — Email, IP Address, Status, Time). No column hiding or stacking. Usable with horizontal scroll.
- 375px: same behavior.
- 414px: same.
- 768px: acceptable — all columns visible.
- 1024px: fine.
- Tap targets: filter buttons ("All", "Success", "Failed") are `px-3 py-1.5 text-xs` — approximately 28px tall, below 44px minimum.

### 6. Deployment drift
- Frontend was built against `AdminAuditController.cs` (backup, route `api/admin/audit/login-attempts`).
- Current live DLL has `AdminAuditLogController` at `api/admin/audit-log` — different route deployed.
- Frontend JS not rebuilt after controller rename — route drift causes F-2 breakage.
- Backup controller had richer stats: `{uniqueIps7d, suspiciousIps count}` — current API dropped these.

---

## Findings Matrix

| ID | Severity | Axis | CTP | Title |
|---|---|---|---|---|
| F-1 | CRITICAL | code-db-sync | CTP-2 | No dedicated audit log table — tab is mislabeled; zero admin mutations logged |
| F-2 | CRITICAL | functional | CTP-10, CTP-6 | Route + key mismatch — table always empty (404 → empty array) |
| F-3 | HIGH | functional | CTP-11 | Stats KPI shape mismatch — all 4 cards show 0 |
| F-4 | MEDIUM | code-db-sync | CTP-9 | Composite indexes on login_attempts missing from prod |
| F-5 | MEDIUM | security | — | Unbounded table growth — no retention/purge policy |
| F-6 | LOW | security | — | PII stored indefinitely — no right-to-erasure for login_attempts |
| F-7 | INFO | functional | — | Filter param mismatch (`filter` vs `success`) — silently ignored |

**Counts:** 2 critical, 1 high, 2 medium, 1 low, 1 info = **7 total findings**
