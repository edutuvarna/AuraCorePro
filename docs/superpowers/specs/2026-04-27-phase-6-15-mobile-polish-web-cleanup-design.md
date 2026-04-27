# Phase 6.15 — Mobile Polish + Web Carry-forward Wave — Design

**Date:** 2026-04-27
**Author:** Brainstormed in Phase 6.15 session (post-6.14-merge, brainstorming-only mode; plan + execution → fresh session)
**Status:** Draft, ready for `superpowers:writing-plans` skill in a fresh session
**Successor to:** [Phase 6.14 RN Mobile Companion (merged at `565fa81`)](../../../memory/project_phase_6_item_14_rn_mobile_complete.md)

## Goal

Close two carry-forward streams in one phase:

- **Mobile Tier-0 UX correctness** — eliminate the multiple-biometric-prompt UX (4 prompts per cold start, +1 per tab switch) and add JWT refresh-token rotation so the mobile app remains usable past the 15-min access-token window.
- **Web Tier-3 deferred features** — three items that have been carried from Phase 6.13 → 6.14 → 6.15: bulk role change (multi-select admin promote/demote), ASP.NET Core RateLimiter hot-reload (UI edits already write the policy to DB but the middleware ignores it), and audit-log retention (table grows unbounded; needs UI + scheduled cleanup).

Total: 5 items across mobile + backend + web admin. Scope C from the brainstorm dialogue (Q1).

## Non-goals

- **No FCM env-var setup** — Phase 6.16 (user has not paid Firebase Cloud Messaging $10 verification fee). `FcmService` graceful no-op stays as-is; push notifications inert until 6.16.
- **No iOS port** — 6.16+ when Apple Developer account ($99/yr) and Mac available.
- **No Play Store Internal Testing migration** — 6.16+ when Google Play Developer ($25) acquired.
- **No incident-response mobile feature pack** (Users list / ban / force-logout from mobile) — 6.16 alongside iOS port.
- **No TOTP backup codes / force-logout-all / web admin RedeemInvitationPage refactor** — 6.16+.
- **No FcmService cache lifecycle refactor (I2 from 6.14 review)** — backend perf optimization, not correctness; 6.16+.
- **No WebView Turnstile / Play Integrity** — replaces mobile-client header bypass; 6.16+.
- **No archival to S3/R2** of audit_log on retention cleanup — over-engineering for current compliance posture.
- **No CSV upload bulk role change** — 5-admin team scope; multi-select UI is sufficient.
- **No eager JWT pre-refresh** — lazy 401-retry sufficient.

## Scope (5 items)

| # | Item | Surface | Effort |
|---|---|---|---|
| **6.15.1** | Mobile single-gate biometric + module-level JWT cache | `mobile/src/lib/secureStore.ts` + `authContext.tsx` + `api.ts` | S |
| **6.15.2** | Mobile JWT refresh-token rotation on 401 | `mobile/src/lib/api.ts` + `auth.ts` | S |
| **6.15.3** | Backend custom `IRateLimiter` with DB-cache + on-save invalidation | `Program.cs` + new `Infrastructure/RateLimiting/` + existing `IRateLimitPoliciesService` | M |
| **6.15.4** | Web admin bulk role change (multi-select on AdminManagementPage + audit preview modal + bulk endpoint) | `admin-panel/src/views/AdminManagementPage.tsx` + new components + `Controllers/Superadmin/AdminManagementController.cs` | M |
| **6.15.5** | Audit-log retention UI + scheduled cleanup `IHostedService` | `admin-panel/src/views/AuditRetentionPage.tsx` + new `Services/Background/AuditLogCleanupService.cs` + `system_settings` row | M |

Effort: 5-7 days for a solo dev. Items 1+2 are mobile-only (parallel with backend if multi-process). Items 3+5 share the `IHostedService` + DI registration patterns. Item 4 is admin-panel + backend.

## Locked design decisions

