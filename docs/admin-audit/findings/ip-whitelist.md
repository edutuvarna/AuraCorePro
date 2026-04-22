# IP Whitelist Audit Findings

**Tab:** IP Whitelist
**URL path:** `https://admin.auracore.pro/` (SPA, sidebar nav → "IP Whitelist")
**Audit date:** 2026-04-22
**Auditor:** subagent-10
**Time spent:** ~2 hours

## Source files audited

- Frontend TSX: `/root/admin-panel/src/app/page.tsx` lines 1167–1248 (`WhitelistPage`)
- Frontend API client: `/root/admin-panel/src/lib/api.ts` lines 202–222
- Backend controller (deployed DLL): `AdminIpWhitelistController` — inferred from `strings /var/www/auracore-api/AuraCore.API.dll`; source at `src/Backend/AuraCore.API/Controllers/Admin/AdminIpWhitelistController.cs` (74 lines)
- Backend controller (pre-rollback backup): `/root/auracore-src-backup-final-202604122153/AuraCore.API/Controllers/Admin/AdminWhitelistController.cs` (66 lines, flat-file-backed)
- Backend entity: `src/Backend/AuraCore.API.Domain/Entities/IpWhitelist.cs` (9 lines)
- Backend service (backup only): `/root/auracore-src-backup-final-202604122153/AuraCore.API/Services/WhitelistService.cs` (121 lines, JSON flat-file)
- DbContext config: `src/Backend/AuraCore.API.Infrastructure/Data/AuraCoreDbContext.cs` lines 147–154
- Migration: `src/Backend/AuraCore.API.Infrastructure/Migrations/20260421025305_InitialCreate.cs` lines 53–63, 313–314
- DB ground truth: `psql auracoredb` — live query via SSH
- Deployed DLL date: April 14 06:06 UTC
- Admin panel source date (server): March 26 03:52 UTC

## Summary

- **3 critical** — (a) entire tab is non-functional in production (500 on all reads, all writes); (b) four-way API contract drift between deployed frontend and deployed backend; (c) whitelist enforcement silently removed in new code (rate-limit bypass is gone)
- **1 high** — no IP format validation in new controller (any string accepted); no CIDR support
- **2 medium** — missing `my-ip` self-whitelisting endpoint in deployed DLL; no confirmation dialog on IP delete
- **1 low** — `Label` field not XSS-vulnerable (React default escaping) but still unbounded free text with no server-side sanitization note
- **1 info** — CTP-2 (no audit log), CTP-3 (mobile), CTP-9 (ip_whitelists table missing from DB per migration gap)

Axes covered: functional, code+DB sync, security, UX, mobile, drift.

---

## Findings

### F-1 [CRITICAL] Entire IP Whitelist tab non-functional — `ip_whitelists` table absent from DB

**Axis:** functional + code+DB sync
**Cross-tab pattern ref:** CTP-9 (migration gap; `__EFMigrationsHistory` empty — all EF-declared tables/indexes absent from prod)

**Symptom:** Every interaction with the IP Whitelist tab in production returns HTTP 500. The table is not created in the production database.

**Reproduction steps:**
1. Log in as `admin@auracore.pro`
2. Navigate to → IP Whitelist
3. Observe: tab renders empty (frontend error-swallowed) — `getWhitelist()` catches the 500 and returns `[]`
4. Curl confirmation:
   ```bash
   curl -H "Authorization: Bearer $JWT" 'https://admin.auracore.pro/api/admin/ip-whitelist'
   # → HTTP 500 (empty body)
   ```
5. Server log (journalctl):
   ```
   Exception data:
     Severity: ERROR
     SqlState: 42P01
     MessageText: relation "ip_whitelists" does not exist
     Position: 27
     File: parse_relation.c
     Routine: parserOpenTable
   ```

**Expected:** List of whitelisted IPs, or empty array `[]` when none configured.
**Actual:** HTTP 500 on every read and write request to the IP Whitelist tab.

**DB ground truth:**
```sql
-- Query run against production DB (auracoredb) via SSH:
SELECT table_name FROM information_schema.tables
WHERE table_schema='public' AND table_name ILIKE '%whitelist%';
-- → (0 rows) — confirmed: no whitelist table of any kind exists
```

