# Phase 6.11 — Superadmin Foundation Design Spec

**Status:** Design approved (user, 2026-04-23, brainstorm Q1-Q9 sealed). Next: writing-plans.
**Branch:** `phase-6-superadmin-foundation` (will be created from `main` HEAD `d9e9353` — Phase 6.10 sealed + Round 1-3 hotfixes complete + spec docs).
**Phase ref:** Phase 6 Item 11 — superadmin role hierarchy + permission gating + admin lifecycle management + email notifications + audit log CSV export + API rate limit configuration UI. Phase 6.12+ queued separately.

## Context

Phase 6.10 closed the admin panel rebuild with two roles: `user` and `admin`. The current model is binary — every admin has full access to every tab and every action. This is fine for a single-admin setup but breaks down as the team grows. Specifically:

- **No principle of least privilege.** Any admin can delete users, approve crypto payments, change feature flags, publish app releases. Compromised admin = catastrophic damage.
- **No accountability hierarchy.** No "supervisor" admin to investigate or revoke access.
- **No staged onboarding.** Adding a new admin = giving them everything immediately.
- **Manual account creation only.** Admins seeded by DB UPDATE; no UI.
- **No observability of email deliverability.** `PasswordResetController.cs:146-168` uses inline fire-and-forget `new HttpClient()` POST to Resend — no status check, no logging, no abstraction.
- **No runtime control of API rate limits.** Hardcoded in `Program.cs`; changes require code deploy.

Phase 6.11 introduces a `superadmin` role above `admin` with a separate stricter login endpoint, a full permission grant/request/approval system, admin lifecycle management (create/promote/suspend/restore/delete), 4 permission templates (including Custom with per-permission expiry), hybrid 2FA enforcement, transactional email notifications via a proper `IEmailService` abstraction around Resend, audit log CSV export, and runtime-editable API rate limit policies. nginx basic auth is removed from `admin.auracore.pro` (modern auth = MFA + lockout + password policy is sufficient; basic auth ≠ app identity, double-auth UX is poor).

## Scope

### In scope — Phase 6.11

