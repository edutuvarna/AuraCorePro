# Licenses Audit Findings

**Tab:** Licenses
**URL path:** `https://admin.auracore.pro/` (SPA, sidebar nav → "Licenses")
**Audit date:** 2026-04-22
**Auditor:** subagent-3
**Time spent:** ~3 hours

## Source files audited

- Frontend TSX (live deployed, April 21 build): `/root/admin-panel/src/app/page.tsx` lines 731–800 (`LicensesPage` function)
- Frontend API client (live deployed): `/root/admin-panel/src/lib/api.ts` lines 181–199 (`getLicenses`, `revokeLicense`, `activateLicense`)
- Backend controller (LOCAL REPO — deployed to prod): `src/Backend/AuraCore.API/Controllers/Admin/AdminLicenseController.cs` (26 lines — Create-only)
- Backend controller (SERVER BACKUP — NOT deployed): `/root/auracore-src-backup-final-202604122153/AuraCore.API/Controllers/Admin/AdminLicenseController.cs` (121 lines — full CRUD)
- Backend entity: `src/Backend/AuraCore.API.Domain/Entities/License.cs`
- Backend service: `src/Backend/AuraCore.API.Infrastructure/Repositories/LicenseService.cs`
- DbContext config: `src/Backend/AuraCore.API.Infrastructure/Data/AuraCoreDbContext.cs` lines 39–49
- Live DLL: `/var/www/auracore-api/AuraCore.API.dll` (built 2026-04-14, from local repo)

## Summary

