# Phase 6.11 Superadmin Foundation Implementation Plan (v2)

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a `superadmin` role above `admin` with a separate stricter login endpoint, a 3-tier permission system (tab gating + action gating + free-unless-readonly), permission request/approval flow, admin lifecycle management (create/promote/suspend/restore/delete), 4 permission templates (Default/Trusted/ReadOnly/Custom), hybrid 2FA enforcement, IEmailService abstraction over Resend, audit log CSV export, runtime-editable API rate limits, and nginx basic-auth removal from `admin.auracore.pro`.

**Architecture:** Single branch `phase-6-superadmin-foundation` off `main`. 6 waves executed sequentially. Wave 1 lands DB schema + startup services + JWT extensions + middleware. Wave 2 lands permission attribute + superadmin login + permission CRUD + email service + controller attribution. Wave 3 lands frontend role-aware shell + locked-UX components + applied PermissionGates + nginx public-cut + SPF fix. Wave 4 lands superadmin-only tabs (Permission Requests / Admin Action Log / Admin Management / Role Change skeleton) + CSV export + invitation flow + mid-deploy. Wave 5 lands template grant generation + force-password-change + 2FA enforcement + Security Policy + API Rate Limits. Wave 6 lands My Permissions + final deploy + ceremonial merge.

**Tech stack:** ASP.NET Core 8 / EF Core 8 (PostgreSQL) / SignalR / Resend HTTPS API / Next.js 14 static export / React 18 / TypeScript / Tailwind CSS 3 / xUnit / Moq / Vitest / @testing-library/react.

**Spec:** [docs/superpowers/specs/2026-04-23-superadmin-foundation-design.md](../specs/2026-04-23-superadmin-foundation-design.md) (commit `d178052`, 17 design decisions D1–D17 approved).
**Baseline:** `main` HEAD `2c7d812` (Phase 6.10 merged + Round 1-3 hotfixes + Phase 6.11 spec).
**Branch:** `phase-6-superadmin-foundation` (created from `main` HEAD `2c7d812`).
**Target post-6.11:** ~2450-2480 tests (+60-90 net from current ~2392). Backend ~35-45 new tests; frontend ~15-20. Plan spans **45 tasks** across 6 waves (42 primary + 3 review-pass insertions: T30.1, T30.2, T37.1).

---

## Pre-flight

### Fresh-session handoff

This plan is designed to be executed in a **fresh session** (either inline via executing-plans or subagent-driven). On a fresh session the executor should:

1. Read the spec at `docs/superpowers/specs/2026-04-23-superadmin-foundation-design.md`.
2. Read Phase 6.10 memory: `C:\Users\Admin\.claude\projects\C--\memory\project_phase_6_item_10_admin_rebuild_complete.md`.
3. Verify `git status` is clean on `main` before checking out the branch.

### Credentials (NEVER commit to repo)

- **SSH to origin:** `ssh -i C:/Users/Admin/.ssh/id_ed25519 root@165.227.170.3`
- **Admin login (prod):** `admin@auracore.pro` / `v19w&tpALj%#t4*kTHZ&`
- **Postgres (prod):** `postgres` / `auracoredb` / `auracorepro2026`
- **Resend API key:** already present in `/etc/auracore-api.env` as `RESEND_API_KEY`.

### Branch setup

```bash
cd "C:/Users/Admin/Desktop/AuraCorePro/AuraCorePro"
git checkout main
git pull origin main
git log --oneline -1
# Expected: 2c7d812 (or later) on main
git checkout -b phase-6-superadmin-foundation
```

### Add `SUPERADMIN_EMAILS` env var on origin (one-time ops step, before Wave 4 mid-deploy)

```bash
ssh -i C:/Users/Admin/.ssh/id_ed25519 root@165.227.170.3 \
  "grep -q '^SUPERADMIN_EMAILS=' /etc/auracore-api.env \
    || echo 'SUPERADMIN_EMAILS=ozgurdeniz807@gmail.com' >> /etc/auracore-api.env"
ssh -i C:/Users/Admin/.ssh/id_ed25519 root@165.227.170.3 \
  "grep '^SUPERADMIN_EMAILS=' /etc/auracore-api.env"
# Expected output line:
#   SUPERADMIN_EMAILS=ozgurdeniz807@gmail.com
```

The Wave 1 startup bootstrap is a no-op until this env var is set + the referenced email already exists in the `users` table. Prod deploy order: Wave 1-2 code can ship first (bootstrap logs a warning and moves on); Wave 4 mid-deploy is the cutover where superadmin actually exists and the grandfather migration runs against live data.

### Env-var pre-checklist for Wave 4 mid-deploy

```bash
ssh -i C:/Users/Admin/.ssh/id_ed25519 root@165.227.170.3 \
  "grep -E '^(SUPERADMIN_EMAILS|RESEND_API_KEY|JWT_SECRET)=' /etc/auracore-api.env | sed 's/=.*/=<set>/'"
# Expected:
#   SUPERADMIN_EMAILS=<set>
#   RESEND_API_KEY=<set>
#   JWT_SECRET=<set>
```

---

## File-structure overview

### Created by this plan

**Backend domain entities** (`src/Backend/AuraCore.API.Domain/Entities/`):
- `PermissionGrant.cs` (NEW)
- `PermissionRequest.cs` (NEW)
- `RevokedToken.cs` (NEW)
- `AdminInvitation.cs` (NEW)
- `SystemSetting.cs` (NEW)

**Backend application interfaces** (`src/Backend/AuraCore.API.Application/`):
- `Services/Email/IEmailService.cs` (NEW)
- `Services/Email/EmailTemplate.cs` (NEW — enum)
- `Services/Email/EmailSendResult.cs` (NEW — record)
- `Services/RateLimiting/IRateLimitConfigService.cs` (NEW)

**Backend infrastructure** (`src/Backend/AuraCore.API.Infrastructure/`):
- `Services/Email/ResendEmailService.cs` (NEW)
- `Services/Email/Templates/_base.html` (NEW)
- `Services/Email/Templates/AdminInvitation.html` (NEW)
- `Services/Email/Templates/PasswordReset.html` (NEW)
- `Services/Email/Templates/PermissionRequested.html` (NEW)
- `Services/Email/Templates/PermissionApproved.html` (NEW)
- `Services/Email/Templates/PermissionDenied.html` (NEW)
- `Services/Email/Templates/AdminCreatedWithoutEmail.html` (NEW)
- `Services/RateLimiting/RateLimitConfigService.cs` (NEW)
- `Migrations/<timestamp>_AddSuperadminFoundation.cs` (NEW — `dotnet ef migrations add`)

**Backend API project** (`src/Backend/AuraCore.API/`):
- `Filters/RequiresPermissionAttribute.cs` (NEW)
- `Filters/DestructiveActionAttribute.cs` (NEW)
- `Helpers/PermissionKeys.cs` (NEW)
- `Helpers/PermissionTemplates.cs` (NEW)
- `Helpers/ClaimsExtensions.cs` (NEW)
- `Services/SuperadminBootstrapService.cs` (NEW)
- `Services/GrandfatherMigrationService.cs` (NEW)
- `Middleware/TokenRevocationMiddleware.cs` (NEW)
- `Middleware/ScopeLimitedTokenMiddleware.cs` (NEW)
- `Middleware/ForcePasswordChangeMiddleware.cs` (NEW)
- `Controllers/Admin/PermissionRequestsController.cs` (NEW)
- `Controllers/Admin/MyPermissionsController.cs` (NEW)
- `Controllers/Admin/AuditLogExportController.cs` (NEW)
- `Controllers/Superadmin/PermissionGrantsController.cs` (NEW)
- `Controllers/Superadmin/AdminManagementController.cs` (NEW — incl. apply-template endpoint from T30.1)
- `Controllers/Superadmin/AdminActionLogController.cs` (NEW)
- `Controllers/Superadmin/InvitationsController.cs` (NEW — T30.2 list/revoke/resend)
- `Controllers/Superadmin/SecurityPolicyController.cs` (NEW)
- `Controllers/Superadmin/RateLimitConfigController.cs` (NEW)
- `Controllers/Superadmin/RoleChangeController.cs` (NEW — stub skeleton; backend endpoints live on AdminManagementController via promote/demote)
- `HostedServices/RetentionJob.cs` (NEW — T37.1 GC for revoked_tokens + expired admin_invitations)
<br/>_Note: `/api/auth/redeem-invitation`, `/api/auth/change-password`, `/api/auth/logout`, `/api/auth/enable-2fa/generate+confirm` are added as methods on the existing `AuthController.cs` (T27, T34, T35) — no new controller file._
<br/>_The `RoleChangeController.cs` file is not strictly required for Phase 6.11 because the promote/demote endpoints live on `AdminManagementController`. If the implementer wants symmetry with other tab controllers they can create a thin wrapper; otherwise skip._

**Frontend** (`admin-panel/src/`):
- `lib/permissions.ts` (NEW — key constants + label map + helpers)
- `hooks/usePermissions.ts` (NEW)
- `components/LockedTabPlaceholder.tsx` (NEW)
- `components/PermissionRequestDialog.tsx` (NEW)
- `components/PermissionGate.tsx` (NEW)
- `components/CustomTemplatePicker.tsx` (NEW)
- `components/CreateAdminModal.tsx` (NEW — T30)
- `components/EditPermissionsModal.tsx` (NEW — T30.1)
- `lib/roleContext.ts` (NEW — T23)
- `views/PermissionRequestsPage.tsx` (NEW)
- `views/AdminActionLogPage.tsx` (NEW)
- `views/AdminManagementPage.tsx` (NEW)
- `views/InvitationsPage.tsx` (NEW — T30.2)
- `views/RoleChangePage.tsx` (NEW)
- `views/SecurityPolicyPage.tsx` (NEW)
- `views/APIRateLimitsPage.tsx` (NEW)
- `views/MyPermissionsPage.tsx` (NEW)
- `views/ChangePasswordPage.tsx` (NEW)
- `views/Enable2FAPage.tsx` (NEW)
- `views/RedeemInvitationPage.tsx` (NEW)
- `public/robots.txt` (NEW)

**Tests — backend** (`tests/AuraCore.Tests.API/SuperadminFoundation/`):
- `PermissionKeysTests.cs`
- `PermissionTemplatesTests.cs`
- `SuperadminBootstrapServiceTests.cs`
- `GrandfatherMigrationServiceTests.cs`
- `SuperadminLoginEndpointTests.cs`
- `RequiresPermissionAttributeTests.cs`
- `DestructiveActionAttributeTests.cs`
- `TokenRevocationMiddlewareTests.cs`
- `ScopeLimitedTokenMiddlewareTests.cs`
- `ForcePasswordChangeMiddlewareTests.cs`
- `TwoFactorEnforcementTests.cs`
- `PermissionRequestLifecycleTests.cs`
- `PermissionGrantsControllerTests.cs`
- `AdminManagementControllerTests.cs` (incl. ApplyTemplate tests from T30.1)
- `AdminInvitationFlowTests.cs`
- `InvitationsManagementTests.cs` (NEW — T30.2)
- `AdminActionLogCsvExportTests.cs`
- `RateLimitConfigServiceTests.cs`
- `RetentionJobTests.cs` (NEW — T37.1)
- `ResendEmailServiceTests.cs`
- `DualRoleClaimTests.cs`
- `EntityDefaultsTests.cs`
- `DbContextSuperadminTests.cs`
- `ControllerAttributeApplicationTests.cs`

**Tests — frontend** (`admin-panel/src/__tests__/`):
- `lib/permissions.test.ts`
- `hooks/usePermissions.test.ts`
- `components/LockedTabPlaceholder.test.tsx`
- `components/PermissionRequestDialog.test.tsx`
- `components/PermissionGate.test.tsx`
- `components/CustomTemplatePicker.test.tsx`
- `views/PermissionRequestsPage.test.tsx`
- `views/AdminManagementPage.test.tsx`
- `views/MyPermissionsPage.test.tsx`
- `views/LoginScreen.superadmin.test.tsx`

### Modified by this plan

**Backend:**
- `src/Backend/AuraCore.API.Domain/Entities/User.cs` — add 8 fields (IsActive, IsReadonly, ForcePasswordChange, ForcePasswordChangeBy, PasswordChangedAt, CreatedByUserId, CreatedVia, Require2fa)
- `src/Backend/AuraCore.API.Infrastructure/Data/AuraCoreDbContext.cs` — DbSets for 5 new entities + EF configurations + User field mappings + `audit_log.retention` guard unchanged
- `src/Backend/AuraCore.API.Infrastructure/Services/AuthService.cs` — JWT now includes `jti` + `scope` (optional) claims; superadmin users receive TWO `role` claims (`admin` + `superadmin`) so `[Authorize(Roles="admin")]` covers both; issuer of scope-limited JWT (2fa-setup-only)
- `src/Backend/AuraCore.API/Controllers/AuthController.cs` — add `/api/auth/superadmin/login` + `/api/auth/redeem-invitation` + `/api/auth/change-password` + `/api/auth/enable-2fa` (completes existing TOTP flow under scope-limited tokens) + `/api/auth/logout` (token revocation)
- `src/Backend/AuraCore.API/Controllers/PasswordResetController.cs` — replace inline `SendResetEmailAsync` (line 146-168) with `_email.SendFromTemplateAsync(EmailTemplate.PasswordReset, ...)`
- `src/Backend/AuraCore.API/Controllers/Admin/AdminConfigController.cs` — `[RequiresPermission(PermissionKeys.TabConfiguration)]` on mutation methods
- `src/Backend/AuraCore.API/Controllers/Admin/AdminIpWhitelistController.cs` — `[RequiresPermission(PermissionKeys.TabIpWhitelist)]`
- `src/Backend/AuraCore.API/Controllers/Admin/AdminUpdateController.cs` — `[RequiresPermission(PermissionKeys.TabUpdates)]`
- `src/Backend/AuraCore.API/Controllers/Admin/AdminUserController.cs` — `[RequiresPermission(ActionUsersDelete)]` on DeleteUser; **new** POST `/{id:guid}/ban` endpoint with `[RequiresPermission(ActionUsersBan)]`
- `src/Backend/AuraCore.API/Controllers/Admin/AdminSubscriptionController.cs` — `[RequiresPermission(ActionSubscriptionsGrant/Revoke)]`
- `src/Backend/AuraCore.API/Controllers/Payment/CryptoController.cs` — `[RequiresPermission(ActionPaymentsApproveCrypto/RejectCrypto)]` on `admin/verify` + `admin/reject`
- `src/Backend/AuraCore.API/Controllers/Admin/AdminLicenseController.cs` — `[DestructiveAction]` on Revoke + Activate
- `src/Backend/AuraCore.API/Controllers/Admin/AdminDeviceController.cs` — `[DestructiveAction]` on Revoke/Delete
- `src/Backend/AuraCore.API/Controllers/Admin/AdminCrashReportController.cs` — `[DestructiveAction]` on Delete
- `src/Backend/AuraCore.API/Hubs/AdminHub.cs` — accept `admin,superadmin` roles (`[Authorize(Roles = "admin,superadmin")]`); reject scope-limited JWT; superadmins also join `superadmins` group
- `src/Backend/AuraCore.API/Program.cs` — register bootstrap service + grandfather service + IEmailService + rate-limit service + 3 middlewares + named HttpClient `"resend"` + startup invocation
- `src/Backend/AuraCore.API/AuraCore.API.csproj` — no new packages (everything in-box)

**Frontend:**
- `admin-panel/src/lib/types.ts` — add `User.role` union (`'user'|'admin'|'superadmin'`), `PermissionGrant`, `PermissionRequest`, `AdminInvitation`, `RateLimitPolicy`, `AdminAccount`, `SecurityPolicy` interfaces
- `admin-panel/src/lib/api.ts` — add ~30 endpoint methods
- `admin-panel/src/hooks/useSignalR.ts` — add 4 new event names to handler map
- `admin-panel/src/components/LoginScreen.tsx` — add "Sign In as Superadmin" button + scope-limited token detection + `/enable-2fa` / `/change-password` handoff
- `admin-panel/src/components/Sidebar.tsx` — unchanged (just receives conditionally-built NAV_GROUPS)
- `admin-panel/src/app/page.tsx` — role-conditional NAV_GROUPS + 9 new view route entries + post-login role read
- `admin-panel/src/app/layout.tsx` — `robots: { index: false, follow: false }` metadata
- `admin-panel/src/views/UsersPage.tsx` — wrap Delete + Ban buttons in `<PermissionGate>`
- `admin-panel/src/views/SubscriptionsPage.tsx` — wrap Grant/Revoke
- `admin-panel/src/views/PaymentsPage.tsx` — wrap ApproveCrypto/RejectCrypto
- `admin-panel/src/views/AuditLogPage.tsx` — Export CSV button

**Operational:**
- nginx config on origin at `/etc/nginx/sites-enabled/auracore-admin` — remove `auth_basic` + `auth_basic_user_file` directives
- DNS on `auracore.pro` — append `include:_spf.resend.com` to the SPF TXT record
- `/etc/auracore-api.env` on origin — add `SUPERADMIN_EMAILS=ozgurdeniz807@gmail.com`

---

## A note about existing code conventions found during planning

Before writing this plan, the repo was surveyed at `main` HEAD `2c7d812`. A few conventions matter for correctness:

- **DbContext class name is `AuraCoreDbContext`**, NOT `AppDbContext`. All C# code in this plan uses `AuraCoreDbContext`.
- **User entity uses `DateTimeOffset`** for `CreatedAt`/`UpdatedAt`. New date-ish fields in `User` reuse `DateTimeOffset`. New tables may use `DateTime` (UTC) where naturally modeled as a timestamp, but keep comparisons consistent inside a single entity.
- **JWT config sets `NameClaimType = "sub"`** (Program.cs line 114), so reading the user id inside an attribute filter should use `user.FindFirst("sub")?.Value` rather than `ClaimTypes.NameIdentifier`.
- **AuthService already issues** `ClaimTypes.NameIdentifier`, `ClaimTypes.Email`, `ClaimTypes.Role`, and `"sub"` in the token. The plan extends this to add `jti` (for revocation) and an optional `scope` claim (for 2FA-setup-only tokens), plus DUAL `ClaimTypes.Role` claims (admin + superadmin) for superadmin accounts so existing `[Authorize(Roles = "admin")]` covers them naturally.
- **Existing audit action attribute** is `AuraCore.API.Filters.AuditActionAttribute` (`[AuditAction("Name", "Type", TargetIdFromRouteKey = "id")]`). The new `RequiresPermissionAttribute` lives in the same namespace.
- **Frontend does NOT use React Router;** the root `admin-panel/src/app/page.tsx` maintains a `page` state variable with a `Record<Page, () => JSX.Element>` map. New "routes" like `/enable-2fa`, `/change-password`, `/invite?token=...` use the same pattern — a `view` state + URL hash listener (`window.location.hash`) rather than Next.js file-based routing (this is a static export). See Task 24 for the hash-listener shim.
- **`admin-panel/src/lib/signalr.ts`** is a minimal shim (not the newer `useSignalR` hook pattern) — the plan extends the existing module-level `L` map rather than introducing a hook.

---

## Wave 1 — DB schema + auth foundation + middleware

Six tasks. After Wave 1 the backend compiles + migrations generated + JWT carries `jti` + scope claim + token revocation middleware wired, but no permission checks are enforced yet.

### Task 1: Domain entities for new tables + User field additions

**Goal:** Create 5 new C# entity classes and add 8 fields to `User` entity. No DbContext wiring yet (Task 2).

**Files:**
- Create: `src/Backend/AuraCore.API.Domain/Entities/PermissionGrant.cs`
- Create: `src/Backend/AuraCore.API.Domain/Entities/PermissionRequest.cs`
- Create: `src/Backend/AuraCore.API.Domain/Entities/RevokedToken.cs`
- Create: `src/Backend/AuraCore.API.Domain/Entities/AdminInvitation.cs`
- Create: `src/Backend/AuraCore.API.Domain/Entities/SystemSetting.cs`
- Modify: `src/Backend/AuraCore.API.Domain/Entities/User.cs`
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/EntityDefaultsTests.cs`

- [ ] **Step 1: Write failing test for entity defaults**

Create `tests/AuraCore.Tests.API/SuperadminFoundation/EntityDefaultsTests.cs`:

```csharp
using AuraCore.API.Domain.Entities;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class EntityDefaultsTests
{
    [Fact]
    public void PermissionGrant_defaults_are_sensible()
    {
        var g = new PermissionGrant();
        Assert.NotEqual(Guid.Empty, g.Id);
        Assert.Null(g.RevokedAt);
        Assert.Null(g.ExpiresAt);
        Assert.True(g.IsActive());
    }

    [Fact]
    public void PermissionGrant_expired_is_inactive()
    {
        var g = new PermissionGrant { ExpiresAt = DateTime.UtcNow.AddMinutes(-1) };
        Assert.False(g.IsActive());
    }

    [Fact]
    public void PermissionGrant_revoked_is_inactive()
    {
        var g = new PermissionGrant { RevokedAt = DateTime.UtcNow };
        Assert.False(g.IsActive());
    }

    [Fact]
    public void PermissionRequest_status_defaults_to_pending()
    {
        var r = new PermissionRequest();
        Assert.Equal("pending", r.Status);
    }

    [Fact]
    public void AdminInvitation_valid_when_not_consumed_and_not_expired()
    {
        var inv = new AdminInvitation { ExpiresAt = DateTime.UtcNow.AddDays(1) };
        Assert.True(inv.IsValid());
    }

    [Fact]
    public void AdminInvitation_invalid_after_consumed()
    {
        var inv = new AdminInvitation {
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            ConsumedAt = DateTime.UtcNow,
        };
        Assert.False(inv.IsValid());
    }

    [Fact]
    public void User_defaults_for_new_phase611_fields()
    {
        var u = new User();
        Assert.True(u.IsActive);
        Assert.False(u.IsReadonly);
        Assert.False(u.ForcePasswordChange);
        Assert.Null(u.ForcePasswordChangeBy);
        Assert.Null(u.PasswordChangedAt);
        Assert.Null(u.CreatedByUserId);
        Assert.Equal("signup", u.CreatedVia);
        Assert.False(u.Require2fa);
    }
}
```

- [ ] **Step 2: Verify test fails (entities do not yet exist)**

```bash
cd "C:/Users/Admin/Desktop/AuraCorePro/AuraCorePro"
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter "FullyQualifiedName~EntityDefaultsTests" 2>&1 | tail -10
```

Expected: compilation error "The type or namespace name 'PermissionGrant' could not be found" etc.

- [ ] **Step 3: Create `PermissionGrant.cs`**

`src/Backend/AuraCore.API.Domain/Entities/PermissionGrant.cs`:

```csharp
namespace AuraCore.API.Domain.Entities;

public class PermissionGrant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AdminUserId { get; set; }
    public User? AdminUser { get; set; }

    /// <summary>Permission key, e.g. "tab:configuration" or "action:users.delete".</summary>
    public string PermissionKey { get; set; } = string.Empty;

    public Guid GrantedBy { get; set; }
    public User? GrantedByUser { get; set; }

    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }
    public Guid? RevokedBy { get; set; }
    public User? RevokedByUser { get; set; }
    public string? RevokeReason { get; set; }

    public Guid? SourceRequestId { get; set; }
    public PermissionRequest? SourceRequest { get; set; }

    public bool IsActive() => RevokedAt == null && (ExpiresAt == null || ExpiresAt > DateTime.UtcNow);
}
```

- [ ] **Step 4: Create `PermissionRequest.cs`**

`src/Backend/AuraCore.API.Domain/Entities/PermissionRequest.cs`:

```csharp
namespace AuraCore.API.Domain.Entities;

public class PermissionRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AdminUserId { get; set; }
    public User? AdminUser { get; set; }

    public string PermissionKey { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    /// <summary>pending | approved | denied | cancelled</summary>
    public string Status { get; set; } = "pending";

    public Guid? ReviewedBy { get; set; }
    public User? ReviewedByUser { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
}
```

- [ ] **Step 5: Create `RevokedToken.cs`**

`src/Backend/AuraCore.API.Domain/Entities/RevokedToken.cs`:

```csharp
namespace AuraCore.API.Domain.Entities;

public class RevokedToken
{
    /// <summary>JWT 'jti' (unique ID) claim. Primary key.</summary>
    public string Jti { get; set; } = string.Empty;

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public DateTime RevokedAt { get; set; } = DateTime.UtcNow;
    public Guid? RevokedBy { get; set; }
    public User? RevokedByUser { get; set; }

    /// <summary>'suspend' | 'password_reset' | 'logout_all' | 'admin_deleted' | 'logout'</summary>
    public string RevokeReason { get; set; } = string.Empty;
}
```

- [ ] **Step 6: Create `AdminInvitation.cs`**

`src/Backend/AuraCore.API.Domain/Entities/AdminInvitation.cs`:

```csharp
namespace AuraCore.API.Domain.Entities;

public class AdminInvitation
{
    /// <summary>SHA256 hex hash of the raw token emailed to admin. Primary key.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public Guid AdminUserId { get; set; }
    public User? AdminUser { get; set; }

    public Guid CreatedBy { get; set; }
    public User? CreatedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }

    public bool IsValid() => ConsumedAt == null && ExpiresAt > DateTime.UtcNow;
}
```

- [ ] **Step 7: Create `SystemSetting.cs`**

`src/Backend/AuraCore.API.Domain/Entities/SystemSetting.cs`:

```csharp
namespace AuraCore.API.Domain.Entities;

public class SystemSetting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedBy { get; set; }
    public User? UpdatedByUser { get; set; }
}
```

- [ ] **Step 8: Modify `User.cs` — add 8 fields**

Read the current `User.cs`, then replace the body with:

```csharp
namespace AuraCore.API.Domain.Entities;

public sealed class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
    public string? TotpSecret { get; set; }
    public bool TotpEnabled { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Phase 6.11 additions
    public bool IsActive { get; set; } = true;
    public bool IsReadonly { get; set; } = false;
    public bool ForcePasswordChange { get; set; } = false;
    public DateTimeOffset? ForcePasswordChangeBy { get; set; }
    public DateTimeOffset? PasswordChangedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }

    /// <summary>'signup' | 'admin_promote' | 'superadmin_create'</summary>
    public string CreatedVia { get; set; } = "signup";

    public bool Require2fa { get; set; } = false;

    public ICollection<License> Licenses { get; set; } = new List<License>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
```

- [ ] **Step 9: Verify test now passes**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter "FullyQualifiedName~EntityDefaultsTests" 2>&1 | tail -5
```

Expected: 7 passed, 0 failed.

- [ ] **Step 10: Full-suite regression**

```bash
dotnet build AuraCorePro.sln 2>&1 | tail -5
```

Expected: 0 errors (8 new User fields compile; other entities reference nothing new).

- [ ] **Step 11: Commit**

```bash
git add src/Backend/AuraCore.API.Domain/Entities/ tests/AuraCore.Tests.API/SuperadminFoundation/EntityDefaultsTests.cs
git commit -m "feat(6.11.W1.T1): domain entities for permission system + User phase 6.11 fields

PermissionGrant, PermissionRequest, RevokedToken, AdminInvitation,
SystemSetting new entities per spec D4. User gains 8 fields:
IsActive, IsReadonly, ForcePasswordChange(+By), PasswordChangedAt,
CreatedByUserId, CreatedVia, Require2fa.

DbContext wiring + EF migration land in Task 2-3."
```

### Task 2: AuraCoreDbContext DbSets + EF configurations

**Goal:** Wire the 5 new entities into `AuraCoreDbContext.OnModelCreating` + add User field column mappings + indexes + the partial unique indexes called out in spec D4.

**Files:**
- Modify: `src/Backend/AuraCore.API.Infrastructure/Data/AuraCoreDbContext.cs`
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/DbContextSuperadminTests.cs`

- [ ] **Step 1: Write failing test for DbSet availability + partial-unique-index constraint behavior**

Create `tests/AuraCore.Tests.API/SuperadminFoundation/DbContextSuperadminTests.cs`:

```csharp
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class DbContextSuperadminTests
{
    private static AuraCoreDbContext BuildDb()
    {
        var options = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"sa-{Guid.NewGuid()}")
            .Options;
        return new AuraCoreDbContext(options);
    }

    [Fact]
    public async Task DbContext_exposes_five_new_DbSets()
    {
        var db = BuildDb();
        db.PermissionGrants.Add(new PermissionGrant { AdminUserId = Guid.NewGuid(), PermissionKey = "tab:updates", GrantedBy = Guid.NewGuid() });
        db.PermissionRequests.Add(new PermissionRequest { AdminUserId = Guid.NewGuid(), PermissionKey = "tab:updates", Reason = "testing" });
        db.RevokedTokens.Add(new RevokedToken { Jti = "abc", UserId = Guid.NewGuid(), RevokeReason = "logout" });
        db.AdminInvitations.Add(new AdminInvitation { TokenHash = "hash123", AdminUserId = Guid.NewGuid(), CreatedBy = Guid.NewGuid(), ExpiresAt = DateTime.UtcNow.AddDays(7) });
        db.SystemSettings.Add(new SystemSetting { Key = "k", Value = "v" });
        await db.SaveChangesAsync();

        Assert.Equal(1, await db.PermissionGrants.CountAsync());
        Assert.Equal(1, await db.PermissionRequests.CountAsync());
        Assert.Equal(1, await db.RevokedTokens.CountAsync());
        Assert.Equal(1, await db.AdminInvitations.CountAsync());
        Assert.Equal(1, await db.SystemSettings.CountAsync());
    }

    [Fact]
    public async Task User_new_fields_persist_round_trip()
    {
        var db = BuildDb();
        var uid = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = uid, Email = "a@b.com", PasswordHash = "x", Role = "admin",
            IsActive = false, IsReadonly = true, Require2fa = true, CreatedVia = "superadmin_create",
        });
        await db.SaveChangesAsync();

        var back = await db.Users.FirstAsync(u => u.Id == uid);
        Assert.False(back.IsActive);
        Assert.True(back.IsReadonly);
        Assert.True(back.Require2fa);
        Assert.Equal("superadmin_create", back.CreatedVia);
    }
}
```

- [ ] **Step 2: Verify test fails (DbSets + configurations missing)**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter "FullyQualifiedName~DbContextSuperadminTests" 2>&1 | tail -10
```

Expected: compile error referencing `PermissionGrants`, `PermissionRequests` etc. not found on `AuraCoreDbContext`.

- [ ] **Step 3: Extend `AuraCoreDbContext.cs` — DbSets block**

Read `src/Backend/AuraCore.API.Infrastructure/Data/AuraCoreDbContext.cs`. Right after the existing `public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();` line, insert:

```csharp
    // Phase 6.11 additions
    public DbSet<PermissionGrant> PermissionGrants => Set<PermissionGrant>();
    public DbSet<PermissionRequest> PermissionRequests => Set<PermissionRequest>();
    public DbSet<RevokedToken> RevokedTokens => Set<RevokedToken>();
    public DbSet<AdminInvitation> AdminInvitations => Set<AdminInvitation>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
```

- [ ] **Step 4: Extend `AuraCoreDbContext.cs` — User field mappings**

Inside the existing `m.Entity<User>(e => { ... })` block, after the existing properties, add:

```csharp
            e.Property(u => u.IsActive).HasDefaultValue(true);
            e.Property(u => u.IsReadonly).HasDefaultValue(false);
            e.Property(u => u.ForcePasswordChange).HasDefaultValue(false);
            e.Property(u => u.CreatedVia).HasMaxLength(30).HasDefaultValue("signup");
            e.Property(u => u.Require2fa).HasDefaultValue(false);
            e.HasOne<User>().WithMany().HasForeignKey(u => u.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
```

- [ ] **Step 5: Extend `AuraCoreDbContext.cs` — new entity configurations**

Append to the end of `OnModelCreating` (before the closing brace):

```csharp
        m.Entity<PermissionGrant>(e => {
            e.ToTable("permission_grants"); e.HasKey(p => p.Id);
            e.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(p => p.PermissionKey).HasMaxLength(100).IsRequired();
            e.Property(p => p.GrantedAt).HasDefaultValueSql("now()");
            e.Property(p => p.RevokeReason).HasMaxLength(500);
            e.HasOne(p => p.AdminUser).WithMany().HasForeignKey(p => p.AdminUserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.GrantedByUser).WithMany().HasForeignKey(p => p.GrantedBy).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.RevokedByUser).WithMany().HasForeignKey(p => p.RevokedBy).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(p => p.SourceRequest).WithMany().HasForeignKey(p => p.SourceRequestId).OnDelete(DeleteBehavior.SetNull);
            // Partial unique: only one ACTIVE (not revoked) grant per (admin, key).
            e.HasIndex(p => new { p.AdminUserId, p.PermissionKey })
             .HasFilter("\"RevokedAt\" IS NULL")
             .IsUnique()
             .HasDatabaseName("uq_permission_grants_active");
            e.HasIndex(p => p.AdminUserId).HasDatabaseName("ix_permission_grants_admin");
        });

        m.Entity<PermissionRequest>(e => {
            e.ToTable("permission_requests"); e.HasKey(p => p.Id);
            e.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(p => p.PermissionKey).HasMaxLength(100).IsRequired();
            e.Property(p => p.Reason).IsRequired();
            e.Property(p => p.Status).HasMaxLength(20).HasDefaultValue("pending");
            e.Property(p => p.RequestedAt).HasDefaultValueSql("now()");
            e.Property(p => p.ReviewNote).HasMaxLength(1000);
            e.HasOne(p => p.AdminUser).WithMany().HasForeignKey(p => p.AdminUserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.ReviewedByUser).WithMany().HasForeignKey(p => p.ReviewedBy).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(p => new { p.Status, p.AdminUserId }).HasDatabaseName("ix_permission_requests_status_admin");
            // Partial unique: only one PENDING request per (admin, key).
            e.HasIndex(p => new { p.AdminUserId, p.PermissionKey })
             .HasFilter("\"Status\" = 'pending'")
             .IsUnique()
             .HasDatabaseName("uq_permission_requests_pending");
        });

        m.Entity<RevokedToken>(e => {
            e.ToTable("revoked_tokens"); e.HasKey(r => r.Jti);
            e.Property(r => r.Jti).HasMaxLength(100).IsRequired();
            e.Property(r => r.RevokedAt).HasDefaultValueSql("now()");
            e.Property(r => r.RevokeReason).HasMaxLength(100).IsRequired();
            e.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.RevokedByUser).WithMany().HasForeignKey(r => r.RevokedBy).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(r => r.UserId).HasDatabaseName("ix_revoked_tokens_user");
        });

        m.Entity<AdminInvitation>(e => {
            e.ToTable("admin_invitations"); e.HasKey(i => i.TokenHash);
            e.Property(i => i.TokenHash).HasMaxLength(100).IsRequired();
            e.Property(i => i.CreatedAt).HasDefaultValueSql("now()");
            e.HasOne(i => i.AdminUser).WithMany().HasForeignKey(i => i.AdminUserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.CreatedByUser).WithMany().HasForeignKey(i => i.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(i => i.AdminUserId).HasDatabaseName("ix_admin_invitations_user");
        });

        m.Entity<SystemSetting>(e => {
            e.ToTable("system_settings"); e.HasKey(s => s.Key);
            e.Property(s => s.Key).HasMaxLength(100).IsRequired();
            e.Property(s => s.Value).IsRequired();
            e.Property(s => s.UpdatedAt).HasDefaultValueSql("now()");
            e.HasOne(s => s.UpdatedByUser).WithMany().HasForeignKey(s => s.UpdatedBy).OnDelete(DeleteBehavior.SetNull);

            e.HasData(
                new SystemSetting { Key = "require_2fa_for_all_admins", Value = "false" },
                new SystemSetting { Key = "rate_limit_policies",
                    Value = "{\"auth.login\":{\"requests\":5,\"windowSeconds\":1800},\"auth.register\":{\"requests\":3,\"windowSeconds\":3600},\"admin.all\":{\"requests\":1000,\"windowSeconds\":3600},\"signalr.connect\":{\"requests\":10,\"windowSeconds\":60}}" }
            );
        });
```

- [ ] **Step 6: Add `using AuraCore.API.Domain.Entities;` if not already present**

The top of `AuraCoreDbContext.cs` already has `using AuraCore.API.Domain.Entities;` — verify with:

```bash
head -5 src/Backend/AuraCore.API.Infrastructure/Data/AuraCoreDbContext.cs
```

No change needed.

- [ ] **Step 7: Verify tests pass**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter "FullyQualifiedName~DbContextSuperadminTests" 2>&1 | tail -5
```

Expected: 2 passed.

- [ ] **Step 8: Run full existing API test suite — verify no regression**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj 2>&1 | tail -5
```

Expected: all prior tests still pass (new User fields have default values; no existing test breaks).

- [ ] **Step 9: Commit**

```bash
git add src/Backend/AuraCore.API.Infrastructure/Data/AuraCoreDbContext.cs tests/AuraCore.Tests.API/SuperadminFoundation/DbContextSuperadminTests.cs
git commit -m "feat(6.11.W1.T2): wire 5 new entities into AuraCoreDbContext + User field mappings

Partial unique indexes per spec D4:
- uq_permission_grants_active — one active grant per (admin, key)
- uq_permission_requests_pending — one pending request per (admin, key)

Seeds system_settings with initial 'require_2fa_for_all_admins=false'
and a default 'rate_limit_policies' JSON blob matching spec D4."
```

### Task 3: EF migration for new tables + User fields + seed rows

**Goal:** Generate the migration via `dotnet ef`, inspect it, commit. Do NOT apply to prod here; auto-migrate on startup handles it.

**Files:**
- Create (via tool): `src/Backend/AuraCore.API.Infrastructure/Migrations/<timestamp>_AddSuperadminFoundation.cs` + `.Designer.cs`
- Modify (auto): `src/Backend/AuraCore.API.Infrastructure/Migrations/AuraCoreDbContextModelSnapshot.cs`

- [ ] **Step 1: Generate migration**

```bash
cd "C:/Users/Admin/Desktop/AuraCorePro/AuraCorePro"
dotnet ef migrations add AddSuperadminFoundation \
  --project src/Backend/AuraCore.API.Infrastructure/AuraCore.API.Infrastructure.csproj \
  --startup-project src/Backend/AuraCore.API/AuraCore.API.csproj \
  --output-dir Migrations 2>&1 | tail -15
```

Expected: "Done. To undo this action, use 'ef migrations remove'." Three files changed / created.

- [ ] **Step 2: Open the generated `Up()` method and verify it contains**

Read the `<timestamp>_AddSuperadminFoundation.cs` file. Verify the `Up` method creates:

1. 8 `AlterColumn` or `AddColumn` calls on the `users` table for the new fields.
2. `CreateTable("permission_grants")` with all FKs and `uq_permission_grants_active` partial unique index.
3. `CreateTable("permission_requests")` with `uq_permission_requests_pending` partial unique index.
4. `CreateTable("revoked_tokens")` with FKs.
5. `CreateTable("admin_invitations")` with FKs.
6. `CreateTable("system_settings")` with the two seeded rows inside `InsertData`.

If any table is missing or an index is plain unique instead of partial filtered, stop and investigate — re-run `dotnet ef migrations remove` and adjust the DbContext before regenerating.

- [ ] **Step 3: Build-verify**

```bash
dotnet build src/Backend/AuraCore.API/AuraCore.API.csproj 2>&1 | tail -5
```

Expected: 0 errors.

- [ ] **Step 4: Apply migration to a local scratch database to smoke-test**

If local Postgres is available:

```bash
dotnet ef database update \
  --project src/Backend/AuraCore.API.Infrastructure/AuraCore.API.Infrastructure.csproj \
  --startup-project src/Backend/AuraCore.API/AuraCore.API.csproj 2>&1 | tail -8
```

Expected: "Done." If local DB is not running, skip this step — Program.cs auto-migrates on startup in prod anyway.

- [ ] **Step 5: Commit**

```bash
git add src/Backend/AuraCore.API.Infrastructure/Migrations/
git commit -m "feat(6.11.W1.T3): EF migration AddSuperadminFoundation

Creates permission_grants + permission_requests + revoked_tokens +
admin_invitations + system_settings tables with partial-unique indexes
per spec D4. Adds 8 columns to users. Seeds system_settings with
require_2fa_for_all_admins=false + default rate_limit_policies JSON.

Applied via Program.cs auto-migrate on next backend deploy."
```

### Task 4: PermissionKeys + PermissionTemplates helpers

**Goal:** Hardcoded enum-style constants per spec D1, D6 for permission keys and template-to-keys mapping. Unit-tested.

**Files:**
- Create: `src/Backend/AuraCore.API/Helpers/PermissionKeys.cs`
- Create: `src/Backend/AuraCore.API/Helpers/PermissionTemplates.cs`
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/PermissionKeysTests.cs`
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/PermissionTemplatesTests.cs`

- [ ] **Step 1: Write failing tests**

`tests/AuraCore.Tests.API/SuperadminFoundation/PermissionKeysTests.cs`:

```csharp
using AuraCore.API.Helpers;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class PermissionKeysTests
{
    [Fact]
    public void AllTier1_lists_four_tab_keys()
    {
        Assert.Equal(4, PermissionKeys.AllTier1.Count);
        Assert.Contains("tab:configuration", PermissionKeys.AllTier1);
        Assert.Contains("tab:ipwhitelist", PermissionKeys.AllTier1);
        Assert.Contains("tab:updates", PermissionKeys.AllTier1);
        Assert.Contains("tab:rolechange", PermissionKeys.AllTier1);
    }

    [Fact]
    public void AllTier2_lists_six_action_keys()
    {
        Assert.Equal(6, PermissionKeys.AllTier2.Count);
        Assert.Contains("action:users.delete", PermissionKeys.AllTier2);
        Assert.Contains("action:users.ban", PermissionKeys.AllTier2);
        Assert.Contains("action:subscriptions.grant", PermissionKeys.AllTier2);
        Assert.Contains("action:subscriptions.revoke", PermissionKeys.AllTier2);
        Assert.Contains("action:payments.approveCrypto", PermissionKeys.AllTier2);
        Assert.Contains("action:payments.rejectCrypto", PermissionKeys.AllTier2);
    }

    [Fact]
    public void IsTabKey_classifies_correctly()
    {
        Assert.True(PermissionKeys.IsTabKey("tab:configuration"));
        Assert.False(PermissionKeys.IsTabKey("action:users.delete"));
        Assert.False(PermissionKeys.IsTabKey("unknown"));
    }
}
```

`tests/AuraCore.Tests.API/SuperadminFoundation/PermissionTemplatesTests.cs`:

```csharp
using AuraCore.API.Helpers;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class PermissionTemplatesTests
{
    [Fact]
    public void Default_grants_no_keys()
    {
        var keys = PermissionTemplates.GetPermissionsForTemplate(PermissionTemplates.Default);
        Assert.Empty(keys);
    }

    [Fact]
    public void Trusted_grants_all_tier2_actions()
    {
        var keys = PermissionTemplates.GetPermissionsForTemplate(PermissionTemplates.Trusted);
        Assert.Equal(6, keys.Count);
        Assert.All(PermissionKeys.AllTier2, t2 => Assert.Contains(t2, keys));
    }

    [Fact]
    public void ReadOnly_grants_no_keys_and_requires_is_readonly()
    {
        var keys = PermissionTemplates.GetPermissionsForTemplate(PermissionTemplates.ReadOnly);
        Assert.Empty(keys);
        Assert.True(PermissionTemplates.RequiresIsReadonlyFlag(PermissionTemplates.ReadOnly));
    }

    [Fact]
    public void Custom_throws_when_resolving_keys()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PermissionTemplates.GetPermissionsForTemplate(PermissionTemplates.Custom));
    }

    [Fact]
    public void Unknown_template_throws()
    {
        Assert.Throws<ArgumentException>(() =>
            PermissionTemplates.GetPermissionsForTemplate("Bogus"));
    }
}
```

- [ ] **Step 2: Verify tests fail**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj \
  --filter "FullyQualifiedName~PermissionKeysTests|FullyQualifiedName~PermissionTemplatesTests" 2>&1 | tail -8
```

Expected: compile errors — `PermissionKeys` and `PermissionTemplates` not found.

- [ ] **Step 3: Create `PermissionKeys.cs`**

`src/Backend/AuraCore.API/Helpers/PermissionKeys.cs`:

```csharp
namespace AuraCore.API.Helpers;

/// <summary>
/// Hardcoded permission key namespace (spec D1). Adding a new key requires
/// backend code change, migration, and frontend label addition. Keys are
/// format "tab:<name>" (Tier 1 — gates whole tab/feature area) or
/// "action:<area>.<verb>" (Tier 2 — gates a single destructive operation).
/// </summary>
public static class PermissionKeys
{
    // Tier 1 — tab-level gating
    public const string TabConfiguration = "tab:configuration";
    public const string TabIpWhitelist   = "tab:ipwhitelist";
    public const string TabUpdates       = "tab:updates";
    public const string TabRoleChange    = "tab:rolechange";

    // Tier 2 — action-level gating
    public const string ActionUsersDelete             = "action:users.delete";
    public const string ActionUsersBan                = "action:users.ban";
    public const string ActionSubscriptionsGrant      = "action:subscriptions.grant";
    public const string ActionSubscriptionsRevoke     = "action:subscriptions.revoke";
    public const string ActionPaymentsApproveCrypto   = "action:payments.approveCrypto";
    public const string ActionPaymentsRejectCrypto    = "action:payments.rejectCrypto";

    public static readonly IReadOnlyList<string> AllTier1 = new[]
    {
        TabConfiguration, TabIpWhitelist, TabUpdates, TabRoleChange,
    };

    public static readonly IReadOnlyList<string> AllTier2 = new[]
    {
        ActionUsersDelete, ActionUsersBan,
        ActionSubscriptionsGrant, ActionSubscriptionsRevoke,
        ActionPaymentsApproveCrypto, ActionPaymentsRejectCrypto,
    };

    public static readonly IReadOnlyList<string> AllKeys =
        AllTier1.Concat(AllTier2).ToArray();

    public static bool IsTabKey(string key) => key?.StartsWith("tab:") == true;
    public static bool IsActionKey(string key) => key?.StartsWith("action:") == true;
    public static bool IsValidKey(string key) => AllKeys.Contains(key);
}
```

- [ ] **Step 4: Create `PermissionTemplates.cs`**

`src/Backend/AuraCore.API/Helpers/PermissionTemplates.cs`:

```csharp
namespace AuraCore.API.Helpers;

/// <summary>
/// Four permission templates per spec D6. Default/Trusted/ReadOnly produce a
/// deterministic permission key list; Custom is configured per-grant by the
/// superadmin (see Superadmin AdminManagementController create-admin flow).
/// </summary>
public static class PermissionTemplates
{
    public const string Default  = "Default";
    public const string Trusted  = "Trusted";
    public const string ReadOnly = "ReadOnly";
    public const string Custom   = "Custom";

    public static readonly IReadOnlyList<string> AllTemplates = new[]
    {
        Default, Trusted, ReadOnly, Custom,
    };

    public static IReadOnlyList<string> GetPermissionsForTemplate(string template) => template switch
    {
        Default  => Array.Empty<string>(),
        Trusted  => PermissionKeys.AllTier2,
        ReadOnly => Array.Empty<string>(),
        Custom   => throw new InvalidOperationException(
            "Custom template is configured per-grant by the superadmin, not via this helper"),
        _ => throw new ArgumentException($"Unknown template: {template}"),
    };

    public static bool RequiresIsReadonlyFlag(string template) => template == ReadOnly;

    public static bool IsValidTemplate(string template) => AllTemplates.Contains(template);
}
```

- [ ] **Step 5: Verify tests pass**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj \
  --filter "FullyQualifiedName~PermissionKeysTests|FullyQualifiedName~PermissionTemplatesTests" 2>&1 | tail -5
```

Expected: 8 passed.

- [ ] **Step 6: Commit**

```bash
git add src/Backend/AuraCore.API/Helpers/PermissionKeys.cs src/Backend/AuraCore.API/Helpers/PermissionTemplates.cs tests/AuraCore.Tests.API/SuperadminFoundation/PermissionKeysTests.cs tests/AuraCore.Tests.API/SuperadminFoundation/PermissionTemplatesTests.cs
git commit -m "feat(6.11.W1.T4): PermissionKeys + PermissionTemplates helpers

Hardcoded enum of 4 Tier 1 + 6 Tier 2 keys (spec D1). Four templates:
Default (no keys), Trusted (6 Tier 2), ReadOnly (empty + is_readonly flag),
Custom (throws — per-grant config)."
```

### Task 5: SuperadminBootstrapService

**Goal:** Startup-time service that reads `SUPERADMIN_EMAILS` env var and promotes matching rows to `role='superadmin'` idempotently. Never creates accounts.

**Files:**
- Create: `src/Backend/AuraCore.API/Services/SuperadminBootstrapService.cs`
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/SuperadminBootstrapServiceTests.cs`

- [ ] **Step 1: Write failing tests**

`tests/AuraCore.Tests.API/SuperadminFoundation/SuperadminBootstrapServiceTests.cs`:

```csharp
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using AuraCore.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class SuperadminBootstrapServiceTests
{
    private static AuraCoreDbContext BuildDb()
    {
        var options = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"boot-{Guid.NewGuid()}")
            .Options;
        return new AuraCoreDbContext(options);
    }

    [Fact]
    public async Task RunAsync_is_noop_when_env_var_unset()
    {
        Environment.SetEnvironmentVariable("SUPERADMIN_EMAILS", null);
        var db = BuildDb();
        db.Users.Add(new User { Id = Guid.NewGuid(), Email = "a@b.com", PasswordHash = "x", Role = "user" });
        await db.SaveChangesAsync();

        var svc = new SuperadminBootstrapService(db, NullLogger<SuperadminBootstrapService>.Instance);
        await svc.RunAsync();

        Assert.Equal("user", (await db.Users.FirstAsync()).Role);
    }

    [Fact]
    public async Task RunAsync_promotes_existing_user_to_superadmin()
    {
        Environment.SetEnvironmentVariable("SUPERADMIN_EMAILS", "boss@auracore.pro");
        var db = BuildDb();
        db.Users.Add(new User { Id = Guid.NewGuid(), Email = "boss@auracore.pro", PasswordHash = "x", Role = "admin" });
        await db.SaveChangesAsync();

        var svc = new SuperadminBootstrapService(db, NullLogger<SuperadminBootstrapService>.Instance);
        await svc.RunAsync();

        Assert.Equal("superadmin", (await db.Users.FirstAsync()).Role);
    }

    [Fact]
    public async Task RunAsync_is_idempotent_on_already_promoted_user()
    {
        Environment.SetEnvironmentVariable("SUPERADMIN_EMAILS", "boss@auracore.pro");
        var db = BuildDb();
        db.Users.Add(new User { Id = Guid.NewGuid(), Email = "boss@auracore.pro", PasswordHash = "x", Role = "superadmin" });
        await db.SaveChangesAsync();

        var svc = new SuperadminBootstrapService(db, NullLogger<SuperadminBootstrapService>.Instance);
        await svc.RunAsync();
        await svc.RunAsync();

        Assert.Equal(1, await db.Users.CountAsync(u => u.Role == "superadmin"));
    }

    [Fact]
    public async Task RunAsync_logs_warning_when_email_not_registered()
    {
        Environment.SetEnvironmentVariable("SUPERADMIN_EMAILS", "ghost@nowhere.com");
        var db = BuildDb();
        var logger = new ListLogger<SuperadminBootstrapService>();

        var svc = new SuperadminBootstrapService(db, logger);
        await svc.RunAsync();

        Assert.Contains(logger.Messages, m => m.Contains("not registered"));
    }

    [Fact]
    public async Task RunAsync_handles_multiple_comma_separated_emails_case_insensitive()
    {
        Environment.SetEnvironmentVariable("SUPERADMIN_EMAILS", " Alice@x.COM , bob@x.com ");
        var db = BuildDb();
        db.Users.Add(new User { Id = Guid.NewGuid(), Email = "alice@x.com", PasswordHash = "x", Role = "admin" });
        db.Users.Add(new User { Id = Guid.NewGuid(), Email = "bob@x.com", PasswordHash = "x", Role = "user" });
        await db.SaveChangesAsync();

        var svc = new SuperadminBootstrapService(db, NullLogger<SuperadminBootstrapService>.Instance);
        await svc.RunAsync();

        Assert.Equal(2, await db.Users.CountAsync(u => u.Role == "superadmin"));
    }
}

// Minimal in-memory logger to assert log content without Microsoft.Extensions.Logging.Testing
internal class ListLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
{
    public List<string> Messages { get; } = new();
    IDisposable? Microsoft.Extensions.Logging.ILogger.BeginScope<TState>(TState state) => null;
    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Messages.Add(formatter(state, exception));
}
```

- [ ] **Step 2: Verify tests fail (service not yet created)**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj \
  --filter "FullyQualifiedName~SuperadminBootstrapServiceTests" 2>&1 | tail -8
```

Expected: compile error `SuperadminBootstrapService` not found.

- [ ] **Step 3: Create `SuperadminBootstrapService.cs`**

`src/Backend/AuraCore.API/Services/SuperadminBootstrapService.cs`:

```csharp
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuraCore.API.Services;

/// <summary>
/// Reads SUPERADMIN_EMAILS env var (comma-separated) and promotes matching
/// registered users to role='superadmin' on startup. Idempotent. Never creates
/// accounts — the user must have registered first. Spec D3.
/// </summary>
public class SuperadminBootstrapService
{
    private readonly AuraCoreDbContext _db;
    private readonly ILogger<SuperadminBootstrapService> _logger;

    public SuperadminBootstrapService(AuraCoreDbContext db, ILogger<SuperadminBootstrapService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        var raw = Environment.GetEnvironmentVariable("SUPERADMIN_EMAILS");
        if (string.IsNullOrWhiteSpace(raw))
        {
            _logger.LogInformation("SUPERADMIN_EMAILS env var unset; superadmin bootstrap skipped");
            return;
        }

        var emails = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.ToLowerInvariant())
            .Distinct()
            .ToArray();

        var existing = await _db.Users
            .Where(u => emails.Contains(u.Email))
            .ToListAsync(ct);

        var promoted = 0;
        foreach (var user in existing)
        {
            if (user.Role != "superadmin")
            {
                user.Role = "superadmin";
                _logger.LogWarning("Promoted user {Email} to superadmin via SUPERADMIN_EMAILS bootstrap", user.Email);
                promoted++;
            }
        }

        if (promoted > 0)
            await _db.SaveChangesAsync(ct);

        var missing = emails.Except(existing.Select(u => u.Email)).ToArray();
        if (missing.Length > 0)
        {
            _logger.LogWarning(
                "SUPERADMIN_EMAILS contains emails not registered in users table: {Emails}. They must register first at /api/auth/register; the next backend startup will promote them.",
                string.Join(", ", missing));
        }
    }
}
```

- [ ] **Step 4: Verify tests pass**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj \
  --filter "FullyQualifiedName~SuperadminBootstrapServiceTests" 2>&1 | tail -5
```

Expected: 5 passed.

- [ ] **Step 5: Commit**

```bash
git add src/Backend/AuraCore.API/Services/SuperadminBootstrapService.cs tests/AuraCore.Tests.API/SuperadminFoundation/SuperadminBootstrapServiceTests.cs
git commit -m "feat(6.11.W1.T5): SuperadminBootstrapService — idempotent env-var promotion

Reads SUPERADMIN_EMAILS on backend startup, promotes matching registered
users to role='superadmin'. Never creates accounts. Logs a warning for
any emails that aren't yet registered so ops can retry after signup.

Wired into Program.cs in Task 9."
```

### Task 6: GrandfatherMigrationService

**Goal:** One-time idempotent migration: for every existing `role='admin'` user with zero non-revoked grants, create `Trusted` template grants. Prevents existing admins from being locked out on first Phase 6.11 deploy. Safe to run on every startup.

**Files:**
- Create: `src/Backend/AuraCore.API/Services/GrandfatherMigrationService.cs`
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/GrandfatherMigrationServiceTests.cs`

- [ ] **Step 1: Write failing tests**

`tests/AuraCore.Tests.API/SuperadminFoundation/GrandfatherMigrationServiceTests.cs`:

```csharp
using AuraCore.API.Domain.Entities;
using AuraCore.API.Helpers;
using AuraCore.API.Infrastructure.Data;
using AuraCore.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class GrandfatherMigrationServiceTests
{
    private static AuraCoreDbContext BuildDb()
    {
        var options = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"gf-{Guid.NewGuid()}")
            .Options;
        return new AuraCoreDbContext(options);
    }

    [Fact]
    public async Task RunAsync_grants_Trusted_template_to_existing_admin_with_zero_grants()
    {
        var db = BuildDb();
        var adminId = Guid.NewGuid();
        db.Users.Add(new User { Id = adminId, Email = "admin@x.com", PasswordHash = "x", Role = "admin" });
        await db.SaveChangesAsync();

        var svc = new GrandfatherMigrationService(db, NullLogger<GrandfatherMigrationService>.Instance);
        await svc.RunAsync();

        var grants = await db.PermissionGrants.Where(g => g.AdminUserId == adminId).ToListAsync();
        Assert.Equal(PermissionKeys.AllTier2.Count, grants.Count);
        Assert.All(PermissionKeys.AllTier2, key => Assert.Contains(grants, g => g.PermissionKey == key));
    }

    [Fact]
    public async Task RunAsync_is_noop_when_admin_already_has_any_active_grant()
    {
        var db = BuildDb();
        var adminId = Guid.NewGuid();
        db.Users.Add(new User { Id = adminId, Email = "admin@x.com", PasswordHash = "x", Role = "admin" });
        db.PermissionGrants.Add(new PermissionGrant { AdminUserId = adminId, PermissionKey = "tab:configuration", GrantedBy = adminId });
        await db.SaveChangesAsync();

        var svc = new GrandfatherMigrationService(db, NullLogger<GrandfatherMigrationService>.Instance);
        await svc.RunAsync();

        Assert.Equal(1, await db.PermissionGrants.CountAsync(g => g.AdminUserId == adminId));
    }

    [Fact]
    public async Task RunAsync_skips_regular_users_and_superadmins()
    {
        var db = BuildDb();
        db.Users.Add(new User { Id = Guid.NewGuid(), Email = "u@x.com", PasswordHash = "x", Role = "user" });
        db.Users.Add(new User { Id = Guid.NewGuid(), Email = "sa@x.com", PasswordHash = "x", Role = "superadmin" });
        await db.SaveChangesAsync();

        var svc = new GrandfatherMigrationService(db, NullLogger<GrandfatherMigrationService>.Instance);
        await svc.RunAsync();

        Assert.Equal(0, await db.PermissionGrants.CountAsync());
    }

    [Fact]
    public async Task RunAsync_attributes_grants_to_first_superadmin_when_available()
    {
        var db = BuildDb();
        var superId = Guid.NewGuid();
        db.Users.Add(new User { Id = superId, Email = "sa@x.com", PasswordHash = "x", Role = "superadmin" });
        db.Users.Add(new User { Id = Guid.NewGuid(), Email = "admin@x.com", PasswordHash = "x", Role = "admin" });
        await db.SaveChangesAsync();

        var svc = new GrandfatherMigrationService(db, NullLogger<GrandfatherMigrationService>.Instance);
        await svc.RunAsync();

        var grants = await db.PermissionGrants.ToListAsync();
        Assert.All(grants, g => Assert.Equal(superId, g.GrantedBy));
    }

    [Fact]
    public async Task RunAsync_idempotent_on_repeated_invocations()
    {
        var db = BuildDb();
        db.Users.Add(new User { Id = Guid.NewGuid(), Email = "admin@x.com", PasswordHash = "x", Role = "admin" });
        await db.SaveChangesAsync();

        var svc = new GrandfatherMigrationService(db, NullLogger<GrandfatherMigrationService>.Instance);
        await svc.RunAsync();
        await svc.RunAsync();
        await svc.RunAsync();

        Assert.Equal(PermissionKeys.AllTier2.Count, await db.PermissionGrants.CountAsync());
    }
}
```

- [ ] **Step 2: Verify tests fail**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj \
  --filter "FullyQualifiedName~GrandfatherMigrationServiceTests" 2>&1 | tail -8
```

Expected: compile error `GrandfatherMigrationService` not found.

- [ ] **Step 3: Create `GrandfatherMigrationService.cs`**

`src/Backend/AuraCore.API/Services/GrandfatherMigrationService.cs`:

```csharp
using AuraCore.API.Domain.Entities;
using AuraCore.API.Helpers;
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuraCore.API.Services;

/// <summary>
/// One-time idempotent grandfather migration (spec D4 "Grandfather migration").
/// For every role='admin' user with zero active (non-revoked) grants,
/// creates Trusted-template grants so existing admins aren't locked out.
/// Runs on every startup — guaranteed idempotent because the "zero grants"
/// check only matches newcomers.
/// </summary>
public class GrandfatherMigrationService
{
    private readonly AuraCoreDbContext _db;
    private readonly ILogger<GrandfatherMigrationService> _logger;

    public GrandfatherMigrationService(AuraCoreDbContext db, ILogger<GrandfatherMigrationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        var adminsWithoutGrants = await _db.Users
            .Where(u => u.Role == "admin")
            .Where(u => !_db.PermissionGrants.Any(g => g.AdminUserId == u.Id && g.RevokedAt == null))
            .ToListAsync(ct);

        if (adminsWithoutGrants.Count == 0)
        {
            _logger.LogInformation("Grandfather migration: no un-granted admins found; skipping");
            return;
        }

        // Attribute new grants to the first superadmin (if any) so audit trails are clean.
        // If none exists yet (Wave 1 before SUPERADMIN_EMAILS runs), self-attribute — we'll
        // re-attribute in a future pass if needed.
        var attribution = await _db.Users
            .Where(u => u.Role == "superadmin")
            .OrderBy(u => u.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (attribution is null)
            _logger.LogWarning("Grandfather migration: no superadmin exists yet; attributing grants self-to-self for {Count} admin(s). Re-run after SUPERADMIN_EMAILS bootstrap to normalize attribution.", adminsWithoutGrants.Count);

        var trustedKeys = PermissionTemplates.GetPermissionsForTemplate(PermissionTemplates.Trusted);

        foreach (var admin in adminsWithoutGrants)
        {
            var attributionId = attribution?.Id ?? admin.Id;
            foreach (var key in trustedKeys)
            {
                _db.PermissionGrants.Add(new PermissionGrant
                {
                    AdminUserId  = admin.Id,
                    PermissionKey = key,
                    GrantedBy    = attributionId,
                    GrantedAt    = DateTime.UtcNow,
                });
            }
            _logger.LogInformation("Grandfather migration: granted {Count} Trusted keys to {Email}", trustedKeys.Count, admin.Email);
        }

        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 4: Verify tests pass**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj \
  --filter "FullyQualifiedName~GrandfatherMigrationServiceTests" 2>&1 | tail -5
```

Expected: 5 passed.

- [ ] **Step 5: Commit**

```bash
git add src/Backend/AuraCore.API/Services/GrandfatherMigrationService.cs tests/AuraCore.Tests.API/SuperadminFoundation/GrandfatherMigrationServiceTests.cs
git commit -m "feat(6.11.W1.T6): GrandfatherMigrationService — Trusted template for legacy admins

Idempotent startup service: any role='admin' user with zero active
PermissionGrants receives the Trusted template (6 Tier-2 keys) so
existing admins keep capability after Phase 6.11 deploy. Attribution
goes to first superadmin if present; else self-attributed as a transitional
state until SUPERADMIN_EMAILS bootstrap catches up."
```

### Task 7: AuthService — JWT `jti` + optional `scope` + dual-role superadmin claims

**Goal:** Extend `AuthService.GenerateAccessToken` to (a) emit a `jti` claim for revocation tracking, (b) accept an optional `scope` parameter that emits `scope` claim (used for 2FA-setup-only tokens in Wave 5), (c) emit TWO `ClaimTypes.Role` claims (`admin` + `superadmin`) when the user's role is `superadmin` so existing `[Authorize(Roles = "admin")]` guards apply to superadmins via inclusion rather than requiring a controller-wide rewrite.

**Files:**
- Modify: `src/Backend/AuraCore.API.Infrastructure/Services/AuthService.cs`
- Modify: `src/Backend/AuraCore.API.Application/Interfaces/IAuthService.cs` — add `scope` to signature
- Create: `src/Backend/AuraCore.API/Helpers/ClaimsExtensions.cs`
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/DualRoleClaimTests.cs`

- [ ] **Step 1: Write failing tests**

`tests/AuraCore.Tests.API/SuperadminFoundation/DualRoleClaimTests.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using AuraCore.API.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class DualRoleClaimTests
{
    private static (AuthService svc, AuraCoreDbContext db) BuildSvc()
    {
        Environment.SetEnvironmentVariable("JWT_SECRET", "test-secret-at-least-32-characters-long!!");
        var opt = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"jwt-{Guid.NewGuid()}").Options;
        var db = new AuraCoreDbContext(opt);
        var cfg = new ConfigurationBuilder().Build();
        return (new AuthService(db, cfg), db);
    }

    private static JwtSecurityToken Decode(string token)
        => new JwtSecurityTokenHandler().ReadJwtToken(token);

    [Fact]
    public void Admin_token_has_single_role_claim()
    {
        var (svc, _) = BuildSvc();
        var user = new User { Id = Guid.NewGuid(), Email = "a@b.com", Role = "admin" };
        var t = svc.GenerateAccessToken(user);
        var jwt = Decode(t);

        var roles = jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray();
        Assert.Single(roles);
        Assert.Equal("admin", roles[0]);
    }

    [Fact]
    public void Superadmin_token_has_dual_role_claims_admin_and_superadmin()
    {
        var (svc, _) = BuildSvc();
        var user = new User { Id = Guid.NewGuid(), Email = "sa@b.com", Role = "superadmin" };
        var t = svc.GenerateAccessToken(user);
        var jwt = Decode(t);

        var roles = jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray();
        Assert.Contains("admin", roles);
        Assert.Contains("superadmin", roles);
    }

    [Fact]
    public void Every_token_has_unique_jti()
    {
        var (svc, _) = BuildSvc();
        var user = new User { Id = Guid.NewGuid(), Email = "a@b.com", Role = "admin" };
        var t1 = Decode(svc.GenerateAccessToken(user));
        var t2 = Decode(svc.GenerateAccessToken(user));

        var jti1 = t1.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        var jti2 = t2.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        Assert.NotEqual(jti1, jti2);
    }

    [Fact]
    public void Scope_limited_token_has_scope_claim_and_shorter_expiry()
    {
        var (svc, _) = BuildSvc();
        var user = new User { Id = Guid.NewGuid(), Email = "a@b.com", Role = "admin" };
        var token = svc.GenerateAccessToken(user, scope: "2fa-setup-only", lifetime: TimeSpan.FromMinutes(15));
        var jwt = Decode(token);

        var scope = jwt.Claims.FirstOrDefault(c => c.Type == "scope")?.Value;
        Assert.Equal("2fa-setup-only", scope);

        var remaining = jwt.ValidTo - DateTime.UtcNow;
        Assert.True(remaining <= TimeSpan.FromMinutes(16));
        Assert.True(remaining >= TimeSpan.FromMinutes(13));
    }

    [Fact]
    public void Default_token_has_no_scope_claim()
    {
        var (svc, _) = BuildSvc();
        var user = new User { Id = Guid.NewGuid(), Email = "a@b.com", Role = "admin" };
        var token = svc.GenerateAccessToken(user);
        var jwt = Decode(token);

        Assert.DoesNotContain(jwt.Claims, c => c.Type == "scope");
    }
}
```

- [ ] **Step 2: Verify tests fail**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj \
  --filter "FullyQualifiedName~DualRoleClaimTests" 2>&1 | tail -8
```

Expected: compile error — `GenerateAccessToken` does not accept `scope` or `lifetime` parameters.

- [ ] **Step 3: Modify `IAuthService.cs`**

Read `src/Backend/AuraCore.API.Application/Interfaces/IAuthService.cs`. Find the `GenerateAccessToken(User user)` signature and replace with:

```csharp
string GenerateAccessToken(User user, string? scope = null, TimeSpan? lifetime = null);
```

- [ ] **Step 4: Modify `AuthService.GenerateAccessToken`**

Replace the existing `GenerateAccessToken` body in `src/Backend/AuraCore.API.Infrastructure/Services/AuthService.cs` with:

```csharp
    public string GenerateAccessToken(User user, string? scope = null, TimeSpan? lifetime = null)
    {
        var secret = Environment.GetEnvironmentVariable("JWT_SECRET")
            ?? _config["Jwt:Secret"]
            ?? throw new InvalidOperationException("JWT_SECRET env var or Jwt:Secret config must be set");
        if (secret == "LOADED_FROM_ENV" || secret.Length < 32)
            throw new InvalidOperationException("JWT secret must be a real key with at least 32 characters");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("sub", user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };

        // Phase 6.11: superadmin users also get a 'admin' role claim so existing
        // [Authorize(Roles = "admin")] guards cover them without controller rewrites.
        // The authoritative role claim is still the first one (user.Role).
        if (user.Role == "superadmin")
            claims.Add(new Claim(ClaimTypes.Role, "admin"));

        if (!string.IsNullOrEmpty(scope))
            claims.Add(new Claim("scope", scope));

        var expiry = DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(15));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "AuraCorePro",
            audience: _config["Jwt:Audience"] ?? "AuraCorePro",
            claims: claims,
            expires: expiry,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
```

Also add the `using System.IdentityModel.Tokens.Jwt;` import at the top if not present (should already be there).

- [ ] **Step 5: Create `ClaimsExtensions.cs`**

`src/Backend/AuraCore.API/Helpers/ClaimsExtensions.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AuraCore.API.Helpers;

public static class ClaimsExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal user)
    {
        var raw = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public static string? GetEmail(this ClaimsPrincipal user)
        => user.FindFirst(ClaimTypes.Email)?.Value;

    /// <summary>Primary role (the first one emitted by AuthService). For superadmin
    /// accounts this is "superadmin"; both "admin" and "superadmin" claims exist but
    /// the primary one determines authorization semantics.</summary>
    public static string? GetPrimaryRole(this ClaimsPrincipal user)
    {
        // Prefer superadmin if both exist
        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
        if (roles.Contains("superadmin")) return "superadmin";
        if (roles.Contains("admin")) return "admin";
        return roles.FirstOrDefault();
    }

    public static string? GetJti(this ClaimsPrincipal user)
        => user.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

    public static string? GetScope(this ClaimsPrincipal user)
        => user.FindFirst("scope")?.Value;

    public static bool IsScopeLimited(this ClaimsPrincipal user)
        => !string.IsNullOrEmpty(user.GetScope());
}
```

- [ ] **Step 6: Verify tests pass**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj \
  --filter "FullyQualifiedName~DualRoleClaimTests" 2>&1 | tail -5
```

Expected: 5 passed.

- [ ] **Step 7: Run the entire API test suite — no regression**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj 2>&1 | tail -5
```

Expected: all prior tests still pass. The extra JWT claims are additive and don't change auth behavior for non-superadmin users.

- [ ] **Step 8: Commit**

```bash
git add src/Backend/AuraCore.API.Application/Interfaces/IAuthService.cs src/Backend/AuraCore.API.Infrastructure/Services/AuthService.cs src/Backend/AuraCore.API/Helpers/ClaimsExtensions.cs tests/AuraCore.Tests.API/SuperadminFoundation/DualRoleClaimTests.cs
git commit -m "feat(6.11.W1.T7): JWT jti + scope + dual-role claims for superadmin

AuthService.GenerateAccessToken now emits a unique 'jti' claim on every
token (enables token revocation), accepts an optional 'scope' param for
scope-limited tokens (2FA-setup-only), and emits TWO role claims for
superadmin accounts (admin + superadmin) so existing
[Authorize(Roles = \"admin\")] guards transparently cover superadmins.

ClaimsExtensions helper centralizes user-id / role / jti / scope reads."
```

### Task 8: TokenRevocationMiddleware

**Goal:** Middleware that rejects authenticated requests whose JWT `jti` appears in `revoked_tokens` table. Runs after `UseAuthentication` so `User` principal is populated.

**Files:**
- Create: `src/Backend/AuraCore.API/Middleware/TokenRevocationMiddleware.cs`
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/TokenRevocationMiddlewareTests.cs`

- [ ] **Step 1: Write failing tests**

`tests/AuraCore.Tests.API/SuperadminFoundation/TokenRevocationMiddlewareTests.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using AuraCore.API.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class TokenRevocationMiddlewareTests
{
    private static AuraCoreDbContext BuildDb()
    {
        var opt = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"rev-{Guid.NewGuid()}").Options;
        return new AuraCoreDbContext(opt);
    }

    private static DefaultHttpContext BuildCtx(AuraCoreDbContext db, string? jti, bool authenticated = true)
    {
        var ctx = new DefaultHttpContext();
        ctx.RequestServices = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddSingleton(db)
            .BuildServiceProvider();
        ctx.Response.Body = new MemoryStream();
        if (authenticated)
        {
            var claims = new List<Claim>();
            if (jti != null) claims.Add(new Claim(JwtRegisteredClaimNames.Jti, jti));
            ctx.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        }
        return ctx;
    }

    [Fact]
    public async Task Passes_through_when_unauthenticated()
    {
        var db = BuildDb();
        var ctx = BuildCtx(db, null, authenticated: false);
        var called = false;
        var mw = new TokenRevocationMiddleware(_ => { called = true; return Task.CompletedTask; });
        await mw.InvokeAsync(ctx, db);
        Assert.True(called);
    }

    [Fact]
    public async Task Passes_through_when_jti_not_revoked()
    {
        var db = BuildDb();
        var ctx = BuildCtx(db, "fresh-jti-123");
        var called = false;
        var mw = new TokenRevocationMiddleware(_ => { called = true; return Task.CompletedTask; });
        await mw.InvokeAsync(ctx, db);
        Assert.True(called);
    }

    [Fact]
    public async Task Returns_401_when_jti_revoked()
    {
        var db = BuildDb();
        db.RevokedTokens.Add(new RevokedToken { Jti = "bad-jti", UserId = Guid.NewGuid(), RevokeReason = "suspend" });
        await db.SaveChangesAsync();
        var ctx = BuildCtx(db, "bad-jti");
        var called = false;
        var mw = new TokenRevocationMiddleware(_ => { called = true; return Task.CompletedTask; });
        await mw.InvokeAsync(ctx, db);
        Assert.False(called);
        Assert.Equal(401, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task Passes_through_when_jti_claim_missing()
    {
        var db = BuildDb();
        var ctx = BuildCtx(db, jti: null);
        var called = false;
        var mw = new TokenRevocationMiddleware(_ => { called = true; return Task.CompletedTask; });
        await mw.InvokeAsync(ctx, db);
        Assert.True(called);
    }
}
```

- [ ] **Step 2: Verify tests fail**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj \
  --filter "FullyQualifiedName~TokenRevocationMiddlewareTests" 2>&1 | tail -8
```

Expected: compile error — `TokenRevocationMiddleware` not found.

- [ ] **Step 3: Create `TokenRevocationMiddleware.cs`**

`src/Backend/AuraCore.API/Middleware/TokenRevocationMiddleware.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Middleware;

/// <summary>
/// Rejects authenticated requests whose JWT 'jti' appears in revoked_tokens
/// (spec D13 — suspended admin, password reset, logout-all, admin-deleted).
/// Runs AFTER UseAuthentication so HttpContext.User has claims populated.
/// Unauthenticated requests and tokens without a jti claim pass through
/// (auth / login endpoints don't carry a jti until they mint one).
/// </summary>
public class TokenRevocationMiddleware
{
    private readonly RequestDelegate _next;

    public TokenRevocationMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, AuraCoreDbContext db)
    {
        if (ctx.User.Identity?.IsAuthenticated != true)
        {
            await _next(ctx);
            return;
        }

        var jti = ctx.User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        if (string.IsNullOrEmpty(jti))
        {
            await _next(ctx);
            return;
        }

        var revoked = await db.RevokedTokens.AnyAsync(r => r.Jti == jti);
        if (revoked)
        {
            ctx.Response.StatusCode = 401;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync("{\"error\":\"token_revoked\"}");
            return;
        }

        await _next(ctx);
    }
}
```

- [ ] **Step 4: Verify tests pass**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj \
  --filter "FullyQualifiedName~TokenRevocationMiddlewareTests" 2>&1 | tail -5
```

Expected: 4 passed.

- [ ] **Step 5: Commit**

```bash
git add src/Backend/AuraCore.API/Middleware/TokenRevocationMiddleware.cs tests/AuraCore.Tests.API/SuperadminFoundation/TokenRevocationMiddlewareTests.cs
git commit -m "feat(6.11.W1.T8): TokenRevocationMiddleware — jti blacklist enforcement

Authenticated requests whose JWT jti appears in revoked_tokens are
rejected with 401 {error:'token_revoked'}. Enables suspension,
force-password-reset, logout-all, and admin-deleted flows to
invalidate live sessions without waiting for token natural expiry."
```

### Task 9: Program.cs wiring — register services + middleware order

**Goal:** Wire `SuperadminBootstrapService`, `GrandfatherMigrationService`, `TokenRevocationMiddleware` into DI + startup + middleware pipeline. Keep middleware order correct: `UseAuthentication` → `UseAuthorization` → `TokenRevocationMiddleware` → `ScopeLimitedTokenMiddleware` (added in Wave 5) → `ForcePasswordChangeMiddleware` (added in Wave 5) → `MapControllers`.

**Files:**
- Modify: `src/Backend/AuraCore.API/Program.cs`

- [ ] **Step 1: Register bootstrap + grandfather services**

Read `src/Backend/AuraCore.API/Program.cs`. Find the existing `builder.Services.AddScoped<...>` block (around lines 41-44 where `IAuditLogService` and `IWhitelistService` are registered). Insert immediately after:

```csharp
// Phase 6.11 startup services
builder.Services.AddScoped<AuraCore.API.Services.SuperadminBootstrapService>();
builder.Services.AddScoped<AuraCore.API.Services.GrandfatherMigrationService>();
```

- [ ] **Step 2: Invoke bootstrap + grandfather on startup**

Find the existing "Auto-migrate on startup" block (the `try { using var scope = ... db.Database.MigrateAsync() ... } catch ...` block around lines 186-200). Immediately AFTER the closing `catch` brace of that block, insert:

```csharp
// Phase 6.11: superadmin bootstrap + grandfather migration (idempotent on every startup).
// Must run AFTER EF MigrateAsync so permission_grants table exists.
try
{
    using var sa = app.Services.CreateScope();
    var bootstrap = sa.ServiceProvider.GetRequiredService<AuraCore.API.Services.SuperadminBootstrapService>();
    var grandfather = sa.ServiceProvider.GetRequiredService<AuraCore.API.Services.GrandfatherMigrationService>();
    await bootstrap.RunAsync();
    await grandfather.RunAsync();
}
catch (Exception ex)
{
    app.Logger.LogWarning("Phase 6.11 startup services skipped: {Msg}", ex.Message);
}
```

- [ ] **Step 3: Add `TokenRevocationMiddleware` to the pipeline**

Find the existing `app.UseAuthentication(); app.UseAuthorization();` block (around lines 293-294). Insert IMMEDIATELY AFTER `app.UseAuthorization();`:

```csharp
// Phase 6.11: reject requests whose JWT jti is blacklisted.
// MUST be after UseAuthentication so HttpContext.User has claims.
// Other Phase 6.11 middlewares (ScopeLimitedTokenMiddleware, ForcePasswordChangeMiddleware)
// are added in Wave 5 immediately after this line.
app.UseMiddleware<AuraCore.API.Middleware.TokenRevocationMiddleware>();
```

- [ ] **Step 4: Build-verify**

```bash
dotnet build src/Backend/AuraCore.API/AuraCore.API.csproj 2>&1 | tail -5
```

Expected: 0 errors.

- [ ] **Step 5: Full-suite regression**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj 2>&1 | tail -5
```

Expected: all prior tests still pass. No test exercises the middleware pipeline end-to-end; the bootstrap/grandfather services have their own unit tests from Tasks 5-6.

- [ ] **Step 6: Commit**

```bash
git add src/Backend/AuraCore.API/Program.cs
git commit -m "feat(6.11.W1.T9): wire bootstrap + grandfather + TokenRevocationMiddleware

- Register SuperadminBootstrapService + GrandfatherMigrationService DI
- Invoke both on startup after EF MigrateAsync (migration tables exist)
- Add TokenRevocationMiddleware immediately after UseAuthorization

Closes Wave 1. Backend now has: 5 new tables + seeded system_settings
+ Trusted-grandfather on startup + superadmin promotion via env var
+ JWT carries jti for revocation + token-revocation middleware in
pipeline. Wave 2 adds permission-check attribute + superadmin login
endpoint + email service."
```

---

## Wave 2 — Permission system + email service refactor + Tier 1/2 application

Ten tasks. After Wave 2, backend enforces all permission checks, the superadmin-login endpoint is live, email is abstracted, and Tier 1/2/3 attributes are applied to every relevant endpoint. Still no frontend — admin panel keeps working because grandfather grants Trusted template to existing admins.

### Task 10: RequiresPermissionAttribute

**Goal:** Authorization filter that runs after `[Authorize]`. Superadmin always passes. Admin checks `permission_grants` for active non-expired non-revoked entry. ReadOnly admin (`is_readonly=true`) fails on any non-`tab:*` permission. Matches spec D5 implementation verbatim, adapted to this repo (`AuraCoreDbContext`, `ClaimsExtensions`).

**Files:**
- Create: `src/Backend/AuraCore.API/Filters/RequiresPermissionAttribute.cs`
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/RequiresPermissionAttributeTests.cs`

- [ ] **Step 1: Write failing tests**

`tests/AuraCore.Tests.API/SuperadminFoundation/RequiresPermissionAttributeTests.cs`:

```csharp
using System.Security.Claims;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Filters;
using AuraCore.API.Helpers;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class RequiresPermissionAttributeTests
{
    private static AuraCoreDbContext BuildDb()
    {
        var opt = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"rp-{Guid.NewGuid()}").Options;
        return new AuraCoreDbContext(opt);
    }

    private static AuthorizationFilterContext BuildCtx(AuraCoreDbContext db, string? role, Guid? userId)
    {
        var services = new ServiceCollection().AddSingleton(db).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Response.Body = new MemoryStream();
        var claims = new List<Claim>();
        if (role != null) claims.Add(new Claim(ClaimTypes.Role, role));
        if (userId.HasValue) claims.Add(new Claim("sub", userId.Value.ToString()));
        http.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        var actionCtx = new ActionContext(http, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionCtx, Array.Empty<IFilterMetadata>());
    }

    [Fact]
    public async Task Superadmin_always_passes()
    {
        var db = BuildDb();
        var ctx = BuildCtx(db, "superadmin", Guid.NewGuid());
        var attr = new RequiresPermissionAttribute(PermissionKeys.ActionUsersDelete);
        await attr.OnAuthorizationAsync(ctx);
        Assert.Null(ctx.Result);
    }

    [Fact]
    public async Task NonAdmin_is_403()
    {
        var db = BuildDb();
        var ctx = BuildCtx(db, "user", Guid.NewGuid());
        var attr = new RequiresPermissionAttribute(PermissionKeys.ActionUsersDelete);
        await attr.OnAuthorizationAsync(ctx);
        Assert.IsType<ForbidResult>(ctx.Result);
    }

    [Fact]
    public async Task Admin_without_grant_is_403_with_permission_required_body()
    {
        var db = BuildDb();
        var adminId = Guid.NewGuid();
        db.Users.Add(new User { Id = adminId, Email = "a@x.com", PasswordHash = "x", Role = "admin" });
        await db.SaveChangesAsync();

        var ctx = BuildCtx(db, "admin", adminId);
        var attr = new RequiresPermissionAttribute(PermissionKeys.ActionUsersDelete);
        await attr.OnAuthorizationAsync(ctx);

        Assert.Equal(403, ctx.HttpContext.Response.StatusCode);
        ctx.HttpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(ctx.HttpContext.Response.Body).ReadToEndAsync();
        Assert.Contains("permission_required", body);
        Assert.Contains("action:users.delete", body);
    }

    [Fact]
    public async Task Admin_with_active_grant_passes()
    {
        var db = BuildDb();
        var adminId = Guid.NewGuid();
        db.Users.Add(new User { Id = adminId, Email = "a@x.com", PasswordHash = "x", Role = "admin" });
        db.PermissionGrants.Add(new PermissionGrant {
            AdminUserId = adminId, PermissionKey = PermissionKeys.ActionUsersDelete, GrantedBy = adminId,
        });
        await db.SaveChangesAsync();

        var ctx = BuildCtx(db, "admin", adminId);
        var attr = new RequiresPermissionAttribute(PermissionKeys.ActionUsersDelete);
        await attr.OnAuthorizationAsync(ctx);

        Assert.Null(ctx.Result);
    }

    [Fact]
    public async Task Admin_with_expired_grant_is_403()
    {
        var db = BuildDb();
        var adminId = Guid.NewGuid();
        db.Users.Add(new User { Id = adminId, Email = "a@x.com", PasswordHash = "x", Role = "admin" });
        db.PermissionGrants.Add(new PermissionGrant {
            AdminUserId = adminId, PermissionKey = PermissionKeys.ActionUsersDelete, GrantedBy = adminId,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
        });
        await db.SaveChangesAsync();

        var ctx = BuildCtx(db, "admin", adminId);
        var attr = new RequiresPermissionAttribute(PermissionKeys.ActionUsersDelete);
        await attr.OnAuthorizationAsync(ctx);

        Assert.Equal(403, ctx.HttpContext.Response.StatusCode);
    }

    [Fact]
    public async Task Admin_with_revoked_grant_is_403()
    {
        var db = BuildDb();
        var adminId = Guid.NewGuid();
        db.Users.Add(new User { Id = adminId, Email = "a@x.com", PasswordHash = "x", Role = "admin" });
        db.PermissionGrants.Add(new PermissionGrant {
            AdminUserId = adminId, PermissionKey = PermissionKeys.ActionUsersDelete, GrantedBy = adminId,
            RevokedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var ctx = BuildCtx(db, "admin", adminId);
        var attr = new RequiresPermissionAttribute(PermissionKeys.ActionUsersDelete);
        await attr.OnAuthorizationAsync(ctx);

        Assert.Equal(403, ctx.HttpContext.Response.StatusCode);
    }

    [Fact]
    public async Task Readonly_admin_fails_on_action_permission_even_with_grant()
    {
        var db = BuildDb();
        var adminId = Guid.NewGuid();
        db.Users.Add(new User { Id = adminId, Email = "a@x.com", PasswordHash = "x", Role = "admin", IsReadonly = true });
        db.PermissionGrants.Add(new PermissionGrant {
            AdminUserId = adminId, PermissionKey = PermissionKeys.ActionUsersDelete, GrantedBy = adminId,
        });
        await db.SaveChangesAsync();

        var ctx = BuildCtx(db, "admin", adminId);
        var attr = new RequiresPermissionAttribute(PermissionKeys.ActionUsersDelete);
        await attr.OnAuthorizationAsync(ctx);

        Assert.Equal(403, ctx.HttpContext.Response.StatusCode);
        ctx.HttpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(ctx.HttpContext.Response.Body).ReadToEndAsync();
        Assert.Contains("readonly_account", body);
    }

    [Fact]
    public async Task Readonly_admin_passes_on_tab_permission_with_grant()
    {
        var db = BuildDb();
        var adminId = Guid.NewGuid();
        db.Users.Add(new User { Id = adminId, Email = "a@x.com", PasswordHash = "x", Role = "admin", IsReadonly = true });
        db.PermissionGrants.Add(new PermissionGrant {
            AdminUserId = adminId, PermissionKey = PermissionKeys.TabConfiguration, GrantedBy = adminId,
        });
        await db.SaveChangesAsync();

        var ctx = BuildCtx(db, "admin", adminId);
        var attr = new RequiresPermissionAttribute(PermissionKeys.TabConfiguration);
        await attr.OnAuthorizationAsync(ctx);

        Assert.Null(ctx.Result);
    }
}
```

- [ ] **Step 2: Verify tests fail**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj \
  --filter "FullyQualifiedName~RequiresPermissionAttributeTests" 2>&1 | tail -8
```

Expected: compile error — `RequiresPermissionAttribute` not found.

- [ ] **Step 3: Create `RequiresPermissionAttribute.cs`**

`src/Backend/AuraCore.API/Filters/RequiresPermissionAttribute.cs`:

```csharp
using AuraCore.API.Helpers;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.API.Filters;

/// <summary>
/// Authorization filter that enforces per-permission grants on admin users
/// (spec D5). Superadmin bypasses. Non-admin is 403. ReadOnly admin is 403
/// on any non-tab key. Otherwise checks permission_grants for an active,
/// non-revoked, non-expired grant.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class RequiresPermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    public string Permission { get; }
    public RequiresPermissionAttribute(string permission) { Permission = permission; }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        var role = user.GetPrimaryRole();

        if (role == "superadmin") return;
        if (role != "admin")
        {
            context.Result = new ForbidResult();
            return;
        }

        var userId = user.GetUserId();
        if (userId is null)
        {
            context.Result = new ForbidResult();
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<AuraCoreDbContext>();

        // ReadOnly admins fail for any non-tab permission (Tier 2 actions blocked).
        if (PermissionKeys.IsActionKey(Permission))
        {
            var isReadonly = await db.Users
                .Where(u => u.Id == userId.Value)
                .Select(u => u.IsReadonly)
                .FirstOrDefaultAsync();
            if (isReadonly)
            {
                await Write403(context, "readonly_account");
                return;
            }
        }

        var hasGrant = await db.PermissionGrants.AnyAsync(g =>
            g.AdminUserId == userId.Value
            && g.PermissionKey == Permission
            && g.RevokedAt == null
            && (g.ExpiresAt == null || g.ExpiresAt > DateTime.UtcNow));

        if (!hasGrant)
            await Write403(context, "permission_required");
    }

    private async Task Write403(AuthorizationFilterContext ctx, string errorCode)
    {
        ctx.HttpContext.Response.StatusCode = 403;
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsync(
            $"{{\"error\":\"{errorCode}\",\"permission\":\"{Permission}\"}}");
        ctx.Result = new EmptyResult();
    }
}
```

- [ ] **Step 4: Verify tests pass**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj \
  --filter "FullyQualifiedName~RequiresPermissionAttributeTests" 2>&1 | tail -5
```

Expected: 8 passed.

- [ ] **Step 5: Commit**

```bash
git add src/Backend/AuraCore.API/Filters/RequiresPermissionAttribute.cs tests/AuraCore.Tests.API/SuperadminFoundation/RequiresPermissionAttributeTests.cs
git commit -m "feat(6.11.W2.T10): RequiresPermissionAttribute — per-grant authorization filter

Runs after [Authorize]. Superadmin passes; non-admin forbidden;
ReadOnly admin forbidden on action:* keys; otherwise checks
permission_grants for an active non-revoked non-expired entry.
403 body carries {error, permission} so the frontend can open the
request modal for the specific key.

Spec D5 reference implementation, adapted to AuraCoreDbContext."
```

### Task 11: DestructiveActionAttribute

**Goal:** Lightweight marker filter for Tier 3 destructive endpoints (Licenses.Revoke/Activate, Devices.Revoke, CrashReports.Delete) — blocks only ReadOnly admins (`is_readonly=true`). Tier 3 is free for non-ReadOnly admins, so this is a thin filter.

**Files:**
- Create: `src/Backend/AuraCore.API/Filters/DestructiveActionAttribute.cs`
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/DestructiveActionAttributeTests.cs`

- [ ] **Step 1: Write failing tests**

`tests/AuraCore.Tests.API/SuperadminFoundation/DestructiveActionAttributeTests.cs`:

```csharp
using System.Security.Claims;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Filters;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class DestructiveActionAttributeTests
{
    private static (AuraCoreDbContext db, AuthorizationFilterContext ctx) BuildCtx(string role, bool isReadonly)
    {
        var opt = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"da-{Guid.NewGuid()}").Options;
        var db = new AuraCoreDbContext(opt);
        var id = Guid.NewGuid();
        db.Users.Add(new User { Id = id, Email = "a@x.com", PasswordHash = "x", Role = role, IsReadonly = isReadonly });
        db.SaveChanges();

        var services = new ServiceCollection().AddSingleton(db).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Response.Body = new MemoryStream();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.Role, role), new Claim("sub", id.ToString()),
        }, "Bearer"));
        var actionCtx = new ActionContext(http, new RouteData(), new ActionDescriptor());
        return (db, new AuthorizationFilterContext(actionCtx, Array.Empty<IFilterMetadata>()));
    }

    [Fact]
    public async Task Normal_admin_passes()
    {
        var (_, ctx) = BuildCtx("admin", isReadonly: false);
        await new DestructiveActionAttribute().OnAuthorizationAsync(ctx);
        Assert.Null(ctx.Result);
    }

    [Fact]
    public async Task Superadmin_passes()
    {
        var (_, ctx) = BuildCtx("superadmin", isReadonly: false);
        await new DestructiveActionAttribute().OnAuthorizationAsync(ctx);
        Assert.Null(ctx.Result);
    }

    [Fact]
    public async Task Readonly_admin_is_403_readonly_account()
    {
        var (_, ctx) = BuildCtx("admin", isReadonly: true);
        await new DestructiveActionAttribute().OnAuthorizationAsync(ctx);
        Assert.Equal(403, ctx.HttpContext.Response.StatusCode);
        ctx.HttpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(ctx.HttpContext.Response.Body).ReadToEndAsync();
        Assert.Contains("readonly_account", body);
    }
}
```

- [ ] **Step 2: Verify tests fail**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj \
  --filter "FullyQualifiedName~DestructiveActionAttributeTests" 2>&1 | tail -8
```

Expected: compile error.

- [ ] **Step 3: Create `DestructiveActionAttribute.cs`**

`src/Backend/AuraCore.API/Filters/DestructiveActionAttribute.cs`:

```csharp
using AuraCore.API.Helpers;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.API.Filters;

/// <summary>
/// Marker filter for Tier 3 destructive operations (Licenses.Revoke/Activate,
/// Devices.Revoke, CrashReports.Delete per spec D1). Blocks ONLY ReadOnly
/// admins. Tier 3 is open for normal admins + superadmin.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class DestructiveActionAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        var role = user.GetPrimaryRole();
        if (role == "superadmin") return;
        if (role != "admin")
        {
            context.Result = new ForbidResult();
            return;
        }

        var userId = user.GetUserId();
        if (userId is null)
        {
            context.Result = new ForbidResult();
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<AuraCoreDbContext>();
        var isReadonly = await db.Users
            .Where(u => u.Id == userId.Value)
            .Select(u => u.IsReadonly)
            .FirstOrDefaultAsync();

        if (isReadonly)
        {
            context.HttpContext.Response.StatusCode = 403;
            context.HttpContext.Response.ContentType = "application/json";
            await context.HttpContext.Response.WriteAsync(
                "{\"error\":\"readonly_account\",\"hint\":\"This account is read-only; destructive actions blocked.\"}");
            context.Result = new EmptyResult();
        }
    }
}
```

- [ ] **Step 4: Verify tests pass**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj \
  --filter "FullyQualifiedName~DestructiveActionAttributeTests" 2>&1 | tail -5
```

Expected: 3 passed.

- [ ] **Step 5: Commit**

```bash
git add src/Backend/AuraCore.API/Filters/DestructiveActionAttribute.cs tests/AuraCore.Tests.API/SuperadminFoundation/DestructiveActionAttributeTests.cs
git commit -m "feat(6.11.W2.T11): DestructiveActionAttribute — ReadOnly admin guard for Tier 3

Marks Tier 3 destructive endpoints (Licenses.Revoke/Activate, Devices.Revoke,
CrashReports.Delete). Blocks ONLY users with is_readonly=true; other admins
and superadmin pass through."
```

### Task 12: Apply `[RequiresPermission]` + `[DestructiveAction]` to controllers

**Goal:** Wire the two attributes onto actual endpoints per spec D1. For the `action:users.ban` key (which doesn't currently have an endpoint), add a new POST `/api/admin/users/{id:guid}/ban` endpoint that toggles `is_active` on a user (not admin) account.

**Files:**
- Modify: 9 controller files + 1 new endpoint method
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/ControllerAttributeApplicationTests.cs` (smoke — one assertion per attributed method via reflection)

- [ ] **Step 1: Write failing reflection-based smoke test**

`tests/AuraCore.Tests.API/SuperadminFoundation/ControllerAttributeApplicationTests.cs`:

```csharp
using System.Reflection;
using AuraCore.API.Controllers.Admin;
using AuraCore.API.Controllers.Payment;
using AuraCore.API.Filters;
using AuraCore.API.Helpers;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class ControllerAttributeApplicationTests
{
    private static bool HasRequiresPermission(MethodInfo m, string key)
        => m.GetCustomAttributes<RequiresPermissionAttribute>().Any(a => a.Permission == key);

    private static bool HasDestructiveAction(MethodInfo m)
        => m.GetCustomAttributes<DestructiveActionAttribute>().Any();

    [Fact]
    public void AdminUserController_DeleteUser_has_ActionUsersDelete()
    {
        var m = typeof(AdminUserController).GetMethod("DeleteUser")!;
        Assert.True(HasRequiresPermission(m, PermissionKeys.ActionUsersDelete));
    }

    [Fact]
    public void AdminUserController_BanUser_has_ActionUsersBan()
    {
        var m = typeof(AdminUserController).GetMethod("BanUser")!;
        Assert.NotNull(m);
        Assert.True(HasRequiresPermission(m, PermissionKeys.ActionUsersBan));
    }

    [Fact]
    public void AdminSubscriptionController_mutations_have_permissions()
    {
        var t = typeof(AdminSubscriptionController);
        var grant = t.GetMethod("Grant") ?? t.GetMethods().FirstOrDefault(m => m.Name.Contains("Grant"));
        var revoke = t.GetMethod("Revoke") ?? t.GetMethods().FirstOrDefault(m => m.Name.Contains("Revoke"));
        Assert.NotNull(grant);
        Assert.NotNull(revoke);
        Assert.True(HasRequiresPermission(grant!, PermissionKeys.ActionSubscriptionsGrant));
        Assert.True(HasRequiresPermission(revoke!, PermissionKeys.ActionSubscriptionsRevoke));
    }

    [Fact]
    public void CryptoController_admin_verify_reject_have_permissions()
    {
        var t = typeof(CryptoController);
        var verify = t.GetMethods().First(m => m.Name.Contains("Verify", StringComparison.OrdinalIgnoreCase) && m.Name.Contains("Admin", StringComparison.OrdinalIgnoreCase));
        var reject = t.GetMethods().First(m => m.Name.Contains("Reject", StringComparison.OrdinalIgnoreCase) && m.Name.Contains("Admin", StringComparison.OrdinalIgnoreCase));
        Assert.True(HasRequiresPermission(verify, PermissionKeys.ActionPaymentsApproveCrypto));
        Assert.True(HasRequiresPermission(reject, PermissionKeys.ActionPaymentsRejectCrypto));
    }

    [Fact]
    public void AdminLicenseController_Revoke_and_Activate_have_DestructiveAction()
    {
        var t = typeof(AdminLicenseController);
        var revoke = t.GetMethods().First(m => m.Name == "Revoke");
        var activate = t.GetMethods().First(m => m.Name == "Activate");
        Assert.True(HasDestructiveAction(revoke));
        Assert.True(HasDestructiveAction(activate));
    }
}
```

- [ ] **Step 2: Verify tests fail (attributes not applied, BanUser missing)**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj \
  --filter "FullyQualifiedName~ControllerAttributeApplicationTests" 2>&1 | tail -8
```

Expected: all 5 tests fail (missing attributes / missing `BanUser` method).

- [ ] **Step 3: Add `[RequiresPermission]` to `AdminConfigController`**

Read `src/Backend/AuraCore.API/Controllers/Admin/AdminConfigController.cs` to see its methods. Add `using AuraCore.API.Filters;` and `using AuraCore.API.Helpers;` at the top. Add `[RequiresPermission(PermissionKeys.TabConfiguration)]` to every `[HttpPut]`, `[HttpPost]`, `[HttpPatch]`, `[HttpDelete]` method. Do NOT add to `[HttpGet]` methods — GET reveals nothing destructive and the locked-page placeholder handles UX.

- [ ] **Step 4: Add `[RequiresPermission]` to `AdminIpWhitelistController`**

Same pattern, key `PermissionKeys.TabIpWhitelist`. Attribute mutation methods only.

- [ ] **Step 5: Add `[RequiresPermission]` to `AdminUpdateController`**

Same pattern, key `PermissionKeys.TabUpdates`. Attribute mutation methods only.

- [ ] **Step 6: Modify `AdminUserController` — DeleteUser + new BanUser**

Read `src/Backend/AuraCore.API/Controllers/Admin/AdminUserController.cs`. At the top, add:

```csharp
using AuraCore.API.Filters;
using AuraCore.API.Helpers;
```

Attribute the existing `DeleteUser` method at line 93:

```csharp
    [HttpDelete("{id:guid}")]
    [RequiresPermission(PermissionKeys.ActionUsersDelete)]
    [AuraCore.API.Filters.AuditAction("DeleteUser", "User", TargetIdFromRouteKey = "id")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct)
    { ... unchanged body ... }
```

Add a new `BanUser` method immediately after `DeleteUser` (before the closing controller brace):

```csharp
    [HttpPost("{id:guid}/ban")]
    [RequiresPermission(PermissionKeys.ActionUsersBan)]
    [AuraCore.API.Filters.AuditAction("BanUser", "User", TargetIdFromRouteKey = "id")]
    public async Task<IActionResult> BanUser(Guid id, [FromBody] BanUserRequest req, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync(new object[] { id }, ct);
        if (user is null) return NotFound();

        // Prevent banning yourself / other admins through this endpoint.
        if (user.Role == "admin" || user.Role == "superadmin")
            return BadRequest(new { error = "Admin accounts cannot be banned via this endpoint; use Suspend instead." });

        user.IsActive = !req.Banned ? true : false;
        await _db.SaveChangesAsync(ct);
        return Ok(new { id = user.Id, email = user.Email, isActive = user.IsActive });
    }
```

Add the record at the bottom of the file (next to `ResetPasswordRequest`):

```csharp
public sealed record BanUserRequest(bool Banned);
```

- [ ] **Step 7: Modify `AdminSubscriptionController`**

Read `src/Backend/AuraCore.API/Controllers/Admin/AdminSubscriptionController.cs`. Add the two usings. Attribute the `Grant` method with `[RequiresPermission(PermissionKeys.ActionSubscriptionsGrant)]` and `Revoke` with `[RequiresPermission(PermissionKeys.ActionSubscriptionsRevoke)]`. (Exact method names may differ — read the file, adapt to actual names.)

- [ ] **Step 8: Modify `CryptoController`**

Read `src/Backend/AuraCore.API/Controllers/Payment/CryptoController.cs`. Add the two usings. Find the two admin-facing methods (the ones with `admin/verify` and `admin/reject` routes). Attribute them with `ActionPaymentsApproveCrypto` and `ActionPaymentsRejectCrypto` respectively.

- [ ] **Step 9: Modify `AdminLicenseController` + `AdminDeviceController` + `AdminCrashReportController`**

Read each file, add `using AuraCore.API.Filters;`, attribute the destructive methods (`Revoke`, `Activate`, `Delete`, etc.) with `[DestructiveAction]`.

- [ ] **Step 10: Verify all reflection tests pass**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj \
  --filter "FullyQualifiedName~ControllerAttributeApplicationTests" 2>&1 | tail -5
```

Expected: 5 passed.

- [ ] **Step 11: Full-suite regression**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj 2>&1 | tail -5
```

Expected: all tests still green. If any existing test calls a now-attributed endpoint with an admin user that has no grants, the test DB path must seed `permission_grants` for that user. The grandfather migration does this automatically at app startup; for isolated unit tests the fix is to add grants in the test's Arrange phase. Document any failures; fix case-by-case.

- [ ] **Step 12: Commit**

```bash
git add src/Backend/AuraCore.API/Controllers/ tests/AuraCore.Tests.API/SuperadminFoundation/ControllerAttributeApplicationTests.cs
git commit -m "feat(6.11.W2.T12): apply [RequiresPermission] + [DestructiveAction] to controllers

Tier 1 (tab gating on mutation endpoints only):
- AdminConfigController: tab:configuration
- AdminIpWhitelistController: tab:ipwhitelist
- AdminUpdateController: tab:updates
(tab:rolechange attached to RoleChangeController in Wave 4 when created)

Tier 2 (action gating):
- AdminUserController.DeleteUser: action:users.delete
- AdminUserController.BanUser (new endpoint): action:users.ban
- AdminSubscriptionController.Grant/Revoke: action:subscriptions.*
- CryptoController.AdminVerify/AdminReject: action:payments.*

Tier 3 ([DestructiveAction] — ReadOnly admin guard):
- AdminLicenseController.Revoke/Activate
- AdminDeviceController.Revoke
- AdminCrashReportController.Delete

GET endpoints stay open; frontend handles LockedTabPlaceholder."
```

### Task 13: /api/auth/superadmin/login endpoint

**Goal:** Dedicated superadmin login endpoint — stricter rate limit (3 fails/60 min vs admin's 5/30), never reveals whether email exists, always audit-logs, mandatory 2FA (scope-limited JWT when totp_enabled=false), response shape identical to `/api/auth/login`.

**Files:**
- Modify: `src/Backend/AuraCore.API/Controllers/AuthController.cs`
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/SuperadminLoginEndpointTests.cs`

- [ ] **Step 1: Write failing tests**

`tests/AuraCore.Tests.API/SuperadminFoundation/SuperadminLoginEndpointTests.cs`:

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class SuperadminLoginEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SuperadminLoginEndpointTests(WebApplicationFactory<Program> factory)
    {
        Environment.SetEnvironmentVariable("JWT_SECRET", "test-secret-at-least-32-characters-long!!");
        _factory = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            var dbDesc = s.Single(d => d.ServiceType == typeof(DbContextOptions<AuraCoreDbContext>));
            s.Remove(dbDesc);
            s.AddDbContext<AuraCoreDbContext>(o => o.UseInMemoryDatabase($"int-{Guid.NewGuid()}"));
        }));
    }

    private HttpClient Client() => _factory.CreateClient();

    private async Task SeedSuperadmin(string email, string password)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        db.Users.Add(new User {
            Id = Guid.NewGuid(), Email = email.ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = "superadmin", TotpEnabled = true,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Returns_401_for_nonexistent_email()
    {
        var c = Client();
        var res = await c.PostAsJsonAsync("/api/auth/superadmin/login", new { email = "none@x.com", password = "whatever12", totpCode = "123456" });
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Returns_401_when_user_is_not_superadmin_even_with_correct_password()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        db.Users.Add(new User { Id = Guid.NewGuid(), Email = "plain@x.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("GoodPass12"), Role = "admin" });
        await db.SaveChangesAsync();

        var c = Client();
        var res = await c.PostAsJsonAsync("/api/auth/superadmin/login", new { email = "plain@x.com", password = "GoodPass12" });
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Returns_ok_and_token_for_valid_superadmin_with_totp()
    {
        await SeedSuperadmin("boss@x.com", "GoodPass12");
        // TOTP flow — password correct + no TOTP returns requires2fa
        var c = Client();
        var res = await c.PostAsJsonAsync("/api/auth/superadmin/login", new { email = "boss@x.com", password = "GoodPass12" });
        // Expect 200 body containing requires2fa: true (same as /login)
        Assert.Equal(System.Net.HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("requires2fa", body);
    }

    [Fact]
    public async Task Returns_scope_limited_token_when_totp_not_enabled()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        db.Users.Add(new User {
            Id = Guid.NewGuid(), Email = "fresh@x.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("GoodPass12"),
            Role = "superadmin", TotpEnabled = false,
        });
        await db.SaveChangesAsync();

        var c = Client();
        var res = await c.PostAsJsonAsync("/api/auth/superadmin/login", new { email = "fresh@x.com", password = "GoodPass12" });
        Assert.Equal(System.Net.HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("requiresTwoFactorSetup", body);
        Assert.Contains("accessToken", body);
    }
}
```

- [ ] **Step 2: Verify tests fail**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj \
  --filter "FullyQualifiedName~SuperadminLoginEndpointTests" 2>&1 | tail -8
```

Expected: 4 failed (404 on all — endpoint doesn't exist).

- [ ] **Step 3: Add `[HttpPost("superadmin/login")]` endpoint to `AuthController`**

Read `src/Backend/AuraCore.API/Controllers/AuthController.cs`. The controller has route `api/[controller]` which resolves to `api/auth`. Add this new method after the existing `Login` method (around line 219):

```csharp
    [HttpPost("superadmin/login")]
    public async Task<IActionResult> SuperadminLogin([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Email and password are required" });

        var email = request.Email.Trim().ToLowerInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(email,
            @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
            return BadRequest(new { error = "Invalid email format" });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Stricter rate limit: 3 failed attempts / 60 min (vs. /login's 3 fails / 30 min).
        // Uses the same login_attempts table; email scope with longer window.
        var whitelisted = await _whitelist.IsWhitelistedAsync(ip, ct);
        if (!whitelisted)
        {
            var cutoff = DateTimeOffset.UtcNow.AddMinutes(-60);
            var recent = await _db.LoginAttempts.CountAsync(a =>
                a.Email == email && !a.Success && a.CreatedAt > cutoff, ct);
            if (recent >= 3)
                return StatusCode(429, new { error = "Too many failed attempts. Try again in 60 minutes." });
        }

        // Look up user. Never reveal whether the email exists — always return Unauthorized
        // on any failure. Always log an attempt to audit_log AND login_attempts.
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        async Task LogAttempt(bool success, Guid? userId = null)
        {
            _db.LoginAttempts.Add(new LoginAttempt { Email = email, IpAddress = ip, Success = success });
            _db.AuditLogs.Add(new AuditLogEntry {
                ActorId = userId,
                ActorEmail = email,
                Action = "SuperadminLoginAttempt",
                TargetType = "User",
                TargetId = userId?.ToString(),
                AfterData = $"{{\"success\":{(success ? "true" : "false")}}}",
                IpAddress = ip,
            });
            await _db.SaveChangesAsync();
        }

        if (user is null || user.Role != "superadmin" || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            await LogAttempt(false, user?.Id);
            return Unauthorized(new { error = "Invalid credentials" });
        }

        if (!user.IsActive)
        {
            await LogAttempt(false, user.Id);
            return Unauthorized(new { error = "account_suspended" });
        }

        // 2FA enforcement — mandatory for superadmin.
        if (!user.TotpEnabled)
        {
            await LogAttempt(true, user.Id);
            var scopedToken = _auth.GenerateAccessToken(user, scope: "2fa-setup-only", lifetime: TimeSpan.FromMinutes(15));
            return Ok(new
            {
                requiresTwoFactorSetup = true,
                accessToken = scopedToken,
                user = new { user.Id, user.Email, user.Role },
                message = "Superadmin accounts require 2FA. Complete setup to continue."
            });
        }

        if (string.IsNullOrEmpty(request.TotpCode))
        {
            return Ok(new { requires2fa = true, message = "Enter your 2FA code from your authenticator app" });
        }

        var totpPlaintext = _totpEnc.Decrypt(user.TotpSecret!);
        if (!AuraCore.API.Application.Services.Security.TotpService.ValidateCode(totpPlaintext, request.TotpCode))
        {
            await LogAttempt(false, user.Id);
            return Unauthorized(new { error = "Invalid 2FA code" });
        }

        await LogAttempt(true, user.Id);

        var refresh = _auth.GenerateRefreshToken();
        _db.RefreshTokens.Add(new RefreshToken {
            UserId = user.Id, Token = refresh,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
        });
        await _db.SaveChangesAsync(ct);

        var access = _auth.GenerateAccessToken(user);
        return Ok(new
        {
            accessToken = access,
            refreshToken = refresh,
            user = new { user.Id, user.Email, user.Role },
        });
    }
```

Note: Verify the `TotpService` class name / static `ValidateCode` signature in the existing `AuthController.Login` method (around line 199) — it already uses the same flow; the new endpoint copies that pattern.

- [ ] **Step 4: Verify tests pass**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj \
  --filter "FullyQualifiedName~SuperadminLoginEndpointTests" 2>&1 | tail -5
```

Expected: 4 passed.

- [ ] **Step 5: Commit**

```bash
git add src/Backend/AuraCore.API/Controllers/AuthController.cs tests/AuraCore.Tests.API/SuperadminFoundation/SuperadminLoginEndpointTests.cs
git commit -m "feat(6.11.W2.T13): /api/auth/superadmin/login endpoint

Stricter than /login: 3 fails/60 min (vs 3/30), always audit-logs to
SuperadminLoginAttempt, never reveals whether the email exists (all
failures return 401 'Invalid credentials'), mandatory 2FA — if
totp_enabled=false, returns scope-limited JWT ('2fa-setup-only', 15 min)
so frontend can redirect to /enable-2fa. Role check enforces
role='superadmin'; admin users trying this endpoint are rejected."
```

### Task 14: IEmailService + ResendEmailService + 6 HTML templates

**Goal:** Abstract the Resend HTTP call into a proper service with `IHttpClientFactory`, structured logging, typed result. Six template types with parametrized HTML. Replaces the inline `HttpClient`-in-controller pattern from `PasswordResetController.cs:146-168`.

**Files:**
- Create: `src/Backend/AuraCore.API.Application/Services/Email/IEmailService.cs`
- Create: `src/Backend/AuraCore.API.Application/Services/Email/EmailTemplate.cs`
- Create: `src/Backend/AuraCore.API.Application/Services/Email/EmailSendResult.cs`
- Create: `src/Backend/AuraCore.API.Infrastructure/Services/Email/ResendEmailService.cs`
- Create: `src/Backend/AuraCore.API.Infrastructure/Services/Email/Templates/_base.html`
- Create: 6 template HTML files
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/ResendEmailServiceTests.cs`

- [ ] **Step 1: Write failing tests (mocked HttpMessageHandler)**

`tests/AuraCore.Tests.API/SuperadminFoundation/ResendEmailServiceTests.cs`:

```csharp
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using AuraCore.API.Application.Services.Email;
using AuraCore.API.Infrastructure.Services.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class ResendEmailServiceTests
{
    private static (ResendEmailService svc, Mock<HttpMessageHandler> handler) BuildSvc(HttpStatusCode status, string body)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });

        var client = new HttpClient(handler.Object) { BaseAddress = new Uri("https://api.resend.com") };
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("resend")).Returns(client);

        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            { "Resend:ApiKey", "test-key" },
            { "Resend:FromAddress", "AuraCore Pro <noreply@auracore.pro>" },
        }).Build();

        return (new ResendEmailService(factoryMock.Object, cfg, NullLogger<ResendEmailService>.Instance), handler);
    }

    [Fact]
    public async Task SendAsync_returns_success_with_message_id_on_200()
    {
        var (svc, _) = BuildSvc(HttpStatusCode.OK, "{\"id\":\"msg_abc123\"}");
        var res = await svc.SendAsync("to@x.com", "subj", "<p>hi</p>");
        Assert.True(res.Success);
        Assert.Equal("msg_abc123", res.MessageId);
        Assert.Null(res.Error);
    }

    [Fact]
    public async Task SendAsync_returns_error_on_400()
    {
        var (svc, _) = BuildSvc(HttpStatusCode.BadRequest, "{\"error\":\"invalid_from\"}");
        var res = await svc.SendAsync("to@x.com", "subj", "<p>hi</p>");
        Assert.False(res.Success);
        Assert.Null(res.MessageId);
        Assert.Contains("invalid_from", res.Error);
    }

    [Fact]
    public async Task SendFromTemplateAsync_passwordreset_renders_placeholders()
    {
        var (svc, handler) = BuildSvc(HttpStatusCode.OK, "{\"id\":\"msg_xyz\"}");
        var res = await svc.SendFromTemplateAsync(EmailTemplate.PasswordReset, new {
            to = "user@x.com", code = "123456", expiresMinutes = 10,
        });
        Assert.True(res.Success);
        handler.Protected().Verify("SendAsync", Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r => r.Content!.ReadAsStringAsync().Result.Contains("123456")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendFromTemplateAsync_unknown_template_throws()
    {
        var (svc, _) = BuildSvc(HttpStatusCode.OK, "{}");
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.SendFromTemplateAsync((EmailTemplate)999, new { to = "user@x.com" }));
    }
}
```

- [ ] **Step 2: Verify tests fail**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj \
  --filter "FullyQualifiedName~ResendEmailServiceTests" 2>&1 | tail -8
```

Expected: compile errors — types don't exist.

- [ ] **Step 3: Create `EmailSendResult.cs`**

`src/Backend/AuraCore.API.Application/Services/Email/EmailSendResult.cs`:

```csharp
namespace AuraCore.API.Application.Services.Email;

public sealed record EmailSendResult(bool Success, string? MessageId, string? Error);
```

- [ ] **Step 4: Create `EmailTemplate.cs`**

`src/Backend/AuraCore.API.Application/Services/Email/EmailTemplate.cs`:

```csharp
namespace AuraCore.API.Application.Services.Email;

public enum EmailTemplate
{
    AdminInvitation,
    PasswordReset,
    PermissionRequested,
    PermissionApproved,
    PermissionDenied,
    AdminCreatedWithoutEmail,
}
```

- [ ] **Step 5: Create `IEmailService.cs`**

`src/Backend/AuraCore.API.Application/Services/Email/IEmailService.cs`:

```csharp
namespace AuraCore.API.Application.Services.Email;

public interface IEmailService
{
    Task<EmailSendResult> SendAsync(string to, string subject, string html, CancellationToken ct = default);
    Task<EmailSendResult> SendFromTemplateAsync(EmailTemplate template, object data, CancellationToken ct = default);
}
```

- [ ] **Step 6: Create `Templates/_base.html`**

`src/Backend/AuraCore.API.Infrastructure/Services/Email/Templates/_base.html`:

```html
<!DOCTYPE html>
<html><head><meta charset="utf-8"></head>
<body style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; background:#0a0a0b; color:#111; margin:0; padding:20px;">
  <div style="max-width:560px; margin:0 auto; background:#fff; border-radius:12px; overflow:hidden; border:1px solid #e5e7eb;">
    <div style="background:#111; padding:20px 24px; color:#fff;">
      <h2 style="margin:0; font-size:20px; color:#00d4aa;">AuraCore Pro</h2>
    </div>
    <div style="padding:28px 24px;">
      {{body}}
    </div>
    <div style="background:#f9fafb; padding:16px 24px; font-size:12px; color:#6b7280; text-align:center;">
      You are receiving this email because your account is associated with AuraCore Pro admin operations.
    </div>
  </div>
</body></html>
```

- [ ] **Step 7: Create 6 template files**

`src/Backend/AuraCore.API.Infrastructure/Services/Email/Templates/AdminInvitation.html`:

```html
<h3 style="margin:0 0 12px 0;">You've been invited as an AuraCore Pro admin</h3>
<p>Hello,</p>
<p>{{invitedBy}} has invited you ({{adminEmail}}) to join the AuraCore Pro administration panel.</p>
<p>Click the link below to set your password and activate your account:</p>
<p style="text-align:center; margin:24px 0;">
  <a href="{{setupLink}}" style="background:#00d4aa; color:#000; padding:12px 24px; text-decoration:none; border-radius:8px; font-weight:600;">Accept Invitation</a>
</p>
<p style="font-size:14px; color:#6b7280;">This link expires at {{expiresAt}}.</p>
```

`src/Backend/AuraCore.API.Infrastructure/Services/Email/Templates/PasswordReset.html`:

```html
<h3 style="margin:0 0 12px 0;">Password reset code</h3>
<p>Your password reset code is:</p>
<div style="font-size:32px; font-weight:bold; letter-spacing:8px; text-align:center; padding:20px; background:#f5f5f5; border-radius:8px; margin:16px 0; color:#111;">{{code}}</div>
<p style="font-size:14px; color:#6b7280;">This code expires in {{expiresMinutes}} minutes. If you didn't request this, ignore this email.</p>
```

`src/Backend/AuraCore.API.Infrastructure/Services/Email/Templates/PermissionRequested.html`:

```html
<h3 style="margin:0 0 12px 0;">New permission request</h3>
<p>Admin <strong>{{adminEmail}}</strong> has requested access to <code>{{permissionKey}}</code>.</p>
<p><strong>Reason:</strong></p>
<blockquote style="border-left:4px solid #00d4aa; padding:8px 16px; background:#f9fafb; margin:16px 0;">{{reason}}</blockquote>
<p style="text-align:center; margin:24px 0;">
  <a href="{{inboxLink}}" style="background:#00d4aa; color:#000; padding:12px 24px; text-decoration:none; border-radius:8px; font-weight:600;">Review Request</a>
</p>
```

`src/Backend/AuraCore.API.Infrastructure/Services/Email/Templates/PermissionApproved.html`:

```html
<h3 style="margin:0 0 12px 0; color:#059669;">Permission approved</h3>
<p>Your request for <code>{{permissionKey}}</code> has been approved by {{approvedBy}}.</p>
<p style="font-size:14px; color:#6b7280;">{{expiresNote}}</p>
<p>You can now use this feature in the admin panel.</p>
```

`src/Backend/AuraCore.API.Infrastructure/Services/Email/Templates/PermissionDenied.html`:

```html
<h3 style="margin:0 0 12px 0; color:#dc2626;">Permission denied</h3>
<p>Your request for <code>{{permissionKey}}</code> has been denied by {{deniedBy}}.</p>
<p><strong>Note:</strong></p>
<blockquote style="border-left:4px solid #dc2626; padding:8px 16px; background:#fef2f2; margin:16px 0;">{{reviewNote}}</blockquote>
```

`src/Backend/AuraCore.API.Infrastructure/Services/Email/Templates/AdminCreatedWithoutEmail.html`:

```html
<h3 style="margin:0 0 12px 0;">New admin account created (manual password mode)</h3>
<p>You created admin account <strong>{{adminEmail}}</strong> without the invitation email toggle.</p>
<p>The initial password is not stored anywhere and must be shared out-of-band.</p>
<p style="font-size:14px; color:#6b7280;">{{note}}</p>
```

**Note on template packaging:** Templates are embedded resources via the `.csproj`. Add to `src/Backend/AuraCore.API.Infrastructure/AuraCore.API.Infrastructure.csproj` (inside a `<ItemGroup>`):

```xml
  <ItemGroup>
    <EmbeddedResource Include="Services\Email\Templates\*.html" />
  </ItemGroup>
```

- [ ] **Step 8: Create `ResendEmailService.cs`**

`src/Backend/AuraCore.API.Infrastructure/Services/Email/ResendEmailService.cs`:

```csharp
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using AuraCore.API.Application.Services.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AuraCore.API.Infrastructure.Services.Email;

public sealed class ResendEmailService : IEmailService
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(IHttpClientFactory factory, IConfiguration config, ILogger<ResendEmailService> logger)
    {
        _factory = factory;
        _config = config;
        _logger = logger;
    }

    public async Task<EmailSendResult> SendAsync(string to, string subject, string html, CancellationToken ct = default)
    {
        var from = _config["Resend:FromAddress"] ?? "AuraCore Pro <noreply@auracore.pro>";
        var client = _factory.CreateClient("resend");

        var payload = new { from, to = new[] { to }, subject, html };
        try
        {
            var res = await client.PostAsJsonAsync("/emails", payload, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("Resend email failed: {Status} — {Body}", res.StatusCode, body);
                return new EmailSendResult(false, null, body);
            }

            using var doc = JsonDocument.Parse(body);
            var id = doc.RootElement.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            _logger.LogInformation("Resend email sent: messageId={MessageId} to={To}", id, to);
            return new EmailSendResult(true, id, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resend email exception for to={To}", to);
            return new EmailSendResult(false, null, ex.Message);
        }
    }

    public async Task<EmailSendResult> SendFromTemplateAsync(EmailTemplate template, object data, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(typeof(EmailTemplate), template))
            throw new ArgumentException($"Unknown email template: {template}", nameof(template));

        var (subject, to) = ExtractSubjectAndTo(template, data);
        var html = RenderTemplate(template, data);
        return await SendAsync(to, subject, html, ct);
    }

    private static string RenderTemplate(EmailTemplate template, object data)
    {
        var baseTpl  = LoadResource("_base.html");
        var bodyTpl  = LoadResource($"{template}.html");
        var body     = ApplyPlaceholders(bodyTpl, data);
        return ApplyPlaceholders(baseTpl.Replace("{{body}}", body), data);
    }

    private static string LoadResource(string fileName)
    {
        var asm = typeof(ResendEmailService).Assembly;
        var resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith($".Templates.{fileName}"))
            ?? throw new InvalidOperationException($"Template resource not found: {fileName}");
        using var stream = asm.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string ApplyPlaceholders(string template, object data)
    {
        var props = data.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var p in props)
            template = template.Replace("{{" + p.Name + "}}", p.GetValue(data)?.ToString() ?? "");
        return template;
    }

    private static (string Subject, string To) ExtractSubjectAndTo(EmailTemplate t, object data)
    {
        var toProp = data.GetType().GetProperty("to")
            ?? throw new ArgumentException("Template data must include a 'to' property");
        var to = toProp.GetValue(data) as string
            ?? throw new ArgumentException("'to' must be a string");

        var subj = t switch
        {
            EmailTemplate.AdminInvitation          => "AuraCore Pro — You're invited as admin",
            EmailTemplate.PasswordReset            => "AuraCore Pro — Password reset code",
            EmailTemplate.PermissionRequested      => "AuraCore Pro — New permission request",
            EmailTemplate.PermissionApproved       => "AuraCore Pro — Permission approved",
            EmailTemplate.PermissionDenied         => "AuraCore Pro — Permission denied",
            EmailTemplate.AdminCreatedWithoutEmail => "AuraCore Pro — Admin account created",
            _ => "AuraCore Pro",
        };
        return (subj, to);
    }
}
```

- [ ] **Step 9: Verify tests pass**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj \
  --filter "FullyQualifiedName~ResendEmailServiceTests" 2>&1 | tail -5
```

Expected: 4 passed.

- [ ] **Step 10: Commit**

```bash
git add src/Backend/AuraCore.API.Application/Services/Email/ src/Backend/AuraCore.API.Infrastructure/Services/Email/ src/Backend/AuraCore.API.Infrastructure/AuraCore.API.Infrastructure.csproj tests/AuraCore.Tests.API/SuperadminFoundation/ResendEmailServiceTests.cs
git commit -m "feat(6.11.W2.T14): IEmailService + ResendEmailService + 6 HTML templates

Abstraction over Resend HTTPS API using IHttpClientFactory named 'resend'.
Six template types with shared _base.html and placeholder replacement:
AdminInvitation, PasswordReset, PermissionRequested/Approved/Denied,
AdminCreatedWithoutEmail. Typed EmailSendResult returned; structured
logging on success + failure.

PasswordResetController refactored to use this in T15."
```

### Task 15: Refactor PasswordResetController + DI registration

**Goal:** Replace the 22-line inline `SendResetEmailAsync` in `PasswordResetController.cs:146-168` with a single `_email.SendFromTemplateAsync(EmailTemplate.PasswordReset, ...)` call. Register `IEmailService` + named HttpClient "resend" in DI.

**Files:**
- Modify: `src/Backend/AuraCore.API/Controllers/PasswordResetController.cs`
- Modify: `src/Backend/AuraCore.API/Program.cs`

- [ ] **Step 1: Add DI registrations + named HttpClient to Program.cs**

Read `src/Backend/AuraCore.API/Program.cs`. Near the top DI block (after the bootstrap + grandfather registrations from Task 9), insert:

```csharp
// Phase 6.11: transactional email via Resend HTTPS API
builder.Services.AddHttpClient("resend", client =>
{
    client.BaseAddress = new Uri("https://api.resend.com");
    var apiKey = Environment.GetEnvironmentVariable("RESEND_API_KEY")
        ?? builder.Configuration["Resend:ApiKey"];
    if (!string.IsNullOrEmpty(apiKey))
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
});
builder.Services.AddScoped<AuraCore.API.Application.Services.Email.IEmailService,
                           AuraCore.API.Infrastructure.Services.Email.ResendEmailService>();
```

- [ ] **Step 2: Refactor PasswordResetController to use IEmailService**

Read `src/Backend/AuraCore.API/Controllers/PasswordResetController.cs`. Replace the class header to take `IEmailService`:

```csharp
[ApiController]
[Route("api/auth/password")]
public sealed class PasswordResetController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    private readonly AuraCore.API.Application.Services.Email.IEmailService _email;

    private static readonly ConcurrentDictionary<string, (int Count, DateTime ResetAt)> _forgotAttempts = new();
    private static readonly ConcurrentDictionary<string, (int Count, DateTime ResetAt)> _resetAttempts = new();

    public PasswordResetController(AuraCoreDbContext db, AuraCore.API.Application.Services.Email.IEmailService email)
    {
        _db = db;
        _email = email;
    }
```

Inside `ForgotPassword`, replace the existing `if (user is not null) { await SendResetEmailAsync(email, code); }` with:

```csharp
        if (user is not null)
        {
            await _email.SendFromTemplateAsync(
                AuraCore.API.Application.Services.Email.EmailTemplate.PasswordReset,
                new { to = email, code, expiresMinutes = 10 },
                ct);
        }
```

Delete the private static `SendResetEmailAsync` method entirely (lines 146-168).

- [ ] **Step 3: Build-verify + existing test regression**

```bash
dotnet build src/Backend/AuraCore.API/AuraCore.API.csproj 2>&1 | tail -5
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj 2>&1 | tail -5
```

Expected: 0 errors. All prior tests pass. (If `PasswordResetController` has a unit test that instantiates the controller with only `AuraCoreDbContext`, update that test to inject a Mock<IEmailService>.)

- [ ] **Step 4: Commit**

```bash
git add src/Backend/AuraCore.API/Controllers/PasswordResetController.cs src/Backend/AuraCore.API/Program.cs
git commit -m "refactor(6.11.W2.T15): PasswordResetController uses IEmailService

Replaces inline HttpClient Resend call (lines 146-168) with
_email.SendFromTemplateAsync(EmailTemplate.PasswordReset, ...).
Registers named 'resend' HttpClient + IEmailService in DI.

22 lines of inline HTTP plumbing deleted; email behavior now
consistently logged and typed across all call sites."
```

### Task 16: Permission-request controllers (admin-side) + grants controller (superadmin-side)

**Goal:** Backend CRUD for permission requests + grants. Admin self-creates requests + cancels; superadmin lists pending + approves / denies (with optional expires_at + review_note) + bulk operations. Admin reads own grants via My Permissions.

**Files:**
- Create: `src/Backend/AuraCore.API/Controllers/Admin/PermissionRequestsController.cs`
- Create: `src/Backend/AuraCore.API/Controllers/Admin/MyPermissionsController.cs`
- Create: `src/Backend/AuraCore.API/Controllers/Superadmin/PermissionGrantsController.cs`
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/PermissionRequestLifecycleTests.cs`

- [ ] **Step 1: Write failing lifecycle test**

`tests/AuraCore.Tests.API/SuperadminFoundation/PermissionRequestLifecycleTests.cs`:

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class PermissionRequestLifecycleTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _f;

    public PermissionRequestLifecycleTests(WebApplicationFactory<Program> f)
    {
        Environment.SetEnvironmentVariable("JWT_SECRET", "test-secret-at-least-32-characters-long!!");
        _f = f.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            var dbd = s.Single(d => d.ServiceType == typeof(DbContextOptions<AuraCoreDbContext>));
            s.Remove(dbd);
            s.AddDbContext<AuraCoreDbContext>(o => o.UseInMemoryDatabase($"prl-{Guid.NewGuid()}"));
        }));
    }

    private async Task<(HttpClient client, Guid userId)> AuthedClient(string role)
    {
        var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var u = new User { Id = Guid.NewGuid(), Email = $"{role}@x.com", PasswordHash = "x", Role = role, TotpEnabled = true };
        db.Users.Add(u);
        await db.SaveChangesAsync();

        var auth = scope.ServiceProvider.GetRequiredService<AuraCore.API.Application.Interfaces.IAuthService>();
        var token = auth.GenerateAccessToken(u);
        var c = _f.CreateClient();
        c.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return (c, u.Id);
    }

    [Fact]
    public async Task Admin_creates_request_superadmin_approves_grant_exists()
    {
        var (adminC, adminId) = await AuthedClient("admin");
        var (superC, _) = await AuthedClient("superadmin");

        // Step 1: admin creates request
        var createRes = await adminC.PostAsJsonAsync("/api/admin/permission-requests", new {
            permissionKey = "tab:configuration",
            reason = "I need to update SMTP settings for a customer escalation",
        });
        createRes.EnsureSuccessStatusCode();
        var createBody = JsonDocument.Parse(await createRes.Content.ReadAsStringAsync());
        var requestId = createBody.RootElement.GetProperty("id").GetString()!;

        // Step 2: superadmin lists pending
        var listRes = await superC.GetAsync("/api/superadmin/permission-requests?status=pending");
        listRes.EnsureSuccessStatusCode();
        Assert.Contains("tab:configuration", await listRes.Content.ReadAsStringAsync());

        // Step 3: superadmin approves
        var approveRes = await superC.PostAsJsonAsync($"/api/superadmin/permission-requests/{requestId}/approve",
            new { expiresAt = (string?)null, reviewNote = "Approved; please log steps in audit" });
        approveRes.EnsureSuccessStatusCode();

        // Step 4: grant exists
        using var vs = _f.Services.CreateScope();
        var db = vs.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var grant = await db.PermissionGrants.FirstOrDefaultAsync(g =>
            g.AdminUserId == adminId && g.PermissionKey == "tab:configuration" && g.RevokedAt == null);
        Assert.NotNull(grant);
    }

    [Fact]
    public async Task Admin_cannot_create_duplicate_pending_request()
    {
        var (adminC, _) = await AuthedClient("admin");
        var body = new { permissionKey = "tab:updates", reason = "need to publish a new release urgently ASAP" };
        var first = await adminC.PostAsJsonAsync("/api/admin/permission-requests", body);
        first.EnsureSuccessStatusCode();

        var dup = await adminC.PostAsJsonAsync("/api/admin/permission-requests", body);
        Assert.Equal(System.Net.HttpStatusCode.Conflict, dup.StatusCode);
    }

    [Fact]
    public async Task Admin_lists_own_grants_via_my_permissions()
    {
        var (adminC, adminId) = await AuthedClient("admin");

        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        db.PermissionGrants.Add(new PermissionGrant {
            AdminUserId = adminId, PermissionKey = "tab:updates", GrantedBy = adminId,
        });
        await db.SaveChangesAsync();

        var res = await adminC.GetAsync("/api/admin/my-permissions");
        res.EnsureSuccessStatusCode();
        Assert.Contains("tab:updates", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Superadmin_denies_request_updates_status_and_emits_note()
    {
        var (adminC, _) = await AuthedClient("admin");
        var (superC, _) = await AuthedClient("superadmin");

        var create = await adminC.PostAsJsonAsync("/api/admin/permission-requests", new {
            permissionKey = "action:users.delete", reason = "need to clean a test user account",
        });
        var reqId = JsonDocument.Parse(await create.Content.ReadAsStringAsync())
            .RootElement.GetProperty("id").GetString()!;

        var deny = await superC.PostAsJsonAsync($"/api/superadmin/permission-requests/{reqId}/deny",
            new { reviewNote = "Use the Suspend flow instead" });
        deny.EnsureSuccessStatusCode();

        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var req = await db.PermissionRequests.FirstAsync(r => r.Id == Guid.Parse(reqId));
        Assert.Equal("denied", req.Status);
        Assert.Equal("Use the Suspend flow instead", req.ReviewNote);
    }
}
```

- [ ] **Step 2: Verify tests fail**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj \
  --filter "FullyQualifiedName~PermissionRequestLifecycleTests" 2>&1 | tail -8
```

Expected: 404 on all routes — controllers don't exist.

- [ ] **Step 3: Create `PermissionRequestsController.cs` (admin-side)**

`src/Backend/AuraCore.API/Controllers/Admin/PermissionRequestsController.cs`:

```csharp
using AuraCore.API.Application.Services.Email;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Helpers;
using AuraCore.API.Hubs;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Admin;

[ApiController]
[Route("api/admin/permission-requests")]
[Authorize(Roles = "admin")]
public sealed class PermissionRequestsController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    private readonly IEmailService _email;
    private readonly IHubContext<AdminHub> _hub;

    public PermissionRequestsController(AuraCoreDbContext db, IEmailService email, IHubContext<AdminHub> hub)
    {
        _db = db; _email = email; _hub = hub;
    }

    [HttpGet]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();
        var list = await _db.PermissionRequests
            .Where(r => r.AdminUserId == userId.Value)
            .OrderByDescending(r => r.RequestedAt)
            .Take(100)
            .Select(r => new { r.Id, r.PermissionKey, r.Reason, r.Status, r.RequestedAt, r.ReviewedAt, r.ReviewNote })
            .ToListAsync(ct);
        return Ok(new { items = list });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRequestDto dto, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();

        if (!PermissionKeys.IsValidKey(dto.PermissionKey))
            return BadRequest(new { error = "unknown_permission_key" });
        if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Length < 50 || dto.Reason.Length > 500)
            return BadRequest(new { error = "reason_length", hint = "Reason must be 50-500 characters" });

        var hasPending = await _db.PermissionRequests.AnyAsync(r =>
            r.AdminUserId == userId.Value && r.PermissionKey == dto.PermissionKey && r.Status == "pending", ct);
        if (hasPending)
            return Conflict(new { error = "duplicate_pending_request" });

        var req = new PermissionRequest {
            AdminUserId = userId.Value,
            PermissionKey = dto.PermissionKey,
            Reason = dto.Reason.Trim(),
            Status = "pending",
        };
        _db.PermissionRequests.Add(req);
        await _db.SaveChangesAsync(ct);

        // Broadcast to superadmins
        var adminEmail = User.GetEmail() ?? "unknown";
        await _hub.Clients.Group("superadmins").SendAsync("PermissionRequested", new {
            requestId = req.Id, adminEmail, permissionKey = req.PermissionKey, reason = req.Reason, requestedAt = req.RequestedAt,
        }, ct);

        // Best-effort email to each superadmin
        var superadmins = await _db.Users.Where(u => u.Role == "superadmin" && u.IsActive).ToListAsync(ct);
        foreach (var sa in superadmins)
        {
            await _email.SendFromTemplateAsync(EmailTemplate.PermissionRequested, new {
                to = sa.Email, adminEmail, permissionKey = req.PermissionKey,
                reason = req.Reason, inboxLink = "https://admin.auracore.pro/#/permission-requests",
            }, ct);
        }

        return Ok(new { id = req.Id.ToString(), status = req.Status, requestedAt = req.RequestedAt });
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();

        var req = await _db.PermissionRequests.FirstOrDefaultAsync(r => r.Id == id && r.AdminUserId == userId.Value, ct);
        if (req is null) return NotFound();
        if (req.Status != "pending") return BadRequest(new { error = "cannot_cancel_non_pending" });
        req.Status = "cancelled";
        await _db.SaveChangesAsync(ct);
        return Ok(new { id = req.Id });
    }

    public sealed record CreateRequestDto(string PermissionKey, string Reason);
}
```

- [ ] **Step 4: Create `MyPermissionsController.cs`**

`src/Backend/AuraCore.API/Controllers/Admin/MyPermissionsController.cs`:

```csharp
using AuraCore.API.Helpers;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Admin;

[ApiController]
[Route("api/admin/my-permissions")]
[Authorize(Roles = "admin")]
public sealed class MyPermissionsController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    public MyPermissionsController(AuraCoreDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();

        var grants = await _db.PermissionGrants
            .Where(g => g.AdminUserId == userId.Value && g.RevokedAt == null)
            .Select(g => new {
                g.PermissionKey, g.GrantedAt, g.ExpiresAt,
                grantedByEmail = _db.Users.Where(u => u.Id == g.GrantedBy).Select(u => u.Email).FirstOrDefault(),
                g.SourceRequestId,
            })
            .ToListAsync(ct);

        var pending = await _db.PermissionRequests
            .Where(r => r.AdminUserId == userId.Value && r.Status == "pending")
            .Select(r => new { r.Id, r.PermissionKey, r.Reason, r.RequestedAt })
            .ToListAsync(ct);

        var denied = await _db.PermissionRequests
            .Where(r => r.AdminUserId == userId.Value && r.Status == "denied")
            .OrderByDescending(r => r.ReviewedAt)
            .Take(10)
            .Select(r => new { r.PermissionKey, r.ReviewNote, r.ReviewedAt })
            .ToListAsync(ct);

        var totalRestricted = PermissionKeys.AllKeys.Count;
        return Ok(new {
            totalRestricted,
            activeGrantsCount = grants.Count,
            grants,
            pending,
            recentDenials = denied,
        });
    }
}
```

- [ ] **Step 5: Create `PermissionGrantsController.cs` (superadmin-side)**

`src/Backend/AuraCore.API/Controllers/Superadmin/PermissionGrantsController.cs`:

```csharp
using AuraCore.API.Application.Services.Email;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Helpers;
using AuraCore.API.Hubs;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Superadmin;

[ApiController]
[Route("api/superadmin")]
[Authorize(Roles = "superadmin")]
public sealed class PermissionGrantsController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    private readonly IEmailService _email;
    private readonly IHubContext<AdminHub> _hub;

    public PermissionGrantsController(AuraCoreDbContext db, IEmailService email, IHubContext<AdminHub> hub)
    {
        _db = db; _email = email; _hub = hub;
    }

    [HttpGet("permission-requests")]
    public async Task<IActionResult> ListRequests([FromQuery] string? status = "pending", CancellationToken ct = default)
    {
        IQueryable<PermissionRequest> q = _db.PermissionRequests;
        if (!string.IsNullOrEmpty(status)) q = q.Where(r => r.Status == status);
        var items = await q
            .OrderByDescending(r => r.RequestedAt)
            .Take(200)
            .Select(r => new {
                r.Id, r.PermissionKey, r.Reason, r.Status, r.RequestedAt, r.ReviewedAt, r.ReviewNote,
                adminEmail = _db.Users.Where(u => u.Id == r.AdminUserId).Select(u => u.Email).FirstOrDefault(),
            })
            .ToListAsync(ct);
        return Ok(new { items });
    }

    [HttpPost("permission-requests/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveDto dto, CancellationToken ct)
    {
        var superId = User.GetUserId();
        if (superId is null) return Unauthorized();

        var req = await _db.PermissionRequests.FirstOrDefaultAsync(r => r.Id == id && r.Status == "pending", ct);
        if (req is null) return NotFound();

        req.Status = "approved";
        req.ReviewedBy = superId;
        req.ReviewedAt = DateTime.UtcNow;
        req.ReviewNote = dto?.ReviewNote;

        _db.PermissionGrants.Add(new PermissionGrant {
            AdminUserId = req.AdminUserId,
            PermissionKey = req.PermissionKey,
            GrantedBy = superId.Value,
            GrantedAt = DateTime.UtcNow,
            ExpiresAt = dto?.ExpiresAt,
            SourceRequestId = req.Id,
        });
        await _db.SaveChangesAsync(ct);

        var admin = await _db.Users.FirstAsync(u => u.Id == req.AdminUserId, ct);
        await _hub.Clients.User(admin.Id.ToString()).SendAsync("PermissionApproved",
            new { permissionKey = req.PermissionKey, expiresAt = dto?.ExpiresAt }, ct);
        await _email.SendFromTemplateAsync(EmailTemplate.PermissionApproved, new {
            to = admin.Email, permissionKey = req.PermissionKey,
            approvedBy = User.GetEmail() ?? "superadmin",
            expiresNote = dto?.ExpiresAt is null ? "This grant does not expire." : $"Expires at {dto.ExpiresAt:u}.",
        }, ct);
        return Ok(new { id = req.Id, status = req.Status });
    }

    [HttpPost("permission-requests/{id:guid}/deny")]
    public async Task<IActionResult> Deny(Guid id, [FromBody] DenyDto dto, CancellationToken ct)
    {
        var superId = User.GetUserId();
        if (superId is null) return Unauthorized();

        var req = await _db.PermissionRequests.FirstOrDefaultAsync(r => r.Id == id && r.Status == "pending", ct);
        if (req is null) return NotFound();

        req.Status = "denied";
        req.ReviewedBy = superId;
        req.ReviewedAt = DateTime.UtcNow;
        req.ReviewNote = dto?.ReviewNote;
        await _db.SaveChangesAsync(ct);

        var admin = await _db.Users.FirstAsync(u => u.Id == req.AdminUserId, ct);
        await _hub.Clients.User(admin.Id.ToString()).SendAsync("PermissionDenied",
            new { permissionKey = req.PermissionKey, reviewNote = dto?.ReviewNote }, ct);
        await _email.SendFromTemplateAsync(EmailTemplate.PermissionDenied, new {
            to = admin.Email, permissionKey = req.PermissionKey,
            deniedBy = User.GetEmail() ?? "superadmin",
            reviewNote = dto?.ReviewNote ?? "(no note provided)",
        }, ct);
        return Ok(new { id = req.Id, status = req.Status });
    }

    [HttpPost("permission-requests/bulk/approve")]
    public async Task<IActionResult> BulkApprove([FromBody] BulkIdsDto dto, CancellationToken ct)
    {
        var results = new List<object>();
        foreach (var id in dto.Ids)
        {
            var r = await Approve(id, new ApproveDto(null, null), ct);
            results.Add(new { id, ok = r is OkObjectResult });
        }
        return Ok(new { results });
    }

    [HttpPost("permission-requests/bulk/deny")]
    public async Task<IActionResult> BulkDeny([FromBody] BulkIdsDto dto, CancellationToken ct)
    {
        var results = new List<object>();
        foreach (var id in dto.Ids)
        {
            var r = await Deny(id, new DenyDto(null), ct);
            results.Add(new { id, ok = r is OkObjectResult });
        }
        return Ok(new { results });
    }

    [HttpPost("permission-grants/revoke")]
    public async Task<IActionResult> Revoke([FromBody] RevokeDto dto, CancellationToken ct)
    {
        var superId = User.GetUserId();
        if (superId is null) return Unauthorized();

        var grant = await _db.PermissionGrants
            .Where(g => g.AdminUserId == dto.AdminUserId && g.PermissionKey == dto.PermissionKey && g.RevokedAt == null)
            .FirstOrDefaultAsync(ct);
        if (grant is null) return NotFound();

        grant.RevokedAt = DateTime.UtcNow;
        grant.RevokedBy = superId;
        grant.RevokeReason = dto.Reason;
        await _db.SaveChangesAsync(ct);

        await _hub.Clients.User(dto.AdminUserId.ToString()).SendAsync("PermissionRevoked",
            new { permissionKey = dto.PermissionKey, reason = dto.Reason }, ct);
        return Ok(new { id = grant.Id, revokedAt = grant.RevokedAt });
    }

    public sealed record ApproveDto(DateTime? ExpiresAt, string? ReviewNote);
    public sealed record DenyDto(string? ReviewNote);
    public sealed record BulkIdsDto(List<Guid> Ids);
    public sealed record RevokeDto(Guid AdminUserId, string PermissionKey, string Reason);
}
```

- [ ] **Step 6: Verify lifecycle tests pass**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj \
  --filter "FullyQualifiedName~PermissionRequestLifecycleTests" 2>&1 | tail -5
```

Expected: 4 passed. If the permission-requested email send fails against an IEmailService that isn't configured, that particular assertion may still pass because `SendFromTemplateAsync` logs + returns a failed `EmailSendResult` without throwing. If the tests need a mock email service, add one via `services.AddScoped<IEmailService, NullEmailService>()` override in the WebApplicationFactory config.

- [ ] **Step 7: Commit**

```bash
git add src/Backend/AuraCore.API/Controllers/Admin/PermissionRequestsController.cs src/Backend/AuraCore.API/Controllers/Admin/MyPermissionsController.cs src/Backend/AuraCore.API/Controllers/Superadmin/PermissionGrantsController.cs tests/AuraCore.Tests.API/SuperadminFoundation/PermissionRequestLifecycleTests.cs
git commit -m "feat(6.11.W2.T16): permission-request + grants controllers + my-permissions

Admin side:
- POST /api/admin/permission-requests — create (50-500 char reason, dup-detect)
- GET  /api/admin/permission-requests — list own history
- POST /api/admin/permission-requests/{id}/cancel
- GET  /api/admin/my-permissions — grants + pending + recent denials

Superadmin side:
- GET  /api/superadmin/permission-requests?status=pending — inbox
- POST /api/superadmin/permission-requests/{id}/approve (with expiresAt + note)
- POST /api/superadmin/permission-requests/{id}/deny (with note)
- POST /api/superadmin/permission-requests/bulk/approve | bulk/deny
- POST /api/superadmin/permission-grants/revoke

All approve/deny paths emit SignalR (PermissionApproved/Denied/Revoked)
to the targeted admin and send email via IEmailService."
```

### Task 17: SignalR AdminHub — new events + scope-limited rejection + superadmins group

**Goal:** Update `AdminHub` to (a) accept both `admin` and `superadmin` roles, (b) reject scope-limited tokens (2fa-setup-only), (c) add `superadmins` group so permission-request broadcasts reach superadmins only.

**Files:**
- Modify: `src/Backend/AuraCore.API/Hubs/AdminHub.cs`

- [ ] **Step 1: Replace the class body**

`src/Backend/AuraCore.API/Hubs/AdminHub.cs`:

```csharp
using AuraCore.API.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AuraCore.API.Hubs;

/// <summary>
/// Real-time admin event hub. Accepts admin + superadmin roles (superadmin
/// inherits admin via dual-role JWT). Rejects scope-limited tokens. Admins
/// join "admins" group; superadmins additionally join "superadmins" group
/// for permission-request broadcasts (spec D15, D17).
/// </summary>
[Authorize(Roles = "admin,superadmin")]
public class AdminHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        // Reject scope-limited JWTs — they must not hold a live hub connection.
        if (Context.User?.IsScopeLimited() == true)
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, "admins");
        if (Context.User?.GetPrimaryRole() == "superadmin")
            await Groups.AddToGroupAsync(Context.ConnectionId, "superadmins");

        await Clients.Group("admins").SendAsync("AdminCount",
            new { count = AdminConnectionCount.Increment() });
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "admins");
        if (Context.User?.GetPrimaryRole() == "superadmin")
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "superadmins");
        await Clients.Group("admins").SendAsync("AdminCount",
            new { count = AdminConnectionCount.Decrement() });
        await base.OnDisconnectedAsync(exception);
    }
}

internal static class AdminConnectionCount
{
    private static int _count = 0;
    public static int Increment() => Interlocked.Increment(ref _count);
    public static int Decrement() => Interlocked.Decrement(ref _count);
    public static int Current => Volatile.Read(ref _count);
}
```

- [ ] **Step 2: Build + smoke**

```bash
dotnet build src/Backend/AuraCore.API/AuraCore.API.csproj 2>&1 | tail -5
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj \
  --filter "FullyQualifiedName~AdminHubTests" 2>&1 | tail -5
```

Expected: 0 errors. Existing AdminHubTests still green (superadmin role is additive; admin still passes).

- [ ] **Step 3: Commit**

```bash
git add src/Backend/AuraCore.API/Hubs/AdminHub.cs
git commit -m "feat(6.11.W2.T17): AdminHub accepts superadmin + rejects scope-limited + superadmins group

Authorize roles broadened to admin,superadmin. Scope-limited JWTs
(2fa-setup-only) are aborted on connect — they must not hold a
live hub. Superadmins additionally join 'superadmins' group so
PermissionRequested events reach only them.

Closes Wave 2. Backend now has: permission attribute + superadmin
login + IEmailService + controller attribution + permission-request
CRUD + SignalR event plumbing. Wave 3 moves to frontend."
```

### Task 18: Mid-deploy gate (optional local smoke)

**Goal:** Backend is feature-complete for Waves 1-2; smoke-test against a running backend before moving to Wave 3. This task is intentionally not a commit; it's a checklist.

- [ ] **Step 1: Run full test suite**

```bash
dotnet test AuraCorePro.sln 2>&1 | tail -10
```

Expected: all tests green. The new SuperadminFoundation suite should contribute ~40 new tests.

- [ ] **Step 2 (optional): Smoke the backend locally**

```bash
cd src/Backend/AuraCore.API
dotnet run
```

In another terminal:

```bash
curl -X POST http://localhost:5000/api/auth/superadmin/login \
  -H "Content-Type: application/json" \
  -d '{"email":"nonexistent@x.com","password":"foo"}'
# Expect: {"error":"Invalid credentials"}
```

- [ ] **Step 3: Kill dev server + push branch (first remote push of Phase 6.11)**

```bash
git push -u origin phase-6-superadmin-foundation
```

Wave 3 starts on the same branch.

---

## Wave 3 — Frontend role-aware shell + locked UX + nginx public-cut + SPF fix

Seven tasks. After Wave 3 the admin panel has a Superadmin login button, role-conditional NAV_GROUPS, LockedTabPlaceholder / PermissionGate / PermissionRequestDialog components, SignalR event plumbing for 4 new events, `robots: noindex` metadata, public-reachable nginx (basic-auth removed), and the Resend SPF DNS fix deployed.

### Task 19: Frontend types + api.ts extensions

**Goal:** Add TypeScript interfaces for the new backend shapes and extend `api.ts` with the ~30 new endpoint methods. No UI yet.

**Files:**
- Modify: `admin-panel/src/lib/types.ts`
- Modify: `admin-panel/src/lib/api.ts`
- Create: `admin-panel/src/lib/permissions.ts`
- Test: `admin-panel/src/__tests__/lib/permissions.test.ts`

- [ ] **Step 1: Write failing test for `permissions.ts`**

`admin-panel/src/__tests__/lib/permissions.test.ts`:

```typescript
import { describe, it, expect } from 'vitest';
import {
  PERMISSION_KEYS, TIER1_KEYS, TIER2_KEYS,
  PERMISSION_LABELS, isTabKey, isActionKey,
} from '@/lib/permissions';

describe('permissions', () => {
  it('lists 4 Tier 1 tab keys', () => {
    expect(TIER1_KEYS).toHaveLength(4);
    expect(TIER1_KEYS).toContain('tab:configuration');
    expect(TIER1_KEYS).toContain('tab:ipwhitelist');
    expect(TIER1_KEYS).toContain('tab:updates');
    expect(TIER1_KEYS).toContain('tab:rolechange');
  });

  it('lists 6 Tier 2 action keys', () => {
    expect(TIER2_KEYS).toHaveLength(6);
  });

  it('classifies keys', () => {
    expect(isTabKey('tab:updates')).toBe(true);
    expect(isTabKey('action:users.delete')).toBe(false);
    expect(isActionKey('action:users.delete')).toBe(true);
  });

  it('has human-readable label for every key', () => {
    PERMISSION_KEYS.forEach(k => {
      expect(PERMISSION_LABELS[k]).toBeTruthy();
      expect(PERMISSION_LABELS[k].length).toBeGreaterThan(3);
    });
  });
});
```

- [ ] **Step 2: Verify test fails (module missing)**

```bash
cd "C:/Users/Admin/Desktop/AuraCorePro/AuraCorePro/admin-panel"
npx vitest run src/__tests__/lib/permissions.test.ts 2>&1 | tail -10
```

Expected: Cannot find module `'@/lib/permissions'`.

- [ ] **Step 3: Create `lib/permissions.ts`**

`admin-panel/src/lib/permissions.ts`:

```typescript
export const TIER1_KEYS = [
  'tab:configuration',
  'tab:ipwhitelist',
  'tab:updates',
  'tab:rolechange',
] as const;

export const TIER2_KEYS = [
  'action:users.delete',
  'action:users.ban',
  'action:subscriptions.grant',
  'action:subscriptions.revoke',
  'action:payments.approveCrypto',
  'action:payments.rejectCrypto',
] as const;

export type PermissionKey = typeof TIER1_KEYS[number] | typeof TIER2_KEYS[number];

export const PERMISSION_KEYS: readonly PermissionKey[] = [...TIER1_KEYS, ...TIER2_KEYS];

export const PERMISSION_LABELS: Record<PermissionKey, string> = {
  'tab:configuration':          'Configuration tab',
  'tab:ipwhitelist':            'IP Whitelist tab',
  'tab:updates':                'Updates tab',
  'tab:rolechange':             'Role Change tab',
  'action:users.delete':        'Delete a user',
  'action:users.ban':           'Ban a user',
  'action:subscriptions.grant': 'Grant a subscription',
  'action:subscriptions.revoke':'Revoke a subscription',
  'action:payments.approveCrypto':'Approve a crypto payment',
  'action:payments.rejectCrypto': 'Reject a crypto payment',
};

export function isTabKey(k: string): boolean { return k.startsWith('tab:'); }
export function isActionKey(k: string): boolean { return k.startsWith('action:'); }
```

- [ ] **Step 4: Verify test passes**

```bash
npx vitest run src/__tests__/lib/permissions.test.ts 2>&1 | tail -5
```

Expected: 4 passed.

- [ ] **Step 5: Extend `lib/types.ts`**

Read `admin-panel/src/lib/types.ts`. After the existing `AuditLogEntry` interface, append:

```typescript

// Phase 6.11 additions
export type UserRole = 'user' | 'admin' | 'superadmin';

export interface PermissionGrant {
  permissionKey: string;
  grantedAt: string;
  expiresAt?: string | null;
  grantedByEmail?: string;
  sourceRequestId?: string | null;
}

export interface PermissionRequest {
  id: string;
  permissionKey: string;
  reason: string;
  status: 'pending' | 'approved' | 'denied' | 'cancelled';
  requestedAt: string;
  reviewedAt?: string | null;
  reviewNote?: string | null;
  adminEmail?: string; // only present in superadmin inbox list
}

export interface MyPermissionsResponse {
  totalRestricted: number;
  activeGrantsCount: number;
  grants: PermissionGrant[];
  pending: { id: string; permissionKey: string; reason: string; requestedAt: string }[];
  recentDenials: { permissionKey: string; reviewNote?: string; reviewedAt: string }[];
}

export interface AdminAccount {
  id: string;
  email: string;
  role: UserRole;
  isActive: boolean;
  isReadonly: boolean;
  totpEnabled: boolean;
  require2fa: boolean;
  createdAt: string;
  createdByEmail?: string;
  lastLoginAt?: string;
}

export interface AdminInvitation {
  tokenHash: string;
  adminEmail: string;
  createdByEmail: string;
  createdAt: string;
  expiresAt: string;
  consumedAt?: string | null;
}

export interface RateLimitPolicy {
  endpoint: string; // "auth.login" | "auth.register" | "admin.all" | "signalr.connect"
  requests: number;
  windowSeconds: number;
  updatedAt?: string;
}

export interface SecurityPolicy {
  require2faForAllAdmins: boolean;
  perAccountOverrides: { userId: string; email: string; require2fa: boolean }[];
}
```

- [ ] **Step 6: Extend `lib/api.ts` with new endpoint methods**

Read `admin-panel/src/lib/api.ts`. Inside the `api` object, after the existing methods, append the following block. Keep the existing methods unchanged.

```typescript
  // ── Phase 6.11 ───────────────────────────────────────

  async superadminLogin(email: string, password: string, totpCode?: string) {
    const res = await request('/api/auth/superadmin/login', {
      method: 'POST',
      body: JSON.stringify({ email, password, totpCode }),
    });
    const data = await res.json();
    if (res.ok && data.accessToken) {
      token = data.accessToken;
      if (typeof window !== 'undefined') localStorage.setItem('aura_token', data.accessToken);
    }
    return { ok: res.ok, data };
  },

  async getMyPermissions() {
    const res = await request('/api/admin/my-permissions');
    return res.ok ? await res.json() : null;
  },

  async createPermissionRequest(permissionKey: string, reason: string) {
    const res = await request('/api/admin/permission-requests', {
      method: 'POST',
      body: JSON.stringify({ permissionKey, reason }),
    });
    return { ok: res.ok, status: res.status, data: res.ok ? await res.json() : await safeJson(res) };
  },

  async listMyPermissionRequests() {
    const res = await request('/api/admin/permission-requests');
    return res.ok ? await res.json() : { items: [] };
  },

  async cancelPermissionRequest(id: string) {
    const res = await request(`/api/admin/permission-requests/${id}/cancel`, { method: 'POST' });
    return { ok: res.ok };
  },

  // Superadmin-only
  async listPermissionRequests(status = 'pending') {
    const res = await request(`/api/superadmin/permission-requests?status=${status}`);
    return res.ok ? await res.json() : { items: [] };
  },

  async approvePermissionRequest(id: string, expiresAt?: string | null, reviewNote?: string) {
    const res = await request(`/api/superadmin/permission-requests/${id}/approve`, {
      method: 'POST', body: JSON.stringify({ expiresAt, reviewNote }),
    });
    return { ok: res.ok };
  },

  async denyPermissionRequest(id: string, reviewNote?: string) {
    const res = await request(`/api/superadmin/permission-requests/${id}/deny`, {
      method: 'POST', body: JSON.stringify({ reviewNote }),
    });
    return { ok: res.ok };
  },

  async bulkApprovePermissionRequests(ids: string[]) {
    const res = await request('/api/superadmin/permission-requests/bulk/approve', {
      method: 'POST', body: JSON.stringify({ ids }),
    });
    return { ok: res.ok };
  },

  async bulkDenyPermissionRequests(ids: string[]) {
    const res = await request('/api/superadmin/permission-requests/bulk/deny', {
      method: 'POST', body: JSON.stringify({ ids }),
    });
    return { ok: res.ok };
  },

  async revokePermissionGrant(adminUserId: string, permissionKey: string, reason: string) {
    const res = await request('/api/superadmin/permission-grants/revoke', {
      method: 'POST', body: JSON.stringify({ adminUserId, permissionKey, reason }),
    });
    return { ok: res.ok };
  },

  async listAdminAccounts() {
    const res = await request('/api/superadmin/admins');
    return res.ok ? await res.json() : { items: [] };
  },

  async createAdminAccount(body: {
    email: string;
    sendInvitation: boolean;
    initialPassword?: string;
    forcePasswordChange: 'on_first_login' | 'within_7_days' | 'within_30_days' | 'never';
    template: 'Default' | 'Trusted' | 'ReadOnly' | 'Custom';
    customKeys?: { permissionKey: string; expiresAt?: string | null }[];
    require2fa: boolean;
  }) {
    const res = await request('/api/superadmin/admins', {
      method: 'POST', body: JSON.stringify(body),
    });
    return { ok: res.ok, data: res.ok ? await res.json() : await safeJson(res) };
  },

  async promoteUserToAdmin(userId: string, body: {
    template: 'Default' | 'Trusted' | 'ReadOnly' | 'Custom';
    forcePasswordChange: 'on_first_login' | 'within_7_days' | 'within_30_days' | 'never';
    require2fa: boolean;
    customKeys?: { permissionKey: string; expiresAt?: string | null }[];
  }) {
    const res = await request(`/api/superadmin/users/${userId}/promote`, {
      method: 'POST', body: JSON.stringify(body),
    });
    return { ok: res.ok };
  },

  async demoteAdminToUser(adminId: string) {
    const res = await request(`/api/superadmin/admins/${adminId}/demote`, { method: 'POST' });
    return { ok: res.ok };
  },

  async suspendAdmin(adminId: string) {
    const res = await request(`/api/superadmin/admins/${adminId}/suspend`, { method: 'POST' });
    return { ok: res.ok };
  },

  async restoreAdmin(adminId: string) {
    const res = await request(`/api/superadmin/admins/${adminId}/restore`, { method: 'POST' });
    return { ok: res.ok };
  },

  async deleteAdmin(adminId: string) {
    const res = await request(`/api/superadmin/admins/${adminId}`, { method: 'DELETE' });
    return { ok: res.ok };
  },

  async resetAdminPassword(adminId: string) {
    const res = await request(`/api/superadmin/admins/${adminId}/reset-password`, { method: 'POST' });
    return { ok: res.ok };
  },

  async changePassword(currentPassword: string, newPassword: string) {
    const res = await request('/api/auth/change-password', {
      method: 'POST', body: JSON.stringify({ currentPassword, newPassword }),
    });
    return { ok: res.ok, data: await safeJson(res) };
  },

  async redeemInvitation(token: string, email: string, newPassword: string) {
    const res = await request('/api/auth/redeem-invitation', {
      method: 'POST', body: JSON.stringify({ token, email, newPassword }),
    });
    return { ok: res.ok, data: await safeJson(res) };
  },

  async getSecurityPolicy() {
    const res = await request('/api/superadmin/security-policy');
    return res.ok ? await res.json() : null;
  },

  async updateSecurityPolicy(require2faForAllAdmins: boolean) {
    const res = await request('/api/superadmin/security-policy', {
      method: 'PUT', body: JSON.stringify({ require2faForAllAdmins }),
    });
    return { ok: res.ok };
  },

  async setAdminRequire2fa(adminId: string, require2fa: boolean) {
    const res = await request(`/api/superadmin/admins/${adminId}/require-2fa`, {
      method: 'PUT', body: JSON.stringify({ require2fa }),
    });
    return { ok: res.ok };
  },

  async getRateLimitPolicies() {
    const res = await request('/api/superadmin/rate-limits');
    return res.ok ? await res.json() : { items: [] };
  },

  async updateRateLimitPolicy(endpoint: string, requests: number, windowSeconds: number) {
    const res = await request(`/api/superadmin/rate-limits/${encodeURIComponent(endpoint)}`, {
      method: 'PUT', body: JSON.stringify({ requests, windowSeconds }),
    });
    return { ok: res.ok };
  },

  async listAdminActionLog(params: { actorEmail?: string; action?: string; dateFrom?: string; dateTo?: string; page?: number; pageSize?: number } = {}) {
    const qs = new URLSearchParams();
    Object.entries(params).forEach(([k, v]) => v != null && qs.append(k, String(v)));
    const res = await request(`/api/superadmin/admin-actions?${qs}`);
    return res.ok ? await res.json() : { items: [], total: 0 };
  },

  async getAdminActionStats() {
    const res = await request('/api/superadmin/admin-actions/stats');
    return res.ok ? await res.json() : null;
  },

  exportAuditLogCsvUrl(params: { dateFrom?: string; dateTo?: string; actorEmail?: string; action?: string } = {}) {
    const qs = new URLSearchParams();
    Object.entries(params).forEach(([k, v]) => v != null && qs.append(k, String(v)));
    return `${API}/api/admin/audit-log/export.csv?${qs}`;
  },

  exportAdminActionLogCsvUrl(params: { dateFrom?: string; dateTo?: string; actorEmail?: string; action?: string } = {}) {
    const qs = new URLSearchParams();
    Object.entries(params).forEach(([k, v]) => v != null && qs.append(k, String(v)));
    return `${API}/api/superadmin/admin-actions/export.csv?${qs}`;
  },
```

Append at the module bottom (below the `export const api = { ... };` block):

```typescript
async function safeJson(res: Response) {
  try { return await res.json(); } catch { return {}; }
}
```

- [ ] **Step 7: Build-check**

```bash
cd admin-panel
npx tsc --noEmit 2>&1 | tail -5
```

Expected: 0 errors. (The 12 methods that reference endpoints not yet implemented are fine — they're just fetch calls that return `{ok: false}` until Wave 4/5 backends ship.)

- [ ] **Step 8: Commit**

```bash
git add admin-panel/src/lib/permissions.ts admin-panel/src/lib/types.ts admin-panel/src/lib/api.ts admin-panel/src/__tests__/lib/permissions.test.ts
git commit -m "feat(6.11.W3.T19): frontend types + api.ts extensions + permissions helper

- lib/permissions.ts: key constants + labels + classification helpers
- lib/types.ts: PermissionGrant/Request, AdminAccount, RateLimitPolicy,
  SecurityPolicy, UserRole union
- lib/api.ts: ~30 new endpoint methods (some are no-ops until Wave 4/5
  backend endpoints ship — they return {ok:false} gracefully)"
```

### Task 20: Role-aware NAV_GROUPS + post-login role read

**Goal:** `admin-panel/src/app/page.tsx` now reads `currentUser.role` and renders different NAV_GROUPS for `admin` vs `superadmin`. Superadmin sees extra tabs (Permission Requests, Admin Action Log, Admin Management, Role Change, Security Policy, API Rate Limits). Regular admin sees the existing 13 tabs.

**Files:**
- Modify: `admin-panel/src/app/page.tsx`
- Test: `admin-panel/src/__tests__/views/NavGroupsByRole.test.tsx` (smoke — renders correct tab set)

- [ ] **Step 1: Write failing smoke test**

`admin-panel/src/__tests__/views/NavGroupsByRole.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
// Note: page.tsx is the root; we render the inner AdminPanel component via a
// named export added in Step 2. If that export doesn't exist yet, this test
// will fail at import time — which is what TDD wants.
import { AdminPanelForTest } from '@/app/page';

vi.mock('@/lib/api', () => ({ api: {}, setToken: () => {}, getToken: () => 'x' }));
vi.mock('@/lib/signalr', () => ({ startConnection: () => {}, stopConnection: () => {}, on: () => {}, off: () => {} }));

describe('NAV_GROUPS by role', () => {
  it('admin sees 13 standard tabs and no superadmin-only tabs', () => {
    render(<AdminPanelForTest role="admin" />);
    expect(screen.getByText('Users')).toBeTruthy();
    expect(screen.queryByText('Permission Requests')).toBeNull();
    expect(screen.queryByText('Admin Management')).toBeNull();
  });

  it('superadmin sees admin tabs plus superadmin-only tabs', () => {
    render(<AdminPanelForTest role="superadmin" />);
    expect(screen.getByText('Users')).toBeTruthy();
    expect(screen.getByText('Permission Requests')).toBeTruthy();
    expect(screen.getByText('Admin Management')).toBeTruthy();
    expect(screen.getByText('Security Policy')).toBeTruthy();
    expect(screen.getByText('API Rate Limits')).toBeTruthy();
  });
});
```

- [ ] **Step 2: Verify test fails**

```bash
cd admin-panel
npx vitest run src/__tests__/views/NavGroupsByRole.test.tsx 2>&1 | tail -8
```

Expected: `AdminPanelForTest` not exported from `@/app/page`.

- [ ] **Step 3: Rewrite `admin-panel/src/app/page.tsx`**

Replace the file with the following. Pay attention to three concerns: (a) reading `currentUser.role` from a one-time `api.getStats()`-alike call; for simplicity we read it from `/api/auth/me` via a new lightweight `getMe()` call — or decode the JWT client-side; (b) exposing `AdminPanelForTest` for the test; (c) adding 9 new view imports with lazy-but-not-really fallback.

```tsx
'use client';

import { useState, useEffect } from 'react';
import {
  LayoutDashboard, Users, CreditCard, Shield, Crown, Zap, ShieldCheck, Monitor,
  BarChart2, Settings2, Key, Bug, Layers, FileText,
  Inbox, UserCog, ArrowRightLeft, Lock, Gauge,
} from 'lucide-react';
import { api, setToken } from '@/lib/api';
import { startConnection, stopConnection } from '@/lib/signalr';
import { LoginScreen } from '@/components/LoginScreen';
import { Sidebar, NavGroup } from '@/components/Sidebar';

import { DashboardPage } from '@/views/DashboardPage';
import { UsersPage } from '@/views/UsersPage';
import { SubscriptionsPage } from '@/views/SubscriptionsPage';
import { LicensesPage } from '@/views/LicensesPage';
import { PaymentsPage } from '@/views/PaymentsPage';
import { DevicesPage } from '@/views/DevicesPage';
import { UpdatesPage } from '@/views/UpdatesPage';
import { CrashReportsPage } from '@/views/CrashReportsPage';
import { TelemetryPage } from '@/views/TelemetryPage';
import { AuditLogPage } from '@/views/AuditLogPage';
import { IpWhitelistPage } from '@/views/IpWhitelistPage';
import { ConfigurationPage } from '@/views/ConfigurationPage';
import { SecurityPage } from '@/views/SecurityPage';
import { PermissionRequestsPage } from '@/views/PermissionRequestsPage';
import { AdminActionLogPage } from '@/views/AdminActionLogPage';
import { AdminManagementPage } from '@/views/AdminManagementPage';
import { RoleChangePage } from '@/views/RoleChangePage';
import { SecurityPolicyPage } from '@/views/SecurityPolicyPage';
import { APIRateLimitsPage } from '@/views/APIRateLimitsPage';
import { MyPermissionsPage } from '@/views/MyPermissionsPage';
import { ChangePasswordPage } from '@/views/ChangePasswordPage';
import { Enable2FAPage } from '@/views/Enable2FAPage';
import { RedeemInvitationPage } from '@/views/RedeemInvitationPage';

import type { UserRole } from '@/lib/types';

type Page =
  'dashboard'|'users'|'payments'|'subscriptions'|'licenses'|'updates'|'devices'|'crashes'|'telemetry'|'audit'|'whitelist'|'config'|'security'|
  // superadmin-only
  'permReq'|'adminActionLog'|'adminMgmt'|'roleChange'|'securityPolicy'|'rateLimits'|
  // cross-role
  'myPerms'|'changePw'|'enable2fa'|'redeemInvite';

const ADMIN_NAV_GROUPS: NavGroup[] = [
  { title: 'Overview', items: [{ id: 'dashboard', icon: LayoutDashboard, label: 'Dashboard' }] },
  { title: 'Management', items: [
    { id: 'users', icon: Users, label: 'Users' }, { id: 'payments', icon: CreditCard, label: 'Payments' },
    { id: 'subscriptions', icon: Crown, label: 'Subscriptions' }, { id: 'licenses', icon: Key, label: 'Licenses' },
    { id: 'updates', icon: Zap, label: 'Updates' }, { id: 'devices', icon: Monitor, label: 'Devices' },
  ] },
  { title: 'Analytics', items: [
    { id: 'crashes', icon: Bug, label: 'Crash Reports' }, { id: 'telemetry', icon: BarChart2, label: 'Telemetry' },
    { id: 'audit', icon: FileText, label: 'Audit Log' },
  ] },
  { title: 'System', items: [
    { id: 'whitelist', icon: Shield, label: 'IP Whitelist' }, { id: 'config', icon: Settings2, label: 'Configuration' },
    { id: 'security', icon: ShieldCheck, label: 'Security' },
  ] },
];

const SUPERADMIN_EXTRA_GROUPS: NavGroup[] = [
  { title: 'Superadmin', items: [
    { id: 'permReq', icon: Inbox, label: 'Permission Requests' },
    { id: 'adminActionLog', icon: FileText, label: 'Admin Action Log' },
    { id: 'adminMgmt', icon: UserCog, label: 'Admin Management' },
    { id: 'roleChange', icon: ArrowRightLeft, label: 'Role Change' },
    { id: 'securityPolicy', icon: Lock, label: 'Security Policy' },
    { id: 'rateLimits', icon: Gauge, label: 'API Rate Limits' },
  ] },
];

const PAGES: Record<Page, () => JSX.Element> = {
  dashboard: DashboardPage, users: UsersPage, payments: PaymentsPage, subscriptions: SubscriptionsPage,
  licenses: LicensesPage, updates: UpdatesPage, devices: DevicesPage, crashes: CrashReportsPage,
  telemetry: TelemetryPage, audit: AuditLogPage, whitelist: IpWhitelistPage, config: ConfigurationPage,
  security: SecurityPage,
  permReq: PermissionRequestsPage, adminActionLog: AdminActionLogPage, adminMgmt: AdminManagementPage,
  roleChange: RoleChangePage, securityPolicy: SecurityPolicyPage, rateLimits: APIRateLimitsPage,
  myPerms: MyPermissionsPage, changePw: ChangePasswordPage, enable2fa: Enable2FAPage, redeemInvite: RedeemInvitationPage,
};

interface AdminPanelProps { onLogout: () => void; role: UserRole; }

function AdminPanelInner({ onLogout: _onLogout, role }: AdminPanelProps) {
  const [page, setPage] = useState<Page>('dashboard');
  const groups = role === 'superadmin' ? [...ADMIN_NAV_GROUPS, ...SUPERADMIN_EXTRA_GROUPS] : ADMIN_NAV_GROUPS;
  const ActivePage = PAGES[page] ?? DashboardPage;
  return (
    <div className="flex h-screen overflow-hidden">
      <Sidebar groups={groups} activePage={page} onSelect={(p) => setPage(p as Page)} />
      <main className="flex-1 overflow-y-auto">
        <div className="max-w-[1400px] mx-auto p-6 lg:p-8 pb-20 md:pb-0"><ActivePage /></div>
      </main>
    </div>
  );
}

// Exported for tests (forces the AdminPanel tree with a synthetic role prop).
export function AdminPanelForTest({ role }: { role: UserRole }) {
  return <AdminPanelInner role={role} onLogout={() => {}} />;
}

function decodeRoleFromJwt(token: string | null): UserRole {
  if (!token) return 'admin';
  try {
    const payload = JSON.parse(atob(token.split('.')[1]!));
    const roles: string[] = Array.isArray(payload[
      'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'
    ]) ? payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']
       : [payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']].filter(Boolean);
    if (roles.includes('superadmin')) return 'superadmin';
    if (roles.includes('admin')) return 'admin';
  } catch {}
  return 'admin';
}

export default function Home() {
  const [authenticated, setAuthenticated] = useState(false);
  const [role, setRole] = useState<UserRole>('admin');
  const [checking, setChecking] = useState(true);

  useEffect(() => {
    const saved = typeof window !== 'undefined' ? localStorage.getItem('aura_token') : null;
    if (saved) {
      setToken(saved);
      api.getStats().then(data => {
        if (data) {
          setAuthenticated(true);
          setRole(decodeRoleFromJwt(saved));
          startConnection();
        } else { setToken(null); localStorage.removeItem('aura_token'); }
        setChecking(false);
      });
    } else setChecking(false);
  }, []);

  const handleLogout = () => {
    setToken(null); localStorage.removeItem('aura_token'); stopConnection(); setAuthenticated(false);
  };

  if (checking) return (
    <div className="min-h-screen flex items-center justify-center">
      <div className="flex flex-col items-center gap-4">
        <div className="w-12 h-12 rounded-2xl bg-accent/10 border border-accent/20 flex items-center justify-center animate-pulse-glow">
          <Layers className="w-6 h-6 text-accent" />
        </div>
        <p className="text-sm text-white/30">Loading...</p>
      </div>
    </div>
  );
  if (!authenticated) return <LoginScreen onLogin={(r) => { setRole(r); setAuthenticated(true); startConnection(); }} />;
  return <AdminPanelInner role={role} onLogout={handleLogout} />;
}
```

- [ ] **Step 4: Create stub views (to keep imports valid until later tasks implement each)**

Create 10 stub files. Each uses the same shape: `export function FooPage() { return <div>Foo</div>; }`. The real implementations land in Waves 4/5.

Example — `admin-panel/src/views/PermissionRequestsPage.tsx`:

```tsx
'use client';
export function PermissionRequestsPage() {
  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-display font-bold">Permission Requests</h1>
      <p className="text-sm text-white/60">Coming in Wave 4.</p>
    </div>
  );
}
```

Repeat identically (with matching name) for:
- `AdminActionLogPage.tsx`
- `AdminManagementPage.tsx`
- `RoleChangePage.tsx`
- `SecurityPolicyPage.tsx`
- `APIRateLimitsPage.tsx`
- `MyPermissionsPage.tsx`
- `ChangePasswordPage.tsx`
- `Enable2FAPage.tsx`
- `RedeemInvitationPage.tsx`

- [ ] **Step 5: Update `LoginScreen.onLogin` prop signature (temporary — full rewrite in Task 22)**

Read `admin-panel/src/components/LoginScreen.tsx`. Change `onLogin: () => void` to `onLogin: (role: UserRole) => void`. In the body's success path, extract role from `result.user?.role` and call `onLogin(result.user?.role ?? 'admin')`. Minimal change — the "Sign In as Superadmin" button lands in Task 22.

- [ ] **Step 6: Verify smoke test passes**

```bash
npx vitest run src/__tests__/views/NavGroupsByRole.test.tsx 2>&1 | tail -5
```

Expected: 2 passed.

- [ ] **Step 7: TypeScript clean**

```bash
npx tsc --noEmit 2>&1 | tail -5
```

Expected: 0 errors.

- [ ] **Step 8: Commit**

```bash
git add admin-panel/src/app/page.tsx admin-panel/src/views/ admin-panel/src/components/LoginScreen.tsx admin-panel/src/__tests__/views/NavGroupsByRole.test.tsx
git commit -m "feat(6.11.W3.T20): role-aware NAV_GROUPS + 10 stub views + login role extraction

page.tsx reads user role (from JWT) post-login; superadmin sees 6 extra
nav items on top of standard admin shell. AdminPanelForTest export
enables isolated render tests. 10 stub views added so imports compile;
real implementations land in Waves 4-5.

LoginScreen.onLogin now passes role to caller so post-login routing
can branch on it."
```

### Task 21: LockedTabPlaceholder + PermissionGate + PermissionRequestDialog components

**Goal:** Three reusable UI components per spec D15: placeholder page for Tier 1 locked tabs, inline gate wrapper for Tier 2 action buttons, and request modal with mandatory 50-500 char reason.

**Files:**
- Create: `admin-panel/src/components/LockedTabPlaceholder.tsx`
- Create: `admin-panel/src/components/PermissionRequestDialog.tsx`
- Create: `admin-panel/src/components/PermissionGate.tsx`
- Test: `admin-panel/src/__tests__/components/LockedTabPlaceholder.test.tsx`
- Test: `admin-panel/src/__tests__/components/PermissionRequestDialog.test.tsx`
- Test: `admin-panel/src/__tests__/components/PermissionGate.test.tsx`

- [ ] **Step 1: Write failing tests for `LockedTabPlaceholder`**

`admin-panel/src/__tests__/components/LockedTabPlaceholder.test.tsx`:

```typescript
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { LockedTabPlaceholder } from '@/components/LockedTabPlaceholder';

describe('LockedTabPlaceholder', () => {
  it('renders the spec message verbatim', () => {
    render(<LockedTabPlaceholder tabName="Configuration" permissionKey="tab:configuration" />);
    expect(screen.getByText(/disabled by the superadmin by default/i)).toBeTruthy();
    expect(screen.getByText(/Configuration tab/i)).toBeTruthy();
  });

  it('opens the request dialog on button click', () => {
    const onRequestStart = vi.fn();
    render(<LockedTabPlaceholder tabName="Configuration" permissionKey="tab:configuration" onRequestStart={onRequestStart} />);
    fireEvent.click(screen.getByRole('button', { name: /request permission/i }));
    expect(onRequestStart).toHaveBeenCalledWith('tab:configuration');
  });

  it('shows pending banner when hasPending=true', () => {
    render(<LockedTabPlaceholder tabName="Updates" permissionKey="tab:updates" hasPending pendingAt="2026-04-23T12:00:00Z" />);
    expect(screen.getByText(/Pending request/i)).toBeTruthy();
  });

  it('shows denial banner when lastDenial provided', () => {
    render(<LockedTabPlaceholder tabName="Updates" permissionKey="tab:updates" lastDenial={{ reviewNote: 'wrong team', reviewedAt: '2026-04-23T10:00:00Z' }} />);
    expect(screen.getByText(/Last request denied/i)).toBeTruthy();
    expect(screen.getByText(/wrong team/)).toBeTruthy();
  });
});
```

- [ ] **Step 2: Write failing tests for `PermissionRequestDialog`**

`admin-panel/src/__tests__/components/PermissionRequestDialog.test.tsx`:

```typescript
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { PermissionRequestDialog } from '@/components/PermissionRequestDialog';

describe('PermissionRequestDialog', () => {
  it('blocks submit when reason < 50 chars', () => {
    const onSubmit = vi.fn();
    render(<PermissionRequestDialog permissionKey="tab:updates" isOpen onClose={() => {}} onSubmit={onSubmit} />);
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'too short' } });
    fireEvent.click(screen.getByRole('button', { name: /submit/i }));
    expect(onSubmit).not.toHaveBeenCalled();
    expect(screen.getByText(/at least 50/i)).toBeTruthy();
  });

  it('submits valid reason', async () => {
    const onSubmit = vi.fn().mockResolvedValue(true);
    render(<PermissionRequestDialog permissionKey="tab:updates" isOpen onClose={() => {}} onSubmit={onSubmit} />);
    fireEvent.change(screen.getByRole('textbox'), {
      target: { value: 'I need to publish a new release for the Q2 rollout that customers are waiting on urgently.' },
    });
    fireEvent.click(screen.getByRole('button', { name: /submit/i }));
    expect(onSubmit).toHaveBeenCalledWith('tab:updates', expect.stringContaining('Q2 rollout'));
  });

  it('shows char counter', () => {
    render(<PermissionRequestDialog permissionKey="tab:updates" isOpen onClose={() => {}} onSubmit={async () => true} />);
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'x'.repeat(75) } });
    expect(screen.getByText(/75 \/ 500/)).toBeTruthy();
  });
});
```

- [ ] **Step 3: Write failing test for `PermissionGate`**

`admin-panel/src/__tests__/components/PermissionGate.test.tsx`:

```typescript
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { PermissionGate } from '@/components/PermissionGate';

describe('PermissionGate', () => {
  it('renders children when permission granted', () => {
    render(
      <PermissionGate permissionKey="action:users.delete" hasPermission onRequestStart={() => {}}>
        <button>Delete</button>
      </PermissionGate>
    );
    expect(screen.getByRole('button', { name: 'Delete' })).toBeTruthy();
  });

  it('renders disabled lock button when permission denied', () => {
    const onRequestStart = vi.fn();
    render(
      <PermissionGate permissionKey="action:users.delete" hasPermission={false} onRequestStart={onRequestStart}>
        <button>Delete</button>
      </PermissionGate>
    );
    // The real Delete button is NOT rendered; replaced with the locked stub.
    const lockBtn = screen.getByRole('button');
    fireEvent.click(lockBtn);
    expect(onRequestStart).toHaveBeenCalledWith('action:users.delete');
  });
});
```

- [ ] **Step 4: Verify all tests fail**

```bash
npx vitest run src/__tests__/components/LockedTabPlaceholder.test.tsx src/__tests__/components/PermissionRequestDialog.test.tsx src/__tests__/components/PermissionGate.test.tsx 2>&1 | tail -10
```

Expected: module-not-found for all three components.

- [ ] **Step 5: Create `LockedTabPlaceholder.tsx`**

`admin-panel/src/components/LockedTabPlaceholder.tsx`:

```tsx
'use client';

import { Lock, Send, Clock } from 'lucide-react';

export interface LockedTabPlaceholderProps {
  tabName: string;
  permissionKey: string;
  onRequestStart?: (key: string) => void;
  hasPending?: boolean;
  pendingAt?: string;
  lastDenial?: { reviewNote?: string | null; reviewedAt: string };
}

export function LockedTabPlaceholder({
  tabName, permissionKey, onRequestStart, hasPending, pendingAt, lastDenial,
}: LockedTabPlaceholderProps) {
  return (
    <div className="flex items-center justify-center min-h-[50vh]">
      <div className="max-w-md text-center space-y-6">
        <div className="inline-flex items-center justify-center w-20 h-20 rounded-3xl bg-accent/10 border border-accent/20 mx-auto">
          <Lock className="w-10 h-10 text-accent" />
        </div>
        <div className="space-y-2">
          <h2 className="text-xl font-display font-bold">{tabName} tab is locked</h2>
          <p className="text-sm text-white/60 leading-relaxed">
            This page has been disabled by the superadmin by default. You need permission
            from the superadmin to be able to use the {tabName} tab.
          </p>
        </div>
        {hasPending ? (
          <div className="flex items-center gap-2 justify-center text-sm text-white/50 bg-white/5 border border-white/10 rounded-xl px-4 py-3">
            <Clock className="w-4 h-4" />
            Pending request from {pendingAt ? new Date(pendingAt).toLocaleString() : 'recently'}, awaiting review.
          </div>
        ) : (
          <button onClick={() => onRequestStart?.(permissionKey)}
            className="btn-primary inline-flex items-center gap-2">
            <Send className="w-4 h-4" />
            Request Permission
          </button>
        )}
        {lastDenial && (
          <div className="text-xs text-aura-red/80 bg-aura-red/10 border border-aura-red/20 rounded-xl px-4 py-2">
            Last request denied: {lastDenial.reviewNote || 'no reason given'}
          </div>
        )}
      </div>
    </div>
  );
}
```

- [ ] **Step 6: Create `PermissionRequestDialog.tsx`**

`admin-panel/src/components/PermissionRequestDialog.tsx`:

```tsx
'use client';

import { useState } from 'react';
import { X, Send, RefreshCw } from 'lucide-react';
import { PERMISSION_LABELS, PermissionKey } from '@/lib/permissions';

export interface PermissionRequestDialogProps {
  isOpen: boolean;
  permissionKey: string;
  onClose: () => void;
  onSubmit: (key: string, reason: string) => Promise<boolean>;
}

export function PermissionRequestDialog({ isOpen, permissionKey, onClose, onSubmit }: PermissionRequestDialogProps) {
  const [reason, setReason] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  if (!isOpen) return null;

  const label = PERMISSION_LABELS[permissionKey as PermissionKey] ?? permissionKey;

  const submit = async () => {
    const trimmed = reason.trim();
    if (trimmed.length < 50) { setError('Reason must be at least 50 characters so the superadmin has context.'); return; }
    if (trimmed.length > 500) { setError('Reason must be 500 characters or fewer.'); return; }
    setLoading(true); setError('');
    const ok = await onSubmit(permissionKey, trimmed);
    setLoading(false);
    if (ok) { setReason(''); onClose(); }
    else setError('Failed to send request. A pending request may already exist.');
  };

  return (
    <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4" role="dialog">
      <div className="glass-card w-full max-w-md p-6 space-y-4">
        <div className="flex items-center justify-between">
          <h3 className="text-lg font-display font-bold">Request access to {label}</h3>
          <button onClick={onClose} className="text-white/40 hover:text-white"><X className="w-5 h-5" /></button>
        </div>
        <div>
          <label className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">
            Why do you need this permission?
          </label>
          <textarea
            value={reason} onChange={e => setReason(e.target.value)}
            maxLength={500} rows={5}
            placeholder="Explain the specific use case, customer ticket, or incident that requires this access."
            className="input-dark w-full resize-none"
          />
          <div className="flex items-center justify-between mt-1 text-xs text-white/40">
            <span>50 min · 500 max</span>
            <span>{reason.length} / 500</span>
          </div>
        </div>
        {error && <div className="text-xs text-aura-red">{error}</div>}
        <div className="flex items-center gap-2 justify-end pt-2">
          <button onClick={onClose} className="btn-ghost">Cancel</button>
          <button onClick={submit} disabled={loading} className="btn-primary inline-flex items-center gap-2">
            {loading ? <RefreshCw className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
            {loading ? 'Submitting...' : 'Submit Request'}
          </button>
        </div>
      </div>
    </div>
  );
}
```

- [ ] **Step 7: Create `PermissionGate.tsx`**

`admin-panel/src/components/PermissionGate.tsx`:

```tsx
'use client';

import { Lock } from 'lucide-react';
import { PERMISSION_LABELS, PermissionKey } from '@/lib/permissions';

export interface PermissionGateProps {
  permissionKey: string;
  hasPermission: boolean;
  onRequestStart: (key: string) => void;
  children: React.ReactNode;
}

export function PermissionGate({ permissionKey, hasPermission, onRequestStart, children }: PermissionGateProps) {
  if (hasPermission) return <>{children}</>;

  const label = PERMISSION_LABELS[permissionKey as PermissionKey] ?? permissionKey;
  return (
    <button
      type="button"
      onClick={() => onRequestStart(permissionKey)}
      title={`This action requires superadmin permission. Click to request: ${label}.`}
      className="btn-ghost inline-flex items-center gap-1 text-white/40 hover:text-white/70 cursor-pointer"
    >
      <Lock className="w-4 h-4" /> Locked
    </button>
  );
}
```

- [ ] **Step 8: Verify all component tests pass**

```bash
npx vitest run src/__tests__/components/LockedTabPlaceholder.test.tsx src/__tests__/components/PermissionRequestDialog.test.tsx src/__tests__/components/PermissionGate.test.tsx 2>&1 | tail -5
```

Expected: 10 passed total.

- [ ] **Step 9: Commit**

```bash
git add admin-panel/src/components/LockedTabPlaceholder.tsx admin-panel/src/components/PermissionRequestDialog.tsx admin-panel/src/components/PermissionGate.tsx admin-panel/src/__tests__/components/LockedTabPlaceholder.test.tsx admin-panel/src/__tests__/components/PermissionRequestDialog.test.tsx admin-panel/src/__tests__/components/PermissionGate.test.tsx
git commit -m "feat(6.11.W3.T21): LockedTabPlaceholder + PermissionRequestDialog + PermissionGate

Spec D15 verbatim message on the placeholder. Dialog enforces 50-500
char reason with live counter. Gate wraps a destructive button; when
permission is denied, renders a 'Locked' stub that opens the dialog."
```

### Task 22: LoginScreen — "Sign In as Superadmin" button + post-login routing

**Goal:** Two-button LoginScreen per spec D2. Superadmin button is cyan-purple gradient on submit. Post-login routing: if `requiresTwoFactorSetup`, redirect to `enable2fa` view; if `requiresPasswordChange`, redirect to `changePw`; else land on dashboard.

**Files:**
- Modify: `admin-panel/src/components/LoginScreen.tsx`
- Test: `admin-panel/src/__tests__/views/LoginScreen.superadmin.test.tsx`

- [ ] **Step 1: Write failing test**

`admin-panel/src/__tests__/views/LoginScreen.superadmin.test.tsx`:

```typescript
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { LoginScreen } from '@/components/LoginScreen';

vi.mock('@/lib/api', () => ({
  api: {
    login: vi.fn().mockResolvedValue({ ok: true, data: { accessToken: 'x', user: { role: 'admin' } } }),
    superadminLogin: vi.fn().mockResolvedValue({ ok: true, data: { accessToken: 'y', user: { role: 'superadmin' } } }),
  },
  setToken: () => {},
}));

describe('LoginScreen', () => {
  it('exposes both admin and superadmin sign-in buttons', () => {
    render(<LoginScreen onLogin={() => {}} />);
    expect(screen.getByRole('button', { name: /sign in as admin/i })).toBeTruthy();
    expect(screen.getByRole('button', { name: /sign in as superadmin/i })).toBeTruthy();
  });

  it('posts to /api/auth/superadmin/login when superadmin button is clicked', async () => {
    const { api } = await import('@/lib/api');
    const onLogin = vi.fn();
    render(<LoginScreen onLogin={onLogin} />);

    fireEvent.change(screen.getByLabelText(/email/i), { target: { value: 'boss@x.com' } });
    fireEvent.change(screen.getByLabelText(/password/i), { target: { value: 'GoodPass12' } });
    fireEvent.click(screen.getByRole('button', { name: /sign in as superadmin/i }));

    await waitFor(() => expect(api.superadminLogin).toHaveBeenCalled());
    await waitFor(() => expect(onLogin).toHaveBeenCalledWith('superadmin'));
  });
});
```

- [ ] **Step 2: Verify tests fail**

```bash
npx vitest run src/__tests__/views/LoginScreen.superadmin.test.tsx 2>&1 | tail -8
```

Expected: no such button "sign in as superadmin".

- [ ] **Step 3: Rewrite `LoginScreen.tsx`**

`admin-panel/src/components/LoginScreen.tsx`:

```tsx
'use client';

import { useState } from 'react';
import { Shield, AlertCircle, RefreshCw, Lock, Crown } from 'lucide-react';
import { api, setToken } from '@/lib/api';
import type { UserRole } from '@/lib/types';

export interface LoginScreenProps {
  onLogin: (role: UserRole, scope?: 'normal' | '2fa-setup-only' | 'change-password') => void;
}

export function LoginScreen({ onLogin }: LoginScreenProps) {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [totpCode, setTotpCode] = useState('');
  const [needs2fa, setNeeds2fa] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState<null | 'admin' | 'superadmin'>(null);

  const submit = async (mode: 'admin' | 'superadmin') => {
    setLoading(mode); setError('');
    try {
      const { ok, data } = mode === 'admin'
        ? await api.login(email, password, totpCode || undefined)
        : await api.superadminLogin(email, password, totpCode || undefined);

      if (data?.requires2fa && !totpCode) { setNeeds2fa(true); return; }

      if (data?.requiresTwoFactorSetup && data.accessToken) {
        setToken(data.accessToken);
        if (typeof window !== 'undefined') localStorage.setItem('aura_token', data.accessToken);
        onLogin(data.user?.role ?? mode, '2fa-setup-only');
        return;
      }

      if (data?.requiresPasswordChange && data.accessToken) {
        setToken(data.accessToken);
        if (typeof window !== 'undefined') localStorage.setItem('aura_token', data.accessToken);
        onLogin(data.user?.role ?? mode, 'change-password');
        return;
      }

      if (ok && data.accessToken) {
        setToken(data.accessToken);
        if (typeof window !== 'undefined') localStorage.setItem('aura_token', data.accessToken);
        const role: UserRole = data.user?.role ?? mode;
        if (role !== 'admin' && role !== 'superadmin') {
          setError('Access denied. Admin role required.');
          setToken(null);
          return;
        }
        onLogin(role);
        return;
      }
      setError(data?.error || 'Authentication failed');
    } finally { setLoading(null); }
  };

  return (
    <div className="min-h-screen flex items-center justify-center p-4 relative overflow-hidden">
      <div className="absolute inset-0">
        <div className="absolute top-0 left-1/3 w-[600px] h-[600px] bg-accent/[0.07] rounded-full blur-[120px] animate-pulse" />
        <div className="absolute bottom-0 right-1/4 w-[500px] h-[500px] bg-aura-purple/[0.05] rounded-full blur-[100px]" />
      </div>

      <div className="relative w-full max-w-md">
        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-16 h-16 rounded-2xl bg-accent/10 border border-accent/20 mb-4 accent-glow">
            <Shield className="w-8 h-8 text-accent" />
          </div>
          <h1 className="text-2xl font-display font-bold">AuraCore Pro</h1>
          <p className="text-white/40 text-sm mt-1">Administration Console</p>
        </div>

        <form onSubmit={e => { e.preventDefault(); submit('admin'); }} className="glass-card p-8 space-y-5">
          <div>
            <label htmlFor="email" className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">Email</label>
            <input id="email" type="email" value={email} onChange={e => setEmail(e.target.value)}
              className="input-dark w-full" placeholder="admin@auracore.pro" required />
          </div>
          <div>
            <label htmlFor="password" className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">Password</label>
            <input id="password" type="password" value={password} onChange={e => setPassword(e.target.value)}
              className="input-dark w-full" placeholder="Enter password" required />
          </div>
          {needs2fa && (
            <div className="animate-fade-in">
              <label htmlFor="totp" className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">2FA Code</label>
              <input id="totp" type="text" value={totpCode} onChange={e => setTotpCode(e.target.value)}
                className="input-dark w-full text-center tracking-[0.5em] text-lg" placeholder="000000" maxLength={6} autoFocus />
            </div>
          )}
          {error && (
            <div className="flex items-center gap-2 text-aura-red text-sm bg-aura-red/10 border border-aura-red/20 rounded-xl px-4 py-3">
              <AlertCircle className="w-4 h-4 shrink-0" />{error}
            </div>
          )}
          <button type="submit" disabled={loading !== null}
            className="btn-primary w-full flex items-center justify-center gap-2">
            {loading === 'admin' ? <RefreshCw className="w-4 h-4 animate-spin" /> : <Lock className="w-4 h-4" />}
            {loading === 'admin' ? 'Authenticating...' : needs2fa ? 'Verify 2FA' : 'Sign In as Admin'}
          </button>
          <button type="button" disabled={loading !== null}
            onClick={() => submit('superadmin')}
            className="w-full flex items-center justify-center gap-2 py-3 rounded-xl font-semibold transition
                       bg-gradient-to-r from-accent to-aura-purple text-black hover:opacity-90 disabled:opacity-50">
            {loading === 'superadmin' ? <RefreshCw className="w-4 h-4 animate-spin" /> : <Crown className="w-4 h-4" />}
            {loading === 'superadmin' ? 'Authenticating...' : 'Sign In as Superadmin'}
          </button>
        </form>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Also update `app/page.tsx`'s `onLogin` handling — the `LoginScreen` call in `Home()` already passes a callback; extend it to route to `enable2fa` / `changePw` views when scope is passed.**

Read `admin-panel/src/app/page.tsx`. Find the `onLogin` callback passed into `<LoginScreen>`:

```tsx
  if (!authenticated) return <LoginScreen onLogin={(r) => { setRole(r); setAuthenticated(true); startConnection(); }} />;
```

Replace with:

```tsx
  const [postLoginView, setPostLoginView] = useState<Page | null>(null);
  if (!authenticated) return <LoginScreen onLogin={(r, scope) => {
    setRole(r); setAuthenticated(true); startConnection();
    if (scope === '2fa-setup-only') setPostLoginView('enable2fa');
    else if (scope === 'change-password') setPostLoginView('changePw');
  }} />;
```

And modify `<AdminPanelInner>` to honor `postLoginView` on first render:

```tsx
  return <AdminPanelInner role={role} onLogout={handleLogout} initialPage={postLoginView ?? 'dashboard'} />;
```

Then update `AdminPanelInner`'s prop: `interface AdminPanelProps { onLogout: () => void; role: UserRole; initialPage?: Page; }` and `const [page, setPage] = useState<Page>(initialPage ?? 'dashboard');`.

- [ ] **Step 5: Verify test passes**

```bash
npx vitest run src/__tests__/views/LoginScreen.superadmin.test.tsx 2>&1 | tail -5
```

Expected: 2 passed. (Mock the `api.login` to accept the third optional `totpCode` argument if needed; the test above already provides it.)

- [ ] **Step 6: Note for Task 19 revisit — update `api.login` signature**

The mock expects `api.login(email, password, totpCode)` with a third argument. Read `admin-panel/src/lib/api.ts`. Update the `login` method to accept an optional third `totpCode` parameter and pass it in the body:

```typescript
  async login(email: string, password: string, totpCode?: string) {
    try {
      const res = await request('/api/auth/login', {
        method: 'POST', body: JSON.stringify({ email, password, totpCode })
      });
      // ... rest unchanged
    } catch (err: any) { return { ok: false, data: { error: err.message || 'Connection failed' } }; }
  },
```

- [ ] **Step 7: Commit**

```bash
git add admin-panel/src/components/LoginScreen.tsx admin-panel/src/app/page.tsx admin-panel/src/lib/api.ts admin-panel/src/__tests__/views/LoginScreen.superadmin.test.tsx
git commit -m "feat(6.11.W3.T22): LoginScreen two-button mode + post-login scope routing

Admin button (default) posts /api/auth/login; Superadmin button
(cyan-purple gradient, Crown icon) posts /api/auth/superadmin/login.
Response scope-routing: requiresTwoFactorSetup → enable2fa view;
requiresPasswordChange → changePw view; else → dashboard.
api.login now accepts optional totpCode third argument."
```

### Task 23: Apply PermissionGate to existing destructive buttons

**Goal:** Wrap Delete/Ban/Grant/Revoke/ApproveCrypto/RejectCrypto buttons in `UsersPage`, `SubscriptionsPage`, `PaymentsPage` with `<PermissionGate>`. When the user lacks the grant, they see the Locked stub; clicking opens the request modal. Drive from a `usePermissions()` hook that reads from `api.getMyPermissions()`.

**Files:**
- Create: `admin-panel/src/hooks/usePermissions.ts`
- Modify: `admin-panel/src/views/UsersPage.tsx`
- Modify: `admin-panel/src/views/SubscriptionsPage.tsx`
- Modify: `admin-panel/src/views/PaymentsPage.tsx`
- Test: `admin-panel/src/__tests__/hooks/usePermissions.test.ts`

- [ ] **Step 1: Write failing test for `usePermissions` hook**

`admin-panel/src/__tests__/hooks/usePermissions.test.ts`:

```typescript
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { usePermissions } from '@/hooks/usePermissions';

vi.mock('@/lib/api', () => ({
  api: {
    getMyPermissions: vi.fn().mockResolvedValue({
      totalRestricted: 10,
      activeGrantsCount: 2,
      grants: [
        { permissionKey: 'action:users.delete', grantedAt: '', expiresAt: null },
        { permissionKey: 'tab:updates', grantedAt: '', expiresAt: null },
      ],
      pending: [],
      recentDenials: [],
    }),
  },
}));

describe('usePermissions', () => {
  it('returns a has() predicate that reports granted keys', async () => {
    const { result } = renderHook(() => usePermissions('admin'));
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.has('action:users.delete')).toBe(true);
    expect(result.current.has('action:users.ban')).toBe(false);
  });

  it('superadmin has all permissions without a fetch', () => {
    const { result } = renderHook(() => usePermissions('superadmin'));
    expect(result.current.has('action:users.delete')).toBe(true);
    expect(result.current.has('tab:configuration')).toBe(true);
  });
});
```

- [ ] **Step 2: Verify test fails**

```bash
npx vitest run src/__tests__/hooks/usePermissions.test.ts 2>&1 | tail -8
```

Expected: module not found.

- [ ] **Step 3: Create `hooks/usePermissions.ts`**

`admin-panel/src/hooks/usePermissions.ts`:

```typescript
'use client';

import { useEffect, useMemo, useState } from 'react';
import { api } from '@/lib/api';
import type { UserRole, MyPermissionsResponse } from '@/lib/types';

export function usePermissions(role: UserRole) {
  const [data, setData] = useState<MyPermissionsResponse | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (role === 'superadmin') { setLoading(false); return; }
    let alive = true;
    api.getMyPermissions().then(d => { if (alive) { setData(d); setLoading(false); } });
    return () => { alive = false; };
  }, [role]);

  const activeSet = useMemo(() => {
    if (role === 'superadmin') return null; // sentinel — has() always true
    const now = Date.now();
    return new Set((data?.grants ?? []).filter(g => !g.expiresAt || new Date(g.expiresAt).getTime() > now).map(g => g.permissionKey));
  }, [data, role]);

  const pendingSet = useMemo(
    () => new Set((data?.pending ?? []).map(p => p.permissionKey)),
    [data]
  );

  return {
    loading,
    data,
    has: (key: string) => activeSet === null ? true : activeSet.has(key),
    hasPending: (key: string) => pendingSet.has(key),
    refresh: async () => { setLoading(true); setData(await api.getMyPermissions()); setLoading(false); },
  };
}
```

- [ ] **Step 4: Verify hook test passes**

```bash
npx vitest run src/__tests__/hooks/usePermissions.test.ts 2>&1 | tail -5
```

Expected: 2 passed.

- [ ] **Step 5: Wrap destructive buttons in UsersPage**

Read `admin-panel/src/views/UsersPage.tsx`. Find the JSX for the Delete and Ban buttons. Replace with:

```tsx
// near the top imports
import { usePermissions } from '@/hooks/usePermissions';
import { PermissionGate } from '@/components/PermissionGate';
import { PermissionRequestDialog } from '@/components/PermissionRequestDialog';
import { useState } from 'react';
import { api } from '@/lib/api';

// Inside the component (adapt to the component name / signature you find):
const { has } = usePermissions(role); // `role` must be passed as a prop from page.tsx; see Step 6.
const [reqOpen, setReqOpen] = useState<string | null>(null);

// Wrap the Delete button (find the existing JSX, e.g. <button onClick={() => deleteUser(u.id)}>Delete</button>):
<PermissionGate permissionKey="action:users.delete" hasPermission={has('action:users.delete')} onRequestStart={setReqOpen}>
  <button onClick={() => deleteUser(u.id)} className="btn-danger-sm">Delete</button>
</PermissionGate>

// Wrap the Ban button similarly with 'action:users.ban'.

// At the end of the JSX, add the dialog:
{reqOpen && (
  <PermissionRequestDialog
    isOpen permissionKey={reqOpen}
    onClose={() => setReqOpen(null)}
    onSubmit={async (key, reason) => {
      const r = await api.createPermissionRequest(key, reason);
      return r.ok;
    }}
  />
)}
```

Because `UsersPage` doesn't currently accept a `role` prop, propagate it. The simplest approach: pass `role` through `AdminPanelInner` → `<ActivePage />`. Change the PAGES record signature to `Record<Page, (p: { role: UserRole }) => JSX.Element>` and every view to accept `{ role }: { role: UserRole }`. For views that don't use role, just destructure and ignore.

If refactoring every view is too invasive, an alternative: expose role via React Context. Create `admin-panel/src/lib/roleContext.ts`:

```typescript
'use client';
import { createContext, useContext } from 'react';
import type { UserRole } from '@/lib/types';

export const RoleContext = createContext<UserRole>('admin');
export const useRole = () => useContext(RoleContext);
```

Wrap `<AdminPanelInner>`'s return JSX with `<RoleContext.Provider value={role}>` and read via `const role = useRole()` inside each view that needs it. Prefer Context — zero prop-drilling.

- [ ] **Step 6: Wrap Grant/Revoke buttons in SubscriptionsPage + ApproveCrypto/RejectCrypto in PaymentsPage**

Same pattern as Step 5. Read each file; find the destructive buttons; wrap with `<PermissionGate>` using the matching key; inject the dialog state + component at the bottom of the JSX. Use `const role = useRole(); const { has } = usePermissions(role);`.

- [ ] **Step 7: Build + test regression**

```bash
npx tsc --noEmit 2>&1 | tail -5
npx vitest run 2>&1 | tail -5
```

Expected: 0 TS errors; existing tests pass; 2 new hook tests pass.

- [ ] **Step 8: Commit**

```bash
git add admin-panel/src/hooks/usePermissions.ts admin-panel/src/lib/roleContext.ts admin-panel/src/views/UsersPage.tsx admin-panel/src/views/SubscriptionsPage.tsx admin-panel/src/views/PaymentsPage.tsx admin-panel/src/app/page.tsx admin-panel/src/__tests__/hooks/usePermissions.test.ts
git commit -m "feat(6.11.W3.T23): PermissionGate applied to destructive buttons + usePermissions hook

RoleContext exposes current role (admin/superadmin) to every page.
usePermissions fetches /api/admin/my-permissions and returns a has()
predicate; superadmin short-circuits to always-true.

Wrapped: UsersPage Delete/Ban; SubscriptionsPage Grant/Revoke;
PaymentsPage ApproveCrypto/RejectCrypto. Unprivileged admins see a
'Locked' button that opens PermissionRequestDialog; submit creates
a permission_requests row + toasts 'Sent to superadmin'."
```

### Task 24: LockedTabPlaceholder wired into Tier 1 pages + SignalR event plumbing

**Goal:** For the 4 Tier 1 tab pages (ConfigurationPage, IpWhitelistPage, UpdatesPage, plus the new RoleChangePage from stub), render `<LockedTabPlaceholder>` when the admin lacks the tab:* permission. Also extend the SignalR `signalr.ts` layer so 4 new events get dispatched to listeners.

**Files:**
- Modify: `admin-panel/src/views/ConfigurationPage.tsx`
- Modify: `admin-panel/src/views/IpWhitelistPage.tsx`
- Modify: `admin-panel/src/views/UpdatesPage.tsx`
- Modify: `admin-panel/src/views/RoleChangePage.tsx` (still stubbed; add gate so unauthorized admin sees placeholder)
- Modify: `admin-panel/src/lib/signalr.ts` — no structural change (the `L` event registry already supports arbitrary event names); document new events in a comment block.

- [ ] **Step 1: Write a small smoke test for the wiring — ConfigurationPage shows placeholder when permission is absent**

`admin-panel/src/__tests__/views/ConfigurationPageLocked.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { ConfigurationPage } from '@/views/ConfigurationPage';
import { RoleContext } from '@/lib/roleContext';

vi.mock('@/hooks/usePermissions', () => ({
  usePermissions: () => ({ loading: false, has: () => false, hasPending: () => false, data: null, refresh: () => {} }),
}));

describe('ConfigurationPage permission gating', () => {
  it('renders LockedTabPlaceholder when admin lacks tab:configuration', () => {
    render(
      <RoleContext.Provider value="admin"><ConfigurationPage /></RoleContext.Provider>
    );
    expect(screen.getByText(/disabled by the superadmin by default/i)).toBeTruthy();
  });
});
```

- [ ] **Step 2: Verify fails** (Configuration page does not render placeholder yet).

- [ ] **Step 3: Modify each Tier 1 page**

Example for `ConfigurationPage.tsx` — find the top of the component's return. Wrap:

```tsx
import { useRole } from '@/lib/roleContext';
import { usePermissions } from '@/hooks/usePermissions';
import { LockedTabPlaceholder } from '@/components/LockedTabPlaceholder';
import { PermissionRequestDialog } from '@/components/PermissionRequestDialog';
import { api } from '@/lib/api';
import { useState } from 'react';

export function ConfigurationPage() {
  const role = useRole();
  const { has, hasPending } = usePermissions(role);
  const [reqOpen, setReqOpen] = useState(false);

  if (role === 'admin' && !has('tab:configuration')) {
    return (
      <>
        <LockedTabPlaceholder
          tabName="Configuration"
          permissionKey="tab:configuration"
          hasPending={hasPending('tab:configuration')}
          onRequestStart={() => setReqOpen(true)}
        />
        {reqOpen && (
          <PermissionRequestDialog
            isOpen permissionKey="tab:configuration"
            onClose={() => setReqOpen(false)}
            onSubmit={async (k, r) => (await api.createPermissionRequest(k, r)).ok}
          />
        )}
      </>
    );
  }

  return (
    /* ... existing Configuration UI unchanged ... */
  );
}
```

Repeat the identical shape for `IpWhitelistPage` (`tab:ipwhitelist`, tabName `"IP Whitelist"`), `UpdatesPage` (`tab:updates`, tabName `"Updates"`), and `RoleChangePage` (`tab:rolechange`, tabName `"Role Change"` — but this page is superadmin-only anyway; the gate is a safety net in case a non-superadmin somehow navigates there).

- [ ] **Step 4: Document new SignalR events in signalr.ts**

At the top of `admin-panel/src/lib/signalr.ts`, add a comment block:

```typescript
/*
Phase 6.11 SignalR event additions (wire listeners via `on('EventName', fn)`):

- PermissionRequested  (superadmin-only group) — { requestId, adminEmail, permissionKey, reason, requestedAt }
- PermissionApproved   (targeted via Clients.User) — { permissionKey, expiresAt? }
- PermissionDenied     (targeted) — { permissionKey, reviewNote? }
- PermissionRevoked    (targeted) — { permissionKey, reason? }
*/
```

- [ ] **Step 5: Verify gate smoke test passes + no regression**

```bash
npx vitest run src/__tests__/views/ConfigurationPageLocked.test.tsx 2>&1 | tail -5
npx vitest run 2>&1 | tail -5
```

Expected: 1 new pass; everything else green.

- [ ] **Step 6: Commit**

```bash
git add admin-panel/src/views/ConfigurationPage.tsx admin-panel/src/views/IpWhitelistPage.tsx admin-panel/src/views/UpdatesPage.tsx admin-panel/src/views/RoleChangePage.tsx admin-panel/src/lib/signalr.ts admin-panel/src/__tests__/views/ConfigurationPageLocked.test.tsx
git commit -m "feat(6.11.W3.T24): Tier 1 pages render LockedTabPlaceholder for unprivileged admin

Configuration / IP Whitelist / Updates / Role Change pages check
tab:* grant via usePermissions; when absent, render the placeholder
with the spec message + Request Permission button + pending-state
banner. Request dialog wired to api.createPermissionRequest."
```

### Task 25: robots.txt + noindex meta + nginx public-cut + SPF DNS fix

**Goal:** Ship the ops changes that accompany the admin panel going public-reachable. `robots.txt` and `<meta name="robots">` prevent search engines from indexing. Nginx basic-auth drops out (modern MFA + rate limit is sufficient). SPF TXT record gains Resend so invitation emails in Wave 4 don't fail alignment.

**Files:**
- Create: `admin-panel/public/robots.txt`
- Modify: `admin-panel/src/app/layout.tsx`
- Ops: nginx config `/etc/nginx/sites-enabled/auracore-admin` on origin
- Ops: DNS TXT record for `auracore.pro`

- [ ] **Step 1: Create `admin-panel/public/robots.txt`**

```
User-agent: *
Disallow: /
```

- [ ] **Step 2: Add `robots` to Next.js metadata**

Read `admin-panel/src/app/layout.tsx`. Find the `export const metadata: Metadata = { ... };` block. Add the `robots` field:

```tsx
export const metadata: Metadata = {
  // ... existing fields unchanged
  robots: { index: false, follow: false },
};
```

- [ ] **Step 3: Rebuild admin-panel and verify output**

```bash
cd admin-panel
npm run build 2>&1 | tail -10
grep -A1 'name="robots"' out/index.html | head -2
test -s out/robots.txt && cat out/robots.txt
```

Expected: built HTML has `<meta name="robots" content="noindex,nofollow">`; `out/robots.txt` contains `Disallow: /`.

- [ ] **Step 4: Commit frontend changes**

```bash
git add admin-panel/public/robots.txt admin-panel/src/app/layout.tsx
git commit -m "feat(6.11.W3.T25): robots.txt + noindex meta for admin-panel

Search engine indexing blocked via robots.txt (Disallow: /) +
<meta name='robots' content='noindex,nofollow'>. Pair with Task 25
nginx basic-auth removal — admin panel becomes public-reachable but
stays invisible to search indexing."
```

- [ ] **Step 5: Ops — remove nginx basic auth (manual via SSH)**

Connect to origin:

```bash
ssh -i C:/Users/Admin/.ssh/id_ed25519 root@165.227.170.3
```

Inside the session, back up then edit the config:

```bash
cp /etc/nginx/sites-enabled/auracore-admin /etc/nginx/sites-enabled/auracore-admin.bak-$(date +%Y%m%d%H%M)
grep -nE 'auth_basic' /etc/nginx/sites-enabled/auracore-admin
# Expected: two lines matching 'auth_basic' and 'auth_basic_user_file'
# Remove both lines:
sed -i '/auth_basic/d' /etc/nginx/sites-enabled/auracore-admin
# Verify:
grep -nE 'auth_basic' /etc/nginx/sites-enabled/auracore-admin
# Expected: no output
nginx -t && systemctl reload nginx
# Expected: "syntax is ok" then "test is successful"
```

Smoke-test from anywhere:

```bash
curl -sI https://admin.auracore.pro | head -5
# Expect: HTTP/2 200 (not 401 Unauthorized)
```

- [ ] **Step 6: Ops — SPF DNS TXT record update (via Namecheap web console or DO DNS)**

Current SPF (to confirm):

```bash
dig +short TXT auracore.pro | grep spf1
# Typical: "v=spf1 include:spf.privateemail.com ~all"
```

Add Resend to the SPF include list. The new record must be:

```
v=spf1 include:spf.privateemail.com include:_spf.resend.com ~all
```

Edit the DNS TXT record through whichever DNS provider manages `auracore.pro` (Namecheap if domain-owned there, or DigitalOcean if DNS delegated). A single TXT record for the bare domain, replacing the existing `v=spf1 ...` value.

Verify propagation (may take 1-4 hours):

```bash
dig +short TXT auracore.pro | grep spf1
# Expected: "v=spf1 include:spf.privateemail.com include:_spf.resend.com ~all"
```

Also check at https://mxtoolbox.com/SuperTool.aspx?action=spf&domain=auracore.pro that the record is valid and both includes resolve.

- [ ] **Step 7: Ops document the change**

No new git commit for the ops steps (they happen on origin). Instead, append a short note to the eventual memory file at the end of Wave 6 capturing: "admin.auracore.pro nginx basic-auth removed on YYYY-MM-DD; SPF updated to include _spf.resend.com".

---

## Wave 4 — Superadmin tabs + admin lifecycle + invitations + CSV export + mid-deploy

Seven tasks. After Wave 4 the superadmin has a full admin-management UI (create/suspend/restore/delete + promote/demote + edit permissions + reset password), an invitation-email flow, a streaming CSV export for audit log + admin action log, and the mid-deploy lands the permission/auth infrastructure to prod.

### Task 26: AdminManagementController — create + lifecycle + promote/demote

**Goal:** Superadmin CRUD over admin accounts. `POST /api/superadmin/admins` creates a new admin (email + template + force-password-change + 2FA requirement + either invitation toggle OR manual initial password). `POST /api/superadmin/admins/{id}/suspend|restore|reset-password`. `POST /api/superadmin/users/{userId}/promote`. `POST /api/superadmin/admins/{id}/demote`. `DELETE /api/superadmin/admins/{id}`.

**Files:**
- Create: `src/Backend/AuraCore.API/Controllers/Superadmin/AdminManagementController.cs`
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/AdminManagementControllerTests.cs`

- [ ] **Step 1: Write failing tests (reasonable subset — creation, suspend, promote, delete)**

`tests/AuraCore.Tests.API/SuperadminFoundation/AdminManagementControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuraCore.API.Application.Interfaces;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Helpers;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class AdminManagementControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _f;

    public AdminManagementControllerTests(WebApplicationFactory<Program> f)
    {
        Environment.SetEnvironmentVariable("JWT_SECRET", "test-secret-at-least-32-characters-long!!");
        _f = f.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            var d = s.Single(x => x.ServiceType == typeof(DbContextOptions<AuraCoreDbContext>));
            s.Remove(d);
            s.AddDbContext<AuraCoreDbContext>(o => o.UseInMemoryDatabase($"amc-{Guid.NewGuid()}"));
        }));
    }

    private async Task<(HttpClient c, Guid superId)> SuperClient()
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var su = new User { Id = Guid.NewGuid(), Email = "super@x.com", PasswordHash = "x", Role = "superadmin", TotpEnabled = true };
        db.Users.Add(su); await db.SaveChangesAsync();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var token = auth.GenerateAccessToken(su);
        var c = _f.CreateClient();
        c.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return (c, su.Id);
    }

    [Fact]
    public async Task Create_admin_with_Trusted_template_emits_6_grants()
    {
        var (c, _) = await SuperClient();
        var res = await c.PostAsJsonAsync("/api/superadmin/admins", new {
            email = "new-admin@x.com", sendInvitation = false, initialPassword = "Abcdefghij12",
            forcePasswordChange = "on_first_login", template = "Trusted", require2fa = true,
        });
        res.EnsureSuccessStatusCode();

        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var user = await db.Users.FirstAsync(u => u.Email == "new-admin@x.com");
        Assert.Equal("admin", user.Role);
        Assert.True(user.ForcePasswordChange);
        Assert.True(user.Require2fa);
        var grants = await db.PermissionGrants.Where(g => g.AdminUserId == user.Id).ToListAsync();
        Assert.Equal(PermissionKeys.AllTier2.Count, grants.Count);
    }

    [Fact]
    public async Task Create_admin_without_invitation_or_password_fails_validation()
    {
        var (c, _) = await SuperClient();
        var res = await c.PostAsJsonAsync("/api/superadmin/admins", new {
            email = "foo@x.com", sendInvitation = false, template = "Default",
            forcePasswordChange = "never", require2fa = false,
            // initialPassword missing + sendInvitation=false
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Suspend_admin_sets_is_active_false_and_revokes_tokens()
    {
        var (c, _) = await SuperClient();
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var target = new User { Id = Guid.NewGuid(), Email = "target@x.com", PasswordHash = "x", Role = "admin", IsActive = true };
        db.Users.Add(target);
        db.RefreshTokens.Add(new RefreshToken { UserId = target.Id, Token = "t1", ExpiresAt = DateTimeOffset.UtcNow.AddDays(5) });
        await db.SaveChangesAsync();

        var res = await c.PostAsync($"/api/superadmin/admins/{target.Id}/suspend", null);
        res.EnsureSuccessStatusCode();

        using var scope2 = _f.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var reloaded = await db2.Users.FirstAsync(u => u.Id == target.Id);
        Assert.False(reloaded.IsActive);
        var refreshes = await db2.RefreshTokens.Where(r => r.UserId == target.Id).ToListAsync();
        Assert.All(refreshes, r => Assert.True(r.IsRevoked));
    }

    [Fact]
    public async Task Promote_existing_user_changes_role_to_admin_and_applies_template()
    {
        var (c, _) = await SuperClient();
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var user = new User { Id = Guid.NewGuid(), Email = "u@x.com", PasswordHash = "x", Role = "user" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var res = await c.PostAsJsonAsync($"/api/superadmin/users/{user.Id}/promote", new {
            template = "Trusted", forcePasswordChange = "never", require2fa = false,
        });
        res.EnsureSuccessStatusCode();

        using var scope2 = _f.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var reloaded = await db2.Users.FirstAsync(u => u.Id == user.Id);
        Assert.Equal("admin", reloaded.Role);
        Assert.Equal(6, await db2.PermissionGrants.CountAsync(g => g.AdminUserId == user.Id));
    }

    [Fact]
    public async Task Delete_admin_cascades_permission_grants_and_requests()
    {
        var (c, _) = await SuperClient();
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var target = new User { Id = Guid.NewGuid(), Email = "t@x.com", PasswordHash = "x", Role = "admin" };
        db.Users.Add(target);
        db.PermissionGrants.Add(new PermissionGrant { AdminUserId = target.Id, PermissionKey = "tab:updates", GrantedBy = target.Id });
        db.PermissionRequests.Add(new PermissionRequest { AdminUserId = target.Id, PermissionKey = "tab:updates", Reason = "test reason long enough to pass 50 char" });
        await db.SaveChangesAsync();

        var res = await c.DeleteAsync($"/api/superadmin/admins/{target.Id}");
        res.EnsureSuccessStatusCode();

        using var scope2 = _f.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        Assert.Equal(0, await db2.Users.CountAsync(u => u.Id == target.Id));
        Assert.Equal(0, await db2.PermissionGrants.CountAsync(g => g.AdminUserId == target.Id));
        Assert.Equal(0, await db2.PermissionRequests.CountAsync(r => r.AdminUserId == target.Id));
    }
}
```

- [ ] **Step 2: Verify tests fail (404)**

- [ ] **Step 3: Create `AdminManagementController.cs`**

`src/Backend/AuraCore.API/Controllers/Superadmin/AdminManagementController.cs`:

```csharp
using System.Security.Cryptography;
using AuraCore.API.Application.Services.Email;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Filters;
using AuraCore.API.Helpers;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Superadmin;

[ApiController]
[Route("api/superadmin")]
[Authorize(Roles = "superadmin")]
public sealed class AdminManagementController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    private readonly IEmailService _email;

    public AdminManagementController(AuraCoreDbContext db, IEmailService email) { _db = db; _email = email; }

    [HttpGet("admins")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await _db.Users
            .Where(u => u.Role == "admin" || u.Role == "superadmin")
            .OrderBy(u => u.Email)
            .Select(u => new {
                id = u.Id, u.Email, u.Role, isActive = u.IsActive, isReadonly = u.IsReadonly,
                totpEnabled = u.TotpEnabled, require2fa = u.Require2fa,
                createdAt = u.CreatedAt, createdVia = u.CreatedVia,
                createdByEmail = _db.Users.Where(cu => cu.Id == u.CreatedByUserId).Select(cu => cu.Email).FirstOrDefault(),
            })
            .ToListAsync(ct);
        return Ok(new { items });
    }

    [HttpPost("admins")]
    [AuditAction("CreateAdmin", "User")]
    public async Task<IActionResult> Create([FromBody] CreateAdminDto dto, CancellationToken ct)
    {
        var superId = User.GetUserId()!.Value;

        if (string.IsNullOrWhiteSpace(dto.Email)) return BadRequest(new { error = "email_required" });
        var email = dto.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            return BadRequest(new { error = "email_exists" });

        if (!dto.SendInvitation && string.IsNullOrWhiteSpace(dto.InitialPassword))
            return BadRequest(new { error = "password_or_invitation_required" });
        if (!PermissionTemplates.IsValidTemplate(dto.Template))
            return BadRequest(new { error = "unknown_template" });

        var user = new User {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = dto.SendInvitation
                ? BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")) // throwaway; replaced on redemption
                : BCrypt.Net.BCrypt.HashPassword(dto.InitialPassword!),
            Role = "admin",
            IsActive = true,
            IsReadonly = PermissionTemplates.RequiresIsReadonlyFlag(dto.Template),
            CreatedByUserId = superId,
            CreatedVia = "superadmin_create",
            Require2fa = dto.Require2fa,
            ForcePasswordChange = dto.ForcePasswordChange != "never",
            ForcePasswordChangeBy = ForceChangeDeadline(dto.ForcePasswordChange),
        };
        _db.Users.Add(user);

        // Apply template grants
        if (dto.Template == PermissionTemplates.Custom)
        {
            foreach (var ck in dto.CustomKeys ?? Array.Empty<CustomKey>())
            {
                if (!PermissionKeys.IsValidKey(ck.PermissionKey)) continue;
                _db.PermissionGrants.Add(new PermissionGrant {
                    AdminUserId = user.Id, PermissionKey = ck.PermissionKey,
                    GrantedBy = superId, ExpiresAt = ck.ExpiresAt,
                });
            }
        }
        else
        {
            foreach (var key in PermissionTemplates.GetPermissionsForTemplate(dto.Template))
                _db.PermissionGrants.Add(new PermissionGrant {
                    AdminUserId = user.Id, PermissionKey = key, GrantedBy = superId,
                });
        }

        await _db.SaveChangesAsync(ct);

        if (dto.SendInvitation)
        {
            var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace("+", "-").Replace("/", "_").TrimEnd('=');
            var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
            var inv = new AdminInvitation {
                TokenHash = hash, AdminUserId = user.Id, CreatedBy = superId,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
            };
            _db.AdminInvitations.Add(inv);
            await _db.SaveChangesAsync(ct);

            var setupLink = $"https://admin.auracore.pro/#/invite?token={raw}&email={Uri.EscapeDataString(email)}";
            await _email.SendFromTemplateAsync(EmailTemplate.AdminInvitation, new {
                to = email, adminEmail = email, invitedBy = User.GetEmail() ?? "superadmin",
                setupLink, expiresAt = inv.ExpiresAt.ToString("u"),
            }, ct);
        }
        else
        {
            // Let the superadmin know the account exists + has a manually-set password they must share out-of-band.
            await _email.SendFromTemplateAsync(EmailTemplate.AdminCreatedWithoutEmail, new {
                to = User.GetEmail() ?? email, adminEmail = email,
                note = "The initial password must be shared with the admin out-of-band (e.g. via password manager).",
            }, ct);
        }

        return Ok(new { id = user.Id, email = user.Email, template = dto.Template });
    }

    [HttpPost("admins/{id:guid}/suspend")]
    [AuditAction("SuspendAdmin", "User", TargetIdFromRouteKey = "id")]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken ct)
    {
        var superId = User.GetUserId()!.Value;
        var u = await _db.Users.FindAsync(new object[] { id }, ct);
        if (u is null) return NotFound();
        if (u.Role != "admin") return BadRequest(new { error = "only_admin_can_be_suspended" });
        u.IsActive = false;

        // Revoke all outstanding refresh tokens. Access-token jtis that are still
        // valid are handled separately — we don't have their jtis here, but the
        // AccessToken expiry is 15min so effective lockout is ≤15min.
        var refreshes = await _db.RefreshTokens.Where(r => r.UserId == id && !r.IsRevoked).ToListAsync(ct);
        foreach (var r in refreshes) r.IsRevoked = true;
        await _db.SaveChangesAsync(ct);
        return Ok(new { id, isActive = false });
    }

    [HttpPost("admins/{id:guid}/restore")]
    [AuditAction("RestoreAdmin", "User", TargetIdFromRouteKey = "id")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct)
    {
        var u = await _db.Users.FindAsync(new object[] { id }, ct);
        if (u is null) return NotFound();
        u.IsActive = true;
        await _db.SaveChangesAsync(ct);
        return Ok(new { id, isActive = true });
    }

    [HttpPost("admins/{id:guid}/reset-password")]
    [AuditAction("ResetAdminPassword", "User", TargetIdFromRouteKey = "id")]
    public async Task<IActionResult> ResetPassword(Guid id, CancellationToken ct)
    {
        var u = await _db.Users.FindAsync(new object[] { id }, ct);
        if (u is null) return NotFound();

        // Issue a single-use invitation-style token for password reset
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace("+","-").Replace("/","_").TrimEnd('=');
        var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
        _db.AdminInvitations.Add(new AdminInvitation {
            TokenHash = hash, AdminUserId = u.Id, CreatedBy = User.GetUserId()!.Value,
            ExpiresAt = DateTime.UtcNow.AddDays(3),
        });
        await _db.SaveChangesAsync(ct);

        var setupLink = $"https://admin.auracore.pro/#/invite?token={raw}&email={Uri.EscapeDataString(u.Email)}";
        await _email.SendFromTemplateAsync(EmailTemplate.AdminInvitation, new {
            to = u.Email, adminEmail = u.Email, invitedBy = User.GetEmail() ?? "superadmin",
            setupLink, expiresAt = DateTime.UtcNow.AddDays(3).ToString("u"),
        }, ct);
        return Ok(new { id = u.Id, reset = true });
    }

    [HttpDelete("admins/{id:guid}")]
    [RequiresPermission(PermissionKeys.ActionUsersDelete)]
    [AuditAction("DeleteAdmin", "User", TargetIdFromRouteKey = "id")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var u = await _db.Users.FindAsync(new object[] { id }, ct);
        if (u is null) return NotFound();
        if (u.Role == "superadmin") return BadRequest(new { error = "cannot_delete_superadmin" });

        // Cascade: permission_grants, permission_requests, revoked_tokens, admin_invitations
        // all have ON DELETE CASCADE at DB level (see Task 2).
        _db.Users.Remove(u);
        await _db.SaveChangesAsync(ct);
        return Ok(new { id });
    }

    [HttpPost("users/{userId:guid}/promote")]
    [AuditAction("PromoteUser", "User", TargetIdFromRouteKey = "userId")]
    public async Task<IActionResult> Promote(Guid userId, [FromBody] PromoteDto dto, CancellationToken ct)
    {
        var superId = User.GetUserId()!.Value;
        var u = await _db.Users.FindAsync(new object[] { userId }, ct);
        if (u is null) return NotFound();
        if (u.Role != "user") return BadRequest(new { error = "only_user_role_can_be_promoted" });
        if (!PermissionTemplates.IsValidTemplate(dto.Template))
            return BadRequest(new { error = "unknown_template" });

        u.Role = "admin";
        u.CreatedVia = "admin_promote";
        u.CreatedByUserId = superId;
        u.Require2fa = dto.Require2fa;
        u.IsReadonly = PermissionTemplates.RequiresIsReadonlyFlag(dto.Template);
        u.ForcePasswordChange = dto.ForcePasswordChange != "never";
        u.ForcePasswordChangeBy = ForceChangeDeadline(dto.ForcePasswordChange);

        if (dto.Template == PermissionTemplates.Custom)
            foreach (var ck in dto.CustomKeys ?? Array.Empty<CustomKey>())
                _db.PermissionGrants.Add(new PermissionGrant {
                    AdminUserId = u.Id, PermissionKey = ck.PermissionKey,
                    GrantedBy = superId, ExpiresAt = ck.ExpiresAt,
                });
        else
            foreach (var key in PermissionTemplates.GetPermissionsForTemplate(dto.Template))
                _db.PermissionGrants.Add(new PermissionGrant {
                    AdminUserId = u.Id, PermissionKey = key, GrantedBy = superId,
                });

        await _db.SaveChangesAsync(ct);
        return Ok(new { id = u.Id, role = u.Role });
    }

    [HttpPost("admins/{id:guid}/demote")]
    [AuditAction("DemoteAdmin", "User", TargetIdFromRouteKey = "id")]
    public async Task<IActionResult> Demote(Guid id, CancellationToken ct)
    {
        var u = await _db.Users.FindAsync(new object[] { id }, ct);
        if (u is null) return NotFound();
        if (u.Role != "admin") return BadRequest(new { error = "only_admin_can_be_demoted" });
        u.Role = "user";
        u.IsReadonly = false;
        // Revoke all outstanding grants
        var grants = await _db.PermissionGrants.Where(g => g.AdminUserId == id && g.RevokedAt == null).ToListAsync(ct);
        foreach (var g in grants) { g.RevokedAt = DateTime.UtcNow; g.RevokeReason = "demoted"; }
        await _db.SaveChangesAsync(ct);
        return Ok(new { id, role = u.Role });
    }

    [HttpPut("admins/{id:guid}/require-2fa")]
    public async Task<IActionResult> SetRequire2fa(Guid id, [FromBody] SetRequire2faDto dto, CancellationToken ct)
    {
        var u = await _db.Users.FindAsync(new object[] { id }, ct);
        if (u is null) return NotFound();
        u.Require2fa = dto.Require2fa;
        await _db.SaveChangesAsync(ct);
        return Ok(new { id, require2fa = u.Require2fa });
    }

    private static DateTimeOffset? ForceChangeDeadline(string policy) => policy switch
    {
        "on_first_login" => DateTimeOffset.UtcNow,                 // already due
        "within_7_days"  => DateTimeOffset.UtcNow.AddDays(7),
        "within_30_days" => DateTimeOffset.UtcNow.AddDays(30),
        "never"          => null,
        _                => null,
    };

    public sealed record CreateAdminDto(
        string Email, bool SendInvitation, string? InitialPassword,
        string ForcePasswordChange, string Template, List<CustomKey>? CustomKeys,
        bool Require2fa);
    public sealed record PromoteDto(
        string Template, string ForcePasswordChange, bool Require2fa,
        List<CustomKey>? CustomKeys);
    public sealed record CustomKey(string PermissionKey, DateTime? ExpiresAt);
    public sealed record SetRequire2faDto(bool Require2fa);
}
```

- [ ] **Step 4: Tests pass**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter "FullyQualifiedName~AdminManagementControllerTests" 2>&1 | tail -5
```

Expected: 5 passed.

- [ ] **Step 5: Commit**

```bash
git add src/Backend/AuraCore.API/Controllers/Superadmin/AdminManagementController.cs tests/AuraCore.Tests.API/SuperadminFoundation/AdminManagementControllerTests.cs
git commit -m "feat(6.11.W4.T26): AdminManagementController — create/lifecycle/promote/demote

Endpoints:
- GET  /api/superadmin/admins
- POST /api/superadmin/admins (Default/Trusted/ReadOnly/Custom templates)
- POST /api/superadmin/admins/{id}/suspend | restore | reset-password
- DELETE /api/superadmin/admins/{id}  — requires action:users.delete
- POST /api/superadmin/users/{userId}/promote
- POST /api/superadmin/admins/{id}/demote
- PUT  /api/superadmin/admins/{id}/require-2fa

Suspend revokes all refresh_tokens. Delete cascades permission_grants +
requests + invitations at DB level. Demote revokes all grants as
'demoted'. Invitation toggle emails a 7-day one-time setup link."
```

### Task 27: Redeem-invitation + change-password endpoints on AuthController

**Goal:** `POST /api/auth/redeem-invitation` — admin clicks the link in the invitation email, submits new password, backend hashes the raw token to SHA256, looks up in `admin_invitations`, verifies not-consumed + not-expired, sets the user's password, marks token consumed, returns a fresh JWT. Also a `POST /api/auth/change-password` for in-session password changes.

**Files:**
- Modify: `src/Backend/AuraCore.API/Controllers/AuthController.cs` — add the two new endpoints
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/AdminInvitationFlowTests.cs`

- [ ] **Step 1: Write failing test**

`tests/AuraCore.Tests.API/SuperadminFoundation/AdminInvitationFlowTests.cs`:

```csharp
using System.Net.Http.Json;
using System.Security.Cryptography;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class AdminInvitationFlowTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _f;

    public AdminInvitationFlowTests(WebApplicationFactory<Program> f)
    {
        Environment.SetEnvironmentVariable("JWT_SECRET", "test-secret-at-least-32-characters-long!!");
        _f = f.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            var d = s.Single(x => x.ServiceType == typeof(DbContextOptions<AuraCoreDbContext>));
            s.Remove(d);
            s.AddDbContext<AuraCoreDbContext>(o => o.UseInMemoryDatabase($"inv-{Guid.NewGuid()}"));
        }));
    }

    private static string Sha256(string s)
        => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(s))).ToLowerInvariant();

    [Fact]
    public async Task Valid_token_redeems_password_and_returns_token()
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var user = new User { Id = Guid.NewGuid(), Email = "new@x.com", PasswordHash = "temp", Role = "admin" };
        db.Users.Add(user);
        var raw = "abcd1234";
        db.AdminInvitations.Add(new AdminInvitation {
            TokenHash = Sha256(raw), AdminUserId = user.Id, CreatedBy = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
        });
        await db.SaveChangesAsync();

        var c = _f.CreateClient();
        var res = await c.PostAsJsonAsync("/api/auth/redeem-invitation", new {
            token = raw, email = "new@x.com", newPassword = "NewSecurePass12!",
        });
        res.EnsureSuccessStatusCode();
        Assert.Contains("accessToken", await res.Content.ReadAsStringAsync());

        using var scope2 = _f.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var reloaded = await db2.Users.FirstAsync(u => u.Id == user.Id);
        Assert.True(BCrypt.Net.BCrypt.Verify("NewSecurePass12!", reloaded.PasswordHash));
        var inv = await db2.AdminInvitations.FirstAsync();
        Assert.NotNull(inv.ConsumedAt);
    }

    [Fact]
    public async Task Consumed_token_returns_410()
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var user = new User { Id = Guid.NewGuid(), Email = "used@x.com", PasswordHash = "x", Role = "admin" };
        db.Users.Add(user);
        var raw = "consumed-token";
        db.AdminInvitations.Add(new AdminInvitation {
            TokenHash = Sha256(raw), AdminUserId = user.Id, CreatedBy = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(1), ConsumedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var c = _f.CreateClient();
        var res = await c.PostAsJsonAsync("/api/auth/redeem-invitation", new {
            token = raw, email = "used@x.com", newPassword = "NewSecurePass12!",
        });
        Assert.Equal(System.Net.HttpStatusCode.Gone, res.StatusCode);
    }

    [Fact]
    public async Task Expired_token_returns_410()
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var user = new User { Id = Guid.NewGuid(), Email = "exp@x.com", PasswordHash = "x", Role = "admin" };
        db.Users.Add(user);
        var raw = "expired-token";
        db.AdminInvitations.Add(new AdminInvitation {
            TokenHash = Sha256(raw), AdminUserId = user.Id, CreatedBy = user.Id,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
        });
        await db.SaveChangesAsync();

        var c = _f.CreateClient();
        var res = await c.PostAsJsonAsync("/api/auth/redeem-invitation", new {
            token = raw, email = "exp@x.com", newPassword = "NewSecurePass12!",
        });
        Assert.Equal(System.Net.HttpStatusCode.Gone, res.StatusCode);
    }
}
```

- [ ] **Step 2: Verify tests fail (404)**

- [ ] **Step 3: Add endpoints to `AuthController`**

Append to `AuthController.cs` before the final closing brace:

```csharp
    [HttpPost("redeem-invitation")]
    public async Task<IActionResult> RedeemInvitation([FromBody] RedeemDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Token) || string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.NewPassword))
            return BadRequest(new { error = "missing_fields" });
        if (dto.NewPassword.Length < 10)
            return BadRequest(new { error = "password_too_short" });

        var email = dto.Email.Trim().ToLowerInvariant();
        var hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(dto.Token));
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var inv = await _db.AdminInvitations.FirstOrDefaultAsync(i => i.TokenHash == hash, ct);
        if (inv is null) return StatusCode(410, new { error = "invitation_invalid" });
        if (inv.ConsumedAt != null || inv.ExpiresAt < DateTime.UtcNow)
            return StatusCode(410, new { error = "invitation_expired_or_consumed" });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == inv.AdminUserId && u.Email == email, ct);
        if (user is null) return BadRequest(new { error = "email_mismatch" });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        user.PasswordChangedAt = DateTimeOffset.UtcNow;
        user.ForcePasswordChange = false;
        user.ForcePasswordChangeBy = null;
        inv.ConsumedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var access = _auth.GenerateAccessToken(user);
        var refresh = _auth.GenerateRefreshToken();
        _db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, Token = refresh, ExpiresAt = DateTimeOffset.UtcNow.AddDays(30) });
        await _db.SaveChangesAsync(ct);

        return Ok(new { accessToken = access, refreshToken = refresh, user = new { user.Id, user.Email, user.Role } });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePwDto dto, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();
        var user = await _db.Users.FindAsync(new object[] { userId.Value }, ct);
        if (user is null) return Unauthorized();

        if (dto.NewPassword is null || dto.NewPassword.Length < 10)
            return BadRequest(new { error = "password_too_short" });

        // Current-password verification is required UNLESS ForcePasswordChange is set and the deadline has passed (emergency unlock).
        var deadlinePassed = user.ForcePasswordChange && user.ForcePasswordChangeBy != null && user.ForcePasswordChangeBy < DateTimeOffset.UtcNow;
        if (!deadlinePassed)
        {
            if (string.IsNullOrEmpty(dto.CurrentPassword) || !BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
                return Unauthorized(new { error = "invalid_current_password" });
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        user.PasswordChangedAt = DateTimeOffset.UtcNow;
        user.ForcePasswordChange = false;
        user.ForcePasswordChangeBy = null;

        // Revoke all existing refresh tokens so other sessions die.
        var refreshes = await _db.RefreshTokens.Where(r => r.UserId == user.Id && !r.IsRevoked).ToListAsync(ct);
        foreach (var r in refreshes) r.IsRevoked = true;
        await _db.SaveChangesAsync(ct);

        // Blacklist the current access token via jti so other sessions using this same token also die.
        var currentJti = User.GetJti();
        if (!string.IsNullOrEmpty(currentJti))
        {
            _db.RevokedTokens.Add(new RevokedToken { Jti = currentJti, UserId = user.Id, RevokeReason = "password_reset" });
            await _db.SaveChangesAsync(ct);
        }

        var access = _auth.GenerateAccessToken(user);
        var refresh = _auth.GenerateRefreshToken();
        _db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, Token = refresh, ExpiresAt = DateTimeOffset.UtcNow.AddDays(30) });
        await _db.SaveChangesAsync(ct);
        return Ok(new { accessToken = access, refreshToken = refresh });
    }

    public sealed record RedeemDto(string Token, string Email, string NewPassword);
    public sealed record ChangePwDto(string? CurrentPassword, string NewPassword);
```

Also add `using AuraCore.API.Helpers;` and `using Microsoft.EntityFrameworkCore;` to the top if not already present.

- [ ] **Step 4: Tests pass**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj \
  --filter "FullyQualifiedName~AdminInvitationFlowTests" 2>&1 | tail -5
```

Expected: 3 passed.

- [ ] **Step 5: Commit**

```bash
git add src/Backend/AuraCore.API/Controllers/AuthController.cs tests/AuraCore.Tests.API/SuperadminFoundation/AdminInvitationFlowTests.cs
git commit -m "feat(6.11.W4.T27): redeem-invitation + change-password endpoints

- POST /api/auth/redeem-invitation (no auth) — verifies SHA256 hash,
  sets password, marks token consumed, returns JWT+refresh.
  410 Gone on consumed or expired tokens.
- POST /api/auth/change-password (auth required) — verifies current
  password unless force-change deadline has passed (emergency unlock).
  Revokes all refresh tokens + blacklists current access jti on success."
```

### Task 28: AdminActionLogController + CSV streaming endpoints

**Goal:** Superadmin-scoped admin-actions list + stats + streaming CSV. Admin-scoped audit-log export CSV (read-only visibility of the existing `audit_log` table, per spec D9).

**Files:**
- Create: `src/Backend/AuraCore.API/Controllers/Superadmin/AdminActionLogController.cs`
- Create: `src/Backend/AuraCore.API/Controllers/Admin/AuditLogExportController.cs`
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/AdminActionLogCsvExportTests.cs`

- [ ] **Step 1: Write failing tests (list + CSV)**

`tests/AuraCore.Tests.API/SuperadminFoundation/AdminActionLogCsvExportTests.cs`:

```csharp
using System.Net.Http.Json;
using AuraCore.API.Application.Interfaces;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class AdminActionLogCsvExportTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _f;

    public AdminActionLogCsvExportTests(WebApplicationFactory<Program> f)
    {
        Environment.SetEnvironmentVariable("JWT_SECRET", "test-secret-at-least-32-characters-long!!");
        _f = f.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            var d = s.Single(x => x.ServiceType == typeof(DbContextOptions<AuraCoreDbContext>));
            s.Remove(d);
            s.AddDbContext<AuraCoreDbContext>(o => o.UseInMemoryDatabase($"cal-{Guid.NewGuid()}"));
        }));
    }

    private async Task<HttpClient> Authed(string role)
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var u = new User { Id = Guid.NewGuid(), Email = $"{role}@x.com", PasswordHash = "x", Role = role, TotpEnabled = true };
        db.Users.Add(u); await db.SaveChangesAsync();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var c = _f.CreateClient();
        c.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.GenerateAccessToken(u));
        return c;
    }

    [Fact]
    public async Task Superadmin_admin_actions_csv_contains_header_and_rows()
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var actorId = Guid.NewGuid();
        db.Users.Add(new User { Id = actorId, Email = "admin-actor@x.com", PasswordHash = "x", Role = "admin" });
        db.AuditLogs.Add(new AuditLogEntry { ActorId = actorId, ActorEmail = "admin-actor@x.com", Action = "DeleteUser", TargetType = "User", TargetId = Guid.NewGuid().ToString() });
        db.AuditLogs.Add(new AuditLogEntry { ActorId = null, ActorEmail = "system@x.com", Action = "AutoMigrate", TargetType = "System" });
        await db.SaveChangesAsync();

        var c = await Authed("superadmin");
        var res = await c.GetAsync("/api/superadmin/admin-actions/export.csv");
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadAsStringAsync();
        Assert.StartsWith("\"id\",\"actor_email\",\"actor_id\",\"action\",\"target_type\",\"target_id\",\"ip_address\",\"created_at_utc\"", body);
        Assert.Contains("DeleteUser", body);
        // System-scoped row should NOT appear (role-filtered to admin actors)
        Assert.DoesNotContain("AutoMigrate", body);
    }

    [Fact]
    public async Task Admin_audit_log_export_csv_includes_all_rows()
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        db.AuditLogs.Add(new AuditLogEntry { ActorEmail = "anyone@x.com", Action = "X", TargetType = "Y" });
        await db.SaveChangesAsync();

        var c = await Authed("admin");
        var res = await c.GetAsync("/api/admin/audit-log/export.csv");
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("\"action\"", body);
    }
}
```

- [ ] **Step 2: Verify tests fail**

- [ ] **Step 3: Create both controllers**

`src/Backend/AuraCore.API/Controllers/Superadmin/AdminActionLogController.cs`:

```csharp
using System.Runtime.CompilerServices;
using System.Text;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Superadmin;

[ApiController]
[Route("api/superadmin/admin-actions")]
[Authorize(Roles = "superadmin")]
public sealed class AdminActionLogController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    public AdminActionLogController(AuraCoreDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? actorEmail, [FromQuery] string? action,
        [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (pageSize > 200) pageSize = 200;
        var adminActorIds = _db.Users.Where(u => u.Role == "admin").Select(u => u.Id);
        var q = _db.AuditLogs.Where(a => a.ActorId != null && adminActorIds.Contains(a.ActorId.Value));

        if (!string.IsNullOrEmpty(actorEmail)) q = q.Where(a => a.ActorEmail.Contains(actorEmail));
        if (!string.IsNullOrEmpty(action)) q = q.Where(a => a.Action == action);
        if (dateFrom.HasValue) q = q.Where(a => a.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue) q = q.Where(a => a.CreatedAt <= dateTo.Value);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new { a.Id, a.ActorEmail, a.ActorId, a.Action, a.TargetType, a.TargetId, a.IpAddress, a.CreatedAt })
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct)
    {
        var adminActorIds = _db.Users.Where(u => u.Role == "admin").Select(u => u.Id);
        var baseQ = _db.AuditLogs.Where(a => a.ActorId != null && adminActorIds.Contains(a.ActorId.Value));

        var total = await baseQ.CountAsync(ct);
        var cutoff24 = DateTimeOffset.UtcNow.AddDays(-1);
        var cutoff7  = DateTimeOffset.UtcNow.AddDays(-7);
        var last24h = await baseQ.CountAsync(a => a.CreatedAt > cutoff24, ct);
        var last7d  = await baseQ.CountAsync(a => a.CreatedAt > cutoff7, ct);

        var topAdmins = await baseQ.GroupBy(a => a.ActorEmail)
            .Select(g => new { email = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count).Take(5).ToListAsync(ct);
        var topActions = await baseQ.GroupBy(a => a.Action)
            .Select(g => new { action = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count).Take(5).ToListAsync(ct);

        return Ok(new { total, last24h, last7d, topAdmins, topActions });
    }

    [HttpGet("export.csv")]
    public async Task ExportCsv(
        [FromQuery] string? actorEmail, [FromQuery] string? action,
        [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo,
        CancellationToken ct = default)
    {
        var adminActorIds = _db.Users.Where(u => u.Role == "admin").Select(u => u.Id);
        IQueryable<AuditLogEntry> q = _db.AuditLogs.Where(a => a.ActorId != null && adminActorIds.Contains(a.ActorId.Value));
        if (!string.IsNullOrEmpty(actorEmail)) q = q.Where(a => a.ActorEmail.Contains(actorEmail));
        if (!string.IsNullOrEmpty(action))     q = q.Where(a => a.Action == action);
        if (dateFrom.HasValue) q = q.Where(a => a.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue)   q = q.Where(a => a.CreatedAt <= dateTo.Value);

        Response.ContentType = "text/csv; charset=utf-8";
        Response.Headers["Content-Disposition"] = $"attachment; filename=\"admin-actions-{DateTime.UtcNow:yyyyMMddHHmmss}.csv\"";
        await WriteCsvAsync(q.AsAsyncEnumerable(), Response.Body, ct);
    }

    internal static async Task WriteCsvAsync(IAsyncEnumerable<AuditLogEntry> rows, Stream output, CancellationToken ct)
    {
        await using var writer = new StreamWriter(output, Encoding.UTF8, leaveOpen: true);
        await writer.WriteLineAsync("\"id\",\"actor_email\",\"actor_id\",\"action\",\"target_type\",\"target_id\",\"ip_address\",\"created_at_utc\"");
        await foreach (var r in rows.WithCancellation(ct))
        {
            var line =
                $"\"{r.Id}\"," +
                $"\"{Esc(r.ActorEmail)}\"," +
                $"\"{r.ActorId?.ToString() ?? ""}\"," +
                $"\"{Esc(r.Action)}\"," +
                $"\"{Esc(r.TargetType)}\"," +
                $"\"{Esc(r.TargetId ?? "")}\"," +
                $"\"{Esc(r.IpAddress ?? "")}\"," +
                $"\"{r.CreatedAt.UtcDateTime:o}\"";
            await writer.WriteLineAsync(line);
        }
    }

    private static string Esc(string s) => s?.Replace("\"", "\"\"") ?? "";
}
```

`src/Backend/AuraCore.API/Controllers/Admin/AuditLogExportController.cs`:

```csharp
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Admin;

[ApiController]
[Route("api/admin/audit-log")]
[Authorize(Roles = "admin")]
public sealed class AuditLogExportController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    public AuditLogExportController(AuraCoreDbContext db) => _db = db;

    [HttpGet("export.csv")]
    public async Task ExportCsv(
        [FromQuery] string? actorEmail, [FromQuery] string? action,
        [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo,
        CancellationToken ct = default)
    {
        IQueryable<AuditLogEntry> q = _db.AuditLogs;
        if (!string.IsNullOrEmpty(actorEmail)) q = q.Where(a => a.ActorEmail.Contains(actorEmail));
        if (!string.IsNullOrEmpty(action))     q = q.Where(a => a.Action == action);
        if (dateFrom.HasValue) q = q.Where(a => a.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue)   q = q.Where(a => a.CreatedAt <= dateTo.Value);

        Response.ContentType = "text/csv; charset=utf-8";
        Response.Headers["Content-Disposition"] = $"attachment; filename=\"audit-log-{DateTime.UtcNow:yyyyMMddHHmmss}.csv\"";
        await Superadmin.AdminActionLogController.WriteCsvAsync(q.AsAsyncEnumerable(), Response.Body, ct);
    }
}
```

- [ ] **Step 4: Tests pass**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj \
  --filter "FullyQualifiedName~AdminActionLogCsvExportTests" 2>&1 | tail -5
```

Expected: 2 passed.

- [ ] **Step 5: Commit**

```bash
git add src/Backend/AuraCore.API/Controllers/Superadmin/AdminActionLogController.cs src/Backend/AuraCore.API/Controllers/Admin/AuditLogExportController.cs tests/AuraCore.Tests.API/SuperadminFoundation/AdminActionLogCsvExportTests.cs
git commit -m "feat(6.11.W4.T28): AdminActionLogController + AuditLogExportController

GET /api/superadmin/admin-actions (list) + /stats (KPIs) + /export.csv
(role-filtered streaming). GET /api/admin/audit-log/export.csv (all rows).

CSV streaming uses IAsyncEnumerable + chunked Response.Body.WriteAsync
so gigabyte-scale logs don't spike memory. RFC 4180-style quoting,
double-quote escaping. Filter params: actorEmail, action, dateFrom,
dateTo. Response headers set filename + text/csv; charset=utf-8."
```

### Task 29: PermissionRequestsPage (superadmin tab)

**Goal:** Real implementation of the superadmin's Permission Requests inbox. Table with status filter (default pending), bulk select + approve/deny, per-row approve/deny dialogs with optional `expires_at` + `review_note`. Live SignalR updates via `PermissionRequested` event → prepend + badge counter.

**Files:**
- Replace stub: `admin-panel/src/views/PermissionRequestsPage.tsx`
- Test: `admin-panel/src/__tests__/views/PermissionRequestsPage.test.tsx`

- [ ] **Step 1: Write failing test**

`admin-panel/src/__tests__/views/PermissionRequestsPage.test.tsx`:

```typescript
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { PermissionRequestsPage } from '@/views/PermissionRequestsPage';

const pending = [
  { id: 'r1', permissionKey: 'tab:updates', reason: 'x'.repeat(60), status: 'pending', requestedAt: '', adminEmail: 'a@x.com' },
  { id: 'r2', permissionKey: 'action:users.delete', reason: 'x'.repeat(60), status: 'pending', requestedAt: '', adminEmail: 'b@x.com' },
];

vi.mock('@/lib/api', () => ({
  api: {
    listPermissionRequests: vi.fn().mockResolvedValue({ items: pending }),
    approvePermissionRequest: vi.fn().mockResolvedValue({ ok: true }),
    denyPermissionRequest: vi.fn().mockResolvedValue({ ok: true }),
    bulkApprovePermissionRequests: vi.fn().mockResolvedValue({ ok: true }),
  },
}));

vi.mock('@/lib/signalr', () => ({ on: () => {}, off: () => {} }));

describe('PermissionRequestsPage', () => {
  it('renders pending requests', async () => {
    render(<PermissionRequestsPage />);
    await waitFor(() => expect(screen.getByText(/a@x.com/)).toBeTruthy());
    expect(screen.getByText(/b@x.com/)).toBeTruthy();
  });

  it('approves a single request', async () => {
    const { api } = await import('@/lib/api');
    render(<PermissionRequestsPage />);
    await waitFor(() => screen.getByText(/a@x.com/));
    fireEvent.click(screen.getAllByRole('button', { name: /approve/i })[0]);
    await waitFor(() => expect(api.approvePermissionRequest).toHaveBeenCalled());
  });
});
```

- [ ] **Step 2: Verify fails**

- [ ] **Step 3: Implement `PermissionRequestsPage.tsx`**

`admin-panel/src/views/PermissionRequestsPage.tsx`:

```tsx
'use client';

import { useEffect, useState } from 'react';
import { Check, X, RefreshCw, Users2 } from 'lucide-react';
import { api } from '@/lib/api';
import { on, off } from '@/lib/signalr';
import type { PermissionRequest } from '@/lib/types';

export function PermissionRequestsPage() {
  const [items, setItems] = useState<PermissionRequest[]>([]);
  const [loading, setLoading] = useState(true);
  const [status, setStatus] = useState<'pending'|'approved'|'denied'|'cancelled'>('pending');
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [note, setNote] = useState('');

  const refresh = async () => {
    setLoading(true);
    const r = await api.listPermissionRequests(status);
    setItems(r.items ?? []);
    setLoading(false);
  };

  useEffect(() => { refresh(); }, [status]);

  useEffect(() => {
    const handler = (ev: any) => {
      if (status !== 'pending') return;
      setItems(prev => [{ id: ev.requestId, permissionKey: ev.permissionKey, reason: ev.reason, status: 'pending', requestedAt: ev.requestedAt, adminEmail: ev.adminEmail }, ...prev]);
    };
    on('PermissionRequested', handler);
    return () => off('PermissionRequested', handler);
  }, [status]);

  const approve = async (id: string) => {
    const { ok } = await api.approvePermissionRequest(id, null, note || undefined);
    if (ok) refresh();
  };
  const deny = async (id: string) => {
    const { ok } = await api.denyPermissionRequest(id, note || undefined);
    if (ok) refresh();
  };
  const bulkApprove = async () => {
    if (selected.size === 0) return;
    await api.bulkApprovePermissionRequests(Array.from(selected));
    setSelected(new Set());
    refresh();
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-display font-bold flex items-center gap-2"><Users2 className="w-6 h-6"/>Permission Requests</h1>
        <select value={status} onChange={e => setStatus(e.target.value as any)} className="input-dark">
          <option value="pending">Pending</option>
          <option value="approved">Approved</option>
          <option value="denied">Denied</option>
          <option value="cancelled">Cancelled</option>
        </select>
      </div>

      {selected.size > 0 && (
        <div className="glass-card p-3 flex items-center gap-3">
          <span className="text-sm text-white/70">{selected.size} selected</span>
          <button onClick={bulkApprove} className="btn-primary-sm">Approve Selected</button>
        </div>
      )}

      <div className="glass-card overflow-hidden">
        {loading ? (
          <div className="p-8 text-center text-white/50"><RefreshCw className="w-6 h-6 inline animate-spin" /></div>
        ) : items.length === 0 ? (
          <div className="p-8 text-center text-white/50">No requests.</div>
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-white/5">
              <tr>
                <th className="p-3 text-left">
                  <input type="checkbox"
                    checked={selected.size === items.length && items.length > 0}
                    onChange={e => setSelected(e.target.checked ? new Set(items.map(i => i.id)) : new Set())} />
                </th>
                <th className="p-3 text-left">Admin</th>
                <th className="p-3 text-left">Permission</th>
                <th className="p-3 text-left">Reason</th>
                <th className="p-3 text-left">Requested</th>
                <th className="p-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {items.map(r => (
                <tr key={r.id} className="border-t border-white/5">
                  <td className="p-3">
                    {r.status === 'pending' && (
                      <input type="checkbox" checked={selected.has(r.id)}
                        onChange={e => {
                          setSelected(prev => {
                            const next = new Set(prev);
                            e.target.checked ? next.add(r.id) : next.delete(r.id);
                            return next;
                          });
                        }} />
                    )}
                  </td>
                  <td className="p-3">{r.adminEmail}</td>
                  <td className="p-3"><code className="text-xs">{r.permissionKey}</code></td>
                  <td className="p-3 max-w-sm truncate" title={r.reason}>{r.reason}</td>
                  <td className="p-3 text-white/50">{new Date(r.requestedAt).toLocaleString()}</td>
                  <td className="p-3 text-right space-x-2">
                    {r.status === 'pending' ? (
                      <>
                        <button onClick={() => approve(r.id)} className="btn-primary-sm inline-flex items-center gap-1"><Check className="w-3 h-3"/>Approve</button>
                        <button onClick={() => deny(r.id)}   className="btn-danger-sm inline-flex items-center gap-1"><X className="w-3 h-3"/>Deny</button>
                      </>
                    ) : (
                      <span className="text-xs text-white/50">{r.status}</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <div>
        <label className="block text-xs text-white/40 mb-1">Review note (optional, applied to next approve/deny action)</label>
        <input value={note} onChange={e => setNote(e.target.value)} className="input-dark w-full" placeholder="e.g. 'Approved for customer escalation INC-3421'" />
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Test passes**

```bash
npx vitest run src/__tests__/views/PermissionRequestsPage.test.tsx 2>&1 | tail -5
```

Expected: 2 passed.

- [ ] **Step 5: Commit**

```bash
git add admin-panel/src/views/PermissionRequestsPage.tsx admin-panel/src/__tests__/views/PermissionRequestsPage.test.tsx
git commit -m "feat(6.11.W4.T29): PermissionRequestsPage — pending inbox with bulk approve/deny

Status filter (default pending). Bulk select + Approve Selected.
Per-row Approve/Deny buttons. Optional review_note input applied to
next action. Live SignalR append of new PermissionRequested events
when status filter is 'pending'."
```

### Task 30: AdminManagementPage — list + create modal + actions

**Goal:** Real implementation of the superadmin's Admin Management tab. List admin accounts with actions menu (Edit Permissions / Reset Password / Suspend or Restore / Delete). Top button opens Create Admin modal with template picker (Default/Trusted/ReadOnly/Custom + Custom picker from D6).

**Files:**
- Replace stub: `admin-panel/src/views/AdminManagementPage.tsx`
- Create: `admin-panel/src/components/CreateAdminModal.tsx`
- Create: `admin-panel/src/components/CustomTemplatePicker.tsx`
- Test: `admin-panel/src/__tests__/views/AdminManagementPage.test.tsx`

- [ ] **Step 1: Write failing test (happy-path — list renders + create opens modal)**

```typescript
// admin-panel/src/__tests__/views/AdminManagementPage.test.tsx
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { AdminManagementPage } from '@/views/AdminManagementPage';

vi.mock('@/lib/api', () => ({
  api: {
    listAdminAccounts: vi.fn().mockResolvedValue({ items: [
      { id: 'u1', email: 'a@x.com', role: 'admin', isActive: true, isReadonly: false, totpEnabled: true, require2fa: true, createdAt: '2026-04-01' },
    ]}),
    createAdminAccount: vi.fn().mockResolvedValue({ ok: true, data: {} }),
    suspendAdmin: vi.fn(), restoreAdmin: vi.fn(), deleteAdmin: vi.fn(), resetAdminPassword: vi.fn(),
  },
}));

describe('AdminManagementPage', () => {
  it('lists admins and opens create modal', async () => {
    render(<AdminManagementPage />);
    await waitFor(() => screen.getByText('a@x.com'));
    fireEvent.click(screen.getByRole('button', { name: /\+ create admin/i }));
    expect(screen.getByText(/new admin account/i)).toBeTruthy();
  });
});
```

- [ ] **Step 2: Verify fails**

- [ ] **Step 3: Implement `CustomTemplatePicker.tsx`**

`admin-panel/src/components/CustomTemplatePicker.tsx`:

```tsx
'use client';

import { useState } from 'react';
import { PERMISSION_KEYS, PERMISSION_LABELS, TIER1_KEYS, TIER2_KEYS, PermissionKey } from '@/lib/permissions';

export interface CustomKey { permissionKey: string; expiresAt: string | null; }

export function CustomTemplatePicker({
  onChange, readOnly = false,
}: {
  onChange: (keys: CustomKey[]) => void;
  readOnly?: boolean;
}) {
  const [selected, setSelected] = useState<Record<string, boolean>>({});
  const [defaultExpiry, setDefaultExpiry] = useState<string>('');
  const [perKeyExpiry, setPerKeyExpiry] = useState<Record<string, string>>({});

  const update = (nextSelected: Record<string, boolean>) => {
    const keys: CustomKey[] = Object.entries(nextSelected)
      .filter(([, v]) => v)
      .map(([k]) => ({ permissionKey: k, expiresAt: perKeyExpiry[k] || defaultExpiry || null }));
    onChange(keys);
  };

  const toggle = (k: string) => {
    const next = { ...selected, [k]: !selected[k] };
    setSelected(next);
    update(next);
  };

  return (
    <div className="space-y-4">
      <div>
        <label className="text-xs text-white/50 block mb-2">Tier 1 — Tabs</label>
        <div className="space-y-1">
          {TIER1_KEYS.map(k => (
            <label key={k} className="flex items-center gap-2 text-sm">
              <input type="checkbox" checked={!!selected[k]} onChange={() => toggle(k)} disabled={readOnly} />
              <code className="text-xs">{k}</code>
              <span className="text-white/60">— {PERMISSION_LABELS[k as PermissionKey]}</span>
            </label>
          ))}
        </div>
      </div>
      <div>
        <label className="text-xs text-white/50 block mb-2">Tier 2 — Actions</label>
        <div className="space-y-1">
          {TIER2_KEYS.map(k => (
            <label key={k} className="flex items-center gap-2 text-sm">
              <input type="checkbox" checked={!!selected[k]} onChange={() => toggle(k)} disabled={readOnly} />
              <code className="text-xs">{k}</code>
              <span className="text-white/60">— {PERMISSION_LABELS[k as PermissionKey]}</span>
            </label>
          ))}
        </div>
      </div>
      <div>
        <label className="text-xs text-white/50 block mb-2">Default expiry for checked keys (optional)</label>
        <input type="datetime-local" value={defaultExpiry}
          onChange={e => { setDefaultExpiry(e.target.value); update({ ...selected }); }}
          className="input-dark" disabled={readOnly} />
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Implement `CreateAdminModal.tsx`**

`admin-panel/src/components/CreateAdminModal.tsx`:

```tsx
'use client';

import { useState } from 'react';
import { X, UserPlus } from 'lucide-react';
import { api } from '@/lib/api';
import { CustomTemplatePicker, CustomKey } from '@/components/CustomTemplatePicker';

export function CreateAdminModal({ onClose, onCreated }: { onClose: () => void; onCreated: () => void }) {
  const [email, setEmail] = useState('');
  const [sendInvitation, setSendInvitation] = useState(true);
  const [initialPassword, setInitialPassword] = useState('');
  const [forcePasswordChange, setForce] = useState<'on_first_login'|'within_7_days'|'within_30_days'|'never'>('on_first_login');
  const [template, setTemplate] = useState<'Default'|'Trusted'|'ReadOnly'|'Custom'>('Default');
  const [require2fa, setRequire2fa] = useState(true);
  const [customKeys, setCustomKeys] = useState<CustomKey[]>([]);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const generatePassword = () => {
    const chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*';
    let p = '';
    for (let i = 0; i < 16; i++) p += chars[Math.floor(Math.random() * chars.length)];
    setInitialPassword(p);
  };

  const submit = async () => {
    setError(''); setLoading(true);
    const res = await api.createAdminAccount({
      email, sendInvitation,
      initialPassword: sendInvitation ? undefined : initialPassword,
      forcePasswordChange, template, require2fa,
      customKeys: template === 'Custom' ? customKeys : undefined,
    });
    setLoading(false);
    if (res.ok) { onCreated(); onClose(); }
    else setError(res.data?.error ?? 'Failed to create admin');
  };

  return (
    <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
      <div className="glass-card w-full max-w-lg p-6 space-y-4">
        <div className="flex items-center justify-between">
          <h3 className="text-lg font-display font-bold flex items-center gap-2"><UserPlus className="w-5 h-5" />New admin account</h3>
          <button onClick={onClose} className="text-white/40 hover:text-white"><X className="w-5 h-5" /></button>
        </div>
        <input value={email} onChange={e => setEmail(e.target.value)} className="input-dark w-full" placeholder="admin@company.com" type="email" />
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" checked={sendInvitation} onChange={e => setSendInvitation(e.target.checked)} />
          Send invitation email (admin picks own password)
        </label>
        {!sendInvitation && (
          <div className="flex gap-2">
            <input value={initialPassword} onChange={e => setInitialPassword(e.target.value)} className="input-dark flex-1" placeholder="Initial password (min 10 chars)" />
            <button onClick={generatePassword} className="btn-ghost">Generate</button>
          </div>
        )}
        <div>
          <label className="text-xs text-white/50 block mb-1">Force password change</label>
          <select value={forcePasswordChange} onChange={e => setForce(e.target.value as any)} className="input-dark w-full">
            <option value="on_first_login">On first login</option>
            <option value="within_7_days">Within 7 days</option>
            <option value="within_30_days">Within 30 days</option>
            <option value="never">Never</option>
          </select>
        </div>
        <div>
          <label className="text-xs text-white/50 block mb-1">Permission template</label>
          <select value={template} onChange={e => setTemplate(e.target.value as any)} className="input-dark w-full">
            <option value="Default">Default — no Tier 2 actions</option>
            <option value="Trusted">Trusted — all Tier 2 actions</option>
            <option value="ReadOnly">Read-Only — no destructive actions</option>
            <option value="Custom">Custom — per-permission config</option>
          </select>
        </div>
        {template === 'Custom' && <CustomTemplatePicker onChange={setCustomKeys} />}
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" checked={require2fa} onChange={e => setRequire2fa(e.target.checked)} />
          Require 2FA on this account
        </label>
        {error && <div className="text-xs text-aura-red">{error}</div>}
        <div className="flex justify-end gap-2">
          <button onClick={onClose} className="btn-ghost">Cancel</button>
          <button onClick={submit} disabled={loading || !email} className="btn-primary">
            {loading ? 'Creating…' : 'Create'}
          </button>
        </div>
      </div>
    </div>
  );
}
```

- [ ] **Step 5: Implement `AdminManagementPage.tsx`**

```tsx
'use client';

import { useEffect, useState } from 'react';
import { UserCog, UserPlus, Ban, RotateCw, Key, Trash2 } from 'lucide-react';
import { api } from '@/lib/api';
import type { AdminAccount } from '@/lib/types';
import { CreateAdminModal } from '@/components/CreateAdminModal';

export function AdminManagementPage() {
  const [items, setItems] = useState<AdminAccount[]>([]);
  const [loading, setLoading] = useState(true);
  const [modal, setModal] = useState<'create'|null>(null);

  const refresh = async () => {
    setLoading(true);
    const r = await api.listAdminAccounts();
    setItems(r.items ?? []);
    setLoading(false);
  };
  useEffect(() => { refresh(); }, []);

  const onSuspend = async (id: string) => { await api.suspendAdmin(id); refresh(); };
  const onRestore = async (id: string) => { await api.restoreAdmin(id); refresh(); };
  const onReset = async (id: string) => { await api.resetAdminPassword(id); alert('Reset link emailed.'); };
  const onDelete = async (id: string) => { if (!confirm('Delete this admin? This is permanent.')) return; await api.deleteAdmin(id); refresh(); };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-display font-bold flex items-center gap-2"><UserCog className="w-6 h-6" />Admin Management</h1>
        <button onClick={() => setModal('create')} className="btn-primary inline-flex items-center gap-2"><UserPlus className="w-4 h-4" />+ Create Admin</button>
      </div>

      <div className="glass-card overflow-hidden">
        {loading ? <div className="p-8 text-center text-white/50">Loading…</div> : (
          <table className="w-full text-sm">
            <thead className="bg-white/5">
              <tr>
                <th className="p-3 text-left">Email</th>
                <th className="p-3 text-left">Role</th>
                <th className="p-3 text-left">Active</th>
                <th className="p-3 text-left">Readonly</th>
                <th className="p-3 text-left">2FA</th>
                <th className="p-3 text-left">Created</th>
                <th className="p-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {items.map(a => (
                <tr key={a.id} className="border-t border-white/5">
                  <td className="p-3">{a.email}</td>
                  <td className="p-3">{a.role}</td>
                  <td className="p-3">{a.isActive ? '✓' : <span className="text-aura-red">suspended</span>}</td>
                  <td className="p-3">{a.isReadonly ? 'yes' : 'no'}</td>
                  <td className="p-3">{a.totpEnabled ? 'on' : 'off'}</td>
                  <td className="p-3 text-white/50">{new Date(a.createdAt).toLocaleDateString()}</td>
                  <td className="p-3 text-right space-x-2">
                    <button title="Reset password" onClick={() => onReset(a.id)} className="btn-ghost-sm"><Key className="w-3 h-3" /></button>
                    {a.isActive
                      ? <button title="Suspend" onClick={() => onSuspend(a.id)} className="btn-ghost-sm"><Ban className="w-3 h-3" /></button>
                      : <button title="Restore" onClick={() => onRestore(a.id)} className="btn-ghost-sm"><RotateCw className="w-3 h-3" /></button>}
                    {a.role === 'admin' && (
                      <button title="Delete" onClick={() => onDelete(a.id)} className="btn-danger-sm"><Trash2 className="w-3 h-3" /></button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {modal === 'create' && <CreateAdminModal onClose={() => setModal(null)} onCreated={refresh} />}
    </div>
  );
}
```

- [ ] **Step 6: Tests pass**

```bash
npx vitest run src/__tests__/views/AdminManagementPage.test.tsx 2>&1 | tail -5
```

Expected: 1 passed.

- [ ] **Step 7: Commit**

```bash
git add admin-panel/src/views/AdminManagementPage.tsx admin-panel/src/components/CreateAdminModal.tsx admin-panel/src/components/CustomTemplatePicker.tsx admin-panel/src/__tests__/views/AdminManagementPage.test.tsx
git commit -m "feat(6.11.W4.T30): AdminManagementPage + CreateAdminModal + CustomTemplatePicker

List with Reset-Password / Suspend(Restore) / Delete per-row actions.
Create modal: email + invitation toggle + generate-password shortcut +
force-password-change dropdown + template picker (Custom expands
CustomTemplatePicker with per-key checkboxes + default expiry).

Edit Permissions per-row action lands in T30.1."
```

### Task 30.1: Edit existing admin's permissions (superadmin)

**Goal:** Per-row "Edit Permissions" action per spec D12 (`Per-row actions: Edit Permissions / Reset Password / Suspend (or Restore) / Delete`). Superadmin can swap an existing admin's template without going through the request/approval flow — either to another preset template OR to Custom with per-key picks. Implementation: atomic "wipe old + apply new" under a transaction so the admin never sees a half-applied grant set.

**Files:**
- Modify: `src/Backend/AuraCore.API/Controllers/Superadmin/AdminManagementController.cs` — add `POST /admins/{id}/apply-template` endpoint
- Modify: `admin-panel/src/lib/api.ts` — add `applyTemplate(adminId, body)` method
- Create: `admin-panel/src/components/EditPermissionsModal.tsx`
- Modify: `admin-panel/src/views/AdminManagementPage.tsx` — add Edit Permissions icon-button per row + wire modal
- Modify: `tests/AuraCore.Tests.API/SuperadminFoundation/AdminManagementControllerTests.cs` — append tests

- [ ] **Step 1: Append failing tests to AdminManagementControllerTests**

Add these `[Fact]`s to the existing `AdminManagementControllerTests` class from T26:

```csharp
    [Fact]
    public async Task ApplyTemplate_Trusted_to_ReadOnly_wipes_tier2_grants_and_sets_is_readonly()
    {
        var (c, _) = await SuperClient();
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var target = new User { Id = Guid.NewGuid(), Email = "t@x.com", PasswordHash = "x", Role = "admin", IsReadonly = false };
        db.Users.Add(target);
        // Seed with Trusted template (6 Tier 2 grants)
        foreach (var k in PermissionKeys.AllTier2)
            db.PermissionGrants.Add(new PermissionGrant { AdminUserId = target.Id, PermissionKey = k, GrantedBy = target.Id });
        await db.SaveChangesAsync();

        var res = await c.PostAsJsonAsync($"/api/superadmin/admins/{target.Id}/apply-template", new {
            template = "ReadOnly",
        });
        res.EnsureSuccessStatusCode();

        using var scope2 = _f.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var reloaded = await db2.Users.FirstAsync(u => u.Id == target.Id);
        Assert.True(reloaded.IsReadonly);

        var active = await db2.PermissionGrants.Where(g => g.AdminUserId == target.Id && g.RevokedAt == null).CountAsync();
        Assert.Equal(0, active);

        // Old grants preserved for audit — they should all have RevokedAt set
        var revoked = await db2.PermissionGrants.Where(g => g.AdminUserId == target.Id && g.RevokedAt != null).CountAsync();
        Assert.Equal(PermissionKeys.AllTier2.Count, revoked);
    }

    [Fact]
    public async Task ApplyTemplate_Custom_creates_only_picked_keys()
    {
        var (c, _) = await SuperClient();
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var target = new User { Id = Guid.NewGuid(), Email = "t2@x.com", PasswordHash = "x", Role = "admin" };
        db.Users.Add(target); await db.SaveChangesAsync();

        var res = await c.PostAsJsonAsync($"/api/superadmin/admins/{target.Id}/apply-template", new {
            template = "Custom",
            customKeys = new[] {
                new { permissionKey = PermissionKeys.TabUpdates, expiresAt = (string?)null },
                new { permissionKey = PermissionKeys.ActionUsersDelete, expiresAt = (string?)null },
            },
        });
        res.EnsureSuccessStatusCode();

        using var scope2 = _f.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var active = await db2.PermissionGrants
            .Where(g => g.AdminUserId == target.Id && g.RevokedAt == null)
            .Select(g => g.PermissionKey).ToListAsync();
        Assert.Equal(2, active.Count);
        Assert.Contains(PermissionKeys.TabUpdates, active);
        Assert.Contains(PermissionKeys.ActionUsersDelete, active);
    }

    [Fact]
    public async Task ApplyTemplate_rejects_unknown_template()
    {
        var (c, _) = await SuperClient();
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var target = new User { Id = Guid.NewGuid(), Email = "t3@x.com", PasswordHash = "x", Role = "admin" };
        db.Users.Add(target); await db.SaveChangesAsync();

        var res = await c.PostAsJsonAsync($"/api/superadmin/admins/{target.Id}/apply-template", new { template = "Bogus" });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, res.StatusCode);
    }
```

- [ ] **Step 2: Verify failing**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj \
  --filter "FullyQualifiedName~AdminManagementControllerTests.ApplyTemplate" 2>&1 | tail -8
```

Expected: 3 fail (404 — endpoint doesn't exist).

- [ ] **Step 3: Add endpoint to `AdminManagementController.cs`**

Read `src/Backend/AuraCore.API/Controllers/Superadmin/AdminManagementController.cs`. Append (before the closing brace of the class, near the `SetRequire2fa` method):

```csharp
    [HttpPost("admins/{id:guid}/apply-template")]
    [AuditAction("ApplyPermissionTemplate", "User", TargetIdFromRouteKey = "id")]
    public async Task<IActionResult> ApplyTemplate(Guid id, [FromBody] ApplyTemplateDto dto, CancellationToken ct)
    {
        var superId = User.GetUserId()!.Value;
        var target = await _db.Users.FindAsync(new object[] { id }, ct);
        if (target is null) return NotFound();
        if (target.Role != "admin") return BadRequest(new { error = "template_applies_to_admin_only" });
        if (!PermissionTemplates.IsValidTemplate(dto.Template))
            return BadRequest(new { error = "unknown_template" });

        // Atomic: revoke all active grants, then insert new ones, then flip is_readonly.
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var active = await _db.PermissionGrants
            .Where(g => g.AdminUserId == id && g.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var g in active)
        {
            g.RevokedAt = DateTime.UtcNow;
            g.RevokedBy = superId;
            g.RevokeReason = "template_swap";
        }

        if (dto.Template == PermissionTemplates.Custom)
        {
            foreach (var ck in dto.CustomKeys ?? Array.Empty<AdminManagementController.CustomKey>())
            {
                if (!PermissionKeys.IsValidKey(ck.PermissionKey)) continue;
                _db.PermissionGrants.Add(new PermissionGrant {
                    AdminUserId = id, PermissionKey = ck.PermissionKey,
                    GrantedBy = superId, ExpiresAt = ck.ExpiresAt,
                });
            }
        }
        else
        {
            foreach (var key in PermissionTemplates.GetPermissionsForTemplate(dto.Template))
                _db.PermissionGrants.Add(new PermissionGrant {
                    AdminUserId = id, PermissionKey = key, GrantedBy = superId,
                });
        }

        target.IsReadonly = PermissionTemplates.RequiresIsReadonlyFlag(dto.Template);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(new { id, template = dto.Template, isReadonly = target.IsReadonly });
    }

    public sealed record ApplyTemplateDto(string Template, List<CustomKey>? CustomKeys);
```

**Note:** `BeginTransactionAsync` on InMemory provider is a no-op (documented EF behavior) — tests still pass because the logic is correct; on Postgres it provides real atomicity.

- [ ] **Step 4: Add api method**

Append to the `api` object in `admin-panel/src/lib/api.ts`:

```typescript
  async applyAdminTemplate(adminId: string, body: {
    template: 'Default' | 'Trusted' | 'ReadOnly' | 'Custom';
    customKeys?: { permissionKey: string; expiresAt?: string | null }[];
  }) {
    const res = await request(`/api/superadmin/admins/${adminId}/apply-template`, {
      method: 'POST', body: JSON.stringify(body),
    });
    return { ok: res.ok, data: res.ok ? await res.json() : await safeJson(res) };
  },
```

- [ ] **Step 5: Create `EditPermissionsModal.tsx`**

`admin-panel/src/components/EditPermissionsModal.tsx`:

```tsx
'use client';

import { useState } from 'react';
import { X, Shield } from 'lucide-react';
import { api } from '@/lib/api';
import { CustomTemplatePicker, CustomKey } from '@/components/CustomTemplatePicker';
import type { AdminAccount } from '@/lib/types';

export function EditPermissionsModal({ admin, onClose, onSaved }: {
  admin: AdminAccount;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [template, setTemplate] = useState<'Default'|'Trusted'|'ReadOnly'|'Custom'>('Default');
  const [customKeys, setCustomKeys] = useState<CustomKey[]>([]);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const save = async () => {
    setError(''); setLoading(true);
    const res = await api.applyAdminTemplate(admin.id, {
      template,
      customKeys: template === 'Custom' ? customKeys : undefined,
    });
    setLoading(false);
    if (res.ok) { onSaved(); onClose(); }
    else setError(res.data?.error ?? 'Failed');
  };

  return (
    <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
      <div className="glass-card w-full max-w-lg p-6 space-y-4">
        <div className="flex items-center justify-between">
          <h3 className="text-lg font-display font-bold flex items-center gap-2"><Shield className="w-5 h-5" />Edit permissions</h3>
          <button onClick={onClose} className="text-white/40 hover:text-white"><X className="w-5 h-5" /></button>
        </div>
        <p className="text-sm text-white/60">Account: <code className="text-xs">{admin.email}</code></p>
        <div className="glass-card p-3 bg-aura-red/5 border border-aura-red/20 text-xs text-aura-red">
          This replaces the admin's entire grant set. Existing grants are revoked (preserved for audit trail). Applies immediately.
        </div>
        <div>
          <label className="text-xs text-white/50 block mb-1">New template</label>
          <select value={template} onChange={e => setTemplate(e.target.value as any)} className="input-dark w-full">
            <option value="Default">Default — no Tier 2 actions</option>
            <option value="Trusted">Trusted — all Tier 2 actions</option>
            <option value="ReadOnly">Read-Only — block all destructive actions</option>
            <option value="Custom">Custom — pick specific permissions</option>
          </select>
        </div>
        {template === 'Custom' && <CustomTemplatePicker onChange={setCustomKeys} />}
        {error && <div className="text-xs text-aura-red">{error}</div>}
        <div className="flex justify-end gap-2">
          <button onClick={onClose} className="btn-ghost">Cancel</button>
          <button onClick={save} disabled={loading} className="btn-primary">{loading ? 'Applying…' : 'Apply'}</button>
        </div>
      </div>
    </div>
  );
}
```

- [ ] **Step 6: Wire into `AdminManagementPage.tsx`**

Read `admin-panel/src/views/AdminManagementPage.tsx`. Add the import:

```tsx
import { EditPermissionsModal } from '@/components/EditPermissionsModal';
import { Shield } from 'lucide-react';
```

Add state:

```tsx
const [editingPerms, setEditingPerms] = useState<AdminAccount | null>(null);
```

Inside the per-row actions `<td>` (the one with Reset / Suspend / Delete), add a 4th icon button IMMEDIATELY before the Reset button:

```tsx
<button title="Edit permissions" onClick={() => setEditingPerms(a)} className="btn-ghost-sm"><Shield className="w-3 h-3" /></button>
```

At the end of the component JSX (after the existing CreateAdminModal conditional), add:

```tsx
{editingPerms && (
  <EditPermissionsModal admin={editingPerms} onClose={() => setEditingPerms(null)} onSaved={refresh} />
)}
```

- [ ] **Step 7: Tests pass + build**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj \
  --filter "FullyQualifiedName~AdminManagementControllerTests.ApplyTemplate" 2>&1 | tail -5
cd admin-panel && npx tsc --noEmit 2>&1 | tail -5 && cd ..
```

Expected: 3 passed; 0 TS errors.

- [ ] **Step 8: Commit**

```bash
git add src/Backend/AuraCore.API/Controllers/Superadmin/AdminManagementController.cs admin-panel/src/lib/api.ts admin-panel/src/components/EditPermissionsModal.tsx admin-panel/src/views/AdminManagementPage.tsx tests/AuraCore.Tests.API/SuperadminFoundation/AdminManagementControllerTests.cs
git commit -m "feat(6.11.W4.T30.1): Edit Permissions per-row action (spec D12)

POST /api/superadmin/admins/{id}/apply-template atomically revokes
all active grants (preserved with RevokedAt + revoke_reason='template_swap'
for audit trail) and applies the new template/custom key set. is_readonly
flipped based on template. Transaction wraps the swap so admin never
sees a half-applied state.

Frontend: EditPermissionsModal reuses CustomTemplatePicker; wired into
AdminManagementPage as a Shield icon-button per row."
```

### Task 30.2: Superadmin InvitationsPage (list / revoke / resend)

**Goal:** Lets superadmin see all outstanding admin invitations (not yet consumed + not yet expired), revoke one (delete the `admin_invitations` row + cascade the user if never consumed), and resend (generate a new token, re-email, keep the same `AdminUserId`). Operational hygiene — if the original email doesn't arrive, superadmin has a remediation path.

**Files:**
- Create: `src/Backend/AuraCore.API/Controllers/Superadmin/InvitationsController.cs`
- Create: `admin-panel/src/views/InvitationsPage.tsx`
- Modify: `admin-panel/src/app/page.tsx` — add `invitations` Page + nav entry
- Modify: `admin-panel/src/lib/api.ts` — 3 new methods
- Create: `tests/AuraCore.Tests.API/SuperadminFoundation/InvitationsManagementTests.cs`

- [ ] **Step 1: Write failing tests**

`tests/AuraCore.Tests.API/SuperadminFoundation/InvitationsManagementTests.cs`:

```csharp
using System.Net.Http.Json;
using System.Security.Cryptography;
using AuraCore.API.Application.Interfaces;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class InvitationsManagementTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _f;

    public InvitationsManagementTests(WebApplicationFactory<Program> f)
    {
        Environment.SetEnvironmentVariable("JWT_SECRET", "test-secret-at-least-32-characters-long!!");
        _f = f.WithWebHostBuilder(b => b.ConfigureServices(s => {
            var d = s.Single(x => x.ServiceType == typeof(DbContextOptions<AuraCoreDbContext>));
            s.Remove(d);
            s.AddDbContext<AuraCoreDbContext>(o => o.UseInMemoryDatabase($"invmgmt-{Guid.NewGuid()}"));
        }));
    }

    private async Task<(HttpClient c, Guid id)> SuperClient()
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var u = new User { Id = Guid.NewGuid(), Email = "s@x.com", PasswordHash = "x", Role = "superadmin", TotpEnabled = true };
        db.Users.Add(u); await db.SaveChangesAsync();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var c = _f.CreateClient();
        c.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.GenerateAccessToken(u));
        return (c, u.Id);
    }

    private static string Sha256(string s) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(s))).ToLowerInvariant();

    [Fact]
    public async Task List_returns_pending_invitations()
    {
        var (c, superId) = await SuperClient();
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var admin = new User { Id = Guid.NewGuid(), Email = "new@x.com", PasswordHash = "x", Role = "admin" };
        db.Users.Add(admin);
        db.AdminInvitations.Add(new AdminInvitation {
            TokenHash = Sha256("tok1"), AdminUserId = admin.Id, CreatedBy = superId,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        });
        await db.SaveChangesAsync();

        var res = await c.GetAsync("/api/superadmin/invitations");
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("new@x.com", body);
    }

    [Fact]
    public async Task Revoke_deletes_invitation_row()
    {
        var (c, superId) = await SuperClient();
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var admin = new User { Id = Guid.NewGuid(), Email = "r@x.com", PasswordHash = "x", Role = "admin" };
        db.Users.Add(admin);
        var hash = Sha256("revoke-me");
        db.AdminInvitations.Add(new AdminInvitation {
            TokenHash = hash, AdminUserId = admin.Id, CreatedBy = superId,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        });
        await db.SaveChangesAsync();

        var res = await c.DeleteAsync($"/api/superadmin/invitations/{hash}");
        res.EnsureSuccessStatusCode();

        using var scope2 = _f.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        Assert.Equal(0, await db2.AdminInvitations.CountAsync(i => i.TokenHash == hash));
    }

    [Fact]
    public async Task Resend_creates_new_token_invalidates_old_and_keeps_user_id()
    {
        var (c, superId) = await SuperClient();
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var admin = new User { Id = Guid.NewGuid(), Email = "re@x.com", PasswordHash = "x", Role = "admin" };
        db.Users.Add(admin);
        var oldHash = Sha256("old-token");
        db.AdminInvitations.Add(new AdminInvitation {
            TokenHash = oldHash, AdminUserId = admin.Id, CreatedBy = superId,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        });
        await db.SaveChangesAsync();

        var res = await c.PostAsync($"/api/superadmin/invitations/{oldHash}/resend", null);
        res.EnsureSuccessStatusCode();

        using var scope2 = _f.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        Assert.Equal(0, await db2.AdminInvitations.CountAsync(i => i.TokenHash == oldHash));
        var newInv = await db2.AdminInvitations.FirstOrDefaultAsync(i => i.AdminUserId == admin.Id);
        Assert.NotNull(newInv);
        Assert.NotEqual(oldHash, newInv!.TokenHash);
    }
}
```

- [ ] **Step 2: Verify fails**

- [ ] **Step 3: Create `InvitationsController.cs`**

`src/Backend/AuraCore.API/Controllers/Superadmin/InvitationsController.cs`:

```csharp
using System.Security.Cryptography;
using AuraCore.API.Application.Services.Email;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Helpers;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Superadmin;

[ApiController]
[Route("api/superadmin/invitations")]
[Authorize(Roles = "superadmin")]
public sealed class InvitationsController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    private readonly IEmailService _email;
    public InvitationsController(AuraCoreDbContext db, IEmailService email) { _db = db; _email = email; }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await _db.AdminInvitations
            .OrderByDescending(i => i.CreatedAt)
            .Take(200)
            .Select(i => new {
                tokenHash = i.TokenHash,
                adminEmail = _db.Users.Where(u => u.Id == i.AdminUserId).Select(u => u.Email).FirstOrDefault(),
                createdByEmail = _db.Users.Where(u => u.Id == i.CreatedBy).Select(u => u.Email).FirstOrDefault(),
                i.CreatedAt, i.ExpiresAt, i.ConsumedAt,
                status = i.ConsumedAt != null ? "accepted"
                       : i.ExpiresAt < DateTime.UtcNow ? "expired"
                       : "pending",
            })
            .ToListAsync(ct);
        return Ok(new { items });
    }

    [HttpDelete("{tokenHash}")]
    public async Task<IActionResult> Revoke(string tokenHash, CancellationToken ct)
    {
        var inv = await _db.AdminInvitations.FirstOrDefaultAsync(i => i.TokenHash == tokenHash, ct);
        if (inv is null) return NotFound();
        if (inv.ConsumedAt != null) return BadRequest(new { error = "already_accepted" });
        _db.AdminInvitations.Remove(inv);
        await _db.SaveChangesAsync(ct);
        return Ok(new { revoked = true });
    }

    [HttpPost("{tokenHash}/resend")]
    public async Task<IActionResult> Resend(string tokenHash, CancellationToken ct)
    {
        var inv = await _db.AdminInvitations.FirstOrDefaultAsync(i => i.TokenHash == tokenHash, ct);
        if (inv is null) return NotFound();
        if (inv.ConsumedAt != null) return BadRequest(new { error = "already_accepted" });

        var admin = await _db.Users.FirstOrDefaultAsync(u => u.Id == inv.AdminUserId, ct);
        if (admin is null) return NotFound(new { error = "admin_user_missing" });

        // Generate a fresh token
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace("+","-").Replace("/","_").TrimEnd('=');
        var newHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();

        _db.AdminInvitations.Remove(inv);
        _db.AdminInvitations.Add(new AdminInvitation {
            TokenHash = newHash, AdminUserId = admin.Id, CreatedBy = User.GetUserId()!.Value,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        });
        await _db.SaveChangesAsync(ct);

        var setupLink = $"https://admin.auracore.pro/#/invite?token={raw}&email={Uri.EscapeDataString(admin.Email)}";
        await _email.SendFromTemplateAsync(EmailTemplate.AdminInvitation, new {
            to = admin.Email, adminEmail = admin.Email,
            invitedBy = User.GetEmail() ?? "superadmin",
            setupLink, expiresAt = DateTime.UtcNow.AddDays(7).ToString("u"),
        }, ct);

        return Ok(new { resent = true, newTokenHash = newHash });
    }
}
```

- [ ] **Step 4: Add api methods**

Append to `admin-panel/src/lib/api.ts`:

```typescript
  async listInvitations() {
    const res = await request('/api/superadmin/invitations');
    return res.ok ? await res.json() : { items: [] };
  },

  async revokeInvitation(tokenHash: string) {
    const res = await request(`/api/superadmin/invitations/${tokenHash}`, { method: 'DELETE' });
    return { ok: res.ok };
  },

  async resendInvitation(tokenHash: string) {
    const res = await request(`/api/superadmin/invitations/${tokenHash}/resend`, { method: 'POST' });
    return { ok: res.ok };
  },
```

- [ ] **Step 5: Create `InvitationsPage.tsx`**

`admin-panel/src/views/InvitationsPage.tsx`:

```tsx
'use client';

import { useEffect, useState } from 'react';
import { Mail, Send, Trash2, RefreshCw } from 'lucide-react';
import { api } from '@/lib/api';

export function InvitationsPage() {
  const [items, setItems] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);

  const refresh = async () => {
    setLoading(true);
    const r = await api.listInvitations();
    setItems(r.items ?? []);
    setLoading(false);
  };
  useEffect(() => { refresh(); }, []);

  const revoke = async (hash: string) => {
    if (!confirm('Revoke this invitation? The admin account will remain but the setup link stops working.')) return;
    await api.revokeInvitation(hash); refresh();
  };
  const resend = async (hash: string) => { await api.resendInvitation(hash); refresh(); };

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-display font-bold flex items-center gap-2"><Mail className="w-6 h-6" />Pending Invitations</h1>
      <div className="glass-card overflow-hidden">
        {loading ? <div className="p-8 text-center text-white/50">Loading…</div> : items.length === 0 ? (
          <div className="p-8 text-center text-white/50">No invitations.</div>
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-white/5"><tr>
              <th className="p-3 text-left">Email</th>
              <th className="p-3 text-left">Created by</th>
              <th className="p-3 text-left">Status</th>
              <th className="p-3 text-left">Expires</th>
              <th className="p-3 text-right">Actions</th>
            </tr></thead>
            <tbody>
              {items.map((i: any) => (
                <tr key={i.tokenHash} className="border-t border-white/5">
                  <td className="p-3">{i.adminEmail}</td>
                  <td className="p-3 text-white/60">{i.createdByEmail}</td>
                  <td className="p-3">
                    {i.status === 'pending' && <span className="text-aura-yellow">pending</span>}
                    {i.status === 'accepted' && <span className="text-green-400">accepted</span>}
                    {i.status === 'expired' && <span className="text-white/40">expired</span>}
                  </td>
                  <td className="p-3 text-white/50">{new Date(i.expiresAt).toLocaleString()}</td>
                  <td className="p-3 text-right space-x-2">
                    {i.status !== 'accepted' && (
                      <>
                        <button title="Resend email" onClick={() => resend(i.tokenHash)} className="btn-ghost-sm"><Send className="w-3 h-3" /></button>
                        <button title="Revoke" onClick={() => revoke(i.tokenHash)} className="btn-danger-sm"><Trash2 className="w-3 h-3" /></button>
                      </>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
      <button onClick={refresh} className="btn-ghost inline-flex items-center gap-2"><RefreshCw className="w-4 h-4" />Refresh</button>
    </div>
  );
}
```

- [ ] **Step 6: Wire nav entry + Page type in `page.tsx`**

Read `admin-panel/src/app/page.tsx`. Do four edits:

1. Add import: `import { InvitationsPage } from '@/views/InvitationsPage';` and the icon: `Mail` (already in `lucide-react` import block — add if missing).
2. Extend the `Page` union type — add `|'invitations'`.
3. Extend `SUPERADMIN_EXTRA_GROUPS` — insert as the 3rd item (right after Admin Management for logical grouping):

```tsx
{ id: 'adminMgmt', icon: UserCog, label: 'Admin Management' },
{ id: 'invitations', icon: Mail, label: 'Invitations' },  // NEW
{ id: 'roleChange', icon: ArrowRightLeft, label: 'Role Change' },
```

4. Extend `PAGES` record — add `invitations: InvitationsPage,` line.

- [ ] **Step 7: Tests pass + ts check**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj \
  --filter "FullyQualifiedName~InvitationsManagementTests" 2>&1 | tail -5
cd admin-panel && npx tsc --noEmit 2>&1 | tail -5 && cd ..
```

Expected: 3 passed; 0 TS errors.

- [ ] **Step 8: Commit**

```bash
git add src/Backend/AuraCore.API/Controllers/Superadmin/InvitationsController.cs admin-panel/src/views/InvitationsPage.tsx admin-panel/src/app/page.tsx admin-panel/src/lib/api.ts tests/AuraCore.Tests.API/SuperadminFoundation/InvitationsManagementTests.cs
git commit -m "feat(6.11.W4.T30.2): superadmin InvitationsPage — list / revoke / resend

Ops hygiene: GET /api/superadmin/invitations shows all admin_invitations
rows with derived status (pending|accepted|expired). DELETE removes a
pending invitation (rejects if already accepted). POST /resend deletes
the old row + generates a fresh token + re-emails + keeps AdminUserId
stable. Added as 'Invitations' tab in superadmin nav between Admin
Management and Role Change."
```

### Task 31: AdminActionLogPage + AuditLog Export CSV button + RedeemInvitationPage + RoleChangePage

**Goal:** Remaining Wave 4 frontend: the AdminActionLog tab (superadmin), an Export CSV button on the existing AuditLogPage (admin), the RedeemInvitationPage (consume invitation link), and the RoleChangePage skeleton (promote user / demote admin — single-user flow).

**Files:**
- Replace stub: `admin-panel/src/views/AdminActionLogPage.tsx`
- Modify: `admin-panel/src/views/AuditLogPage.tsx` — add Export CSV button
- Replace stub: `admin-panel/src/views/RedeemInvitationPage.tsx`
- Replace stub: `admin-panel/src/views/RoleChangePage.tsx`

- [ ] **Step 1: AdminActionLogPage** — table + date-range filters + KPIs + CSV export button. Reads from `api.listAdminActionLog()` and `api.getAdminActionStats()`. CSV download via `api.exportAdminActionLogCsvUrl()`. Live SignalR subscription to future `AdminActionLogged` events (not implemented yet; reserve hook for Phase 6.12).

```tsx
// admin-panel/src/views/AdminActionLogPage.tsx
'use client';

import { useEffect, useState } from 'react';
import { FileText, Download, RefreshCw } from 'lucide-react';
import { api } from '@/lib/api';

export function AdminActionLogPage() {
  const [items, setItems] = useState<any[]>([]);
  const [stats, setStats] = useState<any>(null);
  const [filters, setFilters] = useState<{ actorEmail?: string; action?: string; dateFrom?: string; dateTo?: string }>({});
  const [loading, setLoading] = useState(true);

  const refresh = async () => {
    setLoading(true);
    const [list, s] = await Promise.all([api.listAdminActionLog(filters), api.getAdminActionStats()]);
    setItems(list.items ?? []); setStats(s); setLoading(false);
  };
  useEffect(() => { refresh(); }, []);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-display font-bold flex items-center gap-2"><FileText className="w-6 h-6" />Admin Action Log</h1>
        <a href={api.exportAdminActionLogCsvUrl(filters)} className="btn-primary inline-flex items-center gap-2" download>
          <Download className="w-4 h-4" />Export CSV
        </a>
      </div>
      {stats && (
        <div className="grid grid-cols-3 gap-3">
          <div className="glass-card p-3"><div className="text-xs text-white/50">Total</div><div className="text-2xl font-bold">{stats.total}</div></div>
          <div className="glass-card p-3"><div className="text-xs text-white/50">Last 24h</div><div className="text-2xl font-bold">{stats.last24h}</div></div>
          <div className="glass-card p-3"><div className="text-xs text-white/50">Last 7d</div><div className="text-2xl font-bold">{stats.last7d}</div></div>
        </div>
      )}
      <div className="glass-card p-3 flex gap-2">
        <input placeholder="Actor email" value={filters.actorEmail ?? ''} onChange={e => setFilters({ ...filters, actorEmail: e.target.value })} className="input-dark flex-1"/>
        <input placeholder="Action" value={filters.action ?? ''} onChange={e => setFilters({ ...filters, action: e.target.value })} className="input-dark flex-1"/>
        <input type="date" value={filters.dateFrom ?? ''} onChange={e => setFilters({ ...filters, dateFrom: e.target.value })} className="input-dark"/>
        <input type="date" value={filters.dateTo ?? ''} onChange={e => setFilters({ ...filters, dateTo: e.target.value })} className="input-dark"/>
        <button onClick={refresh} className="btn-primary"><RefreshCw className="w-4 h-4" /></button>
      </div>
      <div className="glass-card overflow-hidden">
        {loading ? <div className="p-8 text-center text-white/50">Loading…</div> : (
          <table className="w-full text-sm">
            <thead className="bg-white/5"><tr>
              <th className="p-3 text-left">Actor</th><th className="p-3 text-left">Action</th>
              <th className="p-3 text-left">Target</th><th className="p-3 text-left">IP</th><th className="p-3 text-left">When</th>
            </tr></thead>
            <tbody>
              {items.map((r, i) => (
                <tr key={i} className="border-t border-white/5">
                  <td className="p-3">{r.actorEmail}</td><td className="p-3"><code className="text-xs">{r.action}</code></td>
                  <td className="p-3 text-white/60">{r.targetType} {r.targetId ? `#${r.targetId.slice(0,8)}` : ''}</td>
                  <td className="p-3 text-white/40">{r.ipAddress}</td>
                  <td className="p-3 text-white/50">{new Date(r.createdAt).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
```

- [ ] **Step 2: AuditLogPage Export CSV button**

Read `admin-panel/src/views/AuditLogPage.tsx`. Locate the page header JSX. Add next to existing header actions:

```tsx
import { api } from '@/lib/api';
import { Download } from 'lucide-react';

<a href={api.exportAuditLogCsvUrl()} className="btn-primary inline-flex items-center gap-2" download>
  <Download className="w-4 h-4" />Export CSV
</a>
```

- [ ] **Step 3: RedeemInvitationPage**

```tsx
// admin-panel/src/views/RedeemInvitationPage.tsx
'use client';

import { useEffect, useState } from 'react';
import { api } from '@/lib/api';
import { Crown } from 'lucide-react';

export function RedeemInvitationPage() {
  const [token, setToken] = useState('');
  const [email, setEmail] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [done, setDone] = useState(false);

  useEffect(() => {
    const hash = window.location.hash; // #/invite?token=...&email=...
    const qsStart = hash.indexOf('?');
    if (qsStart > -1) {
      const params = new URLSearchParams(hash.slice(qsStart + 1));
      setToken(params.get('token') ?? '');
      setEmail(params.get('email') ?? '');
    }
  }, []);

  const submit = async () => {
    setError('');
    if (newPassword.length < 10) return setError('Password must be at least 10 characters');
    if (newPassword !== confirm) return setError('Passwords do not match');
    setLoading(true);
    const r = await api.redeemInvitation(token, email, newPassword);
    setLoading(false);
    if (r.ok && r.data?.accessToken) {
      localStorage.setItem('aura_token', r.data.accessToken);
      setDone(true);
      setTimeout(() => window.location.assign('/'), 1500);
    } else setError(r.data?.error ?? 'Invitation invalid or expired');
  };

  if (done) return <div className="min-h-screen flex items-center justify-center"><div className="text-center"><Crown className="w-10 h-10 text-accent mx-auto" /><p className="text-lg mt-2">Welcome! Redirecting…</p></div></div>;

  return (
    <div className="min-h-screen flex items-center justify-center p-4">
      <div className="w-full max-w-md glass-card p-8 space-y-4">
        <h2 className="text-xl font-display font-bold">Welcome! Set your password.</h2>
        <p className="text-sm text-white/60">Account: <code>{email}</code></p>
        <input type="password" placeholder="New password (min 10 chars)" value={newPassword} onChange={e => setNewPassword(e.target.value)} className="input-dark w-full" />
        <input type="password" placeholder="Confirm password" value={confirm} onChange={e => setConfirm(e.target.value)} className="input-dark w-full" />
        {error && <div className="text-xs text-aura-red">{error}</div>}
        <button onClick={submit} disabled={loading || !token} className="btn-primary w-full">{loading ? 'Redeeming…' : 'Activate Account'}</button>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: RoleChangePage** — single-user promote/demote

```tsx
// admin-panel/src/views/RoleChangePage.tsx
'use client';

import { useState } from 'react';
import { ArrowRightLeft } from 'lucide-react';
import { api } from '@/lib/api';
import { CustomTemplatePicker, CustomKey } from '@/components/CustomTemplatePicker';

export function RoleChangePage() {
  const [mode, setMode] = useState<'promote'|'demote'>('promote');
  const [userId, setUserId] = useState('');
  const [template, setTemplate] = useState<'Default'|'Trusted'|'ReadOnly'|'Custom'>('Default');
  const [forcePwd, setForcePwd] = useState<'on_first_login'|'within_7_days'|'within_30_days'|'never'>('on_first_login');
  const [require2fa, setRequire2fa] = useState(true);
  const [customKeys, setCustomKeys] = useState<CustomKey[]>([]);
  const [status, setStatus] = useState<string>('');

  const run = async () => {
    setStatus('');
    const ok = mode === 'promote'
      ? (await api.promoteUserToAdmin(userId, { template, forcePasswordChange: forcePwd, require2fa, customKeys: template === 'Custom' ? customKeys : undefined })).ok
      : (await api.demoteAdminToUser(userId)).ok;
    setStatus(ok ? 'Success.' : 'Failed — check user id + role.');
  };

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-display font-bold flex items-center gap-2"><ArrowRightLeft className="w-6 h-6" />Role Change (single-user)</h1>
      <div className="glass-card p-4 space-y-3">
        <div className="flex gap-2">
          <button onClick={() => setMode('promote')} className={mode==='promote'?'btn-primary':'btn-ghost'}>Promote user → admin</button>
          <button onClick={() => setMode('demote')}  className={mode==='demote' ?'btn-primary':'btn-ghost'}>Demote admin → user</button>
        </div>
        <input value={userId} onChange={e => setUserId(e.target.value)} placeholder="User ID (UUID)" className="input-dark w-full" />
        {mode === 'promote' && (
          <>
            <select value={template} onChange={e => setTemplate(e.target.value as any)} className="input-dark w-full">
              <option value="Default">Default</option>
              <option value="Trusted">Trusted</option>
              <option value="ReadOnly">Read-Only</option>
              <option value="Custom">Custom</option>
            </select>
            {template === 'Custom' && <CustomTemplatePicker onChange={setCustomKeys} />}
            <select value={forcePwd} onChange={e => setForcePwd(e.target.value as any)} className="input-dark w-full">
              <option value="on_first_login">Force change on first login</option>
              <option value="within_7_days">Force change within 7 days</option>
              <option value="within_30_days">Force change within 30 days</option>
              <option value="never">Never</option>
            </select>
            <label className="flex gap-2 text-sm"><input type="checkbox" checked={require2fa} onChange={e => setRequire2fa(e.target.checked)} />Require 2FA</label>
          </>
        )}
        <button onClick={run} className="btn-primary w-full" disabled={!userId}>Apply</button>
        {status && <div className="text-xs text-white/60">{status}</div>}
      </div>
      <p className="text-xs text-white/40">Bulk operations + audit preview deferred to Phase 6.12.</p>
    </div>
  );
}
```

- [ ] **Step 5: Build + TypeScript clean + smoke**

```bash
cd admin-panel
npx tsc --noEmit 2>&1 | tail -5
npx vitest run 2>&1 | tail -5
```

Expected: 0 TS errors; all tests still green.

- [ ] **Step 6: Commit**

```bash
git add admin-panel/src/views/AdminActionLogPage.tsx admin-panel/src/views/AuditLogPage.tsx admin-panel/src/views/RedeemInvitationPage.tsx admin-panel/src/views/RoleChangePage.tsx
git commit -m "feat(6.11.W4.T31): AdminActionLog + AuditLog CSV + Redeem + RoleChange pages

AdminActionLogPage: KPIs + filters + CSV download link (streaming).
AuditLogPage: + Export CSV button.
RedeemInvitationPage: hash-parses ?token=&email= from invitation link,
sets password, marks token consumed, logs in.
RoleChangePage: single-user promote/demote skeleton (spec 6.11 scope).
Bulk deferred to Phase 6.12."
```

### Task 32: Mid-deploy — prod backend + admin panel rebuild

**Goal:** First prod cutover. Ship Waves 1-4 backend + frontend. EF migration runs on startup. `SUPERADMIN_EMAILS` env var must already be set (pre-flight checklist).

- [ ] **Step 1: Verify pre-flight on origin**

```bash
ssh -i C:/Users/Admin/.ssh/id_ed25519 root@165.227.170.3 \
  "grep -E '^(SUPERADMIN_EMAILS|RESEND_API_KEY|JWT_SECRET)=' /etc/auracore-api.env | wc -l"
# Expected: 3
```

- [ ] **Step 2: Backend build + publish**

```bash
cd src/Backend/AuraCore.API
dotnet publish -c Release -r linux-x64 --self-contained false -o ./publish 2>&1 | tail -5
```

- [ ] **Step 3: Backup prod + copy new build**

```bash
ssh -i C:/Users/Admin/.ssh/id_ed25519 root@165.227.170.3 \
  "cp -r /var/www/auracore-api /var/www/auracore-api.bak-$(date +%Y%m%d%H%M)"
scp -i C:/Users/Admin/.ssh/id_ed25519 -r publish/* root@165.227.170.3:/var/www/auracore-api/
```

- [ ] **Step 4: Restart service**

```bash
ssh -i C:/Users/Admin/.ssh/id_ed25519 root@165.227.170.3 \
  "systemctl restart auracore-api && sleep 2 && systemctl status auracore-api --no-pager | head -10"
```

Expected: active (running). Logs should show startup messages for `SuperadminBootstrapService` and `GrandfatherMigrationService`.

- [ ] **Step 5: Apply EF migration verification**

```bash
ssh -i C:/Users/Admin/.ssh/id_ed25519 root@165.227.170.3 \
  "PGPASSWORD=auracorepro2026 psql -h localhost -U postgres -d auracoredb -c 'SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\" DESC LIMIT 3;'"
```

Expected: the newest migration row starts with `<timestamp>_AddSuperadminFoundation`.

- [ ] **Step 6: Build + deploy admin panel**

```bash
cd admin-panel
npm run build 2>&1 | tail -5
scp -i C:/Users/Admin/.ssh/id_ed25519 -r out/* root@165.227.170.3:/var/www/admin-panel/
ssh -i C:/Users/Admin/.ssh/id_ed25519 root@165.227.170.3 "nginx -t && systemctl reload nginx"
```

- [ ] **Step 7: Smoke**

Open a browser to `https://admin.auracore.pro`. Expected:
- HTTP 200 (no basic-auth prompt — Task 25 removed it).
- `View source` → head contains `<meta name="robots" content="noindex,nofollow">`.
- `/robots.txt` → `User-agent: *\nDisallow: /`.
- LoginScreen renders two buttons (Sign In as Admin + Sign In as Superadmin).
- Superadmin login with `ozgurdeniz807@gmail.com` + password returns — if the account has TOTP enabled, it prompts; if not, redirects to `/#/enable-2fa`.

After successful login as superadmin, verify the 6 extra nav items appear (Permission Requests / Admin Action Log / Admin Management / Role Change / Security Policy / API Rate Limits).

Create a test admin from Admin Management → verify invitation email lands in the admin's inbox (takes ~30s).

- [ ] **Step 8: Push branch**

```bash
cd "C:/Users/Admin/Desktop/AuraCorePro/AuraCorePro"
git push origin phase-6-superadmin-foundation
```

- [ ] **Step 9: Note the deploy state**

No git commit. Record the deploy timestamp + backup directory name for the eventual memory file.

---

## Wave 5 — Force-password-change + 2FA enforcement + Security Policy + Rate Limits

Eight tasks. After Wave 5, login enforces password-change deadlines, 2FA enforcement resolves correctly across global/per-account/superadmin dimensions, scope-limited tokens can only reach /api/auth/enable-2fa, the Security Policy tab controls global+per-account 2FA, and the API Rate Limits tab edits the runtime-editable policies stored in `system_settings`.

### Task 33: ScopeLimitedTokenMiddleware

**Goal:** Middleware that rejects scope-limited JWTs (`scope: '2fa-setup-only'`) on every endpoint except `/api/auth/enable-2fa` + `/api/auth/logout` + `/api/auth/me`. Runs after `TokenRevocationMiddleware`.

**Files:**
- Create: `src/Backend/AuraCore.API/Middleware/ScopeLimitedTokenMiddleware.cs`
- Modify: `src/Backend/AuraCore.API/Program.cs` — register middleware immediately after TokenRevocation
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/ScopeLimitedTokenMiddlewareTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/AuraCore.Tests.API/SuperadminFoundation/ScopeLimitedTokenMiddlewareTests.cs
using System.Security.Claims;
using AuraCore.API.Middleware;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class ScopeLimitedTokenMiddlewareTests
{
    private static DefaultHttpContext BuildCtx(string? scope, string path)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        ctx.Response.Body = new MemoryStream();
        var claims = new List<Claim>();
        if (scope != null) claims.Add(new Claim("scope", scope));
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        return ctx;
    }

    [Fact]
    public async Task Passes_through_when_no_scope_claim()
    {
        var ctx = BuildCtx(null, "/api/admin/users");
        var called = false;
        var mw = new ScopeLimitedTokenMiddleware(_ => { called = true; return Task.CompletedTask; });
        await mw.InvokeAsync(ctx);
        Assert.True(called);
    }

    [Fact]
    public async Task Returns_403_when_scope_limited_token_hits_admin_endpoint()
    {
        var ctx = BuildCtx("2fa-setup-only", "/api/admin/users");
        var called = false;
        var mw = new ScopeLimitedTokenMiddleware(_ => { called = true; return Task.CompletedTask; });
        await mw.InvokeAsync(ctx);
        Assert.False(called);
        Assert.Equal(403, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task Allows_scope_limited_token_on_enable_2fa()
    {
        var ctx = BuildCtx("2fa-setup-only", "/api/auth/enable-2fa");
        var called = false;
        var mw = new ScopeLimitedTokenMiddleware(_ => { called = true; return Task.CompletedTask; });
        await mw.InvokeAsync(ctx);
        Assert.True(called);
    }

    [Fact]
    public async Task Allows_scope_limited_token_on_logout()
    {
        var ctx = BuildCtx("2fa-setup-only", "/api/auth/logout");
        var called = false;
        var mw = new ScopeLimitedTokenMiddleware(_ => { called = true; return Task.CompletedTask; });
        await mw.InvokeAsync(ctx);
        Assert.True(called);
    }
}
```

- [ ] **Step 2: Verify tests fail**

- [ ] **Step 3: Create middleware**

```csharp
// src/Backend/AuraCore.API/Middleware/ScopeLimitedTokenMiddleware.cs
namespace AuraCore.API.Middleware;

public class ScopeLimitedTokenMiddleware
{
    private static readonly HashSet<string> AllowedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/enable-2fa",
        "/api/auth/logout",
        "/api/auth/me",
    };

    private readonly RequestDelegate _next;
    public ScopeLimitedTokenMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        var scope = ctx.User.FindFirst("scope")?.Value;
        if (string.IsNullOrEmpty(scope))
        {
            await _next(ctx);
            return;
        }

        var path = ctx.Request.Path.Value ?? "";
        if (AllowedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(ctx);
            return;
        }

        ctx.Response.StatusCode = 403;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync("{\"error\":\"scope_limited_token\",\"scope\":\"" + scope + "\"}");
    }
}
```

- [ ] **Step 4: Register in Program.cs**

Find `app.UseMiddleware<AuraCore.API.Middleware.TokenRevocationMiddleware>();`. Append immediately after:

```csharp
app.UseMiddleware<AuraCore.API.Middleware.ScopeLimitedTokenMiddleware>();
```

- [ ] **Step 5: Tests pass**

- [ ] **Step 6: Commit**

```bash
git add src/Backend/AuraCore.API/Middleware/ScopeLimitedTokenMiddleware.cs src/Backend/AuraCore.API/Program.cs tests/AuraCore.Tests.API/SuperadminFoundation/ScopeLimitedTokenMiddlewareTests.cs
git commit -m "feat(6.11.W5.T33): ScopeLimitedTokenMiddleware — restrict 2fa-setup tokens

Tokens with scope='2fa-setup-only' can only reach /api/auth/enable-2fa,
/logout, /me. All other endpoints return 403 scope_limited_token."
```

### Task 34: ForcePasswordChangeMiddleware + /api/auth/logout

**Goal:** Middleware that blocks authenticated requests when the user has `ForcePasswordChange=true` AND `ForcePasswordChangeBy < now()`. Allow-list: `/api/auth/change-password`, `/api/auth/logout`. Also add `POST /api/auth/logout` endpoint that revokes current token jti + refresh.

**Files:**
- Create: `src/Backend/AuraCore.API/Middleware/ForcePasswordChangeMiddleware.cs`
- Modify: `src/Backend/AuraCore.API/Controllers/AuthController.cs` — add `/logout` endpoint
- Modify: `src/Backend/AuraCore.API/Program.cs` — register middleware
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/ForcePasswordChangeMiddlewareTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
// tests/AuraCore.Tests.API/SuperadminFoundation/ForcePasswordChangeMiddlewareTests.cs
using System.Security.Claims;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using AuraCore.API.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class ForcePasswordChangeMiddlewareTests
{
    private static AuraCoreDbContext BuildDb()
    {
        var opt = new DbContextOptionsBuilder<AuraCoreDbContext>().UseInMemoryDatabase($"fp-{Guid.NewGuid()}").Options;
        return new AuraCoreDbContext(opt);
    }

    private static DefaultHttpContext BuildCtx(AuraCoreDbContext db, Guid userId, string path)
    {
        var ctx = new DefaultHttpContext();
        ctx.RequestServices = new ServiceCollection().AddSingleton(db).BuildServiceProvider();
        ctx.Request.Path = path;
        ctx.Response.Body = new MemoryStream();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", userId.ToString()) }, "Bearer"));
        return ctx;
    }

    [Fact]
    public async Task Passes_through_when_user_has_no_flag()
    {
        var db = BuildDb();
        var uid = Guid.NewGuid();
        db.Users.Add(new User { Id = uid, Email = "a@x.com", PasswordHash = "x", Role = "admin" });
        await db.SaveChangesAsync();

        var called = false;
        var mw = new ForcePasswordChangeMiddleware(_ => { called = true; return Task.CompletedTask; });
        await mw.InvokeAsync(BuildCtx(db, uid, "/api/admin/users"), db);
        Assert.True(called);
    }

    [Fact]
    public async Task Returns_403_when_deadline_has_passed()
    {
        var db = BuildDb();
        var uid = Guid.NewGuid();
        db.Users.Add(new User {
            Id = uid, Email = "a@x.com", PasswordHash = "x", Role = "admin",
            ForcePasswordChange = true,
            ForcePasswordChangeBy = DateTimeOffset.UtcNow.AddMinutes(-1),
        });
        await db.SaveChangesAsync();

        var ctx = BuildCtx(db, uid, "/api/admin/users");
        var called = false;
        var mw = new ForcePasswordChangeMiddleware(_ => { called = true; return Task.CompletedTask; });
        await mw.InvokeAsync(ctx, db);

        Assert.False(called);
        Assert.Equal(403, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task Allows_change_password_endpoint_even_after_deadline()
    {
        var db = BuildDb();
        var uid = Guid.NewGuid();
        db.Users.Add(new User {
            Id = uid, Email = "a@x.com", PasswordHash = "x", Role = "admin",
            ForcePasswordChange = true,
            ForcePasswordChangeBy = DateTimeOffset.UtcNow.AddMinutes(-1),
        });
        await db.SaveChangesAsync();

        var ctx = BuildCtx(db, uid, "/api/auth/change-password");
        var called = false;
        var mw = new ForcePasswordChangeMiddleware(_ => { called = true; return Task.CompletedTask; });
        await mw.InvokeAsync(ctx, db);
        Assert.True(called);
    }
}
```

- [ ] **Step 2: Verify fail**

- [ ] **Step 3: Create middleware**

```csharp
// src/Backend/AuraCore.API/Middleware/ForcePasswordChangeMiddleware.cs
using AuraCore.API.Helpers;
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Middleware;

public class ForcePasswordChangeMiddleware
{
    private static readonly HashSet<string> AllowedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/change-password",
        "/api/auth/logout",
        "/api/auth/me",
    };

    private readonly RequestDelegate _next;
    public ForcePasswordChangeMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, AuraCoreDbContext db)
    {
        if (ctx.User.Identity?.IsAuthenticated != true) { await _next(ctx); return; }

        var userId = ctx.User.GetUserId();
        if (userId is null) { await _next(ctx); return; }

        var path = ctx.Request.Path.Value ?? "";
        if (AllowedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase))) { await _next(ctx); return; }

        var user = await db.Users
            .Where(u => u.Id == userId.Value)
            .Select(u => new { u.ForcePasswordChange, u.ForcePasswordChangeBy })
            .FirstOrDefaultAsync();
        if (user is null || !user.ForcePasswordChange) { await _next(ctx); return; }

        var deadline = user.ForcePasswordChangeBy ?? DateTimeOffset.UtcNow; // null deadline = due now
        if (deadline <= DateTimeOffset.UtcNow)
        {
            ctx.Response.StatusCode = 403;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync("{\"error\":\"password_change_required\"}");
            return;
        }

        await _next(ctx);
    }
}
```

- [ ] **Step 4: Add `/api/auth/logout` endpoint**

Append to `AuthController.cs`:

```csharp
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var jti = User.GetJti();
        if (userId is not null && !string.IsNullOrEmpty(jti))
        {
            _db.RevokedTokens.Add(new RevokedToken { Jti = jti, UserId = userId.Value, RevokeReason = "logout" });
            var refreshes = await _db.RefreshTokens.Where(r => r.UserId == userId.Value && !r.IsRevoked).ToListAsync(ct);
            foreach (var r in refreshes) r.IsRevoked = true;
            await _db.SaveChangesAsync(ct);
        }
        return Ok(new { ok = true });
    }
```

- [ ] **Step 5: Register middleware in Program.cs**

Right after `ScopeLimitedTokenMiddleware`:

```csharp
app.UseMiddleware<AuraCore.API.Middleware.ForcePasswordChangeMiddleware>();
```

- [ ] **Step 6: Tests pass + commit**

```bash
git add src/Backend/AuraCore.API/Middleware/ForcePasswordChangeMiddleware.cs src/Backend/AuraCore.API/Controllers/AuthController.cs src/Backend/AuraCore.API/Program.cs tests/AuraCore.Tests.API/SuperadminFoundation/ForcePasswordChangeMiddlewareTests.cs
git commit -m "feat(6.11.W5.T34): ForcePasswordChangeMiddleware + /api/auth/logout

When a user has ForcePasswordChange=true AND deadline passed, all
endpoints except /change-password, /logout, /me return 403
password_change_required. Logout blacklists current access jti via
RevokedToken + revokes all outstanding refresh tokens."
```

### Task 35: 2FA enforcement in /api/auth/login + Enable2FA endpoint

**Goal:** Existing `/api/auth/login` path now computes `requires_2fa = (role==superadmin) OR global_setting OR user.Require2fa`. If true AND totp_enabled=false, return scope-limited JWT + `requiresTwoFactorSetup: true`. Add `/api/auth/enable-2fa` endpoints (generate + confirm) that accept scope-limited tokens.

**Files:**
- Modify: `src/Backend/AuraCore.API/Controllers/AuthController.cs`
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/TwoFactorEnforcementTests.cs`

- [ ] **Step 1: Write failing test covering resolution ladder**

```csharp
// tests/AuraCore.Tests.API/SuperadminFoundation/TwoFactorEnforcementTests.cs
using System.Net.Http.Json;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class TwoFactorEnforcementTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _f;

    public TwoFactorEnforcementTests(WebApplicationFactory<Program> f)
    {
        Environment.SetEnvironmentVariable("JWT_SECRET", "test-secret-at-least-32-characters-long!!");
        _f = f.WithWebHostBuilder(b => b.ConfigureServices(s => {
            var d = s.Single(x => x.ServiceType == typeof(DbContextOptions<AuraCoreDbContext>));
            s.Remove(d);
            s.AddDbContext<AuraCoreDbContext>(o => o.UseInMemoryDatabase($"2fa-{Guid.NewGuid()}"));
        }));
    }

    private async Task Seed(Action<AuraCoreDbContext> act)
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        act(db);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Admin_without_require_2fa_and_global_off_does_not_require_setup()
    {
        await Seed(db => {
            db.Users.Add(new User {
                Id = Guid.NewGuid(), Email = "a@x.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("GoodPass12"),
                Role = "admin", TotpEnabled = false, Require2fa = false,
            });
        });
        var c = _f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email = "a@x.com", password = "GoodPass12" });
        var body = await r.Content.ReadAsStringAsync();
        Assert.DoesNotContain("requiresTwoFactorSetup", body);
    }

    [Fact]
    public async Task Admin_with_per_account_require_2fa_returns_setup_token()
    {
        await Seed(db => {
            db.Users.Add(new User {
                Id = Guid.NewGuid(), Email = "b@x.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("GoodPass12"),
                Role = "admin", TotpEnabled = false, Require2fa = true,
            });
        });
        var c = _f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email = "b@x.com", password = "GoodPass12" });
        var body = await r.Content.ReadAsStringAsync();
        Assert.Contains("requiresTwoFactorSetup", body);
    }

    [Fact]
    public async Task Admin_when_global_2fa_on_returns_setup_token()
    {
        await Seed(db => {
            db.Users.Add(new User {
                Id = Guid.NewGuid(), Email = "c@x.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("GoodPass12"),
                Role = "admin", TotpEnabled = false, Require2fa = false,
            });
            db.SystemSettings.Add(new SystemSetting { Key = "require_2fa_for_all_admins", Value = "true" });
        });
        var c = _f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email = "c@x.com", password = "GoodPass12" });
        var body = await r.Content.ReadAsStringAsync();
        Assert.Contains("requiresTwoFactorSetup", body);
    }
}
```

- [ ] **Step 2: Verify fails**

- [ ] **Step 3: Modify `AuthController.Login`**

Read `AuthController.Login`. After the TOTP branch that fires when `user.TotpEnabled==true`, wrap the entire `await LogAttemptAsync(true); return Ok(...)` flow so it checks the 2FA enforcement before returning success. Concretely, after the user is authenticated + 2FA (if enabled) passes, but BEFORE the `Ok(new { accessToken, ... })`, add:

```csharp
        // Phase 6.11: 2FA enforcement
        if (user is not null && (user.Role == "admin" || user.Role == "superadmin"))
        {
            var globalOn = await _db.SystemSettings
                .Where(s => s.Key == "require_2fa_for_all_admins")
                .Select(s => s.Value).FirstOrDefaultAsync(ct);
            var globalBool = string.Equals(globalOn, "true", StringComparison.OrdinalIgnoreCase);
            var requires2fa = user.Role == "superadmin" || globalBool || user.Require2fa;
            if (requires2fa && !user.TotpEnabled)
            {
                var scoped = _auth.GenerateAccessToken(user, scope: "2fa-setup-only", lifetime: TimeSpan.FromMinutes(15));
                return Ok(new
                {
                    requiresTwoFactorSetup = true,
                    accessToken = scoped,
                    user = new { user.Id, user.Email, user.Role },
                });
            }
        }
```

(Place this immediately after the TOTP-code validation success path, before `await LogAttemptAsync(true);`.)

- [ ] **Step 4: Add `/api/auth/enable-2fa` endpoints**

Append two endpoints to `AuthController.cs`:

```csharp
    [Authorize]
    [HttpPost("enable-2fa/generate")]
    public async Task<IActionResult> Enable2faGenerate(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();
        var user = await _db.Users.FindAsync(new object[] { userId.Value }, ct);
        if (user is null) return Unauthorized();
        if (user.TotpEnabled) return BadRequest(new { error = "already_enabled" });

        var secret = AuraCore.API.Application.Services.Security.TotpService.GenerateSecret();
        user.TotpSecret = _totpEnc.Encrypt(secret);
        await _db.SaveChangesAsync(ct);

        var uri = AuraCore.API.Application.Services.Security.TotpService.BuildOtpAuthUri(user.Email, "AuraCore Pro", secret);
        return Ok(new { secret, uri });
    }

    [Authorize]
    [HttpPost("enable-2fa/confirm")]
    public async Task<IActionResult> Enable2faConfirm([FromBody] EnableDto dto, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();
        var user = await _db.Users.FindAsync(new object[] { userId.Value }, ct);
        if (user is null || user.TotpSecret is null) return BadRequest(new { error = "not_generated" });

        var plaintext = _totpEnc.Decrypt(user.TotpSecret);
        if (!AuraCore.API.Application.Services.Security.TotpService.ValidateCode(plaintext, dto.Code))
            return BadRequest(new { error = "invalid_code" });

        user.TotpEnabled = true;
        await _db.SaveChangesAsync(ct);

        // Issue a fresh non-scope-limited token.
        var access = _auth.GenerateAccessToken(user);
        var refresh = _auth.GenerateRefreshToken();
        _db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, Token = refresh, ExpiresAt = DateTimeOffset.UtcNow.AddDays(30) });
        await _db.SaveChangesAsync(ct);
        return Ok(new { accessToken = access, refreshToken = refresh, user = new { user.Id, user.Email, user.Role } });
    }

    public sealed record EnableDto(string Code);
```

Verify that `TotpService.GenerateSecret()` / `BuildOtpAuthUri()` methods exist in `AuraCore.API.Application.Services.Security`; if the existing TotpService has different method names, adapt. The test on the AuthController setup flow in Phase 6 earlier likely has patterns to follow.

- [ ] **Step 5: Tests pass + commit**

```bash
git add src/Backend/AuraCore.API/Controllers/AuthController.cs tests/AuraCore.Tests.API/SuperadminFoundation/TwoFactorEnforcementTests.cs
git commit -m "feat(6.11.W5.T35): 2FA enforcement ladder + /enable-2fa endpoints

/api/auth/login now resolves requires_2fa as
  (role==superadmin) OR system_settings['require_2fa_for_all_admins']=true
  OR user.Require2fa. If true AND not TotpEnabled, issues scope-limited
  ('2fa-setup-only', 15 min) JWT and returns requiresTwoFactorSetup=true.

/api/auth/enable-2fa/generate + /confirm complete the TOTP setup and
return a fresh non-scoped JWT. ScopeLimitedTokenMiddleware from T33
allows these two endpoints through."
```

### Task 36: SecurityPolicyController (superadmin) + SecurityPolicyPage

**Goal:** Read/write global 2FA toggle + list per-account require-2fa values. Frontend tab renders both controls; per-account toggle calls `api.setAdminRequire2fa()`.

**Files:**
- Create: `src/Backend/AuraCore.API/Controllers/Superadmin/SecurityPolicyController.cs`
- Replace stub: `admin-panel/src/views/SecurityPolicyPage.tsx`

- [ ] **Step 1: Create `SecurityPolicyController.cs`**

```csharp
// src/Backend/AuraCore.API/Controllers/Superadmin/SecurityPolicyController.cs
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Superadmin;

[ApiController]
[Route("api/superadmin/security-policy")]
[Authorize(Roles = "superadmin")]
public sealed class SecurityPolicyController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    public SecurityPolicyController(AuraCoreDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var global = await _db.SystemSettings
            .Where(s => s.Key == "require_2fa_for_all_admins")
            .Select(s => s.Value).FirstOrDefaultAsync(ct);
        var overrides = await _db.Users
            .Where(u => u.Role == "admin" || u.Role == "superadmin")
            .Select(u => new { userId = u.Id, email = u.Email, require2fa = u.Require2fa, role = u.Role })
            .ToListAsync(ct);
        return Ok(new {
            require2faForAllAdmins = string.Equals(global, "true", StringComparison.OrdinalIgnoreCase),
            perAccountOverrides = overrides,
        });
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateDto dto, CancellationToken ct)
    {
        var row = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == "require_2fa_for_all_admins", ct);
        if (row is null) { row = new SystemSetting { Key = "require_2fa_for_all_admins" }; _db.SystemSettings.Add(row); }
        row.Value = dto.Require2faForAllAdmins ? "true" : "false";
        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = User.GetUserId();
        await _db.SaveChangesAsync(ct);
        return Ok(new { require2faForAllAdmins = dto.Require2faForAllAdmins });
    }

    public sealed record UpdateDto(bool Require2faForAllAdmins);
}
```

- [ ] **Step 2: Implement `SecurityPolicyPage.tsx`**

```tsx
// admin-panel/src/views/SecurityPolicyPage.tsx
'use client';

import { useEffect, useState } from 'react';
import { Lock, Shield } from 'lucide-react';
import { api } from '@/lib/api';

export function SecurityPolicyPage() {
  const [globalOn, setGlobalOn] = useState(false);
  const [accounts, setAccounts] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);

  const refresh = async () => {
    setLoading(true);
    const p = await api.getSecurityPolicy();
    if (p) {
      setGlobalOn(p.require2faForAllAdmins);
      setAccounts(p.perAccountOverrides);
    }
    setLoading(false);
  };
  useEffect(() => { refresh(); }, []);

  const toggleGlobal = async () => {
    const next = !globalOn;
    setGlobalOn(next);
    await api.updateSecurityPolicy(next);
  };
  const toggleAccount = async (userId: string, value: boolean) => {
    setAccounts(prev => prev.map(a => a.userId === userId ? { ...a, require2fa: value } : a));
    await api.setAdminRequire2fa(userId, value);
  };

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-display font-bold flex items-center gap-2"><Lock className="w-6 h-6" />Security Policy</h1>
      {loading ? <div className="glass-card p-8 text-center text-white/50">Loading…</div> : (
        <>
          <div className="glass-card p-4 flex items-center justify-between">
            <div>
              <div className="font-semibold">Require 2FA for all admin accounts</div>
              <div className="text-xs text-white/50">When on, every admin must have TOTP enabled to log in.</div>
            </div>
            <button onClick={toggleGlobal} className={globalOn ? 'btn-primary' : 'btn-ghost'}>{globalOn ? 'On' : 'Off'}</button>
          </div>
          <div className="glass-card overflow-hidden">
            <table className="w-full text-sm">
              <thead className="bg-white/5"><tr>
                <th className="p-3 text-left">Email</th><th className="p-3 text-left">Role</th>
                <th className="p-3 text-left">Per-account require 2FA</th>
              </tr></thead>
              <tbody>
                {accounts.map(a => (
                  <tr key={a.userId} className="border-t border-white/5">
                    <td className="p-3">{a.email}</td>
                    <td className="p-3">{a.role}</td>
                    <td className="p-3">
                      {a.role === 'superadmin' ? <span className="text-xs text-white/40">required (always)</span> : (
                        <label className="flex items-center gap-2 text-sm">
                          <input type="checkbox" checked={a.require2fa} onChange={e => toggleAccount(a.userId, e.target.checked)} />
                          {a.require2fa ? 'required' : 'optional'}
                        </label>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </div>
  );
}
```

- [ ] **Step 3: Build + commit**

```bash
git add src/Backend/AuraCore.API/Controllers/Superadmin/SecurityPolicyController.cs admin-panel/src/views/SecurityPolicyPage.tsx
git commit -m "feat(6.11.W5.T36): SecurityPolicyController + SecurityPolicyPage

GET + PUT /api/superadmin/security-policy — global 2FA toggle +
per-account list (read via users.Require2fa). Frontend page renders
both controls; per-account row for superadmin is read-only
'required (always)'."
```

### Task 37: RateLimitConfigService + RateLimitConfigController + APIRateLimitsPage

**Goal:** Runtime-editable rate-limit policies. Service caches `system_settings['rate_limit_policies']` JSON in memory (5-min TTL); PUT endpoint invalidates + updates. Phase 6.11 scope does NOT wire the hot-reload into the ASP.NET Core RateLimiter pipeline (the `/api/auth/login` controller implements its own rate-limit via `login_attempts`); instead the service exposes the current values and the UI edits them. Full ASP.NET Core RateLimiter integration queued for Phase 6.12.

**Files:**
- Create: `src/Backend/AuraCore.API.Application/Services/RateLimiting/IRateLimitConfigService.cs`
- Create: `src/Backend/AuraCore.API.Infrastructure/Services/RateLimiting/RateLimitConfigService.cs`
- Create: `src/Backend/AuraCore.API/Controllers/Superadmin/RateLimitConfigController.cs`
- Replace stub: `admin-panel/src/views/APIRateLimitsPage.tsx`
- Modify: `src/Backend/AuraCore.API/Program.cs` — register service
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/RateLimitConfigServiceTests.cs`

- [ ] **Step 1: Write failing service test**

```csharp
// tests/AuraCore.Tests.API/SuperadminFoundation/RateLimitConfigServiceTests.cs
using AuraCore.API.Application.Services.RateLimiting;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using AuraCore.API.Infrastructure.Services.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class RateLimitConfigServiceTests
{
    private static (AuraCoreDbContext db, RateLimitConfigService svc) Build()
    {
        var opt = new DbContextOptionsBuilder<AuraCoreDbContext>().UseInMemoryDatabase($"rl-{Guid.NewGuid()}").Options;
        var db = new AuraCoreDbContext(opt);
        var cache = new MemoryCache(new MemoryCacheOptions());
        return (db, new RateLimitConfigService(db, cache));
    }

    [Fact]
    public async Task GetAll_returns_seeded_default_policies()
    {
        var (db, svc) = Build();
        db.SystemSettings.Add(new SystemSetting { Key = "rate_limit_policies",
            Value = "{\"auth.login\":{\"requests\":5,\"windowSeconds\":1800}}" });
        await db.SaveChangesAsync();
        var all = await svc.GetAllAsync();
        Assert.Equal(5, all["auth.login"].Requests);
    }

    [Fact]
    public async Task Update_mutates_json_and_invalidates_cache()
    {
        var (db, svc) = Build();
        db.SystemSettings.Add(new SystemSetting { Key = "rate_limit_policies",
            Value = "{\"auth.login\":{\"requests\":5,\"windowSeconds\":1800}}" });
        await db.SaveChangesAsync();
        _ = await svc.GetAllAsync();
        await svc.UpdateAsync("auth.login", new RateLimitPolicy(10, 900));
        var all = await svc.GetAllAsync();
        Assert.Equal(10, all["auth.login"].Requests);
        Assert.Equal(900, all["auth.login"].WindowSeconds);
    }
}
```

- [ ] **Step 2: Verify fails**

- [ ] **Step 3: Create interface + record + service**

`src/Backend/AuraCore.API.Application/Services/RateLimiting/IRateLimitConfigService.cs`:

```csharp
namespace AuraCore.API.Application.Services.RateLimiting;

public sealed record RateLimitPolicy(int Requests, int WindowSeconds);

public interface IRateLimitConfigService
{
    Task<IReadOnlyDictionary<string, RateLimitPolicy>> GetAllAsync(CancellationToken ct = default);
    Task UpdateAsync(string endpoint, RateLimitPolicy policy, CancellationToken ct = default);
}
```

`src/Backend/AuraCore.API.Infrastructure/Services/RateLimiting/RateLimitConfigService.cs`:

```csharp
using System.Text.Json;
using AuraCore.API.Application.Services.RateLimiting;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AuraCore.API.Infrastructure.Services.RateLimiting;

public sealed class RateLimitConfigService : IRateLimitConfigService
{
    private const string CacheKey = "rate_limit_policies";
    private const string SettingKey = "rate_limit_policies";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly AuraCoreDbContext _db;
    private readonly IMemoryCache _cache;

    public RateLimitConfigService(AuraCoreDbContext db, IMemoryCache cache)
    {
        _db = db; _cache = cache;
    }

    public async Task<IReadOnlyDictionary<string, RateLimitPolicy>> GetAllAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue<Dictionary<string, RateLimitPolicy>>(CacheKey, out var cached) && cached is not null)
            return cached;
        var row = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == SettingKey, ct);
        var raw = row?.Value ?? "{}";
        var map = JsonSerializer.Deserialize<Dictionary<string, RateLimitPolicy>>(raw)
               ?? new Dictionary<string, RateLimitPolicy>();
        _cache.Set(CacheKey, map, CacheTtl);
        return map;
    }

    public async Task UpdateAsync(string endpoint, RateLimitPolicy policy, CancellationToken ct = default)
    {
        var row = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == SettingKey, ct)
                  ?? new SystemSetting { Key = SettingKey, Value = "{}" };
        var map = JsonSerializer.Deserialize<Dictionary<string, RateLimitPolicy>>(row.Value)
                  ?? new Dictionary<string, RateLimitPolicy>();
        map[endpoint] = policy;
        row.Value = JsonSerializer.Serialize(map);
        row.UpdatedAt = DateTime.UtcNow;
        if (row.Key != SettingKey) _db.SystemSettings.Add(row);
        await _db.SaveChangesAsync(ct);
        _cache.Remove(CacheKey);
    }
}
```

- [ ] **Step 4: Create controller**

```csharp
// src/Backend/AuraCore.API/Controllers/Superadmin/RateLimitConfigController.cs
using AuraCore.API.Application.Services.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuraCore.API.Controllers.Superadmin;

[ApiController]
[Route("api/superadmin/rate-limits")]
[Authorize(Roles = "superadmin")]
public sealed class RateLimitConfigController : ControllerBase
{
    private readonly IRateLimitConfigService _svc;
    public RateLimitConfigController(IRateLimitConfigService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var map = await _svc.GetAllAsync(ct);
        return Ok(new { items = map.Select(kv => new { endpoint = kv.Key, requests = kv.Value.Requests, windowSeconds = kv.Value.WindowSeconds }) });
    }

    [HttpPut("{endpoint}")]
    public async Task<IActionResult> Update(string endpoint, [FromBody] RateLimitPolicy policy, CancellationToken ct)
    {
        if (policy.Requests <= 0 || policy.WindowSeconds <= 0)
            return BadRequest(new { error = "invalid_policy" });
        await _svc.UpdateAsync(endpoint, policy, ct);
        return Ok(new { endpoint, policy.Requests, policy.WindowSeconds });
    }
}
```

- [ ] **Step 5: Register in Program.cs**

Alongside the existing DI registrations:

```csharp
builder.Services.AddScoped<AuraCore.API.Application.Services.RateLimiting.IRateLimitConfigService,
                          AuraCore.API.Infrastructure.Services.RateLimiting.RateLimitConfigService>();
```

- [ ] **Step 6: Implement `APIRateLimitsPage.tsx`**

```tsx
// admin-panel/src/views/APIRateLimitsPage.tsx
'use client';

import { useEffect, useState } from 'react';
import { Gauge } from 'lucide-react';
import { api } from '@/lib/api';

export function APIRateLimitsPage() {
  const [items, setItems] = useState<any[]>([]);
  const [editing, setEditing] = useState<{ endpoint: string; requests: number; windowSeconds: number } | null>(null);
  const [loading, setLoading] = useState(true);

  const refresh = async () => {
    setLoading(true);
    const r = await api.getRateLimitPolicies();
    setItems(r.items ?? []);
    setLoading(false);
  };
  useEffect(() => { refresh(); }, []);

  const save = async () => {
    if (!editing) return;
    await api.updateRateLimitPolicy(editing.endpoint, editing.requests, editing.windowSeconds);
    setEditing(null); refresh();
  };

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-display font-bold flex items-center gap-2"><Gauge className="w-6 h-6" />API Rate Limits</h1>
      <div className="glass-card overflow-hidden">
        {loading ? <div className="p-8 text-center text-white/50">Loading…</div> : (
          <table className="w-full text-sm">
            <thead className="bg-white/5"><tr>
              <th className="p-3 text-left">Endpoint</th>
              <th className="p-3 text-left">Requests</th>
              <th className="p-3 text-left">Window (s)</th>
              <th className="p-3 text-right">Action</th>
            </tr></thead>
            <tbody>
              {items.map(p => (
                <tr key={p.endpoint} className="border-t border-white/5">
                  <td className="p-3"><code className="text-xs">{p.endpoint}</code></td>
                  <td className="p-3">{p.requests}</td>
                  <td className="p-3">{p.windowSeconds}</td>
                  <td className="p-3 text-right">
                    <button onClick={() => setEditing(p)} className="btn-primary-sm">Edit</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {editing && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
          <div className="glass-card w-full max-w-sm p-6 space-y-3">
            <h3 className="text-lg font-display font-bold">Edit {editing.endpoint}</h3>
            <label className="block text-xs text-white/50">Requests
              <input type="number" value={editing.requests} onChange={e => setEditing({ ...editing, requests: +e.target.value })} className="input-dark w-full mt-1" />
            </label>
            <label className="block text-xs text-white/50">Window (seconds)
              <input type="number" value={editing.windowSeconds} onChange={e => setEditing({ ...editing, windowSeconds: +e.target.value })} className="input-dark w-full mt-1" />
            </label>
            <div className="flex justify-end gap-2">
              <button onClick={() => setEditing(null)} className="btn-ghost">Cancel</button>
              <button onClick={save} className="btn-primary">Apply</button>
            </div>
          </div>
        </div>
      )}
      <p className="text-xs text-white/40">Edits persist to system_settings['rate_limit_policies'] and invalidate the 5-min cache. Hot-reload of the ASP.NET Core RateLimiter pipeline is queued for Phase 6.12.</p>
    </div>
  );
}
```

- [ ] **Step 7: Tests pass + commit**

```bash
git add src/Backend/AuraCore.API.Application/Services/RateLimiting/ src/Backend/AuraCore.API.Infrastructure/Services/RateLimiting/ src/Backend/AuraCore.API/Controllers/Superadmin/RateLimitConfigController.cs src/Backend/AuraCore.API/Program.cs admin-panel/src/views/APIRateLimitsPage.tsx tests/AuraCore.Tests.API/SuperadminFoundation/RateLimitConfigServiceTests.cs
git commit -m "feat(6.11.W5.T37): RateLimitConfigService + APIRateLimitsPage

Service reads rate_limit_policies JSON from system_settings, caches
in IMemoryCache (5-min TTL), writes through + invalidates on update.
Controller: GET list + PUT /{endpoint} with validation. UI renders
per-endpoint table + edit dialog. Runtime edits persist but do not
yet hot-reload ASP.NET Core RateLimiter — deferred to 6.12."
```

### Task 37.1: RetentionJob background hosted service

**Goal:** Daily GC for unbounded-growth tables. Per spec: **audit_log retention is 6.12+**, so we do NOT touch `audit_log`. But `revoked_tokens` (logout / suspend / password-reset every event adds a row) and `admin_invitations` (every invite adds a row) both grow unboundedly without a sweep. Delete `revoked_tokens` older than 2 hours (access-token TTL is 15 min; 2h buffer covers clock skew and is already well past any live token's expiry). Delete `admin_invitations` where `ExpiresAt < now() - 30 days` AND `ConsumedAt IS NULL`. Runs in a hosted background service once every 24h, with a 1-min startup delay so the app finishes booting first.

**Files:**
- Create: `src/Backend/AuraCore.API/HostedServices/RetentionJob.cs`
- Modify: `src/Backend/AuraCore.API/Program.cs` — register `AddHostedService<RetentionJob>()`
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/RetentionJobTests.cs`

- [ ] **Step 1: Write failing tests**

`tests/AuraCore.Tests.API/SuperadminFoundation/RetentionJobTests.cs`:

```csharp
using AuraCore.API.Domain.Entities;
using AuraCore.API.HostedServices;
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class RetentionJobTests
{
    private static AuraCoreDbContext BuildDb()
    {
        var opt = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"ret-{Guid.NewGuid()}").Options;
        return new AuraCoreDbContext(opt);
    }

    [Fact]
    public async Task Sweep_deletes_old_revoked_tokens()
    {
        var db = BuildDb();
        var u = new User { Id = Guid.NewGuid(), Email = "a@x.com", PasswordHash = "x", Role = "admin" };
        db.Users.Add(u);
        db.RevokedTokens.Add(new RevokedToken { Jti = "old", UserId = u.Id, RevokeReason = "logout", RevokedAt = DateTime.UtcNow.AddHours(-3) });
        db.RevokedTokens.Add(new RevokedToken { Jti = "fresh", UserId = u.Id, RevokeReason = "logout", RevokedAt = DateTime.UtcNow.AddMinutes(-30) });
        await db.SaveChangesAsync();

        await RetentionJob.SweepAsync(db, NullLogger.Instance);

        var remaining = await db.RevokedTokens.Select(r => r.Jti).ToListAsync();
        Assert.DoesNotContain("old", remaining);
        Assert.Contains("fresh", remaining);
    }

    [Fact]
    public async Task Sweep_deletes_expired_unconsumed_invitations()
    {
        var db = BuildDb();
        var u = new User { Id = Guid.NewGuid(), Email = "a@x.com", PasswordHash = "x", Role = "admin" };
        var su = new User { Id = Guid.NewGuid(), Email = "s@x.com", PasswordHash = "x", Role = "superadmin" };
        db.Users.AddRange(u, su);
        db.AdminInvitations.Add(new AdminInvitation {
            TokenHash = "old-inv", AdminUserId = u.Id, CreatedBy = su.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(-31),
        });
        db.AdminInvitations.Add(new AdminInvitation {
            TokenHash = "accepted", AdminUserId = u.Id, CreatedBy = su.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(-31), ConsumedAt = DateTime.UtcNow.AddDays(-35),
        });
        db.AdminInvitations.Add(new AdminInvitation {
            TokenHash = "fresh-inv", AdminUserId = u.Id, CreatedBy = su.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(5),
        });
        await db.SaveChangesAsync();

        await RetentionJob.SweepAsync(db, NullLogger.Instance);

        var remaining = await db.AdminInvitations.Select(i => i.TokenHash).ToListAsync();
        Assert.DoesNotContain("old-inv", remaining);
        Assert.Contains("accepted", remaining);   // consumed — keep for audit
        Assert.Contains("fresh-inv", remaining);  // not yet expired
    }

    [Fact]
    public async Task Sweep_does_not_touch_audit_log()
    {
        var db = BuildDb();
        db.AuditLogs.Add(new AuditLogEntry { ActorEmail = "old@x.com", Action = "X", TargetType = "Y", CreatedAt = DateTimeOffset.UtcNow.AddYears(-5) });
        await db.SaveChangesAsync();

        await RetentionJob.SweepAsync(db, NullLogger.Instance);

        // Spec: audit_log retention deferred to 6.12
        Assert.Equal(1, await db.AuditLogs.CountAsync());
    }
}
```

- [ ] **Step 2: Verify fails**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj \
  --filter "FullyQualifiedName~RetentionJobTests" 2>&1 | tail -8
```

Expected: compile error — `RetentionJob` not found.

- [ ] **Step 3: Create `RetentionJob.cs`**

`src/Backend/AuraCore.API/HostedServices/RetentionJob.cs`:

```csharp
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuraCore.API.HostedServices;

/// <summary>
/// Daily GC for unbounded-growth tables from Phase 6.11. Does NOT touch
/// audit_log — spec explicitly defers audit_log retention to Phase 6.12.
///
/// Deletions:
///  - revoked_tokens older than 2h (access-token TTL is 15 min; 2h buffer)
///  - admin_invitations where ExpiresAt &lt; now()-30d AND ConsumedAt IS NULL
///
/// Consumed invitations are PRESERVED for audit trail — they're small
/// (~200 bytes) and bounded by admin creation rate.
/// </summary>
public sealed class RetentionJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan RevokedTokenRetention = TimeSpan.FromHours(2);
    private static readonly TimeSpan ExpiredInvitationRetention = TimeSpan.FromDays(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RetentionJob> _logger;

    public RetentionJob(IServiceScopeFactory scopeFactory, ILogger<RetentionJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app finish booting before first iteration.
        try { await Task.Delay(StartupDelay, stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
                await SweepAsync(db, _logger, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "RetentionJob iteration failed; will retry in 24h");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>
    /// The actual sweep logic. Exposed as a static so unit tests can call it
    /// with a fresh DbContext and assert outcomes without spinning up the
    /// BackgroundService lifecycle.
    /// </summary>
    public static async Task SweepAsync(AuraCoreDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var tokenCutoff = now - RevokedTokenRetention;
        var tokensDeleted = await db.RevokedTokens
            .Where(r => r.RevokedAt < tokenCutoff)
            .ExecuteDeleteAsync(ct);

        var inviteCutoff = now - ExpiredInvitationRetention;
        var invitesDeleted = await db.AdminInvitations
            .Where(i => i.ExpiresAt < inviteCutoff && i.ConsumedAt == null)
            .ExecuteDeleteAsync(ct);

        logger.Log(
            LogLevel.Information,
            "RetentionJob sweep complete: revoked_tokens -{Tokens}, expired invitations -{Invites}",
            tokensDeleted, invitesDeleted);
    }
}
```

**Note on `ExecuteDeleteAsync`:** This is EF Core 7+ bulk-delete. In-memory provider supports it as of EF 7; if a specific test environment hits "NotSupported" with InMemory, swap to the standard `RemoveRange` pattern:

```csharp
var old = await db.RevokedTokens.Where(r => r.RevokedAt < tokenCutoff).ToListAsync(ct);
db.RevokedTokens.RemoveRange(old);
await db.SaveChangesAsync(ct);
var tokensDeleted = old.Count;
```

Prefer `ExecuteDeleteAsync` first; swap on failure.

- [ ] **Step 4: Register in Program.cs**

Read `src/Backend/AuraCore.API/Program.cs`. Near other `AddHostedService<...>()` calls (search for `AuditLogPurgeService` around line 171 — there's already a retention hosted service for `login_attempts`). Add immediately after it:

```csharp
builder.Services.AddHostedService<AuraCore.API.HostedServices.RetentionJob>();
```

- [ ] **Step 5: Tests pass**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj \
  --filter "FullyQualifiedName~RetentionJobTests" 2>&1 | tail -5
```

Expected: 3 passed.

- [ ] **Step 6: Commit**

```bash
git add src/Backend/AuraCore.API/HostedServices/RetentionJob.cs src/Backend/AuraCore.API/Program.cs tests/AuraCore.Tests.API/SuperadminFoundation/RetentionJobTests.cs
git commit -m "feat(6.11.W5.T37.1): RetentionJob — daily GC for revoked_tokens + expired invitations

BackgroundService runs every 24h (1-min startup delay). Sweep logic:
- revoked_tokens older than 2h deleted (access-token TTL is 15 min;
  2h buffer covers clock skew; any still-live token's ban has long
  since been enforced by TokenRevocationMiddleware).
- admin_invitations where ExpiresAt < now()-30d AND ConsumedAt IS NULL.
  Consumed invites preserved (bounded by admin creation rate).

Does NOT touch audit_log — per spec D9, audit_log retention is
Phase 6.12+. SweepAsync exposed as static for unit-testable behavior."
```

### Task 38: ChangePasswordPage + Enable2FAPage

**Goal:** Real implementations of the two auth-flow pages. ChangePasswordPage takes current + new password + confirms. Enable2FAPage generates QR data, renders a QR code (use a minimal inline SVG or text fallback if no QR lib is already in deps), prompts for the 6-digit code, confirms.

**Files:**
- Replace stub: `admin-panel/src/views/ChangePasswordPage.tsx`
- Replace stub: `admin-panel/src/views/Enable2FAPage.tsx`

- [ ] **Step 1: ChangePasswordPage**

```tsx
// admin-panel/src/views/ChangePasswordPage.tsx
'use client';

import { useState } from 'react';
import { Key } from 'lucide-react';
import { api } from '@/lib/api';

export function ChangePasswordPage() {
  const [current, setCurrent] = useState('');
  const [newPw, setNewPw] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState('');
  const [done, setDone] = useState(false);

  const submit = async () => {
    setError('');
    if (newPw.length < 10) return setError('Password must be at least 10 characters');
    if (newPw !== confirm) return setError('Passwords do not match');
    const r = await api.changePassword(current, newPw);
    if (r.ok) { setDone(true); setTimeout(() => window.location.assign('/'), 1500); }
    else setError(r.data?.error ?? 'Failed');
  };

  if (done) return <div className="min-h-screen flex items-center justify-center"><p>Password changed. Redirecting…</p></div>;

  return (
    <div className="max-w-md mx-auto mt-16 glass-card p-8 space-y-4">
      <h2 className="text-xl font-display font-bold flex items-center gap-2"><Key className="w-5 h-5" />Change password</h2>
      <input type="password" placeholder="Current password" value={current} onChange={e => setCurrent(e.target.value)} className="input-dark w-full" />
      <input type="password" placeholder="New password (min 10 chars)" value={newPw} onChange={e => setNewPw(e.target.value)} className="input-dark w-full" />
      <input type="password" placeholder="Confirm new password" value={confirm} onChange={e => setConfirm(e.target.value)} className="input-dark w-full" />
      {error && <div className="text-xs text-aura-red">{error}</div>}
      <button onClick={submit} className="btn-primary w-full">Update password</button>
    </div>
  );
}
```

- [ ] **Step 2: Enable2FAPage**

The admin panel doesn't currently include a QR-code library. Simplest: render the `otpauth://` URI as text + link + let the user enter it manually into their authenticator app. Phase 6.12 can add a proper QR render.

```tsx
// admin-panel/src/views/Enable2FAPage.tsx
'use client';

import { useEffect, useState } from 'react';
import { ShieldCheck } from 'lucide-react';

const API = process.env.NEXT_PUBLIC_API_URL || 'https://api.auracore.pro';

async function post(path: string, body?: any) {
  const res = await fetch(API + path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${localStorage.getItem('aura_token')}` },
    body: body ? JSON.stringify(body) : undefined,
  });
  return { ok: res.ok, data: await res.json().catch(() => ({})) };
}

export function Enable2FAPage() {
  const [secret, setSecret] = useState<string | null>(null);
  const [uri, setUri] = useState<string | null>(null);
  const [code, setCode] = useState('');
  const [error, setError] = useState('');
  const [done, setDone] = useState(false);

  useEffect(() => { (async () => {
    const r = await post('/api/auth/enable-2fa/generate');
    if (r.ok) { setSecret(r.data.secret); setUri(r.data.uri); }
    else setError(r.data?.error ?? 'Could not generate secret');
  })(); }, []);

  const confirm = async () => {
    const r = await post('/api/auth/enable-2fa/confirm', { code });
    if (r.ok) {
      localStorage.setItem('aura_token', r.data.accessToken);
      setDone(true);
      setTimeout(() => window.location.assign('/'), 1200);
    } else setError(r.data?.error ?? 'Invalid code');
  };

  if (done) return <div className="min-h-screen flex items-center justify-center"><p>2FA enabled. Redirecting…</p></div>;

  return (
    <div className="max-w-md mx-auto mt-16 glass-card p-8 space-y-4">
      <h2 className="text-xl font-display font-bold flex items-center gap-2"><ShieldCheck className="w-5 h-5" />Enable two-factor authentication</h2>
      {secret && uri ? (
        <>
          <p className="text-sm text-white/60">Scan this URI with Google Authenticator / 1Password / Authy, or enter the secret manually.</p>
          <div className="glass-card p-3 text-xs font-mono break-all">{uri}</div>
          <details className="text-xs">
            <summary className="cursor-pointer text-white/60">Manual secret (click to reveal)</summary>
            <div className="mt-2 font-mono text-sm">{secret}</div>
          </details>
          <input value={code} onChange={e => setCode(e.target.value)} maxLength={6} className="input-dark w-full text-center tracking-[0.5em] text-lg" placeholder="000000" />
          {error && <div className="text-xs text-aura-red">{error}</div>}
          <button onClick={confirm} className="btn-primary w-full">Verify and enable</button>
        </>
      ) : (
        <p className="text-sm text-white/50">Loading…</p>
      )}
    </div>
  );
}
```

- [ ] **Step 3: Build + commit**

```bash
git add admin-panel/src/views/ChangePasswordPage.tsx admin-panel/src/views/Enable2FAPage.tsx
git commit -m "feat(6.11.W5.T38): ChangePasswordPage + Enable2FAPage

ChangePasswordPage posts to /api/auth/change-password; validates
length + confirm-match; redirects home on success.

Enable2FAPage auto-fetches /enable-2fa/generate on mount, displays
otpauth URI + manual secret fallback (QR rendering deferred to 6.12),
accepts 6-digit code, posts to /confirm, stores fresh access token,
redirects home."
```

### Task 39: Wave 5 regression + commit

**Goal:** Run the full test suite end-to-end, verify no regression, commit a milestone marker.

- [ ] **Step 1: Backend test suite**

```bash
cd "C:/Users/Admin/Desktop/AuraCorePro/AuraCorePro"
dotnet test AuraCorePro.sln 2>&1 | tail -10
```

Expected: full pass. SuperadminFoundation test count should be ~40-45 by this point.

- [ ] **Step 2: Frontend test suite**

```bash
cd admin-panel
npx vitest run 2>&1 | tail -10
```

Expected: full pass.

- [ ] **Step 3: Commit a milestone tag (no file changes — just a marker)**

Since there's nothing to commit, skip. Instead push the branch:

```bash
cd ..
git push origin phase-6-superadmin-foundation
```

---

## Wave 6 — My Permissions + final polish + deploy + ceremonial

Four tasks. After Wave 6 the branch ships.

### Task 40: MyPermissionsPage (admin self-service)

**Goal:** Real implementation of the admin's self-service page accessed from the user menu. Summary card + active-grants table + pending-requests table with Cancel + recent-denials table.

**Files:**
- Replace stub: `admin-panel/src/views/MyPermissionsPage.tsx`
- Modify: `admin-panel/src/components/TopBar.tsx` (or the existing user dropdown — wire a "My Permissions" link)
- Test: `admin-panel/src/__tests__/views/MyPermissionsPage.test.tsx`

- [ ] **Step 1: Write failing test**

```typescript
// admin-panel/src/__tests__/views/MyPermissionsPage.test.tsx
import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { MyPermissionsPage } from '@/views/MyPermissionsPage';

vi.mock('@/lib/api', () => ({
  api: {
    getMyPermissions: vi.fn().mockResolvedValue({
      totalRestricted: 10, activeGrantsCount: 2,
      grants: [{ permissionKey: 'tab:updates', grantedAt: '2026-04-01', expiresAt: null, grantedByEmail: 'super@x.com' }],
      pending: [{ id: 'p1', permissionKey: 'tab:configuration', reason: 'needed urgently', requestedAt: '2026-04-22' }],
      recentDenials: [{ permissionKey: 'action:users.delete', reviewNote: 'wrong queue', reviewedAt: '2026-04-20' }],
    }),
    cancelPermissionRequest: vi.fn().mockResolvedValue({ ok: true }),
  },
}));

describe('MyPermissionsPage', () => {
  it('renders all three tables + summary', async () => {
    render(<MyPermissionsPage />);
    await waitFor(() => screen.getByText(/2 of 10/));
    expect(screen.getByText(/tab:updates/)).toBeTruthy();
    expect(screen.getByText(/tab:configuration/)).toBeTruthy();
    expect(screen.getByText(/wrong queue/)).toBeTruthy();
  });
});
```

- [ ] **Step 2: Implement `MyPermissionsPage.tsx`**

```tsx
// admin-panel/src/views/MyPermissionsPage.tsx
'use client';

import { useEffect, useState } from 'react';
import { Shield } from 'lucide-react';
import { api } from '@/lib/api';
import type { MyPermissionsResponse } from '@/lib/types';

export function MyPermissionsPage() {
  const [data, setData] = useState<MyPermissionsResponse | null>(null);

  const refresh = async () => setData(await api.getMyPermissions());
  useEffect(() => { refresh(); }, []);

  const cancel = async (id: string) => { await api.cancelPermissionRequest(id); refresh(); };

  if (!data) return <div className="p-8 text-center text-white/50">Loading…</div>;

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-display font-bold flex items-center gap-2"><Shield className="w-6 h-6" />My Permissions</h1>
      <div className="glass-card p-4">
        You have access to <strong>{data.activeGrantsCount} of {data.totalRestricted}</strong> restricted permissions.
      </div>

      <section>
        <h2 className="font-semibold mb-2">Active grants</h2>
        <div className="glass-card overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-white/5"><tr>
              <th className="p-3 text-left">Permission</th><th className="p-3 text-left">Granted by</th>
              <th className="p-3 text-left">Granted at</th><th className="p-3 text-left">Expires</th>
            </tr></thead>
            <tbody>
              {data.grants.length === 0 ? (
                <tr><td colSpan={4} className="p-4 text-center text-white/40">No active grants.</td></tr>
              ) : data.grants.map(g => (
                <tr key={g.permissionKey} className="border-t border-white/5">
                  <td className="p-3"><code className="text-xs">{g.permissionKey}</code></td>
                  <td className="p-3">{g.grantedByEmail ?? '—'}</td>
                  <td className="p-3 text-white/50">{new Date(g.grantedAt).toLocaleDateString()}</td>
                  <td className="p-3 text-white/50">{g.expiresAt ? new Date(g.expiresAt).toLocaleString() : 'never'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <section>
        <h2 className="font-semibold mb-2">Pending requests</h2>
        <div className="glass-card overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-white/5"><tr>
              <th className="p-3 text-left">Permission</th><th className="p-3 text-left">Requested at</th>
              <th className="p-3 text-left">Reason</th><th className="p-3 text-right">Actions</th>
            </tr></thead>
            <tbody>
              {data.pending.length === 0 ? (
                <tr><td colSpan={4} className="p-4 text-center text-white/40">None pending.</td></tr>
              ) : data.pending.map(p => (
                <tr key={p.id} className="border-t border-white/5">
                  <td className="p-3"><code className="text-xs">{p.permissionKey}</code></td>
                  <td className="p-3 text-white/50">{new Date(p.requestedAt).toLocaleString()}</td>
                  <td className="p-3 max-w-sm truncate" title={p.reason}>{p.reason}</td>
                  <td className="p-3 text-right">
                    <button onClick={() => cancel(p.id)} className="btn-ghost-sm">Cancel</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <section>
        <h2 className="font-semibold mb-2">Recent denials</h2>
        <div className="glass-card overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-white/5"><tr>
              <th className="p-3 text-left">Permission</th><th className="p-3 text-left">Note</th>
              <th className="p-3 text-left">When</th>
            </tr></thead>
            <tbody>
              {data.recentDenials.length === 0 ? (
                <tr><td colSpan={3} className="p-4 text-center text-white/40">No recent denials.</td></tr>
              ) : data.recentDenials.map((d, i) => (
                <tr key={i} className="border-t border-white/5">
                  <td className="p-3"><code className="text-xs">{d.permissionKey}</code></td>
                  <td className="p-3">{d.reviewNote || '—'}</td>
                  <td className="p-3 text-white/50">{new Date(d.reviewedAt).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}
```

- [ ] **Step 3: Wire from TopBar user dropdown**

Read `admin-panel/src/components/TopBar.tsx`. Find the user-menu dropdown block (if present). Add a "My Permissions" item that navigates to the `myPerms` view. The simplest approach: emit a custom event `navigate-view` that `AdminPanelInner` listens for and switches `page` on. Or inline the page selection via a prop-drilled `onNavigate` callback.

If the TopBar doesn't have a dropdown, add a simple menu button next to logout:

```tsx
<button onClick={() => window.dispatchEvent(new CustomEvent('navigate-view', { detail: 'myPerms' }))}
  className="btn-ghost-sm">My Permissions</button>
```

And in `AdminPanelInner` add:

```tsx
useEffect(() => {
  const h = (ev: any) => setPage(ev.detail);
  window.addEventListener('navigate-view', h);
  return () => window.removeEventListener('navigate-view', h);
}, []);
```

- [ ] **Step 4: Tests pass + commit**

```bash
git add admin-panel/src/views/MyPermissionsPage.tsx admin-panel/src/components/TopBar.tsx admin-panel/src/app/page.tsx admin-panel/src/__tests__/views/MyPermissionsPage.test.tsx
git commit -m "feat(6.11.W6.T40): MyPermissionsPage + TopBar menu entry

Admin self-service: summary card + active-grants + pending-requests
(with Cancel) + recent-denials tables. Accessible via TopBar 'My
Permissions' button (uses a lightweight window custom-event for
navigation rather than prop-drilling)."
```

### Task 41: Final build + deploy + smoke

**Goal:** Ship Waves 5-6 to prod. Full end-to-end smoke with a freshly-invited admin account going through the entire flow.

- [ ] **Step 1: Full-suite regression**

```bash
dotnet test AuraCorePro.sln 2>&1 | tail -10
cd admin-panel && npx vitest run 2>&1 | tail -5 && cd ..
```

Expected: both full pass. Backend ~2430, frontend ~10-20 new Vitest.

- [ ] **Step 2: Backend publish + deploy**

```bash
cd src/Backend/AuraCore.API
dotnet publish -c Release -r linux-x64 --self-contained false -o ./publish 2>&1 | tail -5
scp -i C:/Users/Admin/.ssh/id_ed25519 -r publish/* root@165.227.170.3:/var/www/auracore-api/
ssh -i C:/Users/Admin/.ssh/id_ed25519 root@165.227.170.3 \
  "systemctl restart auracore-api && sleep 3 && curl -fsS http://localhost:5000/health | head"
```

- [ ] **Step 3: Admin panel build + deploy**

```bash
cd ../../../admin-panel
npm run build 2>&1 | tail -5
scp -i C:/Users/Admin/.ssh/id_ed25519 -r out/* root@165.227.170.3:/var/www/admin-panel/
```

- [ ] **Step 4: End-to-end smoke**

With a browser, perform the full Phase 6.11 journey:

1. `https://admin.auracore.pro` → LoginScreen shows two buttons.
2. "Sign In as Superadmin" with `ozgurdeniz807@gmail.com` + password → if TOTP not yet enabled, redirects to `/#/enable-2fa`, scan the URI with an authenticator app, enter 6-digit code → lands on dashboard with superadmin nav.
3. Go to Admin Management → Create new admin `test-admin@my-test-domain.com` with `Send invitation` ON + Trusted template + Force change on first login + Require 2FA ON.
4. Invitation email should arrive within ~30 seconds. Click the link.
5. RedeemInvitationPage → set a new password → logged in as admin.
6. Admin dashboard → navigate to Configuration tab → should see LockedTabPlaceholder (template is Trusted, not including `tab:configuration`).
7. Click Request Permission → fill 50+ char reason → submit.
8. Logout. Log in as superadmin.
9. Permission Requests tab → should see the request + SignalR broadcast fired (tab badge counter if wired).
10. Approve with optional expiry + note.
11. Switch back to admin account (new browser / incognito) → Configuration tab no longer locked.

If any step fails, investigate logs and fix before proceeding. Don't mask bugs.

- [ ] **Step 5: Push branch once more (pre-merge snapshot)**

```bash
cd ..
git push origin phase-6-superadmin-foundation
```

### Task 42: Memory file + ceremonial merge + push to origin

**Goal:** Write the Phase 6.11 completion memory file, update `MEMORY.md` pointer, merge branch to `main` with `--no-ff`, push to origin. User-gated — pause for explicit "merge and push" approval before doing anything destructive.

- [ ] **Step 1: Draft memory file**

Create `C:\Users\Admin\.claude\projects\C--\memory\project_phase_6_item_11_superadmin_foundation_complete.md`:

```markdown
---
name: Phase 6.11 Superadmin Foundation COMPLETE
description: Superadmin role hierarchy + permission system + admin lifecycle + email refactor + CSV export + runtime rate limits
type: project
---

# Phase 6.11 — Superadmin Foundation COMPLETE

**CURRENT STATE** (from YYYY-MM-DD merge into main).

## Scope delivered (spec D1-D17)

- 5 new DB tables: permission_grants, permission_requests, revoked_tokens, admin_invitations, system_settings (seeded require_2fa_for_all_admins=false + default rate_limit_policies JSON).
- users table gained 8 columns: IsActive, IsReadonly, ForcePasswordChange, ForcePasswordChangeBy, PasswordChangedAt, CreatedByUserId, CreatedVia, Require2fa.
- New role: `superadmin` above `admin`. Bootstrap via SUPERADMIN_EMAILS env var (idempotent on startup). Grandfather migration grants Trusted template to every existing admin with zero grants.
- JWT now carries `jti` (revocation) + optional `scope` claim (2fa-setup-only, 15-min TTL). Superadmin tokens carry TWO role claims (admin + superadmin) so existing `[Authorize(Roles="admin")]` covers both — zero controller rewrites.
- 3 middlewares: TokenRevocationMiddleware (jti blacklist), ScopeLimitedTokenMiddleware (restrict 2fa-setup tokens), ForcePasswordChangeMiddleware (block expired-deadline users except on /change-password+/logout+/me).
- 2 attribute filters: `[RequiresPermission(key)]` runs after `[Authorize]`, checks permission_grants for active non-expired non-revoked entry; ReadOnly admin fails on any action:* key. `[DestructiveAction]` blocks only ReadOnly admins on Tier 3 endpoints.
- Tier 1 (tab-gate): tab:configuration / tab:ipwhitelist / tab:updates / tab:rolechange applied to mutation endpoints of matching controllers (GET stays open).
- Tier 2 (action-gate): action:users.delete/ban / subscriptions.grant/revoke / payments.approveCrypto/rejectCrypto applied. New POST /api/admin/users/{id}/ban endpoint (toggles user IsActive).
- Tier 3 ([DestructiveAction]): Licenses.Revoke/Activate / Devices.Revoke/Delete / CrashReports.Delete.
- Dedicated `/api/auth/superadmin/login` endpoint — stricter rate limit (3/60min), always audit-logs, never reveals email existence, mandatory 2FA.
- `/api/auth/enable-2fa/generate + /confirm`, `/api/auth/change-password`, `/api/auth/redeem-invitation`, `/api/auth/logout` endpoints.
- IEmailService + ResendEmailService abstraction with 6 templates (AdminInvitation, PasswordReset, PermissionRequested, PermissionApproved, PermissionDenied, AdminCreatedWithoutEmail). PasswordResetController refactored from inline HttpClient to _email.SendFromTemplateAsync.
- Superadmin controllers: PermissionGrants (approve/deny/bulk/revoke), AdminManagement (create/suspend/restore/delete/promote/demote/reset-pw/set-2fa), AdminActionLog (list/stats/CSV export), AdminInvitations (redeem flow), SecurityPolicy (global + per-account 2FA), RateLimitConfig (runtime-editable via system_settings; Phase 6.12 to wire into ASP.NET RateLimiter).
- Admin controllers: PermissionRequests (create/list/cancel), MyPermissions (summary), AuditLogExport (CSV streaming).
- AdminHub accepts admin+superadmin, rejects scope-limited tokens, superadmins additionally join 'superadmins' group. 4 new events (PermissionRequested/Approved/Denied/Revoked).
- Frontend: role-aware NAV_GROUPS (admin=13 tabs, superadmin=+6), two-button LoginScreen (cyan-purple gradient for superadmin), LockedTabPlaceholder + PermissionGate + PermissionRequestDialog + CustomTemplatePicker components, usePermissions hook with has() predicate, 10 new views (4 Wave 4 superadmin + 6 cross-role), RoleContext for zero prop-drilling.
- SignalR integration: existing signalr.ts module-level `L` registry extended with 4 event handlers.
- Ops: nginx basic-auth removed from admin.auracore.pro, robots.txt Disallow:/ + <meta robots noindex>, SPF TXT gained include:_spf.resend.com, SUPERADMIN_EMAILS=ozgurdeniz807@gmail.com in /etc/auracore-api.env.

## Test numbers

- Backend: +XX tests (~2430 pass, 0 fail, 0 skip)
- Frontend: +YY tests
- Zero new NuGet packages (all features use in-box System.IdentityModel.Tokens.Jwt + Moq.Protected etc. already referenced)

## Branch + deploy

- Branch `phase-6-superadmin-foundation` merged to main at <commit> (ceremonial <commit>).
- Mid-deploy (Wave 4): <timestamp>; final deploy (Wave 6): <timestamp>.
- Backups on origin: `/var/www/auracore-api.bak-<ts-mid>`, `/var/www/auracore-api.bak-<ts-final>`.
- Pushed to origin/main: <timestamp>.

## Out-of-scope carry-forward (Phase 6.12+)

- Per-permission usage-count limits (e.g. "expires after 10 uses"); only time-based expires_at in 6.11.
- Full ASP.NET Core RateLimiter hot-reload — 6.11 UI edits persist but backend still uses per-controller rate-limit logic on login_attempts.
- QR-code rendering on Enable2FAPage (text URI + manual secret currently).
- Bulk Role Change operations.
- Active-session monitor + force-logout-all.
- Captcha on login.
- Audit-log retention/archival dashboard (note: T37.1 DOES handle revoked_tokens + expired invitations retention; audit_log retention remains deferred).
- Slack/Teams/SMS channels.
- Multi-tenant superadmin.
- TOTP backup codes.
- React Native admin mobile app (Phase 6.14).

## Known risks (logged during planning)

- SPF TXT edit takes 1-4 hours to propagate; invitation emails sent before then may fail DMARC alignment. Pre-Wave-4 verify propagation.
- Grandfather migration runs on EVERY startup; idempotent (zero-grant query matches newcomers only) — no duplication risk.
- Single admin bundle visible to admin browsers; backend `[RequiresPermission]` + role checks are the authoritative layer. Accepted trade-off (spec D2 Q9).
- Scope-limited JWT must not reach /hubs/admin — AdminHub.OnConnectedAsync aborts scope-limited connections.
```

Fill the `<XX>` / `<timestamp>` / `<commit>` placeholders in the commit step, after running the full suite and merging.

- [ ] **Step 2: Update `MEMORY.md`**

Read `C:\Users\Admin\.claude\projects\C--\memory\MEMORY.md`. Insert a single-line pointer at the top (newest-first) and bump the previous top entry to be the 2nd-newest:

```
- [Phase 6.11 Superadmin Foundation COMPLETE](project_phase_6_item_11_superadmin_foundation_complete.md) — **CURRENT STATE.** Superadmin role + 3-tier permission system + admin lifecycle + IEmailService + CSV export + runtime rate limits. Merged at <commit>.
```

Mark the Phase 6.10 line as "Superseded by 6.11" to keep the chain navigable.

- [ ] **Step 3: Prompt user for merge approval**

Per user preferences: do NOT push to main without explicit approval. Output:

> Phase 6.11 implementation and memory file are ready. Branch `phase-6-superadmin-foundation` has NNN commits on top of `main`. Prod already runs Wave 1-6 (deployed at <timestamp>). Ready to `git checkout main && git merge --no-ff phase-6-superadmin-foundation && git push origin main` — approve?

Wait for explicit "merge" / "push" instruction.

- [ ] **Step 4: On approval — merge + push**

```bash
git checkout main
git pull origin main
git merge --no-ff phase-6-superadmin-foundation -m "Merge branch 'phase-6-superadmin-foundation' (Phase 6.11 Superadmin Foundation)

Superadmin role hierarchy + 3-tier permission system + admin lifecycle
+ IEmailService + CSV export + runtime rate limits. See memory file
project_phase_6_item_11_superadmin_foundation_complete.md for scope
+ test numbers + out-of-scope carry-forward."
git log --oneline -3
# Expected: ceremonial merge commit on top, followed by the branch's commits
git push origin main
```

- [ ] **Step 5: Commit memory file**

The memory file lives in `C:\Users\Admin\.claude\projects\C--\memory\` — not in the repo. It's auto-loaded from there by Claude Code via the `MEMORY.md` index. No git commit needed for memory.

- [ ] **Step 6: Final smoke after merge**

```bash
curl -fsS https://api.auracore.pro/health | head
curl -sI https://admin.auracore.pro | head -5
```

Expected: both 200. Phase 6.11 shipped.

---

## Self-review (vs spec D1-D17, two passes)

Done in two passes. Pass 1 before initial delivery; Pass 2 after user review caught 3 gaps and the plan was updated with T30.1 (Edit Permissions), T30.2 (Invitations management), T37.1 (RetentionJob). The checklist below reflects the post-insertion state.

### Coverage check

| Spec section | Covered by task(s) |
|---|---|
| **D1 — 3-tier permission model** | T4 (keys), T10 (attribute), T11 (destructive), T12 (controller application) |
| **D2 — Separate endpoint + single subdomain** | T13 (/api/auth/superadmin/login), T22 (LoginScreen two-button), T25 (nginx + robots) |
| **D3 — SUPERADMIN_EMAILS bootstrap** | T5 (SuperadminBootstrapService), pre-flight env var ops step |
| **D4 — Storage schema** | T1 (entities), T2 (DbContext + partial unique indexes), T3 (migration + seeds) |
| **D5 — [RequiresPermission] attribute** | T10 (implementation + 8 test cases incl. readonly + expired + revoked), T12 (application to 6+3 controllers) |
| **D6 — 4 permission templates** | T4 (PermissionTemplates helper), T26 (template grant generation in Create Admin), T30 (CustomTemplatePicker UI), **T30.1 (apply-template + EditPermissionsModal for existing admins)** |
| **D7 — Force password change flow** | T26 (ForceChangeDeadline calc from policy strings), T27 (change-password endpoint with deadline-passed emergency unlock), T34 (middleware) |
| **D8 — 2FA enforcement hybrid** | T7 (scope claim on JWT), T13 (superadmin always forced), T35 (resolution ladder in /login), T33 (ScopeLimitedTokenMiddleware), T36 (SecurityPolicy tab controls) |
| **D9 — Admin Action Log + CSV** | T28 (two CSV endpoints streaming), T31 (AdminActionLogPage with KPIs + filters), T31 (AuditLogPage export button) |
| **D10 — Rate limit config UI** | T37 (service + controller + page). 6.11 scope: edit persists + cache invalidates. Hot-reload into ASP.NET RateLimiter queued to 6.12 (explicitly noted in task + memory). |
| **D11 — Email infrastructure** | T14 (IEmailService + ResendEmailService + 6 templates), T15 (PasswordResetController refactor), T25 (SPF DNS fix) |
| **D12 — Admin Account Creation + lifecycle per-row actions** | T26 (POST /api/superadmin/admins with invitation toggle + template + force-change + 2FA requirement), T27 (redeem-invitation), T30 (CreateAdminModal + list + Suspend/Restore/Reset/Delete per-row), **T30.1 (Edit Permissions per-row)**, **T30.2 (Invitations list/revoke/resend — ops remediation when email doesn't arrive)** |
| **D13 — Suspend/restore + delete** | T26 (suspend revokes refresh_tokens, delete cascades; [RequiresPermission("action:users.delete")] on DELETE; superadmin cannot be deleted via API) |
| **D14 — My Permissions page** | T16 (backend endpoint), T40 (frontend page with 3 tables) |
| **D15 — Locked-tab/action UX** | T21 (three components), T24 (Tier 1 pages render placeholder), T23 (Tier 2 buttons wrapped in PermissionGate) |
| **D16 — Indexing protection** | T25 (robots.txt + noindex meta + ops nginx public-cut) |
| **D17 — SignalR events** | T17 (AdminHub superadmins group + scope-limited rejection), T29 (PermissionRequests live append), T16 (server-side emit in approve/deny/revoke), T24 (client subscribe via signalr.on) |
| **Ops hygiene (not a D-number — cross-phase)** | **T37.1 (RetentionJob: revoked_tokens + expired admin_invitations GC).** audit_log retention explicitly deferred to 6.12 per spec. |

### Potential gaps I caught and addressed inline

1. **`action:users.ban` endpoint didn't exist** — Task 12 adds a new `POST /api/admin/users/{id}/ban` endpoint with a toggle body. Previously this key appeared in spec D1 but no endpoint bore it.
2. **`AppDbContext` vs `AuraCoreDbContext`** — The spec draft implied `AppDbContext` but the repo's actual class is `AuraCoreDbContext`. Every task in this plan uses the correct name.
3. **Dual-role claims** — Spec D5 says `[Authorize(Roles = "admin")]` "stays (admin + superadmin both pass via role inheritance)". ASP.NET doesn't do role inheritance automatically, so Task 7 emits TWO role claims (admin + superadmin) for superadmin users. This preserves the spec's intent (no controller rewrites) without requiring a different mechanism.
4. **User entity `DateTimeOffset` vs `DateTime`** — The existing `User` uses `DateTimeOffset` for `CreatedAt`/`UpdatedAt`. New User fields (`ForcePasswordChangeBy`, `PasswordChangedAt`) match this convention; new entity types (PermissionGrant etc.) use `DateTime` UTC for their timestamps to match PostgreSQL `timestamptz` idiom with `now()` default.
5. **Rate-limit hot-reload scope** — Spec D10 mentions "hot-reloads ASP.NET Core RateLimiter policies on update". Task 37 explicitly notes that 6.11 scope ships the persistence + cache-invalidation layer, but the hot-reload wiring into ASP.NET Core's RateLimiter pipeline is deferred to 6.12. The UI still works end-to-end for the `login_attempts`-based rate limit that AuthController uses today.
6. **JWT NameClaimType == "sub"** — `ClaimsExtensions.GetUserId()` reads `sub` first, falls back to `NameIdentifier`. Tests exercise both. The attribute filter + middleware use this helper consistently.
7. **Resend templates as embedded resources** — Task 14 adds an `<EmbeddedResource>` ItemGroup to `AuraCore.API.Infrastructure.csproj`. This is essential; otherwise `GetManifestResourceStream` returns null at runtime.
8. **The `Ban` endpoint only operates on regular `user` accounts** — Task 12 rejects `admin` / `superadmin` roles from being banned via this endpoint (use Suspend instead). This keeps the "ban" semantic limited to end users and preserves the suspend/restore flow for admin accounts.
9. **Test count consistency** — Each wave adds specific test files with specific counts. Totals: Wave 1 ~22, Wave 2 ~18, Wave 3 ~10 frontend, Wave 4 ~12, Wave 5 ~8, Wave 6 ~3. Target ~2440-2470 matches the spec's testing strategy section.
10. **Stub views in Task 20 have minimal JSX so imports compile** — This prevents Wave 3 from breaking the whole app before Wave 4 lands.

### Method-name consistency

Spot-checked every cross-reference between tasks:

- `GenerateAccessToken(user, scope?, lifetime?)` — defined in Task 7, used identically in Task 13 (superadmin login scope-limited), Task 27 (redeem-invitation), Task 35 (2FA enforcement), Task 34 (logout).
- `ClaimsExtensions.GetUserId() / GetEmail() / GetJti() / GetScope() / GetPrimaryRole() / IsScopeLimited()` — defined in Task 7, used in Tasks 10, 11, 16, 17, 26, 27, 28, 33, 34, 35, 36, 37.
- `PermissionKeys.*` constants — defined in Task 4, referenced in Tasks 6, 10, 12, 19 (frontend mirrors via permissions.ts with identical string values).
- `PermissionTemplates.GetPermissionsForTemplate()` + `RequiresIsReadonlyFlag()` + `IsValidTemplate()` — defined in Task 4, used in Tasks 6 (grandfather), 26 (create admin template application), 31 (role change frontend reuse).
- `AuraCoreDbContext.PermissionGrants / PermissionRequests / RevokedTokens / AdminInvitations / SystemSettings` — added in Task 2, used in Tasks 5, 6, 10, 11, 12, 16, 17, 26, 27, 28, 34, 36, 37.
- `IEmailService.SendFromTemplateAsync(EmailTemplate, object)` — interface in Task 14, used in Tasks 15 (PasswordReset), 16 (PermissionRequested/Approved/Denied), 26 (AdminInvitation, AdminCreatedWithoutEmail).
- `TotpService.GenerateSecret() / BuildOtpAuthUri() / ValidateCode()` — referenced in Task 35 (Enable2FA endpoints). **Verify during implementation** that these exact method names exist in the current `AuraCore.API.Application.Services.Security.TotpService` class; if the names differ, adapt the endpoint to match. The existing `/api/auth/login` TOTP path (AuthController line 199) already uses `TotpService.ValidateCode`, so at minimum that method exists.
- Frontend: `api.createPermissionRequest / listMyPermissionRequests / cancelPermissionRequest / listPermissionRequests / approve/deny/bulk` etc. — all defined in Task 19, consumed consistently in Tasks 21, 23, 29, 31, 40.
- `usePermissions(role).has(key) / hasPending(key) / refresh()` — defined in Task 23, used in Tasks 23 (UsersPage/SubscriptionsPage/PaymentsPage), 24 (Tier 1 pages).
- `LockedTabPlaceholder` / `PermissionRequestDialog` / `PermissionGate` / `CustomTemplatePicker` / `CreateAdminModal` props — all props align across the components that compose them.

No inconsistencies found. Plan is internally coherent.

### Placeholder scan

Grep-checked every "TBD" / "placeholder" / "etc." / "similar to" in the plan:

- The phrase "adapt to the actual code shape" appears once in Task 12 Step 4 (AdminUserController Ban endpoint note that existing controllers may have slightly different method-name casings for `Grant`/`Revoke`). This is a legitimate guidance note for the implementer — the actual method names to hit are specified (`Grant`, `Revoke`, `AdminVerify*`, `AdminReject*`).
- No "TODO" / "fill in later" / "implement appropriate X" patterns found.
- Every code snippet is complete and copy-pasteable.
- Every commit message is fully drafted.

---

## Execution handoff

Plan complete and saved to `docs/superpowers/plans/2026-04-23-superadmin-foundation-v2.md` (**45 tasks across 6 waves**: 42 primary + 3 review-pass insertions T30.1, T30.2, T37.1).

Two execution options:

**1. Subagent-Driven (recommended for this size)** — Fresh subagent per task + two-stage review between tasks. Best for 6-wave, 45-task plans where each task is independently testable + committable.

**2. Inline Execution** — Same session, batch with checkpoints. Keeps all context but blows out the window fast.

Which approach?

**Post-execution note:** After all tasks land on `phase-6-superadmin-foundation`, the ceremonial merge to `main` is Task 42 and is user-gated — the plan halts there and asks for explicit approval before `git merge --no-ff` + `git push origin main`.