**Auth model:**
- New `superadmin` role on `users.role` field (alongside existing `user` and `admin`).
- Dedicated `/api/auth/superadmin/login` endpoint (separate from `/api/auth/login`) — stricter rate limit (3 fails / 60 min vs admin's 5 / 30 min), mandatory audit logging, never reveals whether the email exists.
- Superadmin bootstrap via `SUPERADMIN_EMAILS` env var (idempotent on backend startup); initial value `ozgurdeniz807@gmail.com`.
- Superadmin accounts MUST have 2FA enabled (mandatory override on first login — bootstrap-promoted superadmin forced to `/enable-2fa` before reaching dashboard).
- Single `admin.auracore.pro` subdomain (no separate superadmin subdomain — A approach per Q9). Single frontend bundle, post-login role detection renders admin panel or superadmin panel.
- Frontend LoginScreen has two buttons: "Sign In as Admin" (default form, posts to `/api/auth/login`) + "Sign In as Superadmin" (cyan-purple gradient on submit, posts to `/api/auth/superadmin/login`).
- **Remove nginx basic auth from `admin.auracore.pro`** (admin panel goes public-internet-reachable; backend login + 2FA + rate-limit are the security layers). `robots.txt` (`Disallow: /`) + `<meta name="robots" content="noindex,nofollow">` prevent search engine indexing.

**Permission model (3-tier):**
- **Tier 1 — Tab gating:** 4 tabs default-locked for admins (Configuration, IP Whitelist, Updates, Role Change). Locked tabs render placeholder: `"This page has been disabled by the superadmin by default. You need permission from the superadmin to be able to use the {TabName} tab."` + Request Permission button.
- **Tier 2 — Action gating:** 6 destructive actions default-locked (Users.Delete, Users.Ban, Subscriptions.Grant, Subscriptions.Revoke, Payments.ApproveCrypto, Payments.RejectCrypto). Buttons render with lock icon + disabled. Click → request modal with mandatory reason.
- **Tier 3 — Free for admins:** Licenses.Revoke / Activate, Devices.Revoke, CrashReports.Delete, all read-only ops (unless `users.is_readonly = true` in which case all destructive blocked).

Permission keys: `tab:configuration`, `tab:ipwhitelist`, `tab:updates`, `tab:rolechange`, `action:users.delete`, `action:users.ban`, `action:subscriptions.grant`, `action:subscriptions.revoke`, `action:payments.approveCrypto`, `action:payments.rejectCrypto`. Namespace allows future expansion.

**Permission storage:**
- `permission_grants` table: active grants per (admin, permission_key) with optional `expires_at` and soft-revoke.
- `permission_requests` table: history of requests (pending/approved/denied/cancelled) with admin's reason text + superadmin's optional `review_note`.
- Unique constraint: 1 pending request per (admin, permission) at a time + 1 active grant per (admin, permission).

**Permission request/approval flow:**
- Admin clicks locked tab or locked action → modal with mandatory "Why do you need access?" textarea (50-500 chars) → submit creates `permission_requests` row + emits SignalR `PermissionRequested` to superadmins + sends email to superadmin.
- Superadmin sees pending requests in dedicated "Permission Requests" tab → Approve (with optional `expires_at` + note) creates `permission_grants` row, emits `PermissionApproved` to specific admin via SignalR + email; Deny updates request status, emits `PermissionDenied` + email. Bulk approve/deny supported.

**Admin lifecycle (superadmin tooling):**
- **Create Admin Account** modal: email + "Send invitation email" toggle (default ON). If ON: backend generates one-time setup token (7-day TTL, SHA256 stored in `admin_invitations`), emails link to admin who sets own password (initial password never emailed). If OFF: superadmin manually sets initial password + shares out-of-band. Plus: force-password-change policy (4 options) + permission template (Default/Trusted/ReadOnly/Custom) + 2FA requirement toggle.
- **Reset Admin Password** — new reset link emailed; force_password_change applies on next login.
- **Suspend / Restore Admin** — `is_active` flag; suspended admins blocked at login + active sessions invalidated via `revoked_tokens` table; reversible (data preserved).
- **Promote existing user** — alternative path: pick an existing `user`, promote to `admin` with template choice. User keeps existing password (no initial password needed). Notification email sent.
- **Delete Admin** — Tier 2 action requiring `action:users.delete`; hard delete with cascade (permission_grants, permission_requests, revoked_tokens, admin_invitations all cascade).

**Permission templates (4 presets):**
- **Default** — no extra grants; Tier 3 destructive open by default; all read-only tabs.
- **Trusted** — Default + all Tier 2 actions unlocked.
- **Read-Only** — `users.is_readonly = true` flag set; all destructive blocked server-side; frontend hides destructive buttons.
- **Custom** — superadmin manually picks per-tab visibility + per-action permission + per-permission `expires_at`. Saved as multiple `permission_grants` rows directly.

**Force password change policy** (4 options at admin creation or password reset):
1. **On first login** (default — most secure).
2. **Within 7 days** (short grace period).
3. **Within 30 days** (NIST corporate pattern).
4. **Never** (admin discretion).

**2FA enforcement policy (hybrid):**
- **Global toggle** (`system_settings['require_2fa_for_all_admins']`) — applies to all admin accounts.
- **Per-account override** (`users.require_2fa`) — superadmin can require 2FA on individual admins even if global is off.
- Resolution: `requires_2fa = (role == 'superadmin') OR global_enabled OR account_override`.
- If `requires_2fa AND NOT user.totp_enabled` at login: scope-limited JWT (claim `scope: '2fa-setup-only'`, 15-min TTL) returned + frontend redirects to `/enable-2fa`. Middleware enforces scope — only `/api/auth/enable-2fa` + `/api/auth/logout` accept scope-limited tokens.
- Superadmin row in Security Policy tab: read-only "2FA required (always)" — cannot toggle off.

**Superadmin-exclusive views (only rendered when role='superadmin'):**
- **Permission Requests** — inbox + history; real-time updates; approve/deny dialogs; bulk actions.
- **Admin Action Log** — `audit_log` filtered view: rows where `actor_user.role = 'admin'`; per-admin / action-type / date-range filters; KPIs (total, last 24h, last 7d, top 5 admins, top 5 actions); live SignalR appends; CSV export.
- **Admin Management** — list + create new admin + edit permissions + reset password + suspend/restore + delete admin accounts.
- **Role Change UI (skeleton)** — promote user→admin / demote admin→user (single-user flow; bulk operations deferred to Phase 6.12).
- **Security Policy** — global 2FA toggle + per-account 2FA override list + force-password-change defaults.
- **API Rate Limits** — read + edit per-endpoint-group rate limit policies (auth.login, auth.register, admin.*, signalr.*) stored in `system_settings` and hot-reloaded into ASP.NET Core RateLimiter.

**"My Permissions" page** (admin self-service, accessible via user dropdown menu — not in main nav):
- Summary card: "You have access to {N} of {M} restricted permissions."
- Active grants table: Permission | Granted by | Granted at | Expires at | Source.
- Pending requests table with Cancel button.
- Recent denials/revocations table with note.

**Email notifications (transactional via Resend):**
- Refactor `PasswordResetController.cs:146-168` inline HTTPS into `IEmailService` interface + `ResendEmailService` implementation using `IHttpClientFactory` + structured logging + typed `EmailSendResult` DTO.
- 6 transactional types: `AdminInvitation`, `PasswordReset`, `PermissionRequested` (to superadmin), `PermissionApproved`, `PermissionDenied`, `AdminCreatedWithoutEmail` (fallback when invite-toggle is off).
- All emails use `noreply@auracore.pro` from-address + shared HTML template matching admin panel aesthetic.
- DNS fix (Wave 6 ops): add `include:_spf.resend.com` to `auracore.pro` SPF TXT record (current SPF authorizes only Namecheap Private Email; Resend outbound risks SPF alignment failure).

**Audit log CSV export:**
- New endpoints: `GET /api/admin/audit-log/export.csv?dateFrom=&dateTo=&actorEmail=&action=` (admin scope) + `GET /api/superadmin/admin-actions/export.csv?...` (superadmin scope, role-filtered).
- Streaming via `IAsyncEnumerable<AuditLogEntry>` + `Response.Body.WriteAsync` (avoids memory spike on large exports).
- Filter-aware (respects same query params as list endpoints), RFC 4180 CSV.
- Rate limit: 10 exports/hour/user.
- Buttons on both AuditLog (admin panel) and AdminActionLog (superadmin) tabs.

**API rate limit configuration UI:**
- Backend `RateLimitConfigService`: reads `system_settings['rate_limit_policies']` JSON on startup, caches in memory (5-min TTL), hot-reloads ASP.NET Core RateLimiter policies on update.
- Superadmin "API Rate Limits" tab: per-endpoint-group config rows (endpoint name | requests | window seconds | last updated | edit button).
- Edit dialog → PUT `/api/superadmin/rate-limits/{endpoint}` → cache invalidated + policies reconfigured atomically + new limits effective immediately (no deploy).

**Backend authorization mechanism:**
- Existing `[Authorize(Roles = "admin")]` stays (admin + superadmin both pass via role inheritance).
- New `[RequiresPermission("permission_key")]` attribute filter — runs after `[Authorize]`. Superadmin always passes; admin checks `permission_grants` for active non-expired non-revoked entry; ReadOnly admin (`is_readonly=true`) fails for any non-`tab:*` permission. No match returns 403 with `{ "error": "permission_required", "permission": "<key>" }`. Frontend reads body to know which permission to request.

**SignalR events** (additions to AdminHub from Phase 6.10):
- `PermissionRequested` (to superadmins group) — `{ adminEmail, permissionKey, reason, requestedAt }` + tab badge counter.
- `PermissionApproved` / `PermissionDenied` / `PermissionRevoked` (targeted to specific admin via `Clients.User(userId)`) — frontend toast + tab state refresh.

**Grandfather migration:**
- On first deploy with Phase 6.11 code, backend startup runs idempotent migration: for every existing `role='admin'` user with zero `permission_grants` rows, create "Trusted" template grants. Prevents existing admin (`admin@auracore.pro`) from being locked out.

### Out of scope — deferred

**Phase 6.12** (queued):
- 15 deferred Low audit findings.
- Role Change UI fleshed out (bulk operations, multi-admin transitions).
- TOTP backup codes, blockchain explorer link, online/offline device indicator, PII erasure UI.
- audit_log retention dashboard.
- SignalR enhancements (presence, typing, multi-admin awareness).
- Active Admin Sessions Monitor (force-logout).
- Advanced rate limit (per-user overrides, IP denylists).
- Optional: separate `superadmin.auracore.pro` subdomain hardening if threat model evolves (deferred per Q9 — current spec uses single subdomain since Cert Transparency defeats URL secrecy + RN app sees no subdomain benefit).

**Phase 6.13** (queued):
- Hotfix debt sweep: error-swallow cleanup, backend HTTP method audit, frontend `any` cast cleanup, nginx sites-enabled drift, brute-force lockout UX, PWA real icons, Vitest broader coverage, CI/CD deploy pipeline.

**Phase 6.14** (queued):
- React Native admin mobile app (full feature parity, supports both admin + superadmin login via in-app radio toggle, leverages 6.11 role/permission system).

**6.11 explicitly NOT included:**
- Per-permission usage-count limits ("expires after N uses") — `expires_at` covers time-based; use-count is 6.12+.
- Email/Slack/SMS beyond the 6 transactional types.
- Customizable permission key creation by superadmin (keys are hardcoded enum).
- Multi-tenant superadmin (single-org assumption).
- Role hierarchy beyond 3 levels.
- Email delivery receipts / bounce handling UI (Resend dashboard covers).
- Audit log retention/archival policy.
- Captcha on login (Phase 6.12+ polish — backend rate limit sufficient now).

## Design decisions

### D1 — 3-tier permission model

| Tier | Mechanism | Members | UX |
|---|---|---|---|
| **Tier 1** | Tab-level gating | Configuration, IP Whitelist, Updates, Role Change | Tab visible in nav; click → locked-page placeholder + Request button |
| **Tier 2** | Action-level gating | Users.Delete, Users.Ban, Subscriptions.Grant, Subscriptions.Revoke, Payments.ApproveCrypto, Payments.RejectCrypto | Tab open; specific buttons rendered with lock icon + disabled; click → request modal |
| **Tier 3** | No gating (unless ReadOnly template) | Licenses.Revoke, Licenses.Activate, Devices.Revoke, CrashReports.Delete, all read-only ops | Normal admin behavior, unless `users.is_readonly = true` |

### D2 — Auth model: separate endpoint, single subdomain

`/api/auth/superadmin/login`:
- Accepts same payload (email, password, optional totpCode).
- Validates user exists AND `role = 'superadmin'` (else 403 — never reveals account existence).
- Stricter rate limit: 3 fails → 60-min lockout (vs admin: 5 / 30 min).
- Audit-logged with action `'SuperadminLoginAttempt'` regardless of outcome.
- Response shape identical (JWT + refresh token).
- Mandatory 2FA — first login after bootstrap returns scope-limited JWT + redirect to `/enable-2fa`. No bypass.

Frontend `LoginScreen` has two submit buttons:
- **"Sign In as Admin"** (default form styling) → posts to `/api/auth/login`.
- **"Sign In as Superadmin"** (cyan-purple gradient) → posts to `/api/auth/superadmin/login`.

Post-login: frontend reads JWT role claim. If `role='superadmin'`, renders panel with extra superadmin-only tabs visible in nav (Permission Requests, Admin Action Log, Admin Management, Role Change, Security Policy, API Rate Limits). If `role='admin'`, renders standard admin panel; superadmin-only tabs are excluded from `NAV_GROUPS` array entirely (not rendered, not requestable). Conditional in `src/app/page.tsx`:
```tsx
const NAV_GROUPS = currentUser.role === 'superadmin' ? SUPERADMIN_NAV_GROUPS : ADMIN_NAV_GROUPS;
```

Single bundle. The trade-off: superadmin code paths exist in the JS bundle that admin browsers download (admin can DevTools-inspect). Acceptable — code visibility ≠ access; backend `[RequiresPermission]` and `role='superadmin'` checks are the authoritative authorization layer. Bundle inspection reveals UI structure but not credentials/data.

`admin.auracore.pro` nginx config: **basic auth removed** (admin panel goes public-reachable). `robots.txt` (`Disallow: /`) + `<meta name="robots" content="noindex,nofollow">` prevent search engine indexing. Backend login + 2FA + rate-limit are the security layers.

### D3 — Superadmin bootstrap

Backend startup reads `SUPERADMIN_EMAILS` env var (comma-separated) and runs idempotent upsert:
```sql
UPDATE users SET role = 'superadmin' WHERE email = ANY(@emails) AND role != 'superadmin';
```
Only promotes existing registered users. Initial value: `SUPERADMIN_EMAILS=ozgurdeniz807@gmail.com`. On first login post-bootstrap, if `totp_enabled=false`, scope-limited JWT issued + redirect to `/enable-2fa` — mandatory.

Demotion is NOT auto-managed by env var changes (would be too dangerous on accidental config edit). Demotion is a manual operation via Role Change UI.

### D4 — Storage schema

```sql
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

CREATE TABLE permission_requests (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    admin_user_id   UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    permission_key  VARCHAR(100) NOT NULL,
    reason          TEXT NOT NULL,
    requested_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    status          VARCHAR(20) NOT NULL DEFAULT 'pending', -- pending|approved|denied|cancelled
    reviewed_by     UUID NULL REFERENCES users(id),
    reviewed_at     TIMESTAMPTZ NULL,
    review_note     TEXT NULL
);
CREATE INDEX ix_permission_requests_status_admin
  ON permission_requests(status, admin_user_id);
CREATE UNIQUE INDEX uq_permission_requests_pending
  ON permission_requests(admin_user_id, permission_key)
  WHERE status = 'pending';

CREATE TABLE revoked_tokens (
    jti           VARCHAR(100) PRIMARY KEY,    -- JWT unique ID claim
    user_id       UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    revoked_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    revoked_by    UUID NULL REFERENCES users(id),
    revoke_reason VARCHAR(100) NOT NULL         -- 'suspend'|'password_reset'|'logout_all'|'admin_deleted'
);
CREATE INDEX ix_revoked_tokens_user ON revoked_tokens(user_id);

CREATE TABLE admin_invitations (
    token_hash    VARCHAR(100) PRIMARY KEY,    -- SHA256 of the token sent in email
    admin_user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    created_by    UUID NOT NULL REFERENCES users(id),
    created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at    TIMESTAMPTZ NOT NULL,
    consumed_at   TIMESTAMPTZ NULL
);
CREATE INDEX ix_admin_invitations_user ON admin_invitations(admin_user_id);
```

**Users table additions:**
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

**System settings table** (new generic key-value store):
```sql
CREATE TABLE IF NOT EXISTS system_settings (
    key         VARCHAR(100) PRIMARY KEY,
    value       TEXT NOT NULL,
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by  UUID NULL REFERENCES users(id)
);
INSERT INTO system_settings (key, value) VALUES
    ('require_2fa_for_all_admins', 'false'),
    ('rate_limit_policies', '{"auth.login":{"requests":5,"windowSeconds":1800},"auth.register":{"requests":3,"windowSeconds":3600},"admin.all":{"requests":1000,"windowSeconds":3600},"signalr.connect":{"requests":10,"windowSeconds":60}}');
```

### D5 — `[RequiresPermission]` attribute

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
        if (role != "admin") { context.Result = new ForbidResult(); return; }

        var userId = Guid.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();

        // ReadOnly admins fail for all destructive actions (Tier 2 and Tier 3)
        var isReadonly = await db.Users.Where(u => u.Id == userId).Select(u => u.IsReadonly).FirstOrDefaultAsync();
        if (isReadonly && !Permission.StartsWith("tab:"))
        {
            context.HttpContext.Response.StatusCode = 403;
            await context.HttpContext.Response.WriteAsJsonAsync(new { error = "readonly_account", permission = Permission });
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
            await context.HttpContext.Response.WriteAsJsonAsync(new { error = "permission_required", permission = Permission });
            context.Result = new EmptyResult();
        }
    }
}
```

Applied as: `[Authorize(Roles = "admin")] [RequiresPermission("action:users.delete")]`. The `[Authorize]` filter runs first (basic auth check), then `[RequiresPermission]` runs (permission check).

For Tier 3 destructive actions (not attribute-gated by default), add a small `[DestructiveAction]` marker attribute that consults `is_readonly` flag at action entry — keeps logic centralized.

### D6 — Permission templates

Backend constants:
```csharp
public static class PermissionTemplates
{
    public const string Default = "Default";
    public const string Trusted = "Trusted";
    public const string ReadOnly = "ReadOnly";
    public const string Custom = "Custom";

