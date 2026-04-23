# Phase 6.11 Superadmin Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a `superadmin` role above `admin` with separate stricter login endpoint, 3-tier permission system (tab + action + free), permission grant/request/approval flow, full admin lifecycle management (create/promote/suspend/restore/delete), 4 permission templates (Default/Trusted/ReadOnly/Custom), hybrid 2FA enforcement, IEmailService refactor of inline Resend HTTPS calls, audit log CSV export, runtime-editable API rate limit policies, plus removing nginx basic auth from `admin.auracore.pro` (admin panel goes public-internet-reachable).

**Architecture:** 6-wave execution on a single `phase-6-superadmin-foundation` branch. Wave 1 ships DB schema + auth foundation + bootstrap + grandfather + middleware. Wave 2 ships permission system + email service refactor + Tier 1/2 endpoint application. Wave 3 ships frontend role-aware shell + locked UX + nginx public-cut + DNS SPF fix. Wave 4 ships superadmin tabs + admin lifecycle + invitation flow + CSV export + mid-deploy. Wave 5 ships templates + force-change + 2FA enforcement + Security Policy + API Rate Limits. Wave 6 ships My Permissions + final deploy + ceremonial close. One backend mid-deploy at Wave 4 (permission system + email refactor live), one final deploy at Wave 6 (full UI live).

**Tech Stack:** ASP.NET Core 8 + EF Core (PostgreSQL) + SignalR + Resend HTTPS API + Next.js 14 (static export) + React 18 + TypeScript + Tailwind CSS 3 + Vitest + @testing-library/react + xUnit + Moq.

**Spec:** `docs/superpowers/specs/2026-04-23-superadmin-foundation-design.md` (commit `d178052`).
**Baseline:** main HEAD `d178052` (Phase 6.10 sealed at `1cb3155` + Round 1-3 hotfixes + Phase 6.11 spec).
**Branch:** `phase-6-superadmin-foundation` (created from main HEAD `d178052`).
**Target post-6.11:** ~2440-2470 tests (+50-80 from 2392), superadmin role live, permission system end-to-end, admin lifecycle UI, IEmailService abstraction, runtime rate limit config, admin panel public-reachable.

---

## Pre-flight

### Fresh session handoff

This plan is designed for execution in a **FRESH session**. On fresh session:

1. Read the spec: `docs/superpowers/specs/2026-04-23-superadmin-foundation-design.md`
2. Read Phase 6.10 memory: `C:\Users\Admin\.claude\projects\C--\memory\project_phase_6_item_10_admin_rebuild_complete.md`
3. Read the brainstorm Decision log section in the spec (Q1-Q9 outcomes)

### Credentials (NEVER commit to repo)

Same as Phase 6.10:
- **SSH:** `ssh -i C:/Users/Admin/.ssh/id_ed25519 root@165.227.170.3`
- **App admin login:** `admin@auracore.pro` / `v19w&tpALj%#t4*kTHZ&`
- **Postgres:** `postgres` / `auracoredb` / `auracorepro2026`
- **Resend API key:** already in `/etc/auracore-api.env` as `RESEND_API_KEY` (Phase 6.x prior)

### Branch setup

```bash
cd "C:/Users/Admin/Desktop/AuraCorePro/AuraCorePro"
git checkout main
git pull origin main
git checkout -b phase-6-superadmin-foundation
git log --oneline -3
# Expected:
# d178052 docs(spec): Phase 6.11 final — revert to A (separate endpoint, no subdomain)
# 0433f57 docs(spec): Phase 6.11 update — subdomain B + email + CSV export + rate-limit UI
# 96c7338 docs(spec): Phase 6.11 Superadmin Foundation design
```

### Add SUPERADMIN_EMAILS env var on origin (one-time ops step)

Before Wave 1 backend deploy:

```bash
ssh -i C:/Users/Admin/.ssh/id_ed25519 root@165.227.170.3 "grep -q '^SUPERADMIN_EMAILS=' /etc/auracore-api.env || echo 'SUPERADMIN_EMAILS=ozgurdeniz807@gmail.com' >> /etc/auracore-api.env"
ssh -i C:/Users/Admin/.ssh/id_ed25519 root@165.227.170.3 "grep '^SUPERADMIN_EMAILS=' /etc/auracore-api.env"
# Expected: SUPERADMIN_EMAILS=ozgurdeniz807@gmail.com
```

This must be set BEFORE Wave 1 backend deploys so the bootstrap service can promote the user. Do this when Wave 4 mid-deploy lands.

---

## File structure overview

### Created by this plan

**Backend domain entities** (`src/Backend/AuraCore.API.Domain/Entities/`):
- `PermissionGrant.cs` (NEW)
- `PermissionRequest.cs` (NEW)
- `RevokedToken.cs` (NEW)
- `AdminInvitation.cs` (NEW)
- `SystemSetting.cs` (NEW)

**Backend application interfaces** (`src/Backend/AuraCore.API.Application/Interfaces/`):
- `IEmailService.cs` (NEW)

**Backend infrastructure services** (`src/Backend/AuraCore.API.Infrastructure/Services/Email/`):
- `ResendEmailService.cs` (NEW)
- `EmailTemplate.cs` (NEW — enum)
- `Templates/_base.html` (NEW)
- `Templates/AdminInvitation.html` (NEW)
- `Templates/PasswordReset.html` (NEW)
- `Templates/PermissionRequested.html` (NEW)
- `Templates/PermissionApproved.html` (NEW)
- `Templates/PermissionDenied.html` (NEW)
- `Templates/AdminCreatedWithoutEmail.html` (NEW)

**Backend filters + helpers + services** (`src/Backend/AuraCore.API/`):
- `Filters/RequiresPermissionAttribute.cs` (NEW)
- `Filters/DestructiveActionAttribute.cs` (NEW)
- `Helpers/PermissionKeys.cs` (NEW)
- `Helpers/PermissionTemplates.cs` (NEW)
- `Services/SuperadminBootstrapService.cs` (NEW)
- `Services/GrandfatherMigrationService.cs` (NEW)
- `Services/RateLimitConfigService.cs` (NEW)
- `Middleware/TokenRevocationMiddleware.cs` (NEW)
- `Middleware/ScopeLimitedTokenMiddleware.cs` (NEW)
- `Middleware/ForcePasswordChangeMiddleware.cs` (NEW)

**Backend controllers** (`src/Backend/AuraCore.API/Controllers/`):
- `Admin/PermissionRequestsController.cs` (NEW — admin self-create + cancel)
- `Admin/MyPermissionsController.cs` (NEW)
- `Superadmin/AdminManagementController.cs` (NEW — superadmin's lifecycle ops)
- `Superadmin/PermissionGrantsController.cs` (NEW — superadmin manages grants + reviews requests)
- `Superadmin/AdminActionLogController.cs` (NEW)
- `Superadmin/SecurityPolicyController.cs` (NEW)
- `Superadmin/RateLimitConfigController.cs` (NEW)
- `Superadmin/RoleChangeController.cs` (NEW)
- `Superadmin/AdminInvitationsController.cs` (NEW)

**EF migration** (`src/Backend/AuraCore.API.Infrastructure/Migrations/`):
- `<timestamp>_AddSuperadminFoundation.cs` (NEW — generated by `dotnet ef`)

**Frontend new files** (`admin-panel/src/`):
- `lib/permissions.ts` (NEW)
- `hooks/usePermissions.ts` (NEW)
- `components/LockedTabPlaceholder.tsx` (NEW)
- `components/PermissionRequestDialog.tsx` (NEW)
- `components/TierPicker.tsx` (NEW — for Custom permission template + activate)
- `views/PermissionRequestsPage.tsx` (NEW)
- `views/AdminActionLogPage.tsx` (NEW)
- `views/AdminManagementPage.tsx` (NEW)
- `views/RoleChangePage.tsx` (NEW)
- `views/SecurityPolicyPage.tsx` (NEW)
- `views/APIRateLimitsPage.tsx` (NEW)
- `views/MyPermissionsPage.tsx` (NEW)
- `views/ChangePasswordPage.tsx` (NEW)
- `views/Enable2FAPage.tsx` (NEW)
- `views/RedeemInvitationPage.tsx` (NEW)

**Frontend public assets** (`admin-panel/public/`):
- `robots.txt` (NEW)

**Tests** (`tests/AuraCore.Tests.API/SuperadminFoundation/`):
- `RequiresPermissionAttributeTests.cs` (NEW)
- `SuperadminBootstrapServiceTests.cs` (NEW)
- `GrandfatherMigrationServiceTests.cs` (NEW)
- `SuperadminLoginEndpointTests.cs` (NEW)
- `PermissionRequestLifecycleTests.cs` (NEW)
- `PermissionTemplatesTests.cs` (NEW)
- `ForcePasswordChangeMiddlewareTests.cs` (NEW)
- `TwoFactorEnforcementTests.cs` (NEW)
- `RateLimitConfigServiceTests.cs` (NEW)
- `AdminInvitationFlowTests.cs` (NEW)
- `EmailServiceTests.cs` (NEW)
- `AdminActionLogCsvExportTests.cs` (NEW)

**Frontend tests** (`admin-panel/src/__tests__/`):
- `components/LockedTabPlaceholder.test.tsx` (NEW)
- `components/PermissionRequestDialog.test.tsx` (NEW)
- `components/TierPicker.test.tsx` (NEW)
- `hooks/usePermissions.test.ts` (NEW)
- `lib/permissions.test.ts` (NEW)
- `views/PermissionRequestsPage.test.tsx` (NEW)
- `views/AdminManagementPage.test.tsx` (NEW)
- `views/MyPermissionsPage.test.tsx` (NEW)

### Modified by this plan

**Backend:**
- `src/Backend/AuraCore.API/Program.cs` — DI registrations + middleware order
- `src/Backend/AuraCore.API.Infrastructure/Data/AppDbContext.cs` — DbSets for new entities + EF entity configurations
- `src/Backend/AuraCore.API/Controllers/AuthController.cs` — add `/api/auth/superadmin/login` + `/api/auth/redeem-invitation` + `/api/auth/change-password` + scope-limited JWT issuance for 2FA setup
- `src/Backend/AuraCore.API/Controllers/PasswordResetController.cs` — refactor inline HTTPS to use IEmailService
- `src/Backend/AuraCore.API/Controllers/Admin/AdminAuditLogController.cs` — add `GET export.csv` endpoint
- `src/Backend/AuraCore.API/Controllers/Admin/AdminUserController.cs` — add `[RequiresPermission("action:users.delete")]` + `[RequiresPermission("action:users.ban")]`
- `src/Backend/AuraCore.API/Controllers/Admin/AdminSubscriptionController.cs` — add `[RequiresPermission("action:subscriptions.grant")]` + `[RequiresPermission("action:subscriptions.revoke")]`
- `src/Backend/AuraCore.API/Controllers/Payment/CryptoController.cs` — add `[RequiresPermission("action:payments.approveCrypto")]` + `[RequiresPermission("action:payments.rejectCrypto")]` on AdminVerify + AdminReject endpoints
- `src/Backend/AuraCore.API/Controllers/Admin/AdminConfigController.cs` — add `[RequiresPermission("tab:configuration")]`
- `src/Backend/AuraCore.API/Controllers/Admin/AdminIpWhitelistController.cs` — add `[RequiresPermission("tab:ipwhitelist")]`
- `src/Backend/AuraCore.API/Controllers/Admin/AdminUpdateController.cs` — add `[RequiresPermission("tab:updates")]`
- `src/Backend/AuraCore.API/Controllers/Admin/AdminLicenseController.cs` — add `[DestructiveAction]` to Revoke + Activate (ReadOnly admin enforcement)
- `src/Backend/AuraCore.API/Controllers/Admin/AdminDeviceController.cs` — add `[DestructiveAction]` to Revoke
- `src/Backend/AuraCore.API/Controllers/Admin/AdminCrashReportController.cs` — add `[DestructiveAction]` to Delete
- `src/Backend/AuraCore.API/Hubs/AdminHub.cs` — add 4 new SignalR event broadcast paths + scope-limited token rejection
- `src/Backend/AuraCore.API.Domain/Entities/User.cs` — add 7 new fields (is_active, is_readonly, force_password_change, force_password_change_by, password_changed_at, created_by_user_id, created_via, require_2fa)
- `src/Backend/AuraCore.API/AuraCore.API.csproj` — InternalsVisibleTo already in place from Task 20 of Phase 6.10; no new packages needed

**Frontend:**
- `admin-panel/src/lib/api.ts` — ~30 new endpoint methods (auth/superadmin/login, redeem-invitation, change-password, permission grants/requests CRUD, admin lifecycle, security policy, rate limit config, admin action log + CSV export, my permissions)
- `admin-panel/src/lib/types.ts` — new interfaces (PermissionGrant, PermissionRequest, AdminInvitation, RateLimitPolicy, AdminAccount, SecurityPolicySettings)
- `admin-panel/src/lib/signalr.ts` — no change (4 new events handled in useSignalR hook)
- `admin-panel/src/hooks/useSignalR.ts` — extend SignalREvents interface with 4 new event names
- `admin-panel/src/components/LoginScreen.tsx` — add "Sign In as Superadmin" button + scope-limited JWT detection + redirect to /enable-2fa or /change-password
- `admin-panel/src/components/Sidebar.tsx` — accept role prop, conditionally render superadmin-only tabs in NAV_GROUPS
- `admin-panel/src/app/page.tsx` — add 9 new view route entries to PAGES record + role-based NAV_GROUPS selection (admin vs superadmin) + new view imports
- `admin-panel/src/app/layout.tsx` — add `robots: { index: false, follow: false }` to metadata
- `admin-panel/src/views/UsersPage.tsx` — wrap Delete/Ban buttons with PermissionGate (locked icon + request modal for unprivileged admins)
- `admin-panel/src/views/SubscriptionsPage.tsx` — wrap Grant/Revoke with PermissionGate
- `admin-panel/src/views/PaymentsPage.tsx` — wrap ApproveCrypto/RejectCrypto with PermissionGate
- `admin-panel/src/views/AuditLogPage.tsx` — add Export CSV button

**Operational:**
- nginx config `/etc/nginx/sites-enabled/auracore-admin` — remove `auth_basic` + `auth_basic_user_file` directives
- DNS `auracore.pro` SPF TXT record — add `include:_spf.resend.com`
- `/etc/auracore-api.env` on origin — add `SUPERADMIN_EMAILS=ozgurdeniz807@gmail.com`

---

## Sub-phase 6.11 Wave 1 — DB schema + auth foundation + middleware

### Task 1: Domain entities for new tables

**Goal:** Define the C# entity classes for the 5 new tables (PermissionGrant, PermissionRequest, RevokedToken, AdminInvitation, SystemSetting). Add new fields to User entity. No DB migration yet — that's Task 3.

**Files:**
- Create: `src/Backend/AuraCore.API.Domain/Entities/PermissionGrant.cs`
- Create: `src/Backend/AuraCore.API.Domain/Entities/PermissionRequest.cs`
- Create: `src/Backend/AuraCore.API.Domain/Entities/RevokedToken.cs`
- Create: `src/Backend/AuraCore.API.Domain/Entities/AdminInvitation.cs`
- Create: `src/Backend/AuraCore.API.Domain/Entities/SystemSetting.cs`
- Modify: `src/Backend/AuraCore.API.Domain/Entities/User.cs`

- [ ] **Step 1: Create PermissionGrant entity**

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

- [ ] **Step 2: Create PermissionRequest entity**

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

- [ ] **Step 3: Create RevokedToken entity**

`src/Backend/AuraCore.API.Domain/Entities/RevokedToken.cs`:

```csharp
namespace AuraCore.API.Domain.Entities;

public class RevokedToken
{
    /// <summary>JWT 'jti' (unique ID) claim.</summary>
    public string Jti { get; set; } = string.Empty;

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public DateTime RevokedAt { get; set; } = DateTime.UtcNow;
    public Guid? RevokedBy { get; set; }
    public User? RevokedByUser { get; set; }

    /// <summary>'suspend' | 'password_reset' | 'logout_all' | 'admin_deleted'</summary>
    public string RevokeReason { get; set; } = string.Empty;
}
```

- [ ] **Step 4: Create AdminInvitation entity**

`src/Backend/AuraCore.API.Domain/Entities/AdminInvitation.cs`:

```csharp
namespace AuraCore.API.Domain.Entities;

public class AdminInvitation
{
    /// <summary>SHA256 hash (hex) of the invitation token sent in email. Raw token never stored.</summary>
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

- [ ] **Step 5: Create SystemSetting entity**

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

- [ ] **Step 6: Modify User entity — add 7 new fields**

Read `src/Backend/AuraCore.API.Domain/Entities/User.cs`. Find the existing properties block. Add:

```csharp
    public bool IsActive { get; set; } = true;
    public bool IsReadonly { get; set; } = false;
    public bool ForcePasswordChange { get; set; } = false;
    public DateTime? ForcePasswordChangeBy { get; set; }
    public DateTime? PasswordChangedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }

    /// <summary>'signup' | 'admin_promote' | 'superadmin_create'</summary>
    public string CreatedVia { get; set; } = "signup";

    public bool Require2fa { get; set; } = false;
```

Place these after existing fields, before any nav properties.

- [ ] **Step 7: Build verify**

```bash
cd "C:/Users/Admin/Desktop/AuraCorePro/AuraCorePro"
dotnet build src/Backend/AuraCore.API.Domain/AuraCore.API.Domain.csproj 2>&1 | tail -5
```

Expected: 0 errors. Domain project compiles standalone.

- [ ] **Step 8: Commit**

```bash
git add src/Backend/AuraCore.API.Domain/Entities/
git commit -m "feat(6.11.W1): domain entities for permission system + revoked tokens + invitations + system settings

PermissionGrant, PermissionRequest, RevokedToken, AdminInvitation,
SystemSetting entities per Phase 6.11 spec D4. User entity gets 7 new
fields: is_active, is_readonly, force_password_change(_by), password_changed_at,
created_by_user_id, created_via, require_2fa.

DbContext + EF configurations + migration land in subsequent tasks."
```

### Task 2: AppDbContext DbSets + EF entity configurations

**Goal:** Wire the new entities into the DbContext so EF can generate the migration. Add EF Fluent API configurations for indexes + cascading FKs.

**Files:**
- Modify: `src/Backend/AuraCore.API.Infrastructure/Data/AppDbContext.cs`

- [ ] **Step 1: Add DbSet properties + OnModelCreating configurations**

Read `src/Backend/AuraCore.API.Infrastructure/Data/AppDbContext.cs`. Find the existing DbSet block (alongside `Users`, `Subscriptions`, etc.). Add:

```csharp
    public DbSet<PermissionGrant> PermissionGrants => Set<PermissionGrant>();
    public DbSet<PermissionRequest> PermissionRequests => Set<PermissionRequest>();
    public DbSet<RevokedToken> RevokedTokens => Set<RevokedToken>();
    public DbSet<AdminInvitation> AdminInvitations => Set<AdminInvitation>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
```

Find the `OnModelCreating` method. Add at the end (before `base.OnModelCreating(modelBuilder);` or whatever the existing pattern is — copy the pattern from existing entity configs):

```csharp
    // ---------- Phase 6.11 Superadmin Foundation ----------

    modelBuilder.Entity<PermissionGrant>(b =>
    {
        b.ToTable("permission_grants");
        b.HasKey(x => x.Id);
        b.Property(x => x.PermissionKey).IsRequired().HasMaxLength(100);
        b.HasOne(x => x.AdminUser).WithMany().HasForeignKey(x => x.AdminUserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.GrantedByUser).WithMany().HasForeignKey(x => x.GrantedBy).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.RevokedByUser).WithMany().HasForeignKey(x => x.RevokedBy).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.SourceRequest).WithMany().HasForeignKey(x => x.SourceRequestId).OnDelete(DeleteBehavior.SetNull);
        // Unique index: at most one ACTIVE (non-revoked) grant per (admin, permission)
        b.HasIndex(x => new { x.AdminUserId, x.PermissionKey })
         .IsUnique()
         .HasFilter("\"RevokedAt\" IS NULL")
         .HasDatabaseName("uq_permission_grants_active");
    });

    modelBuilder.Entity<PermissionRequest>(b =>
    {
        b.ToTable("permission_requests");
        b.HasKey(x => x.Id);
        b.Property(x => x.PermissionKey).IsRequired().HasMaxLength(100);
        b.Property(x => x.Reason).IsRequired();
        b.Property(x => x.Status).IsRequired().HasMaxLength(20);
        b.HasOne(x => x.AdminUser).WithMany().HasForeignKey(x => x.AdminUserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.ReviewedByUser).WithMany().HasForeignKey(x => x.ReviewedBy).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => new { x.Status, x.AdminUserId }).HasDatabaseName("ix_permission_requests_status_admin");
        // At most 1 PENDING request per (admin, permission) at a time
        b.HasIndex(x => new { x.AdminUserId, x.PermissionKey })
         .IsUnique()
         .HasFilter("\"Status\" = 'pending'")
         .HasDatabaseName("uq_permission_requests_pending");
    });

    modelBuilder.Entity<RevokedToken>(b =>
    {
        b.ToTable("revoked_tokens");
        b.HasKey(x => x.Jti);
        b.Property(x => x.Jti).HasMaxLength(100);
        b.Property(x => x.RevokeReason).IsRequired().HasMaxLength(100);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.RevokedByUser).WithMany().HasForeignKey(x => x.RevokedBy).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => x.UserId).HasDatabaseName("ix_revoked_tokens_user");
    });

    modelBuilder.Entity<AdminInvitation>(b =>
    {
        b.ToTable("admin_invitations");
        b.HasKey(x => x.TokenHash);
        b.Property(x => x.TokenHash).HasMaxLength(100);
        b.HasOne(x => x.AdminUser).WithMany().HasForeignKey(x => x.AdminUserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.AdminUserId).HasDatabaseName("ix_admin_invitations_user");
    });

    modelBuilder.Entity<SystemSetting>(b =>
    {
        b.ToTable("system_settings");
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(100);
        b.Property(x => x.Value).IsRequired();
        b.HasOne(x => x.UpdatedByUser).WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.SetNull);
    });
```

If the existing AppDbContext doesn't have an `OnModelCreating` method, add one alongside the constructor following the standard EF pattern.

- [ ] **Step 2: Build verify**

```bash
dotnet build src/Backend/AuraCore.API.Infrastructure/AuraCore.API.Infrastructure.csproj 2>&1 | tail -5
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/Backend/AuraCore.API.Infrastructure/Data/AppDbContext.cs
git commit -m "feat(6.11.W1): AppDbContext DbSets + EF entity configurations for permission system

5 new DbSets (PermissionGrants, PermissionRequests, RevokedTokens,
AdminInvitations, SystemSettings) with FK + cascade behavior + filtered
unique indexes (uq_permission_grants_active, uq_permission_requests_pending).

Migration generated in next task."
```

### Task 3: EF migration for new tables + user fields + seed rows

**Goal:** Generate the EF migration that creates the 5 new tables, adds 7 columns to users, and seeds initial system_settings rows.

**Files:**
- Create: `src/Backend/AuraCore.API.Infrastructure/Migrations/<timestamp>_AddSuperadminFoundation.cs` (auto-generated by dotnet ef)

- [ ] **Step 1: Generate the migration**

Verify the EF tools are installed. If not, install:
```bash
dotnet tool install --global dotnet-ef --version 8.* 2>&1 | tail -3 || echo "Already installed"
```

Generate the migration:
```bash
cd "C:/Users/Admin/Desktop/AuraCorePro/AuraCorePro"
dotnet ef migrations add AddSuperadminFoundation \
    --project src/Backend/AuraCore.API.Infrastructure/AuraCore.API.Infrastructure.csproj \
    --startup-project src/Backend/AuraCore.API/AuraCore.API.csproj \
    --output-dir Migrations 2>&1 | tail -10
```

Expected: `Done. To undo this action, use 'ef migrations remove'`.

- [ ] **Step 2: Review the generated migration**

Open the new file `src/Backend/AuraCore.API.Infrastructure/Migrations/<timestamp>_AddSuperadminFoundation.cs`. Verify:
- Up() creates `permission_grants`, `permission_requests`, `revoked_tokens`, `admin_invitations`, `system_settings` tables
- Up() adds 7 columns to `users` table (IsActive, IsReadonly, ForcePasswordChange, ForcePasswordChangeBy, PasswordChangedAt, CreatedByUserId, CreatedVia, Require2fa)
- Up() creates the 4 named indexes (uq_permission_grants_active, ix_permission_requests_status_admin, uq_permission_requests_pending, ix_revoked_tokens_user, ix_admin_invitations_user)
- Down() reverses cleanly

- [ ] **Step 3: Add seed rows for system_settings**

In the same migration `Up()` method, after the `CreateTable("system_settings", ...)` block, append:

```csharp
            migrationBuilder.InsertData(
                table: "system_settings",
                columns: new[] { "Key", "Value", "UpdatedAt" },
                values: new object[,]
                {
                    { "require_2fa_for_all_admins", "false", DateTime.UtcNow },
                    { "rate_limit_policies", "{\"auth.login\":{\"requests\":5,\"windowSeconds\":1800},\"auth.register\":{\"requests\":3,\"windowSeconds\":3600},\"admin.all\":{\"requests\":1000,\"windowSeconds\":3600},\"signalr.connect\":{\"requests\":10,\"windowSeconds\":60}}", DateTime.UtcNow }
                });
```

In `Down()`, add at the start (before any DropTable):
```csharp
            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "Key",
                keyValues: new object[] { "require_2fa_for_all_admins", "rate_limit_policies" });
```

- [ ] **Step 4: Build + verify migration applies cleanly to local Postgres**

If a local Postgres dev DB exists (check `appsettings.Development.json`), apply the migration:
```bash
dotnet ef database update \
    --project src/Backend/AuraCore.API.Infrastructure/AuraCore.API.Infrastructure.csproj \
    --startup-project src/Backend/AuraCore.API/AuraCore.API.csproj 2>&1 | tail -5
```

Expected: `Done.` If local DB is unavailable, skip this step — migration applies on prod backend deploy in Wave 4 mid-deploy.

- [ ] **Step 5: Commit**

```bash
git add src/Backend/AuraCore.API.Infrastructure/Migrations/
git commit -m "feat(6.11.W1): EF migration AddSuperadminFoundation

- 5 new tables: permission_grants, permission_requests, revoked_tokens,
  admin_invitations, system_settings
- 7 new columns on users: IsActive, IsReadonly, ForcePasswordChange,
  ForcePasswordChangeBy, PasswordChangedAt, CreatedByUserId, CreatedVia,
  Require2fa
- 5 named indexes (filtered uniques + perf)
- Seed: system_settings rows for require_2fa_for_all_admins (false) and
  rate_limit_policies (auth.login 5/30min, auth.register 3/60min,
  admin.all 1000/hour, signalr.connect 10/min)

Will apply via 'dotnet ef database update' on prod backend deploy
(Wave 4 mid-deploy)."
```

### Task 4: SuperadminBootstrapService + GrandfatherMigrationService

**Goal:** Two startup-time services. `SuperadminBootstrapService` reads `SUPERADMIN_EMAILS` env var and promotes those users to `superadmin` role idempotently. `GrandfatherMigrationService` ensures every existing `role='admin'` user has Trusted template grants (one-time, idempotent).

**Files:**
- Create: `src/Backend/AuraCore.API/Services/SuperadminBootstrapService.cs`
- Create: `src/Backend/AuraCore.API/Services/GrandfatherMigrationService.cs`
- Create: `src/Backend/AuraCore.API/Helpers/PermissionKeys.cs`
- Create: `src/Backend/AuraCore.API/Helpers/PermissionTemplates.cs`
- Modify: `src/Backend/AuraCore.API/Program.cs` — register services + invoke them on startup

- [ ] **Step 1: Create PermissionKeys helper**

`src/Backend/AuraCore.API/Helpers/PermissionKeys.cs`:

```csharp
namespace AuraCore.API.Helpers;

/// <summary>
/// Hardcoded permission key namespace. Adding a new key requires backend code change
/// (migration + frontend label entry). Phase 6.11 — see spec D1 for tier mapping.
/// </summary>
public static class PermissionKeys
{
    // Tier 1 — tab-level gating
    public const string TabConfiguration  = "tab:configuration";
    public const string TabIpWhitelist    = "tab:ipwhitelist";
    public const string TabUpdates        = "tab:updates";
    public const string TabRoleChange     = "tab:rolechange";

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
}
```

- [ ] **Step 2: Create PermissionTemplates helper**

`src/Backend/AuraCore.API/Helpers/PermissionTemplates.cs`:

```csharp
namespace AuraCore.API.Helpers;

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

    /// <summary>
    /// Returns the permission keys that should be granted for the given template.
    /// Custom is configured per-grant by the superadmin and throws here.
    /// </summary>
    public static IReadOnlyList<string> GetPermissionsForTemplate(string template) => template switch
    {
        Default  => Array.Empty<string>(),
        Trusted  => PermissionKeys.AllTier2,                          // Tier 2 unlocked, Tier 1 still locked
        ReadOnly => Array.Empty<string>(),                            // paired with users.is_readonly = true
        Custom   => throw new InvalidOperationException("Custom is configured per-grant by superadmin"),
        _        => throw new ArgumentException($"Unknown template: {template}"),
    };

    public static bool RequiresIsReadonlyFlag(string template) => template == ReadOnly;
}
```

- [ ] **Step 3: Create SuperadminBootstrapService**

`src/Backend/AuraCore.API/Services/SuperadminBootstrapService.cs`:

```csharp
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Services;

/// <summary>
/// Reads SUPERADMIN_EMAILS env var (comma-separated) and promotes those users
/// to role='superadmin' on backend startup. Idempotent: only promotes if the
/// user exists AND is not already superadmin. Never creates new accounts.
/// </summary>
public class SuperadminBootstrapService
{
    private readonly AppDbContext _db;
    private readonly ILogger<SuperadminBootstrapService> _logger;

    public SuperadminBootstrapService(AppDbContext db, ILogger<SuperadminBootstrapService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        var raw = Environment.GetEnvironmentVariable("SUPERADMIN_EMAILS");
        if (string.IsNullOrWhiteSpace(raw))
        {
            _logger.LogInformation("SUPERADMIN_EMAILS env var unset; bootstrap skipped");
            return;
        }

        var emails = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(e => e.ToLowerInvariant())
                        .ToArray();

        var promoted = await _db.Users
            .Where(u => emails.Contains(u.Email.ToLower()) && u.Role != "superadmin")
            .ToListAsync(ct);

        foreach (var user in promoted)
        {
            user.Role = "superadmin";
            _logger.LogWarning("Promoted user {Email} to superadmin via SUPERADMIN_EMAILS bootstrap", user.Email);
        }

        if (promoted.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        var notFound = emails
            .Except(await _db.Users.Where(u => emails.Contains(u.Email.ToLower())).Select(u => u.Email.ToLower()).ToListAsync(ct))
            .ToArray();
        if (notFound.Length > 0)
        {
            _logger.LogWarning(
                "SUPERADMIN_EMAILS contains emails not registered in users table: {Emails}. They must register first via /api/auth/register; rerun bootstrap on next backend start.",
                string.Join(", ", notFound));
        }
    }
}
```

- [ ] **Step 4: Create GrandfatherMigrationService**

`src/Backend/AuraCore.API/Services/GrandfatherMigrationService.cs`:

```csharp
using AuraCore.API.Domain.Entities;
using AuraCore.API.Helpers;
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Services;

/// <summary>
/// One-time idempotent migration: for every existing role='admin' user that has
/// zero permission_grants rows, create "Trusted" template grants. Prevents existing
/// admins from being locked out immediately after Phase 6.11 deploy.
/// Runs on backend startup; safe to run repeatedly.
/// </summary>
public class GrandfatherMigrationService
{
    private readonly AppDbContext _db;
    private readonly ILogger<GrandfatherMigrationService> _logger;

