# Subscriptions Audit Findings

**Tab:** Subscriptions
**URL path:** `https://admin.auracore.pro/` (SPA, sidebar nav → "Subscriptions")
**Audit date:** 2026-04-22
**Auditor:** subagent-1
**Time spent:** ~3 hours

## Source files audited

- Frontend TSX: `/root/admin-panel/src/app/page.tsx` lines 685–729 (SubscriptionsPage); lines 539–608 (UsersPage, for Revoke cross-reference)
- Frontend API client: `/root/admin-panel/src/lib/api.ts` lines 99–112
- Backend controller: `src/Backend/AuraCore.API/Controllers/Admin/AdminSubscriptionController.cs` (56 lines total)
- Backend entity: `src/Backend/AuraCore.API.Domain/Entities/Subscription.cs` + `License.cs`
- Users controller (cross-ref): `src/Backend/AuraCore.API/Controllers/Admin/AdminUserController.cs` lines 34–46
- DbContext config: `src/Backend/AuraCore.API.Infrastructure/Data/AuraCoreDbContext.cs` (referenced)
- Nginx config: `/etc/nginx/sites-enabled/auracore-admin`, `/etc/nginx/sites-enabled/auracore-api`

## Summary

- **1 critical** — tier sync broken (all pro users appear free)
- **1 medium** (downgraded from critical) — Revoke button practically unreachable due to F-1 cascade; button renders but tier display makes it misleading
- **3 high** — subscriptions table completely unused; API layer bypasses Nginx basic auth; audit log records zero admin actions
- **3 medium** — no Days field validation (min/max/tier), no confirmation on grant, UX confusion from toast showing success for a name-misleading action
- **2 low** — deployment drift (out/ is 26 days stale vs live), admin@auracore.pro has `license.Tier = 'free'` which is likely unintentional

Axes covered: functional, code+DB sync, security, UX, mobile, drift.

---

## Findings

### F-1 [CRITICAL] Tier badge always shows "free" for all users — TSX reads `u.tier` but API sends `u.license.tier`

**Axis:** code-db-sync
**Baseline bug ref:** B-1

**Symptom:** Every user in the Users tab shows a "free" tier badge, regardless of their actual license tier in the database. After granting Pro via the Subscriptions form, the Users tab immediately shows the correct license row in the API response but renders "free" anyway. The Revoke button on each user row (which uses the same `u.tier` check) therefore never appears.

**Reproduction steps:**
1. Log in as `admin@auracore.pro`
2. Navigate to → Subscriptions
3. Enter user ID `5dc362e6-5186-4a40-8ddd-23a52ac90807` (ozgurdeniz807@gmail.com), tier=Pro, days=30
4. Click Grant Access — toast says "Subscription granted!"
5. Navigate to → Users
6. Observe: ozgurdeniz807@gmail.com Tier column shows "free" (not "Pro")
7. Observe: No Revoke icon appears in Actions column for any user

**Expected behavior:** User's Tier badge reflects their active license tier. Revoke icon is visible for users with non-free licenses.

**Actual behavior:** All Tier badges show "free". No Revoke icons are shown anywhere.

**Root cause:**

- `src/Backend/AuraCore.API/Controllers/Admin/AdminUserController.cs:38–44` — The API returns user objects shaped as `{ id, email, role, createdAt, license: { tier, expiresAt } }`. The tier is nested under `license`, not at the top level. There is no top-level `u.tier` field in the response.
- `/root/admin-panel/src/app/page.tsx:582` — `<TierBadge tier={u.tier || 'free'} />` — reads `u.tier` which is `undefined` on every response object; falls through to `'free'`.
- `/root/admin-panel/src/app/page.tsx:586` — `u.role !== 'admin' && u.tier !== 'free'` — Revoke button condition is always false because `u.tier` is `undefined`, which is not `!== 'free'` in a falsy way.