    public static IReadOnlyList<string> GetPermissionsForTemplate(string t) => t switch
    {
        Default  => Array.Empty<string>(),
        Trusted  => new[] {
            "action:users.delete", "action:users.ban",
            "action:subscriptions.grant", "action:subscriptions.revoke",
            "action:payments.approveCrypto", "action:payments.rejectCrypto"
        },
        ReadOnly => Array.Empty<string>(),
        Custom   => throw new InvalidOperationException("Custom is configured per-grant"),
        _ => throw new ArgumentException($"Unknown template: {t}"),
    };

    public static bool RequiresIsReadonlyFlag(string t) => t == ReadOnly;
}
```

**Custom template UX** (Create Admin modal, when "Custom" selected):
- Tab checkboxes (per Tier 1 tab) — superadmin picks which Tier 1 tabs unlock.
- Action checkboxes (per Tier 2 action) — superadmin picks which Tier 2 actions unlock.
- Per-permission `expires_at` date picker (default: never) + "Apply same expiry to all" shortcut.
- ReadOnly override checkbox ("This admin is read-only — block Tier 3 destructive actions too").
- Submit creates individual `permission_grants` rows (one per checked permission).

### D7 — Force password change flow

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

Frontend: if `requiresPasswordChange === true`, redirect to `/change-password` view. Server-side middleware: authenticated user with `force_password_change=true` AND `force_password_change_by < now()` → all endpoints (except change-password, logout) return 403 `{ "error": "password_change_required" }` → frontend catches + redirects.

The `/change-password` endpoint validates current password (bypass-able if force flag is true and deadline passed — emergency unlock). On success: updates password, clears flag, invalidates refresh token, returns new JWT.

### D8 — 2FA enforcement policy

Login flow:
1. Validate credentials (email + password + rate-limit + lockout checks).
2. Check `is_active`: false → 403 `{ "error": "account_suspended" }`.
3. If `role in ('admin', 'superadmin')`:
   - `requires_2fa = (role == 'superadmin') OR system_settings['require_2fa_for_all_admins'] OR user.require_2fa`.
   - If `requires_2fa AND NOT user.totp_enabled`:
     - Issue scope-limited JWT (claim `scope: '2fa-setup-only'`, 15-min TTL).
     - Response: `{ requiresTwoFactorSetup: true, accessToken (scope-limited), ... }`.
     - Frontend redirects to `/enable-2fa`.
   - Else: continue normal TOTP flow.

Middleware enforces scope: endpoints other than `/api/auth/enable-2fa` + `/api/auth/logout` reject scope-limited tokens with 403 `{ "error": "scope_limited_token" }`.

Superadmin Security Policy tab:
- Global toggle: "Require 2FA for all admin accounts" → updates `system_settings`.
- Per-account list: shows all admin accounts with toggle "Require 2FA on this account" → updates `users.require_2fa`.
- Read-only row for superadmin accounts: "2FA required (always)" — cannot toggle off.

### D9 — Admin Action Log + CSV export

Backend endpoints:
- `GET /api/superadmin/admin-actions?page=&pageSize=&actorEmail=&action=&targetType=&dateFrom=&dateTo=` — paginated list + filter.
- `GET /api/superadmin/admin-actions/stats` — KPIs (total, last 24h, last 7d, top 5 admins, top 5 actions).
- `GET /api/admin/audit-log/export.csv?dateFrom=&dateTo=&actorEmail=&action=` — CSV stream, admin scope.
- `GET /api/superadmin/admin-actions/export.csv?...` — CSV stream, superadmin scope (filtered to admin role).

CSV format (RFC 4180):
```
"id","actor_email","actor_id","action","target_type","target_id","ip_address","created_at_utc"
```

Streaming via `IAsyncEnumerable<AuditLogEntry>` + chunked `Response.Body.WriteAsync` to avoid memory spikes.

Authorization:
- Admin-side export: requires `tab:auditlog` (Tier 3 open by default).
- Superadmin-side export: requires `role = 'superadmin'`.

Rate limit: 10 exports/hour/user.

### D10 — API rate limit config UI

Backend `RateLimitConfigService`:
- Reads `system_settings['rate_limit_policies']` JSON on startup, caches in `IMemoryCache` (5-min TTL).
- On config update via PUT endpoint: serializes new JSON, persists, invalidates cache, calls `_rateLimiter.Reconfigure(current)` to atomically apply new policies.
- Hot-reload — new limits effective without restart.

UI (superadmin "API Rate Limits" tab):
- Table: Endpoint | Requests | Window (s) | Last Updated | Edit button.
- Edit dialog: number inputs + Apply button → PUT `/api/superadmin/rate-limits/{endpoint}`.

### D11 — Email infrastructure

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
    AdminInvitation,           // { adminEmail, setupLink, expiresAt }
    PasswordReset,             // { email, resetLink, expiresAt }
    PermissionRequested,       // (to superadmin) { adminEmail, permissionKey, reason, inboxLink }
    PermissionApproved,        // (to admin) { permissionKey, approvedBy, expiresAt? }
    PermissionDenied,          // (to admin) { permissionKey, deniedBy, reviewNote? }
    AdminCreatedWithoutEmail,  // (to superadmin) { adminEmail, initialPassword, note }
}
```