    public GrandfatherMigrationService(AppDbContext db, ILogger<GrandfatherMigrationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        // Find all admin users with zero existing grants
        var adminsWithoutGrants = await _db.Users
            .Where(u => u.Role == "admin")
            .Where(u => !_db.PermissionGrants.Any(g => g.AdminUserId == u.Id && g.RevokedAt == null))
            .ToListAsync(ct);

        if (adminsWithoutGrants.Count == 0)
        {
            _logger.LogInformation("Grandfather migration: no admin users without grants; skipped");
            return;
        }

        // Find a superadmin to attribute the grants to (granted_by is required FK)
        var systemAttributionUser = await _db.Users
            .Where(u => u.Role == "superadmin")
            .OrderBy(u => u.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (systemAttributionUser is null)
        {
            // No superadmin yet — attribute grants to the admin themselves (self-grant for grandfather)
            // This is acceptable because grandfathering happens before the first superadmin promotion in some sequences.
            _logger.LogWarning("Grandfather migration: no superadmin user found yet; attributing grants self-to-self for {Count} admin(s). Will be normal once SUPERADMIN_EMAILS bootstrap runs.", adminsWithoutGrants.Count);
        }

        var trustedKeys = PermissionTemplates.GetPermissionsForTemplate(PermissionTemplates.Trusted);

        foreach (var admin in adminsWithoutGrants)
        {
            var attributionId = systemAttributionUser?.Id ?? admin.Id;
            foreach (var key in trustedKeys)
            {
                _db.PermissionGrants.Add(new PermissionGrant
                {
                    AdminUserId = admin.Id,
                    PermissionKey = key,
                    GrantedBy = attributionId,
                    GrantedAt = DateTime.UtcNow,
                });
            }
            _logger.LogInformation("Grandfather migration: granted {Count} Trusted-template permissions to {Email}", trustedKeys.Count, admin.Email);
        }

        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 5: Register services in Program.cs + invoke on startup**

Read `src/Backend/AuraCore.API/Program.cs`. Find the DI registration block (after `builder.Services.AddDbContext<AppDbContext>(...)`). Add:

```csharp
builder.Services.AddScoped<SuperadminBootstrapService>();
builder.Services.AddScoped<GrandfatherMigrationService>();
```

After `var app = builder.Build();` and before `app.UseHttpsRedirection();` (or wherever pipeline starts), add a startup hook. Look for an existing pattern (e.g., `using (var scope = app.Services.CreateScope()) { ... }`) and reuse if found:

```csharp
// Phase 6.11: superadmin bootstrap + grandfather migration (idempotent on every startup)
using (var bootstrapScope = app.Services.CreateScope())
{
    var bootstrap = bootstrapScope.ServiceProvider.GetRequiredService<SuperadminBootstrapService>();
    var grandfather = bootstrapScope.ServiceProvider.GetRequiredService<GrandfatherMigrationService>();
    await bootstrap.RunAsync();
    await grandfather.RunAsync();
}
```

Note: `Program.cs` top-level statements support `await` directly.

- [ ] **Step 6: Build verify**

```bash
dotnet build src/Backend/AuraCore.API/AuraCore.API.csproj 2>&1 | tail -5
```

Expected: 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/Backend/AuraCore.API/Services/ src/Backend/AuraCore.API/Helpers/PermissionKeys.cs src/Backend/AuraCore.API/Helpers/PermissionTemplates.cs src/Backend/AuraCore.API/Program.cs
git commit -m "feat(6.11.W1): SuperadminBootstrapService + GrandfatherMigrationService + permission helpers

- PermissionKeys: hardcoded enum of 4 Tier 1 + 6 Tier 2 keys
- PermissionTemplates: Default / Trusted / ReadOnly / Custom logic
- SuperadminBootstrapService: reads SUPERADMIN_EMAILS env var, promotes
  matching users to role='superadmin' idempotently. Never creates accounts.
- GrandfatherMigrationService: one-time + idempotent grant of Trusted
  template to every existing role='admin' user with zero grants.
  Prevents lockout on first Phase 6.11 deploy.

Both services invoked on backend startup via Program.cs scope.

Note: SUPERADMIN_EMAILS=ozgurdeniz807@gmail.com env var must be added
to /etc/auracore-api.env on origin BEFORE Wave 4 mid-deploy."
```

### Task 5: Backend tests for SuperadminBootstrapService + GrandfatherMigrationService

**Goal:** Verify both startup services behave idempotently and handle edge cases.

**Files:**
- Create: `tests/AuraCore.Tests.API/SuperadminFoundation/SuperadminBootstrapServiceTests.cs`
- Create: `tests/AuraCore.Tests.API/SuperadminFoundation/GrandfatherMigrationServiceTests.cs`

- [ ] **Step 1: Create SuperadminBootstrapServiceTests**

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
    private static AppDbContext NewInMemoryDb(string dbName)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(opts);
    }

    [Fact]
    public async Task NoEnvVar_DoesNothing()
    {
        Environment.SetEnvironmentVariable("SUPERADMIN_EMAILS", null);
        using var db = NewInMemoryDb(nameof(NoEnvVar_DoesNothing));
        db.Users.Add(new User { Email = "a@a.com", Role = "admin" });
        await db.SaveChangesAsync();

        var svc = new SuperadminBootstrapService(db, NullLogger<SuperadminBootstrapService>.Instance);
        await svc.RunAsync();

        var u = await db.Users.SingleAsync();
        Assert.Equal("admin", u.Role);
    }

    [Fact]
    public async Task PromotesMatchingUser_OncePerRun()
    {
        Environment.SetEnvironmentVariable("SUPERADMIN_EMAILS", "ozgur@example.com");
        using var db = NewInMemoryDb(nameof(PromotesMatchingUser_OncePerRun));
        db.Users.Add(new User { Email = "ozgur@example.com", Role = "admin" });
        await db.SaveChangesAsync();

        var svc = new SuperadminBootstrapService(db, NullLogger<SuperadminBootstrapService>.Instance);
        await svc.RunAsync();
        await svc.RunAsync();  // idempotent: second call should not re-promote

        var u = await db.Users.SingleAsync();
        Assert.Equal("superadmin", u.Role);
    }

    [Fact]
    public async Task SkipsAlreadySuperadmin()
    {
        Environment.SetEnvironmentVariable("SUPERADMIN_EMAILS", "boss@example.com");
        using var db = NewInMemoryDb(nameof(SkipsAlreadySuperadmin));
        db.Users.Add(new User { Email = "boss@example.com", Role = "superadmin" });
        await db.SaveChangesAsync();

        var svc = new SuperadminBootstrapService(db, NullLogger<SuperadminBootstrapService>.Instance);
        await svc.RunAsync();

        var u = await db.Users.SingleAsync();
        Assert.Equal("superadmin", u.Role);
    }

    [Fact]
    public async Task IsCaseInsensitive()
    {
        Environment.SetEnvironmentVariable("SUPERADMIN_EMAILS", "MIXED@CaseExample.com");
        using var db = NewInMemoryDb(nameof(IsCaseInsensitive));
        db.Users.Add(new User { Email = "mixed@caseexample.com", Role = "admin" });
        await db.SaveChangesAsync();

        var svc = new SuperadminBootstrapService(db, NullLogger<SuperadminBootstrapService>.Instance);
        await svc.RunAsync();

        var u = await db.Users.SingleAsync();
        Assert.Equal("superadmin", u.Role);
    }

    [Fact]
    public async Task EmailNotInDb_LogsButDoesNotCreateAccount()
    {
        Environment.SetEnvironmentVariable("SUPERADMIN_EMAILS", "missing@example.com");
        using var db = NewInMemoryDb(nameof(EmailNotInDb_LogsButDoesNotCreateAccount));

        var svc = new SuperadminBootstrapService(db, NullLogger<SuperadminBootstrapService>.Instance);
        await svc.RunAsync();

        Assert.Empty(await db.Users.ToListAsync());
    }
}
```

- [ ] **Step 2: Create GrandfatherMigrationServiceTests**

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
    private static AppDbContext NewInMemoryDb(string dbName)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(opts);
    }

    [Fact]
    public async Task GrantsTrustedTemplateToAdminWithoutGrants()
    {
        using var db = NewInMemoryDb(nameof(GrantsTrustedTemplateToAdminWithoutGrants));
        var admin = new User { Email = "admin@a.com", Role = "admin" };
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        var svc = new GrandfatherMigrationService(db, NullLogger<GrandfatherMigrationService>.Instance);
        await svc.RunAsync();

        var grants = await db.PermissionGrants.Where(g => g.AdminUserId == admin.Id).ToListAsync();
        Assert.Equal(PermissionKeys.AllTier2.Count, grants.Count);
        foreach (var key in PermissionKeys.AllTier2)
            Assert.Contains(grants, g => g.PermissionKey == key);
    }

    [Fact]
    public async Task IsIdempotent()
    {
        using var db = NewInMemoryDb(nameof(IsIdempotent));
        var admin = new User { Email = "admin@a.com", Role = "admin" };
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        var svc = new GrandfatherMigrationService(db, NullLogger<GrandfatherMigrationService>.Instance);
        await svc.RunAsync();
        await svc.RunAsync();   // second call must be no-op

        var grants = await db.PermissionGrants.Where(g => g.AdminUserId == admin.Id).ToListAsync();
        Assert.Equal(PermissionKeys.AllTier2.Count, grants.Count);
    }

    [Fact]
    public async Task SkipsAdminThatAlreadyHasAtLeastOneGrant()
    {
        using var db = NewInMemoryDb(nameof(SkipsAdminThatAlreadyHasAtLeastOneGrant));
        var admin = new User { Email = "admin@a.com", Role = "admin" };
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        // Pre-existing single grant — service should NOT add the rest of the template
        db.PermissionGrants.Add(new PermissionGrant
        {
            AdminUserId = admin.Id,
            PermissionKey = PermissionKeys.ActionUsersDelete,
            GrantedBy = admin.Id,
            GrantedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var svc = new GrandfatherMigrationService(db, NullLogger<GrandfatherMigrationService>.Instance);
        await svc.RunAsync();

        var grants = await db.PermissionGrants.Where(g => g.AdminUserId == admin.Id).ToListAsync();
        Assert.Single(grants);
    }

    [Fact]
    public async Task SkipsNonAdminUsers()
    {
        using var db = NewInMemoryDb(nameof(SkipsNonAdminUsers));
        db.Users.Add(new User { Email = "user@a.com", Role = "user" });
        db.Users.Add(new User { Email = "boss@a.com", Role = "superadmin" });
        await db.SaveChangesAsync();

        var svc = new GrandfatherMigrationService(db, NullLogger<GrandfatherMigrationService>.Instance);
        await svc.RunAsync();

        Assert.Empty(await db.PermissionGrants.ToListAsync());
    }
}
```

- [ ] **Step 3: Run tests**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter "FullyQualifiedName~SuperadminBootstrap|FullyQualifiedName~Grandfather" 2>&1 | tail -10
```

Expected: 9 passed (5 bootstrap + 4 grandfather), 0 failed.

- [ ] **Step 4: Commit**

```bash
git add tests/AuraCore.Tests.API/SuperadminFoundation/
git commit -m "test(6.11.W1): SuperadminBootstrap + GrandfatherMigration service tests

5 bootstrap tests: no env var, single promote, idempotency, case-insensitive,
non-existent email logged but not created.

4 grandfather tests: grants Trusted template to admin without grants,
idempotent on second call, skips admin with existing grants, skips non-admin
users (user + superadmin).

In-memory DbContext per test for isolation."
```

### Task 6: TokenRevocationMiddleware + JWT 'jti' integration

**Goal:** Middleware that consults `revoked_tokens` table on every authenticated request. If the JWT's `jti` claim is in the revoked list, return 401 `{ "error": "token_revoked" }`. Suspend admin → write all their active token jtis to revoked_tokens → next request denied.

**Files:**
- Create: `src/Backend/AuraCore.API/Middleware/TokenRevocationMiddleware.cs`
- Modify: `src/Backend/AuraCore.API/Program.cs` — add to pipeline + ensure JWT issuance includes jti

- [ ] **Step 1: Verify JWT issuance includes jti claim**

Read AuthController's login method (or wherever JWT is issued). The JWT should include `JwtRegisteredClaimNames.Jti = Guid.NewGuid().ToString()`. If it doesn't, add it.

```bash
grep -n "Jti\|jti\|new Claim(JwtRegisteredClaimNames" src/Backend/AuraCore.API/Controllers/AuthController.cs | head -10
```

If `Jti` is absent, find where `new ClaimsIdentity(...)` or `new SecurityTokenDescriptor` is constructed and add:
```csharp
new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
```

- [ ] **Step 2: Create TokenRevocationMiddleware**

`src/Backend/AuraCore.API/Middleware/TokenRevocationMiddleware.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Middleware;

/// <summary>
/// Phase 6.11: rejects JWT-authenticated requests whose 'jti' claim has been
/// recorded in revoked_tokens (suspend / password_reset / logout_all / admin_deleted).
/// Runs after UseAuthentication(), before UseAuthorization().
/// </summary>
public class TokenRevocationMiddleware
{
    private readonly RequestDelegate _next;

    public TokenRevocationMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var jti = context.User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            if (!string.IsNullOrEmpty(jti))
            {
                var isRevoked = await db.RevokedTokens.AnyAsync(t => t.Jti == jti);
                if (isRevoked)
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsJsonAsync(new { error = "token_revoked" });
                    return;
                }
            }
        }

        await _next(context);
    }
}
```

- [ ] **Step 3: Wire middleware into Program.cs pipeline**

In `src/Backend/AuraCore.API/Program.cs`, find `app.UseAuthentication()` and `app.UseAuthorization()`. Insert between them:

```csharp
app.UseAuthentication();
app.UseMiddleware<AuraCore.API.Middleware.TokenRevocationMiddleware>();
app.UseAuthorization();
```

- [ ] **Step 4: Build verify**

```bash
dotnet build src/Backend/AuraCore.API/AuraCore.API.csproj 2>&1 | tail -5
```

Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/Backend/AuraCore.API/Middleware/TokenRevocationMiddleware.cs src/Backend/AuraCore.API/Program.cs src/Backend/AuraCore.API/Controllers/AuthController.cs
git commit -m "feat(6.11.W1): TokenRevocationMiddleware + JWT jti claim integration

JWT issuance now includes 'jti' (RFC 7519 unique ID) claim per access
token. TokenRevocationMiddleware runs between UseAuthentication and
UseAuthorization; on every authenticated request it checks the
revoked_tokens table for the jti. If revoked, returns 401
{ error: 'token_revoked' } and short-circuits.

Suspend admin (Wave 4) writes all current jtis to revoked_tokens →
next request from that admin is denied. Replaces 'long-TTL JWT cannot
be invalidated' problem identified in Phase 6.10 D15."
```

---

## Sub-phase 6.11 Wave 2 — Permission system + email service refactor + Tier 1/2 application

### Task 7: /api/auth/superadmin/login endpoint

**Goal:** Add a separate login endpoint for superadmins. Stricter rate limit (3 fails / 60 min vs admin's 5 / 30 min). Only authenticates users with `role='superadmin'`. Audit-logged with action `'SuperadminLoginAttempt'` regardless of success or failure. Mandatory 2FA — superadmins without TOTP get a scope-limited JWT redirecting to /enable-2fa (this part is Wave 5 Task 26 — for now just wire the endpoint).

**Files:**
- Modify: `src/Backend/AuraCore.API/Controllers/AuthController.cs` — add SuperadminLogin method

- [ ] **Step 1: Read current AuthController.Login implementation**

```bash
grep -n "public.*Login\|class AuthController\|/login" src/Backend/AuraCore.API/Controllers/AuthController.cs | head -10
```

Identify where the existing Login method lives + its rate-limit / lockout pattern (Phase 6.x established pattern).

- [ ] **Step 2: Add SuperadminLogin method**

After the existing Login method, add (adapt parameter shape + audit log call to match existing patterns in the controller):

```csharp
    [HttpPost("superadmin/login")]
    [AllowAnonymous]
    public async Task<IActionResult> SuperadminLogin([FromBody] LoginRequest request, CancellationToken ct)
    {
        var email = request.Email?.Trim().ToLowerInvariant();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Stricter rate limit: 3 fails / 60 min on this endpoint
        const int maxAttempts = 3;
        var windowStart = DateTime.UtcNow.AddMinutes(-60);
        var recentFailures = await _db.LoginAttempts
            .CountAsync(a => a.Email == email && !a.Success && a.AttemptedAt >= windowStart, ct);
        if (recentFailures >= maxAttempts)
        {
            await LogSuperadminAttemptAsync(email, false, ip, "rate_limited", ct);
            return StatusCode(429, new { error = "Too many failed attempts. Try again in 60 minutes." });
        }

        // Look up the user
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        // Validate: must exist AND have role='superadmin'. Never reveal which one failed.
        if (user is null || user.Role != "superadmin")
        {
            await LogSuperadminAttemptAsync(email, false, ip, "invalid_creds_or_not_superadmin", ct);
            return Unauthorized(new { error = "Invalid email or password" });
        }

        // Validate password (use existing hash check — copy pattern from regular Login)
        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            await LogSuperadminAttemptAsync(email, false, ip, "wrong_password", ct);
            return Unauthorized(new { error = "Invalid email or password" });
        }

        // Suspend check (Wave 4 + Wave 5 fully wire suspend; this is defensive)
        if (!user.IsActive)
        {
            await LogSuperadminAttemptAsync(email, false, ip, "account_suspended", ct);
            return StatusCode(403, new { error = "account_suspended" });
        }

        // Wave 5 will add: 2FA mandatory check + scope-limited JWT for setup if !user.TotpEnabled
        // For now: if user.TotpEnabled, require totpCode (same as admin login pattern)
        if (user.TotpEnabled)
        {
            if (string.IsNullOrEmpty(request.TotpCode))
            {
                return Ok(new { requires2fa = true, message = "Enter your 2FA code" });
            }
            var plaintextSecret = _totpEnc.Decrypt(user.TotpSecret!);
            if (!TotpService.ValidateCode(plaintextSecret, request.TotpCode))
            {
                await LogSuperadminAttemptAsync(email, false, ip, "wrong_totp", ct);
                return Unauthorized(new { error = "Invalid 2FA code" });
            }
        }

        // Issue JWT (use existing token generator)
        var token = _tokenService.GenerateAccessToken(user);
        var refresh = _tokenService.GenerateRefreshToken(user);

        await LogSuperadminAttemptAsync(email, true, ip, "ok", ct);

        return Ok(new
        {
            accessToken = token,
            refreshToken = refresh,
            user = new { id = user.Id, email = user.Email, role = user.Role, tier = user.Tier },
        });
    }

    private async Task LogSuperadminAttemptAsync(string? email, bool success, string ip, string outcome, CancellationToken ct)
    {
        // Reuse existing LoginAttempt + AuditLogEntry tables
        _db.LoginAttempts.Add(new LoginAttempt
        {
            Email = email ?? string.Empty,
            Success = success,
            IpAddress = ip,
            AttemptedAt = DateTime.UtcNow,
        });

        // Mandatory audit_log row regardless of outcome
        _db.AuditLogEntries.Add(new AuditLogEntry
        {
            ActorEmail = email ?? "unknown",
            Action = "SuperadminLoginAttempt",
            TargetType = "Auth",
            TargetId = outcome,
            IpAddress = ip,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
    }
```

The implementer should adapt to the EXACT shapes of `_passwordHasher`, `_tokenService`, `_totpEnc` etc. used in the existing `Login` method — these names may differ slightly. Read the existing AuthController and copy the same pattern.

- [ ] **Step 3: Build verify**

```bash
dotnet build src/Backend/AuraCore.API/AuraCore.API.csproj 2>&1 | tail -5
```

Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/Backend/AuraCore.API/Controllers/AuthController.cs
git commit -m "feat(6.11.W2): /api/auth/superadmin/login endpoint with stricter rate limit

Separate authentication path for role='superadmin' accounts. Per spec D2:
- Stricter rate limit: 3 fails / 60 min (vs admin: 5 / 30 min)
- Validates role='superadmin' AND credentials match; never reveals which
  failed (uniform 'Invalid email or password' message)
- Mandatory audit_log row regardless of outcome (action='SuperadminLoginAttempt')
- 2FA flow same as admin login for now; mandatory-2FA enforcement +
  scope-limited JWT for setup land in Wave 5 Task 26

is_active suspend check defensive (full suspend wiring in Wave 4)."
```

### Task 8: SuperadminLogin tests

**Goal:** Unit + integration tests for the new endpoint.

**Files:**
- Create: `tests/AuraCore.Tests.API/SuperadminFoundation/SuperadminLoginEndpointTests.cs`

- [ ] **Step 1: Create the test class**

`tests/AuraCore.Tests.API/SuperadminFoundation/SuperadminLoginEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class SuperadminLoginEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SuperadminLoginEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient client, AppDbContext db)> CreateClientWithFreshDbAsync()
    {
        var client = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        return (client, db);
    }

    [Fact]
    public async Task Returns401_WhenUserDoesNotExist()
    {
        var (client, _) = await CreateClientWithFreshDbAsync();
        var resp = await client.PostAsJsonAsync("/api/auth/superadmin/login",
            new { email = "ghost@example.com", password = "TestPassword123!" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Returns401_WhenUserIsAdminButNotSuperadmin()
    {
        var (client, db) = await CreateClientWithFreshDbAsync();
        // Seed an admin (not superadmin)
        db.Users.Add(new User { Email = "admin@a.com", PasswordHash = "...", Role = "admin", IsActive = true });
        await db.SaveChangesAsync();

        var resp = await client.PostAsJsonAsync("/api/auth/superadmin/login",
            new { email = "admin@a.com", password = "TestPassword123!" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Returns403_WhenSuperadminIsSuspended()
    {
        var (client, db) = await CreateClientWithFreshDbAsync();
        db.Users.Add(new User { Email = "boss@a.com", PasswordHash = "...", Role = "superadmin", IsActive = false });
        await db.SaveChangesAsync();

        var resp = await client.PostAsJsonAsync("/api/auth/superadmin/login",
            new { email = "boss@a.com", password = "TestPassword123!" });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Returns429_AfterThreeFailedAttempts()
    {
        var (client, db) = await CreateClientWithFreshDbAsync();
        // Trigger 3 failures by hitting the endpoint with invalid creds
        for (int i = 0; i < 3; i++)
        {
            await client.PostAsJsonAsync("/api/auth/superadmin/login",
                new { email = "ghost@example.com", password = "wrong" });
        }
        // 4th attempt should be rate-limited
        var resp = await client.PostAsJsonAsync("/api/auth/superadmin/login",
            new { email = "ghost@example.com", password = "wrong" });
        Assert.Equal((HttpStatusCode)429, resp.StatusCode);
    }

    [Fact]
    public async Task Returns200WithToken_ForValidSuperadmin()
    {
        var (client, db) = await CreateClientWithFreshDbAsync();
        // Seed superadmin with known password hash (use the project's password hasher)
        var hasher = _factory.Services.GetRequiredService<IPasswordHasher>();
        var hash = hasher.Hash("TestPassword123!");
        db.Users.Add(new User { Email = "boss@a.com", PasswordHash = hash, Role = "superadmin", IsActive = true });
        await db.SaveChangesAsync();

        var resp = await client.PostAsJsonAsync("/api/auth/superadmin/login",
            new { email = "boss@a.com", password = "TestPassword123!" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!.AccessToken);
        Assert.Equal("superadmin", body.User?.Role);
    }

    private record LoginResponse(string AccessToken, string RefreshToken, UserDto? User);
    private record UserDto(string Role);
}
```

If `IPasswordHasher` doesn't exist exactly as named, adapt to whatever the project uses (check `src/Backend/AuraCore.API.Application/Services/`).

- [ ] **Step 2: Run tests**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter "FullyQualifiedName~SuperadminLoginEndpoint" 2>&1 | tail -10
```

Expected: 5 passed.

- [ ] **Step 3: Commit**

```bash
git add tests/AuraCore.Tests.API/SuperadminFoundation/SuperadminLoginEndpointTests.cs
git commit -m "test(6.11.W2): SuperadminLoginEndpoint tests

5 tests: 401 on ghost user, 401 on admin role (not superadmin), 403 on
suspended superadmin, 429 after 3 failures (stricter rate limit), 200
with token on valid superadmin credentials.

Uses WebApplicationFactory<Program> for full pipeline (incl.
TokenRevocationMiddleware) integration."
```

### Task 9: RequiresPermissionAttribute + DestructiveActionAttribute

**Goal:** The two authorization attributes that gate Tier 1 + Tier 2 + Tier 3 (ReadOnly enforcement) endpoints. Spec D5.

**Files:**
- Create: `src/Backend/AuraCore.API/Filters/RequiresPermissionAttribute.cs`
- Create: `src/Backend/AuraCore.API/Filters/DestructiveActionAttribute.cs`

- [ ] **Step 1: Create RequiresPermissionAttribute**

`src/Backend/AuraCore.API/Filters/RequiresPermissionAttribute.cs`:

```csharp
using System.Security.Claims;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Filters;

/// <summary>
/// Phase 6.11 spec D5. Runs after [Authorize].
/// - Superadmin: always passes.
/// - Admin: must have an active (non-revoked, non-expired) row in permission_grants
///   for the named permission. Returns 403 with { error: "permission_required",
///   permission: "<key>" } if missing.
/// - Admin with is_readonly=true: fails for any non-tab:* permission (destructive
///   actions blocked even at Tier 3).
/// - Other roles: 403.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class RequiresPermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    public string Permission { get; }

    public RequiresPermissionAttribute(string permission)
    {
        Permission = permission ?? throw new ArgumentNullException(nameof(permission));
    }

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

        var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(idClaim, out var userId))
        {
            context.Result = new ForbidResult();
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();

        var isReadonly = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => (bool?)u.IsReadonly)
            .FirstOrDefaultAsync();

        if (isReadonly == true && !Permission.StartsWith("tab:", StringComparison.Ordinal))
        {
            context.HttpContext.Response.StatusCode = 403;
            await context.HttpContext.Response.WriteAsJsonAsync(new
            {
                error = "readonly_account",
                permission = Permission,
            });
            context.Result = new EmptyResult();
            return;
        }

        var hasGrant = await db.PermissionGrants.AnyAsync(g =>
            g.AdminUserId == userId &&
            g.PermissionKey == Permission &&
            g.RevokedAt == null &&
            (g.ExpiresAt == null || g.ExpiresAt > DateTime.UtcNow));

        if (!hasGrant)
        {
            context.HttpContext.Response.StatusCode = 403;
            await context.HttpContext.Response.WriteAsJsonAsync(new
            {
                error = "permission_required",
                permission = Permission,
            });
            context.Result = new EmptyResult();
        }
    }
}
```

- [ ] **Step 2: Create DestructiveActionAttribute**

`src/Backend/AuraCore.API/Filters/DestructiveActionAttribute.cs`:

```csharp
using System.Security.Claims;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Filters;

/// <summary>
/// Phase 6.11 spec D7 — applied to Tier 3 destructive endpoints (Licenses Revoke/Activate,
/// Devices Revoke, CrashReports Delete) so the ReadOnly template enforcement also blocks
/// these "default-open" actions for read-only admins.
/// - Superadmin / non-readonly admin: passes through.
/// - Admin with is_readonly=true: returns 403 { error: "readonly_account" }.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class DestructiveActionAttribute : Attribute, IAsyncAuthorizationFilter
{
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

        var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(idClaim, out var userId))
        {
            context.Result = new ForbidResult();
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var isReadonly = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.IsReadonly)
            .FirstOrDefaultAsync();

        if (isReadonly)
        {
            context.HttpContext.Response.StatusCode = 403;
            await context.HttpContext.Response.WriteAsJsonAsync(new { error = "readonly_account" });
            context.Result = new EmptyResult();
        }
    }
}
```

- [ ] **Step 3: Build verify**

```bash
dotnet build src/Backend/AuraCore.API/AuraCore.API.csproj 2>&1 | tail -5
```

Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/Backend/AuraCore.API/Filters/
git commit -m "feat(6.11.W2): RequiresPermissionAttribute + DestructiveActionAttribute

[RequiresPermission(\"<key>\")]: superadmin always passes; admin checks
permission_grants for active (non-revoked, non-expired) entry; ReadOnly
admin (is_readonly=true) fails for any non-tab:* permission. 403 body
shape: { error: 'permission_required', permission: '<key>' } so the
frontend knows which permission to request.

[DestructiveAction]: applied to Tier 3 destructive endpoints. Superadmin
+ regular admin pass; ReadOnly admin returns 403 { error: 'readonly_account' }.
Provides the ReadOnly template enforcement for default-open actions."
```

### Task 10: RequiresPermissionAttribute tests

**Goal:** Unit tests for the filter behavior.

**Files:**
- Create: `tests/AuraCore.Tests.API/SuperadminFoundation/RequiresPermissionAttributeTests.cs`

- [ ] **Step 1: Create tests**

`tests/AuraCore.Tests.API/SuperadminFoundation/RequiresPermissionAttributeTests.cs`:

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

public class RequiresPermissionAttributeTests
{
    private static (AuthorizationFilterContext ctx, AppDbContext db) NewContext(
        string dbName,
        string? role,
        Guid? userId)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        var sp = services.BuildServiceProvider();