- **D1: Phase scope (Q1)** — Tier 0 mobile (2 items: JWT cache + JWT refresh; FCM env-vars out) + Tier 3 web (3 items: bulk role + RateLimiter hot-reload + audit retention). Approach C from brainstorm.
- **D2: FCM defer (Q2)** — Phase 6.16. Firebase Cloud Messaging $10 verification fee not paid yet. `FcmService` graceful no-op via env-var check stays from 6.14.
- **D3: Audit retention design (Q3)** — Variant B. UI + scheduled `IHostedService` background job. Retention days persisted in `system_settings` (default 365). Daily run deletes older rows + writes a summary `retention_run` row to audit_log itself. No S3/R2 archival.
- **D4: Bulk role change UX (Q4)** — Variant A. Multi-select on the existing AdminManagementPage admin list (checkbox per row + floating "Promote N / Demote N" toolbar). Modal with shared template + force_pw_change config + audit preview (diff: who changes from what to what). Single transaction.
- **D5: RateLimiter hot-reload approach (Q5)** — Variant A1. Custom `IRateLimiter` reads policy via cached `IRateLimitPoliciesService`. **On-save invalidation = instant** in single-replica deployments (cache invalidation is microseconds; next request fresh policy). 5-min TTL is defensive safety net only. The user's "anlık hot-reload" requirement is satisfied — the cache misnomer is just terminology.
- **D6: Mobile JWT (Q6)** — Item 1: single-gate biometric (drop `requireAuthentication: true` from secureStore JWT key, use ONE explicit `LocalAuthentication.authenticateAsync` at app start) + module-level memory cache populated by AuthProvider on successful unlock. All `api.ts request()` calls read from cache, never touch secureStore. Item 2: lazy refresh on 401 (intercept 401, POST `/api/auth/refresh`, store new tokens, retry original request; on refresh-token 401 → logout).

## Architecture per item

### 6.15.1 — Mobile JWT cache + single-gate biometric

**Threat-model trade-off:** Current double-gate (requireAuthentication on key + explicit LocalAuthentication prompt) gives defense-in-depth at significant UX cost (4 prompts per cold start, +1 per tab switch). Single-gate (LocalAuthentication only) matches banking-app standard pattern. The Android Keystore master key is still hardware-encrypted; physical-device-theft + screen-unlock-bypass scenario already exposes the in-memory cache equivalently — no marginal security loss.

**Files:**
- Modify: `mobile/src/lib/secureStore.ts`
- Modify: `mobile/src/lib/authContext.tsx`
- Modify: `mobile/src/lib/api.ts`
- Modify: `mobile/src/lib/auth.ts`

**`secureStore.ts` changes:**
- `setJwt(token)` writes with `requireAuthentication: false` (was true)
- `setRefreshToken(token)` same
- `getLastActiveAt` was already without requireAuthentication
- Add module-level cache: `let cachedJwt: string | null = null;` and `let cachedRefresh: string | null = null;`
- Export `setCachedJwt(t)`, `getCachedJwt()`, `setCachedRefreshToken(t)`, `getCachedRefreshToken()`, `clearAuthCache()`
- `clearAuth()` now also clears the in-memory caches

**`authContext.tsx` `AuthProvider` mount effect:**
1. Check `isInactiveBeyondLimit(30)` (no biometric prompt — last_active key has no requireAuthentication)
2. If inactive → `clearAuth()` + setChecking(false) → Index dispatcher redirects to /(auth)/login
3. If JWT exists in secureStore (no biometric prompt because we removed requireAuthentication):
   - Call `LocalAuthentication.authenticateAsync({promptMessage: 'Unlock AuraCore Admin', fallbackLabel: 'Use password', disableDeviceFallback: false})` — **the single biometric prompt**
   - On success: read JWT + refresh from secureStore (no biometric since requireAuthentication=false now) → call `setCachedJwt(jwt)` + `setCachedRefreshToken(rt)` → setAuth(...) → router.replace('/(app)')
   - On 3-fail/cancel: clearAuth() → /(auth)/login
4. If JWT missing: setChecking(false) → /(auth)/login

**`api.ts` changes:**
- `request()` reads token via `getCachedJwt()` (no biometric, no secureStore round-trip)
- On login success (LoginScreen.submit): caller writes via `setCachedJwt(at)` + `setCachedRefreshToken(rt)` AFTER `persistLoginSuccess` (which writes to secureStore), then sets AuthContext

**`auth.ts.logout()`:**
- `unregisterPush()` (best-effort)
- `clearAuthCache()` (in-memory)
- `clearAuth()` (secureStore)

**Result:** cold start = 1 prompt total (the explicit "Unlock AuraCore Admin" biometric). Tab switching, Dashboard refresh, pull-to-refresh = 0 prompts. Logout cleanly resets state.