**Implementation** (`src/Backend/AuraCore.API.Infrastructure/Services/Email/ResendEmailService.cs`):
- `IHttpClientFactory` named client `"resend"` → `https://api.resend.com`.
- Reads `RESEND_API_KEY` from `IConfiguration`.
- Logs request + response status + message ID + error body.
- Returns typed `EmailSendResult` — callers decide retry/error UX.
- Templates: 6 HTML files in `src/Backend/AuraCore.API.Infrastructure/Services/Email/Templates/` with `{{placeholder}}` markers + shared `_base.html` layout.

**Refactor:**
- `PasswordResetController.cs:146-168` inline HTTPS → `await _emailService.SendFromTemplateAsync(EmailTemplate.PasswordReset, ...)`.
- All Phase 6.11 notifications use `_emailService`.

**DI** (`Program.cs`):
```csharp
builder.Services.AddHttpClient("resend", c => {
    c.BaseAddress = new Uri("https://api.resend.com");
    c.DefaultRequestHeaders.Add("Authorization", $"Bearer {Environment.GetEnvironmentVariable("RESEND_API_KEY")}");
});
builder.Services.AddScoped<IEmailService, ResendEmailService>();
```

**DNS fix** (Wave 6 ops): update `auracore.pro` SPF TXT:
```
v=spf1 include:spf.privateemail.com include:_spf.resend.com ~all
```