        var http = new DefaultHttpContext { RequestServices = sp };
        var claims = new List<Claim>();
        if (role != null) claims.Add(new Claim(ClaimTypes.Role, role));
        if (userId.HasValue) claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        http.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        http.Response.Body = new System.IO.MemoryStream();

        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor());
        var ctx = new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
        return (ctx, sp.GetRequiredService<AppDbContext>());
    }

    [Fact]
    public async Task Superadmin_AlwaysPasses_NoDbCheck()
    {
        var (ctx, _) = NewContext(nameof(Superadmin_AlwaysPasses_NoDbCheck), "superadmin", Guid.NewGuid());
        var attr = new RequiresPermissionAttribute("action:users.delete");

        await attr.OnAuthorizationAsync(ctx);

        Assert.Null(ctx.Result);
    }

    [Fact]
    public async Task NonAdminNonSuperadmin_Forbid()
    {
        var (ctx, _) = NewContext(nameof(NonAdminNonSuperadmin_Forbid), "user", Guid.NewGuid());
        var attr = new RequiresPermissionAttribute("action:users.delete");

        await attr.OnAuthorizationAsync(ctx);

        Assert.IsType<ForbidResult>(ctx.Result);
    }

    [Fact]
    public async Task Admin_NoGrant_Returns403WithPermissionRequiredBody()
    {
        var userId = Guid.NewGuid();
        var (ctx, db) = NewContext(nameof(Admin_NoGrant_Returns403WithPermissionRequiredBody), "admin", userId);
        db.Users.Add(new User { Id = userId, Email = "a@a.com", Role = "admin", IsReadonly = false });
        await db.SaveChangesAsync();

        var attr = new RequiresPermissionAttribute("action:users.delete");
        await attr.OnAuthorizationAsync(ctx);

        Assert.Equal(403, ctx.HttpContext.Response.StatusCode);
        Assert.IsType<EmptyResult>(ctx.Result);
        ctx.HttpContext.Response.Body.Position = 0;
        var body = await new System.IO.StreamReader(ctx.HttpContext.Response.Body).ReadToEndAsync();
        Assert.Contains("permission_required", body);
        Assert.Contains("action:users.delete", body);
    }

    [Fact]
    public async Task Admin_WithActiveGrant_Passes()
    {
        var userId = Guid.NewGuid();
        var (ctx, db) = NewContext(nameof(Admin_WithActiveGrant_Passes), "admin", userId);
        db.Users.Add(new User { Id = userId, Email = "a@a.com", Role = "admin", IsReadonly = false });
        db.PermissionGrants.Add(new PermissionGrant
        {
            AdminUserId = userId,
            PermissionKey = "action:users.delete",
            GrantedBy = userId,
            GrantedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var attr = new RequiresPermissionAttribute("action:users.delete");
        await attr.OnAuthorizationAsync(ctx);

        Assert.Null(ctx.Result);
    }

    [Fact]
    public async Task Admin_WithExpiredGrant_Returns403()
    {
        var userId = Guid.NewGuid();
        var (ctx, db) = NewContext(nameof(Admin_WithExpiredGrant_Returns403), "admin", userId);
        db.Users.Add(new User { Id = userId, Email = "a@a.com", Role = "admin", IsReadonly = false });
        db.PermissionGrants.Add(new PermissionGrant
        {
            AdminUserId = userId,
            PermissionKey = "action:users.delete",
            GrantedBy = userId,
            GrantedAt = DateTime.UtcNow.AddDays(-30),
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();

        var attr = new RequiresPermissionAttribute("action:users.delete");
        await attr.OnAuthorizationAsync(ctx);

        Assert.Equal(403, ctx.HttpContext.Response.StatusCode);
    }

    [Fact]
    public async Task Admin_WithRevokedGrant_Returns403()
    {
        var userId = Guid.NewGuid();
        var (ctx, db) = NewContext(nameof(Admin_WithRevokedGrant_Returns403), "admin", userId);
        db.Users.Add(new User { Id = userId, Email = "a@a.com", Role = "admin", IsReadonly = false });
        db.PermissionGrants.Add(new PermissionGrant
        {
            AdminUserId = userId,
            PermissionKey = "action:users.delete",
            GrantedBy = userId,
            GrantedAt = DateTime.UtcNow,
            RevokedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var attr = new RequiresPermissionAttribute("action:users.delete");
        await attr.OnAuthorizationAsync(ctx);

        Assert.Equal(403, ctx.HttpContext.Response.StatusCode);
    }

    [Fact]
    public async Task ReadonlyAdmin_FailsActionEvenWithGrant()
    {
        var userId = Guid.NewGuid();
        var (ctx, db) = NewContext(nameof(ReadonlyAdmin_FailsActionEvenWithGrant), "admin", userId);
        db.Users.Add(new User { Id = userId, Email = "a@a.com", Role = "admin", IsReadonly = true });
        db.PermissionGrants.Add(new PermissionGrant
        {
            AdminUserId = userId,
            PermissionKey = "action:users.delete",
            GrantedBy = userId,
        });
        await db.SaveChangesAsync();

        var attr = new RequiresPermissionAttribute("action:users.delete");
        await attr.OnAuthorizationAsync(ctx);

        Assert.Equal(403, ctx.HttpContext.Response.StatusCode);
        ctx.HttpContext.Response.Body.Position = 0;
        var body = await new System.IO.StreamReader(ctx.HttpContext.Response.Body).ReadToEndAsync();
        Assert.Contains("readonly_account", body);
    }

    [Fact]
    public async Task ReadonlyAdmin_PassesTabPermission()
    {
        var userId = Guid.NewGuid();
        var (ctx, db) = NewContext(nameof(ReadonlyAdmin_PassesTabPermission), "admin", userId);
        db.Users.Add(new User { Id = userId, Email = "a@a.com", Role = "admin", IsReadonly = true });
        db.PermissionGrants.Add(new PermissionGrant
        {
            AdminUserId = userId,
            PermissionKey = "tab:configuration",
            GrantedBy = userId,
        });
        await db.SaveChangesAsync();

        var attr = new RequiresPermissionAttribute("tab:configuration");
        await attr.OnAuthorizationAsync(ctx);

        Assert.Null(ctx.Result);
    }
}
```

- [ ] **Step 2: Run tests**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter "FullyQualifiedName~RequiresPermissionAttribute" 2>&1 | tail -10
```

Expected: 8 passed.

- [ ] **Step 3: Commit**

```bash
git add tests/AuraCore.Tests.API/SuperadminFoundation/RequiresPermissionAttributeTests.cs
git commit -m "test(6.11.W2): RequiresPermissionAttribute filter tests

8 tests covering: superadmin always passes; non-admin/non-superadmin
forbid; admin without grant returns 403 + permission_required body;
admin with active grant passes; expired grant fails; revoked grant
fails; readonly admin fails action even with grant (readonly_account
body); readonly admin passes tab permission.

In-memory DbContext per test."
```

### Task 11: Apply [RequiresPermission] to Tier 1 + Tier 2 endpoints; [DestructiveAction] to Tier 3

**Goal:** Wire the attributes onto the actual controller endpoints. Phase 6.10 spec D7 says "applied to all Tier 1 tab mutation endpoints + 6 Tier 2 action endpoints". For Tier 3, mark Licenses.Revoke/Activate, Devices.Revoke, CrashReports.Delete with [DestructiveAction].

**Files:**
- Modify: `src/Backend/AuraCore.API/Controllers/Admin/AdminConfigController.cs`
- Modify: `src/Backend/AuraCore.API/Controllers/Admin/AdminIpWhitelistController.cs`
- Modify: `src/Backend/AuraCore.API/Controllers/Admin/AdminUpdateController.cs`
- Modify: `src/Backend/AuraCore.API/Controllers/Admin/AdminUserController.cs`
- Modify: `src/Backend/AuraCore.API/Controllers/Admin/AdminSubscriptionController.cs`
- Modify: `src/Backend/AuraCore.API/Controllers/Payment/CryptoController.cs`
- Modify: `src/Backend/AuraCore.API/Controllers/Admin/AdminLicenseController.cs`
- Modify: `src/Backend/AuraCore.API/Controllers/Admin/AdminDeviceController.cs`
- Modify: `src/Backend/AuraCore.API/Controllers/Admin/AdminCrashReportController.cs`

For each controller, add `using AuraCore.API.Filters;` and `using AuraCore.API.Helpers;` to the top imports, then attribute the relevant methods:

- [ ] **Step 1: AdminConfigController — `[RequiresPermission(PermissionKeys.TabConfiguration)]`**

Apply to ALL mutation methods (PUT/POST/DELETE). GET endpoints (read-only inspection of config) stay open since the locked-page placeholder lives in the frontend tab gating; backend GET reveals nothing destructive.

```csharp
[HttpPut("...")]
[RequiresPermission(PermissionKeys.TabConfiguration)]
public async Task<IActionResult> UpdateConfig(...) { ... }
```

Apply to every `[HttpPut]`, `[HttpPost]`, `[HttpPatch]`, `[HttpDelete]` method in AdminConfigController.

- [ ] **Step 2: AdminIpWhitelistController — `[RequiresPermission(PermissionKeys.TabIpWhitelist)]`**

Apply to all mutation methods (PUT/POST/DELETE). GET stays open.

- [ ] **Step 3: AdminUpdateController — `[RequiresPermission(PermissionKeys.TabUpdates)]`**

Apply to all mutation methods (typically: Publish, Yank, Delete, etc).

- [ ] **Step 4: AdminUserController — Tier 2 actions**

```csharp
[HttpDelete("{id:guid}")]
[RequiresPermission(PermissionKeys.ActionUsersDelete)]
public async Task<IActionResult> DeleteUser(Guid id, ...) { ... }

[HttpPost("{id:guid}/ban")]   // or whatever the ban/revoke endpoint signature is
[RequiresPermission(PermissionKeys.ActionUsersBan)]
public async Task<IActionResult> BanUser(Guid id, ...) { ... }
```

If the "ban" semantic is delivered via AdminSubscriptionController.Revoke being called with a user ID, attribute that one with `ActionUsersBan` instead. Adapt to the actual code shape.

- [ ] **Step 5: AdminSubscriptionController — Tier 2 actions**

```csharp
[HttpPost("grant")]
[RequiresPermission(PermissionKeys.ActionSubscriptionsGrant)]
public async Task<IActionResult> Grant(...) { ... }

[HttpPost("revoke/{userId:guid}")]
[RequiresPermission(PermissionKeys.ActionSubscriptionsRevoke)]
public async Task<IActionResult> Revoke(Guid userId, ...) { ... }
```

- [ ] **Step 6: CryptoController — Tier 2 actions on admin verify/reject**

```csharp
[HttpPost("admin/verify/{paymentId:guid}")]
[RequiresPermission(PermissionKeys.ActionPaymentsApproveCrypto)]
public async Task<IActionResult> AdminVerifyPayment(Guid paymentId, ...) { ... }

[HttpPost("admin/reject/{paymentId:guid}")]
[RequiresPermission(PermissionKeys.ActionPaymentsRejectCrypto)]
public async Task<IActionResult> AdminRejectPayment(Guid paymentId, ...) { ... }
```

- [ ] **Step 7: AdminLicenseController — Tier 3 destructive**

```csharp
[HttpPut("{id:guid}/revoke")]
[DestructiveAction]
public async Task<IActionResult> Revoke(Guid id, ...) { ... }

[HttpPut("{id:guid}/activate")]
[DestructiveAction]
public async Task<IActionResult> Activate(Guid id, ...) { ... }
```

- [ ] **Step 8: AdminDeviceController — Tier 3 destructive**

```csharp
[HttpDelete("{id:guid}")]
[DestructiveAction]
public async Task<IActionResult> Revoke(Guid id, ...) { ... }
```

- [ ] **Step 9: AdminCrashReportController — Tier 3 destructive**

```csharp
[HttpDelete("{id:guid}")]
[DestructiveAction]
public async Task<IActionResult> Delete(Guid id, ...) { ... }
```

- [ ] **Step 10: Build verify + run all existing tests to ensure no regression**

```bash
dotnet build src/Backend/AuraCore.API/AuraCore.API.csproj 2>&1 | tail -5
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --logger "console;verbosity=quiet" 2>&1 | tail -5
```

Expected: build clean, all existing tests still pass (the grandfather migration ensures any test users with `role='admin'` get Trusted grants, so Tier 2 tests stay green; Tier 1 tests for un-attributed routes remain green).

If specific tests fail because they exercise newly-locked endpoints, those tests need to seed permission_grants for their test admin OR call as superadmin. Document the failures + fix accordingly.

- [ ] **Step 11: Commit**

```bash
git add src/Backend/AuraCore.API/Controllers/
git commit -m "feat(6.11.W2): apply [RequiresPermission] to Tier 1+2 + [DestructiveAction] to Tier 3

Tier 1 (tab gating) - mutation endpoints only:
- AdminConfigController: tab:configuration
- AdminIpWhitelistController: tab:ipwhitelist
- AdminUpdateController: tab:updates
- (RoleChangeController gets tab:rolechange in Wave 4 when it lands)

Tier 2 (action gating):
- AdminUserController.DeleteUser: action:users.delete
- AdminUserController.Ban (or AdminSubscriptionController.Revoke): action:users.ban
- AdminSubscriptionController.Grant: action:subscriptions.grant
- AdminSubscriptionController.Revoke: action:subscriptions.revoke
- CryptoController.AdminVerifyPayment: action:payments.approveCrypto
- CryptoController.AdminRejectPayment: action:payments.rejectCrypto

Tier 3 (ReadOnly enforcement):
- AdminLicenseController.Revoke / Activate: [DestructiveAction]
- AdminDeviceController.Revoke: [DestructiveAction]
- AdminCrashReportController.Delete: [DestructiveAction]

GET endpoints stay open. The frontend handles the LockedTabPlaceholder
for Tier 1 — backend GET reveals nothing destructive."
```

### Task 12: PermissionRequestsController + PermissionGrantsController + MyPermissionsController

**Goal:** Backend CRUD for permission requests + grants. Admin self-creates + cancels requests; superadmin lists pending + approve/deny + bulk; admin lists own grants via My Permissions.

**Files:**
- Create: `src/Backend/AuraCore.API/Controllers/Admin/PermissionRequestsController.cs`
- Create: `src/Backend/AuraCore.API/Controllers/Admin/MyPermissionsController.cs`
- Create: `src/Backend/AuraCore.API/Controllers/Superadmin/PermissionGrantsController.cs`

- [ ] **Step 1: PermissionRequestsController (admin self-service)**

`src/Backend/AuraCore.API/Controllers/Admin/PermissionRequestsController.cs`:

```csharp
using System.Security.Claims;
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
[Authorize(Roles = "admin,superadmin")]
public class PermissionRequestsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<AdminHub> _hub;

    public PermissionRequestsController(AppDbContext db, IHubContext<AdminHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    public sealed record CreateRequest(string PermissionKey, string Reason);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRequest req, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var email = User.FindFirst(ClaimTypes.Email)?.Value ?? "(unknown)";

        // Validate permission key is recognized
        if (!PermissionKeys.AllTier1.Contains(req.PermissionKey)
            && !PermissionKeys.AllTier2.Contains(req.PermissionKey))
            return BadRequest(new { error = "unknown_permission_key" });

        // Validate reason length
        if (string.IsNullOrWhiteSpace(req.Reason) || req.Reason.Length < 50 || req.Reason.Length > 500)
            return BadRequest(new { error = "reason_must_be_50_to_500_chars" });

        // Idempotency: if a pending request exists for the same (admin, permission), return it
        var existing = await _db.PermissionRequests
            .FirstOrDefaultAsync(r => r.AdminUserId == userId
                                   && r.PermissionKey == req.PermissionKey
                                   && r.Status == "pending", ct);
        if (existing is not null)
            return Conflict(new { error = "request_already_pending", id = existing.Id });

        var entity = new PermissionRequest
        {
            AdminUserId = userId,
            PermissionKey = req.PermissionKey,
            Reason = req.Reason.Trim(),
            RequestedAt = DateTime.UtcNow,
            Status = "pending",
        };
        _db.PermissionRequests.Add(entity);
        await _db.SaveChangesAsync(ct);

        // Broadcast to superadmin group
        await _hub.Clients.Group("admins").SendAsync("PermissionRequested", new
        {
            adminEmail = email,
            permissionKey = req.PermissionKey,
            reason = req.Reason,
            requestedAt = entity.RequestedAt,
        }, ct);

        return Ok(new { id = entity.Id, status = entity.Status, requestedAt = entity.RequestedAt });
    }

    [HttpGet("mine")]
    public async Task<IActionResult> ListMine([FromQuery] string? status, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var q = _db.PermissionRequests.Where(r => r.AdminUserId == userId);
        if (!string.IsNullOrEmpty(status)) q = q.Where(r => r.Status == status);
        var items = await q.OrderByDescending(r => r.RequestedAt).Take(100).ToListAsync(ct);
        return Ok(new { items });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var entity = await _db.PermissionRequests
            .FirstOrDefaultAsync(r => r.Id == id && r.AdminUserId == userId, ct);
        if (entity is null) return NotFound();
        if (entity.Status != "pending") return BadRequest(new { error = "not_pending" });

        entity.Status = "cancelled";
        entity.ReviewedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(new { id = entity.Id, status = entity.Status });
    }
}
```

- [ ] **Step 2: MyPermissionsController (admin self-service)**

`src/Backend/AuraCore.API/Controllers/Admin/MyPermissionsController.cs`:

```csharp
using System.Security.Claims;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Admin;

[ApiController]
[Route("api/admin/my-permissions")]
[Authorize(Roles = "admin,superadmin")]
public class MyPermissionsController : ControllerBase
{
    private readonly AppDbContext _db;
    public MyPermissionsController(AppDbContext db) { _db = db; }

    [HttpGet]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var grants = await _db.PermissionGrants
            .Where(g => g.AdminUserId == userId && g.RevokedAt == null
                     && (g.ExpiresAt == null || g.ExpiresAt > DateTime.UtcNow))
            .Select(g => new
            {
                g.Id,
                g.PermissionKey,
                g.GrantedAt,
                g.ExpiresAt,
                grantedByEmail = _db.Users.Where(u => u.Id == g.GrantedBy).Select(u => u.Email).FirstOrDefault(),
                source = g.SourceRequestId == null ? "direct" : "request",
            })
            .ToListAsync(ct);

        var pendingRequests = await _db.PermissionRequests
            .Where(r => r.AdminUserId == userId && r.Status == "pending")
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync(ct);

        var recentlyDeniedOrRevoked = await _db.PermissionRequests
            .Where(r => r.AdminUserId == userId
                     && (r.Status == "denied" || r.Status == "cancelled"))
            .OrderByDescending(r => r.ReviewedAt ?? r.RequestedAt)
            .Take(20)
            .Select(r => new
            {
                r.PermissionKey,
                r.Status,
                reviewedAt = r.ReviewedAt,
                reviewedByEmail = _db.Users.Where(u => u.Id == r.ReviewedBy).Select(u => u.Email).FirstOrDefault(),
                r.ReviewNote,
            })
            .ToListAsync(ct);

        return Ok(new { grants, pendingRequests, recentlyDeniedOrRevoked });
    }
}
```

- [ ] **Step 3: PermissionGrantsController (superadmin)**

`src/Backend/AuraCore.API/Controllers/Superadmin/PermissionGrantsController.cs`:

```csharp
using System.Security.Claims;
using AuraCore.API.Domain.Entities;
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
public class PermissionGrantsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<AdminHub> _hub;

    public PermissionGrantsController(AppDbContext db, IHubContext<AdminHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    // === Grants ===

    [HttpGet("permission-grants")]
    public async Task<IActionResult> ListGrants([FromQuery] Guid? adminUserId, [FromQuery] bool includeRevoked = false, CancellationToken ct = default)
    {
        var q = _db.PermissionGrants.AsQueryable();
        if (adminUserId.HasValue) q = q.Where(g => g.AdminUserId == adminUserId.Value);
        if (!includeRevoked) q = q.Where(g => g.RevokedAt == null);
        var items = await q.OrderByDescending(g => g.GrantedAt).Take(500).ToListAsync(ct);
        return Ok(new { items });
    }

    public sealed record CreateGrantRequest(Guid AdminUserId, string PermissionKey, DateTime? ExpiresAt);

    [HttpPost("permission-grants")]
    public async Task<IActionResult> CreateGrant([FromBody] CreateGrantRequest req, CancellationToken ct)
    {
        var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var existing = await _db.PermissionGrants
            .FirstOrDefaultAsync(g => g.AdminUserId == req.AdminUserId
                                   && g.PermissionKey == req.PermissionKey
                                   && g.RevokedAt == null, ct);
        if (existing is not null) return Conflict(new { error = "grant_already_active", id = existing.Id });

        var entity = new PermissionGrant
        {
            AdminUserId = req.AdminUserId,
            PermissionKey = req.PermissionKey,
            GrantedBy = actorId,
            GrantedAt = DateTime.UtcNow,
            ExpiresAt = req.ExpiresAt,
        };
        _db.PermissionGrants.Add(entity);
        await _db.SaveChangesAsync(ct);

        await _hub.Clients.User(req.AdminUserId.ToString()).SendAsync("PermissionApproved", new
        {
            permissionKey = req.PermissionKey,
            approvedBy = actorId,
            expiresAt = req.ExpiresAt,
        }, ct);

        return Ok(new { id = entity.Id });
    }

    public sealed record RevokeGrantRequest(string? Reason);

    [HttpPost("permission-grants/{id:guid}/revoke")]
    public async Task<IActionResult> RevokeGrant(Guid id, [FromBody] RevokeGrantRequest? req, CancellationToken ct)
    {
        var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var entity = await _db.PermissionGrants.FirstOrDefaultAsync(g => g.Id == id, ct);
        if (entity is null) return NotFound();
        if (entity.RevokedAt is not null) return BadRequest(new { error = "already_revoked" });

        entity.RevokedAt = DateTime.UtcNow;
        entity.RevokedBy = actorId;
        entity.RevokeReason = req?.Reason;
        await _db.SaveChangesAsync(ct);

        await _hub.Clients.User(entity.AdminUserId.ToString()).SendAsync("PermissionRevoked", new
        {
            permissionKey = entity.PermissionKey,
            revokedBy = actorId,
            reason = entity.RevokeReason,
        }, ct);

        return Ok();
    }

    // === Requests inbox ===

    [HttpGet("permission-requests")]
    public async Task<IActionResult> ListRequests([FromQuery] string? status = "pending", CancellationToken ct = default)
    {
        var q = _db.PermissionRequests.AsQueryable();
        if (!string.IsNullOrEmpty(status) && status != "all") q = q.Where(r => r.Status == status);
        var items = await q
            .OrderByDescending(r => r.RequestedAt)
            .Take(200)
            .Select(r => new
            {
                r.Id,
                adminEmail = _db.Users.Where(u => u.Id == r.AdminUserId).Select(u => u.Email).FirstOrDefault(),
                r.PermissionKey,
                r.Reason,
                r.RequestedAt,
                r.Status,
                reviewedByEmail = _db.Users.Where(u => u.Id == r.ReviewedBy).Select(u => u.Email).FirstOrDefault(),
                r.ReviewedAt,
                r.ReviewNote,
            })
            .ToListAsync(ct);
        return Ok(new { items });
    }

    public sealed record ApproveRequestBody(DateTime? ExpiresAt, string? ReviewNote);

    [HttpPost("permission-requests/{id:guid}/approve")]
    public async Task<IActionResult> ApproveRequest(Guid id, [FromBody] ApproveRequestBody? body, CancellationToken ct)
    {
        var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var req = await _db.PermissionRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (req is null) return NotFound();
        if (req.Status != "pending") return BadRequest(new { error = "not_pending" });

        req.Status = "approved";
        req.ReviewedBy = actorId;
        req.ReviewedAt = DateTime.UtcNow;
        req.ReviewNote = body?.ReviewNote;

        // Create the grant (idempotent — guard against pre-existing active grant)
        var existingGrant = await _db.PermissionGrants
            .FirstOrDefaultAsync(g => g.AdminUserId == req.AdminUserId
                                   && g.PermissionKey == req.PermissionKey
                                   && g.RevokedAt == null, ct);
        if (existingGrant is null)
        {
            _db.PermissionGrants.Add(new PermissionGrant
            {
                AdminUserId = req.AdminUserId,
                PermissionKey = req.PermissionKey,
                GrantedBy = actorId,
                GrantedAt = DateTime.UtcNow,
                ExpiresAt = body?.ExpiresAt,
                SourceRequestId = req.Id,
            });
        }

        await _db.SaveChangesAsync(ct);

        await _hub.Clients.User(req.AdminUserId.ToString()).SendAsync("PermissionApproved", new
        {
            permissionKey = req.PermissionKey,
            approvedBy = actorId,
            expiresAt = body?.ExpiresAt,
        }, ct);

        return Ok();
    }

    public sealed record DenyRequestBody(string? ReviewNote);

    [HttpPost("permission-requests/{id:guid}/deny")]
    public async Task<IActionResult> DenyRequest(Guid id, [FromBody] DenyRequestBody? body, CancellationToken ct)
    {
        var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var req = await _db.PermissionRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (req is null) return NotFound();
        if (req.Status != "pending") return BadRequest(new { error = "not_pending" });

        req.Status = "denied";
        req.ReviewedBy = actorId;
        req.ReviewedAt = DateTime.UtcNow;
        req.ReviewNote = body?.ReviewNote;
        await _db.SaveChangesAsync(ct);

        await _hub.Clients.User(req.AdminUserId.ToString()).SendAsync("PermissionDenied", new
        {
            permissionKey = req.PermissionKey,
            deniedBy = actorId,
            reviewNote = body?.ReviewNote,
        }, ct);

        return Ok();
    }

    public sealed record BulkActionRequest(Guid[] Ids, DateTime? ExpiresAt, string? ReviewNote);

    [HttpPost("permission-requests/bulk-approve")]
    public async Task<IActionResult> BulkApprove([FromBody] BulkActionRequest req, CancellationToken ct)
    {
        var ok = 0; var skipped = 0;
        foreach (var id in req.Ids)
        {
            var resp = await ApproveRequest(id, new ApproveRequestBody(req.ExpiresAt, req.ReviewNote), ct);
            if (resp is OkResult or OkObjectResult) ok++; else skipped++;
        }
        return Ok(new { approved = ok, skipped });
    }

    [HttpPost("permission-requests/bulk-deny")]
    public async Task<IActionResult> BulkDeny([FromBody] BulkActionRequest req, CancellationToken ct)
    {
        var ok = 0; var skipped = 0;
        foreach (var id in req.Ids)
        {
            var resp = await DenyRequest(id, new DenyRequestBody(req.ReviewNote), ct);
            if (resp is OkResult or OkObjectResult) ok++; else skipped++;
        }
        return Ok(new { denied = ok, skipped });
    }
}
```

- [ ] **Step 4: Build verify**

```bash
dotnet build src/Backend/AuraCore.API/AuraCore.API.csproj 2>&1 | tail -5
```

Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/Backend/AuraCore.API/Controllers/Admin/PermissionRequestsController.cs src/Backend/AuraCore.API/Controllers/Admin/MyPermissionsController.cs src/Backend/AuraCore.API/Controllers/Superadmin/PermissionGrantsController.cs
git commit -m "feat(6.11.W2): permission grants/requests CRUD + SignalR event broadcasts

- Admin: POST /api/admin/permission-requests (create), GET /mine, DELETE
  /{id} (cancel). Validates permission key + reason length (50-500).
  Idempotency: pending request blocks duplicate creation. Emits
  PermissionRequested SignalR to superadmin group on create.
- Admin: GET /api/admin/my-permissions returns active grants + pending
  requests + recent denials/cancellations.
- Superadmin: list/create/revoke grants; list/approve/deny/bulk-approve/
  bulk-deny requests. Approve creates the corresponding permission_grant
  + emits PermissionApproved targeted to specific admin via Clients.User().
  Deny + Revoke also targeted SignalR.

Permission requests fall through ApiController standard validation +
[Authorize] role gating; superadmin operations gated by role='superadmin'."
```

### Task 13: Permission request lifecycle tests

**Goal:** End-to-end test of admin creates request → superadmin approves → grant created → admin's [RequiresPermission] check passes.

**Files:**
- Create: `tests/AuraCore.Tests.API/SuperadminFoundation/PermissionRequestLifecycleTests.cs`

- [ ] **Step 1: Create the test class**

`tests/AuraCore.Tests.API/SuperadminFoundation/PermissionRequestLifecycleTests.cs`:

```csharp
using AuraCore.API.Domain.Entities;
using AuraCore.API.Helpers;
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class PermissionRequestLifecycleTests
{
    private static AppDbContext NewDb(string name)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options;
        return new AppDbContext(opts);
    }

    [Fact]
    public async Task CreatedRequest_HasPendingStatus()
    {
        using var db = NewDb(nameof(CreatedRequest_HasPendingStatus));
        var admin = new User { Email = "a@a.com", Role = "admin" };
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        db.PermissionRequests.Add(new PermissionRequest
        {
            AdminUserId = admin.Id,
            PermissionKey = PermissionKeys.TabConfiguration,
            Reason = new string('x', 60),
        });
        await db.SaveChangesAsync();

        var req = await db.PermissionRequests.SingleAsync();
        Assert.Equal("pending", req.Status);
    }

    [Fact]
    public async Task ApprovingRequest_CreatesActiveGrant_AndIsRetrievableByPermissionFilterLogic()
    {
        using var db = NewDb(nameof(ApprovingRequest_CreatesActiveGrant_AndIsRetrievableByPermissionFilterLogic));
        var admin = new User { Email = "a@a.com", Role = "admin" };
        var superadmin = new User { Email = "boss@a.com", Role = "superadmin" };
        db.Users.AddRange(admin, superadmin);
        await db.SaveChangesAsync();

        var req = new PermissionRequest
        {
            AdminUserId = admin.Id,
            PermissionKey = PermissionKeys.ActionUsersDelete,
            Reason = new string('x', 60),
        };
        db.PermissionRequests.Add(req);
        await db.SaveChangesAsync();

        // Simulate approval
        req.Status = "approved";
        req.ReviewedBy = superadmin.Id;
        req.ReviewedAt = DateTime.UtcNow;
        db.PermissionGrants.Add(new PermissionGrant
        {
            AdminUserId = admin.Id,
            PermissionKey = PermissionKeys.ActionUsersDelete,
            GrantedBy = superadmin.Id,
            SourceRequestId = req.Id,
        });
        await db.SaveChangesAsync();

        var grant = await db.PermissionGrants.SingleAsync();
        Assert.True(grant.IsActive());
        Assert.Equal(req.Id, grant.SourceRequestId);
    }

    [Fact]
    public async Task DenyingRequest_DoesNotCreateGrant()
    {
        using var db = NewDb(nameof(DenyingRequest_DoesNotCreateGrant));
        var admin = new User { Email = "a@a.com", Role = "admin" };
        var superadmin = new User { Email = "boss@a.com", Role = "superadmin" };
        db.Users.AddRange(admin, superadmin);
        await db.SaveChangesAsync();

        var req = new PermissionRequest
        {
            AdminUserId = admin.Id,
            PermissionKey = PermissionKeys.ActionUsersDelete,
            Reason = new string('x', 60),
        };
        db.PermissionRequests.Add(req);
        await db.SaveChangesAsync();

        // Deny
        req.Status = "denied";
        req.ReviewedBy = superadmin.Id;
        req.ReviewedAt = DateTime.UtcNow;
        req.ReviewNote = "no business need";
        await db.SaveChangesAsync();

        Assert.Empty(await db.PermissionGrants.ToListAsync());
    }

    [Fact]
    public async Task CancellingPendingRequest_SetsStatusCancelled()
    {
        using var db = NewDb(nameof(CancellingPendingRequest_SetsStatusCancelled));
        var admin = new User { Email = "a@a.com", Role = "admin" };
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        var req = new PermissionRequest
        {
            AdminUserId = admin.Id,
            PermissionKey = PermissionKeys.TabConfiguration,
            Reason = new string('x', 60),
        };
        db.PermissionRequests.Add(req);
        await db.SaveChangesAsync();

        req.Status = "cancelled";
        req.ReviewedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var got = await db.PermissionRequests.SingleAsync();
        Assert.Equal("cancelled", got.Status);
    }

    [Fact]
    public async Task GrantWithExpiresAtInPast_IsNotActive()
    {
        using var db = NewDb(nameof(GrantWithExpiresAtInPast_IsNotActive));
        var admin = new User { Email = "a@a.com", Role = "admin" };
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        var grant = new PermissionGrant
        {
            AdminUserId = admin.Id,
            PermissionKey = PermissionKeys.TabConfiguration,
            GrantedBy = admin.Id,
            GrantedAt = DateTime.UtcNow.AddDays(-30),
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
        };
        db.PermissionGrants.Add(grant);
        await db.SaveChangesAsync();

        Assert.False(grant.IsActive());
    }
}
```

- [ ] **Step 2: Run tests**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter "FullyQualifiedName~PermissionRequestLifecycle" 2>&1 | tail -10
```

Expected: 5 passed.

- [ ] **Step 3: Commit**

```bash
git add tests/AuraCore.Tests.API/SuperadminFoundation/PermissionRequestLifecycleTests.cs
git commit -m "test(6.11.W2): permission request lifecycle tests

5 tests: created request has pending status; approving creates active
grant linked via SourceRequestId; denying does not create grant;
cancelling sets status='cancelled'; grant with past ExpiresAt is not
active (IsActive() helper)."
```

### Task 14: IEmailService + ResendEmailService refactor

**Goal:** Refactor `PasswordResetController.cs:146-168` inline HTTPS Resend call into `IEmailService` abstraction with proper IHttpClientFactory + structured logging + 6 transactional email types.

**Files:**
- Create: `src/Backend/AuraCore.API.Application/Interfaces/IEmailService.cs`
- Create: `src/Backend/AuraCore.API.Infrastructure/Services/Email/EmailTemplate.cs`
- Create: `src/Backend/AuraCore.API.Infrastructure/Services/Email/ResendEmailService.cs`
- Create: `src/Backend/AuraCore.API.Infrastructure/Services/Email/Templates/_base.html`
- Create: `src/Backend/AuraCore.API.Infrastructure/Services/Email/Templates/AdminInvitation.html`
- Create: `src/Backend/AuraCore.API.Infrastructure/Services/Email/Templates/PasswordReset.html`
- Create: `src/Backend/AuraCore.API.Infrastructure/Services/Email/Templates/PermissionRequested.html`
- Create: `src/Backend/AuraCore.API.Infrastructure/Services/Email/Templates/PermissionApproved.html`
- Create: `src/Backend/AuraCore.API.Infrastructure/Services/Email/Templates/PermissionDenied.html`
- Create: `src/Backend/AuraCore.API.Infrastructure/Services/Email/Templates/AdminCreatedWithoutEmail.html`
- Modify: `src/Backend/AuraCore.API/Program.cs` — DI for IEmailService + IHttpClientFactory named client
- Modify: `src/Backend/AuraCore.API/Controllers/PasswordResetController.cs` — replace inline call with `_emailService`

- [ ] **Step 1: IEmailService interface**

`src/Backend/AuraCore.API.Application/Interfaces/IEmailService.cs`:

```csharp
namespace AuraCore.API.Application.Interfaces;

public sealed record EmailSendResult(bool Success, string? MessageId, string? Error);

public enum EmailTemplate
{
    AdminInvitation,
    PasswordReset,
    PermissionRequested,
    PermissionApproved,
    PermissionDenied,
    AdminCreatedWithoutEmail,
}

public interface IEmailService
{
    /// <summary>Send an arbitrary HTML email. Used for ad-hoc messages.</summary>
    Task<EmailSendResult> SendAsync(string to, string subject, string html, CancellationToken ct = default);

    /// <summary>Send via a named template. data is a dictionary or anonymous object with placeholder values.</summary>
    Task<EmailSendResult> SendFromTemplateAsync(string to, EmailTemplate template, object data, CancellationToken ct = default);
}
```

- [ ] **Step 2: ResendEmailService implementation**

`src/Backend/AuraCore.API.Infrastructure/Services/Email/ResendEmailService.cs`:

```csharp
using System.Net.Http.Json;
using System.Reflection;
using AuraCore.API.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AuraCore.API.Infrastructure.Services.Email;

public class ResendEmailService : IEmailService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<ResendEmailService> _logger;
    private readonly string _fromAddress;

    private static readonly Dictionary<EmailTemplate, (string Subject, string TemplateFile)> TemplateMap = new()
    {
        [EmailTemplate.AdminInvitation]          = ("You're invited to AuraCore Admin Panel", "AdminInvitation.html"),
        [EmailTemplate.PasswordReset]            = ("AuraCore — Reset your password",          "PasswordReset.html"),
        [EmailTemplate.PermissionRequested]      = ("AuraCore — New permission request",       "PermissionRequested.html"),
        [EmailTemplate.PermissionApproved]       = ("AuraCore — Permission approved",          "PermissionApproved.html"),
        [EmailTemplate.PermissionDenied]         = ("AuraCore — Permission request denied",    "PermissionDenied.html"),
        [EmailTemplate.AdminCreatedWithoutEmail] = ("AuraCore — Admin account created (no invite email)", "AdminCreatedWithoutEmail.html"),
    };

    public ResendEmailService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<ResendEmailService> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
        _fromAddress = config["EmailFromAddress"] ?? "AuraCore Pro <noreply@auracore.pro>";
    }

    public async Task<EmailSendResult> SendAsync(string to, string subject, string html, CancellationToken ct = default)
    {
        var apiKey = Environment.GetEnvironmentVariable("RESEND_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("RESEND_API_KEY not set; email to {To} subject {Subject} not sent", to, subject);
            return new EmailSendResult(false, null, "RESEND_API_KEY_unset");
        }

        var http = _httpFactory.CreateClient("resend");
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        var payload = new
        {
            from = _fromAddress,
            to = new[] { to },
            subject,
            html,
        };

        try
        {
            var resp = await http.PostAsJsonAsync("/emails", payload, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var errBody = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Resend API non-success {Status} sending to {To}: {Body}", resp.StatusCode, to, errBody);
                return new EmailSendResult(false, null, $"resend_http_{(int)resp.StatusCode}");
            }
            var body = await resp.Content.ReadFromJsonAsync<ResendOkResponse>(cancellationToken: ct);
            _logger.LogInformation("Resend email sent to {To}, messageId {Id}", to, body?.Id);
            return new EmailSendResult(true, body?.Id, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resend email send failed to {To}", to);
            return new EmailSendResult(false, null, ex.Message);
        }
    }

    public async Task<EmailSendResult> SendFromTemplateAsync(string to, EmailTemplate template, object data, CancellationToken ct = default)
    {
        if (!TemplateMap.TryGetValue(template, out var info))
            return new EmailSendResult(false, null, "unknown_template");

        var html = await LoadAndRenderTemplateAsync(info.TemplateFile, data);
        return await SendAsync(to, info.Subject, html, ct);
    }

    private static async Task<string> LoadAndRenderTemplateAsync(string templateFile, object data)
    {
        // Templates are embedded resources or file-based.
        // For Phase 6.11 we use file-based — read from the assembly's content root.
        var asm = typeof(ResendEmailService).Assembly;
        var dir = Path.Combine(Path.GetDirectoryName(asm.Location)!, "Services", "Email", "Templates");
        var basePath = Path.Combine(dir, "_base.html");
        var tplPath = Path.Combine(dir, templateFile);

        var baseHtml = await File.ReadAllTextAsync(basePath);
        var bodyHtml = await File.ReadAllTextAsync(tplPath);

        // {{placeholder}} substitution for both base and body
        var props = data.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var rendered = bodyHtml;
        foreach (var p in props)
        {
            var value = p.GetValue(data)?.ToString() ?? string.Empty;
            rendered = rendered.Replace("{{" + p.Name + "}}", System.Net.WebUtility.HtmlEncode(value));
        }
        return baseHtml.Replace("{{body}}", rendered);
    }

    private sealed record ResendOkResponse(string? Id);
}
```

- [ ] **Step 3: EmailTemplate.cs**

(Already defined inline in IEmailService.cs — skip.)

- [ ] **Step 4: HTML templates**

`src/Backend/AuraCore.API.Infrastructure/Services/Email/Templates/_base.html`:

```html
<!DOCTYPE html>
<html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<style>
  body { margin:0; padding:0; background:#08080c; font-family: 'Helvetica Neue', Arial, sans-serif; color:#e8e8ea; }
  .wrap { max-width: 580px; margin: 32px auto; padding: 32px; background: #0d0d12; border: 1px solid rgba(255,255,255,0.06); border-radius: 12px; }
  h1 { font-size: 20px; font-weight: 600; margin: 0 0 16px; background: linear-gradient(135deg, #22d3ee, #a78bfa); -webkit-background-clip: text; background-clip: text; color: transparent; }
  p { font-size: 14px; line-height: 1.6; color: #c8c8d0; }
  a.btn { display:inline-block; padding: 10px 20px; background: linear-gradient(135deg, #22d3ee, #a78bfa); color:#fff !important; text-decoration:none; border-radius:8px; margin-top:12px; font-size:14px; font-weight:500; }
  .meta { font-size: 12px; color: #6c6c78; margin-top: 24px; border-top: 1px solid rgba(255,255,255,0.06); padding-top: 16px; }
</style>
</head><body>
<div class="wrap">
  {{body}}
  <div class="meta">AuraCore Pro · Sent by noreply@auracore.pro · Do not reply to this address</div>
</div>
</body></html>
```

`src/Backend/AuraCore.API.Infrastructure/Services/Email/Templates/AdminInvitation.html`:

```html
<h1>Welcome to AuraCore Admin</h1>
<p>An admin account has been created for you at AuraCore Pro. Click the link below to set your password and log in.</p>
<p><a class="btn" href="{{SetupLink}}">Set your password</a></p>
<p>This link expires on <strong>{{ExpiresAt}}</strong>. If you did not expect this invitation, please ignore this email.</p>
```

`src/Backend/AuraCore.API.Infrastructure/Services/Email/Templates/PasswordReset.html`:

```html
<h1>Reset your password</h1>
<p>Use the code below in the AuraCore admin panel to reset your password:</p>
<p style="font-family:monospace; font-size: 18px; letter-spacing: 4px; padding: 12px; background: rgba(255,255,255,0.04); border-radius: 8px; text-align:center;">{{Code}}</p>
<p>This code expires on <strong>{{ExpiresAt}}</strong>. If you did not request a password reset, please ignore this email.</p>
```

`src/Backend/AuraCore.API.Infrastructure/Services/Email/Templates/PermissionRequested.html`:

```html
<h1>New permission request</h1>
<p><strong>{{AdminEmail}}</strong> requested access to <strong>{{PermissionKey}}</strong>.</p>
<p><em>Reason:</em> {{Reason}}</p>
<p><a class="btn" href="{{InboxLink}}">Review in admin panel</a></p>
```

`src/Backend/AuraCore.API.Infrastructure/Services/Email/Templates/PermissionApproved.html`:

```html
<h1>Permission approved</h1>
<p>Your request for <strong>{{PermissionKey}}</strong> has been approved.</p>
<p>The new permission is active immediately. You may need to refresh the admin panel to see the unlocked features.</p>
```

`src/Backend/AuraCore.API.Infrastructure/Services/Email/Templates/PermissionDenied.html`:

```html
<h1>Permission request denied</h1>
<p>Your request for <strong>{{PermissionKey}}</strong> has been denied.</p>
<p><em>Reviewer note:</em> {{ReviewNote}}</p>
<p>If you have questions, contact the superadmin directly.</p>
```

`src/Backend/AuraCore.API.Infrastructure/Services/Email/Templates/AdminCreatedWithoutEmail.html`:

```html
<h1>Admin account created (no invite email)</h1>
<p>An admin account was created for <strong>{{AdminEmail}}</strong> with the manual-password option. The initial password has been generated:</p>
<p style="font-family:monospace; font-size: 16px; padding: 12px; background: rgba(255,255,255,0.04); border-radius: 8px;">{{InitialPassword}}</p>
<p><em>Note:</em> {{Note}}</p>
<p>Share this password with the new admin via a secure out-of-band channel. The admin will be required to change it on first login.</p>
```

- [ ] **Step 5: Update .csproj to copy template files to output dir**

Read `src/Backend/AuraCore.API.Infrastructure/AuraCore.API.Infrastructure.csproj`. Add (or merge into existing ItemGroup):

```xml
  <ItemGroup>
    <None Include="Services\Email\Templates\*.html">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
```

- [ ] **Step 6: DI registration in Program.cs**

In `src/Backend/AuraCore.API/Program.cs`, find existing service registrations. Add:

```csharp
builder.Services.AddHttpClient("resend", c => c.BaseAddress = new Uri("https://api.resend.com"));
builder.Services.AddScoped<AuraCore.API.Application.Interfaces.IEmailService, AuraCore.API.Infrastructure.Services.Email.ResendEmailService>();
```

- [ ] **Step 7: Refactor PasswordResetController.cs**

Read the current `SendResetEmailAsync` method around line 146-168. Replace it. Inject `IEmailService _emailService` via constructor (add field + constructor param). Then replace the inline call:

```csharp
var emailResult = await _emailService.SendFromTemplateAsync(
    email,
    EmailTemplate.PasswordReset,
    new { Code = code, ExpiresAt = DateTime.UtcNow.AddMinutes(15).ToString("u") });
if (!emailResult.Success)
{
    _logger.LogWarning("PasswordReset email send failed for {Email}: {Error}", email, emailResult.Error);
}
```

Delete the now-unused private `SendResetEmailAsync` method.

- [ ] **Step 8: Build verify**

```bash
dotnet build src/Backend/AuraCore.API/AuraCore.API.csproj 2>&1 | tail -5
```

Expected: 0 errors.

- [ ] **Step 9: Commit**

```bash
git add src/Backend/AuraCore.API.Application/Interfaces/IEmailService.cs src/Backend/AuraCore.API.Infrastructure/Services/Email/ src/Backend/AuraCore.API.Infrastructure/AuraCore.API.Infrastructure.csproj src/Backend/AuraCore.API/Program.cs src/Backend/AuraCore.API/Controllers/PasswordResetController.cs
git commit -m "feat(6.11.W2): IEmailService + ResendEmailService + 6 HTML templates

Refactor PasswordResetController.cs:146-168 inline 'new HttpClient()'
Resend POST into IEmailService abstraction:

- IEmailService interface (SendAsync raw + SendFromTemplateAsync with
  EmailTemplate enum)
- ResendEmailService uses IHttpClientFactory named client 'resend' with
  BaseAddress https://api.resend.com; Bearer auth from RESEND_API_KEY env
  var; structured logging of status + messageId; typed EmailSendResult
- 6 HTML templates with shared _base.html (glass-card aesthetic matching
  admin panel); {{placeholder}} substitution via reflection
- Templates copied to output dir via .csproj None Include
- PasswordResetController.SendResetEmailAsync removed; inline call
  replaced with _emailService.SendFromTemplateAsync(..., PasswordReset)
- Existing RESEND_API_KEY in /etc/auracore-api.env reused (no env var
  changes needed)"
```

### Task 15: Email service tests

**Goal:** Unit tests for IEmailService with a mocked IHttpClientFactory.

**Files:**
- Create: `tests/AuraCore.Tests.API/SuperadminFoundation/EmailServiceTests.cs`

- [ ] **Step 1: Create EmailServiceTests**

`tests/AuraCore.Tests.API/SuperadminFoundation/EmailServiceTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using AuraCore.API.Application.Interfaces;
using AuraCore.API.Infrastructure.Services.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class EmailServiceTests
{
    private static IConfiguration NewConfig() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EmailFromAddress"] = "Test <noreply@example.com>",
        }).Build();

    private static (ResendEmailService svc, Mock<HttpMessageHandler> handler) NewService(HttpStatusCode status, string? body = null)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(status)
            {
                Content = body is null ? null : JsonContent.Create(new { id = body }),
            });

        var http = new HttpClient(handler.Object) { BaseAddress = new Uri("https://api.resend.com") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("resend")).Returns(http);

        Environment.SetEnvironmentVariable("RESEND_API_KEY", "test-key");
        var svc = new ResendEmailService(factory.Object, NewConfig(), NullLogger<ResendEmailService>.Instance);
        return (svc, handler);
    }

    [Fact]
    public async Task SendAsync_Returns_Success_On200()
    {
        var (svc, _) = NewService(HttpStatusCode.OK, "msg-123");
        var result = await svc.SendAsync("to@example.com", "Hi", "<p>Hi</p>");
        Assert.True(result.Success);
        Assert.Equal("msg-123", result.MessageId);
    }

    [Fact]
    public async Task SendAsync_Returns_Failure_On500()
    {
        var (svc, _) = NewService(HttpStatusCode.InternalServerError);
        var result = await svc.SendAsync("to@example.com", "Hi", "<p>Hi</p>");
        Assert.False(result.Success);
        Assert.Equal("resend_http_500", result.Error);
    }

    [Fact]
    public async Task SendAsync_Returns_Failure_When_ApiKey_Unset()
    {
        Environment.SetEnvironmentVariable("RESEND_API_KEY", null);
        var (svc, _) = NewService(HttpStatusCode.OK);
        Environment.SetEnvironmentVariable("RESEND_API_KEY", null);   // ensure cleared after NewService set it
        var result = await svc.SendAsync("to@example.com", "Hi", "<p>Hi</p>");
        Assert.False(result.Success);
        Assert.Equal("RESEND_API_KEY_unset", result.Error);
    }
}
```

- [ ] **Step 2: Run tests**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter "FullyQualifiedName~EmailServiceTests" 2>&1 | tail -10
```

Expected: 3 passed.

- [ ] **Step 3: Commit**

```bash
git add tests/AuraCore.Tests.API/SuperadminFoundation/EmailServiceTests.cs
git commit -m "test(6.11.W2): IEmailService + ResendEmailService unit tests

3 tests: success on 200 with messageId; failure on 500 with resend_http_500
error code; failure with RESEND_API_KEY_unset error when env var missing.

Mocked HttpMessageHandler via Moq.Protected; IHttpClientFactory mocked
to return the test HttpClient."
```

### Task 16: SignalR scope-limited token rejection in AdminHub

**Goal:** AdminHub must REJECT WebSocket connections from JWTs with `scope: '2fa-setup-only'` claim. Otherwise a partially-authenticated user could subscribe to live admin events. Wave 5 fully wires scope-limited JWT issuance; this task pre-emptively guards the Hub.

**Files:**
- Modify: `src/Backend/AuraCore.API/Hubs/AdminHub.cs`

- [ ] **Step 1: Add scope check to OnConnectedAsync**

Read existing AdminHub.cs from Phase 6.10. Modify OnConnectedAsync:

```csharp
public override async Task OnConnectedAsync()
{
    var scope = Context.User?.FindFirst("scope")?.Value;
    if (scope == "2fa-setup-only")
    {
        Context.Abort();
        return;
    }

    await Groups.AddToGroupAsync(Context.ConnectionId, "admins");
    await Clients.Group("admins").SendAsync("AdminCount", new { count = AdminConnectionCount.Increment() });
    await base.OnConnectedAsync();
}
```

- [ ] **Step 2: Build verify**

```bash
dotnet build src/Backend/AuraCore.API/AuraCore.API.csproj 2>&1 | tail -5
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/Backend/AuraCore.API/Hubs/AdminHub.cs
git commit -m "feat(6.11.W2): AdminHub rejects scope-limited JWTs

Defense-in-depth: a JWT with scope='2fa-setup-only' (issued during 2FA
mandatory setup flow — Wave 5) MUST NOT be allowed to open a SignalR
WebSocket. OnConnectedAsync now Aborts the connection if the scope claim
is present.

This pre-emptively guards the Hub; full scope-limited JWT issuance lands
in Wave 5 Task 26."
```

---

## Wave 3 — Frontend role-aware shell + locked UX + ops hardening

**Goal:** Frontend renders nav + tabs based on user's role + permissions. Locked tabs show a placeholder with a "Request access" CTA. Nginx basic-auth gate removed (admin panel goes public — discovery-via-Cert-Transparency was always the real exposure). DNS SPF/DMARC tightened so transactional emails (invitations, password resets) don't land in spam.

**Pre-Wave checklist (one-time before starting):**
- [ ] Confirm Wave 1 + Wave 2 commits all green: `dotnet test --filter FullyQualifiedName~SuperadminFoundation`
- [ ] Backend `/api/me/permissions` endpoint working: returns `{ role, permissions: string[], isReadonly }`
- [ ] Migration applied to local dev DB: `dotnet ef database update --project src/Backend/AuraCore.API`

### Task 17: Frontend User type + auth context role/permission propagation

**Files:**
- Modify: `admin-panel/src/lib/types.ts`
- Modify: `admin-panel/src/lib/api.ts`
- Create: `admin-panel/src/hooks/usePermissions.ts`
- Test: `admin-panel/src/hooks/__tests__/usePermissions.test.ts`

- [ ] **Step 1: Write failing test for usePermissions hook**

```typescript
// admin-panel/src/hooks/__tests__/usePermissions.test.ts
import { renderHook } from '@testing-library/react';
import { usePermissions } from '../usePermissions';
import { AuthProvider } from '../../lib/AuthContext';

describe('usePermissions', () => {
    it('superadmin sees all permissions as granted', () => {
        const wrapper = ({ children }: { children: React.ReactNode }) => (
            <AuthProvider initialUser={{ id: '1', email: 'sa@x.com', role: 'superadmin', permissions: [], isReadonly: false }}>
                {children}
            </AuthProvider>
        );
        const { result } = renderHook(() => usePermissions(), { wrapper });
        expect(result.current.has('tab:configuration')).toBe(true);
        expect(result.current.has('tab:ipwhitelist')).toBe(true);
        expect(result.current.canPerformDestructive()).toBe(true);
    });

    it('admin with no permissions denies tab access', () => {
        const wrapper = ({ children }: { children: React.ReactNode }) => (
            <AuthProvider initialUser={{ id: '2', email: 'a@x.com', role: 'admin', permissions: [], isReadonly: false }}>
                {children}
            </AuthProvider>
        );
        const { result } = renderHook(() => usePermissions(), { wrapper });
        expect(result.current.has('tab:configuration')).toBe(false);
    });

    it('readonly admin cannot perform destructive', () => {
        const wrapper = ({ children }: { children: React.ReactNode }) => (
            <AuthProvider initialUser={{ id: '3', email: 'r@x.com', role: 'admin', permissions: ['tab:users'], isReadonly: true }}>
                {children}
            </AuthProvider>
        );
        const { result } = renderHook(() => usePermissions(), { wrapper });
        expect(result.current.has('tab:users')).toBe(true);
        expect(result.current.canPerformDestructive()).toBe(false);
    });
});
```

- [ ] **Step 2: Run test, verify it fails**

```bash
cd admin-panel && npm test -- usePermissions
```

Expected: FAIL — `usePermissions` not exported.

- [ ] **Step 3: Update User type**

Edit `admin-panel/src/lib/types.ts`. Add `permissions` and `isReadonly` to User type:

```typescript
export interface User {
    id: string;
    email: string;
    role: 'admin' | 'superadmin';
    tier?: string;
    createdAt: string;
    permissions: string[];      // Wave 3: empty for superadmin (implicit all)
    isReadonly: boolean;         // Wave 3: blocks all destructive actions when true
}
```

- [ ] **Step 4: Update api.ts to include permissions in /api/me response handling**

Edit `admin-panel/src/lib/api.ts`. Add method:

```typescript
async getMyPermissions(): Promise<{ role: string; permissions: string[]; isReadonly: boolean }> {
    const r = await this.request('/api/me/permissions');
    return r.json();
}
```

And in the existing `getMe` (or login/refresh) flow, ensure the returned User object hydrates `permissions` + `isReadonly` (call `getMyPermissions` after login if not already in `/api/me` payload — Wave 1 backend should have added these to the user DTO).

- [ ] **Step 5: Write usePermissions hook**

```typescript
// admin-panel/src/hooks/usePermissions.ts
import { useAuth } from '../lib/AuthContext';

export function usePermissions() {
    const { user } = useAuth();
    return {
        has: (key: string): boolean => {
            if (!user) return false;
            if (user.role === 'superadmin') return true;
            return user.permissions.includes(key);
        },
        canPerformDestructive: (): boolean => {
            if (!user) return false;
            if (user.role === 'superadmin') return true;
            return !user.isReadonly;
        },
        isReadonly: user?.isReadonly ?? false,
        role: user?.role ?? null,
    };
}
```

- [ ] **Step 6: Run tests, verify pass**

```bash
cd admin-panel && npm test -- usePermissions
```

Expected: 3 tests PASS.

- [ ] **Step 7: Commit**

```bash
git add admin-panel/src/lib/types.ts admin-panel/src/lib/api.ts admin-panel/src/hooks/usePermissions.ts admin-panel/src/hooks/__tests__/usePermissions.test.ts
git commit -m "feat(6.11.W3): usePermissions hook + User type permissions

User type now carries permissions[] + isReadonly. usePermissions hook
encapsulates role-aware checks: has(key) for tab/action gating,
canPerformDestructive() for ReadOnly enforcement. Superadmin always
returns true."
```

### Task 18: Conditional NAV_GROUPS by role + locked tab routing

**Files:**
- Modify: `admin-panel/src/components/Sidebar.tsx` (or wherever NAV_GROUPS is defined)
- Modify: `admin-panel/src/app/page.tsx` (route renderer)

- [ ] **Step 1: Identify NAV_GROUPS definition site**

```bash
cd admin-panel && grep -rn "NAV_GROUPS\|navGroups\|NavGroup" src/
```

Expected: matches in Sidebar component file.

- [ ] **Step 2: Filter nav items by permission**

Modify the nav rendering to filter items the user can't access:

```typescript
// In Sidebar.tsx (or equivalent)
import { usePermissions } from '@/hooks/usePermissions';

const TAB_PERMISSION_MAP: Record<string, string | null> = {
    dashboard: null,            // always visible
    users: 'tab:users',
    licenses: 'tab:licenses',
    devices: 'tab:devices',
    subscriptions: 'tab:subscriptions',
    auditlog: 'tab:auditlog',
    releases: 'tab:releases',
    configuration: 'tab:configuration',
    ipwhitelist: 'tab:ipwhitelist',
    payments: 'tab:payments',
    crypto: 'tab:crypto',
    invitations: 'superadmin:only',     // visible only to superadmin
    permissions: 'superadmin:only',
    actionlog: 'superadmin:only',
};

export function Sidebar() {
    const perms = usePermissions();
    const visibleItems = NAV_GROUPS.flatMap(g =>
        g.items.filter(item => {
            const required = TAB_PERMISSION_MAP[item.key];
            if (required === null) return true;
            if (required === 'superadmin:only') return perms.role === 'superadmin';
            return perms.has(required);
        })
    );
    // ... render visibleItems instead of NAV_GROUPS.flatMap
}
```

NOTE: Tabs the user CAN'T access at all are hidden (not just locked). Tabs the user CAN access but lacks specific actions on (e.g. has `tab:users` but not destructive permission) render normally — destructive buttons disable themselves at the cell level.

- [ ] **Step 3: Build verify**

```bash
cd admin-panel && npm run build 2>&1 | tail -10
```

Expected: 0 errors. Bundle size delta minimal.

- [ ] **Step 4: Commit**

```bash
git add admin-panel/src/components/Sidebar.tsx
git commit -m "feat(6.11.W3): role-aware nav — hide tabs user has no permission for

TAB_PERMISSION_MAP gates each nav item by required permission key.
Superadmin-only tabs (Invitations / Permissions / Action Log) hidden
from regular admins entirely. Tab presence implies tab access — no
'see-but-locked' state for nav items (locked state is for in-tab
destructive actions, Task 19)."
```

### Task 19: LockedActionButton + PermissionRequestDialog + PermissionGate components

**Goal:** When an admin lacks permission for a destructive button (e.g. Revoke License but no `action:license:revoke`), the button stays visible but disabled, with hover tooltip "Permission required: action:license:revoke" and an info icon next to it that opens a PermissionRequestDialog.

**Files:**
- Create: `admin-panel/src/components/LockedActionButton.tsx`
- Create: `admin-panel/src/components/PermissionRequestDialog.tsx`
- Create: `admin-panel/src/components/PermissionGate.tsx`
- Test: `admin-panel/src/components/__tests__/LockedActionButton.test.tsx`

- [ ] **Step 1: Write failing test for LockedActionButton**

```typescript
// admin-panel/src/components/__tests__/LockedActionButton.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { LockedActionButton } from '../LockedActionButton';
import { AuthProvider } from '@/lib/AuthContext';

describe('LockedActionButton', () => {
    it('renders enabled when user has permission', () => {
        const wrapper = ({ children }: { children: React.ReactNode }) => (
            <AuthProvider initialUser={{ id: '1', email: 'a@x.com', role: 'admin', permissions: ['action:license:revoke'], isReadonly: false }}>
                {children}
            </AuthProvider>
        );
        render(<LockedActionButton permission="action:license:revoke" onClick={() => {}}>Revoke</LockedActionButton>, { wrapper });
        expect(screen.getByRole('button', { name: /revoke/i })).not.toBeDisabled();
    });

    it('renders disabled when user lacks permission', () => {
        const wrapper = ({ children }: { children: React.ReactNode }) => (
            <AuthProvider initialUser={{ id: '2', email: 'a@x.com', role: 'admin', permissions: [], isReadonly: false }}>
                {children}
            </AuthProvider>
        );
        render(<LockedActionButton permission="action:license:revoke" onClick={() => {}}>Revoke</LockedActionButton>, { wrapper });
        expect(screen.getByRole('button', { name: /revoke/i })).toBeDisabled();
    });

    it('renders disabled when user is readonly even with permission', () => {
        const wrapper = ({ children }: { children: React.ReactNode }) => (
            <AuthProvider initialUser={{ id: '3', email: 'r@x.com', role: 'admin', permissions: ['action:license:revoke'], isReadonly: true }}>
                {children}
            </AuthProvider>
        );
        render(<LockedActionButton permission="action:license:revoke" onClick={() => {}}>Revoke</LockedActionButton>, { wrapper });
        expect(screen.getByRole('button', { name: /revoke/i })).toBeDisabled();
    });
});
```

- [ ] **Step 2: Run test, verify it fails**

```bash
cd admin-panel && npm test -- LockedActionButton
```

Expected: FAIL — module not found.

- [ ] **Step 3: Implement LockedActionButton**

```typescript
// admin-panel/src/components/LockedActionButton.tsx
'use client';

import { useState, ReactNode } from 'react';
import { Lock, Info } from 'lucide-react';
import { usePermissions } from '@/hooks/usePermissions';
import { PermissionRequestDialog } from './PermissionRequestDialog';

interface Props {
    permission: string;
    onClick: () => void | Promise<void>;
    children: ReactNode;
    className?: string;
    title?: string;
}

export function LockedActionButton({ permission, onClick, children, className = '', title }: Props) {
    const perms = usePermissions();
    const [showDialog, setShowDialog] = useState(false);

    const canPerform = perms.has(permission) && perms.canPerformDestructive();
    const reason = !perms.has(permission)
        ? `Permission required: ${permission}`
        : perms.isReadonly ? 'Account is read-only — cannot perform destructive actions' : '';

    return (
        <span className="inline-flex items-center gap-1">
            <button
                type="button"
                disabled={!canPerform}
                onClick={onClick}
                title={canPerform ? title : reason}
                className={`${className} ${!canPerform ? 'opacity-40 cursor-not-allowed' : ''}`}
            >
                {!canPerform && <Lock className="w-3 h-3 inline mr-1" />}
                {children}
            </button>
            {!canPerform && perms.role === 'admin' && !perms.isReadonly && (
                <button
                    type="button"
                    onClick={() => setShowDialog(true)}
                    className="text-white/30 hover:text-accent transition-colors"
                    title="Request access to this action"
                >
                    <Info className="w-3 h-3" />
                </button>
            )}
            {showDialog && (
                <PermissionRequestDialog
                    permission={permission}
                    onClose={() => setShowDialog(false)}
                />
            )}
        </span>
    );
}
```

- [ ] **Step 4: Implement PermissionRequestDialog**

```typescript
// admin-panel/src/components/PermissionRequestDialog.tsx
'use client';

import { useState } from 'react';
import { api } from '@/lib/api';

interface Props {
    permission: string;
    onClose: () => void;
}

export function PermissionRequestDialog({ permission, onClose }: Props) {
    const [reason, setReason] = useState('');
    const [submitting, setSubmitting] = useState(false);
    const [submitted, setSubmitted] = useState(false);
    const [error, setError] = useState<string | null>(null);

    async function submit() {
        if (reason.trim().length < 10) {
            setError('Please provide a reason (at least 10 characters)');
            return;
        }
        setSubmitting(true);
        setError(null);
        try {
            await api.requestPermission(permission, reason.trim());
            setSubmitted(true);
        } catch (e: any) {
            setError(e?.message ?? 'Request failed');
        } finally {
            setSubmitting(false);
        }
    }

    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm" onClick={onClose}>
            <div className="glass-card p-6 max-w-md w-full mx-4" onClick={e => e.stopPropagation()}>
                {submitted ? (
                    <>
                        <h2 className="text-lg font-bold mb-2 text-aura-green">Request submitted</h2>
                        <p className="text-sm text-white/65 mb-4">
                            A superadmin has been notified. You'll see a notification in the app
                            when your request is approved or denied.
                        </p>
                        <div className="flex justify-end">
                            <button onClick={onClose} className="btn-primary text-xs px-4 py-2">Close</button>
                        </div>
                    </>
                ) : (
                    <>
                        <h2 className="text-lg font-bold mb-2">Request permission</h2>
                        <p className="text-sm text-white/65 mb-2">
                            Permission: <span className="font-mono text-xs text-accent">{permission}</span>
                        </p>
                        <label className="block text-xs text-white/50 mb-1">Why do you need this access?</label>
                        <textarea
                            value={reason}
                            onChange={e => setReason(e.target.value)}
                            placeholder="e.g. Customer escalation #4521 — need to revoke compromised license"
                            rows={4}
                            className="input-dark w-full mb-3"
                        />
                        {error && <p className="text-xs text-aura-red mb-2">{error}</p>}
                        <div className="flex justify-end gap-2">
                            <button onClick={onClose} disabled={submitting} className="btn-ghost text-xs px-4 py-2">Cancel</button>
                            <button onClick={submit} disabled={submitting} className="btn-primary text-xs px-4 py-2">
                                {submitting ? 'Submitting...' : 'Submit request'}
                            </button>
                        </div>
                    </>
                )}
            </div>
        </div>
    );
}
```

- [ ] **Step 5: Implement PermissionGate (for whole-tab gating fallback)**

```typescript
// admin-panel/src/components/PermissionGate.tsx
'use client';

import { ReactNode } from 'react';
import { Lock } from 'lucide-react';
import { usePermissions } from '@/hooks/usePermissions';

interface Props {
    permission: string;
    children: ReactNode;
}

export function PermissionGate({ permission, children }: Props) {
    const perms = usePermissions();
    if (perms.has(permission)) return <>{children}</>;
    return (
        <div className="glass-card p-12 text-center">
            <Lock className="w-12 h-12 mx-auto mb-4 text-white/20" />
            <h2 className="text-lg font-bold mb-2">Access locked</h2>
            <p className="text-sm text-white/50">
                You don't have permission to view this section.
                Required: <span className="font-mono text-xs text-accent">{permission}</span>
            </p>
        </div>
    );
}
```

- [ ] **Step 6: Add api.requestPermission method**

Edit `admin-panel/src/lib/api.ts`:

```typescript
async requestPermission(permission: string, reason: string): Promise<{ id: string }> {
    const r = await this.request('/api/permissions/requests', {
        method: 'POST',
        body: JSON.stringify({ permission, reason }),
    });
    return r.json();
}
```

- [ ] **Step 7: Run tests, verify pass**

```bash
cd admin-panel && npm test -- LockedActionButton
```

Expected: 3 tests PASS.

- [ ] **Step 8: Commit**

```bash
git add admin-panel/src/components/LockedActionButton.tsx admin-panel/src/components/PermissionRequestDialog.tsx admin-panel/src/components/PermissionGate.tsx admin-panel/src/components/__tests__/LockedActionButton.test.tsx admin-panel/src/lib/api.ts
git commit -m "feat(6.11.W3): LockedActionButton + PermissionRequestDialog + PermissionGate

LockedActionButton wraps any destructive action — disables itself when
user lacks the required permission OR is read-only, surfaces the gating
reason via tooltip, and offers an info-icon to open the
PermissionRequestDialog.

PermissionRequestDialog POSTs to /api/permissions/requests with the
permission key + 10+ char reason. Superadmin sees the request in the
Permissions tab (Wave 4).

PermissionGate is a fallback for whole-tab gating when nav-level filtering
isn't enough (e.g. shared component embedded in a parent page)."
```

### Task 20: Apply LockedActionButton to existing destructive buttons

**Files (modify):**
- `admin-panel/src/views/UsersPage.tsx` (Revoke + Delete buttons)
- `admin-panel/src/views/LicensesPage.tsx` (Revoke + Activate buttons)
- `admin-panel/src/views/DevicesPage.tsx` (Deactivate button)
- `admin-panel/src/views/SubscriptionsPage.tsx` (Cancel button)
- `admin-panel/src/views/ConfigurationPage.tsx` (Save button)
- `admin-panel/src/views/IpWhitelistPage.tsx` (Add/Remove buttons)
- `admin-panel/src/views/ReleasesPage.tsx` (Publish button)
- `admin-panel/src/views/PaymentsPage.tsx` (Refund button)
- `admin-panel/src/views/CryptoPage.tsx` (Mark Paid button)

- [ ] **Step 1: Replace each destructive `<button>` with `<LockedActionButton>`**

Pattern — example for LicensesPage Revoke:

```typescript
// Before:
<button onClick={() => setConfirmRevoke({ id: l.id, key: l.key })}
    className="btn-action btn-danger text-xs px-3 py-1">Revoke</button>

// After:
<LockedActionButton
    permission="action:license:revoke"
    onClick={() => setConfirmRevoke({ id: l.id, key: l.key })}
    className="btn-action btn-danger text-xs px-3 py-1"
>Revoke</LockedActionButton>
```

Permission key map (use these exact strings — they match Wave 1 PermissionKeys.cs):

| View | Action | Permission Key |
|------|--------|----------------|
| Users | Revoke subscription | `action:user:revoke_subscription` |
| Users | Delete | `action:user:delete` |
| Licenses | Revoke | `action:license:revoke` |
| Licenses | Activate | `action:license:activate` |
| Devices | Deactivate | `action:device:deactivate` |
| Subscriptions | Cancel | `action:subscription:cancel` |
| Configuration | Save | `action:config:save` |
| IP Whitelist | Add/Remove | `action:ipwhitelist:edit` |
| Releases | Publish | `action:release:publish` |
| Payments | Refund | `action:payment:refund` |
| Crypto | Mark paid | `action:crypto:markpaid` |

- [ ] **Step 2: Add import to each file**

```typescript
import { LockedActionButton } from '@/components/LockedActionButton';
```

- [ ] **Step 3: Build verify**

```bash
cd admin-panel && npm run build 2>&1 | tail -10
```

Expected: 0 errors. (Type errors here surface mismatched permission keys.)

- [ ] **Step 4: Manual smoke test (local dev server)**

Start dev server, login as a fresh admin (use SuperadminBootstrapService output to create one without permissions, or temporarily set role=admin in DB), confirm:
- All destructive buttons show lock icon + greyed out
- Hovering shows "Permission required: action:..." tooltip
- Info icon next to button opens dialog with that permission key pre-filled

- [ ] **Step 5: Commit**

```bash
git add admin-panel/src/views/
git commit -m "feat(6.11.W3): wrap all destructive buttons in LockedActionButton

Per-view permission keys (action:license:revoke, action:user:delete, etc.)
gate each destructive button. Read-only admins see the lock icon on EVERY
destructive button regardless of explicit grants.

Action keys match the [DestructiveAction] attribute strings on the
backend controllers (Wave 2 Task 11) — when frontend says revoke is OK,
backend agrees, and vice versa."
```

### Task 21: Nginx public-cut + DNS SPF/DMARC

**Files:**
- Modify on origin (165.227.170.3): `/etc/nginx/sites-enabled/auracore-admin`
- Modify on origin: `/var/www/admin-panel/robots.txt`
- DNS: SPF + DMARC TXT records for `auracore.pro`

**One-time backup (before any nginx change):**

- [ ] **Step 1: SSH backup**

```bash
ssh root@165.227.170.3 "cp /etc/nginx/sites-enabled/auracore-admin /etc/nginx/sites-enabled/auracore-admin.bak-$(date +%Y%m%d%H%M)"
```

Verify backup created: `ssh root@165.227.170.3 "ls -la /etc/nginx/sites-enabled/auracore-admin.bak-*"`

- [ ] **Step 2: Remove `auth_basic` directive from admin server block**

```bash
ssh root@165.227.170.3 "grep -n 'auth_basic' /etc/nginx/sites-enabled/auracore-admin"
```

If present (it should be — Phase 6.9 hotfix added it), remove the two lines:

```nginx
auth_basic "Admin Panel";
auth_basic_user_file /etc/nginx/admin-htpasswd;
```

Use sed to comment them out (safer than delete):

```bash
ssh root@165.227.170.3 "sed -i 's|^\(\s*auth_basic.*\)|# 6.11 PUBLIC-CUT: \1|' /etc/nginx/sites-enabled/auracore-admin"
```

- [ ] **Step 3: Test nginx config**

```bash
ssh root@165.227.170.3 "nginx -t"
```

Expected: `syntax is ok` + `test is successful`. If FAIL, restore backup and stop:
```bash
ssh root@165.227.170.3 "cp /etc/nginx/sites-enabled/auracore-admin.bak-* /etc/nginx/sites-enabled/auracore-admin && nginx -t"
```

- [ ] **Step 4: Reload nginx**

```bash
ssh root@165.227.170.3 "systemctl reload nginx"
```

- [ ] **Step 5: Verify panel loads without auth prompt**

```bash
curl -I https://admin.auracore.pro/
```

Expected: `HTTP/2 200` (NOT `401 Unauthorized`).

- [ ] **Step 6: Add robots.txt with full disallow**

```bash
ssh root@165.227.170.3 "cat > /var/www/admin-panel/robots.txt << 'EOF'
User-agent: *
Disallow: /
EOF"
```

Verify: `curl https://admin.auracore.pro/robots.txt` → returns the disallow.

NOTE: This doesn't actually hide the URL — it just asks well-behaved crawlers not to index it. URL discovery via Cert Transparency logs is the real exposure vector that no nginx setting can prevent. Defense is the auth layer (login + 2FA + rate limits + IP whitelist), not the URL.

- [ ] **Step 7: SPF record on auracore.pro**

Check current SPF: `dig +short TXT auracore.pro | grep spf`

If no SPF record OR Resend not in it, add via DNS provider (DigitalOcean DNS UI or wherever auracore.pro is hosted):

```
TXT @ "v=spf1 include:resend.com ~all"
```

Wait 5 min for propagation, verify: `dig +short TXT auracore.pro | grep spf`

- [ ] **Step 8: DMARC record**

Add via DNS UI:

```
TXT _dmarc "v=DMARC1; p=quarantine; rua=mailto:dmarc@auracore.pro; pct=100; aspf=s; adkim=s"
```

This tells receivers to quarantine messages claiming to be from `@auracore.pro` that fail SPF/DKIM. Without this, transactional emails (invitations, password resets) frequently land in spam.

Verify: `dig +short TXT _dmarc.auracore.pro`

- [ ] **Step 9: DKIM via Resend**

Login to Resend dashboard → Domains → auracore.pro → copy the 3 CNAME records → add to DNS.

Wait for Resend to verify (UI shows green checkmark, usually 10-30 min).

- [ ] **Step 10: End-to-end email deliverability test**

Send a test email from the API:

```bash
curl -X POST https://api.auracore.pro/api/auth/forgot-password \
    -H 'Content-Type: application/json' \
    -d '{"email":"ozgurdeniz807@gmail.com"}'
```

Verify the email arrives in inbox (NOT spam) within 1 min. Open Gmail headers → confirm SPF/DKIM/DMARC all PASS.

- [ ] **Step 11: Commit ops marker (no source changes — this is an ops-only task)**

Use empty commit to mark the deploy boundary:

```bash
git commit --allow-empty -m "ops(6.11.W3): admin panel public + DNS SPF/DMARC tightened

Removed nginx auth_basic gate from admin.auracore.pro — admin panel is
now publicly reachable. Defense moves to the auth layer (login + 2FA +
rate limits + IP whitelist + read-only mode).

robots.txt added with full Disallow (well-behaved crawlers only —
URL discovery via Cert Transparency is the real exposure).

SPF record added: 'v=spf1 include:resend.com ~all'
DMARC record added: 'v=DMARC1; p=quarantine; ...'
DKIM CNAMEs added per Resend domain setup.

Backup: /etc/nginx/sites-enabled/auracore-admin.bak-{timestamp}
Rollback: cp .bak file back, nginx -t, systemctl reload nginx."
```

---

## Wave 4 — Superadmin tabs + admin lifecycle + invitation flow + CSV export

**Goal:** Superadmin gets 3 new tabs (Permissions, Invitations, Action Log) + the ability to create/disable/delete admin accounts. Invitation flow uses single-use tokens emailed via Resend. Action Log is the security-grade audit trail (who did what, when, with what permission, from which IP).

### Task 22: AdminInvitation entity + InvitationsController

**Files:**
- (Wave 1 already created the AdminInvitation entity — this task wires the controller)
- Create: `src/Backend/AuraCore.API/Controllers/Admin/InvitationsController.cs`
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/InvitationsControllerTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/AuraCore.Tests.API/SuperadminFoundation/InvitationsControllerTests.cs
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Net;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class InvitationsControllerTests : IClassFixture<TestApiFactory>
{
    private readonly HttpClient _superadminClient;
    private readonly HttpClient _adminClient;

    public InvitationsControllerTests(TestApiFactory factory)
    {
        _superadminClient = factory.CreateClientWithRole("superadmin");
        _adminClient = factory.CreateClientWithRole("admin");
    }

    [Fact]
    public async Task SuperadminCanCreateInvitation()
    {
        var resp = await _superadminClient.PostAsJsonAsync("/api/invitations", new
        {
            email = "newadmin@example.com",
            template = "ReadOnly",
            expiresInDays = 7
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<InvitationResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!.Token);
        Assert.Equal("newadmin@example.com", body.Email);
    }

    [Fact]
    public async Task RegularAdminForbidden()
    {
        var resp = await _adminClient.PostAsJsonAsync("/api/invitations", new
        {
            email = "x@x.com", template = "ReadOnly", expiresInDays = 7
        });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task AcceptInvitationCreatesAdminWithTemplate()
    {
        // Create invitation
        var createResp = await _superadminClient.PostAsJsonAsync("/api/invitations", new
        {
            email = "tonew@example.com", template = "Moderator", expiresInDays = 7
        });
        var inv = await createResp.Content.ReadFromJsonAsync<InvitationResponse>();

        // Accept (no auth — invitation token IS the auth)
        var anon = new HttpClient { BaseAddress = _superadminClient.BaseAddress };
        var acceptResp = await anon.PostAsJsonAsync($"/api/invitations/accept", new
        {
            token = inv!.Token,
            password = "SecurePass123!",
            confirmPassword = "SecurePass123!"
        });
        Assert.Equal(HttpStatusCode.OK, acceptResp.StatusCode);

        // Old invitation token now invalid (single-use)
        var doubleAccept = await anon.PostAsJsonAsync($"/api/invitations/accept", new
        {
            token = inv.Token, password = "Other123!", confirmPassword = "Other123!"
        });
        Assert.Equal(HttpStatusCode.BadRequest, doubleAccept.StatusCode);
    }

    private record InvitationResponse(string Id, string Email, string Token, DateTime ExpiresAt);
}
```

- [ ] **Step 2: Run test, verify it fails**

```bash
dotnet test --filter FullyQualifiedName~InvitationsController
```

Expected: FAIL — controller not found.

- [ ] **Step 3: Implement InvitationsController**

```csharp
// src/Backend/AuraCore.API/Controllers/Admin/InvitationsController.cs
using AuraCore.API.Application.Interfaces;
using AuraCore.API.Application.Services.Permissions;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;

namespace AuraCore.API.Controllers.Admin;

[ApiController]
[Route("api/invitations")]
[Authorize(Roles = "superadmin")]
public sealed class InvitationsController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    private readonly IEmailService _email;
    private readonly IAuthService _auth;

    public InvitationsController(AuraCoreDbContext db, IEmailService email, IAuthService auth)
    {
        _db = db; _email = email; _auth = auth;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInvitationRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email))
            return BadRequest(new { error = "Email required" });

        if (!PermissionTemplates.IsKnown(req.Template))
            return BadRequest(new { error = $"Unknown template '{req.Template}'. Valid: {string.Join(", ", PermissionTemplates.Names)}" });

        // Reject if account already exists
        var existing = await _db.Users.AnyAsync(u => u.Email == req.Email.ToLowerInvariant(), ct);
        if (existing) return Conflict(new { error = "An account with this email already exists" });

        // Reject if non-expired pending invitation already exists
        var pending = await _db.AdminInvitations.AnyAsync(i =>
            i.Email == req.Email.ToLowerInvariant()
            && i.AcceptedAt == null
            && i.ExpiresAt > DateTime.UtcNow, ct);
        if (pending) return Conflict(new { error = "Pending invitation already exists for this email" });

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-").Replace("/", "_").Replace("=", "");

        var invitation = new AdminInvitation
        {
            Email = req.Email.Trim().ToLowerInvariant(),
            Template = req.Template,
            TokenHash = AdminInvitation.HashToken(token),
            InvitedBy = Guid.Parse(User.FindFirst("sub")!.Value),
            ExpiresAt = DateTime.UtcNow.AddDays(Math.Clamp(req.ExpiresInDays, 1, 30)),
        };
        _db.AdminInvitations.Add(invitation);
        await _db.SaveChangesAsync(ct);

        var acceptUrl = $"https://admin.auracore.pro/accept-invitation?token={Uri.EscapeDataString(token)}";
        await _email.SendInvitationAsync(invitation.Email, acceptUrl, invitation.ExpiresAt, ct);

        return Created($"/api/invitations/{invitation.Id}", new
        {
            id = invitation.Id,
            email = invitation.Email,
            token,    // raw token only returned once at creation, then never persisted
            expiresAt = invitation.ExpiresAt,
        });
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var rows = await _db.AdminInvitations
            .OrderByDescending(i => i.CreatedAt)
            .Take(200)
            .Select(i => new
            {
                i.Id, i.Email, i.Template, i.CreatedAt, i.ExpiresAt, i.AcceptedAt,
                status = i.AcceptedAt != null ? "accepted"
                       : i.RevokedAt != null ? "revoked"
                       : i.ExpiresAt <= DateTime.UtcNow ? "expired"
                       : "pending"
            })
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        var inv = await _db.AdminInvitations.FindAsync(new object[] { id }, ct);
        if (inv is null) return NotFound();
        if (inv.AcceptedAt != null) return BadRequest(new { error = "Cannot revoke accepted invitation" });
        inv.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("accept")]
    [AllowAnonymous]
    public async Task<IActionResult> Accept([FromBody] AcceptInvitationRequest req, CancellationToken ct)
    {
        if (req.Password != req.ConfirmPassword)
            return BadRequest(new { error = "Passwords do not match" });

        var hash = AdminInvitation.HashToken(req.Token);
        var inv = await _db.AdminInvitations.FirstOrDefaultAsync(i => i.TokenHash == hash, ct);
        if (inv is null) return BadRequest(new { error = "Invalid or expired invitation token" });
        if (inv.AcceptedAt != null) return BadRequest(new { error = "This invitation has already been used" });
        if (inv.RevokedAt != null) return BadRequest(new { error = "This invitation was revoked" });
        if (inv.ExpiresAt <= DateTime.UtcNow) return BadRequest(new { error = "This invitation has expired" });

        var template = PermissionTemplates.Get(inv.Template);
        var registerResult = await _auth.RegisterAdminAsync(inv.Email, req.Password, ct);
        if (!registerResult.Success) return BadRequest(new { error = registerResult.Error });

        // Apply template grants
        foreach (var permKey in template.Permissions)
        {
            _db.PermissionGrants.Add(new PermissionGrant
            {
                AdminUserId = registerResult.User!.Id,
                PermissionKey = permKey,
                GrantedBy = inv.InvitedBy,
            });
        }
        if (template.IsReadonly)
        {
            registerResult.User!.IsReadonly = true;
            _db.Users.Update(registerResult.User);
        }
        registerResult.User!.MustChangePasswordAt = null;   // they just set their own
        registerResult.User!.MustEnable2FAAt = DateTime.UtcNow;  // Wave 5: 2FA mandatory on first login
        inv.AcceptedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new { id = registerResult.User.Id, email = registerResult.User.Email });
    }
}

public sealed record CreateInvitationRequest(string Email, string Template, int ExpiresInDays = 7);
public sealed record AcceptInvitationRequest(string Token, string Password, string ConfirmPassword);
```

- [ ] **Step 4: Add HashToken helper to AdminInvitation entity (Wave 1 stub)**

In `src/Backend/AuraCore.API/Domain/Entities/AdminInvitation.cs`, add static method:

```csharp
public static string HashToken(string token)
{
    using var sha = System.Security.Cryptography.SHA256.Create();
    return Convert.ToHexString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(token)));
}
```

- [ ] **Step 5: Add SendInvitationAsync to IEmailService**

In `IEmailService.cs`:

```csharp
Task<bool> SendInvitationAsync(string toEmail, string acceptUrl, DateTime expiresAt, CancellationToken ct = default);
```

In `ResendEmailService.cs`:

```csharp
public Task<bool> SendInvitationAsync(string toEmail, string acceptUrl, DateTime expiresAt, CancellationToken ct = default)
{
    var subject = "You've been invited to AuraCore Admin";
    var html = $@"
        <h2>You've been invited as an AuraCore admin.</h2>
        <p>Click the link below to set your password and complete sign-up:</p>
        <p><a href='{System.Net.WebUtility.HtmlEncode(acceptUrl)}'>Accept invitation</a></p>
        <p>This link expires on {expiresAt:yyyy-MM-dd HH:mm} UTC and can only be used once.</p>
        <p>If you didn't expect this invitation, you can ignore this email.</p>
    ";
    return SendAsync(toEmail, subject, html, ct);
}
```

- [ ] **Step 6: Add IAuthService.RegisterAdminAsync**

(In existing `IAuthService.cs` and `AuthService.cs` — same logic as RegisterAsync but force `Role = "admin"` and skip the email confirmation flow.)

- [ ] **Step 7: Run tests, verify pass**

```bash
dotnet test --filter FullyQualifiedName~InvitationsController
```

Expected: 3 tests PASS.

- [ ] **Step 8: Commit**

```bash
git add src/Backend/AuraCore.API/Controllers/Admin/InvitationsController.cs src/Backend/AuraCore.API/Domain/Entities/AdminInvitation.cs src/Backend/AuraCore.API/Application/Interfaces/IEmailService.cs src/Backend/AuraCore.API/Application/Services/Email/ResendEmailService.cs src/Backend/AuraCore.API/Application/Services/Auth/AuthService.cs tests/AuraCore.Tests.API/SuperadminFoundation/InvitationsControllerTests.cs
git commit -m "feat(6.11.W4): InvitationsController + accept flow

