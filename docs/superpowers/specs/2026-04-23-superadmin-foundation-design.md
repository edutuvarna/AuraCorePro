# Phase 6.11 — Superadmin Foundation Design Spec

**Status:** Design approved (user, 2026-04-23, brainstorm Q&A — including subdomain-B + email notifications + CSV export + rate-limit UI additions). Next: writing-plans.
**Branch:** `phase-6-superadmin-foundation` (will be created from `main` HEAD `04908be` — Phase 6.10 sealed + Round 1-3 hotfixes complete).
**Phase ref:** Phase 6 Item 11 — superadmin role hierarchy + permission gating + admin lifecycle management + dedicated superadmin subdomain + email notifications + audit log CSV export + API rate limit configuration UI. Phase 6.12+ queued separately.

## Context

Phase 6.10 closed the admin panel rebuild with two roles: `user` and `admin`. The current model is binary — every admin has full access to every tab and every action. This is fine for a single-admin setup but will break down as the admin team grows. Specifically:

- **No principle of least privilege.** Any admin can delete users, approve crypto payments, change feature flags, publish app releases. A compromised or careless admin account causes catastrophic damage.
- **No accountability hierarchy.** When something goes wrong, there's no "supervisor" admin to investigate or revoke access.
- **No staged onboarding.** Adding a new admin = giving them everything immediately. No way to start with read-only access and gradually unlock destructive permissions.
- **Manual account creation only.** Admins are seeded by DB UPDATE; no UI to create or manage admin accounts.
- **No isolation between admin and superadmin surface area.** Even if a superadmin role existed, it would live on the same subdomain + bundle as admin — any bundle exfiltration attack reveals superadmin code paths.
- **No observability of email deliverability.** Current `PasswordResetController.cs:146-168` uses inline fire-and-forget `new HttpClient()` POST to Resend — no status check, no logging, no retry, no abstraction.
- **No runtime control of API rate limits.** Rate limit policies are hardcoded in `Program.cs`; changing them requires code deploy.

Phase 6.11 introduces a `superadmin` role above `admin`, isolates it on a dedicated `superadmin.auracore.pro` subdomain (defense-in-depth), adds a full permission grant/request/approval system, admin lifecycle management, 4 permission templates (including Custom with per-permission expiry), hybrid 2FA enforcement, transactional email notifications via a proper `IEmailService` abstraction around Resend, audit log CSV export, and runtime-editable API rate limit policies.

## Scope

### In scope — Phase 6.11

**Auth model + subdomain isolation:**
- New `superadmin` role on `users.role` field (alongside existing `user` and `admin`).
- Dedicated `/api/auth/superadmin/login` endpoint (separate from `/api/auth/login`) — stricter rate limit, mandatory audit logging, never reveals whether the email exists.
- **Dedicated `superadmin.auracore.pro` subdomain** — separate nginx server block, separate Let's Encrypt cert, optional IP allowlist hook for future hardening, separate frontend bundle.
- Superadmin bootstrap via `SUPERADMIN_EMAILS` env var (idempotent on backend startup); initial value `ozgurdeniz807@gmail.com`.
- Superadmin accounts MUST have 2FA enabled (mandatory override on first login — bootstrap-promoted superadmin forced to `/enable-2fa` before reaching dashboard).
- Frontend "Sign In as Superadmin" button on `admin.auracore.pro` LoginScreen → redirects to `https://superadmin.auracore.pro/` (so the superadmin login form lives only on the superadmin subdomain — cross-link only, no superadmin code in the admin bundle).

**Permission model (3-tier):**
- **Tier 1 — Tab gating:** 4 tabs default-locked for admins (Configuration, IP Whitelist, Updates, Role Change). Locked tabs render a placeholder page: `"This page has been disabled by the superadmin by default. You need permission from the superadmin to be able to use the {TabName} tab."` + `"Request Permission"` button.
- **Tier 2 — Action gating:** 6 destructive actions default-locked for admins. Buttons render with a lock icon + disabled state. Click → opens a request modal with mandatory reason text.
- **Tier 3 — Free for admins:** Everything else (read-only operations + low-risk reversible destructive operations).

**Permission storage:**
- `permission_grants` table: active grants per (admin, permission_key) with optional expires_at and soft-revoke.
- `permission_requests` table: history of requests (pending / approved / denied / cancelled) with admin's reason text + superadmin's optional review_note.

**Permission request/approval flow:**
- Admin clicks locked tab or locked action → modal with mandatory "Why do you need access?" textarea (50-500 chars) → submit.
- Superadmin sees pending requests in a dedicated "Permission Requests" sub-tab → Approve (with optional expires_at + note) / Deny (with optional note).
- Real-time SignalR notifications: `PermissionRequested` (to superadmins), `PermissionApproved` / `PermissionDenied` / `PermissionRevoked` (to specific admin).
- Transactional email notifications: `permission.requested` → superadmin, `permission.approved` / `permission.denied` → admin.

**Admin lifecycle (superadmin tooling):**
- **Create Admin Account** — superadmin creates a new admin with email, optional initial password (manual or generated), permission template, force-password-change policy, and **Send invitation email** toggle (default ON). Invitation email has a one-time setup token link (7-day TTL) that lets the admin set their own password directly (no password is ever emailed).
- **Reset Admin Password** — superadmin triggers a reset; admin receives reset link email; force_password_change applies on next login.
- **Suspend / Restore Admin** — `is_active` flag; suspended admins can't log in, active sessions invalidated via `revoked_tokens` table, but data preserved (reversible alternative to delete).
- **Promote existing user** — alternative path: pick an existing `user` and promote to `admin` with template choice (kept alongside the create-new-account flow; no initial password needed — user keeps their existing password).

**Permission templates (4 presets):**
- **Default** — Tier 3 destructive only + all read-only tabs; no Tier 1 / Tier 2 grants.
- **Trusted** — Default + all Tier 2 actions unlocked (Subscriptions Grant/Revoke, Users Delete/Ban, Payments Approve/Reject Crypto).
- **Read-Only** — `users.is_readonly = true` flag; all destructive actions blocked server-side (both Tier 2 and Tier 3); frontend hides destructive buttons.
- **Custom** — superadmin manually picks per-tab visibility + per-action permission + per-permission `expires_at` (granular config saved as multiple `permission_grants` rows).

**Force password change policy (set at admin account creation or password reset):**
1. **On first login** (default — most secure).
2. **Within 7 days** (short grace period).
3. **Within 30 days** (NIST-aligned corporate pattern).
4. **Never** (admin discretion).