### D12 — Admin Account Creation flow

**New superadmin "Admin Management" tab:**
- List: email | created_at | created_by | last_login | is_active | template | 2FA | actions.
- Per-row actions: Edit Permissions / Reset Password / Suspend (or Restore) / Delete.
- Top button: **+ Create Admin Account**.

**Create modal:**
1. **Email** (required, format + uniqueness validation).
2. **Send invitation email** toggle (default ON):
   - ON: no password input shown; admin receives invite link to set own password.
   - OFF: "Initial password" input + "Generate Strong" (16-char alphanum+symbol) + Show/Hide + Copy.
3. **Force password change** dropdown (4 options, default "On first login").
4. **Permission template** dropdown (Default / Trusted / Read-Only / Custom — Custom expands the picker described in D6).
5. **Initial 2FA requirement** checkbox (auto-checked + disabled if global 2FA policy is on).
6. **Create** → POST `/api/superadmin/admins/create` → backend creates user, hashes password (if set), generates `permission_grants` from template, sends invite email (if toggle ON) with token link.

**Promote-existing-user flow:**
- Users tab → existing `user` row → "Promote to Admin".
- Modal: template + force change + 2FA requirement (no password — user keeps existing).
- Backend updates `role='admin'`, applies template, sends notification email.

**Invitation token flow:**
- Backend generates 256-bit cryptographic token → stores SHA256 in `admin_invitations` (7-day expiry).
- Email link: `https://admin.auracore.pro/invite?token=<raw>&email=<urlencoded>`.
- Admin clicks link → frontend "Welcome! Set your password" form → POST `/api/auth/redeem-invitation` with token + new password → backend verifies hash, sets password, marks token consumed, returns JWT.
- Token is one-time-use; subsequent attempts return 410 Gone.