### 6.15.2 — Mobile JWT refresh on 401

**Files:**
- Modify: `mobile/src/lib/api.ts` (add 401 interceptor + refresh path)
- Modify: `mobile/src/lib/auth.ts` (add `tryRefreshToken()` helper)
- Add: `mobile/__tests__/lib/api-refresh.test.ts`

**`request()` 401 interceptor:**
```typescript
async function request(path: string, init: RequestInit = {}, isRetry = false): Promise<Response> {
  const token = getCachedJwt();
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...((init.headers as Record<string, string>) ?? {}),
  };
  if (token) headers['Authorization'] = `Bearer ${token}`;
  if (MOBILE_CLIENT_SECRET) headers['X-Auracore-Mobile-Client'] = MOBILE_CLIENT_SECRET;
  const res = await fetch(`${API}${path}`, { ...init, headers });

  // Phase 6.15.2 — lazy refresh on 401. Single retry; if refresh itself 401s,
  // the user's refresh token is dead → logout + redirect to login.
  if (res.status === 401 && !isRetry && !path.includes('/auth/')) {
    const refreshed = await tryRefreshToken();
    if (refreshed) {
      return request(path, init, true);  // retry once with new token
    }
    // refresh failed → trigger logout via auth context elsewhere
  }
  return res;
}
```

**`auth.ts.tryRefreshToken()`:**
```typescript
export async function tryRefreshToken(): Promise<boolean> {
  const refresh = getCachedRefreshToken();
  if (!refresh) return false;
  const res = await fetch(`${API}/api/auth/refresh`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json',
      ...(MOBILE_CLIENT_SECRET ? { 'X-Auracore-Mobile-Client': MOBILE_CLIENT_SECRET } : {}) },
    body: JSON.stringify({ refreshToken: refresh }),
  });
  if (!res.ok) return false;
  const data = await res.json().catch(() => null);
  if (!data?.accessToken || !data?.refreshToken) return false;
  await persistLoginSuccess(data.accessToken, data.refreshToken);
  setCachedJwt(data.accessToken);
  setCachedRefreshToken(data.refreshToken);
  return true;
}
```

**Edge cases:**
- Concurrent requests after token expires: each gets 401, each tries refresh. Race possible — second refresh might 401 because refresh token was just rotated by first. Mitigation: single-flight via `Promise<boolean>` cache for in-flight refresh; concurrent callers await the same promise.
- Refresh fails → logout. Implementation: emit an event (or set a global "needs logout" flag) that AuthProvider observes and triggers `setAuth(null)` + router.replace.

**Test surface:**
- 401 → refresh OK → retry with new token → success
- 401 → refresh 401 → no infinite loop, returns 401 to caller
- Concurrent 401s → single refresh, both callers retry with same new token

### 6.15.3 — Backend custom `IRateLimiter` with cached policy

**Existing surface:** Phase 6.12 added `APIRateLimitsPage` (admin-panel UI to edit policy in `system_settings['rate_limit_policies']`) and `IRateLimitPoliciesService` (DB-backed read with 5-min cache, invalidate-on-save). But ASP.NET Core's built-in `services.AddRateLimiter(...)` middleware uses startup-time static policies — DB edits never reach it.

**Files:**
- Create: `src/Backend/AuraCore.API.Infrastructure/RateLimiting/CustomRateLimiter.cs`
- Create: `src/Backend/AuraCore.API.Infrastructure/RateLimiting/RateLimiterMiddleware.cs`
- Modify: `src/Backend/AuraCore.API/Program.cs` (replace `AddRateLimiter` with custom middleware registration)
- Modify: `src/Backend/AuraCore.API.Infrastructure/Services/RateLimit/RateLimitPoliciesService.cs` (already has cache invalidation; verify)
- Add: `tests/AuraCore.Tests.API/Phase615/CustomRateLimiterTests.cs`

**`CustomRateLimiter`:** Token-bucket implementation, `ConcurrentDictionary<string, BucketState>` keyed on `(policyName, bucketKey)` where bucketKey is the IP or other identifier per the policy. `BucketState` = `(double tokens, long lastRefillTimestamp)`. On request:
1. Resolve policy name (e.g. "auth.login") from endpoint → look up in `IRateLimitPoliciesService` (cached 5-min, invalidate on save)
2. Compute bucket key (default IP)
3. Atomically refill bucket based on elapsed time + policy's tokens-per-window
4. If tokens >= 1: decrement, allow. Else: block (return Retry-After header).
5. Per-bucket lock via `ConcurrentDictionary.AddOrUpdate` lambda — no global lock.