The fix is one character change: `u.tier` → `u.license?.tier`.

**DB state verification:**
```sql
-- Before grant (ozgurdeniz807@gmail.com had no license):
SELECT u."Email", l."Tier" AS license_tier, l."Status"
FROM users u LEFT JOIN licenses l ON l."UserId" = u."Id"
WHERE u."Email" = 'ozgurdeniz807@gmail.com';
-- Result: license_tier = null, Status = null

-- After grant via admin UI:
-- Result: license_tier = 'pro', Status = 'active', ExpiresAt = 2026-05-22

-- API raw response confirmed via JS:
-- fetch('/api/admin/users').then(r=>r.json()) →
-- { users: [{ email: 'ozgurdeniz807@gmail.com', license: { tier: 'pro', expiresAt: '...' } }] }
-- Note: NO top-level u.tier field in API response.
```
- Actual: `u.tier = undefined` → rendered as "free" by `|| 'free'` fallback
- Expected: `u.license.tier = 'pro'` → should be the source read

**Fix suggestion:**
- Option A (minimal): Change `u.tier` → `u.license?.tier` in both `page.tsx:582` and `page.tsx:586`.
- Option B (API consistency): Add a denormalized `tier` field to the API response (`tier: license?.Tier ?? "free"`) and keep frontend unchanged. Consistent with how `AdminUserController.GetById` already does this (line 63: `tier = license?.Tier ?? "free"`).
- Option B is preferred — it matches `GetById` behavior and avoids frontend knowing about nested shape.

**Risk if unfixed:**
- User-facing (admin): Cannot see who is Pro or Free at a glance. Revoke button permanently hidden — admin cannot revoke subscriptions from the Users tab.
- Support: "Our admin panel shows everyone as Free" — confusing during support escalations.
- Security: Admin might re-grant subscriptions to already-pro users without realizing, creating duplicate license entries.

---

### F-2 [MEDIUM] Revoke button practically unreachable due to F-1 tier display bug — downstream cascade of F-1

**Axis:** functional, code-db-sync
**Baseline bug ref:** B-1 (downstream effect)

**Symptom:** The Revoke (ban) icon button that should appear for pro-tier users in the Users tab is never visible in practice. Admin cannot revoke subscriptions from the Users tab — only via a direct API call or the raw DB.

**Reproduction steps:**
1. Log in as `admin@auracore.pro`
2. Navigate to → Users
3. Observe: No revoke/ban icons in the Actions column for any user (even known pro users: baconungabunga@gmail.com, prouser99@mailnull.com)
4. Inspect the rendered HTML — no `<button title="Revoke">` exists anywhere

**Expected behavior:** For users with `license.tier !== 'free'`, a Revoke icon appears in Actions column.

**Actual behavior:** Because all users display `tier="free"` due to F-1 (the `u.tier || 'free'` fallback from undefined), the tier-display gate means no user appears revocable.

**Root cause:**
- `/root/admin-panel/src/app/page.tsx:586` — `u.tier !== 'free'` — `undefined !== 'free'` is actually `true` in JS, so technically the Revoke button IS rendered for non-admin users. However, since F-1 causes all tier badges to show "free" via `u.tier || 'free'`, the admin panel gives no indication that any user has a Pro tier, so the admin never perceives a need to click Revoke.
- This is a downstream cascade of F-1 — not an independent critical issue. The button mechanism works; the tier data feeding it is wrong.

**Revised root cause:** The Revoke button condition (`u.role !== 'admin' && u.tier !== 'free'`) evaluates to `true` for all non-admin users (because `undefined !== 'free'` is truthy). The button IS rendered. The real problem is that all tier badges show "free" due to F-1, making the Revoke buttons appear without meaningful context — and more importantly, admin cannot tell at a glance which users actually have Pro licenses, undermining the Revoke workflow entirely.