**2FA enforcement policy:**
- **Global toggle** (system_settings) — "Require 2FA for all admin accounts".
- **Per-account override** (users.require_2fa) — superadmin can require 2FA on individual admins even if global is off.
- Resolution: `requires_2fa = (global_enabled OR account_override OR role='superadmin') AND NOT user.totp_enabled`.
- If `requires_2fa` is true at login, response returns a limited-scope JWT (claim: `scope: '2fa-setup-only'`) and frontend redirects to `/enable-2fa`. Backend middleware enforces scope — only `/api/auth/enable-2fa` + `/api/auth/logout` endpoints accept a scope-limited token.

**Superadmin-exclusive tabs (visible only on `superadmin.auracore.pro`):**
- **Permission Requests** — inbox + history, real-time updates, approve/deny dialogs with optional expiry + note, bulk approve/deny.
- **Admin Action Log** — audit_log filtered view: rows where `actor_user.role = 'admin'` with per-admin / action-type / date-range filters + KPIs + live SignalR appends + **CSV export**.
- **Admin Management** — list + create + edit permissions + reset password + suspend/restore + delete admin accounts.
- **Role Change UI** — basic skeleton: promote user→admin / demote admin→user (advanced flows like bulk role changes deferred to Phase 6.12).
- **Security Policy** — global 2FA toggle + per-account override list + force-password-change defaults.
- **API Rate Limits** — read + edit per-endpoint-group rate limit policies (auth.login, auth.register, admin.*, signalr.*, etc.) stored in `system_settings` and hot-reloaded into the ASP.NET Core RateLimiter middleware.

**"My Permissions" page (admin self-service on `admin.auracore.pro`):**
- Admin sees their own grants: which tabs/actions are unlocked, when they expire, who granted them, when granted.
- Table of pending requests (with cancel button) and recent denials/revocations (with note).
- Accessible via user dropdown menu, not in main nav.

**Email notifications (transactional, via Resend):**
- Refactor `PasswordResetController.cs:146-168` inline HTTPS call into `IEmailService` abstraction + `ResendEmailService` implementation using `IHttpClientFactory`, structured logging, typed response DTO.
- 6 notification types in 6.11: `admin.invitation`, `password.reset`, `permission.requested` (to superadmin), `permission.approved`, `permission.denied`, `admin.created-by-superadmin-without-email` (fallback when invite-email toggle is off — logs that superadmin must manually share the initial password).
- All emails use `noreply@auracore.pro` from-address and shared HTML template (glass + terminal aesthetic matching the admin panel).
- Add `include:_spf.resend.com` to auracore.pro SPF TXT record (deliverability — small DNS edit, part of Wave 6 deploy steps).

**Audit Log CSV export:**
- New endpoint: `GET /api/admin/audit-log/export.csv?dateFrom=...&dateTo=...&actorEmail=...&action=...`.
- Respects the same filters as the regular audit log list endpoint (filter-aware export).
- Streams CSV content (proper Content-Disposition + Content-Type: text/csv).
- Column set: actor_email, actor_id, action, target_type, target_id, ip_address, created_at_utc.
- Authorization: requires `tab:auditlog` (which is open by default since AuditLog is Tier 3).
- Button on both AuditLog (admin panel) and AdminActionLog (superadmin panel) tabs.

**API rate limit configuration UI:**
- New superadmin tab section (under Configuration or as sibling "API Rate Limits" sub-tab).
- Per-endpoint-group config rows: `auth.login` (5 req / 30 min), `auth.register` (3 req / 60 min), `admin.*` (1000 req / 1 hour), `signalr.connect` (10 req / 1 min), `api.public` (100 req / 1 min).
- Stored in `system_settings` as JSON blob (`rate_limit_policies`).
- Backend: `RateLimitConfigService` reads from DB on startup, caches in memory; on config update, invalidates cache and re-configures the ASP.NET Core `RateLimiter` policies atomically.
- Edit dialog: limit + window picker + "Apply" button (applies live; no restart needed).

**Backend authorization mechanism:**
- Existing `[Authorize(Roles = "admin")]` on controllers stays (admin + superadmin both pass via role inheritance).
- New `[RequiresPermission("permission_key")]` attribute filter — runs after `[Authorize]`. Superadmin always passes; admin role checks `permission_grants` for active non-expired non-revoked entry; no match returns 403 with `{ "error": "permission_required", "permission": "<key>" }`.
- Frontend reads the 403 body to know which permission to request.

**SignalR events (additions to AdminHub from Phase 6.10):**
- `PermissionRequested` (to superadmins group) — `{ adminEmail, permissionKey, reason, requestedAt }`.
- `PermissionApproved` (to specific admin user) — `{ permissionKey, approvedBy, expiresAt? }`.
- `PermissionDenied` (to specific admin user) — `{ permissionKey, deniedBy, reviewNote? }`.
- `PermissionRevoked` (to specific admin user) — `{ permissionKey, revokedBy, reason? }`.

**Grandfather migration:**
- On first deploy with Phase 6.11 code, backend startup runs a one-time idempotent migration: for every existing `role='admin'` user that has zero `permission_grants` rows, create "Trusted" template grants. This prevents existing admins from being locked out immediately after deploy. User has a single admin account (`admin@auracore.pro`) — Trusted template is correct.

### Out of scope — deferred

**Phase 6.12** (queued separately, brainstorm later):
- 15 deferred Low audit findings from Phase 6.10 spec.
- Role Change UI fleshed out (bulk operations, audit-traced demote-to-user flow, multi-admin role transition UX).
- TOTP backup codes, blockchain explorer link, online/offline device indicator, PII erasure UI surface.
- audit_log retention dashboard (long-horizon analytics + anomaly detection).
- SignalR enhancements (presence, typing indicators, multi-admin awareness on shared views).
- **Active Admin Sessions Monitor** (live "who's logged in right now" with force-logout).
- Deeper 2FA compliance reporting (enforcement audit, 2FA adoption %, backup-code rotation).
- Advanced rate limit editor (per-user overrides, IP-based denylists).

**Phase 6.13** (queued separately):
- Hotfix debt sweep: error-swallow cleanup (descriptive errors + toasts), backend HTTP method audit, frontend `any` cast cleanup, nginx sites-enabled drift cleanup, brute-force lockout UX, PWA real icons, Vitest broader coverage, CI/CD deploy pipeline.

**Phase 6.14** (queued separately):
- React Native admin mobile app (full feature parity with web, leveraging the role/permission system from 6.11). **MUST support both admin and superadmin login paths** (per user requirement — both URLs available via in-app radio selector or host switcher).

**6.11 explicitly NOT included:**
- Per-permission temporary "trial mode" with auto-expire after N uses — `expires_at` picker on the Custom template already covers time-based limits; use-count limits are Phase 6.12+.
- Email/Slack/SMS notifications beyond the 6 transactional types listed above (e.g., daily digest, weekly summary).
- Customizable permission key creation by superadmin (permission keys are a hardcoded enum — extending requires code change).
- Multi-tenant superadmin (single-org assumption).
- Role hierarchy beyond 3 levels (no "owner" above superadmin in this phase).
- Mobile admin app superadmin support — handled in Phase 6.14.