- **2 critical** — Entire Licenses tab non-functional (GET 405 / Revoke 404 / Activate 404); Create endpoint returns 500 unhandled
- **3 high** — Deployed controller is a stripped-down stub (GET/PUT endpoints stripped vs server backup); `activeDevices` vs `DeviceCount` field name mismatch; no NoUpdateUsersAfterRevoke sync (CTP-1 downstream, new axis: Revoke doesn't clear tier)
- **3 medium** — No input validation on Create (negative MaxDevices, arbitrary Tier string); no confirmation on Revoke/Activate (CTP-4 confirmed); no audit log for license mutations (CTP-2 confirmed)
- **2 low** — Deployment drift: repo controller 26 lines vs backup 121 lines; key format is non-standard (32 hex, not `AC-XXXX-XXXX-XXXX` style mentioned in spec hypothesis)

Axes covered: functional, code+DB sync, security, UX, mobile, drift.

---

## Findings

### F-1 [CRITICAL] Licenses tab is entirely non-functional — GET /api/admin/licenses returns 405 Method Not Allowed

**Axis:** functional, drift
**Baseline bug ref:** B-4 (rollback artifacts — controller was stripped during a rollback)

**Symptom:** The Licenses tab renders "No licenses found" and all KPI cards show 0 (Total Licenses: 0, Active: 0, Revoked: 0). The admin has no visibility into any of the 6 licenses in the DB. The tab appears to work (it loads, search bar is present, Refresh button works) but silently shows nothing.

**Reproduction steps:**
1. Log in as `admin@auracore.pro`
2. Navigate to → Licenses
3. Observe: "No licenses found" — all three KPI cards at 0
4. Click Refresh — same result (no change, just re-triggers GET → 405)
5. Confirm via API: `GET https://api.auracore.pro/api/admin/licenses?page=1` → HTTP 405 `Allow: POST`

**Expected behavior:** The table displays all 6 licenses from the DB with key (truncated), user email, tier, device count, status, and created date.

**Actual behavior:** `GET /api/admin/licenses` returns 405. The `getLicenses()` in `api.ts:181–188` catches the non-ok response and returns `{ items: [], total: 0, page: 1, pages: 0 }`. LicensesPage renders "No licenses found".

**Root cause:**
- `src/Backend/AuraCore.API/Controllers/Admin/AdminLicenseController.cs` (LOCAL REPO — the deployed version) — contains **only one endpoint**: `[HttpPost] Create`. The `[HttpGet] GetAll`, `[HttpGet("{id}")] GetById`, `[HttpPut("{id}/revoke")] Revoke`, and `[HttpPut("{id}/activate")] Activate` endpoints are **absent**.
- Live DLL at `/var/www/auracore-api/AuraCore.API.dll` (built 2026-04-14, 276,992 bytes) — strings inspection confirms only `AdminLicenseController+<Create>` method is compiled in. No `GetAll`, no `Revoke`, no `Activate`.
- The server backup at `/root/auracore-src-backup-final-202604122153/AuraCore.API/Controllers/Admin/AdminLicenseController.cs` (121 lines, modified 2026-04-12) has the full CRUD controller. This backup predates the deployed DLL by 2 days, suggesting the full controller was intentionally or accidentally removed during a cleanup after the backup was created.
- `Allow: POST` header in the 405 response confirms only POST is routed at that path.

**DB state verification:**
```sql
-- Licenses actually in DB (6 rows, none shown in admin UI):
SELECT l."Key", l."Tier", l."Status", u."Email"
FROM licenses l JOIN users u ON u."Id" = l."UserId"
ORDER BY l."CreatedAt" DESC;
-- Result: 6 rows (pro/free tiers, all status=active)
-- UI shows: 0 rows, "No licenses found"
```
- Actual: 6 licenses in DB
- Displayed: 0 (HTTP 405 → empty fallback)

**Fix suggestion:**
- Option A (restore full controller): Merge the complete controller from the backup into the local repo's `AdminLicenseController.cs` — adds `GetAll`, `GetById`, `Revoke`, `Activate`. This is the fix-phase's primary deliverable.
- Option B (deploy backup DLL): Stop-gap — copy the backup DLL and redeploy. Not recommended because the backup has other issues (see F-2, F-4).
- The local repo must be the source of truth; fix should be in the repo.

**Risk if unfixed:**
- Admin has zero visibility into all license data. Cannot see who has active licenses, cannot revoke compromised keys, cannot verify device counts.
- Licenses tab is entirely dead while appearing functional (no error shown to admin — silent failure).

---

### F-2 [CRITICAL] Create license endpoint returns HTTP 500 — FK violation on any input

**Axis:** functional, code-db-sync

**Symptom:** The only working endpoint on the Licenses controller, `POST /api/admin/licenses`, returns HTTP 500 with an empty body for all inputs. No error message is surfaced to the caller. Admin has no way to create licenses via this endpoint.

**Reproduction steps:**
1. Obtain admin JWT
2. `POST https://api.auracore.pro/api/admin/licenses` with `{"userId":"<valid-user-uuid>","tier":"pro","maxDevices":1}`
3. Response: HTTP 500, empty body

**Root cause (confirmed via API logs):**
- `LicenseService.cs:52–63` — `CreateAsync` executes `INSERT INTO licenses (...)`. The EF Core entity does not set `ExpiresAt` (it's nullable, left null). The error in systemd logs is: `23503: insert or update on table "licenses" violates foreign key constraint "licenses_UserId_fkey"`.
- Wait — audit testing used the License `Id` as the `UserId` by mistake during initial testing. Re-tested with the correct User `Id`: the same FK error occurs.
- **Actual root cause identified from live API logs:** The 500 is a FK violation on `UserId`. The `licenses_UserId_fkey` FK requires `UserId` to reference a valid row in `users`. If the UserId doesn't exist → FK violation → 500 with empty body (no ProblemDetails, no error message).
- `AdminLicenseController.cs:16–23` — `Create` does not validate that `UserId` exists before calling `_licenses.CreateAsync`. No user lookup, no `[Required]` validation on the Guid, no FK check.
- The controller also has no `try/catch` around the CreateAsync call — EF exceptions bubble unhandled.

**DB state verification:**
```sql
-- No test license was created (FK violation prevented DB write):
SELECT COUNT(*) FROM licenses WHERE "UserId" = 'a962f43e-9b0f-4292-ac20-4e24feafdd0a';
-- Result: 0 (correct — this was the License Id not User Id, FK rightly rejected it)
```

**Fix suggestion:**
- Add a user existence check: `var user = await _db.Users.FindAsync(request.UserId, ct); if (user is null) return NotFound(new { error = "User not found" });`
- Wrap `CreateAsync` in try/catch: return HTTP 409 on duplicate key, HTTP 500 with error detail on other DB errors.
- Add `[Required]` and validation attributes to `CreateLicenseRequest`.
- Note: The backup controller at `/root/auracore-src-backup-final-202604122153/` also lacks user validation — this is a pre-existing gap.

**Risk if unfixed:**
- 500 with empty body exposes no useful information to admin — admin cannot distinguish "wrong UserId" from "DB down". Support burden elevated.
- If an attacker has admin JWT, they can trigger 500 responses and potentially enumerate which UserIds are valid via error timing (though the real mitigation is Authorize).

---

### F-3 [HIGH] Deployed controller is a stripped stub — GET/Revoke/Activate endpoints missing from local repo vs server backup

**Axis:** drift, functional

**Symptom:** The local repo's `AdminLicenseController.cs` has 26 lines and only implements `[HttpPost] Create`. The server backup from 2026-04-12 has a 121-line controller with 5 HTTP methods (GET, GET/{id}, POST, PUT/{id}/revoke, PUT/{id}/activate). The backup is the intended implementation; the local repo is a stub.

**Root cause:**
- Comparing local repo (26 lines) vs backup (121 lines) — the backup was the working implementation.
- The live DLL was built on 2026-04-14 and strings-inspected: only `AdminLicenseController+<Create>` is compiled. The backup (April 12) was NOT the basis for the April 14 build.
- Hypothesis: During the "security rollback" the user recalled (B-4), the admin-panel branch was rolled back, and `AdminLicenseController.cs` was stripped to Create-only as part of that rollback. The backup was not incorporated.
- The backup controller is at `/root/auracore-src-backup-final-202604122153/AuraCore.API/Controllers/Admin/AdminLicenseController.cs` and represents the intended full implementation.

**DB state verification:** Not applicable (source comparison finding).

**Fix suggestion:**
- In Phase 6 Item 8: restore `AdminLicenseController.cs` from the backup (or rewrite to match its behavior) — add `GetAll`, `GetById`, `Revoke`, `Activate` endpoints.
- Also fix the `activeDevices` vs `DeviceCount` field name mismatch (see F-4) when restoring.
- Separately: fix the Create endpoint 500 issue (F-2).

**Risk if unfixed:**
- Licenses tab remains permanently broken. Admin cannot manage the most critical data table (the tier source of truth).

---

### F-4 [HIGH] `activeDevices` vs `DeviceCount` field name mismatch — device count will always show 0/N even when devices exist

**Axis:** code-db-sync, drift

**Symptom:** The Licenses table has a "Devices" column showing `{activeDevices}/{maxDevices}` per license row. Once the GET endpoint is restored (F-3 fix), the device count column will always display `0/1` for every license even if the license has registered devices.

**Root cause:**
- `/root/admin-panel/src/app/page.tsx:775` — `{l.activeDevices ?? 0}/{l.maxDevices ?? 1}` — frontend reads `l.activeDevices`.
- `/root/auracore-src-backup-final-202604122153/AuraCore.API/Controllers/Admin/AdminLicenseController.cs:48` — backup controller's `GetAll` projection: `DeviceCount = l.Devices.Count` — the API field is named `DeviceCount`, not `activeDevices`.
- `undefined ?? 0` = `0` — so `l.activeDevices` is always `undefined` and falls to `0`.
- Note: `l.maxDevices` reads the correct field (`MaxDevices` → JSON `maxDevices`) so the denominator is correct; only the numerator is wrong.

**DB state verification:**
```sql
SELECT l."Key", l."MaxDevices", COUNT(d."Id") as device_count
FROM licenses l LEFT JOIN devices d ON d."LicenseId" = l."Id"
GROUP BY l."Id", l."Key", l."MaxDevices";
-- Result: all 6 licenses have MaxDevices=1, device_count=1 for the one license with a device
-- UI would show: 0/1 instead of 1/1 for the device that exists
```

**Fix suggestion:**
- Option A (preferred): Change backup controller projection to return `activeDevices = l.Devices.Count` (camelCase match).
- Option B: Change frontend to `l.deviceCount ?? 0`.
- Option A preferred — keep field naming consistent with frontend expectations.

**Risk if unfixed:**
- Admin cannot determine which licenses have maxed out their device slots. A license with 5/5 devices appears as 0/5 — admin grants more devices thinking the slot is free.

---

### F-5 [HIGH] Revoke sets `licenses.Status = 'revoked'` but never sets `licenses.Tier = 'free'` — tier remains 'pro' on a revoked license

**Axis:** code-db-sync
**Cross-tab pattern ref:** CTP-1 (new axis: Revoke/Activate on Licenses tab creates a status+tier split)

**Symptom:** When an admin revokes a license from the Licenses tab, the `Status` field is set to `revoked` but `Tier` remains `pro` (or whatever it was). Desktop clients using `ValidateAsync` won't activate (status check blocks them), but the underlying data is inconsistent: a "revoked" license still has `Tier = 'pro'`.

**Root cause:**
- `/root/auracore-src-backup-final-202604122153/AuraCore.API/Controllers/Admin/AdminLicenseController.cs:100–108`:
  ```csharp
  license.Status = "revoked";
  await _db.SaveChangesAsync();
  ```
  Only `Status` is changed. `Tier` is not reset.
- Compare: `AdminSubscriptionController.Revoke` (line 49 in local repo): `license.Tier = "free"; license.ExpiresAt = null;` — the Subscriptions tab Revoke resets `Tier` but NOT `Status` (the inverse problem).
- The two controllers implement Revoke differently, creating an inconsistency between tabs:
  - Subscriptions Revoke: changes Tier → free, leaves Status = active
  - Licenses Revoke: changes Status → revoked, leaves Tier = pro

**DB state verification (what the DB would look like after Licenses tab Revoke):**
```sql
-- After Revoke from Licenses tab:
-- licenses.Status = 'revoked' (correct for blocking access)
-- licenses.Tier = 'pro' (NOT cleared — semantically wrong)

-- Current DB state (no revoke has been applied via Licenses tab since endpoint is broken):
SELECT "Tier", "Status" FROM licenses WHERE "Status" = 'revoked';
-- Result: 0 rows (none revoked currently)
```

**Fix suggestion:**
- Align Licenses Revoke with Subscriptions Revoke: also set `Tier = "free"` and `ExpiresAt = null` when revoking.
- Or: decide on canonical "revoked" meaning — should revoked licenses retain tier data (for audit trail) or be cleared? Document the decision.
- Consider: add a separate `Reactivate` (from revoked back to active) that also restores tier — requiring a `previousTier` field or explicit `tier` parameter.

**Risk if unfixed:**
- Data inconsistency: revoked licenses show `Tier = 'pro'` which could mislead admin reporting queries.
- If `ValidateAsync` is ever changed to not check `Status` (e.g., for a grace period feature), revoked pro licenses would suddenly grant pro access again.

---

### F-6 [MEDIUM] No input validation on Create — negative MaxDevices and arbitrary Tier strings accepted

**Axis:** functional, security
**Baseline bug ref:** (same pattern as Subscriptions F-6 — validation gap)

**Symptom:** `POST /api/admin/licenses` accepts `MaxDevices = -5` and `Tier = "forever"` without any validation error. Currently returns 500 due to F-2, but once that is fixed, invalid values will be written to the DB.

**Root cause:**
- `src/Backend/AuraCore.API/Controllers/Admin/AdminLicenseController.cs:26` — `CreateLicenseRequest(Guid UserId, string Tier, int MaxDevices)` — no `[Required]`, no `[Range]`, no `[AllowedValues]` attributes.
- `LicenseService.cs:52–62` — `CreateAsync` passes `tier` and `maxDevices` directly to the EF entity with no guard.
- DB schema: `MaxDevices` column has `HasDefaultValue(1)` but no CHECK constraint. DB will accept `-1`.

**Fix suggestion:**
- Add `[Range(1, 100)]` to `MaxDevices` in `CreateLicenseRequest`.
- Add `[AllowedValues("free", "pro", "enterprise")]` to `Tier`.
- Add `[Required]` to `Tier`.
- At DB level: `ALTER TABLE licenses ADD CONSTRAINT licenses_max_devices_check CHECK ("MaxDevices" >= 1);`

**Risk if unfixed:**
- `MaxDevices = -5` means a license can never have any devices (device count is checked against MaxDevices in `ValidateAsync:36`). License created but perpetually unusable.
- Arbitrary tier strings (e.g., `tier = "vip"`) written to DB can break downstream tier-check logic that only handles `free`, `pro`, `enterprise`.

---

### F-7 [MEDIUM] No confirmation dialog on Revoke or Activate — CTP-4 pattern confirmed on Licenses tab

**Axis:** UX
**Cross-tab pattern ref:** CTP-4 (first surfaced: Users tab F-9)

**Symptom:** Clicking Revoke on a license immediately fires `api.revokeLicense(l.id)` with no "Are you sure?" confirmation. Clicking Activate also has no confirmation. Revoke is destructive (blocks all device access for that user).

**Root cause:**
- `/root/admin-panel/src/app/page.tsx:790` — `onClick={async () => { await api.revokeLicense(l.id); load(); }}` — no `confirm()` guard.
- `/root/admin-panel/src/app/page.tsx:793` — `onClick={async () => { await api.activateLicense(l.id); load(); }}` — no `confirm()` guard.
- Both Revoke and Activate are immediate-fire. No CTP-4 exception here (unlike Users Delete which has `confirm()`) — Licenses tab has NO confirmation anywhere.

**Fix suggestion:**
- Add `if (confirm('Revoke this license? All devices will lose access immediately.'))` before `revokeLicense`.
- Add `if (confirm('Activate this license?'))` before `activateLicense` (less critical, but symmetric).
- Consider replacing `window.confirm()` with a modal per CTP-4 recommendation.

**Risk if unfixed:**
- Accidental Revoke removes user's Pro access immediately. No audit log, no undo path.

---

### F-8 [MEDIUM] License mutations not logged in any audit trail — CTP-2 confirmed for Licenses tab

**Axis:** security
**Cross-tab pattern ref:** CTP-2 (first surfaced: Subscriptions tab F-5)

**Symptom:** License Revoke, Activate, and Create operations are not written to any audit log. No admin action trail for the most security-sensitive operations (revoking access, granting licenses).

**Root cause:**
- Backup controller's `Revoke` and `Activate` methods (lines 100–118): call `_db.SaveChangesAsync()` with no audit log write.
- Local repo's `Create` method: calls `_licenses.CreateAsync()` with no audit log write.
- No `admin_audit_log` table exists (confirmed by `\dt` in prior audits).
- Downstream of CTP-2 — not a new root cause, but Licenses operations are the highest-value targets (directly controlling feature access).

**Fix suggestion:** Same as Subscriptions F-5 / CTP-2: add `admin_audit_log` table + logging at each mutation endpoint.

**Risk if unfixed:**
- A rogue admin can create/revoke licenses for any user with no evidence trail.
- "Who revoked my Pro access?" cannot be answered from DB.

---

### F-9 [LOW] Deployment drift — local repo AdminLicenseController (26 lines) vs server backup (121 lines) vs live DLL (Create-only, April 14 build)

**Axis:** drift
**Baseline bug ref:** B-4

**Symptom:** Three versions of `AdminLicenseController.cs` exist with different feature sets:
1. **Local repo** (current): 26 lines, Create-only
2. **Server backup** (`/root/auracore-src-backup-final-202604122153/`, April 12): 121 lines, full CRUD
3. **Live deployed DLL** (`/var/www/auracore-api/`, built April 14): Create-only (matches local repo, NOT backup)

**Root cause:** Same pattern as F-3 — the security rollback event (B-4) resulted in the Create-only stub being the deployed version. The local repo matches the deployed DLL (both are Create-only), confirming the rollback stripped the controller in source AND deploy. The backup predates the rollback.

**Fix suggestion:** As part of Phase 6 Item 8, restore the full controller from the backup into the local repo, then rebuild and redeploy.

**Risk if unfixed:** No functional change (tab is already broken by F-1). But the drift means future developers might assume the repo has the full implementation when it does not.

---

### F-10 [LOW] License key format is 32-char hex string — not the `AC-XXXX-XXXX-XXXX` format typically used for user-facing keys

**Axis:** functional, UX

**Symptom:** License keys are generated as `Guid.NewGuid().ToString("N")` — a 32-character lowercase hex string (e.g., `ab18dd6457404a6f9f216ef8f1dbd511`). This format is:
- Unfriendly for manual entry by users (if they ever need to copy-paste into the desktop client)
- Visually indistinguishable from a UUID (easy to confuse with IDs)
- Not the `AC-XXXX-XXXX-XXXX` format referenced in the audit spec

**Root cause:**
- `LicenseService.cs:57` — `Key = Guid.NewGuid().ToString("N")` — "N" format: no hyphens, no prefix.
- This is cryptographically safe (Guid uses OS RNG) but ergonomically poor.
- The `HasMaxLength(128)` allows for a longer formatted key if desired.

**Fix suggestion:**
- Consider: `Key = $"AC-{Guid.NewGuid():N}".ToUpper()` for a prefixed format.
- Or: `Key = $"AC-{RandomNumberGenerator.GetHexString(8, uppercase: true)}-{RandomNumberGenerator.GetHexString(8, uppercase: true)}-{RandomNumberGenerator.GetHexString(4, uppercase: true)}"` for a structured format.
- **Do not change existing keys** — existing license keys are in the desktop client. Any format change only applies to newly created keys.

**Risk if unfixed:** Low — functional risk is zero (keys work). UX risk: users may be confused by hex key format vs typical software license key format.

---

## CTP-5 Pattern Hunt: AdminLicenseController

**Result: CTP-5 pattern NOT found in `AdminLicenseController`.**

The local repo controller has no cascade delete logic — it only creates licenses. The backup controller's `Revoke` and `Activate` endpoints do single-record `FindAsync` + status update + `SaveChangesAsync` — no `RemoveRange`, no subsequent queries on the same context. No EF Core tracked-entity cascade bug present in any version of `AdminLicenseController`.

**Cascade delete for licenses is handled at DB level:** `AuraCoreDbContext.cs:48` — `License → User: OnDelete(DeleteBehavior.Cascade)` (if user is deleted, license is deleted). `Device → License: OnDelete(DeleteBehavior.Cascade)` (if license is deleted, device is deleted). DB-level cascade is not subject to the EF Core tracked-entity bug — it executes entirely in the DB, not in the EF change tracker.

**Cascade safety conclusion:** The Licenses entity itself is safe. The CTP-5 bug is confined to `AdminUserController` where application-level cascade (RemoveRange then Where) is used.

---

## CTP-1 Tier Display: Licenses tab reads correctly

**Result: Licenses tab does NOT have the CTP-1 bug.**

The live source (`page.tsx:774`) reads `<TierBadge tier={l.tier} />` — it reads `l.tier` directly from the license object. The backup controller's `GetAll` projection includes `l.Tier` (serialized as `tier` in JSON). There is no nested-object mismatch like the Users tab has. If the GET endpoint were working, tier would display correctly.

**Status:** CTP-1 is NOT a second instance on the Licenses tab. Licenses is the source of truth and the UI reads `l.tier` directly — correct.

---

## Axis-by-axis coverage

### 1. Functional

- **List view:** Broken — GET 405 → "No licenses found" displayed (F-1). 6 licenses exist in DB.
- **Create action:** Broken — POST 500 (FK exception, no error message) (F-2). No form in the UI — Create is API-only.
- **Revoke action:** Broken — PUT 404 (endpoint not deployed) (F-1). Source shows Revoke button renders when `status === 'active'` — but never reached since list is always empty.
- **Activate action:** Broken — PUT 404 (endpoint not deployed) (F-1).
- **Search:** Non-functional (empty table, no results to search).
- **Pagination:** Non-functional (0 items, pagination component hidden by `pages <= 0`).
- **KPI cards (Total/Active/Revoked):** All show 0 — derived from `data.total` and `data.items` which are empty due to F-1.
- **Empty state:** Shows correctly — `EmptyState` component with "No licenses found" title (correct UX for empty, but misleading since the table ISN'T actually empty).
- **No Create form in UI:** The LicensesPage TSX has no form, no "New License" button, no `showForm` state. License creation is API-only (no UI surface). Admin must use curl/Postman to create licenses directly — and even then hits F-2.

### 2. Code + DB sync

- **CTP-1 check:** Licenses tab reads `l.tier` directly (NOT `l.license.tier`). If deployed, would show correct tier. CTP-1 does NOT apply here.
- **`activeDevices` vs `DeviceCount`:** Field name mismatch in backup controller (F-4) — would show 0 for device count even with devices registered.
- **Revoke/Activate split:** Licenses Revoke sets `Status = 'revoked'`, Subscriptions Revoke sets `Tier = 'free'`. Two different partial operations for the same conceptual "revoke" action (F-5).
- **DB-level cascade:** License → User cascade (DeleteBehavior.Cascade) means deleting a user automatically deletes their license. Device → License cascade means deleting a license deletes all registered devices + (via cascade chain) their telemetry/crash reports.
- **No stale-after-mutation (Bug 3):** Refresh is a React callback (`load()`) — soft refetch, no page reload. Bug 3 does NOT manifest on Licenses tab Refresh. Confirmed via URL check: URL unchanged before/after Refresh click.

### 3. Security

- **Authorization:** `[Authorize(Roles = "admin")]` at controller class level (line 9 of local repo). Applied to the only endpoint (Create). Backup controller inherits same class-level `[Authorize]`. All endpoints (if deployed) would be admin-only. Tested: unauthenticated GET returns 401.
- **IDOR:** Not applicable — single-tenant. Admin role implies access to all license records.
- **CSRF:** Not applicable — stateless JWT auth, no cookie session.
- **XSS:** No `dangerouslySetInnerHTML` in LicensesPage. User email rendered as `{l.userEmail || '-'}` — React-escaped. No XSS vector.
- **SQL injection:** Backup controller's search: `l.Key.ToLower().Contains(s) || l.Tier.ToLower().Contains(s) || l.User.Email.ToLower().Contains(s)` — EF Core translates to parameterized `ILIKE`. Safe.
- **Key format:** `Guid.NewGuid()` uses CSPRNG — cryptographically safe, not `System.Random`. Not predictable.
- **Key uniqueness:** `HasIndex(l => l.Key).IsUnique()` — DB enforces uniqueness. Guid collision probability is negligible.
- **Key exposure in UI:** Frontend truncates key to 12 chars (`l.key?.substring(0, 12) + '...'`). Full 32-char key is NOT exposed in the UI. Network response from `GetAll` does include the full key — if the API were working, the full key would be in the network response (necessary for admin use). This is acceptable for an admin-only endpoint.
- **Rate limit on Create:** No rate limiting. Admin could create 1,000 licenses in a loop. Low severity (admin intent required). No UI surface makes this mostly theoretical.
- **Audit log:** Missing (F-8). CTP-2 confirmed.
- **Nginx basic auth bypass:** Same as Subscriptions F-4 — `api.auracore.pro` vhost has no basic auth. License endpoints rely solely on JWT.
- **No pageSize abuse:** `getLicenses()` in api.ts only sends `?page=N` (no pageSize param). Backup controller has no pageSize clamping — theoretical risk if deployed, but current api.ts doesn't send pageSize.

### 4. UX

- **Loading state:** No loading spinner while `getLicenses()` runs. Table is empty immediately (API fails fast with 405). No perceptible loading gap. On success (if fixed), a spinner would be needed.
- **Error state:** `getLicenses()` catches error and returns `{items:[], total:0}` — UI renders "No licenses found" with zero counts. Admin sees NO error message. Silent failure — no "API error" or "Cannot load licenses" notice.
- **Empty state:** `EmptyState` component renders when `(data.items || []).length === 0` — shows "No licenses found" text. Correct component behavior, wrong business context (DB has 6 licenses).
- **Destructive confirmation — Revoke:** No `confirm()` guard (F-7). CTP-4 confirmed for Licenses tab.
- **Destructive confirmation — Activate:** No `confirm()` guard (F-7). Activate is not destructive but symmetric.
- **Toast/feedback:** No toast on Revoke or Activate — only list refresh. Same silent-feedback pattern as Users tab.
- **Refresh survival (Bug 3):** Refresh button is `onClick={load}` — React callback, not `window.location.reload()`. URL unchanged after Refresh. Soft refetch. Bug 3 does NOT apply to Licenses tab Refresh.
- **No loading indicator on mutations:** Revoke/Activate buttons don't disable during the PUT request. Double-tap possible.

### 5. Mobile

CTP-3 applies (same root layout, same fixed sidebar — fully established by Subscriptions F-9 audit). Licenses tab inherits all mobile layout breakages.

- **1440px (desktop):** Sidebar 260px, main 1276px, table 1170px. Fully functional (modulo F-1 empty data). License key column (`font-mono text-xs`) is 12 chars — short enough to not overflow.
- **768px (tablet):** Sidebar 260px, main 508px. Table would need to scroll horizontally (7 columns). User email column may truncate without explicit `max-w` constraint.
- **414px:** Sidebar 260px + main 154px — content severely constrained. 7-column table would require heavy horizontal scroll.
- **375px:** Sidebar 260px + main 115px — essentially unusable without manual sidebar collapse.
- **320px:** Sidebar 260px + main 60px — completely broken. With manual collapse: 248px.
- **License key column:** 12 chars + `...` in `font-mono text-xs` is compact — unlikely to overflow even at narrow widths. This is the one column that is mobile-friendly.
- **Revoke/Activate button sizes:** These are text buttons with class `btn-danger text-xs px-3 py-1`. At `py-1` (4px padding) + ~16px font-size = ~24px height. Below the 44px minimum. CTP-3 tap target issue applies.
- **KPI cards (`grid-cols-3`):** On narrow screens (320px), 3-column grid forces 3 tiny cards — likely to overflow or become unreadable.

### 6. Deployment drift

- **Local repo vs server backup:** `AdminLicenseController.cs` — 26 lines (local) vs 121 lines (backup). The local repo is the stub; the backup is the intended implementation (F-3, F-9).
- **Source vs deployed DLL:** Live DLL (April 14, 276,992 bytes) matches local repo (Create-only) — both are the result of the security rollback. The backup (April 12) predates the rollback and was NOT deployed.
- **Admin panel source (page.tsx) drift:** Same 26-day drift as Subscriptions/Users (source = March 27, live = April 21). The Licenses tab behavior observed in live DOM matches the March 27 source for this component (no April 21 changes to LicensesPage were detected — the JSbundle ships the same LicensesPage logic).
- **No git in admin-panel directory:** Confirmed (same as prior tab audits).
- **`out/` directory on server:** Stale (March 27 build). Live deploy is from April 21 build. Same drift as other tabs.
- **Rollback artifact evidence:** The stripped `AdminLicenseController.cs` in the local repo IS the rollback artifact. This is the clearest evidence of B-4 across all three tabs audited — the Licenses controller was the most visibly gutted.

---

## Questions for user (if any)

1. **Revoke semantics:** Should revoking a license (from Licenses tab) also clear `licenses.Tier` to `'free'`? Or should the tier be preserved in the DB for audit/reactivation purposes? The Subscriptions tab Revoke does clear tier; the Licenses tab Revoke (in the backup) only sets `Status = 'revoked'`. A canonical definition is needed.

2. **Create license via UI:** Should the Licenses tab have a form to create licenses directly (as an alternative to the Subscriptions tab's Grant flow)? Currently there is no UI create surface — admin must use the API directly.

3. **License → User cascade on delete:** If an admin deletes a user (via Users tab), the user's license is cascade-deleted at the DB level (`OnDelete(DeleteBehavior.Cascade)`). This means all associated devices + telemetry + crash reports are also DB-cascade deleted. Is this the intended behavior? No warning is shown to admin before user deletion about this cascade.

4. **Multiple licenses per user:** `LicenseService.CreateAsync` has no uniqueness check on `UserId` — one user can have multiple licenses (only `Key` is unique, not `UserId`). Is this intentional (e.g., for future multi-plan support) or should a user be limited to one active license?