**Severity note:** Downgraded from CRITICAL to MEDIUM — this finding is a practical consequence of F-1, not an independent critical. Fixing F-1 (API shape correction) also repairs the Revoke workflow's usability.

**Risk if unfixed:** Admin cannot reliably identify which users have Pro licenses to revoke. Fix F-1 to restore full Revoke workflow usability.

---

### F-3 [HIGH] Subscriptions table is entirely unused — "Grant Subscription" writes to `licenses`, not `subscriptions`

**Axis:** code-db-sync, functional
**Baseline bug ref:** B-1 (confirmed, extended)

**Symptom:** Clicking "Grant Subscription" in the Subscriptions tab does not write to the `subscriptions` table at all. The `subscriptions` table has remained at 0 rows throughout the entire product's lifecycle despite multiple grant operations being performed. The tab's name "Subscriptions" is misleading — it actually manages `licenses` records.

**Reproduction steps:**
1. Note: `SELECT COUNT(*) FROM subscriptions;` → 0 before test
2. Log in as `admin@auracore.pro`, navigate to Subscriptions
3. Enter any valid user ID, tier=Pro, days=30, click Grant Access
4. Toast: "Subscription granted!"
5. Query: `SELECT COUNT(*) FROM subscriptions;` → still 0
6. Query: `SELECT * FROM licenses WHERE "UserId" = '<userId>';` → new row with `Tier='pro'`, `Status='active'`

**Expected behavior:** Either:
- A "Grant Subscription" action creates a `subscriptions` row (Stripe-compatible structure), OR
- The tab is correctly named "Licenses" to match what it actually does

**Actual behavior:** Grant creates/updates a `licenses` row only. The `subscriptions` table has Stripe-specific fields (`StripeSubscriptionId`, `StripeCustomerId`, `Plan`, `CurrentPeriodEnd`) that suggest it was designed for automated Stripe webhook flow — never manually.

**Root cause:**
- `src/Backend/AuraCore.API/Controllers/Admin/AdminSubscriptionController.cs:23–39` — `Grant` endpoint: writes to `_db.Licenses` only; never touches `_db.Subscriptions`.
- `src/Backend/AuraCore.API.Domain/Entities/Subscription.cs` — Entity has Stripe-specific fields, no manual grant fields.
- Mismatch: The admin UI's "Grant Subscription" is logically a "Grant License" operation.

**DB state verification:**
```sql
SELECT COUNT(*) FROM subscriptions;
-- Result: 0 (always — even after multiple grant operations)

SELECT COUNT(*) FROM licenses;
-- Result: 6 (grows with each grant to a user without a license)
```

**Fix suggestion:**
- Option A (rename): Rename the Subscriptions tab to "Licenses" or "Manual License Grant" to match what it actually does. Remove `subscriptions` from the "delete user" cascade in AdminUserController.cs (line 114–116) if subscriptions is never written.
- Option B (proper subscriptions): Keep the `subscriptions` table for Stripe webhook writes. Create a separate admin-only "manual license grant" workflow that writes to `licenses` only. Rename the Subscriptions tab accordingly.
- Option B preferred for accuracy, but Option A unblocks immediate confusion.

**Risk if unfixed:**
- Admin confusion: Admin thinks they're managing subscriptions but actually managing licenses.
- Data integrity: The `subscriptions` table is maintained in FK cascade deletes but never written to — dead code complexity.
- Future: If Stripe webhooks start writing to `subscriptions`, the admin can't reconcile Stripe vs manual grant.

---

### F-4 [HIGH] Nginx basic auth does NOT protect admin API endpoints — bypass via direct `api.auracore.pro` or `/api/` path

**Axis:** security

**Symptom:** The Nginx basic auth (`auracore_admin`/password) protecting `admin.auracore.pro` is explicitly disabled for the `/api/` location block. Any attacker with a valid JWT (stolen or brute-forced) can call admin mutation endpoints directly on `api.auracore.pro` without ever needing the basic auth credentials.