## Design decisions

### D1 — 3-tier permission model

| Tier | Mechanism | Members | UX |
|---|---|---|---|
| **Tier 1** | Tab-level gating | Configuration, IP Whitelist, Updates, Role Change | Tab visible in nav; click → locked-page placeholder + Request button |
| **Tier 2** | Action-level gating | Users.Delete, Users.Ban, Subscriptions.Grant, Subscriptions.Revoke, Payments.ApproveCrypto, Payments.RejectCrypto | Tab open; specific buttons rendered with lock icon + disabled; click → request modal |
| **Tier 3** | No gating (unless ReadOnly template) | Licenses.Revoke, Licenses.Activate, Devices.Revoke, CrashReports.Delete, all read-only ops | Normal admin behavior, unless `users.is_readonly = true` in which case all destructive actions blocked |

Permission keys: `tab:configuration`, `tab:ipwhitelist`, `tab:updates`, `tab:rolechange`, `action:users.delete`, `action:users.ban`, `action:subscriptions.grant`, `action:subscriptions.revoke`, `action:payments.approveCrypto`, `action:payments.rejectCrypto`. Namespace allows future expansion (e.g., `tab:billing`, `action:*.export`).

### D2 — Auth model: separate endpoint + separate subdomain

**Two layers of isolation:**

1. **Separate login endpoint** `/api/auth/superadmin/login`:
   - Accepts same payload (email, password, optional totpCode).
   - Validates user exists AND `role = 'superadmin'` (else 403 — never reveals whether the email exists).
   - Stricter rate limit: 3 failed attempts → 60-min lockout (vs admin login 5 / 30 min).
   - Audit-logged with action `'SuperadminLoginAttempt'` regardless of success/failure.
   - Response shape identical to `/api/auth/login` (JWT + refresh token).
   - Mandatory 2FA — even if bootstrap superadmin hasn't set up 2FA, first login after bootstrap returns a scope-limited JWT and redirects to `/enable-2fa`. No bypass.

2. **Separate subdomain** `superadmin.auracore.pro`:
   - Own nginx server block (own SSL cert, own server_name, own access_log).
   - Own static bundle (`/var/www/superadmin-panel/`), built from the same codebase with `NEXT_PUBLIC_BUILD_TARGET=superadmin` env flag (conditional rendering hides admin-only UI affordances and shows superadmin-only tabs).
   - Admin code paths not included in the superadmin bundle and vice versa (via conditional imports + Next.js tree-shaking).
   - CORS: backend's `app.UseCors()` adds `https://superadmin.auracore.pro` to the allowed origins list.
   - Basic auth: superadmin subdomain uses the existing nginx htpasswd + optional per-IP allow/deny block (can be tightened later without code change).

**Frontend flow:**
- `admin.auracore.pro` LoginScreen has a small "Sign In as Superadmin" link → external redirect to `https://superadmin.auracore.pro/`.
- `superadmin.auracore.pro` has only the superadmin login form (no admin login option); default bundle target.
- After login on superadmin subdomain, JWT is issued; frontend stores in `superadmin_token` localStorage key (distinct from `aura_token` on admin) to prevent cross-bundle token confusion.
- Backend JWT issuer claim: same (`auracore-api`); audience claim: different (`aud: "admin" | "superadmin"` based on login endpoint). Backend middleware validates audience matches the requested resource.

### D3 — Subdomain infrastructure

**DNS (manual step before deploy):**
- Add A record: `superadmin.auracore.pro → 165.227.170.3` (same droplet as admin.auracore.pro).
- Add SPF TXT record update: extend existing `v=spf1 include:spf.privateemail.com ~all` to `v=spf1 include:spf.privateemail.com include:_spf.resend.com ~all` (fixes SPF alignment for Resend outbound).

**Let's Encrypt cert:**
```bash
ssh root@165.227.170.3 "certbot --nginx -d superadmin.auracore.pro --non-interactive --agree-tos -m <admin-email> --redirect"
```

**Nginx server block** `/etc/nginx/sites-enabled/auracore-superadmin`:
```nginx
server {
    listen 443 ssl;
    server_name superadmin.auracore.pro;

    ssl_certificate /etc/letsencrypt/live/superadmin.auracore.pro/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/superadmin.auracore.pro/privkey.pem;
    include /etc/letsencrypt/options-ssl-nginx.conf;

    # Basic auth (same htpasswd as admin subdomain)
    auth_basic "Superadmin area";
    auth_basic_user_file /etc/nginx/.htpasswd;

    # Optional IP allowlist hook (currently commented out; can be activated for extra hardening)
    # allow <SUPERADMIN_HOME_IP>;
    # deny all;

    root /var/www/superadmin-panel;
    index index.html;

    location /api/ { proxy_pass http://127.0.0.1:5000; ... }
    location /hubs/ { proxy_pass http://127.0.0.1:5000; proxy_http_version 1.1; ... }
    location / { try_files $uri $uri/ /index.html; }

    # Same security headers as admin subdomain
    add_header Strict-Transport-Security ...;
    add_header X-Frame-Options DENY;
    ...
}

server {
    listen 80;
    server_name superadmin.auracore.pro;
    return 301 https://$host$request_uri;
}
```

### D4 — Frontend build: dual target, single codebase

**Approach:** single `admin-panel/` project, two build outputs controlled by an env var.

**Two npm scripts:**
```json
{
    "scripts": {
        "build:admin": "NEXT_PUBLIC_BUILD_TARGET=admin next build && mv out out-admin",
        "build:superadmin": "NEXT_PUBLIC_BUILD_TARGET=superadmin next build && mv out out-superadmin",
        "build:all": "npm run build:admin && npm run build:superadmin"
    }
}
```

**In-code conditional rendering:**
```ts
// admin-panel/src/lib/build-target.ts
export const BUILD_TARGET = (process.env.NEXT_PUBLIC_BUILD_TARGET ?? 'admin') as 'admin' | 'superadmin';
export const IS_SUPERADMIN_BUILD = BUILD_TARGET === 'superadmin';
```

**Nav config** (`src/app/page.tsx`):
```tsx
const NAV_GROUPS: NavGroup[] = IS_SUPERADMIN_BUILD
    ? SUPERADMIN_NAV_GROUPS   // includes Permission Requests, Admin Action Log, Admin Management, Role Change, Security Policy, API Rate Limits
    : ADMIN_NAV_GROUPS;        // same tabs as today
```

**LoginScreen:**
```tsx
{IS_SUPERADMIN_BUILD ? (
    <SuperadminLoginForm />    // posts to /api/auth/superadmin/login
) : (
    <AdminLoginForm>
        <a href="https://superadmin.auracore.pro/" className="text-xs opacity-50">Sign In as Superadmin →</a>
    </AdminLoginForm>
)}
```

