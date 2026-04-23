# Phase 6.11 — Superadmin Foundation Design Spec

**Status:** Design approved (user, 2026-04-23, brainstorm Q&A). Next: writing-plans.
**Branch:** `phase-6-superadmin-foundation` (will be created from `main` HEAD `04908be` — Phase 6.10 sealed + Round 1-3 hotfixes complete).
**Phase ref:** Phase 6 Item 11 — superadmin role hierarchy + permission gating + admin lifecycle management. Phase 6.12+ queued separately.

## Context

Phase 6.10 closed the admin panel rebuild with two roles in the system: `user` and `admin`. The current model is binary — every admin has full access to every tab and every action. This is fine for a single-admin setup but will break down as the admin team grows. Specifically:

- **No principle of least privilege.** Any admin can delete users, approve crypto payments, change feature flags, publish app releases. A compromised or careless admin account causes catastrophic damage.
- **No accountability hierarchy.** When something goes wrong, there's no "supervisor" admin to investigate or revoke access.
- **No staged onboarding.** Adding a new admin = giving them everything immediately. No way to start them with read-only access and gradually unlock destructive permissions.
- **Manual account creation only.** Admins are seeded by DB UPDATE; no UI to create or manage admin accounts.

Phase 6.11 introduces a `superadmin` role above `admin` that owns these gaps. The superadmin can grant fine-grained permissions to admins, approve permission requests, create/disable admin accounts, enforce 2FA, and review all admin activity in a dedicated dashboard.

## Scope

### In scope — Phase 6.11

**Auth model:**
- New `superadmin` role on `users.role` field (alongside existing `user` and `admin`)
- Dedicated `/api/auth/superadmin/login` endpoint (separate from `/api/auth/login`) — extra rate-limit, extra audit logging, extra strict validation
- Superadmin bootstrap via `SUPERADMIN_EMAILS` env var (idempotent on backend startup)
- Frontend "Sign In as Superadmin" button on the existing LoginScreen (separate form, separate endpoint, post-login renders the superadmin panel)

**Permission model (3-tier):**
- **Tier 1 — Tab gating:** 4 tabs default-locked for admins (Configuration, IP Whitelist, Updates, Role Change). Locked tabs render a placeholder page: "This page has been disabled by the superadmin by default. You need permission from the superadmin to be able to use the **{TabName}** tab" + a "Request Permission" button.
- **Tier 2 — Action gating:** 6 destructive actions default-locked for admins. Buttons render with a lock icon + disabled state. Click → opens a request modal.
- **Tier 3 — Free for admins:** Everything else (read-only operations + low-risk reversible destructive operations).

**Permission storage:**
- `permission_grants` table: active grants per (admin, permission_key) with optional expires_at and soft-revoke
- `permission_requests` table: history of requests (pending / approved / denied / cancelled) with admin's reason text + superadmin's optional review_note

**Permission request/approval flow:**
- Admin clicks locked tab or locked action → modal with mandatory "Why do you need access?" textarea (50–500 chars) → submit
- Superadmin sees pending requests in a dedicated "Permission Requests" sub-tab → Approve (with optional expires_at + note) / Deny (with optional note)
- Real-time SignalR notifications: `PermissionRequested` (to superadmins), `PermissionApproved` / `PermissionDenied` / `PermissionRevoked` (to specific admin)

**Admin lifecycle (superadmin tooling):**
- **Create Admin Account** — superadmin creates a new admin with email, initial password (manual or generated), permission template, and force-password-change policy
- **Reset Admin Password** — superadmin sets a new temporary password + force change on next login
- **Suspend / Restore Admin** — `is_active` flag; suspended admins can't log in, sessions invalidated, but data preserved (reversible alternative to delete)
- **Promote existing user** — alternative path: pick an existing `user` and promote to `admin` with template choice (kept alongside the create-new-account flow)

**Permission templates (4 presets):**
- **Default** — Tier 3 destructive only (Licenses Revoke/Activate, Devices Revoke, CrashReports Delete) + all read-only tabs
- **Trusted** — Default + Tier 2 actions enabled (Subscriptions Grant/Revoke, Users Delete/Ban, Payments Approve/Reject Crypto)
- **Read-Only** — All read-only tabs + zero destructive actions (every Tier 2/Tier 3 destructive locked)
- **Custom** — superadmin manually picks per-tab visibility + per-action permission + per-permission expires_at (granular config)