**`RateLimiterMiddleware`:** Reads policy name from endpoint metadata (matches existing `[EnableRateLimiting("auth.login")]` attribute style). Falls through if no policy attribute. Calls `CustomRateLimiter.TryAcquire(policyName, key)` → 429 with Retry-After if blocked.

**Hot-reload mechanics:**
- Admin saves policy via UI → `RateLimitPoliciesService.SetPolicy(name, ...)` writes DB row + `_cache.Remove("rate_limit_policies")`
- Next API call → `CustomRateLimiter.GetPolicy(name)` → cache miss → DB read → cache populate → fresh policy applied to that bucket and all subsequent
- Bucket state itself stays — only the policy parameters change. Existing partial-bucket consumption preserved.
- 5-min TTL is the "no-save" refresh interval (defensive); save-driven invalidation is microseconds in-process.

**Test surface:**
- Token-bucket math: 5-req/30-min policy → 6th req in window blocked
- Cache invalidation: edit policy → next request applies new limit immediately
- Bucket-key isolation: different IPs don't share buckets
- 429 includes Retry-After header

### 6.15.4 — Web admin bulk role change

**Files:**
- Modify: `admin-panel/src/views/AdminManagementPage.tsx` (add multi-select state + toolbar + modal)
- Create: `admin-panel/src/components/BulkRoleChangeModal.tsx` (config + audit preview)
- Modify: `admin-panel/src/lib/api.ts` (add `bulkPromoteUsersToAdmin` / `bulkDemoteAdminsToUser`)
- Modify: `src/Backend/AuraCore.API/Controllers/Superadmin/AdminManagementController.cs` (new bulk endpoints)
- Add: `tests/AuraCore.Tests.API/Phase615/BulkRoleChangeTests.cs`
- Add: `admin-panel/src/__tests__/views/AdminManagementBulk.test.tsx`

**UI flow:**
1. AdminManagement page renders admin list (existing)
2. Per-row checkbox added; clicking ANY checkbox shows a sticky bottom toolbar: "Promote N selected" + "Demote N selected" + "Cancel" + selection count
3. Promote/Demote click → opens `BulkRoleChangeModal`
4. Modal: shared template picker (Default/Trusted/ReadOnly/Custom) + force-pw-change picker + require_2fa checkbox + **audit preview** section showing each user's current state and target state ("user@x.com: user → admin (Trusted, force_pw=on_first_login)")
5. Confirm → POST /api/superadmin/admins/bulk-promote (or bulk-demote) with array of user IDs + shared config
6. Backend wraps everything in single `IDbContextTransaction` — all-or-nothing. Audit log row per user.
7. Response: `{ succeeded: N, failed: 0, errors: [] }` or partial-failure shape.

**Backend endpoints:**
```csharp
[HttpPost("admins/bulk-promote")]
[RequiresPermission(PermissionKeys.ActionUsersPromote)]
[AuditAction("BulkPromoteUsers", "User")]
public async Task<IActionResult> BulkPromote([FromBody] BulkPromoteDto dto, CancellationToken ct) { ... }

[HttpPost("admins/bulk-demote")]
[RequiresPermission(PermissionKeys.ActionUsersDemote)]
[AuditAction("BulkDemoteAdmins", "User")]
public async Task<IActionResult> BulkDemote([FromBody] BulkDemoteDto dto, CancellationToken ct) { ... }
```

DTOs include `Guid[] UserIds`, shared template/force_pw/require_2fa for promote, and audit preview is computed server-side too (defense-in-depth — modal preview is for UX, server-side is canonical).

**Concurrency safety:** Transaction acquires row-level locks on the user rows. Concurrent admin actions on same user → second waits or fails (whichever Postgres serializability returns).

### 6.15.5 — Audit-log retention UI + IHostedService