**Deploy:**
```bash
# Admin bundle
scp -r admin-panel/out-admin/. root@165.227.170.3:/var/www/admin-panel/

# Superadmin bundle
scp -r admin-panel/out-superadmin/. root@165.227.170.3:/var/www/superadmin-panel/
```

**Rationale:** Single codebase avoids code-duplication across two projects; Next.js tree-shaking strips unused imports in each bundle (e.g., superadmin-only views like `PermissionRequestsPage` are excluded from the admin bundle); one CI build pipeline, two deploy outputs; easy to maintain shared components (`DataTable`, `PageHeader`, `ConfirmDialog`, etc.) that both bundles use.

### D5 — Superadmin bootstrap

Backend startup reads `SUPERADMIN_EMAILS` env var (comma-separated for multi-superadmin support) and runs an idempotent upsert:
```sql
UPDATE users SET role = 'superadmin' WHERE email = ANY(@emails) AND role != 'superadmin';
```

Only promotes existing registered users — does not create new accounts (chicken-and-egg avoided). Initial value: `SUPERADMIN_EMAILS=ozgurdeniz807@gmail.com`. On the very first login after bootstrap, if `totp_enabled = false`, backend returns a limited-scope JWT and frontend redirects to `/enable-2fa` — mandatory.

Demotion not auto-managed by env var changes (would be too dangerous on accidental config edit). Demotion is a manual operation through the Role Change UI or DB.

### D6 — Storage schema

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
CREATE UNIQUE INDEX uq_permission_grants_active
  ON permission_grants(admin_user_id, permission_key)
  WHERE revoked_at IS NULL;

-- Permission request inbox + history
CREATE TABLE permission_requests (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    admin_user_id   UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    permission_key  VARCHAR(100) NOT NULL,
    reason          TEXT NOT NULL,
    requested_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    status          VARCHAR(20) NOT NULL DEFAULT 'pending',
    reviewed_by     UUID NULL REFERENCES users(id),
    reviewed_at     TIMESTAMPTZ NULL,
    review_note     TEXT NULL
);
CREATE INDEX ix_permission_requests_status_admin
  ON permission_requests(status, admin_user_id);

-- Per-admin pending request uniqueness: at most 1 pending request per (admin, permission) at a time
CREATE UNIQUE INDEX uq_permission_requests_pending
  ON permission_requests(admin_user_id, permission_key)
  WHERE status = 'pending';

-- Revoked tokens (for suspend session invalidation)
CREATE TABLE revoked_tokens (
    jti           VARCHAR(100) PRIMARY KEY,      -- JWT unique ID claim
    user_id       UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    revoked_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    revoked_by    UUID NULL REFERENCES users(id),
    revoke_reason VARCHAR(100) NOT NULL           -- 'suspend'|'password_reset'|'logout_all'|'admin_deleted'
);
CREATE INDEX ix_revoked_tokens_user ON revoked_tokens(user_id);

-- Admin invitation tokens (one-time setup links)
CREATE TABLE admin_invitations (
    token_hash    VARCHAR(100) PRIMARY KEY,      -- SHA256 of the actual token sent by email
    admin_user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    created_by    UUID NOT NULL REFERENCES users(id),
    created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at    TIMESTAMPTZ NOT NULL,
    consumed_at   TIMESTAMPTZ NULL                -- set when admin uses the link to set password
);
CREATE INDEX ix_admin_invitations_user ON admin_invitations(admin_user_id);
```

**User table additions:**
```sql
ALTER TABLE users ADD COLUMN is_active BOOLEAN NOT NULL DEFAULT TRUE;
ALTER TABLE users ADD COLUMN is_readonly BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE users ADD COLUMN force_password_change BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE users ADD COLUMN force_password_change_by TIMESTAMPTZ NULL;
ALTER TABLE users ADD COLUMN password_changed_at TIMESTAMPTZ NULL;
ALTER TABLE users ADD COLUMN created_by_user_id UUID NULL REFERENCES users(id);
ALTER TABLE users ADD COLUMN created_via VARCHAR(30) NOT NULL DEFAULT 'signup'; -- signup|admin_promote|superadmin_create
ALTER TABLE users ADD COLUMN require_2fa BOOLEAN NOT NULL DEFAULT FALSE;
```

**System settings table** (new generic key-value store; if one already exists, reuse):
```sql
CREATE TABLE IF NOT EXISTS system_settings (
    key         VARCHAR(100) PRIMARY KEY,
    value       TEXT NOT NULL,
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by  UUID NULL REFERENCES users(id)
);
-- Seeds:
INSERT INTO system_settings (key, value) VALUES
    ('require_2fa_for_all_admins', 'false'),
    ('rate_limit_policies', '{"auth.login":{"requests":5,"windowSeconds":1800},"auth.register":{"requests":3,"windowSeconds":3600},"admin.all":{"requests":1000,"windowSeconds":3600},"signalr.connect":{"requests":10,"windowSeconds":60}}');
```

### D7 — Backend `[RequiresPermission]` attribute

```csharp
public sealed class RequiresPermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    public string Permission { get; }
    public RequiresPermissionAttribute(string permission) { Permission = permission; }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        var role = user.FindFirst(ClaimTypes.Role)?.Value;
        if (role == "superadmin") return;

        if (role != "admin")
        {
            context.Result = new ForbidResult();
            return;
        }

        var userId = Guid.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();

        // Read-only admins fail for all destructive actions (Tier 2 and Tier 3)
        var isReadonly = await db.Users
            .Where(u => u.Id == userId).Select(u => u.IsReadonly).FirstOrDefaultAsync();
        if (isReadonly && !Permission.StartsWith("tab:"))
        {
            context.HttpContext.Response.StatusCode = 403;
            await context.HttpContext.Response.WriteAsJsonAsync(new {
                error = "readonly_account",
                permission = Permission
            });
            context.Result = new EmptyResult();
            return;
        }

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

Applied as:
```csharp
[Authorize(Roles = "admin")]
[RequiresPermission("action:users.delete")]
public async Task<IActionResult> DeleteUser(Guid id) { ... }
```

For Tier 3 destructive actions (not attribute-gated by default), add a middleware check against `is_readonly` flag at the start of the action method (or introduce a `[DestructiveAction]` attribute that simply reads is_readonly). The spec plan phase will decide the cleanest shape.

### D8 — Permission templates

Backend: constants in `PermissionTemplates.cs`:
```csharp
public static class PermissionTemplates
{
    public const string Default = "Default";
    public const string Trusted = "Trusted";
    public const string ReadOnly = "ReadOnly";
    public const string Custom = "Custom";

    public static IReadOnlyList<string> GetPermissionsForTemplate(string template) => template switch
    {
        Default  => Array.Empty<string>(),       // Tier 3 open by default — no grants needed
        Trusted  => new[] {
            "action:users.delete",
            "action:users.ban",
            "action:subscriptions.grant",
            "action:subscriptions.revoke",
            "action:payments.approveCrypto",
            "action:payments.rejectCrypto",
        },
        ReadOnly => Array.Empty<string>(),       // paired with users.is_readonly = true
        Custom   => throw new InvalidOperationException("Custom is configured per-grant by superadmin"),
        _        => throw new ArgumentException($"Unknown template: {template}"),
    };

    public static bool RequiresIsReadonlyFlag(string template) => template == ReadOnly;
}
```