**Reproduction steps:**
1. Obtain any admin JWT (e.g., by phishing the admin login page directly via `api.auracore.pro/api/auth/login` which has no basic auth)
2. Call: `curl -X POST https://api.auracore.pro/api/admin/subscriptions/grant -H 'Authorization: Bearer <jwt>' -H 'Content-Type: application/json' -d '{"userId":"...", "tier":"pro", "days":36500}'`
3. Observe: HTTP 200, user granted pro for 100 years
4. Basic auth was never required

**Expected behavior:** Basic auth at Nginx level provides defense-in-depth even if JWT is compromised.

**Actual behavior:** `/etc/nginx/sites-enabled/auracore-admin` line ~28: `location /api/ { auth_basic off; ... }`. The basic auth is explicitly disabled for all `/api/` routes. `api.auracore.pro` vhost has no `auth_basic` directive at all.

**Root cause:**
- `/etc/nginx/sites-enabled/auracore-admin` — `auth_basic off` under the `/api/` location block
- `/etc/nginx/sites-enabled/auracore-api` — no `auth_basic` directive anywhere

The design intent was to let the Next.js SPA (which is behind basic auth) call the API freely. But this leaves the API endpoints exposed to direct access with only JWT as protection.

**DB state verification:** Not applicable (Nginx config issue).

**Fix suggestion:**
- Option A: Keep current design (JWT-only for API) but add rate limiting on `/api/auth/login` endpoint to prevent brute-force JWT acquisition.
- Option B: Implement IP allowlist at Nginx level for admin API endpoints (only allow calls from known admin IPs).
- Option C: Accept the current design — single-tenant app, admin panel only has one admin user; JWT expiry provides adequate protection. Document as accepted risk.
- The current setup is a known and common pattern for SPAs; the key risk is JWT theft/brute-force, not the basic auth bypass itself. Rate limiting on login is the priority fix.

**Risk if unfixed:**
- If admin JWT is stolen (XSS, token logging, MITM), attacker has direct API access without needing basic auth credentials.
- No defense-in-depth layer between JWT compromise and admin mutations.

---

### F-5 [HIGH] Admin actions (grant, revoke) are NOT recorded in any audit log

**Axis:** security

**Symptom:** When an admin grants or revokes a subscription, no record is written to any audit table. The "Audit Log" tab shows only login attempts (`login_attempts` table), not admin actions. There is no separate `admin_audit_log` table.

**Reproduction steps:**
1. Check DB: `SELECT * FROM login_attempts ORDER BY "CreatedAt" DESC LIMIT 5;` — shows only login events, not admin mutations
2. Grant subscription to a user via admin UI
3. Revoke it
4. Check DB again: same login_attempts rows, no new rows anywhere capturing the grant/revoke

**Expected behavior:** Admin actions (grant tier, revoke tier, delete user, etc.) are recorded with: actor email, target userId, action type, before/after state, timestamp, source IP.

**Actual behavior:**
- `src/Backend/AuraCore.API/Controllers/Admin/AdminSubscriptionController.cs` — Neither `Grant` nor `Revoke` writes to any audit table.
- `src/Backend/AuraCore.API/Controllers/Admin/AdminAuditLogController.cs:29` — The "Audit Log" controller reads `_db.LoginAttempts` only — it is entirely based on authentication events, not admin mutations.
- No `admin_audit_log` table exists in the DB (verified via `\dt`).

**DB state verification:**
```sql
\dt  -- Lists 14 tables, no admin_audit_log or similar
SELECT COUNT(*) FROM login_attempts;  -- 386 rows (all login events only)
```

**Fix suggestion:**
- Add a new `admin_audit_log` table with: `Id, ActorEmail, Action, TargetType, TargetId, Before (jsonb), After (jsonb), IpAddress, CreatedAt`.
- Add a service/middleware that logs to this table from `AdminSubscriptionController.Grant`, `AdminSubscriptionController.Revoke`, `AdminUserController.Delete`, etc.
- Update the Audit Log tab to show admin_audit_log in addition to (or instead of) login_attempts.