**2FA enforcement policy:**
- **Global toggle** (system-wide setting) — "Require 2FA for all admin accounts"
- **Per-account override** (per-admin setting) — superadmin can require 2FA on individual admins even if global is off
- Resolution: `requires_2fa = (global_enabled OR account_override) AND NOT user.totp_enabled`
- If `requires_2fa` is true at login, login response → frontend redirects to `/enable-2fa` setup before reaching dashboard

**Force password change policy (set at admin account creation or password reset):**
- 4 options:
  1. **On first login** (default — most secure, initial password is one-shot)
  2. **Within 7 days** (short grace period)
  3. **Within 30 days** (NIST-aligned corporate pattern)
  4. **Never** (admin discretion)
- DB stores `force_password_change` boolean + `force_password_change_by` deadline
- Login response includes `requires_password_change: true` + `deadline: <ISO>` when active
- Frontend redirects to `/change-password` view; admin must change password before reaching dashboard

**Superadmin-exclusive tabs:**
- **Permission Requests** (inbox + history)
- **Admin Action Log** (audit_log filtered view: rows where `actor_user.role = 'admin'`, with per-admin / action-type / date-range filters + KPIs)
- **Role Change UI** (basic skeleton in 6.11 — promote user→admin / demote admin→user; advanced flows like bulk role changes deferred to Phase 6.12)

**"My Permissions" page (admin self-service):**
- Admin sees their own grants: which tabs/actions are unlocked, when they expire, who granted them, when granted
- Reduces noise in superadmin's request inbox (admin can self-check before requesting)

**SignalR events (additions to AdminHub from Phase 6.10):**
- `PermissionRequested` (to superadmins group) — `{ adminEmail, permissionKey, reason, requestedAt }`
- `PermissionApproved` (to specific admin user) — `{ permissionKey, approvedBy, expiresAt? }`
- `PermissionDenied` (to specific admin user) — `{ permissionKey, deniedBy, reviewNote? }`
- `PermissionRevoked` (to specific admin user) — `{ permissionKey, revokedBy, reason? }`

**Backend authorization mechanism:**
- Existing `[Authorize(Roles = "admin")]` on controllers stays (admin + superadmin both pass — inheritance via `app.UseAuthorization()`)
- New `[RequiresPermission("permission_key")]` attribute filter:
  - User role = "superadmin" → always passes
  - User role = "admin" → checks `permission_grants` for active (non-revoked, non-expired) entry → passes if found, else returns 403 `{ "error": "permission_required", "permission": "<key>" }`
  - Frontend reads the 403 body to know which permission to request

### Out of scope — deferred

**Phase 6.12** (queued separately, brainstorm later):
- 15 deferred Low audit findings from Phase 6.10 spec
- Role Change UI fleshed out (bulk operations, audit-traced demote-to-user flow)
- TOTP backup codes, blockchain explorer link, online/offline device indicator, PII erasure UI
- audit_log retention dashboard
- SignalR enhancements (presence, typing indicators, multi-admin awareness)
- **Active Admin Sessions Monitor** (live "who's logged in right now" with force-logout) — deferred from Phase 6.11 scope discussion
- **2FA Enforcement Policy global UI** is in 6.11 (toggle + override); the deeper compliance reporting features are 6.12

**Phase 6.13** (queued separately):
- Hotfix debt sweep: error-swallow cleanup (descriptive errors + toasts), backend HTTP method audit, frontend `any` cast cleanup, nginx sites-enabled drift cleanup, brute-force lockout UX, PWA real icons, Vitest broader coverage, CI/CD deploy pipeline

**Phase 6.14** (queued separately):
- React Native admin mobile app (full feature parity with web, leveraging the role/permission system from 6.11)

**6.11 explicitly NOT included:**
- Email notifications (permission requests / approvals via email)
- Audit log CSV export
- API rate limit configuration UI
- Per-permission temporary "trial mode" (admin gets X uses then auto-revokes)

## Design decisions

### D1 — 3-tier permission model