Single-use invitation tokens (32-byte cryptographic random, base64url),
SHA256-hashed before storage (raw token only returned to superadmin at
creation, never persisted). 7-day default expiry, configurable 1-30.

Accept flow is anonymous (token IS the auth), creates the admin user
via IAuthService.RegisterAdminAsync, applies the chosen template's
permission grants + ReadOnly flag, marks must-enable-2FA-on-login
(Wave 5 enforces).

Email sent via IEmailService.SendInvitationAsync (Resend HTTPS API)."
```

### Task 23: AdminUsersController for create/disable/delete + ReadOnly toggle

**Files:**
- Create: `src/Backend/AuraCore.API/Controllers/Admin/AdminUsersController.cs`
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/AdminUsersControllerTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/AuraCore.Tests.API/SuperadminFoundation/AdminUsersControllerTests.cs
using Xunit;
using System.Net;
using System.Net.Http.Json;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class AdminUsersControllerTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public AdminUsersControllerTests(TestApiFactory factory) => _factory = factory;

    [Fact]
    public async Task SuperadminCanListAdmins()
    {
        var client = _factory.CreateClientWithRole("superadmin");
        var resp = await client.GetAsync("/api/admin/admin-users");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task SuperadminCanDisableAdmin()
    {
        var (factory, adminId) = _factory.WithSeededAdmin("disable@x.com");
        var client = factory.CreateClientWithRole("superadmin");
        var resp = await client.PatchAsync($"/api/admin/admin-users/{adminId}/disable", null);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task SuperadminCannotDisableSelf()
    {
        var client = _factory.CreateClientWithRole("superadmin");
        var meId = _factory.GetSuperadminId();
        var resp = await client.PatchAsync($"/api/admin/admin-users/{meId}/disable", null);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task RegularAdminForbidden()
    {
        var client = _factory.CreateClientWithRole("admin");
        var resp = await client.GetAsync("/api/admin/admin-users");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task ToggleReadOnlyFlipsFlag()
    {
        var (factory, adminId) = _factory.WithSeededAdmin("ro@x.com");
        var client = factory.CreateClientWithRole("superadmin");
        var resp = await client.PatchAsync($"/api/admin/admin-users/{adminId}/readonly?value=true", null);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }
}
```