**Risk if unfixed:**
- Compliance: No trail of who granted Pro tier to whom and when.
- Security: If a rogue admin grants tiers to friends/self, there is no evidence.
- Support: Cannot reconstruct what happened to a user's tier ("who granted this and when?").

---

### F-6 [MEDIUM] Days field has no validation (frontend or backend) — negative days, zero, or extreme values accepted

**Axis:** functional, security

**Symptom:** The Days input in the Grant form has no `min`/`max` HTML attributes and the backend `GrantRequest` record has no `[Range]` validation. An admin can accidentally or intentionally enter `0`, `-1`, or `9999999` days.

**Reproduction steps:**
1. Navigate to Subscriptions tab
2. Enter a valid user ID, set Days = `-1`
3. Click Grant Access
4. Observe: Request succeeds; `ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1)` → already expired at creation

**Expected behavior:** Days must be in a sensible range (e.g., 1–3650). Negative values should be rejected with a clear error.

**Actual behavior:**
- Frontend (`page.tsx:717`): `<input type="number" value={days} .../>` — no `min`, no `max`, no `step`.
- Backend (`AdminSubscriptionController.cs:55`): `GrantRequest(Guid UserId, string Tier, int Days)` — no `[Range(1, 3650)]` attribute.
- `DateTimeOffset.UtcNow.AddDays(-1)` = license expires immediately. The grant "succeeds" but the user is immediately expired.

**Root cause:**
- `src/Backend/AuraCore.API/Controllers/Admin/AdminSubscriptionController.cs:55` — `GrantRequest` record has no validation attributes.
- `/root/admin-panel/src/app/page.tsx:717` — `<input type="number" ... />` with no min/max.

**Fix suggestion:**
- Backend: Add `[Range(1, 3650)]` to `Days` property in `GrantRequest`.
- Frontend: Add `min="1" max="3650"` to the Days input.
- Consider: Add a "Tier must be 'pro' or 'enterprise'" validation (`[AllowedValues("pro", "enterprise")]`) to prevent arbitrary tier string injection.

**Risk if unfixed:**
- Admin typo (e.g., `0` days) creates an immediately-expired license — user appears to have Pro but gets no access.
- Arbitrary tier strings can be stored in DB (e.g., `tier = "forever"`) which downstream code may not handle.

---

### F-7 [MEDIUM] No confirmation dialog on Grant — accidental grant has no undo in UI

**Axis:** UX

**Symptom:** Clicking "Grant Access" immediately fires the API call with no "Are you sure?" confirmation. If admin accidentally enters wrong user ID or wrong tier, there is no undo in the UI (must manually revoke).

**Reproduction steps:**
1. Navigate to Subscriptions
2. Enter any text in User ID (even wrong GUID), set tier, click Grant Access
3. If wrong: only option is to navigate to Users tab and try to find the Revoke button (which is broken per F-1/F-2)

**Expected behavior:** A confirmation step showing: "Grant Pro for 30 days to [email]?" with confirm/cancel before firing the API.

**Actual behavior:** `/root/admin-panel/src/app/page.tsx:692` — `const handleGrant = async () => { if (!userId) { ... return; } const { ok } = await api.grantSubscription(...)` — no confirmation, immediate fire.

**Fix suggestion:**
- Add a `window.confirm()` before the API call (quick fix), or a modal dialog showing the resolved user email (better UX — requires a user-lookup step first).
- The modal approach would also surface the correct user email before grant (currently admin must copy-paste GUID from Users tab without confirmation).

**Risk if unfixed:**
- Admin typo grants Pro to wrong user — support ticket and reputation issue.
- Double-click on Grant Access could fire twice (no debounce — though the backend is idempotent, the second call extends expiry again).