### D13 — Suspend/restore + delete

**Suspend:**
- `users.is_active = false`.
- Active tokens revoked: insert all non-expired JWT `jti` claims into `revoked_tokens`.
- Login attempt: 403 `{ "error": "account_suspended" }`.
- Existing API requests with valid token: middleware checks `revoked_tokens` → 401 `{ "error": "token_revoked" }`.

**Restore:** `is_active = true`. User can log in again.

**Delete:** Tier 2 action requiring `action:users.delete`. Hard delete with cascade (permission_grants, permission_requests, revoked_tokens, admin_invitations all cascade).

### D14 — "My Permissions" page (admin self-service)

`src/views/MyPermissionsPage.tsx` (visible to all admins via user dropdown menu — not in main nav):
- Summary card: "You are an admin (`{email}`). You have access to {N} of {M} restricted permissions."
- Active grants table: Permission | Label | Granted by | Granted at | Expires at | Source.
- Pending requests table: Permission | Label | Requested at | Reason | Cancel button.
- Recent denials/revocations table: Permission | Label | Reviewed by | Note | When.

### D15 — UX for locked tabs and actions

**Locked tab page** (`src/components/LockedTabPlaceholder.tsx`): centered lock icon + the spec'd message + Request Permission button. If pending request: "Pending request from {requestedAt}, awaiting review". If recently denied: "Last request denied: {reviewNote || 'no reason given'}".

**Locked action button:** existing destructive button gets `disabled` + lock icon when admin lacks permission. `title` tooltip "This action requires superadmin permission. Click to request." On click → opens request modal.

**Request modal** (`src/components/PermissionRequestDialog.tsx`):
- Title: "Request access to {permissionLabel}".
- Body: "Why do you need this permission?" textarea (50-500 chars, char counter visible).
- Footer: Cancel / Submit Request.
- Submit → POST `/api/admin/permission-requests` → toast "Request sent to superadmin" + close modal. Duplicate detection: if pending request exists for same permission → banner "You already have a pending request."

**Permission Requests inbox** (superadmin tab `src/views/PermissionRequestsPage.tsx`):
- Table: admin email | permission | reason (truncated, click to expand) | requested at | status | actions.
- Status filter (default: pending). Bulk actions: select multiple → "Approve Selected" / "Deny Selected".
- Per-row: Approve dialog (optional `expires_at` picker + optional review_note) / Deny dialog (optional review_note).
- Real-time: SignalR `PermissionRequested` → table prepend + tab badge increment.

### D16 — Indexing protection

`admin-panel/public/robots.txt`:
```
User-agent: *
Disallow: /
```
`admin-panel/src/app/layout.tsx` adds to metadata:
```tsx
export const metadata: Metadata = {
    // ... existing fields
    robots: { index: false, follow: false },
};
```
Both bundles have this (admin-panel is a single bundle in Phase 6.11 per Q9 decision).

### D17 — SignalR events summary

Existing from Phase 6.10: `UserRegistered`, `UserLogin`, `Payment`, `CrashReport`, `Telemetry`, `AdminCount`.

**New in 6.11:**
- `PermissionRequested` (to superadmins group) — tab badge counter + toast.
- `PermissionApproved` (to specific admin via `Clients.User(userId)`) — toast + tab state refresh.
- `PermissionDenied` (to specific admin) — toast with optional note.
- `PermissionRevoked` (to specific admin) — toast + tab state refresh.

## Architecture — Wave breakdown

**Wave 1 — DB schema + auth foundation:**
- EF migration: permission_grants + permission_requests + revoked_tokens + admin_invitations + users field additions + system_settings + seeds.
- Backend: `superadmin` role enum + `/api/auth/superadmin/login` + bootstrap from `SUPERADMIN_EMAILS` + grandfather migration (Trusted template for existing admins).
- Backend: `[RequiresPermission]` attribute + filter + `[DestructiveAction]` marker for Tier 3 + JWT scope claim validation.
- Backend: token revocation middleware + `revoked_tokens` lookup on every authenticated request.
- Backend tests: attribute behavior + bootstrap idempotency + grandfather migration + superadmin login auth + scope claim enforcement.

**Wave 2 — Permission system + Tier 1/2 application + email service refactor:**
- Backend: permission grants CRUD endpoints (admin lists own, superadmin lists all + create/revoke).
- Backend: permission requests CRUD endpoints (admin create + list own + cancel; superadmin list pending + approve + deny + bulk actions).
- Backend: `[RequiresPermission]` applied to Tier 1 tab mutation endpoints + 6 Tier 2 action endpoints.
- Backend: SignalR new events (4 types).
- Backend: refactor `PasswordResetController.cs` inline HTTPS into `IEmailService` + `ResendEmailService` + 6 HTML templates + `IHttpClientFactory` registration.
- Backend tests: grant lifecycle + request flow end-to-end + permission filter + email service unit tests (mock IHttpClientFactory).

**Wave 3 — Frontend role-aware shell + locked-tab/action UX + nginx public-cut:**
- Frontend: post-login role detection + conditional `NAV_GROUPS` (admin vs superadmin tab set).
- Frontend: LoginScreen "Sign In as Superadmin" button (cyan-purple gradient, posts to superadmin endpoint).
- Frontend: LockedTabPlaceholder component + locked-button rendering in views with Tier 2 actions.
- Frontend: PermissionRequestDialog component.
- Frontend: useSignalR additions for permission events + targeted user notifications.
- Frontend: `public/robots.txt` (Disallow: /) + `<meta name="robots" content="noindex,nofollow">` in layout.tsx metadata.
- Ops: **Remove basic auth** from `admin.auracore.pro` nginx config (admin panel goes public-reachable).
- Ops: SPF TXT record fix (add `include:_spf.resend.com`) — early in Wave 3 so deliverability is good before Wave 4 sends invitation emails.
- Frontend tests: LockedTabPlaceholder render + PermissionRequestDialog interaction + role-based NAV_GROUPS conditional.