**Files:**
- Create: `admin-panel/src/views/AuditRetentionPage.tsx`
- Create: `src/Backend/AuraCore.API/Controllers/Superadmin/AuditRetentionController.cs`
- Create: `src/Backend/AuraCore.API.Infrastructure/Services/Background/AuditLogCleanupService.cs`
- Modify: `src/Backend/AuraCore.API/Program.cs` (register `AddHostedService<AuditLogCleanupService>()`)
- Modify: `admin-panel/src/app/AdminPanel.tsx` (add `auditRetention` to SUPERADMIN_EXTRA_GROUPS)
- Add: `tests/AuraCore.Tests.API/Phase615/AuditRetentionTests.cs`

**UI:**
- Settings → Audit Retention page (superadmin only)
- Current state card: total row count, oldest entry timestamp, last cleanup run timestamp + rows deleted
- Policy editor: "Retain for N days" slider/input (range 30-3650, default 365). Save.
- Manual trigger: "Run cleanup now" button (admin-driven supplement to scheduled run)

**Backend endpoints:**
```csharp
GET  /api/superadmin/audit-retention/policy   → { retentionDays, lastRunAt, lastRunDeletedRows, totalRows, oldestAt }
POST /api/superadmin/audit-retention/policy   { retentionDays }   → updates system_settings row
POST /api/superadmin/audit-retention/run-now  → triggers immediate cleanup (returns deleted count)
```

**`AuditLogCleanupService`:** `BackgroundService` (IHostedService). On startup: read retention from `system_settings` (default 365). Daily timer (24h interval). Each tick:
1. Read current retention days
2. `DELETE FROM audit_log WHERE created_at < now() - retention_days`
3. Write a `retention_run` audit_log row: `Action="AuditRetentionRun"` + `AfterData={"deleted":N,"retentionDays":D}`
4. Update `system_settings` `audit_retention.lastRunAt` + `audit_retention.lastRunDeletedRows`
5. Log warning if deletion count > 10000 (suspicious growth)

**Edge cases:**
- Service restart mid-cleanup: transaction rollback safety — `DELETE` is wrapped in transaction with `SaveChangesAsync`, partial state impossible
- Multiple-replica deployment (future): two cleanup services would race. Mitigation: advisory lock via `pg_try_advisory_lock` keyed on the retention service. Single replica today; flag for 6.16+.

**Pattern reusability:** `IHostedService` foundation is new for AuraCore. Future cleanup tasks (revoked_tokens periodic sweep, FCM stale-token cleanup, expired invitation cleanup) all benefit from this pattern. Consider extracting `IPeriodicCleanupService` base in 6.16 if 2+ cleanup services emerge.

## Testing

| Item | Type | New tests | Notes |
|---|---|---|---|
| 6.15.1 | Mobile unit | 2-3 | secureStore: setJwt without requireAuthentication; cachedJwt roundtrip; clearAuthCache resets |
| 6.15.2 | Mobile unit | 2-3 | request() 401 → tryRefreshToken → retry; refresh 401 → no infinite loop; concurrent 401 → single in-flight |
| 6.15.3 | Backend unit | 4-5 | Token-bucket math; cache invalidation; bucket-key isolation; 429 + Retry-After header; concurrent requests on same bucket |
| 6.15.4 | Backend unit | 3-4 | Bulk promote transaction commit; partial failure rollback; concurrency safety; audit log row per user |
| 6.15.4 | FE unit | 1-2 | Multi-select toolbar appears when N≥1; modal renders audit preview |
| 6.15.5 | Backend unit | 3-4 | Cleanup service deletes old rows; retention policy persisted via system_settings; manual trigger respects current policy; retention_run audit log written |

Backend test budget: 215 → 230-235 (+12-15). Mobile: 16 → 20-23 (+4-7). FE admin-panel: 83 → 86-89 (+3-6).

## Operational deploy

Single deploy window at end-of-phase, similar to 6.13/6.14:

1. Backend Release publish + scp + chown + systemctl restart
2. Admin-panel rebuild + scp + chown
3. Mobile EAS Build + sideload APK to admin team
4. EF migration: none for 6.15 (no schema changes — `system_settings` row is INSERT/UPDATE only). Confirmed by re-read of all 5 architecture sections.
5. No new env-vars (FCM defer to 6.16)

Backend deploy stamp pattern: `bak-YYYYMMDDHHMMSS`. Same as 6.13/6.14.

## Risks