---

### F-8 [MEDIUM] Toast says "Subscription granted!" but actually a license record is written — misleading success message

**Axis:** UX, code-db-sync
**Baseline bug ref:** B-3 (stale-after-mutation variant)

**Symptom:** The success message "Subscription granted!" is semantically wrong (it's a license grant, not a subscription). More critically, the success message uses `msg.includes('!')` to determine green vs red coloring, which means any future error message containing `!` will be styled green.

**Reproduction steps:**
1. Navigate to Subscriptions, grant a valid user
2. Observe: "Subscription granted!" in green
3. Code-review: `page.tsx:694` — `setMsg(ok ? 'Subscription granted!' : 'Failed to grant subscription')`
4. Color check: `page.tsx:695` — `className={... msg.includes('!') ? 'text-aura-green' : 'text-aura-red'}`
5. Edge case: if error message contained `!` (e.g., "Error! Server timeout"), it would be styled green

**Root cause:**
- `/root/admin-panel/src/app/page.tsx:695` — Color determined by `!` in message string rather than `ok` boolean.
- After grant, the form state doesn't reset (userId/tier/days remain filled), making it easy to accidentally double-click.

**Fix suggestion:**
- Option A: Change color logic to use `ok` state variable: `ok ? 'text-aura-green' : 'text-aura-red'`.
- Option B: Keep `!` convention but ensure error messages never contain `!`.
- Also: Clear the userId field after successful grant.
- Also: Rename "Subscription granted!" to "License granted (Pro, 30 days) — email@example.com" for clarity.

**Risk if unfixed:**
- Future error messages styled wrong color → admin misinterprets failure as success (worst case).

---

### F-9 [LOW] Deployment drift: `/root/admin-panel/out/` is 26 days behind live production

**Axis:** drift
**Baseline bug ref:** B-4

**Symptom:** The static export at `/root/admin-panel/out/` was built on March 27, 2026. The live site at `/var/www/admin-panel/` uses chunks from April 21, 2026 (build hash `z9RhfupaUXdIAsFRC1wkP` vs `_QcOcjn-tlhipbIhvzK4k` in `out/`). The admin-panel directory is NOT a git repo.

**Root cause:**
- Someone ran `npm run build` + deployed the new output to `/var/www/admin-panel/` without copying to `out/`.
- The `out/` directory was last rebuilt March 27, 2026; the source `page.tsx` at that path corresponds to that older build.
- `/root/admin-panel/` is not a git repo (verified by `git log` returning "not a git repo"). No version history except timestamped bak dirs in `/root/`.

**DB state verification:**
```bash
stat /root/admin-panel/out/index.html    # Modify: 2026-03-27
stat /var/www/admin-panel/index.html     # Modify: 2026-04-21
diff -r --brief /root/admin-panel/out/ /var/www/admin-panel/
# Shows: deployed has 3 extra build hashes, 5+ extra chunk files vs out/
```

**Fix suggestion:**
- Initialize git in `/root/admin-panel/` and commit source.
- Establish a deploy script that always runs `npm run build && cp -r .next/standalone/* /var/www/admin-panel/` (or equivalent).
- Until then: the source files in `/root/admin-panel/src/` may not perfectly match what's in production. Audit findings based on source are best-effort for the deployed JS (though functionally they match the observed live behavior).

**Risk if unfixed:**
- Source edits to `/root/admin-panel/src/` won't take effect without a rebuild+redeploy — easy to forget.
- No rollback history (only `/root/admin-backup-*` dirs).

---

### F-10 [LOW] `admin@auracore.pro` has `license.Tier = 'free'` with expiry year 2126 — anomalous data

**Axis:** code-db-sync

**Symptom:** The admin account has a license row with `Tier='free'` but `ExpiresAt = 2126-03-27`. Normal free-tier licenses don't have expiry dates (it's a permanent free tier). This appears to be a test artifact or a grant-then-revoke that left the ExpiresAt populated.