**Wave 4 — Superadmin tabs (Permission Requests + Admin Action Log + Admin Management + Role Change skeleton):**
- Frontend: PermissionRequestsPage (table + approve/deny dialogs + bulk actions + SignalR live updates + tab badge).
- Frontend: AdminActionLogPage (filters + KPIs + table + SignalR live appends + CSV export button).
- Frontend: AdminManagementPage (list + create modal + edit permissions modal + reset password + suspend/restore + delete).
- Frontend: Role Change UI skeleton (promote user → admin, demote admin → user — single user flow).
- Backend: superadmin endpoints — admins/create, reset-password, suspend, restore, users/{id}/promote, admins/{id}/demote.
- Backend: admin-actions list + stats + CSV export (streaming).
- Backend: admin_invitations + redeem-invitation endpoint + invite email template.
- Backend tests: admin lifecycle endpoints + invite flow + CSV streaming + bulk approve/deny.

**Wave 5 — Templates + force-change + 2FA enforcement + Security Policy tab + API Rate Limits tab:**
- Backend: PermissionTemplates logic (Default / Trusted / ReadOnly / Custom) + is_readonly handling in [RequiresPermission] + Tier 3 destructive-action check via [DestructiveAction] marker.
- Backend: force password change middleware + deadline enforcement + change-password endpoint.
- Backend: 2FA enforcement logic in login flows + scope-limited JWT issuance + scope validation middleware.
- Backend: RateLimitConfigService + DB-backed policies + hot-reload + superadmin PUT endpoint.
- Frontend: SecurityPolicyPage (superadmin) — global 2FA toggle + per-account override list.
- Frontend: APIRateLimitsPage (superadmin) — table + edit dialog + apply button.
- Frontend: change-password view + enable-2fa view (reuse existing 2FA setup logic; ensure scope-limited JWT works).
- Tests: template grant generation + force change middleware + 2FA enforcement resolution + rate limit hot-reload.

**Wave 6 — My Permissions + final polish + deploy + ceremonial:**
- Frontend: MyPermissionsPage (admin self-service, accessed via user dropdown menu).
- Frontend: final polish (toast notifications + responsive layouts).
- Ops: deploy admin-panel + backend to prod (single bundle deploy — same scp path as Phase 6.10).
- Final full-suite test + memory file + MEMORY.md update + ceremonial merge to main + push to origin (user-gated).

## Testing strategy

- **Backend** (xUnit): ~30-40 tests — [RequiresPermission] attribute, bootstrap idempotency, grandfather migration, superadmin login (success + failures), permission request lifecycle, approval flow, grant expiration, is_readonly enforcement, template generation, force change middleware, 2FA enforcement ladders, RateLimitConfigService, admin lifecycle, invitation token redemption, CSV streaming, IEmailService (mocked).
- **Frontend** (Vitest + RTL): ~15-20 tests — LockedTabPlaceholder, PermissionRequestDialog (validation, submit), PermissionRequestsPage interactions, AdminManagementPage create modal, MyPermissionsPage render, role-based conditional NAV_GROUPS.
- **Skipped:** End-to-end Playwright (Phase 6.13+); manual smoke for visual layout.
- **Target:** ~2392 → ~2440-2470 (+50-80 net new tests).

## Deployment flow

**Mid deploy (after Wave 2):** backend with schema + permission attribute + IEmailService refactor. Smoke: superadmin login curl, create permission grant against test admin, verify [RequiresPermission] returns 403 on unprivileged request.

**Final deploy (after Wave 6):** admin-panel frontend rebuild + backend full rebuild. Smoke: full end-to-end — login as admin (Tier 1 tabs locked), click Request Permission, superadmin receives SignalR + email, login as superadmin, approve request, admin's locked tab unlocks live via SignalR.

DB migrations run via standard EF Core migration in mid-deploy.

## Open questions / known risks

- **Grandfather migration triggers on every backend startup:** Must be idempotent (check if grants exist for the user before inserting). Alternative: use EF migration framework directly (one-time run via `__EFMigrationsHistory`).
- **Scope-limited JWT vs SignalR:** Scope-limited tokens (for 2FA setup) must NOT allow `/hubs/admin` WebSocket connection. SignalR auth handler checks scope claim and rejects if `scope: '2fa-setup-only'`. Verified in Wave 5 tests.
- **Admin browser sees superadmin code paths in JS bundle:** Single-bundle approach (per Q9) trade-off. Acceptable — code visibility ≠ access; backend `[RequiresPermission]` + `role='superadmin'` checks are authoritative. Bundle reveals UI structure, not credentials/data.
- **SPF TXT record propagation:** SPF DNS edit is propagation-sensitive. Wave 3 deploys SPF update early + monitors DMARC reports for 24h before Wave 4 starts sending invitation emails.
- **Existing admin sessions during 2FA enforcement enable:** When superadmin toggles global 2FA, already-logged-in admins keep valid sessions until token expires. Their next login hits 2FA gate. Optional Phase 6.12 polish: "Force re-auth all admins" button to revoke all admin tokens immediately.
- **CORS allowlist:** Single subdomain — `admin.auracore.pro` already in allowlist; no change needed.

## Decision log