Applied on admin creation / promotion:
```csharp
var permissions = PermissionTemplates.GetPermissionsForTemplate(template);
if (PermissionTemplates.RequiresIsReadonlyFlag(template))
    user.IsReadonly = true;

foreach (var permKey in permissions)
    db.PermissionGrants.Add(new PermissionGrant { ... });
await db.SaveChangesAsync();
```

**Custom template UX:**
- Create Admin modal → Template dropdown = "Custom" → expand section:
  - Tab checkboxes (per Tier 1): Configuration, IP Whitelist, Updates, Role Change
  - Action checkboxes (per Tier 2): Users.Delete, Users.Ban, Subscriptions.Grant, Subscriptions.Revoke, Payments.ApproveCrypto, Payments.RejectCrypto
  - Per-permission `expires_at` picker (default: never) + "Apply same expiry to all" shortcut
  - ReadOnly override checkbox ("This admin is read-only — block Tier 3 destructive actions too")
- On submit: backend receives list of `{ permission_key, expires_at? }` + optional is_readonly flag → creates individual permission_grants.

### D9 — Force password change flow

Login response shape (both endpoints):
```json
{
  "accessToken": "...",
  "refreshToken": "...",
  "user": { ... },
  "requiresPasswordChange": true,
  "passwordChangeDeadline": "2026-04-30T12:00:00Z",
  "requiresTwoFactorSetup": false,
  "scope": null
}
```

Frontend: if `requiresPasswordChange === true`, redirect to `/change-password` view. Server-side middleware: if authenticated user has `force_password_change=true` AND `force_password_change_by < now()`, all endpoints (except change-password, logout) return 403 with `{ "error": "password_change_required" }` — frontend catches and redirects.

The `/change-password` endpoint validates current password (bypass-able if `force_password_change=true` and deadline passed — emergency unlock), sets new password, clears the flag, invalidates refresh token, returns new JWT.

### D10 — 2FA enforcement policy

Login flow modification:
1. Validate credentials (email + password + existing rate-limit + lockout checks).
2. Check `is_active`: if false → 403 `{ "error": "account_suspended" }`.
3. If role in ('admin', 'superadmin'):
   - `requires_2fa = (role == 'superadmin') OR system_settings['require_2fa_for_all_admins'] OR user.require_2fa`.
   - If `requires_2fa == true AND user.totp_enabled == false`:
     - Issue scope-limited JWT with claim `scope: '2fa-setup-only'` (short TTL: 15 min).
     - Response: `{ requiresTwoFactorSetup: true, accessToken (scope-limited), ... }`.
     - Frontend redirects to `/enable-2fa`.
   - Else: continue normal TOTP flow (if user.totp_enabled, request totpCode; etc.).

Middleware enforces scope: endpoints other than `/api/auth/enable-2fa` and `/api/auth/logout` reject scope-limited tokens with 403 `{ "error": "scope_limited_token" }`.

Superadmin UI (Security Policy tab):
- Global toggle: "Require 2FA for all admin accounts" → updates `system_settings['require_2fa_for_all_admins']`.
- Per-account list: shows all admin accounts with toggle "Require 2FA on this account" → updates `users.require_2fa`.
- Read-only row for superadmin accounts: "2FA required (always)" — cannot be toggled off.

### D11 — Admin Action Log + CSV export

Backend endpoints:
- `GET /api/superadmin/admin-actions?page=&pageSize=&actorEmail=&action=&targetType=&dateFrom=&dateTo=` — paginated list + filter.
- `GET /api/superadmin/admin-actions/stats` — KPIs (total, last 24h, last 7d, top 5 admins, top 5 actions).
- `GET /api/admin/audit-log/export.csv?dateFrom=&dateTo=&actorEmail=&action=` — CSV stream, admin-side (audit log scope).
- `GET /api/superadmin/admin-actions/export.csv?...` — CSV stream, superadmin-side (filtered to admin role).

CSV format (RFC 4180 compliant):
```
"id","actor_email","actor_id","action","target_type","target_id","ip_address","created_at_utc"
"abc123","admin@auracore.pro","...","RevokeLicense","License","xyz789","1.2.3.4","2026-04-23T10:30:00Z"
```

Streaming via `IAsyncEnumerable<AuditLogEntry>` + `Response.Body.WriteAsync` chunks to avoid memory spikes on large exports.

Authorization:
- Admin-side export: requires `tab:auditlog` (Tier 3 open by default).
- Superadmin-side export: requires `role = 'superadmin'`.

Rate limit: 10 exports / hour / user (CSV generation is expensive).

### D12 — API rate limit config UI

Backend `RateLimitConfigService`:
```csharp
public class RateLimitConfigService
{
    private readonly IDbContext _db;
    private readonly IMemoryCache _cache;
    public static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<Dictionary<string, RateLimitPolicy>> GetPoliciesAsync()
    {
        return await _cache.GetOrCreateAsync("rate_limit_policies", async entry => {
            entry.SetAbsoluteExpiration(CacheTtl);
            var row = await _db.SystemSettings.FindAsync("rate_limit_policies");
            return JsonSerializer.Deserialize<Dictionary<string, RateLimitPolicy>>(row?.Value ?? "{}")!;
        });
    }

    public async Task UpdatePolicyAsync(string endpoint, RateLimitPolicy policy, Guid updatedBy)
    {
        var current = await GetPoliciesAsync();
        current[endpoint] = policy;

        var row = await _db.SystemSettings.FindAsync("rate_limit_policies");
        row!.Value = JsonSerializer.Serialize(current);
        row.UpdatedBy = updatedBy;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _cache.Remove("rate_limit_policies");  // Force reload
        _rateLimiter.Reconfigure(current);     // Hot-apply to ASP.NET Core RateLimiter
    }
}
```

UI (superadmin "API Rate Limits" tab):
- Table: Endpoint | Requests | Window (seconds) | Last Updated | Actions (Edit)
- Edit dialog: number inputs for requests + window + "Apply" button.
- Apply button calls PUT `/api/superadmin/rate-limits/{endpoint}` with the new policy.
- Backend reconfigures immediately — no deploy needed.

### D13 — Email infrastructure

**Architecture:** `IEmailService` interface + `ResendEmailService` implementation.