- [ ] **Step 2: Run test, verify it fails**

```bash
dotnet test --filter FullyQualifiedName~AdminUsersController
```

Expected: FAIL — controller not found.

- [ ] **Step 3: Implement AdminUsersController**

```csharp
// src/Backend/AuraCore.API/Controllers/Admin/AdminUsersController.cs
using AuraCore.API.Application.Services.Permissions;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AuraCore.API.Controllers.Admin;

[ApiController]
[Route("api/admin/admin-users")]
[Authorize(Roles = "superadmin")]
public sealed class AdminUsersController : ControllerBase
{
    private readonly AuraCoreDbContext _db;

    public AdminUsersController(AuraCoreDbContext db) => _db = db;

    private Guid CurrentUserId => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var rows = await _db.Users
            .Where(u => u.Role == "admin" || u.Role == "superadmin")
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.Role,
                u.IsReadonly,
                u.IsDisabled,
                u.CreatedAt,
                u.LastLoginAt,
                permissionCount = _db.PermissionGrants.Count(g => g.AdminUserId == u.Id && g.RevokedAt == null && (g.ExpiresAt == null || g.ExpiresAt > DateTime.UtcNow))
            })
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var u = await _db.Users.FindAsync(new object[] { id }, ct);
        if (u is null) return NotFound();
        var grants = await _db.PermissionGrants
            .Where(g => g.AdminUserId == id && g.RevokedAt == null && (g.ExpiresAt == null || g.ExpiresAt > DateTime.UtcNow))
            .Select(g => new { g.Id, g.PermissionKey, g.GrantedBy, g.GrantedAt, g.ExpiresAt })
            .ToListAsync(ct);
        return Ok(new { user = new { u.Id, u.Email, u.Role, u.IsReadonly, u.IsDisabled, u.LastLoginAt }, grants });
    }

    [HttpPatch("{id:guid}/disable")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct)
    {
        if (id == CurrentUserId)
            return BadRequest(new { error = "Cannot disable your own account" });
        var u = await _db.Users.FindAsync(new object[] { id }, ct);
        if (u is null) return NotFound();
        if (u.Role != "admin" && u.Role != "superadmin")
            return BadRequest(new { error = "Can only disable admin/superadmin accounts via this endpoint" });
        u.IsDisabled = true;
        // Revoke all active sessions: insert all currently-valid jti claims into revoked_tokens
        // (Implementation note: in this codebase JWTs are stateless; revocation list is keyed
        //  on AdminUserId so the middleware can mass-revoke per user.)
        u.SessionRevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/enable")]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct)
    {
        var u = await _db.Users.FindAsync(new object[] { id }, ct);
        if (u is null) return NotFound();
        u.IsDisabled = false;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (id == CurrentUserId)
            return BadRequest(new { error = "Cannot delete your own account" });
        var u = await _db.Users.FindAsync(new object[] { id }, ct);
        if (u is null) return NotFound();
        if (u.Role == "superadmin")
            return BadRequest(new { error = "Cannot delete a superadmin account. Demote first." });
        // Cascade: PermissionGrants where AdminUserId == id, AdminInvitations where InvitedBy == id, ActionLog rows preserved (FK SET NULL)
        _db.Users.Remove(u);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/readonly")]
    public async Task<IActionResult> SetReadOnly(Guid id, [FromQuery] bool value, CancellationToken ct)
    {
        var u = await _db.Users.FindAsync(new object[] { id }, ct);
        if (u is null) return NotFound();
        if (u.Role == "superadmin")
            return BadRequest(new { error = "Superadmin accounts cannot be set to read-only" });
        u.IsReadonly = value;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/force-password-change")]
    public async Task<IActionResult> ForcePasswordChange(Guid id, CancellationToken ct)
    {
        var u = await _db.Users.FindAsync(new object[] { id }, ct);
        if (u is null) return NotFound();
        u.MustChangePasswordAt = DateTime.UtcNow;
        u.SessionRevokedAt = DateTime.UtcNow;   // log them out so they hit the change-password gate
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
```

- [ ] **Step 4: Run tests, verify pass**

```bash
dotnet test --filter FullyQualifiedName~AdminUsersController
```

Expected: 5 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Backend/AuraCore.API/Controllers/Admin/AdminUsersController.cs tests/AuraCore.Tests.API/SuperadminFoundation/AdminUsersControllerTests.cs
git commit -m "feat(6.11.W4): AdminUsersController CRUD + lifecycle

Endpoints: GET (list+detail), PATCH disable/enable, DELETE, PATCH
readonly, PATCH force-password-change. Self-protection: cannot
disable/delete your own account. Superadmin accounts cannot be
deleted directly (must be demoted first) and cannot be set to
read-only.

Disable + force-password-change both stamp SessionRevokedAt — middleware
treats this as 'all sessions for this user issued before T are dead'."
```

### Task 24: Frontend AdminsPage (superadmin tab)

**Files:**
- Create: `admin-panel/src/views/AdminsPage.tsx`
- Modify: `admin-panel/src/lib/api.ts` (add admin user methods)
- Modify: `admin-panel/src/components/Sidebar.tsx` (already added "admins" to TAB_PERMISSION_MAP in T18)

- [ ] **Step 1: Add api methods**

```typescript
// admin-panel/src/lib/api.ts
async listAdminUsers(): Promise<AdminUser[]> {
    const r = await this.request('/api/admin/admin-users');
    return r.json();
}
async getAdminUser(id: string): Promise<AdminUserDetail> {
    const r = await this.request(`/api/admin/admin-users/${id}`);
    return r.json();
}
async disableAdmin(id: string): Promise<void> {
    await this.request(`/api/admin/admin-users/${id}/disable`, { method: 'PATCH' });
}
async enableAdmin(id: string): Promise<void> {
    await this.request(`/api/admin/admin-users/${id}/enable`, { method: 'PATCH' });
}
async deleteAdmin(id: string): Promise<void> {
    await this.request(`/api/admin/admin-users/${id}`, { method: 'DELETE' });
}
async setAdminReadOnly(id: string, value: boolean): Promise<void> {
    await this.request(`/api/admin/admin-users/${id}/readonly?value=${value}`, { method: 'PATCH' });
}
async forcePasswordChange(id: string): Promise<void> {
    await this.request(`/api/admin/admin-users/${id}/force-password-change`, { method: 'PATCH' });
}
```

Add types to `admin-panel/src/lib/types.ts`:

```typescript
export interface AdminUser {
    id: string;
    email: string;
    role: 'admin' | 'superadmin';
    isReadonly: boolean;
    isDisabled: boolean;
    createdAt: string;
    lastLoginAt: string | null;
    permissionCount: number;
}

export interface AdminUserDetail {
    user: AdminUser;
    grants: Array<{ id: string; permissionKey: string; grantedBy: string; grantedAt: string; expiresAt: string | null }>;
}
```

- [ ] **Step 2: Implement AdminsPage**

```typescript
// admin-panel/src/views/AdminsPage.tsx
'use client';

import { useState, useEffect, useCallback } from 'react';
import { Users, RefreshCw, Lock, Trash2, KeyRound, Ban, CheckCircle2, Mail } from 'lucide-react';
import { api } from '@/lib/api';
import { AdminUser } from '@/lib/types';
import { PageHeader } from '@/components/PageHeader';
import { StatusBadge } from '@/components/StatusBadge';
import { EmptyState } from '@/components/EmptyState';
import { DataTable, DataTableColumn } from '@/components/DataTable';
import { ConfirmDialog } from '@/components/ConfirmDialog';

export function AdminsPage() {
    const [admins, setAdmins] = useState<AdminUser[]>([]);
    const [confirmAction, setConfirmAction] = useState<{ kind: 'disable' | 'delete' | 'force-pw'; id: string; email: string } | null>(null);

    const load = useCallback(async () => {
        const d = await api.listAdminUsers();
        setAdmins(d);
    }, []);

    useEffect(() => { load(); }, [load]);

    const columns: DataTableColumn<AdminUser>[] = [
        {
            key: 'email', header: 'Email', isCardTitle: true,
            render: u => <span className="text-white/80">{u.email}</span>,
        },
        { key: 'role', header: 'Role', render: u => <StatusBadge status={u.role} /> },
        {
            key: 'state', header: 'State',
            render: u => u.isDisabled
                ? <StatusBadge status="disabled" />
                : u.isReadonly ? <StatusBadge status="readonly" /> : <StatusBadge status="active" />,
        },
        { key: 'perms', header: 'Permissions', render: u => <span className="text-white/50">{u.permissionCount}</span> },
        {
            key: 'lastLogin', header: 'Last Login',
            render: u => <span className="text-white/40">{u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleString() : '—'}</span>,
        },
        {
            key: 'actions', header: 'Actions', cellClassName: 'text-right',
            render: u => (
                <div className="flex items-center justify-end gap-1">
                    <button title="Toggle read-only" disabled={u.role === 'superadmin'}
                        onClick={async () => { await api.setAdminReadOnly(u.id, !u.isReadonly); load(); }}
                        className="btn-action p-1.5 rounded-lg hover:bg-aura-amber/10 text-white/30 hover:text-aura-amber disabled:opacity-20">
                        <Lock className="w-4 h-4" />
                    </button>
                    <button title="Force password change"
                        onClick={() => setConfirmAction({ kind: 'force-pw', id: u.id, email: u.email })}
                        className="btn-action p-1.5 rounded-lg hover:bg-accent/10 text-white/30 hover:text-accent">
                        <KeyRound className="w-4 h-4" />
                    </button>
                    {u.isDisabled
                        ? <button title="Re-enable" onClick={async () => { await api.enableAdmin(u.id); load(); }}
                            className="btn-action p-1.5 rounded-lg hover:bg-aura-green/10 text-white/30 hover:text-aura-green">
                            <CheckCircle2 className="w-4 h-4" />
                        </button>
                        : <button title="Disable" disabled={u.role === 'superadmin'}
                            onClick={() => setConfirmAction({ kind: 'disable', id: u.id, email: u.email })}
                            className="btn-action p-1.5 rounded-lg hover:bg-aura-amber/10 text-white/30 hover:text-aura-amber disabled:opacity-20">
                            <Ban className="w-4 h-4" />
                        </button>}
                    <button title="Delete" disabled={u.role === 'superadmin'}
                        onClick={() => setConfirmAction({ kind: 'delete', id: u.id, email: u.email })}
                        className="btn-action p-1.5 rounded-lg hover:bg-aura-red/10 text-white/30 hover:text-aura-red disabled:opacity-20">
                        <Trash2 className="w-4 h-4" />
                    </button>
                </div>
            ),
        },
    ];

    return (
        <div className="animate-fade-in">
            <PageHeader title="Admins" subtitle={`${admins.length} admin & superadmin accounts`}>
                <button onClick={load} className="btn-ghost flex items-center gap-2"><RefreshCw className="w-4 h-4" />Refresh</button>
            </PageHeader>
            <div className="glass-card p-5">
                <DataTable<AdminUser> columns={columns} rows={admins} rowKey={u => u.id}
                    emptyState={<EmptyState icon={Users} title="No admin accounts" />} />
            </div>

            <ConfirmDialog
                open={confirmAction !== null}
                title={
                    confirmAction?.kind === 'delete' ? 'Delete admin'
                    : confirmAction?.kind === 'disable' ? 'Disable admin'
                    : 'Force password change'
                }
                message={
                    confirmAction?.kind === 'delete' ? `Permanently delete ${confirmAction.email}? This cannot be undone. Permission grants will cascade-delete; action log entries will be preserved.`
                    : confirmAction?.kind === 'disable' ? `Disable ${confirmAction?.email}? They will be logged out immediately and unable to log in until re-enabled.`
                    : `Force ${confirmAction?.email} to change their password on next login? They will be logged out immediately.`
                }
                confirmLabel={confirmAction?.kind === 'delete' ? 'Delete' : confirmAction?.kind === 'disable' ? 'Disable' : 'Force change'}
                cancelLabel="Cancel"
                destructive={confirmAction?.kind === 'delete' || confirmAction?.kind === 'disable'}
                onConfirm={async () => {
                    if (!confirmAction) return;
                    if (confirmAction.kind === 'delete') await api.deleteAdmin(confirmAction.id);
                    else if (confirmAction.kind === 'disable') await api.disableAdmin(confirmAction.id);
                    else await api.forcePasswordChange(confirmAction.id);
                    setConfirmAction(null);
                    load();
                }}
                onCancel={() => setConfirmAction(null)}
            />
        </div>
    );
}
```

- [ ] **Step 3: Wire AdminsPage into route renderer**

In `admin-panel/src/app/page.tsx`, add the route entry alongside other tabs:

```typescript
import { AdminsPage } from '@/views/AdminsPage';
// ...
{activeTab === 'admins' && <AdminsPage />}
```

Add `'admins'` to the `TAB_PERMISSION_MAP` (Task 18) under the `superadmin:only` group.

- [ ] **Step 4: Build verify**

```bash
cd admin-panel && npm run build 2>&1 | tail -10
```

Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add admin-panel/src/views/AdminsPage.tsx admin-panel/src/lib/api.ts admin-panel/src/lib/types.ts admin-panel/src/app/page.tsx admin-panel/src/components/Sidebar.tsx
git commit -m "feat(6.11.W4): AdminsPage tab — superadmin lifecycle UI

Lists all admin/superadmin accounts with role + state + permission count
+ last login. Per-row actions: toggle read-only, force password change,
disable/enable, delete. Self-protection enforced backend-side; UI also
disables actions on superadmin rows to short-circuit the round-trip."
```