| Tier | Mechanism | Members | UX |
|---|---|---|---|
| **Tier 1** | Tab-level gating | Configuration, IP Whitelist, Updates, Role Change | Tab visible in nav; click → locked-page placeholder + Request button |
| **Tier 2** | Action-level gating | Users.Delete, Users.Ban, Subscriptions.Grant, Subscriptions.Revoke, Payments.ApproveCrypto, Payments.RejectCrypto | Tab open; specific buttons rendered with lock icon + disabled; click → request modal |
| **Tier 3** | No gating | Licenses.Revoke, Licenses.Activate, Devices.Revoke, CrashReports.Delete, all read-only ops | Normal admin behavior |

Permission keys: `tab:configuration`, `tab:ipwhitelist`, `tab:updates`, `tab:rolechange`, `action:users.delete`, `action:users.ban`, `action:subscriptions.grant`, `action:subscriptions.revoke`, `action:payments.approveCrypto`, `action:payments.rejectCrypto`. Namespace allows future expansion.

### D2 — Auth model (separate superadmin endpoint)

`/api/auth/superadmin/login`:
- Accepts same payload (email, password, optional totpCode)
- Validates user exists AND `role = 'superadmin'` (else 403 — never reveals whether the email exists in the user table)
- Stricter rate limit: 3 failed attempts → 60-minute lockout (vs admin login 5 attempts → 30 min)
- Audit-logged with action `'SuperadminLoginAttempt'` regardless of success/failure
- Response shape identical to `/api/auth/login` (JWT + refresh token)
- JWT carries `role: 'superadmin'` claim

Frontend `LoginScreen`:
- Default form posts to `/api/auth/login` (admin path)
- "Sign In as Superadmin" link below the submit button → toggles form into superadmin mode (visual indicator: cyan→purple gradient on the submit button) → on submit, posts to `/api/auth/superadmin/login`
- Post-login: frontend reads JWT role claim and renders superadmin panel (with extra tabs) or admin panel accordingly

### D3 — Superadmin bootstrap

Backend startup hook reads `SUPERADMIN_EMAILS` env var (comma-separated for multi-superadmin support) and runs an idempotent SQL upsert:
```sql
UPDATE users SET role = 'superadmin' WHERE email IN (...)
```
Only promotes existing users — does not create new accounts (chicken-and-egg avoided: superadmin email must register first via normal `/api/auth/register`, then env var promotes them on next backend start).

Initial value: `SUPERADMIN_EMAILS=ozgurdeniz807@gmail.com`.

Demotion: if an email is removed from the env var, backend does NOT auto-demote (that would be too dangerous on accidental config edit). Demotion is a manual operation through the Role Change UI.

### D4 — Storage schema

```sql
-- Active permission grants
CREATE TABLE permission_grants (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    admin_user_id       UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    permission_key      VARCHAR(100) NOT NULL,
    granted_by          UUID NOT NULL REFERENCES users(id),
    granted_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at          TIMESTAMPTZ NULL,
    revoked_at          TIMESTAMPTZ NULL,
    revoked_by          UUID NULL REFERENCES users(id),
    revoke_reason       TEXT NULL,
    source_request_id   UUID NULL REFERENCES permission_requests(id)
);
-- One active grant per (admin, permission)
CREATE UNIQUE INDEX uq_permission_grants_active
  ON permission_grants(admin_user_id, permission_key)
  WHERE revoked_at IS NULL;

-- Permission request inbox + history
CREATE TABLE permission_requests (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    admin_user_id   UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    permission_key  VARCHAR(100) NOT NULL,
    reason          TEXT NOT NULL,                 -- min 50, max 500 chars
    requested_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    status          VARCHAR(20) NOT NULL DEFAULT 'pending', -- pending|approved|denied|cancelled
    reviewed_by     UUID NULL REFERENCES users(id),
    reviewed_at     TIMESTAMPTZ NULL,
    review_note     TEXT NULL
);
CREATE INDEX ix_permission_requests_status_admin
  ON permission_requests(status, admin_user_id);
```