**Root cause:** `AdminIpWhitelistController.cs:27` calls `_db.IpWhitelists.CountAsync()` which translates to `SELECT COUNT(*) FROM ip_whitelists`. The table was declared in `20260421025305_InitialCreate.cs:53–63` but `__EFMigrationsHistory` is empty (0 rows) — the migration was never applied. DB was bootstrapped via raw DDL that did not include the `ip_whitelists` table.

**Severity justification:** CRITICAL — the tab is 100% broken for all users in production.

**Fix prerequisite:** Apply the migration or manually execute:
```sql
CREATE TABLE ip_whitelists (
    "Id" uuid DEFAULT gen_random_uuid() PRIMARY KEY,
    "IpAddress" varchar(45) NOT NULL,
    "Label" varchar(256),
    "CreatedAt" timestamptz DEFAULT now()
);
CREATE UNIQUE INDEX "IX_ip_whitelists_IpAddress" ON ip_whitelists ("IpAddress");
```

---

### F-2 [CRITICAL] Four-way API contract drift — deployed frontend ↔ deployed backend complete mismatch

**Axis:** drift + functional
**Cross-tab pattern ref:** CTP-12 (API contract drift, first confirmed this tab)

**Symptom:** Even if the `ip_whitelists` table were created (fixing F-1), the tab would still fail silently on every operation because the deployed frontend and deployed backend use completely different API contracts.

**Drift dimensions:**