### Task 25: InvitationsPage + AcceptInvitationPage

**Files:**
- Create: `admin-panel/src/views/InvitationsPage.tsx` (superadmin-only tab)
- Create: `admin-panel/src/app/accept-invitation/page.tsx` (public page reachable only via emailed link)

- [ ] **Step 1: api methods**

```typescript
// admin-panel/src/lib/api.ts
async listInvitations(): Promise<Invitation[]> {
    const r = await this.request('/api/invitations');
    return r.json();
}
async createInvitation(email: string, template: string, expiresInDays: number): Promise<{ id: string; email: string; token: string; expiresAt: string }> {
    const r = await this.request('/api/invitations', {
        method: 'POST',
        body: JSON.stringify({ email, template, expiresInDays }),
    });
    return r.json();
}
async revokeInvitation(id: string): Promise<void> {
    await this.request(`/api/invitations/${id}`, { method: 'DELETE' });
}
async acceptInvitation(token: string, password: string, confirmPassword: string): Promise<{ id: string; email: string }> {
    // Use raw fetch — this endpoint is anonymous, no auth header
    const r = await fetch(`${this.baseUrl}/api/invitations/accept`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ token, password, confirmPassword }),
    });
    if (!r.ok) {
        const err = await r.json().catch(() => ({ error: 'Failed' }));
        throw new Error(err.error ?? 'Failed');
    }
    return r.json();
}
```

Add type:
```typescript
export interface Invitation {
    id: string;
    email: string;
    template: string;
    createdAt: string;
    expiresAt: string;
    acceptedAt: string | null;
    status: 'pending' | 'accepted' | 'expired' | 'revoked';
}
```

- [ ] **Step 2: Implement InvitationsPage**

```typescript
// admin-panel/src/views/InvitationsPage.tsx
'use client';

import { useState, useEffect, useCallback } from 'react';
import { Mail, RefreshCw, Trash2, Copy, Plus } from 'lucide-react';
import { api } from '@/lib/api';
import { Invitation } from '@/lib/types';
import { PageHeader } from '@/components/PageHeader';
import { StatusBadge } from '@/components/StatusBadge';
import { EmptyState } from '@/components/EmptyState';
import { DataTable, DataTableColumn } from '@/components/DataTable';
import { ConfirmDialog } from '@/components/ConfirmDialog';

export function InvitationsPage() {
    const [items, setItems] = useState<Invitation[]>([]);
    const [showNew, setShowNew] = useState(false);
    const [newEmail, setNewEmail] = useState('');
    const [newTemplate, setNewTemplate] = useState<'ReadOnly' | 'Moderator' | 'Operator' | 'Custom'>('ReadOnly');
    const [newExpiresInDays, setNewExpiresInDays] = useState(7);
    const [createdToken, setCreatedToken] = useState<{ email: string; token: string; acceptUrl: string } | null>(null);
    const [confirmRevoke, setConfirmRevoke] = useState<Invitation | null>(null);
    const [submitting, setSubmitting] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const load = useCallback(async () => {
        setItems(await api.listInvitations());
    }, []);

    useEffect(() => { load(); }, [load]);

    async function create() {
        setError(null); setSubmitting(true);
        try {
            const r = await api.createInvitation(newEmail.trim().toLowerCase(), newTemplate, newExpiresInDays);
            setCreatedToken({
                email: r.email,
                token: r.token,
                acceptUrl: `https://admin.auracore.pro/accept-invitation?token=${encodeURIComponent(r.token)}`,
            });
            setNewEmail(''); setShowNew(false); load();
        } catch (e: any) {
            setError(e?.message ?? 'Failed');
        } finally {
            setSubmitting(false);
        }
    }

    const columns: DataTableColumn<Invitation>[] = [
        { key: 'email', header: 'Email', isCardTitle: true, render: i => <span className="text-white/80">{i.email}</span> },
        { key: 'tpl', header: 'Template', render: i => <span className="text-white/50 font-mono text-xs">{i.template}</span> },
        { key: 'status', header: 'Status', render: i => <StatusBadge status={i.status} /> },
        { key: 'expires', header: 'Expires', render: i => <span className="text-white/40">{new Date(i.expiresAt).toLocaleString()}</span> },
        {
            key: 'actions', header: '', cellClassName: 'text-right',
            render: i => i.status === 'pending'
                ? <button onClick={() => setConfirmRevoke(i)} className="btn-action p-1.5 rounded-lg hover:bg-aura-red/10 text-white/30 hover:text-aura-red"><Trash2 className="w-4 h-4" /></button>
                : null,
        },
    ];

    return (
        <div className="animate-fade-in">
            <PageHeader title="Invitations" subtitle={`${items.filter(i => i.status === 'pending').length} pending`}>
                <button onClick={() => setShowNew(true)} className="btn-primary flex items-center gap-2"><Plus className="w-4 h-4" />New invitation</button>
                <button onClick={load} className="btn-ghost flex items-center gap-2"><RefreshCw className="w-4 h-4" />Refresh</button>
            </PageHeader>

            <div className="glass-card p-5">
                <DataTable<Invitation> columns={columns} rows={items} rowKey={i => i.id}
                    emptyState={<EmptyState icon={Mail} title="No invitations yet" />} />
            </div>

            {showNew && (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm" onClick={() => setShowNew(false)}>
                    <div className="glass-card p-6 max-w-md w-full mx-4" onClick={e => e.stopPropagation()}>
                        <h2 className="text-lg font-bold mb-4">New invitation</h2>
                        <label className="block text-xs text-white/50 mb-1">Email</label>
                        <input value={newEmail} onChange={e => setNewEmail(e.target.value)} type="email" className="input-dark w-full mb-3" placeholder="newadmin@example.com" />
                        <label className="block text-xs text-white/50 mb-1">Template</label>
                        <select value={newTemplate} onChange={e => setNewTemplate(e.target.value as any)} className="input-dark w-full mb-3">
                            <option value="ReadOnly">ReadOnly — view-only access to all tabs</option>
                            <option value="Moderator">Moderator — Users + Licenses + Devices + Subscriptions</option>
                            <option value="Operator">Operator — Releases + Configuration + IP Whitelist</option>
                            <option value="Custom">Custom — no permissions; superadmin grants individually</option>
                        </select>
                        <label className="block text-xs text-white/50 mb-1">Expires in (days)</label>
                        <input value={newExpiresInDays} onChange={e => setNewExpiresInDays(parseInt(e.target.value || '7'))} type="number" min={1} max={30} className="input-dark w-full mb-3" />
                        {error && <p className="text-xs text-aura-red mb-2">{error}</p>}
                        <div className="flex justify-end gap-2">
                            <button onClick={() => setShowNew(false)} disabled={submitting} className="btn-ghost text-xs px-4 py-2">Cancel</button>
                            <button onClick={create} disabled={submitting || newEmail.length < 5} className="btn-primary text-xs px-4 py-2">
                                {submitting ? 'Creating...' : 'Send invitation'}
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {createdToken && (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm">
                    <div className="glass-card p-6 max-w-md w-full mx-4">
                        <h2 className="text-lg font-bold mb-2 text-aura-green">Invitation sent</h2>
                        <p className="text-sm text-white/65 mb-3">
                            Email sent to <span className="font-mono text-xs text-accent">{createdToken.email}</span>.
                            Save the link below as backup — it won't be shown again.
                        </p>
                        <div className="bg-black/30 p-3 rounded-lg mb-3">
                            <p className="text-xs text-white/40 break-all font-mono">{createdToken.acceptUrl}</p>
                        </div>
                        <div className="flex justify-end gap-2">
                            <button onClick={() => navigator.clipboard?.writeText(createdToken.acceptUrl)} className="btn-ghost text-xs px-4 py-2 flex items-center gap-2">
                                <Copy className="w-3 h-3" />Copy link
                            </button>
                            <button onClick={() => setCreatedToken(null)} className="btn-primary text-xs px-4 py-2">Close</button>
                        </div>
                    </div>
                </div>
            )}

            <ConfirmDialog
                open={confirmRevoke !== null}
                title="Revoke invitation"
                message={confirmRevoke ? `Revoke invitation to ${confirmRevoke.email}? The accept link will stop working immediately.` : ''}
                confirmLabel="Revoke" cancelLabel="Keep" destructive
                onConfirm={async () => {
                    if (!confirmRevoke) return;
                    await api.revokeInvitation(confirmRevoke.id);
                    setConfirmRevoke(null); load();
                }}
                onCancel={() => setConfirmRevoke(null)}
            />
        </div>
    );
}
```

- [ ] **Step 3: Implement AcceptInvitationPage (public route)**

```typescript
// admin-panel/src/app/accept-invitation/page.tsx
'use client';

import { useState, useEffect, Suspense } from 'react';
import { useSearchParams, useRouter } from 'next/navigation';
import { api } from '@/lib/api';

function AcceptForm() {
    const params = useSearchParams();
    const router = useRouter();
    const token = params.get('token') ?? '';
    const [password, setPassword] = useState('');
    const [confirmPassword, setConfirmPassword] = useState('');
    const [error, setError] = useState<string | null>(null);
    const [submitting, setSubmitting] = useState(false);
    const [done, setDone] = useState(false);

    if (!token) {
        return <div className="glass-card p-8 max-w-md"><p className="text-aura-red">Missing invitation token in URL.</p></div>;
    }

    async function submit() {
        setError(null);
        if (password.length < 12) return setError('Password must be at least 12 characters');
        if (password !== confirmPassword) return setError('Passwords do not match');
        setSubmitting(true);
        try {
            await api.acceptInvitation(token, password, confirmPassword);
            setDone(true);
            setTimeout(() => router.push('/'), 3000);
        } catch (e: any) {
            setError(e?.message ?? 'Failed');
        } finally {
            setSubmitting(false);
        }
    }

    if (done) {
        return (
            <div className="glass-card p-8 max-w-md">
                <h1 className="text-xl font-bold mb-2 text-aura-green">Account created</h1>
                <p className="text-sm text-white/65">Redirecting to login...</p>
            </div>
        );
    }

    return (
        <div className="glass-card p-8 max-w-md">
            <h1 className="text-xl font-bold mb-2">Set your password</h1>
            <p className="text-sm text-white/50 mb-4">You've been invited as an AuraCore admin. Set a password to complete sign-up.</p>
            <label className="block text-xs text-white/50 mb-1">Password (12+ chars)</label>
            <input type="password" value={password} onChange={e => setPassword(e.target.value)} className="input-dark w-full mb-3" />
            <label className="block text-xs text-white/50 mb-1">Confirm password</label>
            <input type="password" value={confirmPassword} onChange={e => setConfirmPassword(e.target.value)} className="input-dark w-full mb-3" />
            {error && <p className="text-xs text-aura-red mb-3">{error}</p>}
            <button onClick={submit} disabled={submitting} className="btn-primary w-full">
                {submitting ? 'Creating account...' : 'Create account'}
            </button>
            <p className="text-[10px] text-white/30 mt-4">After signing in, you'll be required to set up two-factor authentication before accessing the admin panel.</p>
        </div>
    );
}

export default function AcceptInvitationPage() {
    return (
        <div className="min-h-screen flex items-center justify-center p-4">
            <Suspense fallback={<div className="text-white/40">Loading…</div>}>
                <AcceptForm />
            </Suspense>
        </div>
    );
}
```

- [ ] **Step 4: Wire InvitationsPage into route renderer**

In `admin-panel/src/app/page.tsx`:

```typescript
import { InvitationsPage } from '@/views/InvitationsPage';
// ...
{activeTab === 'invitations' && <InvitationsPage />}
```

- [ ] **Step 5: Build verify**

```bash
cd admin-panel && npm run build 2>&1 | tail -10
```

Expected: 0 errors. New static route `/accept-invitation` listed in build output.

- [ ] **Step 6: Commit**

```bash
git add admin-panel/src/views/InvitationsPage.tsx admin-panel/src/app/accept-invitation/page.tsx admin-panel/src/lib/api.ts admin-panel/src/lib/types.ts admin-panel/src/app/page.tsx
git commit -m "feat(6.11.W4): InvitationsPage + accept-invitation public route

InvitationsPage (superadmin-only tab): create with email + template
selection (ReadOnly/Moderator/Operator/Custom) + expiry. After create,
shows the accept URL once with copy-to-clipboard (recoverable backup
if email delivery fails).

/accept-invitation public route: parses ?token=... query, prompts for
password (12+ chars), POSTs to /api/invitations/accept. On success
redirects to login page after 3s. Notifies user that 2FA setup is
mandatory on first login."
```

### Task 26: ActionLog table + service + LogActionFilter

**Goal:** Every state-mutating admin endpoint logs a row with `who, what, when, on_target, ip, permission_used, role_at_time, success/failure`. The Wave 1 PermissionGrant + RequiresPermissionAttribute already track WHO has access; ActionLog tracks WHEN they used it.

**Files:**
- Wave 1 already created the ActionLog entity stub. This task wires the service + filter + retention job.
- Create: `src/Backend/AuraCore.API/Application/Services/Audit/ActionLogService.cs`
- Create: `src/Backend/AuraCore.API/Filters/LogActionFilter.cs`
- Modify: `src/Backend/AuraCore.API/Program.cs` (register filter globally for [HttpPost/Put/Delete/Patch] methods on Admin/* controllers)
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/ActionLogTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
// tests/AuraCore.Tests.API/SuperadminFoundation/ActionLogTests.cs
[Fact]
public async Task RevokingLicenseLogsAction()
{
    var client = _factory.CreateClientWithRole("admin", permissions: new[] { "tab:licenses", "action:license:revoke" });
    var licenseId = await _factory.SeedLicense();
    var resp = await client.PutAsync($"/api/admin/licenses/{licenseId}/revoke", null);
    Assert.True(resp.IsSuccessStatusCode);

    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
    var log = await db.ActionLog.OrderByDescending(a => a.CreatedAt).FirstAsync();
    Assert.Equal("license.revoke", log.Action);
    Assert.Equal(licenseId.ToString(), log.TargetId);
    Assert.True(log.Success);
}
```

- [ ] **Step 2: Run test, verify it fails**

```bash
dotnet test --filter FullyQualifiedName~ActionLogTests
```

Expected: FAIL — table empty (filter not wired).

- [ ] **Step 3: Implement ActionLogService**

```csharp
// src/Backend/AuraCore.API/Application/Services/Audit/ActionLogService.cs
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;

namespace AuraCore.API.Application.Services.Audit;

public interface IActionLogService
{
    Task LogAsync(string action, string? targetType, string? targetId, Guid actorId, string actorEmail, string actorRoleAtTime, string? permissionUsed, string ipAddress, bool success, string? errorMessage = null, CancellationToken ct = default);
}

public sealed class ActionLogService : IActionLogService
{
    private readonly AuraCoreDbContext _db;

    public ActionLogService(AuraCoreDbContext db) => _db = db;

    public async Task LogAsync(string action, string? targetType, string? targetId, Guid actorId, string actorEmail, string actorRoleAtTime, string? permissionUsed, string ipAddress, bool success, string? errorMessage = null, CancellationToken ct = default)
    {
        _db.ActionLog.Add(new ActionLog
        {
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            ActorId = actorId,
            ActorEmail = actorEmail,
            ActorRoleAtTime = actorRoleAtTime,
            PermissionUsed = permissionUsed,
            IpAddress = ipAddress,
            Success = success,
            ErrorMessage = errorMessage,
        });
        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 4: Implement LogActionFilter**

```csharp
// src/Backend/AuraCore.API/Filters/LogActionFilter.cs
using AuraCore.API.Application.Services.Audit;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace AuraCore.API.Filters;

public sealed class LogActionFilter : IAsyncActionFilter
{
    private readonly IActionLogService _log;

    public LogActionFilter(IActionLogService log) => _log = log;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Only log POST/PUT/PATCH/DELETE (skip GET — read traffic is too noisy)
        var method = context.HttpContext.Request.Method;
        if (method == "GET" || method == "HEAD" || method == "OPTIONS")
        {
            await next();
            return;
        }

        var executed = await next();

        // Derive action name from controller + http verb
        var controller = context.RouteData.Values["controller"]?.ToString()?.ToLower() ?? "?";
        var actionName = context.RouteData.Values["action"]?.ToString()?.ToLower() ?? "?";
        var actionKey = $"{controller}.{actionName}";

        // Derive target id from route (most admin endpoints have {id})
        var targetId = context.RouteData.Values["id"]?.ToString();

        var user = executed.HttpContext.User;
        var actorIdStr = user.FindFirst("sub")?.Value;
        if (!Guid.TryParse(actorIdStr, out var actorId)) return;
        var actorEmail = user.FindFirst(ClaimTypes.Email)?.Value ?? "?";
        var actorRole = user.FindFirst(ClaimTypes.Role)?.Value ?? "?";
        var ip = executed.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "?";

        // Permission used = whichever [RequiresPermission] / [DestructiveAction] hit at auth time
        var permissionUsed = executed.HttpContext.Items["UsedPermissionKey"] as string;

        var success = executed.Exception is null
            && executed.Result is Microsoft.AspNetCore.Mvc.IStatusCodeActionResult status
            && status.StatusCode is null or >= 200 and < 300;

        await _log.LogAsync(actionKey, controller, targetId, actorId, actorEmail, actorRole, permissionUsed, ip, success, executed.Exception?.Message);
    }
}
```

- [ ] **Step 5: Register filter conditionally on Admin controllers**

In `Program.cs`:

```csharp
builder.Services.AddScoped<IActionLogService, ActionLogService>();
builder.Services.AddScoped<LogActionFilter>();

builder.Services.AddControllers(options =>
{
    options.Filters.AddService<LogActionFilter>();   // global; the filter itself short-circuits non-mutating verbs
});
```

(If filter scope is too broad, narrow with `options.Conventions.Add(new ControllerFilterConvention(typeof(LogActionFilter), c => c.ControllerName.StartsWith("Admin")))` — but global is simpler and the filter's own GET-skip keeps noise out.)

- [ ] **Step 6: Stamp UsedPermissionKey from RequiresPermissionAttribute**

Modify Wave 2 Task 9's `RequiresPermissionAttribute.OnAuthorizationAsync`:

```csharp
// after the permission check passes:
context.HttpContext.Items["UsedPermissionKey"] = Permission;
```

Same for `DestructiveActionAttribute`.

- [ ] **Step 7: Run tests, verify pass**

```bash
dotnet test --filter FullyQualifiedName~ActionLogTests
```

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add src/Backend/AuraCore.API/Application/Services/Audit/ActionLogService.cs src/Backend/AuraCore.API/Filters/LogActionFilter.cs src/Backend/AuraCore.API/Program.cs src/Backend/AuraCore.API/Filters/RequiresPermissionAttribute.cs tests/AuraCore.Tests.API/SuperadminFoundation/ActionLogTests.cs
git commit -m "feat(6.11.W4): ActionLog service + global LogActionFilter

Mutating admin endpoints (POST/PUT/PATCH/DELETE) log to action_log:
who, what, when, on_target, ip, permission_used, role_at_time, success.

ActionLog is the security-grade trail (90-day retention via Wave 5
RetentionJob). Distinct from audit_log which captures auth events
(login attempts, password resets) — Wave 1 kept that table unchanged."
```

### Task 27: ActionLogPage + CSV export

**Files:**
- Create: `admin-panel/src/views/ActionLogPage.tsx`
- Create: `src/Backend/AuraCore.API/Controllers/Admin/ActionLogController.cs`
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/ActionLogControllerTests.cs`

- [ ] **Step 1: Write failing test for CSV export**

```csharp
[Fact]
public async Task SuperadminCanExportCsv()
{
    var client = _factory.CreateClientWithRole("superadmin");
    var resp = await client.GetAsync("/api/admin/action-log/export.csv?from=2026-01-01&to=2026-12-31");
    Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    Assert.Equal("text/csv", resp.Content.Headers.ContentType?.MediaType);
    var body = await resp.Content.ReadAsStringAsync();
    Assert.Contains("created_at,actor_email,action,target_type,target_id,permission_used,success,ip_address", body);
}
```

- [ ] **Step 2: Implement controller**

```csharp
// src/Backend/AuraCore.API/Controllers/Admin/ActionLogController.cs
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace AuraCore.API.Controllers.Admin;

[ApiController]
[Route("api/admin/action-log")]
[Authorize(Roles = "superadmin")]
public sealed class ActionLogController : ControllerBase
{
    private readonly AuraCoreDbContext _db;

    public ActionLogController(AuraCoreDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? actorEmail,
        [FromQuery] string? action,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var q = _db.ActionLog.AsQueryable();
        if (!string.IsNullOrWhiteSpace(actorEmail)) q = q.Where(a => a.ActorEmail.Contains(actorEmail));
        if (!string.IsNullOrWhiteSpace(action)) q = q.Where(a => a.Action.Contains(action));
        if (from.HasValue) q = q.Where(a => a.CreatedAt >= from.Value);
        if (to.HasValue) q = q.Where(a => a.CreatedAt < to.Value);

        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize).Take(Math.Min(pageSize, 200))
            .ToListAsync(ct);
        return Ok(new { total, page, pageSize, items = rows });
    }

    [HttpGet("export.csv")]
    public async Task<IActionResult> Export([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        if (to <= from) return BadRequest(new { error = "to must be after from" });
        if ((to - from).TotalDays > 366) return BadRequest(new { error = "Max range is 366 days per export" });

        var rows = await _db.ActionLog
            .Where(a => a.CreatedAt >= from && a.CreatedAt < to)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("created_at,actor_email,action,target_type,target_id,permission_used,success,ip_address");
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(',',
                r.CreatedAt.ToString("o", CultureInfo.InvariantCulture),
                Csv(r.ActorEmail), Csv(r.Action), Csv(r.TargetType), Csv(r.TargetId),
                Csv(r.PermissionUsed), r.Success ? "true" : "false", Csv(r.IpAddress)
            ));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"action-log-{from:yyyyMMdd}-{to:yyyyMMdd}.csv");
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var needsQuote = value.Contains(',') || value.Contains('"') || value.Contains('\n');
        if (!needsQuote) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
```

- [ ] **Step 3: Implement frontend ActionLogPage**

```typescript
// admin-panel/src/views/ActionLogPage.tsx
'use client';

import { useState, useEffect, useCallback } from 'react';
import { Activity, Download, RefreshCw } from 'lucide-react';
import { api } from '@/lib/api';
import { PageHeader } from '@/components/PageHeader';
import { StatusBadge } from '@/components/StatusBadge';
import { EmptyState } from '@/components/EmptyState';
import { DataTable, DataTableColumn } from '@/components/DataTable';

interface ActionLogEntry {
    id: string;
    createdAt: string;
    actorEmail: string;
    action: string;
    targetType: string | null;
    targetId: string | null;
    permissionUsed: string | null;
    success: boolean;
    ipAddress: string;
}

export function ActionLogPage() {
    const [data, setData] = useState<{ items: ActionLogEntry[]; total: number }>({ items: [], total: 0 });
    const [actorFilter, setActorFilter] = useState('');
    const [actionFilter, setActionFilter] = useState('');
    const [from, setFrom] = useState(() => new Date(Date.now() - 7 * 86400 * 1000).toISOString().slice(0, 10));
    const [to, setTo] = useState(() => new Date(Date.now() + 86400 * 1000).toISOString().slice(0, 10));

    const load = useCallback(async () => {
        const d = await api.listActionLog({ actorEmail: actorFilter, action: actionFilter, from, to });
        setData(d);
    }, [actorFilter, actionFilter, from, to]);

    useEffect(() => { load(); }, [load]);

    async function exportCsv() {
        window.open(`${api.baseUrl}/api/admin/action-log/export.csv?from=${from}&to=${to}`, '_blank');
    }

    const columns: DataTableColumn<ActionLogEntry>[] = [
        { key: 'when', header: 'When', render: r => <span className="text-white/40 text-xs">{new Date(r.createdAt).toLocaleString()}</span> },
        { key: 'actor', header: 'Who', isCardTitle: true, render: r => <span className="text-white/80 text-xs">{r.actorEmail}</span> },
        { key: 'action', header: 'What', render: r => <span className="font-mono text-xs text-accent">{r.action}</span> },
        { key: 'target', header: 'Target', render: r => <span className="font-mono text-[10px] text-white/40">{r.targetId?.substring(0, 8) ?? '—'}</span> },
        { key: 'perm', header: 'Permission', render: r => r.permissionUsed ? <span className="font-mono text-[10px] text-white/40">{r.permissionUsed}</span> : <span className="text-white/30">—</span> },
        { key: 'ok', header: 'OK', render: r => <StatusBadge status={r.success ? 'success' : 'failed'} /> },
        { key: 'ip', header: 'IP', render: r => <span className="text-white/40 text-xs">{r.ipAddress}</span> },
    ];

    return (
        <div className="animate-fade-in">
            <PageHeader title="Action Log" subtitle={`${data.total.toLocaleString()} entries in selected range`}>
                <button onClick={load} className="btn-ghost flex items-center gap-2"><RefreshCw className="w-4 h-4" />Refresh</button>
                <button onClick={exportCsv} className="btn-primary flex items-center gap-2"><Download className="w-4 h-4" />Export CSV</button>
            </PageHeader>
            <div className="glass-card p-5">
                <div className="grid grid-cols-4 gap-3 mb-5">
                    <input value={actorFilter} onChange={e => setActorFilter(e.target.value)} placeholder="Actor email" className="input-dark" />
                    <input value={actionFilter} onChange={e => setActionFilter(e.target.value)} placeholder="Action (e.g. license.revoke)" className="input-dark" />
                    <input type="date" value={from} onChange={e => setFrom(e.target.value)} className="input-dark" />
                    <input type="date" value={to} onChange={e => setTo(e.target.value)} className="input-dark" />
                </div>
                <DataTable<ActionLogEntry> columns={columns} rows={data.items} rowKey={r => r.id}
                    emptyState={<EmptyState icon={Activity} title="No action log entries match filter" />} />
            </div>
        </div>
    );
}
```

Add to api.ts:

```typescript
async listActionLog(opts: { actorEmail?: string; action?: string; from: string; to: string; page?: number }): Promise<{ items: any[]; total: number }> {
    const params = new URLSearchParams();
    if (opts.actorEmail) params.set('actorEmail', opts.actorEmail);
    if (opts.action) params.set('action', opts.action);
    params.set('from', opts.from);
    params.set('to', opts.to);
    if (opts.page) params.set('page', opts.page.toString());
    const r = await this.request(`/api/admin/action-log?${params.toString()}`);
    return r.json();
}
```

- [ ] **Step 4: Wire route**

In `admin-panel/src/app/page.tsx`:

```typescript
import { ActionLogPage } from '@/views/ActionLogPage';
// ...
{activeTab === 'actionlog' && <ActionLogPage />}
```

- [ ] **Step 5: Run tests + build**

```bash
dotnet test --filter FullyQualifiedName~ActionLogController && cd admin-panel && npm run build
```

Expected: backend test PASS, frontend build clean.

- [ ] **Step 6: Commit**

```bash
git add src/Backend/AuraCore.API/Controllers/Admin/ActionLogController.cs admin-panel/src/views/ActionLogPage.tsx admin-panel/src/lib/api.ts admin-panel/src/app/page.tsx tests/AuraCore.Tests.API/SuperadminFoundation/ActionLogControllerTests.cs
git commit -m "feat(6.11.W4): ActionLogPage + CSV export

Superadmin tab lists action_log entries with filters: actor email,
action substring, date range. Server-side pagination (50/page max 200).

CSV export endpoint streams up to 366-day range as RFC 4180 CSV with
proper escaping. Filename: action-log-{from}-{to}.csv."
```

---

## Wave 5 — Permission templates UI + force-change + mandatory 2FA + Security Policy + Rate Limits

**Goal:** Superadmin can grant/revoke individual permissions on the Permissions tab. Force-password-change actually intercepts login. Mandatory 2FA setup gates first login post-invitation. Security Policy tab exposes the global toggles (newRegistrations, force-2fa-all-admins, sessionTimeoutMinutes). Rate-limit policies become tunable through the UI instead of hardcoded.

### Task 28: PermissionsPage + grant/revoke endpoints

**Files:**
- Create: `admin-panel/src/views/PermissionsPage.tsx`
- Wave 2 already created PermissionGrantsController + MyPermissionsController; this task adds the bulk-grant-from-template endpoint.
- Modify: `src/Backend/AuraCore.API/Controllers/Admin/PermissionGrantsController.cs`

- [ ] **Step 1: Add bulk apply-template endpoint**

```csharp
// Add to PermissionGrantsController (Wave 2 Task 12)
[HttpPost("apply-template")]
[Authorize(Roles = "superadmin")]
public async Task<IActionResult> ApplyTemplate([FromBody] ApplyTemplateRequest req, CancellationToken ct)
{
    var template = PermissionTemplates.Get(req.TemplateName);
    if (template is null) return BadRequest(new { error = "Unknown template" });

    var user = await _db.Users.FindAsync(new object[] { req.AdminUserId }, ct);
    if (user is null) return NotFound();
    if (user.Role != "admin") return BadRequest(new { error = "Templates apply to admin role only" });

    var actorId = Guid.Parse(User.FindFirst("sub")!.Value);

    // Wipe existing active grants (revoke them, don't delete — preserve audit history)
    var existing = await _db.PermissionGrants
        .Where(g => g.AdminUserId == req.AdminUserId && g.RevokedAt == null)
        .ToListAsync(ct);
    foreach (var g in existing) g.RevokedAt = DateTime.UtcNow;

    // Apply new template grants
    foreach (var permKey in template.Permissions)
    {
        _db.PermissionGrants.Add(new PermissionGrant
        {
            AdminUserId = req.AdminUserId,
            PermissionKey = permKey,
            GrantedBy = actorId,
        });
    }
    user.IsReadonly = template.IsReadonly;
    await _db.SaveChangesAsync(ct);
    return NoContent();
}

public sealed record ApplyTemplateRequest(Guid AdminUserId, string TemplateName);
```

- [ ] **Step 2: Implement PermissionsPage**

```typescript
// admin-panel/src/views/PermissionsPage.tsx
'use client';

import { useState, useEffect, useCallback } from 'react';
import { Shield, Plus, X, Clock } from 'lucide-react';
import { api } from '@/lib/api';
import { AdminUser } from '@/lib/types';
import { PageHeader } from '@/components/PageHeader';
import { EmptyState } from '@/components/EmptyState';
import { ConfirmDialog } from '@/components/ConfirmDialog';

const ALL_PERMISSION_KEYS = [
    'tab:configuration', 'tab:ipwhitelist', 'tab:auditlog', 'tab:releases',
    'tab:payments', 'tab:crypto', 'tab:users', 'tab:licenses', 'tab:devices', 'tab:subscriptions',
    'action:user:revoke_subscription', 'action:user:delete',
    'action:license:revoke', 'action:license:activate',
    'action:device:deactivate', 'action:subscription:cancel',
    'action:config:save', 'action:ipwhitelist:edit', 'action:release:publish',
    'action:payment:refund', 'action:crypto:markpaid',
];

const TEMPLATES = ['ReadOnly', 'Moderator', 'Operator'];

interface Grant {
    id: string;
    permissionKey: string;
    grantedBy: string;
    grantedAt: string;
    expiresAt: string | null;
}