**DB state verification:**
```sql
SELECT "Email", "Role", l."Tier", l."Status", l."ExpiresAt"
FROM users u JOIN licenses l ON l."UserId" = u."Id"
WHERE u."Email" = 'admin@auracore.pro';
-- Result: Tier='free', Status='active', ExpiresAt='2126-03-27 23:48:23'
```

**Fix suggestion:**
- Clean up: `UPDATE licenses SET "ExpiresAt" = NULL WHERE "UserId" = (SELECT "Id" FROM users WHERE "Email" = 'admin@auracore.pro');` (requires user approval — write gate applies).
- Or leave it — expiry year 2126 is 100 years away; it won't expire during the product's lifetime.
- Document as known test artifact.

**Risk if unfixed:** Low. The admin account doesn't have Pro tier, just an anomalous expiry date that won't cause functional issues.

---

## Axis-by-axis coverage

### 1. Functional

- **Grant action:** Works (writes to `licenses` table) — but is misleadingly named "Grant Subscription". See F-3, F-8.
- **Revoke action:** Backend works (`licenses.Tier = 'free'`), but UI Revoke button in Users tab is inaccessible due to F-1/F-2.
- **List view:** No list view on Subscriptions tab — it is a form-only page. This is by design (no pagination/search/sort to test).
- **Empty state:** No empty state handling needed — form-only page.
- **Success/failure feedback:** Toast present but has color-logic bug (F-8).
- **Days=0 or negative:** Accepted without error (F-6).
- **No confirmation dialog:** Immediate fire on Grant (F-7).

### 2. Code + DB sync

- **B-1 confirmed:** `AdminSubscriptionController.Grant` writes `Licenses.Tier` only. `subscriptions` table has 0 rows. See F-3.
- **Frontend reads wrong field:** `u.tier` instead of `u.license?.tier` on UsersPage (F-1). API sends correct data; frontend renders wrong.
- **After grant, does UI reflect?** Partially — Users tab shows stale tier badge (always free). No optimistic update; depends on `load()` call post-grant.
- **Revoke path:** Same dual-table issue — revoke sets `license.Tier = 'free'` only. Confirmed: revoke 200 OK, `licenses.Tier = 'free'` in DB.
- **Cross-tab impact:** Grant on Subscriptions tab does NOT update Users tab live (separate SPA state). Navigating to Users re-fetches correctly but renders wrong due to F-1.

### 3. Security

- **Authorization on every endpoint:** Yes — `[Authorize(Roles = "admin")]` at controller level (`AdminSubscriptionController.cs:11`). Verified: unauthenticated calls return 401.
- **IDOR:** Not applicable — single-tenant app. Any admin JWT can call any endpoint. No user-scoped access control to bypass.
- **CSRF:** Not applicable — stateless JWT (no cookie-based auth). No CSRF risk.
- **XSS:** No `dangerouslySetInnerHTML` in SubscriptionsPage or UsersPage. React default escaping applies. No XSS vectors found.
- **SQL injection:** No raw SQL in `AdminSubscriptionController`. All queries via EF Core parameterized. No injection vectors.
- **Rate limit:** No rate limiting on Grant or Revoke endpoints (F-4 context). An admin could call Grant in a loop without throttling. Low severity given auth requirement.
- **Audit log:** MISSING — no admin actions logged anywhere (F-5). Critical gap.
- **Nginx basic auth bypass:** The `/api/` location block has `auth_basic off` (F-4). API endpoints rely solely on JWT.
- **Tier value injection:** Backend accepts any string as `Tier` (F-6). No allowlist validation.

### 4. UX