User table additions:
```sql
ALTER TABLE users ADD COLUMN is_active BOOLEAN NOT NULL DEFAULT TRUE;
ALTER TABLE users ADD COLUMN force_password_change BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE users ADD COLUMN force_password_change_by TIMESTAMPTZ NULL;
ALTER TABLE users ADD COLUMN password_changed_at TIMESTAMPTZ NULL;
ALTER TABLE users ADD COLUMN created_by_user_id UUID NULL REFERENCES users(id);
ALTER TABLE users ADD COLUMN created_via VARCHAR(30) NOT NULL DEFAULT 'signup'; -- signup|admin_promote|superadmin_create
ALTER TABLE users ADD COLUMN require_2fa BOOLEAN NOT NULL DEFAULT FALSE;
```

System settings (assumed there is or there will be a `system_settings` key-value table; if not, this spec adds one):
```sql
CREATE TABLE IF NOT EXISTS system_settings (
    key     VARCHAR(100) PRIMARY KEY,
    value   TEXT NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by UUID NULL REFERENCES users(id)
);
-- Seed:
INSERT INTO system_settings (key, value) VALUES ('require_2fa_for_all_admins', 'false');
```

### D5 — Backend `[RequiresPermission]` attribute

C# implementation sketch:
```csharp
public sealed class RequiresPermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    public string Permission { get; }
    public RequiresPermissionAttribute(string permission) { Permission = permission; }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        var role = user.FindFirst(ClaimTypes.Role)?.Value;
        if (role == "superadmin") return; // always passes

        if (role != "admin")
        {
            context.Result = new ForbidResult();
            return;
        }

        var userId = Guid.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var hasGrant = await db.PermissionGrants
            .AnyAsync(g => g.AdminUserId == userId
                        && g.PermissionKey == Permission
                        && g.RevokedAt == null
                        && (g.ExpiresAt == null || g.ExpiresAt > DateTime.UtcNow));
        if (!hasGrant)
        {
            context.HttpContext.Response.StatusCode = 403;
            await context.HttpContext.Response.WriteAsJsonAsync(new {
                error = "permission_required",
                permission = Permission
            });
            context.Result = new EmptyResult();
        }
    }
}
```

Applied as: `[Authorize(Roles = "admin")] [RequiresPermission("action:users.delete")] public IActionResult DeleteUser(...)`. The `[Authorize]` filter runs first (basic auth check), then `[RequiresPermission]` runs (permission check).

### D6 — Permission templates

Backend: hardcoded constants (templates rarely change; if needed, can be moved to system_settings later).
```csharp
public static class PermissionTemplates
{
    public static readonly Dictionary<string, string[]> Templates = new()
    {
        ["Default"] = new[] {
            // No tab unlocks (Tier 1 stays locked)
            // No Tier 2 action unlocks
            // (Tier 3 actions are open by default — no permission_grant needed)
        },
        ["Trusted"] = new[] {
            "action:users.delete",
            "action:users.ban",
            "action:subscriptions.grant",
            "action:subscriptions.revoke",
            "action:payments.approveCrypto",
            "action:payments.rejectCrypto",
        },
        ["ReadOnly"] = new[] {
            // Tier 3 destructive actions BLOCKED for read-only template
            // Implementation: read-only template generates "denial grants" for tier 3 actions
            // (or simpler: Tier 3 stays open by default, and "ReadOnly" admins are expected
            //  to follow policy — frontend hides destructive buttons for ReadOnly template label)
            // RECOMMENDATION: store template label on user (users.permission_template), and
            // backend [RequiresPermission] also denies for ReadOnly template even on Tier 3 ops.
            // SIMPLER: use is_readonly flag on user; frontend + backend both check.
        },
    };
}
```

The "ReadOnly" template is the most complex because it needs to *block* default-open actions. Two implementation paths:
- **(A) is_readonly flag on users** — simplest, single boolean, works as a global override
- **(B) denial-grants in permission_grants** — extend schema with a `is_denial` column, generate denial rows for ReadOnly template

**Recommended path (A):** Add `users.is_readonly` boolean. Backend `[RequiresPermission]` and the (currently un-attributed) Tier 3 action endpoints all consult this flag. UI hides destructive buttons when `currentUser.is_readonly = true`.

Custom template:
- Superadmin manually selects which permissions to grant in the create/edit modal
- Each permission can have an individual expires_at (or a single "Apply same expiry to all" shortcut)
- Saved as multiple `permission_grants` rows directly (no template label stored — just the resulting grants)

### D7 — Force password change flow