```csharp
public interface IEmailService
{
    Task<EmailSendResult> SendAsync(string to, string subject, string html, CancellationToken ct = default);
    Task<EmailSendResult> SendFromTemplateAsync(EmailTemplate template, object data, CancellationToken ct = default);
}

public sealed record EmailSendResult(bool Success, string? MessageId, string? Error);

public enum EmailTemplate
{
    AdminInvitation,          // { adminEmail, setupLink, expiresAt }
    PasswordReset,            // { email, resetLink, expiresAt }
    PermissionRequested,      // (to superadmin) { adminEmail, permissionKey, reason, inboxLink }
    PermissionApproved,       // (to admin) { permissionKey, approvedBy, expiresAt? }
    PermissionDenied,         // (to admin) { permissionKey, deniedBy, reviewNote? }
    AdminCreatedWithoutEmail, // (to superadmin) { adminEmail, initialPassword, note: "invite-email toggle off; share password securely" }
}
```

**Implementation** (`src/Backend/AuraCore.API.Infrastructure/Services/Email/ResendEmailService.cs`):
- Uses `IHttpClientFactory` (registered in `Program.cs` with named client `"resend"` pointing to `https://api.resend.com`).
- Reads `RESEND_API_KEY` from env var via `IConfiguration`.
- Logs request + response (status, message ID, any error body).
- Returns `EmailSendResult` — callers decide whether to retry / surface error.
- Template rendering via simple string-replace (no full MVC view engine needed — 6 templates are small).
- HTML template file: `src/Backend/AuraCore.API.Infrastructure/Services/Email/Templates/*.html` with `{{placeholder}}` markers; shared base layout `_base.html` for consistent styling.

**Refactor targets:**
- `PasswordResetController.cs:146-168` — replace inline HTTPS call with `await _emailService.SendFromTemplateAsync(EmailTemplate.PasswordReset, new { ... })`.
- All new Phase 6.11 notification points use `_emailService`.

**DI registration** (`Program.cs`):
```csharp
builder.Services.AddHttpClient("resend", c => {
    c.BaseAddress = new Uri("https://api.resend.com");
    c.DefaultRequestHeaders.Add("Authorization", $"Bearer {Environment.GetEnvironmentVariable("RESEND_API_KEY")}");
});
builder.Services.AddScoped<IEmailService, ResendEmailService>();
```

**DNS fix (part of Wave 6 ops):**
Update SPF TXT record on `auracore.pro`:
```
v=spf1 include:spf.privateemail.com include:_spf.resend.com ~all
```
Currently missing `include:_spf.resend.com` → Resend outbound can fail SPF alignment for DMARC-strict recipients.

### D14 — Admin Account Creation flow

**New superadmin "Admin Management" tab:**
- List: email | created_at | created_by | last_login | is_active | template | 2FA enabled | actions
- Per-row actions: Edit Permissions / Reset Password / Suspend (or Restore) / Delete
- Top button: **+ Create Admin Account**

**Create modal:**
1. **Email** (zorunlu, format + uniqueness validation).
2. **Send invitation email** toggle (default ON):
   - If ON: no password field shown; admin will receive invite email with setup link.
   - If OFF: "Initial password" input + "Generate Strong" (16-char alphanum+symbol) + Show/Hide + Copy; superadmin shares password via out-of-band channel.
3. **Force password change** dropdown (4 options; default "On first login").
4. **Permission template** (Default / Trusted / Read-Only / Custom — with Custom picker as described in D8).
5. **Initial 2FA requirement** checkbox ("Require 2FA on next login") — auto-checked + disabled if global 2FA policy is on.
6. **Create** → POST `/api/superadmin/admins/create` → backend creates user, hashes password (if set), generates permission_grants, sends invite email (if toggle ON) with token link.

**Promote-existing-user flow:**
- Users tab → existing `user` row → action "Promote to Admin".
- Modal: template picker + force password change policy + 2FA requirement.
- Backend updates `role = 'admin'`, applies template, sends notification email.

**Invitation email flow:**
- Backend generates cryptographic token (256-bit entropy) → stores SHA256 hash in `admin_invitations` table with 7-day expiry.
- Sends email with link: `https://admin.auracore.pro/invite?token=<raw-token>&email=<url-encoded>`.
- Admin clicks link → frontend shows "Welcome! Set your password to get started" form → POST `/api/auth/redeem-invitation` with token + new password → backend verifies hash, creates/updates user row, sets password, marks token consumed, returns JWT.
- On subsequent login, admin uses normal login flow; token is one-time-use only.

### D15 — Admin suspend/restore + delete

**Suspend:**
- `users.is_active = false`.
- All active tokens revoked via `revoked_tokens` (insert rows for all non-expired JWTs — can be tracked via refresh_tokens table if one exists, or via a "revoke all tokens for user" operation that increments a `tokens_valid_from` field on users).
- Login attempt for suspended user → 403 `{ "error": "account_suspended" }`.
- Existing API requests with valid token → 401 `{ "error": "token_revoked" }` (middleware checks revoked_tokens on every authenticated request).

**Restore:**
- `users.is_active = true`.
- User can log in again (force_password_change re-applied if it was set).

**Delete:**
- Hard delete with cascade (existing behavior; Tier 2 action — requires `action:users.delete` permission).
- Cascade includes permission_grants, permission_requests, revoked_tokens, admin_invitations.

### D16 — "My Permissions" page (admin self-service)

New admin-side view `src/views/MyPermissionsPage.tsx` (visible to all admins, not in default nav — accessible via user menu dropdown, top-right):
- **Summary card:** "You are an admin (`{email}`). You have access to {N} of {M} restricted permissions."
- **Active grants table:** Permission | Label | Granted by | Granted at | Expires at | Source (request / template / direct grant).
- **Pending requests table:** Permission | Label | Requested at | Reason (truncated) | Cancel button (admin can withdraw a pending request).
- **Recent denials/revocations table:** Permission | Label | Reviewed by | Note | When.

### D17 — SignalR events summary

Existing from Phase 6.10: `UserRegistered`, `UserLogin`, `Payment`, `CrashReport`, `Telemetry`, `AdminCount`.

**New in 6.11:**
- `PermissionRequested` (to superadmins group) — triggers badge on Permission Requests tab + toast.
- `PermissionApproved` (to specific admin user) — frontend refreshes tab state, shows toast.
- `PermissionDenied` (to specific admin user) — toast with optional note.
- `PermissionRevoked` (to specific admin user) — toast + tab state refresh.

Targeted events use `Clients.User(userId)` pattern (SignalR built-in user mapping).

## Architecture — Wave breakdown

**Wave 1 — DB schema + auth foundation:**
- EF migration: permission_grants + permission_requests + revoked_tokens + admin_invitations + users field additions + system_settings table + seed rows.
- Backend: `superadmin` role enum value + `/api/auth/superadmin/login` endpoint + bootstrap from SUPERADMIN_EMAILS env var + grandfather migration for existing admin accounts (Trusted template).
- Backend: `[RequiresPermission]` attribute + filter + JWT audience + scope claim validation.
- Backend: token revocation middleware.
- Backend tests: attribute behavior + bootstrap idempotency + grandfather migration + superadmin login auth.