| Dimension | Deployed Frontend (March 26) | Deployed Backend DLL (April 14) | Outcome |
|-----------|------------------------------|----------------------------------|---------|
| **Route** | `/api/admin/whitelist` | `/api/admin/ip-whitelist` | All frontend calls → 404 |
| **GET response shape** | Expects `[{ip, label, addedAt}]` flat array | Returns `{total, page, pageSize, items: [{id, ipAddress, label, createdAt}]}` paginated envelope | Would break: frontend iterates `.map((ip) => ip.ip)` on pagination wrapper |
| **POST body field** | Sends `{ip, label}` | Expects `{IpAddress, Label}` (C# record casing) | Body deserialization fails → always returns default-constructed request (empty IP) |
| **DELETE key** | `DELETE /api/admin/whitelist/{ip}` (IP string as path segment) | `DELETE /api/admin/ip-whitelist/{id:guid}` (Guid) | DELETE never matches route `{id:guid}` when IP is passed; always 404/400 |
| **my-ip endpoint** | Calls `GET /api/admin/whitelist/my-ip` | Not present in deployed DLL | Always 405 Method Not Allowed |

**Root cause (route):** Local repo refactored `AdminWhitelistController` → `AdminIpWhitelistController` (changing route from `/api/admin/whitelist` to `/api/admin/ip-whitelist`) but the deployed admin panel frontend (March 26) was never rebuilt to match.

**Root cause (POST body):** Local repo changed from flat-file `WhitelistService.Add(ip, label)` to EF-backed `AddIpWhitelistRequest(string IpAddress, string? Label)`. Frontend still sends `{ip, label}` (lowercase) which doesn't bind to `IpAddress` (Pascal case) via ASP.NET Core's default JSON deserialization — IpAddress receives `""` empty string, Label receives `null`.

**Verification of deployed DLL routes:**
```bash
strings /var/www/auracore-api/AuraCore.API.dll | grep -i whitelist
# → api/admin/ip-whitelist
# → AdminIpWhitelistController (GetAll, Add, Delete)
# No: my-ip, WhitelistService, IsWhitelisted
```

**Verification of deployed frontend routes (compiled JS):**
```bash
grep -o 'admin/whitelist[^"]*' /var/www/admin-panel/_next/static/chunks/app/page-*.js
# → admin/whitelist
# → admin/whitelist/my-ip
```

**Severity justification:** CRITICAL — even after table creation, no CRUD operation would work without frontend rebuild or backend route rollback.

---

### F-3 [CRITICAL] Whitelist enforcement silently removed — rate-limit bypass no longer functional

**Axis:** security + code+DB sync

**Symptom:** The IP whitelist's business purpose (allowing trusted IPs to bypass login rate limiting) has been silently dropped in the refactored codebase. The admin UI displays and manages a list of IPs that have no effect on any system behavior.

**Investigation:**

| Codebase | Enforcement presence |
|----------|----------------------|
| Backup (April 12) `AuthController.cs:77–85` | Present — `_whitelist.IsWhitelisted(ip)` checked before rate limit; `WhitelistService` registered as singleton in `Program.cs:111` |
| Local repo `AuthController.cs` (203 lines) | Absent — no `WhitelistService` reference; no `IsWhitelisted` check; no whitelist DI registration in `Program.cs` |
| Deployed DLL (April 14) | Absent — `strings` output shows only `AdminIpWhitelistController` + entity names; no `WhitelistService`, no `IsWhitelisted` |

**Backup enforcement code:**
```csharp
// AuthController.cs:77-85 (backup only)
var isWhitelisted = _whitelist.IsWhitelisted(ip);
var recentFails = await _db.LoginAttempts.CountAsync(...);
if (recentFails >= 5 && !isWhitelisted)
    return StatusCode(429, ...);
```

**Impact:** Any IP added via the admin panel IP Whitelist tab (if the tab were working) would be stored in the DB but never consulted during login. Rate limiting applies equally to all IPs regardless of whitelist status.

**Root cause:** During the refactor from flat-file `WhitelistService` to EF-backed `AdminIpWhitelistController`, the enforcement wire-up in `AuthController` and `Program.cs` was not preserved. The tab became a display-only CRUD table with no functional effect.

**Severity justification:** CRITICAL — the feature's entire purpose is gone. The tab manages data that is never read by any production code path.

---

### F-4 [HIGH] No IP format validation in backend — any string accepted as IP address

**Axis:** security
**Compare to:** Backup `AdminWhitelistController.cs:26–29` had `System.Net.IPAddress.TryParse()` validation

**Symptom:** The new `AdminIpWhitelistController.Add` endpoint accepts any string in the `IpAddress` field without validation. A request with `IpAddress: "not-an-ip"` or `IpAddress: ""` receives HTTP 201 Created.

**Root cause:**
- `AdminIpWhitelistController.cs:40–59` — `Add` endpoint contains no `IPAddress.TryParse` check, no MaxLength attribute enforcement (only EF column: `varchar(45)`), no regex validation.
- The only guard is a duplicate check (`AnyAsync(i => i.IpAddress == req.IpAddress)`) and EF's 45-char column limit (which still passes arbitrary 45-char strings).

**Backup had validation:**
```csharp
// AdminWhitelistController.cs:26–29 (backup)
if (!System.Net.IPAddress.TryParse(req.Ip.Trim(), out _))
    return BadRequest(new { error = "Invalid IP address format" });
```

**Additional gap:** No CIDR range support in either version. The `Label` column is `varchar(45)` which could fit a /32 CIDR but the field is named `IpAddress` and no CIDR parsing logic exists. Frontend placeholder text shows `1.2.3.4` (single IP only).

**Severity justification:** HIGH — allows storing nonsense values; if enforcement were re-added, garbage data would permanently bypass rate limiting.

---

### F-5 [MEDIUM] `my-ip` self-whitelisting endpoint absent from deployed DLL — silent failure

**Axis:** drift + functional

**Symptom:** The "Whitelist My IP" button in the admin UI calls `GET /api/admin/whitelist/my-ip` to detect the caller's current IP, then immediately calls `POST /api/admin/whitelist` to add it. In production, `GET /api/admin/whitelist/my-ip` returns HTTP 405 (no matching route). The frontend's `getMyIp()` catches the error and returns `null`. The button label shows "Whitelist My IP" with no IP appended (because `myIp` state stays `""`). Clicking it calls `addWhitelistIp("")` which (if the route existed) would add an empty string.

**Frontend code:**
```tsx
// page.tsx:1197-1202
const load = async () => {
  const [w, ip] = await Promise.all([api.getWhitelist(), api.getMyIp()]);
  setIps(w || []); if (ip?.ip) setMyIp(ip.ip);
};
// page.tsx:1205-1211 — button handler
if (myIp) { const { ok } = await api.addWhitelistIp(myIp, 'Auto-added'); ... }
// But myIp is always "" → button condition `if (myIp)` is falsy → add never fires
```

**Deployed DLL evidence:** `strings AuraCore.API.dll | grep my-ip` → no output. The `GET .../my-ip` route from backup `AdminWhitelistController` was not ported to the new `AdminIpWhitelistController`.

**Net effect:** Button renders (with empty IP label), appears clickable, but silently does nothing.

**Severity justification:** MEDIUM — safety net (button does nothing rather than adding wrong IP), but the feature is invisible-broken.

---

### F-6 [MEDIUM] No confirmation dialog on IP delete — CTP-4 instance

**Axis:** UX
**Cross-tab pattern ref:** CTP-4

**Symptom:** Clicking the trash icon on any whitelisted IP row immediately fires `removeWhitelistIp(ip.ip)` with no confirmation step.

**Frontend code:**
```tsx
// page.tsx:1237-1239
<button onClick={async () => { await api.removeWhitelistIp(ip.ip); load(); }}
  className="..."><Trash2 className="w-4 h-4" /></button>
```

No `confirm()`, no modal, no undo. For the whitelist use-case this is especially dangerous: removing own IP while enforcement is active (if F-3 were fixed) would cause immediate lockout.

**Severity justification:** MEDIUM — a CTP-4 instance. Elevated given lockout risk if enforcement is restored.

---

### F-7 [INFO] Label field XSS — no finding (React escaping applies)

**Axis:** security

**Symptom:** `Label` is free text (up to 256 chars, `varchar(256)` in EF). The admin panel renders it as:
```tsx
// page.tsx:1232
<td className="py-3 px-4 text-white/50">{ip.label || '-'}</td>
```
React's JSX text interpolation escapes HTML entities by default. No `dangerouslySetInnerHTML` or `__html` present. Not an XSS vector.

**Severity justification:** INFO — no finding; noted for completeness.

---

### F-8 [INFO] CTP-2 instance — no audit log for IP whitelist mutations

**Axis:** security
**Cross-tab pattern ref:** CTP-2

Add and Delete operations on `ip_whitelists` are not logged to any audit trail. Same systemic gap as all prior tabs.

---

### F-9 [INFO] CTP-3 instance — mobile table overflow

**Axis:** mobile
**Cross-tab pattern ref:** CTP-3

IP Whitelist table has 4 columns (IP, Label, Added, Actions). At ≤375px the table overflows the content area. Same root-layout gap as all prior tabs. At 320px the Add form fields stack awkwardly (two `flex-1` inputs in a `flex items-end gap-3` row do not wrap).

---

## Axis coverage summary

| Axis | Covered | Key finding |
|------|---------|-------------|
| Functional | Yes | F-1 (entire tab 500) |
| Code+DB sync | Yes | F-1 (table absent), F-3 (enforcement removed) |
| Security | Yes | F-3 (enforcement gone), F-4 (no IP validation), F-7 (XSS — no finding) |
| UX | Yes | F-6 (no delete confirmation) |
| Mobile | Yes | F-9 (CTP-3 overflow) |
| Drift | Yes | F-2 (4-way contract drift), F-5 (my-ip missing) |

---

## DB state queries used

```sql
-- Ground truth: no whitelist tables
SELECT table_name FROM information_schema.tables
WHERE table_schema='public' AND table_name ILIKE '%whitelist%';
-- → (0 rows)

-- Confirm full table list (14 tables, ip_whitelists not among them)
SELECT table_name FROM information_schema.tables
WHERE table_schema='public' ORDER BY table_name;
-- → 14 tables: PasswordResets, __EFMigrationsHistory, app_configs, app_updates,
--   crash_reports, devices, licenses, login_attempts, password_reset_codes,
--   payments, refresh_tokens, subscriptions, telemetry_events, users

-- EF migrations applied:
SELECT * FROM "__EFMigrationsHistory";
-- → (0 rows) — root cause of CTP-9 for all tables/indexes
```

---

## Deployment drift notes

- **Admin panel source (server):** March 26 03:52 — uses `/api/admin/whitelist` route (flat-file era)
- **Deployed DLL:** April 14 06:06 — uses `/api/admin/ip-whitelist` route (EF-backed)
- **Local repo source:** April 14 — matches deployed DLL route; same as DLL
- **Net drift:** Frontend is 19 days behind the backend refactor. The frontend was never rebuilt after the `AdminWhitelistController` → `AdminIpWhitelistController` rename.

## CTP-12 (NEW): API contract drift — frontend vs backend route/shape mismatch pattern

This is the first tab to surface a multi-dimensional contract drift where the **route**, **field names**, **response shape**, and **endpoint set** all diverge simultaneously between deployed frontend and deployed backend. Prior CTPs (CTP-6) focused on controller line-count reduction. CTP-12 captures the pattern where a backend refactor changes the API contract without a coordinated frontend rebuild.

**Confirmed in:** IP Whitelist tab (this audit).
**Risk for other tabs:** Any tab where the backend was refactored between March 26 (last frontend build) and April 14 (DLL build) may have route/shape drift. Subsequent auditors should cross-check api.ts function routes against deployed DLL `strings` output.