Login response shape addition:
```json
{
  "accessToken": "...",
  "refreshToken": "...",
  "user": { ... },
  "requiresPasswordChange": true,
  "passwordChangeDeadline": "2026-04-30T12:00:00Z"
}
```

Frontend: if `requiresPasswordChange === true`, store the JWT but redirect to `/change-password` view. The change-password endpoint clears the flag.

The deadline is enforced server-side: every authenticated request includes a middleware check — if `force_password_change=true` AND `force_password_change_by < now()`, all endpoints (except the change-password endpoint itself + `/api/auth/logout`) return 403 with `{ "error": "password_change_required" }`. Frontend catches this and redirects.

### D8 — 2FA enforcement policy

Login flow modification:
1. Validate credentials (email + password) — same as today
2. If user.role in ('admin', 'superadmin'):
   - `requires_2fa = (system_settings.require_2fa_for_all_admins OR user.require_2fa) AND NOT user.totp_enabled`
   - If `requires_2fa`: return `{ requiresTwoFactorSetup: true, accessToken (limited scope: only enable-2fa endpoint allowed), ... }`
   - Frontend redirects to `/enable-2fa`
3. Else: continue normal flow (TOTP check if user has 2FA enabled)

The "limited scope" access token is a JWT with `scope: '2fa-setup-only'` claim. Backend middleware checks this claim and only allows `/api/auth/enable-2fa` and `/api/auth/logout` endpoints.