- **Single-flight refresh** (6.15.2) — concurrent 401s from N parallel API calls during the 100-200ms refresh window. Without single-flight, each fires its own refresh, second+third get 401 because refresh token already rotated. Mitigation: module-level `let inFlightRefresh: Promise<boolean> | null = null;` cache. Test surface includes concurrent-401 scenario.
- **Custom RateLimiter regression** (6.15.3) — replacing the built-in middleware risks behavioral regressions (rate limits suddenly stronger or weaker than before). Mitigation: Phase 6.12 added unit tests for the existing limiter; ensure the new `CustomRateLimiterTests` cover the same input/output contract.
- **Bulk role change permissions cascade** (6.15.4) — promoting a user to admin grants them implicit permissions per their template. If template is "Trusted" they get all Tier 2 permissions immediately. The audit preview must surface this clearly so the operator doesn't miss-click. Mitigation: explicit "Granting permissions: X, Y, Z" line per user in the preview modal.
- **Audit retention cleanup runtime cost** (6.15.5) — `DELETE FROM audit_log WHERE created_at < ...` on a multi-million-row table can be slow + lock-heavy on Postgres without proper index. Mitigation: ensure `audit_log.created_at` has an index (verify; add migration if missing); cleanup runs at low-traffic UTC time.
- **Single-gate biometric** (6.15.1) — the trade-off was discussed; this is a deliberate UX-vs-defense-in-depth call. Worst-case threat is physical-device-theft + screen-unlock-bypass, where in-memory cache is equivalently exposed. Documented in code comment.

## Carry-forward → Phase 6.16

(Subset of 6.14's already-known carry-forward, refreshed for what 6.15 closes:)

**Closed in 6.15:** mobile JWT cache + refresh; bulk role change; RateLimiter hot-reload; audit retention dashboard.

**Still pending → 6.16+:**
- FCM env vars + push notifications activation (Firebase $10 fee blocker)
- FcmService access-token cache singleton refactor (I2 from 6.14 review)
- Web admin RedeemInvitationPage proper refactor (drop Phase 6.13 sessionStorage shim)
- WebView Turnstile or Play Integrity to replace mobile-client header bypass
- TOTP backup codes
- Force-logout-all (superadmin kill switch for compromised accounts)
- Mobile incident-response feature pack (Users list + ban + force-logout) — 6.14 Q4 option C
- iOS port (Apple Developer + Mac required)
- Play Store Internal Testing migration ($25 fee)
- OTA updates via `eas update`
- claude-mem keepalive + auto-patch survival (separate side-project, see `project_claude_mem_keepalive_patch.md`)

## Continuity note

Brainstorming completed in this session. Plan + execution → fresh session.

**Resume command for fresh session:**

```
Read C:\Users\Admin\Desktop\AuraCorePro\AuraCorePro\docs\superpowers\specs\2026-04-27-phase-6-15-mobile-polish-web-cleanup-design.md.

Branch: not yet created. Off main HEAD `7b477e8` (Phase 6.14 ceremonial close).
Standing prefs: subagent-driven, supervisor mode, skip spec-review gate, critical security auto-deploy without asking, visual companion always-yes.
6 decisions locked (D1-D6). 5 items in scope. No further user-confirmation needed before plan generation.
Test budget: backend 215 → ~230, mobile 16 → ~22, FE admin-panel 83 → ~88.
Next step: invoke superpowers:writing-plans skill on this spec.
```

## Self-review notes

- **Placeholders:** none.
- **Internal consistency:** D2 (FCM defer) consistent with non-goals + Item 6.15.x list. D3 (audit retention variant B) consistent with section 6.15.5 architecture (UI + IHostedService, no archival). D5 (RateLimiter A1) consistent with cache-invalidation flow described in section 6.15.3 hot-reload mechanics. D6 (mobile JWT) item 1 consistent with section 6.15.1 (single-gate biometric). D6 item 2 consistent with section 6.15.2 (lazy 401 refresh + single-flight).
- **Scope check:** Single-implementation-plan scope. 5 items each with bounded surface; mobile + backend + admin-panel touch is intentional given the cross-cutting nature of items 4 + 5. No further decomposition needed.
- **Ambiguity check:** "Lazy 401 refresh" (6.15.2) — clarified single-flight requirement under Risks. "Anlık hot-reload" (6.15.3) — clarified that cache invalidation is microseconds in single-replica; 5-min TTL is defensive only. "Multi-select on AdminManagement" (6.15.4) — clarified that the existing per-row promote/demote action is preserved alongside the new bulk toolbar.