**Wave 2 — Permission system + Tier 1/2 application + email service refactor:**
- Backend: permission grants CRUD endpoints (admin can list own, superadmin can list all + create/revoke).
- Backend: permission requests CRUD endpoints (admin create, admin list own, admin cancel; superadmin list pending, approve, deny).
- Backend: `[RequiresPermission]` applied to all Tier 1 tab mutation endpoints + 6 Tier 2 action endpoints.
- Backend: SignalR new events (PermissionRequested / Approved / Denied / Revoked).
- Backend: refactor PasswordResetController.cs inline HTTPS into IEmailService abstraction + ResendEmailService implementation + 6 HTML templates.
- Backend tests: grant lifecycle + request flow + permission filter end-to-end + email service unit tests (mock IHttpClientFactory).

**Wave 3 — Dual frontend builds + nginx superadmin subdomain + shared UX primitives:**
- Frontend: `NEXT_PUBLIC_BUILD_TARGET` env var + conditional NAV_GROUPS + LoginScreen variants.
- Frontend: LockedTabPlaceholder component + locked-button rendering in views that have Tier 2 actions.
- Frontend: PermissionRequestDialog component.
- Frontend: useSignalR additions for permission events + per-user targeting.
- Frontend: `build:admin` + `build:superadmin` scripts.
- Ops: DNS A record for superadmin.auracore.pro; Let's Encrypt cert; nginx server block; initial deploy of both bundles.
- Ops: SPF TXT record fix (add `include:_spf.resend.com`).
- Frontend tests: LockedTabPlaceholder render + PermissionRequestDialog interaction + build target conditional logic.

**Wave 4 — Superadmin-only tabs (Permission Requests + Admin Action Log + Admin Management + Role Change skeleton):**
- Frontend: PermissionRequestsPage (table + approve/deny dialogs + bulk actions + SignalR live updates).
- Frontend: AdminActionLogPage (filters + KPIs + table + SignalR live appends + CSV export button).
- Frontend: AdminManagementPage (list + create admin modal + edit permissions modal + reset password button + suspend/restore + delete).
- Frontend: Role Change UI skeleton (promote user → admin, demote admin → user — single-user flow).
- Backend: superadmin endpoints — /api/superadmin/admins/create, reset-password, suspend, restore, /api/superadmin/users/{id}/promote, /api/superadmin/admins/{id}/demote.
- Backend: admin-actions list + stats + CSV export endpoints.
- Backend: admin_invitations table + redeem-invitation endpoint + invite-email template.
- Backend tests: admin lifecycle endpoints + invite flow + CSV export streaming.

**Wave 5 — Templates + force-change + 2FA enforcement + Security Policy tab + API Rate Limits tab:**
- Backend: PermissionTemplates logic (Default / Trusted / ReadOnly / Custom) + is_readonly handling in [RequiresPermission] + Tier 3 action destructive-action check.
- Backend: force password change middleware + deadline enforcement + change-password endpoint respects scope.
- Backend: 2FA enforcement logic in login flows + scope-limited JWT issuance + scope validation middleware.
- Backend: RateLimitConfigService + DB-backed policies + hot-reload + superadmin PUT endpoint.
- Frontend: SecurityPolicyPage (superadmin) — global 2FA toggle + per-account override list.
- Frontend: APIRateLimitsPage (superadmin) — table + edit dialog + apply button.
- Frontend: change-password view (post-login redirect target).
- Frontend: enable-2fa view (reuse existing SecurityPage 2FA setup logic; ensure scope-limited JWT access works).
- Tests: template grant generation + force change middleware + 2FA enforcement resolution + rate limit hot-reload.

**Wave 6 — My Permissions + DNS SPF fix + final polish + deploy + ceremonial:**
- Frontend: MyPermissionsPage (admin self-service).
- Frontend: final polish (toast notifications for SignalR events, responsive layouts for new tabs).
- Ops: SPF TXT record update live on auracore.pro DNS.
- Ops: deploy admin + superadmin bundles + backend to prod.
- Final full-suite test + memory file + MEMORY.md update + ceremonial merge to main + push to origin (user-gated).

## Testing strategy

- **Backend** (xUnit): ~30-40 tests — [RequiresPermission] attribute behavior, bootstrap idempotency, grandfather migration, superadmin login (success + various failures), permission request lifecycle, approval flow, grant expiration, is_readonly enforcement, template generation, force password change middleware, 2FA enforcement ladders, RateLimitConfigService, admin lifecycle endpoints, invitation token redemption, CSV export streaming, IEmailService (mocked).
- **Frontend** (Vitest + RTL): ~15-25 tests — LockedTabPlaceholder, PermissionRequestDialog (validation, submit flow), PermissionRequestsPage approve/deny interactions, AdminManagementPage create modal, MyPermissionsPage render, build-target conditional rendering.
- **Skipped:** End-to-end Playwright (Phase 6.13+); multi-browser manual QA (manual smoke only).
- **Target:** ~2392 → ~2450-2480 (+55-90 net new tests).

## Deployment flow