Configuration UI (in superadmin's Configuration tab or new "Security Policy" sub-tab):
- Toggle: "Require 2FA for all admin accounts" → updates system_settings
- Below: per-account override list (admin email + toggle) → updates users.require_2fa

### D9 — Admin Action Log (superadmin tab)

Reuses existing `audit_log` table from Phase 6.8 (no schema change needed).

Backend new endpoint:
```
GET /api/superadmin/admin-actions
Query params: page, pageSize, actorEmail (filter), action (filter), targetType, dateFrom, dateTo
Returns: { total, page, pageSize, pages, items: AuditLogEntry[] }
```

Filter: `WHERE actor_user.role = 'admin'` (excludes superadmin's own actions; if superadmin wants to see their own, they use the regular AuditLog tab).

KPIs computed server-side:
- Total admin actions (all-time)
- Last 24h count
- Last 7d count
- Top 5 admins by action count
- Top 5 action types

Frontend new view `src/views/AdminActionLogPage.tsx` (superadmin-only; not visible to admins).

Real-time: existing `AuditLogEntry` SignalR event (Phase 6.10) → live append to the table.

### D10 — UX for locked tabs and locked actions

**Locked tab page** (`src/components/LockedTabPlaceholder.tsx`):
```
[Lock icon, large, centered]

This page has been disabled by the superadmin by default.
You need permission from the superadmin to be able to use the {TabName} tab.

[Request Permission button]
[If pending request exists: "Pending request from {requestedAt}, awaiting review"]
[If denied recently: "Last request denied: {reviewNote || 'no reason given'}"]
```

**Locked action button** (existing destructive buttons get a `disabled` state with lock icon):
```tsx
{hasPermission ? (
  <button onClick={doDelete} className="btn-danger btn-action">Delete</button>
) : (
  <button onClick={openRequestModal} className="btn-action opacity-40 cursor-help" title="This action requires superadmin permission. Click to request.">
    🔒 Delete
  </button>
)}
```

**Request modal** (`src/components/PermissionRequestDialog.tsx`):
- Title: "Request access to {permissionLabel}"
- Body: "Why do you need this permission?" + textarea (50-500 chars, char count visible)
- Footer: Cancel / Submit Request
- On submit: POST `/api/admin/permission-requests` → toast "Request sent to superadmin" + close modal
- If duplicate pending request exists: show banner "You already have a pending request for this permission."

**Permission Requests inbox** (superadmin's new tab `src/views/PermissionRequestsPage.tsx`):
- Table: Admin email | Permission requested | Reason (truncated, click to expand) | Requested at | Status | Actions
- Status filter (pending / approved / denied / cancelled / all), default = pending
- Per-row actions: **Approve** (opens dialog: optional expires_at picker + optional review_note → submit) / **Deny** (opens dialog: optional review_note → submit)
- Real-time: SignalR `PermissionRequested` → table prepends new row + tab badge increments
- Bulk actions: select multiple pending requests → "Approve Selected" / "Deny Selected" with same dialogs

### D11 — Admin Account Creation flow

New superadmin tab section "Admin Management" (or extend existing Users tab with "Admins" sub-section):
- List of current admin accounts: email | created_at | created_by | last_login | is_active | template | actions
- Per-row actions: Edit Permissions / Reset Password / Suspend (or Restore) / Delete
- Top button: **+ Create Admin Account**

Create modal:
1. **Email** input (validated format + uniqueness)
2. **Initial password** input + "Generate Strong" button (16-char alphanum + symbol) + Show/Hide toggle + Copy button
3. **Force password change** dropdown (4 options, default "On first login")
4. **Permission template** dropdown (Default / Trusted / Read-Only / Custom)
   - If Custom: expand a checkbox tree (per-tab + per-action) + per-permission expires_at pickers
5. **Initial 2FA requirement** checkbox: "Require 2FA on next login" (default off — but if global 2FA-required is on, this is forced on and disabled)
6. **Create** button → POST `/api/superadmin/admins/create` with all fields

Backend creates the user, hashes the password, sets the flags, generates the permission_grants based on the template, and returns the new user object. Audit-logged with action `'CreateAdminAccount'`.

Promote-existing-user flow (alongside create-new):
- Users tab → User row with `role='user'` → action **"Promote to Admin"**
- Modal: choose template + force password change policy (no need to set new password — user keeps their existing password unless force change is selected) + initial 2FA requirement
- Backend updates `users.role = 'admin'` + sets template-based permission_grants + sets force flags + audits

### D12 — Admin suspend/restore + delete

**Suspend:**
- `users.is_active = false` + invalidate all sessions (revoke refresh tokens, blacklist current access token via short TTL or token revocation table)
- Login attempt for suspended user → 403 `{ "error": "account_suspended" }`
- Existing API requests with valid token → 401 (token invalidated)

**Restore:**
- `users.is_active = true`
- User can log in again (force_password_change re-applied if it was set)

**Delete:**
- Hard delete with cascade (existing behavior)
- Tier 2 action — requires `action:users.delete` permission
- After 6.11: deletion of an admin also deletes their permission_grants + permission_requests via FK CASCADE

### D13 — "My Permissions" page (admin self-service)

New admin-side view `src/views/MyPermissionsPage.tsx` (visible to all admins, not in nav by default — accessed via user dropdown menu):
- Summary card: "You are an admin. You have access to {N} of {M} restricted permissions."
- Table of grants: Permission | Granted by | Granted at | Expires at | Source (request / template / direct grant)
- Table of pending requests: Permission | Requested at | Reason | Cancel button
- Table of recent denials/revocations: Permission | Reviewed by | Note

## Architecture — Wave breakdown

The Phase 6.11 implementation will follow a wave breakdown similar to Phase 6.10. Here is a tentative outline (the detailed plan is created in the next stage by writing-plans):

**Wave 1 — DB schema + auth model:**
- Migration: permission_grants + permission_requests + users field additions + system_settings
- Backend: `[RequiresPermission]` attribute + filter + `superadmin` role enum value
- Backend: `/api/auth/superadmin/login` endpoint + bootstrap from SUPERADMIN_EMAILS
- Backend tests: attribute behavior + bootstrap idempotency + login auth

**Wave 2 — Permission grants + requests API + Tier 1/2 application:**
- Backend: permission grants CRUD endpoints + permission requests CRUD endpoints
- Backend: apply `[RequiresPermission]` to all 4 Tier 1 tabs' mutation endpoints + 6 Tier 2 actions
- Backend: SignalR new event types
- Backend tests: grant lifecycle + request flow + permission filter end-to-end

**Wave 3 — Frontend role-aware shell + locked-tab + locked-action UX:**
- Frontend: post-login role detection + render superadmin or admin panel
- Frontend: LoginScreen "Sign In as Superadmin" button + form mode toggle
- Frontend: LockedTabPlaceholder component + locked button rendering
- Frontend: PermissionRequestDialog component
- Frontend: useSignalR additions for permission events
- Frontend tests: locked tab render + request modal interaction

**Wave 4 — Superadmin tabs (Permission Requests + Admin Action Log + Admin Management):**
- Frontend: PermissionRequestsPage with approve/deny dialogs
- Frontend: AdminActionLogPage with filters + KPIs
- Frontend: AdminManagementPage (list + create + edit + suspend/restore + delete)
- Backend: superadmin endpoints for create-admin / reset-password / suspend / restore
- Backend tests: admin lifecycle endpoints

**Wave 5 — Templates + force password change + 2FA enforcement:**
- Backend: permission template logic (Default / Trusted / ReadOnly / Custom)
- Backend: force password change middleware + login response field
- Backend: 2FA enforcement middleware + system_settings.require_2fa_for_all_admins
- Frontend: change-password view + 2FA setup view (already exists, just route logic)
- Frontend: Configuration tab additions for global 2FA toggle + per-account override list
- Backend tests: template grant generation + force change flow + 2FA enforcement resolution

**Wave 6 — "My Permissions" page + Role Change UI skeleton + final polish + deploy + ceremonial:**
- Frontend: MyPermissionsPage
- Frontend: Role Change UI skeleton (promote/demote single user — flesh-out in Phase 6.12)
- Frontend tests: My Permissions render + role change actions
- Final smoke + memory file + ceremonial merge to main + push (user-gated)

## Testing strategy

- **Backend** (xUnit): ~25-35 tests across Wave 1-5 (attribute behavior, bootstrap, lifecycle endpoints, template generation, force change middleware, 2FA enforcement, permission requests flow)
- **Frontend** (Vitest + RTL): ~10-20 tests for new components (LockedTabPlaceholder, PermissionRequestDialog, AdminManagement create modal, MyPermissions page)
- **Skipped:** End-to-end Playwright integration (Phase 6.13+); per-page integration tests
- **Target:** ~2392 → ~2440-2470 (+45-80 net new tests)

## Deployment flow

Two-deploy pattern (similar to Phase 6.10):

**Mid deploy (after Wave 2):** backend with new schema + permission attribute + new endpoints. Smoke: superadmin login + create permission grant + apply attribute on a test endpoint.

**Final deploy (after Wave 5):** admin-panel frontend rebuild. Smoke: superadmin panel loads with all extra tabs; admin-only login locks Tier 1 tabs; permission request flow end-to-end.

DB migrations run via standard EF Core migration in mid-deploy.

## Open questions / known risks

- **Schema migration on a live DB with existing admin accounts:** All existing admins get `is_active=true`, `force_password_change=false`, `created_via='signup'`, `require_2fa=false` — backward compatible. Existing admins will NOT have any permission_grants until superadmin grants them (or until backend startup auto-creates "Trusted" template grants for already-admin users to avoid sudden lockout). **DECISION:** Backend startup runs a one-time "grandfather" migration that creates "Trusted" template permission_grants for every existing `role='admin'` user (idempotent — guarded by checking if any grants exist for the user). This prevents the "deploy → all admins suddenly can't do anything" disaster.
- **Superadmin self-promotion attack:** If a superadmin's account is compromised, attacker can create unlimited admin accounts + grant all permissions. Mitigation: superadmin login requires 2FA mandatory (override the per-account/global policy for superadmins specifically). Spec adds this: "Superadmin accounts MUST have 2FA enabled. Bootstrap doesn't enforce this on the env-promoted superadmin, but the first login attempt without 2FA enabled redirects to /enable-2fa setup."
- **Permission request spam:** Admin could send hundreds of requests. Rate limit: 1 pending request per (admin, permission) at a time (DB unique constraint or app-level check). Plus per-admin rate limit: max 20 requests / 24h / admin.
- **Suspend invalidating active sessions:** Requires either (a) short JWT TTL (5-10 min) + refresh token revocation, or (b) a token blacklist table. Phase 6.10 uses long-lived JWTs; switching to short-TTL is a breaking change for the SignalR hub (requires re-auth periodically). **DECISION:** Add a token blacklist table `revoked_tokens(jti, revoked_at)` checked by JWT middleware. Suspend writes to this table for all of the user's active tokens.
- **2FA enforcement for already-logged-in admins:** When superadmin enables global 2FA, existing logged-in admins still have valid sessions. **DECISION:** Their next login (after current token expires) will redirect to /enable-2fa. Optionally, superadmin can "Force re-auth all admins" button (revokes all admin tokens — they re-login and hit the 2FA gate). This is opt-in (Phase 6.12 polish).

## Decision log

| Decision | Chosen | Rejected | Why |
|---|---|---|---|
| Scope decomposition | Phase 6.11 = Superadmin foundation only; 6.12 = features; 6.13 = debt; 6.14 = RN app | Single mega-phase | Foundational change — every later feature builds on it |
| Permission model granularity | 3-tier (tab + action + free) | Pure tab-level OR pure action-level | Mixed model fits real risk profile (high-risk actions in low-risk tabs) |
| Auth model | Separate `/api/auth/superadmin/login` endpoint | Single endpoint with role claim | Extra defense-in-depth: stricter rate limit, isolated audit, never reveals account existence |
| Bootstrap | Env var `SUPERADMIN_EMAILS` idempotent on startup | Manual DB seed / first-user-superadmin / role change UI elevate | Reproducible, source-controlled, no chicken-and-egg |
| ReadOnly template enforcement | `users.is_readonly` boolean checked by backend | Denial-grants in permission_grants | Simpler — single column, clear semantics |
| Storage | New `permission_grants` + `permission_requests` tables | Stuff into users.permissions JSON column | Indexable, auditable, multi-grant per admin |
| Locked-tab UX | Placeholder page + Request button in-place | 404-style hidden tab | User intent: admin sees the tab exists, knows what they're missing, can request |
| Bonus features in 6.11 | Admin Account Creation, Password Reset, Suspend/Restore, Templates (incl. Custom), My Permissions, 2FA Enforcement (global + per-account) | Active Sessions Monitor, Email notifications | Scope balance: lifecycle + policy in 6.11; observability + comms in 6.12+ |

## Non-goals

- Email/Slack/SMS notifications for permission requests (in-app SignalR + UI only)
- Audit log CSV export
- API rate limit configuration UI
- Per-permission "trial mode" with auto-expire after N uses
- Multi-tenant superadmin (single org assumption — superadmin is global)
- Role hierarchy beyond 3 levels (no "owner" above superadmin in this phase)
- Custom permission key creation by superadmin (permission keys are hardcoded enum)

## Success criteria

Phase 6.11 is DONE when:

- `superadmin` role exists in `users.role` enum; bootstrap from env var works idempotently
- `/api/auth/superadmin/login` endpoint live with stricter rate limit + audit
- LoginScreen has "Sign In as Superadmin" button; post-login renders correct panel based on role
- 4 Tier 1 tabs render LockedTabPlaceholder for unprivileged admins; all 4 tabs' mutation endpoints reject admin requests with `permission_required` 403
- 6 Tier 2 action buttons render with lock icon for unprivileged admins; clicking opens PermissionRequestDialog; submitting creates a permission_request row
- Superadmin's Permission Requests tab shows pending requests; Approve creates permission_grant + emits `PermissionApproved`; Deny updates request status + emits `PermissionDenied`
- Admin Action Log tab shows audit_log filtered by `actor_user.role='admin'`, with filters + KPIs + live SignalR appends
- Admin Management tab supports: create new admin (email + password + force change + template + 2FA), promote existing user, reset password, suspend/restore, delete
- 4 permission templates work: Default (no extra grants), Trusted (Tier 2 unlocked), Read-Only (is_readonly enforced), Custom (per-permission picker with expires_at)
- Force password change flow: new admin's first login redirects to /change-password; deadline-based enforcement on subsequent logins
- 2FA enforcement: global toggle + per-account override; resolution at login redirects to /enable-2fa when needed
- Superadmin accounts MUST have 2FA enabled (mandatory override)
- "My Permissions" page lets admin see their grants + pending requests + recent denials
- Role Change UI skeleton: superadmin can promote a user to admin or demote an admin to user (single-user flow; bulk operations are 6.12)
- Existing admins are grandfathered with "Trusted" template grants on first deploy (no sudden lockout)
- Suspend invalidates active sessions via token blacklist
- Backend ~25-35 new tests pass; frontend ~10-20 new tests pass; total ~2440-2470, 0 failed, 0 skipped
- Memory file written + MEMORY.md pointer updated
- Branch merged to main via `--no-ff` (ceremonial) + pushed to origin (user-gated)

**Spec end.** Writing-plans skill invoked next.