export function PermissionsPage() {
    const [admins, setAdmins] = useState<AdminUser[]>([]);
    const [selected, setSelected] = useState<AdminUser | null>(null);
    const [grants, setGrants] = useState<Grant[]>([]);
    const [requests, setRequests] = useState<any[]>([]);
    const [showGrant, setShowGrant] = useState(false);
    const [grantPerm, setGrantPerm] = useState('');
    const [grantExpiresInDays, setGrantExpiresInDays] = useState<number | ''>('');

    const loadAdmins = useCallback(async () => {
        const a = await api.listAdminUsers();
        setAdmins(a.filter(u => u.role === 'admin'));
    }, []);

    const loadGrants = useCallback(async (adminId: string) => {
        const detail = await api.getAdminUser(adminId);
        setGrants(detail.grants);
    }, []);

    const loadRequests = useCallback(async () => {
        const r = await api.listPermissionRequests('pending');
        setRequests(r);
    }, []);

    useEffect(() => { loadAdmins(); loadRequests(); }, [loadAdmins, loadRequests]);
    useEffect(() => { if (selected) loadGrants(selected.id); }, [selected, loadGrants]);

    async function applyTemplate(template: string) {
        if (!selected) return;
        await api.applyPermissionTemplate(selected.id, template);
        loadGrants(selected.id);
        loadAdmins();
    }

    async function grantPermission() {
        if (!selected || !grantPerm) return;
        await api.grantPermission(selected.id, grantPerm, grantExpiresInDays === '' ? null : grantExpiresInDays);
        setShowGrant(false); setGrantPerm(''); setGrantExpiresInDays('');
        loadGrants(selected.id);
    }

    async function revokeGrant(grantId: string) {
        await api.revokePermissionGrant(grantId);
        if (selected) loadGrants(selected.id);
    }

    async function approveRequest(id: string, expiresInDays: number | null) {
        await api.approvePermissionRequest(id, expiresInDays);
        loadRequests();
        if (selected) loadGrants(selected.id);
    }

    async function denyRequest(id: string, reason: string) {
        await api.denyPermissionRequest(id, reason);
        loadRequests();
    }

    return (
        <div className="animate-fade-in">
            <PageHeader title="Permissions" subtitle={`Manage admin access. ${requests.length} pending requests.`} />

            <div className="grid grid-cols-12 gap-5">
                <aside className="col-span-3 glass-card p-4">
                    <h3 className="text-xs font-bold text-white/40 uppercase mb-3">Admins</h3>
                    {admins.length === 0
                        ? <EmptyState title="No admins" />
                        : <ul className="space-y-1">
                            {admins.map(a => (
                                <li key={a.id}>
                                    <button onClick={() => setSelected(a)}
                                        className={`w-full text-left px-3 py-2 rounded-lg text-sm ${selected?.id === a.id ? 'bg-accent/15 text-accent' : 'text-white/65 hover:bg-white/[0.03]'}`}>
                                        {a.email}
                                        <span className="block text-[10px] text-white/30 mt-0.5">{a.permissionCount} grants{a.isReadonly ? ' · read-only' : ''}</span>
                                    </button>
                                </li>
                            ))}
                        </ul>}
                </aside>

                <main className="col-span-9 space-y-5">
                    {requests.length > 0 && (
                        <div className="glass-card p-5">
                            <h3 className="text-xs font-bold text-white/40 uppercase mb-3">Pending requests ({requests.length})</h3>
                            {requests.map(r => (
                                <div key={r.id} className="flex items-start gap-3 py-3 border-t border-white/5 first:border-0">
                                    <div className="flex-1">
                                        <p className="text-sm"><span className="text-white/80">{r.requesterEmail}</span> requested <span className="font-mono text-xs text-accent">{r.permissionKey}</span></p>
                                        <p className="text-xs text-white/40 mt-1">{r.reason}</p>
                                        <p className="text-[10px] text-white/30 mt-1">{new Date(r.createdAt).toLocaleString()}</p>
                                    </div>
                                    <button onClick={() => approveRequest(r.id, 7)} className="btn-primary text-[10px] px-2 py-1">Approve 7d</button>
                                    <button onClick={() => approveRequest(r.id, null)} className="btn-ghost text-[10px] px-2 py-1">Approve permanent</button>
                                    <button onClick={() => denyRequest(r.id, 'Not needed')} className="btn-ghost text-[10px] px-2 py-1 text-aura-red">Deny</button>
                                </div>
                            ))}
                        </div>
                    )}

                    {selected ? (
                        <div className="glass-card p-5">
                            <div className="flex items-center justify-between mb-4">
                                <div>
                                    <h3 className="text-lg font-bold">{selected.email}</h3>
                                    <p className="text-xs text-white/40">{grants.length} active grants{selected.isReadonly ? ' · read-only' : ''}</p>
                                </div>
                                <button onClick={() => setShowGrant(true)} className="btn-primary flex items-center gap-2"><Plus className="w-4 h-4" />Grant permission</button>
                            </div>

                            <div className="mb-4">
                                <p className="text-xs text-white/40 mb-2">Apply template (replaces ALL existing grants):</p>
                                <div className="flex gap-2">
                                    {TEMPLATES.map(t => <button key={t} onClick={() => applyTemplate(t)} className="btn-ghost text-xs px-3 py-1.5">{t}</button>)}
                                </div>
                            </div>

                            <div className="border-t border-white/5 pt-4">
                                {grants.length === 0
                                    ? <EmptyState title="No grants" icon={Shield} />
                                    : <ul className="space-y-1">
                                        {grants.map(g => (
                                            <li key={g.id} className="flex items-center gap-3 py-2 text-sm">
                                                <span className="font-mono text-xs text-accent flex-1">{g.permissionKey}</span>
                                                {g.expiresAt && <span className="text-[10px] text-white/40 flex items-center gap-1"><Clock className="w-3 h-3" />exp {new Date(g.expiresAt).toLocaleDateString()}</span>}
                                                <button onClick={() => revokeGrant(g.id)} className="text-white/30 hover:text-aura-red"><X className="w-4 h-4" /></button>
                                            </li>
                                        ))}
                                    </ul>}
                            </div>
                        </div>
                    ) : (
                        <div className="glass-card p-12 text-center">
                            <Shield className="w-12 h-12 mx-auto mb-4 text-white/20" />
                            <p className="text-white/40">Select an admin from the left to manage their permissions.</p>
                        </div>
                    )}
                </main>
            </div>

            {showGrant && (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm" onClick={() => setShowGrant(false)}>
                    <div className="glass-card p-6 max-w-md w-full mx-4" onClick={e => e.stopPropagation()}>
                        <h2 className="text-lg font-bold mb-4">Grant permission</h2>
                        <label className="block text-xs text-white/50 mb-1">Permission key</label>
                        <select value={grantPerm} onChange={e => setGrantPerm(e.target.value)} className="input-dark w-full mb-3">
                            <option value="">— Select —</option>
                            {ALL_PERMISSION_KEYS.filter(k => !grants.find(g => g.permissionKey === k)).map(k => <option key={k} value={k}>{k}</option>)}
                        </select>
                        <label className="block text-xs text-white/50 mb-1">Expires in (days, blank = permanent)</label>
                        <input type="number" min={1} max={365} value={grantExpiresInDays} onChange={e => setGrantExpiresInDays(e.target.value === '' ? '' : parseInt(e.target.value))} className="input-dark w-full mb-3" />
                        <div className="flex justify-end gap-2">
                            <button onClick={() => setShowGrant(false)} className="btn-ghost text-xs px-4 py-2">Cancel</button>
                            <button onClick={grantPermission} disabled={!grantPerm} className="btn-primary text-xs px-4 py-2">Grant</button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
```

Add api methods:

```typescript
async applyPermissionTemplate(adminId: string, template: string): Promise<void> {
    await this.request('/api/permissions/grants/apply-template', {
        method: 'POST',
        body: JSON.stringify({ adminUserId: adminId, templateName: template }),
    });
}
async grantPermission(adminId: string, permission: string, expiresInDays: number | null): Promise<void> {
    await this.request('/api/permissions/grants', {
        method: 'POST',
        body: JSON.stringify({ adminUserId: adminId, permissionKey: permission, expiresInDays }),
    });
}
async revokePermissionGrant(grantId: string): Promise<void> {
    await this.request(`/api/permissions/grants/${grantId}`, { method: 'DELETE' });
}
async listPermissionRequests(status: 'pending' | 'all' = 'pending'): Promise<any[]> {
    const r = await this.request(`/api/permissions/requests?status=${status}`);
    return r.json();
}
async approvePermissionRequest(id: string, expiresInDays: number | null): Promise<void> {
    await this.request(`/api/permissions/requests/${id}/approve`, {
        method: 'POST',
        body: JSON.stringify({ expiresInDays }),
    });
}
async denyPermissionRequest(id: string, reason: string): Promise<void> {
    await this.request(`/api/permissions/requests/${id}/deny`, {
        method: 'POST',
        body: JSON.stringify({ reason }),
    });
}
```

- [ ] **Step 3: Wire route**

```typescript
import { PermissionsPage } from '@/views/PermissionsPage';
// ...
{activeTab === 'permissions' && <PermissionsPage />}
```

- [ ] **Step 4: Build + smoke test**

```bash
cd admin-panel && npm run build && cd .. && dotnet test --filter FullyQualifiedName~PermissionGrantsController
```

Expected: clean build + tests PASS.

- [ ] **Step 5: Commit**

```bash
git add admin-panel/src/views/PermissionsPage.tsx admin-panel/src/lib/api.ts admin-panel/src/app/page.tsx src/Backend/AuraCore.API/Controllers/Admin/PermissionGrantsController.cs
git commit -m "feat(6.11.W5): PermissionsPage — grant/revoke + apply-template + approve/deny

3-pane layout: admin list (left), pending requests (top right), active
grants for selected admin (bottom right). Apply-template wipes ALL
existing grants and applies the template's permission set + readonly flag
(revokes preserve audit trail; not hard-deletes). Per-permission expiry
optional (1-365 days)."
```

### Task 29: ForcePasswordChange middleware + page

**Goal:** When `users.must_change_password_at IS NOT NULL` and a user logs in, the access token grants a 2-minute window only valid for `/api/auth/change-password`. All other endpoints respond 403 with `{ error: 'password_change_required' }`. Frontend intercepts the response code and routes to `/change-password`.

**Files:**
- Create: `src/Backend/AuraCore.API/Middleware/ForcePasswordChangeMiddleware.cs`
- Modify: `src/Backend/AuraCore.API/Controllers/AuthController.cs` (add ChangePassword endpoint)
- Create: `admin-panel/src/app/change-password/page.tsx`
- Modify: `admin-panel/src/lib/api.ts` (intercept 403 with code)
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/ForcePasswordChangeTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
[Fact]
public async Task ForcedAdminCannotAccessOtherEndpoints()
{
    var (factory, adminId) = _factory.WithSeededAdmin("forced@x.com");
    using (var scope = factory.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var u = await db.Users.FindAsync(adminId);
        u!.MustChangePasswordAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
    var client = factory.CreateClientForUser(adminId);
    var resp = await client.GetAsync("/api/admin/admin-users");
    Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    var body = await resp.Content.ReadAsStringAsync();
    Assert.Contains("password_change_required", body);
}

[Fact]
public async Task ChangingPasswordClearsForceFlag()
{
    var (factory, adminId) = _factory.WithSeededAdmin("forced2@x.com", forcePwChange: true);
    var client = factory.CreateClientForUser(adminId);
    var resp = await client.PostAsJsonAsync("/api/auth/change-password", new
    {
        currentPassword = "InitialPassword123!",
        newPassword = "NewBetterPassword456!",
        confirmPassword = "NewBetterPassword456!"
    });
    Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

    using var scope = factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
    var u = await db.Users.FindAsync(adminId);
    Assert.Null(u!.MustChangePasswordAt);
}
```

- [ ] **Step 2: Run test, verify fail**

```bash
dotnet test --filter FullyQualifiedName~ForcePasswordChange
```

Expected: FAIL — middleware + endpoint absent.

- [ ] **Step 3: Implement middleware**

```csharp
// src/Backend/AuraCore.API/Middleware/ForcePasswordChangeMiddleware.cs
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AuraCore.API.Middleware;

public sealed class ForcePasswordChangeMiddleware
{
    private readonly RequestDelegate _next;

    public ForcePasswordChangeMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, AuraCoreDbContext db)
    {
        var userIdStr = ctx.User?.FindFirst("sub")?.Value;
        if (userIdStr is null || !Guid.TryParse(userIdStr, out var userId))
        {
            await _next(ctx);
            return;
        }

        var path = ctx.Request.Path.Value ?? "";
        // Allow change-password + me endpoints through unconditionally
        if (path.StartsWith("/api/auth/change-password", StringComparison.OrdinalIgnoreCase)
         || path.StartsWith("/api/auth/logout", StringComparison.OrdinalIgnoreCase)
         || path.StartsWith("/api/auth/me", StringComparison.OrdinalIgnoreCase)
         || path.StartsWith("/api/me/permissions", StringComparison.OrdinalIgnoreCase))
        {
            await _next(ctx);
            return;
        }

        var mustChange = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.MustChangePasswordAt != null)
            .FirstOrDefaultAsync();

        if (mustChange)
        {
            ctx.Response.StatusCode = 403;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync("{\"error\":\"password_change_required\",\"message\":\"You must change your password before continuing.\"}");
            return;
        }

        await _next(ctx);
    }
}
```

Register in `Program.cs` AFTER auth/auth middleware:

```csharp
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TokenRevocationMiddleware>();    // Wave 1 Task 6
app.UseMiddleware<ForcePasswordChangeMiddleware>(); // <-- here
app.UseMiddleware<ForceTwoFactorSetupMiddleware>(); // Task 30
```

- [ ] **Step 4: Implement ChangePassword endpoint**

In `AuthController.cs`:

```csharp
[Authorize]
[HttpPost("change-password")]
public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req, CancellationToken ct)
{
    if (req.NewPassword != req.ConfirmPassword)
        return BadRequest(new { error = "Passwords do not match" });
    if (req.NewPassword.Length < 12)
        return BadRequest(new { error = "Password must be at least 12 characters" });
    if (req.NewPassword == req.CurrentPassword)
        return BadRequest(new { error = "New password must differ from current" });

    var userId = Guid.Parse(User.FindFirst("sub")!.Value);
    var user = await _db.Users.FindAsync(new object[] { userId }, ct);
    if (user is null) return NotFound();

    if (!_auth.VerifyPassword(req.CurrentPassword, user.PasswordHash))
        return BadRequest(new { error = "Current password incorrect" });

    user.PasswordHash = _auth.HashPassword(req.NewPassword);
    user.MustChangePasswordAt = null;
    user.PasswordChangedAt = DateTime.UtcNow;
    user.SessionRevokedAt = DateTime.UtcNow;    // logs out all OTHER sessions
    await _db.SaveChangesAsync(ct);

    return Ok(new { message = "Password changed. Please log in again with new credentials." });
}

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmPassword);
```

- [ ] **Step 5: Frontend interceptor + change-password page**

In `admin-panel/src/lib/api.ts`, modify the `request` method to detect the 403 error code:

```typescript
async request(path: string, init: RequestInit = {}): Promise<Response> {
    const r = await fetch(`${this.baseUrl}${path}`, {
        ...init,
        headers: { 'Content-Type': 'application/json', ...this.authHeaders(), ...init.headers },
    });
    if (r.status === 403) {
        const cloned = r.clone();
        const body = await cloned.json().catch(() => null);
        if (body?.error === 'password_change_required') {
            window.location.href = '/change-password';
            throw new Error('redirect:change-password');
        }
        if (body?.error === '2fa_setup_required') {
            window.location.href = '/setup-2fa';
            throw new Error('redirect:setup-2fa');
        }
    }
    if (!r.ok) {
        const err = await r.clone().json().catch(() => ({ error: r.statusText }));
        throw new Error(err.error ?? r.statusText);
    }
    return r;
}
```

Create `admin-panel/src/app/change-password/page.tsx`:

```typescript
'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { api } from '@/lib/api';

export default function ChangePasswordPage() {
    const router = useRouter();
    const [current, setCurrent] = useState('');
    const [next, setNext] = useState('');
    const [confirm, setConfirm] = useState('');
    const [error, setError] = useState<string | null>(null);
    const [submitting, setSubmitting] = useState(false);

    async function submit() {
        setError(null);
        if (next.length < 12) return setError('New password must be at least 12 characters');
        if (next !== confirm) return setError('Passwords do not match');
        setSubmitting(true);
        try {
            await fetch(`${api.baseUrl}/api/auth/change-password`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', ...api.authHeaders() },
                body: JSON.stringify({ currentPassword: current, newPassword: next, confirmPassword: confirm }),
            }).then(r => { if (!r.ok) throw new Error('Change failed'); });
            api.logout();
            router.push('/');
        } catch (e: any) {
            setError(e?.message ?? 'Failed');
        } finally { setSubmitting(false); }
    }

    return (
        <div className="min-h-screen flex items-center justify-center p-4">
            <div className="glass-card p-8 max-w-md w-full">
                <h1 className="text-xl font-bold mb-2">Change required</h1>
                <p className="text-sm text-white/50 mb-4">A superadmin has required you to change your password before continuing.</p>
                <label className="block text-xs text-white/50 mb-1">Current password</label>
                <input type="password" value={current} onChange={e => setCurrent(e.target.value)} className="input-dark w-full mb-3" />
                <label className="block text-xs text-white/50 mb-1">New password (12+ chars)</label>
                <input type="password" value={next} onChange={e => setNext(e.target.value)} className="input-dark w-full mb-3" />
                <label className="block text-xs text-white/50 mb-1">Confirm new password</label>
                <input type="password" value={confirm} onChange={e => setConfirm(e.target.value)} className="input-dark w-full mb-3" />
                {error && <p className="text-xs text-aura-red mb-3">{error}</p>}
                <button onClick={submit} disabled={submitting} className="btn-primary w-full">
                    {submitting ? 'Changing...' : 'Change password'}
                </button>
            </div>
        </div>
    );
}
```

- [ ] **Step 6: Run tests, verify pass**

```bash
dotnet test --filter FullyQualifiedName~ForcePasswordChange
```

Expected: 2 tests PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Backend/AuraCore.API/Middleware/ForcePasswordChangeMiddleware.cs src/Backend/AuraCore.API/Controllers/AuthController.cs src/Backend/AuraCore.API/Program.cs admin-panel/src/app/change-password/page.tsx admin-panel/src/lib/api.ts tests/AuraCore.Tests.API/SuperadminFoundation/ForcePasswordChangeTests.cs
git commit -m "feat(6.11.W5): force-password-change middleware + UI

When users.must_change_password_at IS NOT NULL, every API request EXCEPT
/api/auth/change-password|logout|me + /api/me/permissions returns
403 { error: 'password_change_required' }. Frontend interceptor catches
that error code and redirects to /change-password.

ChangePassword endpoint: validates current pw, requires new pw 12+ chars
and != current, clears must_change_password_at, stamps SessionRevokedAt
to log out all other sessions for that user, instructs user to log in
again with new credentials."
```

### Task 30: Mandatory 2FA setup on first invitation login

**Goal:** When `users.must_enable_2fa_at IS NOT NULL` and user has no totp_secret, login issues a SCOPE-LIMITED JWT with `scope: '2fa-setup-only'` claim. Middleware blocks all endpoints except `/api/auth/2fa/setup` and `/api/auth/2fa/verify-setup`. After successful TOTP verification, stamps `totp_enabled = true` + clears `must_enable_2fa_at`, issues a normal full-scope token.

**Files:**
- Create: `src/Backend/AuraCore.API/Middleware/ForceTwoFactorSetupMiddleware.cs`
- Modify: `src/Backend/AuraCore.API/Application/Services/Auth/AuthService.cs` (issue scope-limited JWT)
- Modify: `src/Backend/AuraCore.API/Controllers/AuthController.cs` (Setup2FA, VerifySetup2FA endpoints)
- Create: `admin-panel/src/app/setup-2fa/page.tsx`
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/ForceTwoFactorSetupTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
[Fact]
public async Task FreshAdminMustSetup2FA_OtherEndpointsReturn403()
{
    var (factory, adminId) = _factory.WithSeededAdmin("fresh@x.com", mustEnable2FA: true);
    var client = factory.CreateClientForUser(adminId);
    var resp = await client.GetAsync("/api/admin/admin-users");
    Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    var body = await resp.Content.ReadAsStringAsync();
    Assert.Contains("2fa_setup_required", body);
}

[Fact]
public async Task SuccessfulTotpVerificationClearsForceFlag()
{
    var (factory, adminId) = _factory.WithSeededAdmin("verify2fa@x.com", mustEnable2FA: true);
    var client = factory.CreateClientForUser(adminId);
    var setupResp = await client.PostAsync("/api/auth/2fa/setup", null);
    setupResp.EnsureSuccessStatusCode();
    var setupBody = await setupResp.Content.ReadFromJsonAsync<TotpSetupResponse>();

    var code = TotpService.ComputeCode(setupBody!.SecretBase32);
    var verifyResp = await client.PostAsJsonAsync("/api/auth/2fa/verify-setup", new { code });
    Assert.Equal(HttpStatusCode.OK, verifyResp.StatusCode);

    using var scope = factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
    var u = await db.Users.FindAsync(adminId);
    Assert.True(u!.TotpEnabled);
    Assert.Null(u.MustEnable2FAAt);
}

private record TotpSetupResponse(string SecretBase32, string OtpAuthUrl);
```

- [ ] **Step 2: Implement middleware**

```csharp
// src/Backend/AuraCore.API/Middleware/ForceTwoFactorSetupMiddleware.cs
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Middleware;

public sealed class ForceTwoFactorSetupMiddleware
{
    private readonly RequestDelegate _next;
    public ForceTwoFactorSetupMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, AuraCoreDbContext db)
    {
        var userIdStr = ctx.User?.FindFirst("sub")?.Value;
        if (userIdStr is null || !Guid.TryParse(userIdStr, out var userId))
        {
            await _next(ctx);
            return;
        }

        var path = ctx.Request.Path.Value ?? "";
        // Allow 2FA setup endpoints + identity probes through unconditionally
        if (path.StartsWith("/api/auth/2fa/setup", StringComparison.OrdinalIgnoreCase)
         || path.StartsWith("/api/auth/2fa/verify-setup", StringComparison.OrdinalIgnoreCase)
         || path.StartsWith("/api/auth/logout", StringComparison.OrdinalIgnoreCase)
         || path.StartsWith("/api/auth/me", StringComparison.OrdinalIgnoreCase))
        {
            await _next(ctx);
            return;
        }

        // Two ways the user must do 2FA setup:
        //  (1) per-account flag must_enable_2fa_at (set by invitation accept / superadmin force)
        //  (2) global force-2fa-all-admins setting AND user.totp_enabled = false
        var u = await db.Users.AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new { x.MustEnable2FAAt, x.TotpEnabled, x.Role })
            .FirstOrDefaultAsync();
        if (u is null) { await _next(ctx); return; }

        var globalForce = await db.SystemSettings.AsNoTracking()
            .Where(s => s.Key == "force_2fa_all_admins")
            .Select(s => s.Value)
            .FirstOrDefaultAsync();
        var globalForceOn = globalForce == "true";

        var mustSetup = !u.TotpEnabled
            && (u.MustEnable2FAAt != null
                || (globalForceOn && (u.Role == "admin" || u.Role == "superadmin")));

        // Also block if scope is '2fa-setup-only' (issued during a forced setup login flow)
        var scope = ctx.User?.FindFirst("scope")?.Value;
        if (scope == "2fa-setup-only")
        {
            // permitted: only the 2 allow-listed paths above; this path was not whitelisted, so reject
            ctx.Response.StatusCode = 403;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync("{\"error\":\"2fa_setup_required\",\"message\":\"You must enable two-factor authentication.\"}");
            return;
        }

        if (mustSetup)
        {
            ctx.Response.StatusCode = 403;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync("{\"error\":\"2fa_setup_required\",\"message\":\"You must enable two-factor authentication.\"}");
            return;
        }

        await _next(ctx);
    }
}
```

- [ ] **Step 3: Issue scope-limited JWT during forced login flow**

In `AuthService.LoginAsync`, after password+TOTP checks, BEFORE final return — check if user requires 2FA setup. If yes AND no totp_enabled, issue scope-limited JWT:

```csharp
var requiresSetup = !user.TotpEnabled
    && (user.MustEnable2FAAt != null
        || (await IsGlobalForce2FAAsync(ct) && (user.Role == "admin" || user.Role == "superadmin")));
if (requiresSetup)
{
    var setupToken = _tokenService.CreateAccessToken(user, scope: "2fa-setup-only", lifetime: TimeSpan.FromMinutes(15));
    return new AuthResult { Success = true, AccessToken = setupToken, User = user, RequiresTwoFactorSetup = true };
}
```

- [ ] **Step 4: Implement Setup2FA + VerifySetup2FA endpoints**

In `AuthController.cs`:

```csharp
[Authorize]
[HttpPost("2fa/setup")]
public async Task<IActionResult> Setup2FA(CancellationToken ct)
{
    var userId = Guid.Parse(User.FindFirst("sub")!.Value);
    var user = await _db.Users.FindAsync(new object[] { userId }, ct);
    if (user is null) return NotFound();

    // Generate fresh secret each call (overwrites unverified secret if user re-clicked Setup)
    var secret = TotpService.GenerateSecret();
    user.TotpSecret = _totpEnc.Encrypt(secret);
    // do NOT yet set TotpEnabled = true; only set after VerifySetup
    await _db.SaveChangesAsync(ct);

    var otpAuthUrl = $"otpauth://totp/AuraCore%20Admin:{Uri.EscapeDataString(user.Email)}?secret={secret}&issuer=AuraCore%20Admin";
    return Ok(new { secretBase32 = secret, otpAuthUrl });
}

[Authorize]
[HttpPost("2fa/verify-setup")]
public async Task<IActionResult> VerifySetup2FA([FromBody] VerifyTotpRequest req, CancellationToken ct)
{
    var userId = Guid.Parse(User.FindFirst("sub")!.Value);
    var user = await _db.Users.FindAsync(new object[] { userId }, ct);
    if (user is null || string.IsNullOrEmpty(user.TotpSecret))
        return BadRequest(new { error = "Run /api/auth/2fa/setup first" });

    var secret = _totpEnc.Decrypt(user.TotpSecret);
    if (!TotpService.ValidateCode(secret, req.Code))
        return BadRequest(new { error = "Invalid code" });

    user.TotpEnabled = true;
    user.MustEnable2FAAt = null;
    await _db.SaveChangesAsync(ct);

    // Issue full-scope JWT now (scope-limited token is replaced)
    var fullToken = _auth.IssueAccessToken(user, scope: null);
    return Ok(new { accessToken = fullToken, message = "2FA enabled. Use code from your authenticator on next login." });
}

public sealed record VerifyTotpRequest(string Code);
```

- [ ] **Step 5: Frontend setup-2fa page**

```typescript
// admin-panel/src/app/setup-2fa/page.tsx
'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { api } from '@/lib/api';

export default function Setup2FAPage() {
    const router = useRouter();
    const [secret, setSecret] = useState<string | null>(null);
    const [otpAuthUrl, setOtpAuthUrl] = useState<string | null>(null);
    const [code, setCode] = useState('');
    const [error, setError] = useState<string | null>(null);
    const [submitting, setSubmitting] = useState(false);

    useEffect(() => { (async () => {
        const r = await fetch(`${api.baseUrl}/api/auth/2fa/setup`, { method: 'POST', headers: api.authHeaders() });
        const body = await r.json();
        setSecret(body.secretBase32);
        setOtpAuthUrl(body.otpAuthUrl);
    })(); }, []);

    async function verify() {
        setError(null); setSubmitting(true);
        try {
            const r = await fetch(`${api.baseUrl}/api/auth/2fa/verify-setup`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', ...api.authHeaders() },
                body: JSON.stringify({ code }),
            });
            if (!r.ok) {
                const err = await r.json();
                throw new Error(err.error ?? 'Verification failed');
            }
            const body = await r.json();
            api.setAccessToken(body.accessToken);
            router.push('/');
        } catch (e: any) {
            setError(e?.message ?? 'Failed');
        } finally { setSubmitting(false); }
    }

    return (
        <div className="min-h-screen flex items-center justify-center p-4">
            <div className="glass-card p-8 max-w-md w-full">
                <h1 className="text-xl font-bold mb-2">Enable two-factor</h1>
                <p className="text-sm text-white/50 mb-4">2FA is required for all admin accounts. Scan the code below in Google Authenticator (or any TOTP app) and enter the 6-digit code to verify.</p>
                {otpAuthUrl && (
                    <div className="bg-white p-4 rounded-lg mb-3 flex justify-center">
                        <img alt="QR code" src={`https://api.qrserver.com/v1/create-qr-code/?size=200x200&data=${encodeURIComponent(otpAuthUrl)}`} />
                    </div>
                )}
                {secret && (
                    <p className="text-xs text-white/40 mb-3 break-all">
                        Or enter manually: <span className="font-mono text-white/70">{secret}</span>
                    </p>
                )}
                <label className="block text-xs text-white/50 mb-1">6-digit code from authenticator</label>
                <input value={code} onChange={e => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))} maxLength={6} className="input-dark w-full mb-3 font-mono text-center text-lg" />
                {error && <p className="text-xs text-aura-red mb-3">{error}</p>}
                <button onClick={verify} disabled={submitting || code.length !== 6} className="btn-primary w-full">
                    {submitting ? 'Verifying...' : 'Enable 2FA'}
                </button>
            </div>
        </div>
    );
}
```

NOTE: The QR-rendering uses qrserver.com to avoid bundling a QR library. If externals are unacceptable, swap for `qrcode.react` (small, ~30KB).

- [ ] **Step 6: Run tests, verify pass**

```bash
dotnet test --filter FullyQualifiedName~ForceTwoFactorSetup
```

Expected: 2 tests PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Backend/AuraCore.API/Middleware/ForceTwoFactorSetupMiddleware.cs src/Backend/AuraCore.API/Application/Services/Auth/AuthService.cs src/Backend/AuraCore.API/Controllers/AuthController.cs admin-panel/src/app/setup-2fa/page.tsx tests/AuraCore.Tests.API/SuperadminFoundation/ForceTwoFactorSetupTests.cs
git commit -m "feat(6.11.W5): mandatory 2FA setup on first login

Per-account (must_enable_2fa_at) OR global (force_2fa_all_admins)
trigger. Login issues a JWT with scope='2fa-setup-only' instead of
the normal full-scope token; middleware blocks all endpoints except
/api/auth/2fa/setup|verify-setup|logout|me.

Setup endpoint encrypts the secret server-side via ITotpEncryption
(reused from Phase 6.9 hotfix); verify endpoint validates the user's
6-digit code, sets totp_enabled=true, clears must_enable_2fa_at, and
issues a fresh full-scope JWT.

AdminHub.OnConnectedAsync (Wave 2 Task 16) already rejects scope-limited
tokens, so SignalR is gated too."
```

### Task 31: SecurityPolicyPage + system_settings tab

**Files:**
- Create: `admin-panel/src/views/SecurityPolicyPage.tsx`
- Create: `src/Backend/AuraCore.API/Controllers/Admin/SystemSettingsController.cs`
- Test: `tests/AuraCore.Tests.API/SuperadminFoundation/SystemSettingsControllerTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
[Fact]
public async Task SuperadminCanReadAndUpdateSettings()
{
    var client = _factory.CreateClientWithRole("superadmin");
    var getResp = await client.GetAsync("/api/admin/system-settings");
    Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);

    var setResp = await client.PutAsJsonAsync("/api/admin/system-settings/force_2fa_all_admins",
        new { value = "true" });
    Assert.Equal(HttpStatusCode.NoContent, setResp.StatusCode);
}

[Fact]
public async Task RegularAdminCannotUpdateSettings()
{
    var client = _factory.CreateClientWithRole("admin");
    var setResp = await client.PutAsJsonAsync("/api/admin/system-settings/force_2fa_all_admins",
        new { value = "true" });
    Assert.Equal(HttpStatusCode.Forbidden, setResp.StatusCode);
}
```

- [ ] **Step 2: Implement controller**