- **Loading state:** No loading indicator during Grant API call. Button doesn't disable during request — double-submit possible.
- **Error state:** Error message shown in red text (when correctly determined) if grant fails.
- **Empty state:** N/A — form-only page.
- **Destructive confirmation:** None on Grant (F-7). Note: Grant is not truly destructive, but an accidental wrong-user grant is painful to undo given F-1/F-2.
- **Toast feedback:** Present but color logic is fragile (F-8).
- **Refresh survival (Bug 3):** Subscriptions tab has NO Refresh button — Bug 3 does not apply to this specific tab. Hard page reload (navigate to `admin.auracore.pro`) survives correctly because JWT is in `localStorage`. SPA state resets to Dashboard on reload (expected for SPA).

### 5. Mobile

- **1440px (desktop):** Functional. Sidebar=260px, main=~1180px, form max-w-xl (576px) renders cleanly.
- **768px (tablet):** Functional but cramped. Sidebar=260px, main=~508px. Form fits but the grid `grid-cols-2` for Tier+Days is tight.
- **414px:** Sidebar (260px) + main (154px) — main content is barely usable. Form would overflow into sidebar.
- **375px:** Sidebar (260px) + main (115px) — completely broken layout. Main content area is ~115px wide; form inputs would be clipped.
- **320px:** Sidebar (260px, or 72px if manually collapsed) + main (60px or 248px). Without manual sidebar collapse: content area is 60px — completely inaccessible. With manual collapse: 248px — barely usable.
- **Root cause:** `/root/admin-panel/src/app/page.tsx:1460` — Layout `<div className="flex h-screen overflow-hidden">` has no responsive breakpoints. Sidebar (`page.tsx:152`) uses fixed `w-[260px]` or `w-[72px]` with no `sm:hidden`, `md:block`, or hamburger trigger. There is a collapse button (small arrow at top of sidebar) but it is tiny (24×24px) and requires manual interaction.
- **No hamburger menu:** Confirmed — no mobile nav toggle exists in the codebase.
- **Tap targets:** Grant Access button is full-width and adequately sized. Days and User ID inputs are standard height. Tier select is standard height. At 320px these are inaccessible due to layout overflow, not tap target size.

### 6. Deployment drift

- **Source vs deployed:** `/root/admin-panel/out/` is from build `_QcOcjn-tlhipbIhvzK4k` (2026-03-27). Live `/var/www/admin-panel/` is from build `z9RhfupaUXdIAsFRC1wkP` (2026-04-21). 26-day gap (F-9).
- **Git status:** `/root/admin-panel/` is NOT a git repo. 
- **Backup dirs:** Multiple `/root/admin-backup-*` dirs with timestamps suggesting rollback history (auracore-src-backup-20260412, auracore-src-backup-audit-202604122118). The April 21 deployment appears to be a rebuild from current source.
- **Features in source vs live:** The live deployed page-9bf9edb4333e55cf.js (73KB) contains `grantSubscription`, `revokeSubscription`, the 30-day default — functionally matching the source. No evidence of features in source but missing from live, or vice versa (despite the build hash difference).
- **Rollback artifact:** The discrepancy between `out/` (March) and live (April) means the source tree for the March version was the baseline for this audit's file:line references. The April 21 deployed version is the actual running code. For Subscriptions specifically, the SubscriptionsPage component logic appears identical based on JS chunk content inspection.

---

## Questions for user (if any)

1. **Admin license data:** `admin@auracore.pro` has `licenses.Tier = 'free'` with `ExpiresAt = 2126-03-27`. Should this be cleaned up (ExpiresAt set to NULL)? Needs a DB write.
2. **Subscriptions table intent:** Is the `subscriptions` table intended only for Stripe webhook flow, or should manual grants also write to it? This shapes the fix for F-3.
3. **Tier validation list:** Is "enterprise" the only non-free tier besides "pro"? Should "enterprise" be grantable from the admin panel, or should the select be changed to include more options?
4. **Mobile priority:** The admin panel is completely unusable on phones (below 768px). Is mobile admin panel access a hard requirement, or is desktop-only acceptable?