**Mid deploy (after Wave 2):** backend with schema + permission attribute + IEmailService refactor. Smoke: superadmin login from an ad-hoc curl (superadmin.auracore.pro isn't live yet, but the endpoint is), create permission grant against a test admin, verify [RequiresPermission] filter returns 403 on unprivileged request.

**Subdomain cut-over (during Wave 3):** DNS + Let's Encrypt cert + nginx server block + initial deploy of superadmin bundle. Smoke: `curl https://superadmin.auracore.pro/` returns 401 (basic auth), after basic auth returns the superadmin LoginScreen. SPF TXT record update.

**Final deploy (after Wave 6):** both admin + superadmin frontend bundles + backend full rebuild. Smoke: complete end-to-end — login as admin, verify Tier 1 tabs are locked, click "Request Permission", verify superadmin receives SignalR + email notification, login as superadmin on separate subdomain, approve request, admin's locked tab unlocks live via SignalR.

DB migrations run via standard EF Core migration in mid-deploy.

## Open questions / known risks

- **Grandfather migration triggers on every backend startup:** Must be guarded to idempotent (check if any grants exist for the user before inserting) to avoid duplicates. Alternative: use the migration framework directly (one-time EF migration run).
- **Scope-limited JWT collision with SignalR:** Scope-limited tokens (for 2FA setup) must NOT allow `/hubs/admin` WebSocket connection. SignalR auth handler must check scope claim and reject if `scope: '2fa-setup-only'`. Verified in Wave 5 tests.
- **CORS origin list maintenance:** Both `admin.auracore.pro` AND `superadmin.auracore.pro` must be in `app.UseCors()` allowlist. Missing either breaks cross-origin fetch from that subdomain.
- **Frontend bundle size vs tree-shaking effectiveness:** Need to verify that `if (IS_SUPERADMIN_BUILD)` branches correctly tree-shake — e.g., `PermissionRequestsPage` import should NOT appear in admin bundle. Mitigation: use dynamic `import()` for bundle-specific views, conditional on build target, so Next.js code-splits them out entirely.
- **Email deliverability with SPF change:** SPF record edit is a propagation-sensitive DNS change. Wave 6 deploys SPF update early and monitors DMARC report delivery for 24h.
- **Subdomain subdivided session collision:** If admin logs into `admin.auracore.pro` and separately logs into `superadmin.auracore.pro`, two separate JWTs in two localStorage namespaces. Both use the same backend. Backend must treat each as independent authenticated request. The `revoked_tokens` table handles cross-bundle revocation (suspend an admin → their admin token AND any superadmin-promoted token are both revoked).
- **Next.js static export + dual build edge case:** `NEXT_PUBLIC_*` env vars are baked at build time. If the same `admin-panel/` project is rebuilt with different env vars, the output dirs must be isolated (`out-admin/` vs `out-superadmin/`) to avoid one bundle overwriting the other.

## Decision log

| Decision | Chosen | Rejected | Why |
|---|---|---|---|
| Scope decomposition | Phase 6.11 = Superadmin foundation; 6.12 = features; 6.13 = debt; 6.14 = RN app | Single mega-phase | Foundational change — every later feature builds on it |
| Permission model granularity | 3-tier (tab + action + free) | Pure tab-level OR pure action-level | Mixed model fits real risk profile |
| Auth isolation | Separate endpoint + separate subdomain | Single endpoint / single subdomain | Defense-in-depth; bundle isolation; future IP allowlist; security-through-layers |
| Frontend architecture | Single codebase, dual build target via env var | Two separate Next.js projects / monorepo | Minimal code duplication; shared components; one CI pipeline; two deploy outputs |
| Bootstrap | Env var `SUPERADMIN_EMAILS` idempotent on startup | Manual DB seed / first-user pattern | Reproducible, source-controlled |
| ReadOnly template enforcement | `users.is_readonly` boolean checked by backend | Denial-grants in permission_grants | Simpler; single column; clear semantics |
| Storage | `permission_grants` + `permission_requests` tables | Single audit_log table for requests / JSON column on users | Indexable, typed, separate lifecycle concerns |
| Locked-tab UX | In-place placeholder page + Request button | 404-style hidden tab | User intent: admin sees missing features, can request |
| Email provider | Keep Resend; refactor into IEmailService | Switch to SendGrid / Mailgun / AWS SES | Already in use; HTTPS-443 works on DO; credential already seeded |
| Password onboarding | Invitation token link (preferred) + fallback manual password | Always-manual password set by superadmin | Never email initial password; token link is one-shot; manual fallback for when email is unavailable |
| Rate limit config storage | DB-backed `system_settings` JSON + in-memory cache | Hardcoded constants / appsettings.json | Runtime editable without deploy; superadmin self-service |
| Grandfather strategy | Trusted template for existing admins | Default template / no grants | User has 1 admin account (self); Trusted keeps current capabilities |
| Superadmin 2FA | Mandatory on first login (no toggle) | Optional per-superadmin | Privileged role; bootstrap must enforce; no bypass |

## Non-goals

- Multi-tenant superadmin (single-org).
- Role hierarchy beyond 3 levels.
- Custom permission key creation by superadmin (keys are hardcoded enum).
- Per-permission usage-count limits (e.g., "this permission expires after 10 uses").
- Advanced rate limit (per-user overrides, IP denylists — Phase 6.12+).
- Email delivery receipts / bounce handling (Resend dashboard covers it; no in-app UI).
- Audit log retention policy (archival / deletion of old rows — Phase 6.12+).
- Slack/Teams/SMS notification channels.
- Mobile app superadmin support (Phase 6.14).

## Success criteria

Phase 6.11 is DONE when:

- `superadmin` role exists in `users.role`; bootstrap from `SUPERADMIN_EMAILS` env var works idempotently.
- `/api/auth/superadmin/login` endpoint live with stricter rate limit + mandatory 2FA + audit logging.
- `superadmin.auracore.pro` subdomain resolves, has valid SSL cert, serves the superadmin bundle.
- `admin.auracore.pro` LoginScreen has "Sign In as Superadmin" link that redirects to the subdomain.
- 4 Tier 1 tabs render LockedTabPlaceholder for unprivileged admins with "Request Permission" button; all 4 tabs' mutation endpoints reject admin requests with `permission_required` 403.
- 6 Tier 2 action buttons render with lock icon for unprivileged admins; click opens PermissionRequestDialog; submit creates a permission_request row.
- Superadmin's Permission Requests tab shows pending requests; Approve creates permission_grant + emits `PermissionApproved` via SignalR + sends email; Deny updates status + emits `PermissionDenied` + sends email. Bulk approve/deny works.
- Admin Action Log tab shows audit_log filtered by `actor_user.role='admin'`, with filters + KPIs + live SignalR appends. Admin-side and superadmin-side CSV export work (streaming).
- Admin Management tab supports: create new admin (email + password/invitation + force change + template + 2FA), promote existing user, edit permissions, reset password, suspend/restore, delete.
- Invite-email flow works: invitation token link sent, admin redeems link to set password.
- 4 permission templates work: Default (no extra grants), Trusted (Tier 2 unlocked), Read-Only (is_readonly enforced server-side), Custom (per-permission picker with individual expires_at).
- Force password change flow works: deadlines enforced by middleware; 4 timing options selectable.
- 2FA enforcement works: global toggle + per-account override; resolution redirects to /enable-2fa when needed; superadmin always forced.
- Security Policy tab (superadmin) controls global 2FA toggle + per-account override.
- API Rate Limits tab (superadmin) shows per-endpoint-group policies; edit + Apply updates live without deploy.
- Suspend invalidates active sessions via revoked_tokens; suspended admins return 401 on authenticated requests.
- Existing admin(s) grandfathered with Trusted template grants on first deploy — no lockout.
- "My Permissions" page lets admin see their grants + pending requests + recent denials.
- Role Change UI skeleton: superadmin can promote a user to admin or demote an admin to user.
- IEmailService abstraction in place; PasswordResetController refactored to use it; 6 transactional email types implemented.
- SPF TXT record updated to include Resend; deliverability improved.
- Backend ~30-40 new tests pass; frontend ~15-25 new tests pass; total ~2450-2480, 0 failed, 0 skipped.
- Memory file written + MEMORY.md pointer updated.
- Branch merged to main via `--no-ff` (ceremonial) + pushed to origin (user-gated).

**Spec end.** Writing-plans skill invoked next.