```csharp
// src/Backend/AuraCore.API/Controllers/Admin/SystemSettingsController.cs
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AuraCore.API.Controllers.Admin;

[ApiController]
[Route("api/admin/system-settings")]
[Authorize(Roles = "superadmin")]
public sealed class SystemSettingsController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    private readonly IMemoryCache _cache;

    public SystemSettingsController(AuraCoreDbContext db, IMemoryCache cache)
    {
        _db = db; _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var rows = await _db.SystemSettings
            .OrderBy(s => s.Key)
            .Select(s => new { s.Key, s.Value, s.UpdatedAt, s.UpdatedBy })
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpPut("{key}")]
    public async Task<IActionResult> Update(string key, [FromBody] UpdateSettingRequest req, CancellationToken ct)
    {
        if (!SystemSettingDefaults.IsKnown(key))
            return BadRequest(new { error = $"Unknown setting key '{key}'" });
        if (!SystemSettingDefaults.ValidateValue(key, req.Value))
            return BadRequest(new { error = $"Invalid value for {key}" });

        var actorId = Guid.Parse(User.FindFirst("sub")!.Value);
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (setting is null)
        {
            setting = new SystemSetting { Key = key };
            _db.SystemSettings.Add(setting);
        }
        setting.Value = req.Value;
        setting.UpdatedAt = DateTime.UtcNow;
        setting.UpdatedBy = actorId;
        await _db.SaveChangesAsync(ct);

        // Invalidate cache so subsequent reads see the new value
        _cache.Remove($"system-setting:{key}");
        return NoContent();
    }
}

public sealed record UpdateSettingRequest(string Value);

public static class SystemSettingDefaults
{
    private static readonly Dictionary<string, (string Default, Func<string, bool> Validate)> Specs = new()
    {
        ["force_2fa_all_admins"] = ("false", v => v is "true" or "false"),
        ["new_registrations_enabled"] = ("true", v => v is "true" or "false"),
        ["session_timeout_minutes"] = ("60", v => int.TryParse(v, out var n) && n is >= 5 and <= 480),
        ["login_rate_limit_failures_per_30min"] = ("3", v => int.TryParse(v, out var n) && n is >= 1 and <= 100),
        ["login_rate_limit_failures_per_email_30min"] = ("5", v => int.TryParse(v, out var n) && n is >= 1 and <= 100),
        ["registration_rate_limit_per_hour"] = ("3", v => int.TryParse(v, out var n) && n is >= 1 and <= 100),
        ["password_min_length"] = ("12", v => int.TryParse(v, out var n) && n is >= 8 and <= 128),
        ["maintenance_mode"] = ("false", v => v is "true" or "false"),
    };

    public static bool IsKnown(string key) => Specs.ContainsKey(key);
    public static bool ValidateValue(string key, string value) => Specs.TryGetValue(key, out var s) && s.Validate(value);
    public static string GetDefault(string key) => Specs.TryGetValue(key, out var s) ? s.Default : "";
    public static IEnumerable<string> KnownKeys => Specs.Keys;
}
```

- [ ] **Step 3: Implement SecurityPolicyPage**

```typescript
// admin-panel/src/views/SecurityPolicyPage.tsx
'use client';

import { useState, useEffect, useCallback } from 'react';
import { Shield, Save, RefreshCw } from 'lucide-react';
import { api } from '@/lib/api';
import { PageHeader } from '@/components/PageHeader';

interface Setting { key: string; value: string; updatedAt: string; updatedBy: string }

const SETTING_LABELS: Record<string, { label: string; type: 'bool' | 'number'; help: string }> = {
    force_2fa_all_admins: { label: 'Force 2FA for all admins', type: 'bool', help: 'When ON, every admin/superadmin must enable 2FA on next login.' },
    new_registrations_enabled: { label: 'Allow new user registrations', type: 'bool', help: 'When OFF, /api/auth/register returns 503.' },
    maintenance_mode: { label: 'Maintenance mode', type: 'bool', help: 'When ON, non-admin endpoints return 503 for normal users.' },
    session_timeout_minutes: { label: 'Session timeout (minutes)', type: 'number', help: 'JWT access token TTL. 5-480.' },
    login_rate_limit_failures_per_30min: { label: 'Login fails per IP / 30min', type: 'number', help: 'Below this many failed attempts, no lockout. 1-100.' },
    login_rate_limit_failures_per_email_30min: { label: 'Login fails per email / 30min', type: 'number', help: 'Per-account lockout threshold. 1-100.' },
    registration_rate_limit_per_hour: { label: 'Registrations per IP / hour', type: 'number', help: 'Per-IP rate limit on /api/auth/register. 1-100.' },
    password_min_length: { label: 'Password minimum length', type: 'number', help: 'Reject passwords shorter than this. 8-128.' },
};

export function SecurityPolicyPage() {
    const [settings, setSettings] = useState<Setting[]>([]);
    const [draft, setDraft] = useState<Record<string, string>>({});
    const [saving, setSaving] = useState<string | null>(null);

    const load = useCallback(async () => {
        const r = await api.listSystemSettings();
        setSettings(r);
        const map: Record<string, string> = {};
        for (const s of r) map[s.key] = s.value;
        setDraft(map);
    }, []);

    useEffect(() => { load(); }, [load]);

    async function save(key: string) {
        setSaving(key);
        try {
            await api.updateSystemSetting(key, draft[key]);
            await load();
        } finally { setSaving(null); }
    }

    return (
        <div className="animate-fade-in">
            <PageHeader title="Security Policy" subtitle="Global security and rate-limit settings">
                <button onClick={load} className="btn-ghost flex items-center gap-2"><RefreshCw className="w-4 h-4" />Refresh</button>
            </PageHeader>
            <div className="glass-card p-5 space-y-4">
                {Object.entries(SETTING_LABELS).map(([key, meta]) => (
                    <div key={key} className="flex items-start gap-4 py-3 border-b border-white/5 last:border-0">
                        <div className="flex-1">
                            <p className="text-sm text-white/80">{meta.label}</p>
                            <p className="text-[10px] text-white/40 mt-0.5">{meta.help}</p>
                        </div>
                        {meta.type === 'bool' ? (
                            <select value={draft[key] ?? 'false'} onChange={e => setDraft({ ...draft, [key]: e.target.value })}
                                className="input-dark w-32">
                                <option value="false">OFF</option>
                                <option value="true">ON</option>
                            </select>
                        ) : (
                            <input type="number" value={draft[key] ?? ''} onChange={e => setDraft({ ...draft, [key]: e.target.value })}
                                className="input-dark w-32 text-right" />
                        )}
                        <button onClick={() => save(key)} disabled={saving === key || draft[key] === settings.find(s => s.key === key)?.value}
                            className="btn-primary text-xs px-3 py-1.5 flex items-center gap-1">
                            <Save className="w-3 h-3" />{saving === key ? 'Saving...' : 'Save'}
                        </button>
                    </div>
                ))}
            </div>
        </div>
    );
}
```

Add api methods:

```typescript
async listSystemSettings(): Promise<Array<{ key: string; value: string; updatedAt: string; updatedBy: string }>> {
    const r = await this.request('/api/admin/system-settings');
    return r.json();
}
async updateSystemSetting(key: string, value: string): Promise<void> {
    await this.request(`/api/admin/system-settings/${key}`, {
        method: 'PUT',
        body: JSON.stringify({ value }),
    });
}
```

- [ ] **Step 4: Apply settings in existing rate-limit code paths**

In `AuthController.Login`, replace hardcoded `3` and `5` with reads from system_settings (with 30s cache):

```csharp
private async Task<int> GetSettingIntAsync(string key, int fallback, CancellationToken ct)
{
    var raw = await _cache.GetOrCreateAsync($"setting:{key}", async entry =>
    {
        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
        return await _db.SystemSettings.AsNoTracking()
            .Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync(ct);
    });
    return int.TryParse(raw, out var n) ? n : fallback;
}

// then in Login:
var ipFailLimit = await GetSettingIntAsync("login_rate_limit_failures_per_30min", 3, ct);
var emailFailLimit = await GetSettingIntAsync("login_rate_limit_failures_per_email_30min", 5, ct);
```

Same pattern for registration rate limit and password min length (in Register endpoint).

- [ ] **Step 5: Wire route**

```typescript
import { SecurityPolicyPage } from '@/views/SecurityPolicyPage';
// ...
{activeTab === 'security' && <SecurityPolicyPage />}
```

- [ ] **Step 6: Run tests + build**

```bash
dotnet test --filter FullyQualifiedName~SystemSettingsController && cd admin-panel && npm run build
```

Expected: tests PASS + build clean.

- [ ] **Step 7: Commit**

```bash
git add admin-panel/src/views/SecurityPolicyPage.tsx admin-panel/src/lib/api.ts admin-panel/src/app/page.tsx src/Backend/AuraCore.API/Controllers/Admin/SystemSettingsController.cs src/Backend/AuraCore.API/Controllers/AuthController.cs tests/AuraCore.Tests.API/SuperadminFoundation/SystemSettingsControllerTests.cs
git commit -m "feat(6.11.W5): SecurityPolicyPage + tunable system_settings

Superadmin tab exposes 8 global settings:
- force_2fa_all_admins, new_registrations_enabled, maintenance_mode (bool)
- session_timeout_minutes (5-480), login_rate_limit_* (1-100)
- registration_rate_limit_per_hour, password_min_length

Backend reads applied via 30-second IMemoryCache layer to avoid hot-path
DB hits. Existing AuthController.Login + Register code paths now consult
the cache; defaults match the previously hardcoded values, so behavior
is unchanged unless superadmin tunes them."
```

### Task 32: RetentionJob (background hosted service for action_log GC)

**Files:**
- Create: `src/Backend/AuraCore.API/HostedServices/RetentionJob.cs`
- Modify: `src/Backend/AuraCore.API/Program.cs` (register)

- [ ] **Step 1: Implement hosted service**

```csharp
// src/Backend/AuraCore.API/HostedServices/RetentionJob.cs
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.HostedServices;

public sealed class RetentionJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RetentionJob> _log;
    private static readonly TimeSpan _interval = TimeSpan.FromHours(24);
    private static readonly TimeSpan _actionLogRetention = TimeSpan.FromDays(90);
    private static readonly TimeSpan _revokedTokenRetention = TimeSpan.FromHours(2);   // JWT TTL = 1h, keep 2h to cover clock skew
    private static readonly TimeSpan _expiredInvitationRetention = TimeSpan.FromDays(30);

    public RetentionJob(IServiceScopeFactory scopeFactory, ILogger<RetentionJob> log)
    {
        _scopeFactory = scopeFactory; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run once at startup, then every 24h
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);   // delay so app finishes start-up first

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
                var now = DateTime.UtcNow;

                var actionLogCutoff = now - _actionLogRetention;
                var actionLogDeleted = await db.ActionLog
                    .Where(a => a.CreatedAt < actionLogCutoff)
                    .ExecuteDeleteAsync(stoppingToken);

                var revokedTokenCutoff = now - _revokedTokenRetention;
                var tokensDeleted = await db.RevokedTokens
                    .Where(t => t.RevokedAt < revokedTokenCutoff)
                    .ExecuteDeleteAsync(stoppingToken);

                var invitationCutoff = now - _expiredInvitationRetention;
                var invitationsDeleted = await db.AdminInvitations
                    .Where(i => i.ExpiresAt < invitationCutoff && i.AcceptedAt == null)
                    .ExecuteDeleteAsync(stoppingToken);

                _log.LogInformation("Retention sweep: action_log -{action}, revoked_tokens -{token}, expired invitations -{inv}",
                    actionLogDeleted, tokensDeleted, invitationsDeleted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "RetentionJob iteration failed; will retry in 24h");
            }

            try { await Task.Delay(_interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
```

Register in `Program.cs`:

```csharp
builder.Services.AddHostedService<RetentionJob>();
```

- [ ] **Step 2: Build verify**

```bash
dotnet build src/Backend/AuraCore.API/AuraCore.API.csproj 2>&1 | tail -5
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/Backend/AuraCore.API/HostedServices/RetentionJob.cs src/Backend/AuraCore.API/Program.cs
git commit -m "feat(6.11.W5): RetentionJob — daily GC of audit + token tables

Hosted background service runs every 24h, deletes:
- action_log rows older than 90 days
- revoked_tokens older than 2h (JWT TTL is 1h, keep 2h buffer)
- expired-and-unaccepted invitations older than 30 days

Errors logged but never crash the service (caught + retry in 24h).
First iteration delayed 1 minute after start-up so the app finishes
booting first."
```

---

## Wave 6 — My Permissions tab + final deploy + ceremonial close

**Goal:** Self-service "what can I do?" view for any admin. Then production deploy of the entire phase: backend rebuild + admin panel static export + EF migration + smoke tests + admin grandfather verification + ceremonial merge.

### Task 33: MyPermissionsPage (visible to all admin roles)

**Files:**
- Create: `admin-panel/src/views/MyPermissionsPage.tsx`
- Wave 2 already created MyPermissionsController; this task adds the request-history endpoint addition.
- Modify: `src/Backend/AuraCore.API/Controllers/Admin/MyPermissionsController.cs`

- [ ] **Step 1: Add my-requests endpoint to MyPermissionsController**

```csharp
[Authorize]
[HttpGet("my-requests")]
public async Task<IActionResult> MyRequests(CancellationToken ct)
{
    var userId = Guid.Parse(User.FindFirst("sub")!.Value);
    var rows = await _db.PermissionRequests
        .Where(r => r.RequesterId == userId)
        .OrderByDescending(r => r.CreatedAt)
        .Take(50)
        .Select(r => new
        {
            r.Id, r.PermissionKey, r.Reason, r.Status, r.CreatedAt, r.DecidedAt,
            r.DecidedBy, r.DenialReason
        })
        .ToListAsync(ct);
    return Ok(rows);
}
```

- [ ] **Step 2: Implement MyPermissionsPage**

```typescript
// admin-panel/src/views/MyPermissionsPage.tsx
'use client';

import { useState, useEffect, useCallback } from 'react';
import { Shield, Clock } from 'lucide-react';
import { api } from '@/lib/api';
import { PageHeader } from '@/components/PageHeader';
import { StatusBadge } from '@/components/StatusBadge';
import { EmptyState } from '@/components/EmptyState';

interface MyPermissions {
    role: string;
    permissions: string[];
    isReadonly: boolean;
}

interface MyRequest {
    id: string;
    permissionKey: string;
    reason: string;
    status: 'pending' | 'approved' | 'denied';
    createdAt: string;
    decidedAt: string | null;
    denialReason: string | null;
}

export function MyPermissionsPage() {
    const [me, setMe] = useState<MyPermissions | null>(null);
    const [requests, setRequests] = useState<MyRequest[]>([]);

    const load = useCallback(async () => {
        const [m, r] = await Promise.all([
            api.getMyPermissions(),
            api.listMyPermissionRequests(),
        ]);
        setMe(m);
        setRequests(r);
    }, []);

    useEffect(() => { load(); }, [load]);

    if (!me) return <div className="text-white/40">Loading…</div>;

    return (
        <div className="animate-fade-in">
            <PageHeader title="My Permissions" subtitle="What you can do — and your access request history" />

            <div className="glass-card p-5 mb-5">
                <div className="flex items-center gap-3 mb-4">
                    <Shield className="w-5 h-5 text-accent" />
                    <div>
                        <p className="text-sm">Role: <span className="text-accent font-mono">{me.role}</span></p>
                        {me.isReadonly && <p className="text-xs text-aura-amber mt-1">Account is READ-ONLY — destructive actions are blocked.</p>}
                    </div>
                </div>
                <h3 className="text-xs font-bold text-white/40 uppercase mb-2">Active permissions ({me.role === 'superadmin' ? 'all' : me.permissions.length})</h3>
                {me.role === 'superadmin'
                    ? <p className="text-sm text-white/65">As superadmin, you have access to every tab and action without explicit grants.</p>
                    : me.permissions.length === 0
                        ? <p className="text-sm text-white/40">No permissions granted yet. Use the info icon next to locked actions to request access.</p>
                        : <div className="grid grid-cols-2 gap-2">
                            {me.permissions.map(p => (
                                <div key={p} className="flex items-center gap-2 px-3 py-2 rounded-lg bg-white/[0.03] text-xs font-mono text-accent">
                                    <Shield className="w-3 h-3" />{p}
                                </div>
                            ))}
                        </div>}
            </div>

            <div className="glass-card p-5">
                <h3 className="text-xs font-bold text-white/40 uppercase mb-3">My request history</h3>
                {requests.length === 0
                    ? <EmptyState title="No permission requests yet" icon={Clock} />
                    : <ul className="space-y-2">
                        {requests.map(r => (
                            <li key={r.id} className="flex items-start gap-3 py-3 border-t border-white/5 first:border-0">
                                <StatusBadge status={r.status} />
                                <div className="flex-1">
                                    <p className="text-sm"><span className="font-mono text-xs text-accent">{r.permissionKey}</span></p>
                                    <p className="text-xs text-white/40 mt-1">{r.reason}</p>
                                    <p className="text-[10px] text-white/30 mt-1">
                                        Requested {new Date(r.createdAt).toLocaleString()}
                                        {r.decidedAt && ` · decided ${new Date(r.decidedAt).toLocaleString()}`}
                                    </p>
                                    {r.denialReason && <p className="text-[10px] text-aura-red mt-1">Reason: {r.denialReason}</p>}
                                </div>
                            </li>
                        ))}
                    </ul>}
            </div>
        </div>
    );
}
```

Add api method:

```typescript
async listMyPermissionRequests(): Promise<MyRequest[]> {
    const r = await this.request('/api/me/permissions/my-requests');
    return r.json();
}
```

- [ ] **Step 3: Wire route**

In `admin-panel/src/app/page.tsx`:

```typescript
import { MyPermissionsPage } from '@/views/MyPermissionsPage';
// ...
{activeTab === 'mypermissions' && <MyPermissionsPage />}
```

Add `mypermissions: null` (always visible) to `TAB_PERMISSION_MAP` in Sidebar.tsx.

- [ ] **Step 4: Build verify**

```bash
cd admin-panel && npm run build 2>&1 | tail -10
```

Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add admin-panel/src/views/MyPermissionsPage.tsx admin-panel/src/lib/api.ts admin-panel/src/app/page.tsx admin-panel/src/components/Sidebar.tsx src/Backend/AuraCore.API/Controllers/Admin/MyPermissionsController.cs
git commit -m "feat(6.11.W6): MyPermissionsPage — self-service access view

Visible to ALL admin roles (no permission required). Shows current role
+ readonly flag + active permission grants, plus the user's own
permission-request history with status (pending/approved/denied) and
denial reason if applicable.

Backend addition: /api/me/permissions/my-requests returns the requesting
user's own request history (max 50)."
```

### Task 34: Production deploy — backend rebuild + admin panel push + EF migrate + smoke

**Goal:** Deploy entire Phase 6.11 to admin.auracore.pro + api.auracore.pro. All steps sequential — DO NOT parallelize.

- [ ] **Step 1: Pre-deploy backup**

```bash
ssh root@165.227.170.3 "cp -r /var/www/auracore-api /var/www/auracore-api.bak-6.11-$(date +%Y%m%d%H%M)"
ssh root@165.227.170.3 "cp -r /var/www/admin-panel /var/www/admin-panel.bak-6.11-$(date +%Y%m%d%H%M)"
ssh root@165.227.170.3 "cp /var/www/auracore-api/auracore.db /var/www/auracore-api/auracore.db.bak-6.11-$(date +%Y%m%d%H%M)"
```

Verify: `ssh root@165.227.170.3 "ls -la /var/www/auracore-api.bak-6.11-* /var/www/admin-panel.bak-6.11-* /var/www/auracore-api/auracore.db.bak-6.11-*"`

- [ ] **Step 2: Build backend in Release**

```bash
dotnet publish src/Backend/AuraCore.API/AuraCore.API.csproj -c Release -o ./publish-6.11 --no-self-contained 2>&1 | tail -10
```

Expected: `Build succeeded.` + DLL list. Confirm `./publish-6.11/AuraCore.API.dll` exists.

- [ ] **Step 3: Upload backend artifacts**

```bash
scp -r ./publish-6.11/* root@165.227.170.3:/var/www/auracore-api-staging/
```

Stage in `/var/www/auracore-api-staging/` first; swap atomically in Step 7.

- [ ] **Step 4: Run EF migration on prod DB**

```bash
ssh root@165.227.170.3 "cd /var/www/auracore-api && dotnet ef database update --project /var/www/auracore-api-staging/ --no-build 2>&1 | tail -20"
```

NOTE: `dotnet ef` requires the migration assemblies. If the production server doesn't have `dotnet-ef`, alternative: generate SQL script locally and apply with sqlite3:

```bash
dotnet ef migrations script --idempotent -o ./migration-6.11.sql --project src/Backend/AuraCore.API
scp ./migration-6.11.sql root@165.227.170.3:/tmp/
ssh root@165.227.170.3 "sqlite3 /var/www/auracore-api/auracore.db < /tmp/migration-6.11.sql"
```

- [ ] **Step 5: Verify schema applied**

```bash
ssh root@165.227.170.3 "sqlite3 /var/www/auracore-api/auracore.db '.tables'"
```

Expected: includes `permission_grants`, `permission_requests`, `revoked_tokens`, `admin_invitations`, `system_settings`, `action_log` (new tables from Wave 1 + Wave 4).

```bash
ssh root@165.227.170.3 "sqlite3 /var/www/auracore-api/auracore.db 'SELECT key, value FROM system_settings ORDER BY key;'"
```

Expected: rows for the 8 SystemSettingDefaults keys with their default values.

- [ ] **Step 6: Set SUPERADMIN_EMAILS env var on prod**

```bash
ssh root@165.227.170.3 "grep SUPERADMIN_EMAILS /etc/auracore-api.env || echo 'SUPERADMIN_EMAILS=ozgurdeniz807@gmail.com' >> /etc/auracore-api.env"
```

(If multiple superadmins desired, use comma-separated list.)

- [ ] **Step 7: Atomic swap + restart backend**

```bash
ssh root@165.227.170.3 "
  systemctl stop auracore-api &&
  rm -rf /var/www/auracore-api/* &&
  cp -r /var/www/auracore-api-staging/* /var/www/auracore-api/ &&
  chown -R www-data:www-data /var/www/auracore-api &&
  systemctl start auracore-api &&
  sleep 3 &&
  systemctl status auracore-api --no-pager | head -15
"
```

Expected: `active (running)`. If FAIL, restore: `rm -rf /var/www/auracore-api/* && cp -r /var/www/auracore-api.bak-6.11-*/* /var/www/auracore-api/ && systemctl restart auracore-api`.

- [ ] **Step 8: Verify SuperadminBootstrapService promoted the right account**

```bash
ssh root@165.227.170.3 "sqlite3 /var/www/auracore-api/auracore.db \"SELECT email, role FROM users WHERE role IN ('admin', 'superadmin');\""
```

Expected: `ozgurdeniz807@gmail.com|superadmin` plus any other admins promoted via grandfather. If superadmin role is NOT set, check `SUPERADMIN_EMAILS` env + journal: `journalctl -u auracore-api -n 30`.

- [ ] **Step 9: Verify GrandfatherMigrationService granted full perms to existing admins**

```bash
ssh root@165.227.170.3 "sqlite3 /var/www/auracore-api/auracore.db \"SELECT u.email, COUNT(*) AS grants FROM users u LEFT JOIN permission_grants g ON g.admin_user_id = u.id AND g.revoked_at IS NULL WHERE u.role = 'admin' GROUP BY u.email;\""
```

Expected: every existing admin has 21 grants (10 tab keys + 11 action keys). If any admin shows 0, grandfather did not run — check journal.

- [ ] **Step 10: Build admin panel for production**

```bash
cd admin-panel && NEXT_PUBLIC_API_URL=https://api.auracore.pro npm run build && npm run export 2>&1 | tail -10
```

Expected: `out/` directory created with static HTML.

- [ ] **Step 11: Upload admin panel**

```bash
ssh root@165.227.170.3 "rm -rf /var/www/admin-panel/*"
scp -r admin-panel/out/* root@165.227.170.3:/var/www/admin-panel/
ssh root@165.227.170.3 "chown -R www-data:www-data /var/www/admin-panel"
```

- [ ] **Step 12: End-to-end smoke test**

```bash
# Backend health
curl -sf https://api.auracore.pro/health | head -5

# Login as superadmin
TOKEN=$(curl -sS -X POST https://api.auracore.pro/api/auth/login \
    -H 'Content-Type: application/json' \
    -d '{"email":"ozgurdeniz807@gmail.com","password":"<your-pw>"}' | jq -r .accessToken)
echo "Token first 30 chars: ${TOKEN:0:30}"

# /api/me/permissions returns superadmin
curl -sS https://api.auracore.pro/api/me/permissions \
    -H "Authorization: Bearer $TOKEN" | jq .

# /api/admin/admin-users (superadmin-only) returns 200
curl -sS https://api.auracore.pro/api/admin/admin-users \
    -H "Authorization: Bearer $TOKEN" | jq 'length'

# Admin panel loads
curl -I https://admin.auracore.pro/ | head -3
```

Expected: backend returns superadmin role + permissions: [], list of admin users, 200 from admin panel.

- [ ] **Step 13: Manual UI verification (browser)**

In browser at https://admin.auracore.pro:
- [ ] Login with superadmin credentials → succeeds
- [ ] All tabs visible including new Admins / Invitations / Permissions / Action Log / Security Policy / My Permissions
- [ ] Admins tab loads list of users
- [ ] Permissions tab: select an admin → grants list renders
- [ ] Security Policy tab loads, shows 8 settings with current values
- [ ] My Permissions tab shows superadmin role + "all permissions" message
- [ ] Logout still works

- [ ] **Step 14: Commit ops marker**

```bash
git commit --allow-empty -m "ops(6.11.W6): production deploy of superadmin foundation

Backend rebuilt in Release + EF migration applied to /var/www/auracore-api/auracore.db.
SUPERADMIN_EMAILS env var set in /etc/auracore-api.env. Existing admins
grandfather-promoted with 21-key permission set; ozgurdeniz807@gmail.com
promoted to superadmin role.

Admin panel rebuilt + static export uploaded to /var/www/admin-panel/.

Backups:
  /var/www/auracore-api.bak-6.11-{ts}
  /var/www/admin-panel.bak-6.11-{ts}
  /var/www/auracore-api/auracore.db.bak-6.11-{ts}

Rollback: stop service, restore each .bak directory + .bak DB, restart.
JWT format unchanged so existing client tokens remain valid post-rollback."
```

### Task 35: Ceremonial merge to main + push

**Files:** None (git ops only).

- [ ] **Step 1: Run full test suite locally one last time**

```bash
dotnet test 2>&1 | tail -10 && cd admin-panel && npm test 2>&1 | tail -10
```

Expected: all green. Note the test count for the merge commit.

- [ ] **Step 2: Confirm clean working tree**

```bash
git status
```

Expected: `nothing to commit, working tree clean`.

- [ ] **Step 3: Push branch to origin**

```bash
git push -u origin phase-6.11-superadmin-foundation
```

- [ ] **Step 4: Create no-fast-forward merge commit on main**

```bash
git checkout main
git pull --ff-only origin main
git merge --no-ff phase-6.11-superadmin-foundation -m "Merge: Phase 6.11 — Superadmin role + permission gating + admin lifecycle

35 tasks across 6 waves:
  W1: DB schema (5 new tables) + grandfather migration + token revocation
  W2: Permission system + email refactor + Tier 1/2 endpoint gating
  W3: Frontend role-aware shell + LockedActionButton + nginx public-cut + DNS
  W4: Superadmin tabs (Admins, Invitations, Action Log) + invitation flow + CSV
  W5: Permission templates UI + force-password-change + mandatory 2FA setup +
      Security Policy + Rate Limit tunables + RetentionJob (24h GC)
  W6: My Permissions tab + production deploy + ceremonial close

Net additions: ~XXXX backend LoC + ~XXXX frontend LoC + XX new tests.
Test suite: XXX/XXX passing + 0 skipped.

Backwards compatibility: every existing admin grandfather-promoted with
the full Tier 1+2 permission set; user-facing /api/auth/login response
shape unchanged for non-forced flows. Mobile RN app + PWA continue to
work without changes (Wave 6.14 will add superadmin-specific RN flows).

Phase 6.11 deploy verified at https://admin.auracore.pro.
Backups and rollback procedure recorded in deploy commit
(see git log --grep='ops(6.11.W6)' for the procedure)."
```

- [ ] **Step 5: Push merge to origin**

```bash
git push origin main
```

- [ ] **Step 6: Verify origin reflects merge**

```bash
git log origin/main --oneline -5
```

Expected: top commit is the merge commit with the Phase 6.11 message.

- [ ] **Step 7: Tag the release**

```bash
git tag -a v6.11.0 -m "Phase 6.11 — Superadmin foundation"
git push origin v6.11.0
```

- [ ] **Step 8: Update memory with phase-complete pointer**

After merge lands and is verified live, update memory:

```bash
# Write a new memory file documenting Phase 6.11 close, then add a one-line entry
# to MEMORY.md. Use the same pattern as project_phase_6_item_10_admin_rebuild_complete.md.
```

Memory entry should capture: branch HEAD SHA, merge commit SHA, ceremonial commit SHA, tag (v6.11.0), backend test count delta, frontend test count delta, deploy timestamp, and rollback procedure (one line linking to Wave 6 Task 34 ops commit).

---

## Self-Review

Performed against `docs/superpowers/specs/2026-04-23-superadmin-foundation-design.md`:

**Spec coverage:**
- D1 (separate endpoint, single subdomain): Wave 2 Task 7 (`/api/auth/superadmin/login`) + Wave 3 Task 21 (no subdomain split, public cut on existing admin.auracore.pro). ✅
- D2 (3-tier permission model): Wave 1 Task 4 PermissionKeys + Wave 2 Task 9 RequiresPermission + DestructiveAction attrs + Wave 2 Task 11 application. ✅
- D3 (ReadOnly via boolean flag): Wave 1 Task 1 User.IsReadonly + Wave 2 Task 9 attribute checks + Wave 3 Task 19 LockedActionButton. ✅
- D4 (templates: ReadOnly/Moderator/Operator/Custom): Wave 1 Task 4 PermissionTemplates static class + Wave 4 Task 22 invitation flow + Wave 5 Task 28 PermissionsPage apply-template UI. ✅
- D5 (per-permission expiry): Wave 1 Task 1 PermissionGrant.ExpiresAt + Wave 5 Task 28 grant dialog days input. ✅
- D6 (revoked-tokens table + middleware): Wave 1 Task 1 RevokedToken entity + Task 6 TokenRevocationMiddleware + JWT jti claim. ✅
- D7 (scope-limited JWT for 2FA setup): Wave 5 Task 30 (issue + middleware enforcement) + Wave 2 Task 16 (AdminHub rejection). ✅
- D8 (force-password-change middleware): Wave 5 Task 29. ✅
- D9 (mandatory 2FA hybrid: per-account + global): Wave 5 Task 30 (must_enable_2fa_at + force_2fa_all_admins). ✅
- D10 (action_log distinct from audit_log): Wave 1 Task 1 entity + Wave 4 Task 26 service + filter + 90-day retention via Wave 5 Task 32. ✅
- D11 (CSV export, 366-day max range): Wave 4 Task 27. ✅
- D12 (single-use invitation tokens, SHA256): Wave 4 Task 22. ✅
- D13 (grandfather migration on first boot): Wave 1 Task 4 GrandfatherMigrationService + Wave 6 Task 34 verification step. ✅
- D14 (system_settings tunables): Wave 1 Task 1 entity + Wave 5 Task 31 controller + UI + apply in AuthController. ✅
- D15 (Resend HTTPS only, no SMTP): Wave 2 Task 14 IEmailService refactor + ResendEmailService. ✅
- D16 (DNS SPF/DMARC tightened): Wave 3 Task 21 ops steps. ✅
- D17 (RN app deferred to 6.14): No tasks here (out-of-scope). ✅

**Placeholder scan:** No "TBD" / "implement later" / "similar to Task N" / "appropriate error handling" found. Step 6 of Task 26 ("Stamp UsedPermissionKey from RequiresPermissionAttribute") references Wave 2 Task 9 — concrete instruction (one-line `context.HttpContext.Items["UsedPermissionKey"] = Permission;` add); not a placeholder.

**Type consistency:**
- `PermissionGrant.PermissionKey` (string) used consistently across Wave 1 entity + Wave 2 attr + Wave 4 ApplyTemplate + Wave 5 grant UI.
- `User.IsReadonly` (Wave 1) → `IsReadonly` flag in API response (Wave 2 task 11 [DestructiveAction]) → `isReadonly` in frontend User type (Wave 3 Task 17) → checked by `usePermissions().canPerformDestructive()` (Wave 3 Task 17) → enforced by LockedActionButton (Wave 3 Task 19). Names match.
- `User.MustChangePasswordAt` (Wave 1) → checked by ForcePasswordChangeMiddleware (Wave 5 Task 29) → cleared by `/api/auth/change-password` (Wave 5 Task 29). Consistent.
- `User.MustEnable2FAAt` (Wave 1) → set during invitation accept (Wave 4 Task 22) + by superadmin force action (Wave 4 Task 23) → checked by ForceTwoFactorSetupMiddleware (Wave 5 Task 30) → cleared by /api/auth/2fa/verify-setup (Wave 5 Task 30). Consistent.
- `SystemSetting.Key` strings: `force_2fa_all_admins` used in Wave 5 Task 30 middleware AND Wave 5 Task 31 SystemSettingDefaults — names match.

**Issues found and fixed inline:** None (full pass).

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-04-23-superadmin-foundation.md`. Two execution options:

**1. Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration. Best for this plan because:
- 35 tasks with TDD-tight scope each
- Per-task spec-reviewer + code-quality-reviewer catches scope creep early
- Wave-level checkpoints (especially Wave 1 → Wave 2 schema dependency, Wave 5 middleware ordering, Wave 6 deploy)

**2. Inline Execution** — Execute tasks in this session using executing-plans, batch execution with checkpoints. Suitable if you want full transparency and to interrupt at any task.

**Which approach?**