| Decision | Chosen | Rejected | Why |
|---|---|---|---|
| Scope decomposition | 6.11 Superadmin foundation; 6.12 features; 6.13 debt; 6.14 RN app | Single mega-phase | Foundational change — every later feature builds on it |
| Permission model | 3-tier (tab + action + free) | Pure tab / pure action | Mixed model fits real risk profile |
| Auth isolation | Separate `/api/auth/superadmin/login` endpoint, single `admin.auracore.pro` subdomain | Subdomain ayrımı (B) | Per Q9: subdomain offers marginal extra security defeated by Cert Transparency public logs; double the build/deploy/nginx maintenance cost; RN app sees no benefit from subdomain isolation; single bundle simpler for native parity in Phase 6.14 |
| Bootstrap | Env var `SUPERADMIN_EMAILS` idempotent on startup | Manual DB seed / first-user pattern | Reproducible, source-controlled |
| ReadOnly enforcement | `users.is_readonly` boolean | Denial-grants in permission_grants | Simpler — single column, clear semantics |
| Storage | `permission_grants` + `permission_requests` tables | JSON column on users | Indexable, typed, separate lifecycle |
| Locked-tab UX | In-place placeholder + Request button | 404-style hidden tab | Admin sees what's missing, can request |
| Email provider | Keep Resend; refactor into IEmailService | Switch to SendGrid / Mailgun / SES | Already in use; HTTPS:443 works on DO; credential seeded; SMTP ports 25/587/465 all blocked on DO |
| Onboarding | Invitation token link (default) + manual password fallback | Always-manual password | Initial password never in email; one-shot token; manual fallback for unavailable email |
| Rate limit storage | DB `system_settings` JSON + memory cache + hot reload | Hardcoded constants / appsettings.json | Runtime editable without deploy; superadmin self-service |
| Grandfather strategy | Trusted template for existing admins | Default template / no grants | User has 1 admin account (self); Trusted preserves capability |
| Superadmin 2FA | Mandatory on first login (no toggle) | Optional per-superadmin | Privileged role; no bypass |
| Admin nginx basic auth | Removed (admin panel public) | Keep / multi-user htpasswd | Modern auth = MFA + lockout + password policy is sufficient; basic auth ≠ app identity, double-auth UX is poor |
| Search engine indexing | robots.txt Disallow:/ + noindex meta | No protection | Cheap, standard, sufficient for non-public admin tooling |

## Non-goals

- Multi-tenant superadmin (single-org).
- Role hierarchy beyond 3 levels.
- Customizable permission key creation by superadmin (keys are hardcoded enum).
- Per-permission usage-count limits (e.g., "expires after 10 uses").
- Advanced rate limit (per-user overrides, IP denylists — Phase 6.12+).
- Email delivery receipts / bounce handling UI (Resend dashboard covers).
- Audit log retention/archival policy (Phase 6.12+).
- Slack/Teams/SMS notification channels.
- Captcha on login (Phase 6.12+ polish).
- Subdomain isolation (deferred to Phase 6.12+ or beyond if threat model evolves).

## Success criteria

Phase 6.11 is DONE when:

- `superadmin` role exists in `users.role`; bootstrap from `SUPERADMIN_EMAILS` env var works idempotently.
- `/api/auth/superadmin/login` endpoint live with stricter rate limit + mandatory 2FA + audit logging.
- LoginScreen has "Sign In as Superadmin" button (cyan-purple gradient) that posts to the superadmin endpoint.
- Post-login: `role='superadmin'` renders panel with extra superadmin-only tabs; `role='admin'` renders standard admin panel without those tabs in nav.
- 4 Tier 1 tabs render LockedTabPlaceholder for unprivileged admins with "Request Permission" button; all 4 tabs' mutation endpoints reject admin requests with `permission_required` 403.
- 6 Tier 2 action buttons render with lock icon for unprivileged admins; click opens PermissionRequestDialog; submit creates a permission_request row.
- Superadmin's Permission Requests tab shows pending requests; Approve creates permission_grant + emits `PermissionApproved` SignalR + sends email; Deny updates status + emits `PermissionDenied` + sends email. Bulk approve/deny works.
- Admin Action Log tab shows audit_log filtered by `actor_user.role='admin'`, with filters + KPIs + live SignalR appends. Both admin-side and superadmin-side CSV export work (streaming, filter-aware).
- Admin Management tab supports: create new admin (email + password/invitation + force change + template + 2FA), promote existing user, edit permissions, reset password, suspend/restore, delete.
- Invite-email flow works: invitation token link sent via Resend, admin redeems link to set own password.
- 4 permission templates work: Default (no extra grants), Trusted (Tier 2 unlocked), Read-Only (is_readonly enforced server-side), Custom (per-permission picker with individual expires_at).
- Force password change flow works: deadlines enforced by middleware; 4 timing options selectable.
- 2FA enforcement works: global toggle + per-account override; resolution redirects to /enable-2fa when needed; superadmin always forced.
- Security Policy tab (superadmin) controls global 2FA toggle + per-account override.
- API Rate Limits tab (superadmin) shows per-endpoint policies; edit + Apply updates live without deploy.
- Suspend invalidates active sessions via revoked_tokens; suspended admins return 401 on authenticated requests.
- Existing admin grandfathered with Trusted template grants on first deploy — no lockout.
- "My Permissions" page lets admin see grants + pending requests + recent denials.
- Role Change UI skeleton: superadmin can promote a user to admin or demote an admin to user (single-user flow).
- IEmailService abstraction in place; PasswordResetController refactored to use it; 6 transactional email types implemented.
- SPF TXT record updated to include Resend; deliverability green.
- `admin.auracore.pro` nginx config has basic auth removed (admin panel public-reachable); robots.txt + noindex meta deployed.
- Backend ~30-40 new tests pass; frontend ~15-20 new tests pass; total ~2440-2470, 0 failed, 0 skipped.
- Memory file written + MEMORY.md pointer updated.
- Branch merged to main via `--no-ff` (ceremonial) + pushed to origin (user-gated).

**Spec end.** Writing-plans skill invoked next.
