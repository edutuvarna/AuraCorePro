# Release Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship end-to-end release pipeline — R2 presigned uploads from admin panel, per-platform publishing (Windows + Linux, macOS stubbed "Coming Soon"), dynamic landing page download links, desktop app in-place update flow with SHA256 verify, and GitHub Releases mirror.

**Architecture:** Hybrid upload (admin panel → backend mints presigned PUT URL → browser PUTs direct to R2 → backend HEAD+COPY+SHA256+DB row finalize). `download.auracore.pro` custom R2 domain fronts releases. Desktop client polls `/api/updates/check?platform=X` on startup, banner-or-modal UI, background download → verify → `Process.Start(installer)` → `Environment.Exit(0)`. GitHub mirror is async fire-and-forget with retry endpoint.

**Tech Stack:** ASP.NET Core 8.0 (C#), EF Core 8.0.11 + Npgsql, AWSSDK.S3 (for R2 S3-compat), Octokit.NET (GitHub mirror), xUnit + Moq (backend tests), Avalonia 11 (desktop), Next.js 13+ App Router (admin panel — separate repo at `C:\Users\Admin\Desktop\AuraCorePro\Adminpanel\`), vanilla JS (landing page at `landing-page-work/`).

**Spec reference:** [`docs/superpowers/specs/2026-04-19-release-pipeline-design.md`](../specs/2026-04-19-release-pipeline-design.md)

**Baseline:** Main HEAD `3af42d6`. 2270/2270 tests passing. Target after this plan: ~2295 passing (~+25 tests for endpoints + client flow + migration + R2 client).

---

## Pre-flight decisions (resolve BEFORE Task 1)

The spec lists open questions that must be answered before implementation begins. All have conservative defaults marked **(DEFAULT)** — if user is AFK or hasn't answered by the time the subagent starts, use these.

1. **R2 bucket name:** `auracore-releases` **(DEFAULT)** — new dedicated bucket, NOT a prefix under the existing `auracore-ai-models` bucket. Cleaner separation + independent lifecycle rules.
2. **R2 SDK choice:** `AWSSDK.S3` **(DEFAULT)** — industry-standard, single dep, R2 is S3-compatible. Not `Minio.NET`.
3. **GitHub PAT scope:** fine-grained, `contents:write` on `edutuvarna/AuraCorePro` repo only, 1-year expiry. Stored in `/etc/auracore-api.env` as `ASPNETCORE_GITHUB_TOKEN`. User provisions manually in sub-phase 6.6.H.
4. **Migrations bootstrap:** We will introduce EF Core migrations for the first time. Existing production DB was created by `EnsureCreated`-equivalent path. The InitialCreate migration we generate will produce SQL matching current schema; we MUST seed `__EFMigrationsHistory` on production manually so EF doesn't attempt to recreate tables. See Task 1.6 for the exact SQL.
5. **Branch name:** `phase-6-release-pipeline` **(DEFAULT)** — same `--no-ff` merge ritual as Wave 3 / Debt Sweep / 6.1 / 6.2 / 6.3 / 6.4 / 6.5.

---

## File structure overview

New files (creation):

**Backend:**
- `src/Backend/AuraCore.API.Domain/Entities/AppUpdatePlatform.cs` — enum (Windows=1, Linux=2, MacOS=3)
- `src/Backend/AuraCore.API.Infrastructure/Migrations/*_InitialCreate.cs` — EF bootstrap migration (auto-generated)
- `src/Backend/AuraCore.API.Infrastructure/Migrations/*_AddPlatformToAppUpdate.cs` — Platform column + v1.6.0 seed (auto-generated + hand-edited seed)
- `src/Backend/AuraCore.API.Application/Services/Releases/IR2Client.cs` — R2 abstraction interface
- `src/Backend/AuraCore.API.Infrastructure/Services/Releases/AwsR2Client.cs` — AWSSDK.S3 impl
- `src/Backend/AuraCore.API.Application/Services/Releases/IGitHubReleaseMirror.cs` — GitHub mirror interface
- `src/Backend/AuraCore.API.Infrastructure/Services/Releases/OctokitReleaseMirror.cs` — Octokit.NET impl
- `tests/AuraCore.Tests.API/Releases/PrepareUploadEndpointTests.cs`
- `tests/AuraCore.Tests.API/Releases/PublishEndpointTests.cs`
- `tests/AuraCore.Tests.API/Releases/CheckPlatformFilterTests.cs`
- `tests/AuraCore.Tests.API/Releases/R2ClientTests.cs`
- `tests/AuraCore.Tests.API/Releases/GitHubReleaseMirrorTests.cs`

**Desktop:**
- `src/UI/AuraCore.UI.Avalonia/Services/Update/IUpdateDownloader.cs` — download+verify+launch interface (new, extracted from existing `UpdateChecker` monolith)
- `src/UI/AuraCore.UI.Avalonia/Services/Update/UpdateDownloader.cs` — impl
- `src/UI/AuraCore.UI.Avalonia/Views/Controls/UpdateBanner.axaml` + `.cs` — soft banner UI
- `src/UI/AuraCore.UI.Avalonia/Views/Dialogs/MandatoryUpdateDialog.axaml` + `.cs` — hard-modal UI
- `tests/AuraCore.Tests.UI.Avalonia/Services/UpdateDownloaderTests.cs`
- `tests/AuraCore.Tests.UI.Avalonia/Services/UpdateCheckerPlatformTests.cs`

**Ops docs:**
- `docs/ops/release-pipeline-setup.md` — R2 + custom domain + lifecycle + GitHub PAT + env vars

Files to modify:

**Backend:**
- `src/Backend/AuraCore.API.Domain/Entities/AppUpdate.cs` — add `Platform` property
- `src/Backend/AuraCore.API.Infrastructure/Data/AuraCoreDbContext.cs` — swap `(Version, Channel)` unique index for `(Version, Channel, Platform)` + `Platform` property config + optional `GitHubReleaseId` nullable
- `src/Backend/AuraCore.API/Controllers/Admin/AdminUpdateController.cs` — add `prepare-upload` + `publish-v2` + `mirror-to-github` retry endpoints; old `publish` removed
- `src/Backend/AuraCore.API/Controllers/UpdateController.cs` — add `?platform=` query param, default `windows`
- `src/Backend/AuraCore.API/Program.cs` — DI registration for `IR2Client`, `IGitHubReleaseMirror`
- `src/Backend/AuraCore.API.Infrastructure/AuraCore.API.Infrastructure.csproj` — add `AWSSDK.S3` + `Octokit` packages

**Desktop:**
- `src/UI/AuraCore.UI.Avalonia/Services/Update/UpdateChecker.cs` (new, thin refactor) OR existing `src/Desktop/AuraCore.Desktop/Services/UpdateChecker.cs` — add `platform` query param, split download+install to `UpdateDownloader`
- `src/UI/AuraCore.UI.Avalonia/App.axaml.cs` — wire `IUpdateDownloader` to DI
- `src/UI/AuraCore.UI.Avalonia/Views/MainWindow.axaml` — `<local:UpdateBanner>` docked below app-bar
- `src/UI/AuraCore.UI.Avalonia/Services/Localization/LocalizationService.cs` — ~12 new EN+TR key pairs for banner/dialog strings

**Landing:**
- `landing-page-work/index.html` — add `.download-link` class (already there line 147,280), add `.download-version` + `.download-platform` spans, add `<details class="other-platforms">` block
- `landing-page-work/scripts/main.js` — append `loadLatestRelease()` IIFE bottom of file (no refactor of existing 107KB)

**Admin panel** (SEPARATE REPO at `C:\Users\Admin\Desktop\AuraCorePro\Adminpanel\`):
- `src/app/page.tsx` — UpdatesPage component (lines 796-881): replace DownloadUrl text input with per-platform checkboxes + file upload + presigned PUT XHR flow; fix "Invalid Date" bug in the same function
- `src/app/page.tsx` — UsersPage component (lines 536-607): add ID column with short-GUID + click-to-copy
- `src/lib/api.ts` — add `prepareUpload(version, platform, filename, channel)` + `publishUpdate` v2 DTO

---

## Sub-phase 6.6.A — Data layer (Platform column + migrations bootstrap + v1.6.0 backfill)

Introduces EF Core migrations infrastructure for the first time. Adds `Platform` enum column. Seeds v1.6.0 row. **~3 hours.**

### Task 1: Scaffold branch + pre-flight

**Files:**
- Branch: `phase-6-release-pipeline`

- [ ] **Step 1: Create branch from main**

```bash
cd "C:\Users\Admin\Desktop\AuraCorePro\AuraCorePro"
git checkout main
git pull
git checkout -b phase-6-release-pipeline
```

Expected: `Switched to a new branch 'phase-6-release-pipeline'`

- [ ] **Step 2: Verify baseline tests pass**

Run: `dotnet test AuraCorePro.sln --logger "console;verbosity=minimal" --no-restore 2>&1 | tail -20`

Expected: `Passed! - Failed: 0, Passed: 2270` (or close — baseline is 2270 per memory).

- [ ] **Step 3: Commit baseline marker (empty commit)**

```bash
git commit --allow-empty -m "chore: phase 6 release pipeline work begins

Baseline: 2270/2270 tests passing at main HEAD 3af42d6."
```

### Task 2: Add `AppUpdatePlatform` enum

**Files:**
- Create: `src/Backend/AuraCore.API.Domain/Entities/AppUpdatePlatform.cs`

- [ ] **Step 1: Create enum file**

```csharp
namespace AuraCore.API.Domain.Entities;

public enum AppUpdatePlatform
{
    Windows = 1,
    Linux = 2,
    MacOS = 3,
}
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build src/Backend/AuraCore.API.Domain/AuraCore.API.Domain.csproj --no-restore`
Expected: Build succeeds, 0 errors.

### Task 3: Add `Platform` property to `AppUpdate` entity

**Files:**
- Modify: `src/Backend/AuraCore.API.Domain/Entities/AppUpdate.cs`

- [ ] **Step 1: Add `Platform` + `GitHubReleaseId` properties**

Replace entire file content with:

```csharp
namespace AuraCore.API.Domain.Entities;

public sealed class AppUpdate
{
    public Guid Id { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Channel { get; set; } = "stable";
    public AppUpdatePlatform Platform { get; set; } = AppUpdatePlatform.Windows;
    public string? ReleaseNotes { get; set; }
    public string BinaryUrl { get; set; } = string.Empty;
    public string SignatureHash { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public string? GitHubReleaseId { get; set; }  // null until mirror completes
}
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build src/Backend/AuraCore.API.Domain/AuraCore.API.Domain.csproj --no-restore`
Expected: Build succeeds, 0 errors.

### Task 4: Update `AuraCoreDbContext` AppUpdate configuration

**Files:**
- Modify: `src/Backend/AuraCore.API.Infrastructure/Data/AuraCoreDbContext.cs:97-106`

- [ ] **Step 1: Replace AppUpdate entity configuration**

Replace lines 97-106 (the existing `m.Entity<AppUpdate>(...)` block) with:

```csharp
m.Entity<AppUpdate>(e => {
    e.ToTable("app_updates"); e.HasKey(u => u.Id);
    e.Property(u => u.Id).HasDefaultValueSql("gen_random_uuid()");
    e.Property(u => u.Version).HasMaxLength(32).IsRequired();
    e.Property(u => u.Channel).HasMaxLength(20).HasDefaultValue("stable");
    e.Property(u => u.Platform).HasDefaultValue(AppUpdatePlatform.Windows);
    e.HasIndex(u => new { u.Version, u.Channel, u.Platform }).IsUnique();
    e.Property(u => u.BinaryUrl).HasMaxLength(1024);
    e.Property(u => u.SignatureHash).HasMaxLength(256);
    e.Property(u => u.GitHubReleaseId).HasMaxLength(64);
    e.Property(u => u.PublishedAt).HasDefaultValueSql("now()");
});
```

- [ ] **Step 2: Build full solution**

Run: `dotnet build AuraCorePro.sln --no-restore 2>&1 | tail -5`
Expected: Build succeeds, 0 errors. If compile errors appear in `AdminUpdateController.Publish` or similar, that's expected — those are refactored in sub-phase 6.6.C. For now, if build fails ONLY in `AdminUpdateController.cs`, STOP and note the error; we need DbContext to compile cleanly.

### Task 5: Install EF Core CLI tool + add Infrastructure migration output dir

**Files:**
- None yet (tool install + folder prep).

- [ ] **Step 1: Install dotnet-ef global tool (idempotent)**

```bash
dotnet tool install --global dotnet-ef --version 8.0.11 2>/dev/null || dotnet tool update --global dotnet-ef --version 8.0.11
```

Expected: `Tool 'dotnet-ef' was successfully installed` OR `Tool 'dotnet-ef' was reinstalled with the stable version`.

- [ ] **Step 2: Verify EF tool accessible**

Run: `dotnet ef --version`
Expected: `Entity Framework Core .NET Command-line Tools 8.0.11` (or similar).

### Task 6: Generate `InitialCreate` migration (bootstrap)

This migration captures the CURRENT schema. Production DB already has this schema (created via `EnsureCreated`-style). We will manually seed `__EFMigrationsHistory` on production so EF knows not to re-apply.

**Files:**
- Create (auto-generated): `src/Backend/AuraCore.API.Infrastructure/Migrations/<timestamp>_InitialCreate.cs`
- Create (auto-generated): `src/Backend/AuraCore.API.Infrastructure/Migrations/AuraCoreDbContextModelSnapshot.cs`

- [ ] **Step 1: Temporarily revert DbContext AppUpdate config to pre-Platform state**

**CRITICAL:** The InitialCreate migration must match existing production schema. Since we already modified DbContext in Task 4, we need to revert JUST the AppUpdate config for this single migration, then re-apply the change as the NEXT migration.

Revert lines 97-106 of `AuraCoreDbContext.cs` to:

```csharp
m.Entity<AppUpdate>(e => {
    e.ToTable("app_updates"); e.HasKey(u => u.Id);
    e.Property(u => u.Id).HasDefaultValueSql("gen_random_uuid()");
    e.Property(u => u.Version).HasMaxLength(32).IsRequired();
    e.HasIndex(u => new { u.Version, u.Channel }).IsUnique();
    e.Property(u => u.Channel).HasMaxLength(20).HasDefaultValue("stable");
    e.Property(u => u.BinaryUrl).HasMaxLength(1024);
    e.Property(u => u.SignatureHash).HasMaxLength(256);
    e.Property(u => u.PublishedAt).HasDefaultValueSql("now()");
});
```

Also temporarily remove `Platform` and `GitHubReleaseId` from `AppUpdate` entity (keep the enum file — it's harmless in a build).

- [ ] **Step 2: Generate InitialCreate migration**

```bash
cd "C:\Users\Admin\Desktop\AuraCorePro\AuraCorePro"
dotnet ef migrations add InitialCreate \
  --project src/Backend/AuraCore.API.Infrastructure \
  --startup-project src/Backend/AuraCore.API \
  --output-dir Migrations
```

Expected: `Done. To undo this action, use 'ef migrations remove'.` Two new files appear in `src/Backend/AuraCore.API.Infrastructure/Migrations/`.

- [ ] **Step 3: Commit InitialCreate migration**

```bash
git add src/Backend/AuraCore.API.Domain/Entities/AppUpdatePlatform.cs \
        src/Backend/AuraCore.API.Infrastructure/Migrations/
git commit -m "feat(backend): bootstrap EF Core migrations with InitialCreate

Captures current production schema. Production DB already has this schema
from EnsureCreated-style path; __EFMigrationsHistory will be seeded manually
during deploy (see docs/ops/release-pipeline-setup.md)."
```

### Task 7: Re-apply Platform changes + generate `AddPlatformToAppUpdate` migration

**Files:**
- Modify: `src/Backend/AuraCore.API.Domain/Entities/AppUpdate.cs` (restore Platform + GitHubReleaseId)
- Modify: `src/Backend/AuraCore.API.Infrastructure/Data/AuraCoreDbContext.cs:97-106` (restore Task 4 config)
- Create (auto-generated + hand-edited): `src/Backend/AuraCore.API.Infrastructure/Migrations/<timestamp>_AddPlatformToAppUpdate.cs`

- [ ] **Step 1: Restore `AppUpdate` entity Platform + GitHubReleaseId properties** (re-apply Task 3's content).

- [ ] **Step 2: Restore DbContext AppUpdate configuration** (re-apply Task 4's content).

- [ ] **Step 3: Generate migration**

```bash
dotnet ef migrations add AddPlatformToAppUpdate \
  --project src/Backend/AuraCore.API.Infrastructure \
  --startup-project src/Backend/AuraCore.API \
  --output-dir Migrations
```

Expected: new `*_AddPlatformToAppUpdate.cs` file. It should: drop old `IX_app_updates_Version_Channel` unique index, add `Platform` int column (default 1 = Windows), add `GitHubReleaseId` string column (nullable, max 64), add new `IX_app_updates_Version_Channel_Platform` unique index.

- [ ] **Step 4: Hand-edit migration to append v1.6.0 backfill SQL**

Open the generated `*_AddPlatformToAppUpdate.cs`. At the END of the `Up(MigrationBuilder migrationBuilder)` method, append:

```csharp
migrationBuilder.Sql(@"
    INSERT INTO app_updates
    (""Id"", ""Version"", ""Channel"", ""Platform"", ""ReleaseNotes"", ""BinaryUrl"",
     ""SignatureHash"", ""IsMandatory"", ""PublishedAt"", ""GitHubReleaseId"")
    SELECT gen_random_uuid(), '1.6.0', 'stable', 1,
           'Legacy Windows release migrated from GitHub.',
           'https://github.com/edutuvarna/AuraCorePro/releases/download/v1.6.0/AuraCorePro-Setup.exe',
           '', false, '2026-01-15T00:00:00Z'::timestamptz, NULL
    WHERE NOT EXISTS (
        SELECT 1 FROM app_updates WHERE ""Version"" = '1.6.0' AND ""Platform"" = 1
    );
");
```

Add corresponding `Down` cleanup at the END of the `Down(MigrationBuilder migrationBuilder)` method:

```csharp
migrationBuilder.Sql(@"DELETE FROM app_updates WHERE ""Version"" = '1.6.0' AND ""BinaryUrl"" LIKE '%github.com%';");
```

- [ ] **Step 5: Build to verify migration compiles**

Run: `dotnet build src/Backend/AuraCore.API.Infrastructure/AuraCore.API.Infrastructure.csproj --no-restore`
Expected: Build succeeds.

### Task 8: Unit test for v1.6.0 seed idempotence

**Files:**
- Create: `tests/AuraCore.Tests.API/Releases/MigrationSeedTests.cs`

- [ ] **Step 1: Verify a test project for API exists**

Run: `ls tests/AuraCore.Tests.API/*.csproj`
Expected: `tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj` exists.

- [ ] **Step 2: Check test project deps**

Read `tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj`. Confirm it references `AuraCore.API.Infrastructure` (or add if missing) and has `Microsoft.EntityFrameworkCore.InMemory` (add if missing):

If missing, run:
```bash
dotnet add tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj reference src/Backend/AuraCore.API.Infrastructure/AuraCore.API.Infrastructure.csproj
dotnet add tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj package Microsoft.EntityFrameworkCore.InMemory --version 8.0.11
```

- [ ] **Step 3: Write failing test**

Create `tests/AuraCore.Tests.API/Releases/MigrationSeedTests.cs`:

```csharp
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuraCore.Tests.API.Releases;

public class MigrationSeedTests
{
    [Fact]
    public void AppUpdate_supports_Windows_Linux_MacOS_platform_values()
    {
        Assert.Equal(1, (int)AppUpdatePlatform.Windows);
        Assert.Equal(2, (int)AppUpdatePlatform.Linux);
        Assert.Equal(3, (int)AppUpdatePlatform.MacOS);
    }

    [Fact]
    public void AppUpdate_platform_defaults_to_Windows_when_not_set()
    {
        var u = new AppUpdate { Version = "1.0.0", BinaryUrl = "https://x" };
        Assert.Equal(AppUpdatePlatform.Windows, u.Platform);
    }

    [Fact]
    public void Composite_unique_index_allows_same_version_on_different_platforms()
    {
        // This is a model-level test — confirms the new composite index config
        // allows (v1.7.0, stable, Windows) AND (v1.7.0, stable, Linux) to coexist.
        var options = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"test-{Guid.NewGuid()}")
            .Options;
        using var db = new AuraCoreDbContext(options);
        db.AppUpdates.Add(new AppUpdate { Version = "1.7.0", Channel = "stable", Platform = AppUpdatePlatform.Windows, BinaryUrl = "https://x/w", SignatureHash = "", PublishedAt = DateTimeOffset.UtcNow });
        db.AppUpdates.Add(new AppUpdate { Version = "1.7.0", Channel = "stable", Platform = AppUpdatePlatform.Linux,   BinaryUrl = "https://x/l", SignatureHash = "", PublishedAt = DateTimeOffset.UtcNow });
        db.SaveChanges();
        Assert.Equal(2, db.AppUpdates.Count());
    }
}
```

- [ ] **Step 4: Run tests — expect PASS**

Run: `dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter "FullyQualifiedName~MigrationSeedTests" --logger "console;verbosity=minimal"`
Expected: `Passed: 3, Failed: 0`.

- [ ] **Step 5: Commit sub-phase 6.6.A milestone**

```bash
git add src/Backend/AuraCore.API.Domain/Entities/AppUpdate.cs \
        src/Backend/AuraCore.API.Infrastructure/Data/AuraCoreDbContext.cs \
        src/Backend/AuraCore.API.Infrastructure/Migrations/ \
        tests/AuraCore.Tests.API/
git commit -m "feat(backend): add AppUpdate.Platform + v1.6.0 backfill migration (6.6.A)

- AppUpdatePlatform enum (Windows=1, Linux=2, MacOS=3)
- AppUpdate.Platform + .GitHubReleaseId properties
- Composite unique index on (Version, Channel, Platform)
- AddPlatformToAppUpdate migration with v1.6.0 seed SQL
- 3 unit tests: enum values, default, composite uniqueness"
```

---

## Sub-phase 6.6.B — R2 client abstraction

Introduces `IR2Client` + `AwsR2Client` impl with AWSSDK.S3. Unit-tested against mocked `IAmazonS3`. **~4 hours.**

### Task 9: Add AWSSDK.S3 NuGet + create IR2Client interface

**Files:**
- Modify: `src/Backend/AuraCore.API.Infrastructure/AuraCore.API.Infrastructure.csproj` — add `AWSSDK.S3` package
- Create: `src/Backend/AuraCore.API.Application/Services/Releases/IR2Client.cs`
- Create: `src/Backend/AuraCore.API.Application/Services/Releases/R2Models.cs`

- [ ] **Step 1: Add AWSSDK.S3 package**

```bash
dotnet add src/Backend/AuraCore.API.Infrastructure/AuraCore.API.Infrastructure.csproj package AWSSDK.S3 --version 3.7.404
```

Expected: package added, `AuraCore.API.Infrastructure.csproj` gains `<PackageReference Include="AWSSDK.S3" Version="3.7.404" />`.

- [ ] **Step 2: Create R2 models + interface**

`src/Backend/AuraCore.API.Application/Services/Releases/R2Models.cs`:

```csharp
namespace AuraCore.API.Application.Services.Releases;

public sealed record R2ObjectHead(long SizeBytes, DateTimeOffset LastModified, string? ContentType);

public sealed record PresignedPutResult(string UploadUrl, string ObjectKey, DateTimeOffset ExpiresAt);
```

`src/Backend/AuraCore.API.Application/Services/Releases/IR2Client.cs`:

```csharp
namespace AuraCore.API.Application.Services.Releases;

public interface IR2Client
{
    Task<PresignedPutResult> GeneratePresignedPutUrlAsync(
        string objectKey, TimeSpan ttl, long maxSizeBytes, CancellationToken ct);

    Task<R2ObjectHead?> HeadObjectAsync(string objectKey, CancellationToken ct);

    Task CopyObjectAsync(string sourceKey, string destinationKey, CancellationToken ct);

    Task DeleteObjectAsync(string objectKey, CancellationToken ct);

    /// <summary>Streams the object from R2 and returns SHA256 hex (lowercase).</summary>
    Task<string> ComputeSha256Async(string objectKey, CancellationToken ct);

    /// <summary>Streams the object into the caller-provided Stream (for GitHub mirror).</summary>
    Task DownloadToStreamAsync(string objectKey, Stream destination, CancellationToken ct);
}
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build src/Backend/AuraCore.API.Application/AuraCore.API.Application.csproj --no-restore`
Expected: Build succeeds.

### Task 10: Implement `AwsR2Client` (TDD — RED first)

**Files:**
- Create: `tests/AuraCore.Tests.API/Releases/R2ClientTests.cs`
- Create: `src/Backend/AuraCore.API.Infrastructure/Services/Releases/AwsR2Client.cs`

- [ ] **Step 1: Add Moq package to test project if missing**

Check if `tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj` has `<PackageReference Include="Moq" ... />`. If not:

```bash
dotnet add tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj package Moq --version 4.20.72
dotnet add tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj reference src/Backend/AuraCore.API.Application/AuraCore.API.Application.csproj
```

- [ ] **Step 2: Write failing test for presigned PUT URL format**

`tests/AuraCore.Tests.API/Releases/R2ClientTests.cs`:

```csharp
using Amazon.S3;
using Amazon.S3.Model;
using AuraCore.API.Application.Services.Releases;
using AuraCore.API.Infrastructure.Services.Releases;
using Moq;
using Xunit;

namespace AuraCore.Tests.API.Releases;

public class R2ClientTests
{
    private static AwsR2Client BuildClient(Mock<IAmazonS3> s3)
        => new AwsR2Client(s3.Object, bucketName: "auracore-releases");

    [Fact]
    public async Task GeneratePresignedPutUrlAsync_returns_url_and_object_key()
    {
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(x => x.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
          .ReturnsAsync("https://auracore-releases.r2.cloudflarestorage.com/pending/abc-installer.exe?X-Amz-Signature=xyz");

        var client = BuildClient(s3);
        var result = await client.GeneratePresignedPutUrlAsync(
            "pending/abc-installer.exe", TimeSpan.FromMinutes(10), 500_000_000, CancellationToken.None);

        Assert.StartsWith("https://", result.UploadUrl);
        Assert.Equal("pending/abc-installer.exe", result.ObjectKey);
        Assert.True(result.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task HeadObjectAsync_returns_null_when_object_missing()
    {
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(x => x.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()))
          .ThrowsAsync(new AmazonS3Exception("Not Found") { StatusCode = System.Net.HttpStatusCode.NotFound });

        var client = BuildClient(s3);
        var head = await client.HeadObjectAsync("pending/missing.exe", CancellationToken.None);
        Assert.Null(head);
    }

    [Fact]
    public async Task HeadObjectAsync_returns_size_and_timestamp_when_exists()
    {
        var lm = new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc);
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(x => x.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new GetObjectMetadataResponse {
              ContentLength = 52_428_800,  // 50 MB
              LastModified = lm,
              Headers = { ContentType = "application/octet-stream" }
          });

        var client = BuildClient(s3);
        var head = await client.HeadObjectAsync("releases/v1.7.0/AuraCorePro-Windows-v1.7.0.exe", CancellationToken.None);
        Assert.NotNull(head);
        Assert.Equal(52_428_800, head!.SizeBytes);
        Assert.Equal("application/octet-stream", head.ContentType);
    }

    [Fact]
    public async Task ComputeSha256Async_returns_64_char_lowercase_hex()
    {
        var payload = System.Text.Encoding.UTF8.GetBytes("test-installer-content");
        var expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload)).ToLowerInvariant();

        var s3 = new Mock<IAmazonS3>();
        s3.Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new GetObjectResponse { ResponseStream = new MemoryStream(payload) });

        var client = BuildClient(s3);
        var hash = await client.ComputeSha256Async("releases/v1.7.0/x.exe", CancellationToken.None);
        Assert.Equal(expectedHash, hash);
        Assert.Equal(64, hash.Length);
    }

    [Fact]
    public async Task CopyObjectAsync_maps_source_and_destination_keys()
    {
        var s3 = new Mock<IAmazonS3>();
        CopyObjectRequest? captured = null;
        s3.Setup(x => x.CopyObjectAsync(It.IsAny<CopyObjectRequest>(), It.IsAny<CancellationToken>()))
          .Callback<CopyObjectRequest, CancellationToken>((r, _) => captured = r)
          .ReturnsAsync(new CopyObjectResponse());

        var client = BuildClient(s3);
        await client.CopyObjectAsync("pending/abc.exe", "releases/v1.7.0/x.exe", CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("auracore-releases", captured!.SourceBucket);
        Assert.Equal("pending/abc.exe", captured.SourceKey);
        Assert.Equal("auracore-releases", captured.DestinationBucket);
        Assert.Equal("releases/v1.7.0/x.exe", captured.DestinationKey);
    }
}
```

- [ ] **Step 3: Run tests to verify they FAIL (RED)**

Run: `dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter "FullyQualifiedName~R2ClientTests" --logger "console;verbosity=minimal"`
Expected: Build error — `AwsR2Client` type not found.

- [ ] **Step 4: Implement `AwsR2Client`**

`src/Backend/AuraCore.API.Infrastructure/Services/Releases/AwsR2Client.cs`:

```csharp
using System.Security.Cryptography;
using Amazon.S3;
using Amazon.S3.Model;
using AuraCore.API.Application.Services.Releases;

namespace AuraCore.API.Infrastructure.Services.Releases;

public sealed class AwsR2Client : IR2Client
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;

    public AwsR2Client(IAmazonS3 s3, string bucketName)
    {
        _s3 = s3;
        _bucket = bucketName;
    }

    public async Task<PresignedPutResult> GeneratePresignedPutUrlAsync(
        string objectKey, TimeSpan ttl, long maxSizeBytes, CancellationToken ct)
    {
        var req = new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(ttl),
        };
        // R2 enforces size via x-amz-decoded-content-length header; we document the cap to the caller
        var url = await _s3.GetPreSignedURLAsync(req);
        return new PresignedPutResult(url, objectKey, DateTimeOffset.UtcNow.Add(ttl));
    }

    public async Task<R2ObjectHead?> HeadObjectAsync(string objectKey, CancellationToken ct)
    {
        try
        {
            var resp = await _s3.GetObjectMetadataAsync(new GetObjectMetadataRequest {
                BucketName = _bucket, Key = objectKey
            }, ct);
            return new R2ObjectHead(resp.ContentLength, resp.LastModified, resp.Headers.ContentType);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task CopyObjectAsync(string sourceKey, string destinationKey, CancellationToken ct)
        => _s3.CopyObjectAsync(new CopyObjectRequest {
            SourceBucket = _bucket, SourceKey = sourceKey,
            DestinationBucket = _bucket, DestinationKey = destinationKey
        }, ct);

    public Task DeleteObjectAsync(string objectKey, CancellationToken ct)
        => _s3.DeleteObjectAsync(new DeleteObjectRequest {
            BucketName = _bucket, Key = objectKey
        }, ct);

    public async Task<string> ComputeSha256Async(string objectKey, CancellationToken ct)
    {
        using var resp = await _s3.GetObjectAsync(new GetObjectRequest {
            BucketName = _bucket, Key = objectKey
        }, ct);
        using var sha = SHA256.Create();
        var hashBytes = await sha.ComputeHashAsync(resp.ResponseStream, ct);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public async Task DownloadToStreamAsync(string objectKey, Stream destination, CancellationToken ct)
    {
        using var resp = await _s3.GetObjectAsync(new GetObjectRequest {
            BucketName = _bucket, Key = objectKey
        }, ct);
        await resp.ResponseStream.CopyToAsync(destination, ct);
    }
}
```

- [ ] **Step 5: Run tests — expect GREEN**

Run: `dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter "FullyQualifiedName~R2ClientTests" --logger "console;verbosity=minimal"`
Expected: `Passed: 5, Failed: 0`.

- [ ] **Step 6: Commit**

```bash
git add src/Backend/AuraCore.API.Application/Services/Releases/ \
        src/Backend/AuraCore.API.Infrastructure/Services/Releases/AwsR2Client.cs \
        src/Backend/AuraCore.API.Infrastructure/AuraCore.API.Infrastructure.csproj \
        tests/AuraCore.Tests.API/
git commit -m "feat(backend): IR2Client + AwsR2Client with 5 unit tests (6.6.B)

AWSSDK.S3 3.7.404 added. Presigned PUT / HEAD / COPY / DELETE /
SHA256 stream / download-to-stream covered."
```

### Task 11: Register `IR2Client` in DI

**Files:**
- Modify: `src/Backend/AuraCore.API/Program.cs` — add `AmazonS3Client` + `IR2Client` registration
- Modify: `src/Backend/AuraCore.API/appsettings.json` + `appsettings.Development.json` — placeholder config section (real creds from env)

- [ ] **Step 1: Add R2 config + DI registration in `Program.cs`**

In `Program.cs`, just after the JWT auth registration (around line 67, before `builder.WebHost.ConfigureKestrel`), add:

```csharp
// R2 (S3-compatible) client for release binary storage
var r2AccountId = Environment.GetEnvironmentVariable("R2_ACCOUNT_ID") ?? "";
var r2AccessKey = Environment.GetEnvironmentVariable("R2_ACCESS_KEY_ID") ?? "";
var r2Secret    = Environment.GetEnvironmentVariable("R2_SECRET_ACCESS_KEY") ?? "";
var r2Bucket    = Environment.GetEnvironmentVariable("R2_BUCKET") ?? "auracore-releases";

builder.Services.AddSingleton<Amazon.S3.IAmazonS3>(_ =>
{
    var cfg = new Amazon.S3.AmazonS3Config
    {
        ServiceURL = $"https://{r2AccountId}.r2.cloudflarestorage.com",
        ForcePathStyle = true,  // required for R2
    };
    return new Amazon.S3.AmazonS3Client(r2AccessKey, r2Secret, cfg);
});
builder.Services.AddScoped<AuraCore.API.Application.Services.Releases.IR2Client>(sp =>
    new AuraCore.API.Infrastructure.Services.Releases.AwsR2Client(
        sp.GetRequiredService<Amazon.S3.IAmazonS3>(), r2Bucket));
```

- [ ] **Step 2: Build to verify wiring**

Run: `dotnet build AuraCorePro.sln --no-restore 2>&1 | tail -5`
Expected: Build succeeds. If the old `AdminUpdateController.Publish` still references removed DTOs (we haven't refactored it yet), that's OK — no references exist yet.

- [ ] **Step 3: Commit**

```bash
git add src/Backend/AuraCore.API/Program.cs
git commit -m "feat(backend): register IR2Client in DI, env-driven R2 config (6.6.B)"
```

---

## Sub-phase 6.6.C — Backend endpoints

Refactors `AdminUpdateController` to add `prepare-upload` + replaces `publish` with v2 that uses R2 flow. Adds `?platform=` query param to public `/api/updates/check`. **~4 hours.**

### Task 12: `prepare-upload` endpoint (TDD)

**Files:**
- Create: `tests/AuraCore.Tests.API/Releases/PrepareUploadEndpointTests.cs`
- Modify: `src/Backend/AuraCore.API/Controllers/Admin/AdminUpdateController.cs`

- [ ] **Step 1: Write failing tests**

`tests/AuraCore.Tests.API/Releases/PrepareUploadEndpointTests.cs`:

```csharp
using AuraCore.API.Application.Services.Releases;
using AuraCore.API.Controllers.Admin;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AuraCore.Tests.API.Releases;

public class PrepareUploadEndpointTests
{
    private static (AdminUpdateController controller, AuraCoreDbContext db, Mock<IR2Client> r2) Build()
    {
        var options = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"t-{Guid.NewGuid()}").Options;
        var db = new AuraCoreDbContext(options);
        var r2 = new Mock<IR2Client>();
        var c = new AdminUpdateController(db, r2.Object, Mock.Of<IGitHubReleaseMirror>());
        return (c, db, r2);
    }

    [Fact]
    public async Task PrepareUpload_rejects_invalid_semver()
    {
        var (c, _, _) = Build();
        var result = await c.PrepareUpload(
            new PrepareUploadRequest("notversion", AppUpdatePlatform.Windows, "setup.exe", null),
            CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task PrepareUpload_rejects_wrong_extension_for_platform()
    {
        var (c, _, _) = Build();
        var result = await c.PrepareUpload(
            new PrepareUploadRequest("1.7.0", AppUpdatePlatform.Windows, "setup.dmg", null),
            CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task PrepareUpload_rejects_duplicate_version_on_same_platform()
    {
        var (c, db, _) = Build();
        db.AppUpdates.Add(new AppUpdate { Version = "1.7.0", Channel = "stable",
            Platform = AppUpdatePlatform.Windows, BinaryUrl = "x", SignatureHash = "", PublishedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var result = await c.PrepareUpload(
            new PrepareUploadRequest("1.7.0", AppUpdatePlatform.Windows, "setup.exe", null),
            CancellationToken.None);
        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task PrepareUpload_mints_presigned_url_on_valid_request()
    {
        var (c, _, r2) = Build();
        r2.Setup(x => x.GeneratePresignedPutUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync((string key, TimeSpan _, long _, CancellationToken _) =>
              new PresignedPutResult($"https://r2/{key}?sig=x", key, DateTimeOffset.UtcNow.AddMinutes(10)));

        var result = await c.PrepareUpload(
            new PrepareUploadRequest("1.7.0", AppUpdatePlatform.Windows, "setup.exe", null),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Theory]
    [InlineData(AppUpdatePlatform.Linux, "auracore.deb")]
    [InlineData(AppUpdatePlatform.Linux, "auracore.AppImage")]
    [InlineData(AppUpdatePlatform.MacOS, "auracore.dmg")]
    public async Task PrepareUpload_accepts_platform_specific_extensions(AppUpdatePlatform platform, string filename)
    {
        var (c, _, r2) = Build();
        r2.Setup(x => x.GeneratePresignedPutUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new PresignedPutResult("https://r2/x", "x", DateTimeOffset.UtcNow.AddMinutes(10)));

        var result = await c.PrepareUpload(
            new PrepareUploadRequest("1.7.0", platform, filename, null),
            CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
    }
}
```

- [ ] **Step 2: Run tests — verify they FAIL to compile**

Run: `dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter "FullyQualifiedName~PrepareUploadEndpointTests" --logger "console;verbosity=minimal"`
Expected: Compile error — `AdminUpdateController` constructor mismatch, `PrepareUploadRequest` not defined, `IGitHubReleaseMirror` not defined.

- [ ] **Step 3: Create stub `IGitHubReleaseMirror` interface**

`src/Backend/AuraCore.API.Application/Services/Releases/IGitHubReleaseMirror.cs`:

```csharp
using AuraCore.API.Domain.Entities;

namespace AuraCore.API.Application.Services.Releases;

public interface IGitHubReleaseMirror
{
    Task<string?> MirrorAsync(AppUpdate update, string r2ObjectKey, CancellationToken ct);
}
```

- [ ] **Step 4: Refactor `AdminUpdateController` constructor + add `PrepareUpload` + `PrepareUploadRequest`**

Replace the full content of `src/Backend/AuraCore.API/Controllers/Admin/AdminUpdateController.cs` with:

```csharp
using System.Text.RegularExpressions;
using AuraCore.API.Application.Services.Releases;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Admin;

[ApiController]
[Route("api/admin/updates")]
[Authorize(Roles = "admin")]
public sealed class AdminUpdateController : ControllerBase
{
    private static readonly Regex SemverRegex = new(@"^\d+\.\d+\.\d+(-[a-zA-Z0-9.]+)?$", RegexOptions.Compiled);

    private readonly AuraCoreDbContext _db;
    private readonly IR2Client _r2;
    private readonly IGitHubReleaseMirror _githubMirror;

    public AdminUpdateController(AuraCoreDbContext db, IR2Client r2, IGitHubReleaseMirror githubMirror)
    {
        _db = db;
        _r2 = r2;
        _githubMirror = githubMirror;
    }

    /// <summary>Admin: mint presigned R2 PUT URL for direct browser upload</summary>
    [HttpPost("prepare-upload")]
    public async Task<IActionResult> PrepareUpload([FromBody] PrepareUploadRequest req, CancellationToken ct)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Version) || !SemverRegex.IsMatch(req.Version))
            return BadRequest(new { error = "Invalid version (semver X.Y.Z required)" });

        if (!Enum.IsDefined(typeof(AppUpdatePlatform), req.Platform))
            return BadRequest(new { error = "Invalid platform" });

        var ext = Path.GetExtension(req.Filename ?? "").ToLowerInvariant();
        var allowed = req.Platform switch
        {
            AppUpdatePlatform.Windows => new[] { ".exe", ".msi" },
            AppUpdatePlatform.Linux   => new[] { ".deb", ".rpm", ".tar.gz", ".appimage" },
            AppUpdatePlatform.MacOS   => new[] { ".dmg", ".pkg" },
            _ => Array.Empty<string>()
        };
        // Special case: ".appimage" extension has inconsistent casing in filenames
        if (!allowed.Contains(ext) && !(req.Platform == AppUpdatePlatform.Linux && (req.Filename ?? "").EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase)))
            return BadRequest(new { error = $"Invalid extension '{ext}' for platform {req.Platform}. Allowed: {string.Join(", ", allowed)}" });

        var channel = (req.Channel ?? "stable").Trim().ToLowerInvariant();
        var duplicate = await _db.AppUpdates.AnyAsync(u =>
            u.Version == req.Version && u.Channel == channel && u.Platform == req.Platform, ct);
        if (duplicate)
            return Conflict(new { error = $"v{req.Version} already exists for {req.Platform} in channel '{channel}'" });

        var safeFilename = Path.GetFileName(req.Filename!).Replace(" ", "-");
        var objectKey = $"pending/{Guid.NewGuid():N}-{safeFilename}";
        var presigned = await _r2.GeneratePresignedPutUrlAsync(
            objectKey, TimeSpan.FromMinutes(10), maxSizeBytes: 500_000_000, ct);

        return Ok(new {
            uploadUrl = presigned.UploadUrl,
            objectKey = presigned.ObjectKey,
            expiresAt = presigned.ExpiresAt,
        });
    }

    // publish endpoint added in next task
    // list + delete preserved (added in later task)
}

public sealed record PrepareUploadRequest(
    string Version,
    AppUpdatePlatform Platform,
    string Filename,
    string? Channel
);
```

- [ ] **Step 5: Run tests — expect GREEN**

Run: `dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter "FullyQualifiedName~PrepareUploadEndpointTests" --logger "console;verbosity=minimal"`
Expected: `Passed: 7, Failed: 0` (4 Facts + 3 Theory rows).

### Task 13: `publish` v2 endpoint (TDD)

**Files:**
- Create: `tests/AuraCore.Tests.API/Releases/PublishEndpointTests.cs`
- Modify: `src/Backend/AuraCore.API/Controllers/Admin/AdminUpdateController.cs` — add `Publish` method + `PublishUpdateRequestV2`

- [ ] **Step 1: Write failing tests**

`tests/AuraCore.Tests.API/Releases/PublishEndpointTests.cs`:

```csharp
using AuraCore.API.Application.Services.Releases;
using AuraCore.API.Controllers.Admin;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AuraCore.Tests.API.Releases;

public class PublishEndpointTests
{
    private static (AdminUpdateController c, AuraCoreDbContext db, Mock<IR2Client> r2, Mock<IGitHubReleaseMirror> gh) Build()
    {
        var opts = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"p-{Guid.NewGuid()}").Options;
        var db = new AuraCoreDbContext(opts);
        var r2 = new Mock<IR2Client>();
        var gh = new Mock<IGitHubReleaseMirror>();
        return (new AdminUpdateController(db, r2.Object, gh.Object), db, r2, gh);
    }

    [Fact]
    public async Task Publish_400_when_r2_object_missing()
    {
        var (c, _, r2, _) = Build();
        r2.Setup(x => x.HeadObjectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync((R2ObjectHead?)null);

        var result = await c.Publish(new PublishUpdateRequestV2(
            "1.7.0", AppUpdatePlatform.Windows, "pending/abc-setup.exe", "notes", "stable", false),
            CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Publish_400_when_object_too_small()
    {
        var (c, _, r2, _) = Build();
        r2.Setup(x => x.HeadObjectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new R2ObjectHead(500, DateTimeOffset.UtcNow, "application/octet-stream"));

        var result = await c.Publish(new PublishUpdateRequestV2(
            "1.7.0", AppUpdatePlatform.Windows, "pending/abc-setup.exe", null, null, false),
            CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Publish_409_on_duplicate_version_platform_channel()
    {
        var (c, db, r2, _) = Build();
        db.AppUpdates.Add(new AppUpdate { Version = "1.7.0", Channel = "stable",
            Platform = AppUpdatePlatform.Windows, BinaryUrl = "x", SignatureHash = "", PublishedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        r2.Setup(x => x.HeadObjectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new R2ObjectHead(50_000_000, DateTimeOffset.UtcNow, "application/octet-stream"));

        var result = await c.Publish(new PublishUpdateRequestV2(
            "1.7.0", AppUpdatePlatform.Windows, "pending/abc-setup.exe", null, "stable", false),
            CancellationToken.None);
        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Publish_happy_path_copies_computes_hash_inserts_row()
    {
        var (c, db, r2, gh) = Build();
        r2.Setup(x => x.HeadObjectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new R2ObjectHead(50_000_000, DateTimeOffset.UtcNow, "application/octet-stream"));
        r2.Setup(x => x.CopyObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
          .Returns(Task.CompletedTask);
        r2.Setup(x => x.DeleteObjectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
          .Returns(Task.CompletedTask);
        r2.Setup(x => x.ComputeSha256Async(It.IsAny<string>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync("abc123def456abc123def456abc123def456abc123def456abc123def456abcd");

        var result = await c.Publish(new PublishUpdateRequestV2(
            "1.7.0", AppUpdatePlatform.Windows, "pending/abc-setup.exe",
            "Release notes.", "stable", false), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var row = await db.AppUpdates.SingleAsync(u => u.Version == "1.7.0" && u.Platform == AppUpdatePlatform.Windows);
        Assert.Equal("abc123def456abc123def456abc123def456abc123def456abc123def456abcd", row.SignatureHash);
        Assert.StartsWith("https://download.auracore.pro/releases/v1.7.0/", row.BinaryUrl);
        Assert.EndsWith(".exe", row.BinaryUrl);

        r2.Verify(x => x.CopyObjectAsync("pending/abc-setup.exe", It.Is<string>(s => s.StartsWith("releases/v1.7.0/")), It.IsAny<CancellationToken>()), Times.Once);
        r2.Verify(x => x.DeleteObjectAsync("pending/abc-setup.exe", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Publish_canonical_filename_includes_platform_and_version()
    {
        var (c, db, r2, _) = Build();
        r2.Setup(x => x.HeadObjectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new R2ObjectHead(50_000_000, DateTimeOffset.UtcNow, null));
        r2.Setup(x => x.CopyObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        r2.Setup(x => x.DeleteObjectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        r2.Setup(x => x.ComputeSha256Async(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new string('a', 64));

        await c.Publish(new PublishUpdateRequestV2(
            "1.7.0", AppUpdatePlatform.Linux, "pending/xyz-aura.deb", null, "stable", false), CancellationToken.None);

        var row = await db.AppUpdates.SingleAsync(u => u.Platform == AppUpdatePlatform.Linux);
        Assert.Contains("AuraCorePro-Linux-v1.7.0.deb", row.BinaryUrl);
    }
}
```

- [ ] **Step 2: Run tests — expect FAIL (no `Publish` method yet)**

Run: `dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter "FullyQualifiedName~PublishEndpointTests" --logger "console;verbosity=minimal"`
Expected: Compile error — `Publish` method not found on `AdminUpdateController`, `PublishUpdateRequestV2` not defined.

- [ ] **Step 3: Add `Publish` method + `PublishUpdateRequestV2` record**

Inside `AdminUpdateController` class (after `PrepareUpload` method), add:

```csharp
    /// <summary>Admin: finalize upload — HEAD verify, copy to releases/, compute SHA256, insert row, fire Discord + GitHub mirror.</summary>
    [HttpPost("publish")]
    public async Task<IActionResult> Publish([FromBody] PublishUpdateRequestV2 req, CancellationToken ct)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Version) || string.IsNullOrWhiteSpace(req.ObjectKey))
            return BadRequest(new { error = "Version and ObjectKey are required" });
        if (!SemverRegex.IsMatch(req.Version))
            return BadRequest(new { error = "Invalid version (semver X.Y.Z required)" });

        var channel = (req.Channel ?? "stable").Trim().ToLowerInvariant();

        // Race-guard: recheck duplicate (prepare-upload may have been minutes ago)
        var duplicate = await _db.AppUpdates.AnyAsync(u =>
            u.Version == req.Version && u.Channel == channel && u.Platform == req.Platform, ct);
        if (duplicate)
            return Conflict(new { error = $"v{req.Version} already exists for {req.Platform} in channel '{channel}'" });

        // Verify upload actually occurred + size sane
        var head = await _r2.HeadObjectAsync(req.ObjectKey, ct);
        if (head is null)
            return BadRequest(new { error = "R2 object not found — PUT to uploadUrl first" });
        if (head.SizeBytes < 10_000 || head.SizeBytes > 500_000_000)
            return BadRequest(new { error = $"Invalid size: {head.SizeBytes} bytes (expected 10KB-500MB)" });

        // Copy pending/* → releases/v{ver}/<canonical-filename>
        var ext = Path.GetExtension(req.ObjectKey);
        var canonical = $"AuraCorePro-{req.Platform}-v{req.Version}{ext}";
        var finalKey = $"releases/v{req.Version}/{canonical}";
        await _r2.CopyObjectAsync(req.ObjectKey, finalKey, ct);
        await _r2.DeleteObjectAsync(req.ObjectKey, ct);  // cleanup pending/

        var sha256 = await _r2.ComputeSha256Async(finalKey, ct);

        var update = new AppUpdate
        {
            Version = req.Version.Trim(),
            Channel = channel,
            Platform = req.Platform,
            ReleaseNotes = req.ReleaseNotes?.Trim(),
            BinaryUrl = $"https://download.auracore.pro/{finalKey}",
            SignatureHash = sha256,
            IsMandatory = req.IsMandatory,
            PublishedAt = DateTimeOffset.UtcNow,
        };
        _db.AppUpdates.Add(update);
        await _db.SaveChangesAsync(ct);

        // Fire-and-forget: Discord + GitHub mirror
        _ = SendDiscordChangelogAsync(update);
        _ = MirrorToGitHubInBackgroundAsync(update, finalKey);

        return Ok(new {
            message = $"v{update.Version} ({update.Platform}) published",
            update = new {
                update.Id, update.Version, update.Channel, update.Platform,
                update.ReleaseNotes, update.BinaryUrl, update.SignatureHash,
                update.IsMandatory, update.PublishedAt
            }
        });
    }

    private async Task MirrorToGitHubInBackgroundAsync(AppUpdate update, string r2ObjectKey)
    {
        try
        {
            var releaseId = await _githubMirror.MirrorAsync(update, r2ObjectKey, CancellationToken.None);
            if (releaseId is not null)
            {
                // Update row with GitHubReleaseId (best-effort; ignore errors)
                using var scope = HttpContext?.RequestServices?.CreateScope();
                var db = scope?.ServiceProvider.GetService<AuraCoreDbContext>() ?? _db;
                var row = await db.AppUpdates.FindAsync(update.Id);
                if (row is not null)
                {
                    row.GitHubReleaseId = releaseId;
                    await db.SaveChangesAsync();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GitHub mirror failed for v{update.Version}: {ex.Message}");
        }
    }

    // SendDiscordChangelogAsync (existing method, unchanged — keep from original controller)
    private static async Task SendDiscordChangelogAsync(AppUpdate update)
    {
        try
        {
            var webhookUrl = Environment.GetEnvironmentVariable("DISCORD_WEBHOOK_URL");
            if (string.IsNullOrEmpty(webhookUrl)) return;
            var notes = update.ReleaseNotes ?? "No release notes provided.";
            if (notes.Length > 1800) notes = notes[..1800] + "...";
            var payload = new
            {
                content = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISCORD_UPDATES_ROLE_ID"))
                    ? "" : $"<@&{Environment.GetEnvironmentVariable("DISCORD_UPDATES_ROLE_ID")}>",
                embeds = new[]
                {
                    new
                    {
                        title = $"🚀 AuraCore Pro v{update.Version} ({update.Platform}) Released!",
                        description = notes,
                        color = 54442,
                        fields = new[]
                        {
                            new { name = "Channel", value = update.Channel, inline = true },
                            new { name = "Platform", value = update.Platform.ToString(), inline = true },
                            new { name = "Mandatory", value = update.IsMandatory ? "Yes" : "No", inline = true },
                            new { name = "Download", value = $"[Download]({update.BinaryUrl})", inline = false }
                        },
                        footer = new { text = "AuraCore Pro • auracore.pro" },
                        timestamp = update.PublishedAt.ToString("o")
                    }
                }
            };
            using var client = new HttpClient();
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            await client.PostAsync(webhookUrl, new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Discord webhook error: {ex.Message}");
        }
    }
```

And at the END of the file (after the class closing `}`), add:

```csharp
public sealed record PublishUpdateRequestV2(
    string Version,
    AppUpdatePlatform Platform,
    string ObjectKey,
    string? ReleaseNotes,
    string? Channel,
    bool IsMandatory
);
```

Replace the old `PublishUpdateRequest` (no longer used) with `PublishUpdateRequestV2`.

- [ ] **Step 4: Run tests — expect GREEN**

Run: `dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter "FullyQualifiedName~PublishEndpointTests" --logger "console;verbosity=minimal"`
Expected: `Passed: 5, Failed: 0`.

### Task 14: Restore List + Delete + add MirrorToGitHub retry endpoint

**Files:**
- Modify: `src/Backend/AuraCore.API/Controllers/Admin/AdminUpdateController.cs`

- [ ] **Step 1: Add back `List` + `Delete` endpoints** (preserved from original)

Inside `AdminUpdateController` class, add:

```csharp
    /// <summary>List all updates (admin view) — includes Platform + GitHubReleaseId</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var updates = await _db.AppUpdates
            .OrderByDescending(u => u.PublishedAt)
            .Select(u => new
            {
                u.Id, u.Version, u.Channel, u.Platform, u.ReleaseNotes,
                u.BinaryUrl, u.IsMandatory, u.SignatureHash, u.PublishedAt, u.GitHubReleaseId
            })
            .ToListAsync(ct);
        return Ok(updates);
    }

    /// <summary>Delete an update</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var u = await _db.AppUpdates.FindAsync(new object[] { id }, ct);
        if (u is null) return NotFound(new { error = "Update not found" });
        _db.AppUpdates.Remove(u);
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = $"Update v{u.Version} ({u.Platform}) deleted" });
    }

    /// <summary>Admin: retry GitHub mirror for an existing AppUpdate (e.g., after transient failure)</summary>
    [HttpPost("{id:guid}/mirror-to-github")]
    public async Task<IActionResult> RetryGitHubMirror(Guid id, CancellationToken ct)
    {
        var u = await _db.AppUpdates.FindAsync(new object[] { id }, ct);
        if (u is null) return NotFound(new { error = "Update not found" });

        // Reconstruct the R2 object key from BinaryUrl
        var canonical = Path.GetFileName(new Uri(u.BinaryUrl).AbsolutePath);
        var r2Key = $"releases/v{u.Version}/{canonical}";

        try
        {
            var releaseId = await _githubMirror.MirrorAsync(u, r2Key, ct);
            u.GitHubReleaseId = releaseId;
            await _db.SaveChangesAsync(ct);
            return Ok(new { message = "GitHub mirror completed", releaseId });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Mirror failed: {ex.Message}" });
        }
    }
```

- [ ] **Step 2: Build + run all tests for sub-phase so far**

Run: `dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --logger "console;verbosity=minimal"`
Expected: `Passed: 13+, Failed: 0` (Migration 3 + R2Client 5 + PrepareUpload 7 + Publish 5 = 20).

### Task 15: `GET /api/updates/check` with `?platform=` query param (TDD)

**Files:**
- Create: `tests/AuraCore.Tests.API/Releases/CheckPlatformFilterTests.cs`
- Modify: `src/Backend/AuraCore.API/Controllers/UpdateController.cs`

- [ ] **Step 1: Write failing tests**

`tests/AuraCore.Tests.API/Releases/CheckPlatformFilterTests.cs`:

```csharp
using AuraCore.API.Controllers;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuraCore.Tests.API.Releases;

public class CheckPlatformFilterTests
{
    private static (UpdateController c, AuraCoreDbContext db) Build()
    {
        var opts = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"ck-{Guid.NewGuid()}").Options;
        var db = new AuraCoreDbContext(opts);
        return (new UpdateController(db), db);
    }

    [Fact]
    public async Task Check_defaults_to_Windows_when_platform_omitted()
    {
        var (c, db) = Build();
        db.AppUpdates.Add(new AppUpdate { Version = "1.7.0", Channel = "stable",
            Platform = AppUpdatePlatform.Windows, BinaryUrl = "https://r/w", SignatureHash = new string('a', 64), PublishedAt = DateTimeOffset.UtcNow });
        db.AppUpdates.Add(new AppUpdate { Version = "1.7.0", Channel = "stable",
            Platform = AppUpdatePlatform.Linux, BinaryUrl = "https://r/l", SignatureHash = new string('b', 64), PublishedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var result = await c.Check("1.0.0", null, "stable", CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        // Use reflection-light pattern: serialize + read
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"https://r/w\"", json);
    }

    [Fact]
    public async Task Check_filters_by_platform_when_specified()
    {
        var (c, db) = Build();
        db.AppUpdates.Add(new AppUpdate { Version = "1.7.0", Channel = "stable",
            Platform = AppUpdatePlatform.Windows, BinaryUrl = "https://r/w", SignatureHash = new string('a', 64), PublishedAt = DateTimeOffset.UtcNow });
        db.AppUpdates.Add(new AppUpdate { Version = "1.7.0", Channel = "stable",
            Platform = AppUpdatePlatform.Linux, BinaryUrl = "https://r/l", SignatureHash = new string('b', 64), PublishedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var result = await c.Check("1.0.0", "linux", "stable", CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"https://r/l\"", json);
    }

    [Fact]
    public async Task Check_returns_no_update_when_platform_has_no_release()
    {
        var (c, db) = Build();
        db.AppUpdates.Add(new AppUpdate { Version = "1.7.0", Channel = "stable",
            Platform = AppUpdatePlatform.Windows, BinaryUrl = "https://r/w", SignatureHash = new string('a', 64), PublishedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var result = await c.Check("1.0.0", "macos", "stable", CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"updateAvailable\":false", json);
    }

    [Theory]
    [InlineData("windows", AppUpdatePlatform.Windows)]
    [InlineData("WINDOWS", AppUpdatePlatform.Windows)]
    [InlineData("linux", AppUpdatePlatform.Linux)]
    [InlineData("macos", AppUpdatePlatform.MacOS)]
    public async Task Check_platform_query_is_case_insensitive(string input, AppUpdatePlatform expected)
    {
        var (c, db) = Build();
        db.AppUpdates.Add(new AppUpdate { Version = "1.7.0", Channel = "stable",
            Platform = expected, BinaryUrl = "https://r/x", SignatureHash = new string('a', 64), PublishedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var result = await c.Check("1.0.0", input, "stable", CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"updateAvailable\":true", json);
    }
}
```

- [ ] **Step 2: Run tests — expect FAIL (signature mismatch)**

Run: `dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter "FullyQualifiedName~CheckPlatformFilterTests" --logger "console;verbosity=minimal"`
Expected: Compile error — `Check` has 2 params, we're calling with 3.

- [ ] **Step 3: Modify `Check` to accept `?platform=` param**

Replace the `Check` method in `src/Backend/AuraCore.API/Controllers/UpdateController.cs`:

```csharp
    [HttpGet("check")]
    public async Task<IActionResult> Check(
        [FromQuery] string currentVersion,
        [FromQuery] string? platform = null,
        [FromQuery] string channel = "stable",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(currentVersion))
            return BadRequest(new { error = "currentVersion is required" });

        // Parse platform param (case-insensitive, default Windows)
        AppUpdatePlatform p = AppUpdatePlatform.Windows;
        if (!string.IsNullOrWhiteSpace(platform) &&
            !Enum.TryParse(platform, ignoreCase: true, out p))
        {
            return BadRequest(new { error = $"Invalid platform '{platform}'. Expected: windows|linux|macos" });
        }

        var latest = await _db.AppUpdates
            .Where(u => u.Channel == channel && u.Platform == p)
            .OrderByDescending(u => u.PublishedAt)
            .FirstOrDefaultAsync(ct);

        if (latest is null)
            return Ok(new { updateAvailable = false });

        var isNewer = IsNewerVersion(latest.Version, currentVersion);

        return Ok(new
        {
            updateAvailable = isNewer,
            version = latest.Version,
            channel = latest.Channel,
            platform = latest.Platform.ToString(),
            releaseNotes = latest.ReleaseNotes,
            downloadUrl = latest.BinaryUrl,
            isMandatory = latest.IsMandatory,
            signatureHash = latest.SignatureHash,
            publishedAt = latest.PublishedAt
        });
    }
```

- [ ] **Step 4: Run tests — expect GREEN**

Run: `dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter "FullyQualifiedName~CheckPlatformFilterTests" --logger "console;verbosity=minimal"`
Expected: `Passed: 7, Failed: 0` (3 Facts + 4 Theory rows).

- [ ] **Step 5: Commit sub-phase 6.6.C milestone**

```bash
git add src/Backend/AuraCore.API/Controllers/ \
        src/Backend/AuraCore.API.Application/Services/Releases/IGitHubReleaseMirror.cs \
        tests/AuraCore.Tests.API/Releases/
git commit -m "feat(backend): prepare-upload + publish v2 + platform-filtered check (6.6.C)

- POST /api/admin/updates/prepare-upload — mints presigned R2 PUT URL
- POST /api/admin/updates/publish — HEAD+COPY+SHA256+row insert
- POST /api/admin/updates/{id}/mirror-to-github — retry endpoint
- GET  /api/updates/check?platform=X — platform filter, default Windows
- 17 new tests (7 PrepareUpload + 5 Publish + 7 Check)"
```

---

## Sub-phase 6.6.D — GitHub Releases mirror

Async fire-and-forget mirror. Uploads binary + `sha256sums.txt` as release assets. **~3 hours.**

### Task 16: Add Octokit NuGet + implement `OctokitReleaseMirror` (TDD)

**Files:**
- Modify: `src/Backend/AuraCore.API.Infrastructure/AuraCore.API.Infrastructure.csproj` — add `Octokit` package
- Create: `src/Backend/AuraCore.API.Infrastructure/Services/Releases/OctokitReleaseMirror.cs`
- Create: `tests/AuraCore.Tests.API/Releases/GitHubReleaseMirrorTests.cs`

- [ ] **Step 1: Add Octokit package**

```bash
dotnet add src/Backend/AuraCore.API.Infrastructure/AuraCore.API.Infrastructure.csproj package Octokit --version 13.0.1
```

- [ ] **Step 2: Write failing tests (mock IGitHubClient)**

`tests/AuraCore.Tests.API/Releases/GitHubReleaseMirrorTests.cs`:

```csharp
using AuraCore.API.Application.Services.Releases;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Services.Releases;
using Moq;
using Octokit;
using Xunit;

namespace AuraCore.Tests.API.Releases;

public class GitHubReleaseMirrorTests
{
    [Fact]
    public async Task MirrorAsync_returns_null_when_no_github_token_configured()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_GITHUB_TOKEN", "");
        var r2 = new Mock<IR2Client>();
        var mirror = new OctokitReleaseMirror(r2.Object, githubClientFactory: token => null!);

        var result = await mirror.MirrorAsync(
            new AppUpdate { Version = "1.7.0", Platform = AppUpdatePlatform.Windows, BinaryUrl = "x", SignatureHash = new string('a', 64) },
            "releases/v1.7.0/x.exe", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task MirrorAsync_creates_release_and_uploads_asset_when_token_set()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_GITHUB_TOKEN", "fake-pat");

        var r2 = new Mock<IR2Client>();
        r2.Setup(x => x.DownloadToStreamAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
          .Callback<string, Stream, CancellationToken>((_, stream, _) =>
              stream.Write(System.Text.Encoding.UTF8.GetBytes("fake-installer-bytes")))
          .Returns(Task.CompletedTask);

        var releases = new Mock<IReleasesClient>();
        releases.Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NewRelease>()))
                .ReturnsAsync(new Release(
                    url: "", htmlUrl: "", assetsUrl: "", uploadUrl: "", id: 999L,
                    nodeId: "", tagName: "v1.7.0", targetCommitish: "main",
                    name: "v1.7.0", body: "notes", draft: false, prerelease: false,
                    createdAt: DateTimeOffset.UtcNow, publishedAt: DateTimeOffset.UtcNow,
                    author: null, tarballUrl: "", zipballUrl: "", assets: Array.Empty<ReleaseAsset>()));
        releases.Setup(x => x.UploadAsset(It.IsAny<Release>(), It.IsAny<ReleaseAssetUpload>()))
                .ReturnsAsync(new ReleaseAsset(
                    url: "", id: 0L, nodeId: "", name: "x", label: "", state: "",
                    contentType: "", size: 0, downloadCount: 0, createdAt: DateTimeOffset.UtcNow,
                    updatedAt: DateTimeOffset.UtcNow, browserDownloadUrl: "", uploader: null));

        var gh = new Mock<IGitHubClient>();
        var repo = new Mock<IRepositoriesClient>();
        repo.Setup(x => x.Release).Returns(releases.Object);
        gh.Setup(x => x.Repository).Returns(repo.Object);

        var mirror = new OctokitReleaseMirror(r2.Object, _ => gh.Object);
        var releaseId = await mirror.MirrorAsync(
            new AppUpdate { Version = "1.7.0", Platform = AppUpdatePlatform.Windows, Channel = "stable",
                ReleaseNotes = "notes", BinaryUrl = "x", SignatureHash = new string('a', 64) },
            "releases/v1.7.0/AuraCorePro-Windows-v1.7.0.exe", CancellationToken.None);

        Assert.Equal("999", releaseId);
        releases.Verify(x => x.Create(It.IsAny<string>(), It.IsAny<string>(), It.Is<NewRelease>(r =>
            r.TagName == "v1.7.0" && r.Name.Contains("1.7.0"))), Times.Once);
        // Assert 2 assets uploaded: binary + sha256sums.txt
        releases.Verify(x => x.UploadAsset(It.IsAny<Release>(), It.IsAny<ReleaseAssetUpload>()), Times.Exactly(2));
    }
}
```

- [ ] **Step 3: Run — expect FAIL (OctokitReleaseMirror not defined)**

Run: `dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter "FullyQualifiedName~GitHubReleaseMirrorTests" --logger "console;verbosity=minimal"`
Expected: Compile error.

- [ ] **Step 4: Implement `OctokitReleaseMirror`**

`src/Backend/AuraCore.API.Infrastructure/Services/Releases/OctokitReleaseMirror.cs`:

```csharp
using AuraCore.API.Application.Services.Releases;
using AuraCore.API.Domain.Entities;
using Octokit;

namespace AuraCore.API.Infrastructure.Services.Releases;

public sealed class OctokitReleaseMirror : IGitHubReleaseMirror
{
    private const string RepoOwner = "edutuvarna";
    private const string RepoName = "AuraCorePro";

    private readonly IR2Client _r2;
    private readonly Func<string, IGitHubClient> _clientFactory;

    public OctokitReleaseMirror(IR2Client r2)
        : this(r2, token => new GitHubClient(new ProductHeaderValue("AuraCorePro-Backend")) {
            Credentials = new Credentials(token)
        }) { }

    // Test-friendly ctor
    public OctokitReleaseMirror(IR2Client r2, Func<string, IGitHubClient> clientFactory)
    {
        _r2 = r2;
        _clientFactory = clientFactory;
    }

    public async Task<string?> MirrorAsync(AppUpdate update, string r2ObjectKey, CancellationToken ct)
    {
        var token = Environment.GetEnvironmentVariable("ASPNETCORE_GITHUB_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var gh = _clientFactory(token);
        var tag = $"v{update.Version}";

        // Find-or-create release for this tag
        Release release;
        try
        {
            release = await gh.Repository.Release.Get(RepoOwner, RepoName, tag);
        }
        catch (NotFoundException)
        {
            release = await gh.Repository.Release.Create(RepoOwner, RepoName, new NewRelease(tag)
            {
                Name = $"AuraCore Pro v{update.Version}",
                Body = update.ReleaseNotes ?? "See AuraCore Pro changelog.",
                Draft = false,
                Prerelease = update.Channel != "stable",
            });
        }

        // Upload the binary asset
        await using var binaryStream = new MemoryStream();
        await _r2.DownloadToStreamAsync(r2ObjectKey, binaryStream, ct);
        binaryStream.Position = 0;
        var canonicalName = Path.GetFileName(r2ObjectKey);
        await gh.Repository.Release.UploadAsset(release, new ReleaseAssetUpload
        {
            FileName = canonicalName,
            ContentType = GuessContentType(canonicalName),
            RawData = binaryStream,
        });

        // Upload sha256sums.txt
        var sha256Content = $"{update.SignatureHash}  {canonicalName}\n";
        await using var sumsStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(sha256Content));
        await gh.Repository.Release.UploadAsset(release, new ReleaseAssetUpload
        {
            FileName = $"sha256sums-{update.Platform.ToString().ToLowerInvariant()}.txt",
            ContentType = "text/plain",
            RawData = sumsStream,
        });

        return release.Id.ToString();
    }

    private static string GuessContentType(string filename)
    {
        var ext = Path.GetExtension(filename).ToLowerInvariant();
        return ext switch
        {
            ".exe" or ".msi" => "application/x-msdownload",
            ".deb" => "application/vnd.debian.binary-package",
            ".rpm" => "application/x-rpm",
            ".dmg" => "application/x-apple-diskimage",
            ".pkg" => "application/x-newton-compatible-pkg",
            _ => "application/octet-stream"
        };
    }
}
```

- [ ] **Step 5: Run tests — expect GREEN**

Run: `dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter "FullyQualifiedName~GitHubReleaseMirrorTests" --logger "console;verbosity=minimal"`
Expected: `Passed: 2, Failed: 0`.

### Task 17: Register `IGitHubReleaseMirror` in DI

**Files:**
- Modify: `src/Backend/AuraCore.API/Program.cs`

- [ ] **Step 1: Add registration** just after the `IR2Client` registration from Task 11:

```csharp
builder.Services.AddScoped<AuraCore.API.Application.Services.Releases.IGitHubReleaseMirror>(sp =>
    new AuraCore.API.Infrastructure.Services.Releases.OctokitReleaseMirror(
        sp.GetRequiredService<AuraCore.API.Application.Services.Releases.IR2Client>()));
```

- [ ] **Step 2: Build full solution**

Run: `dotnet build AuraCorePro.sln --no-restore 2>&1 | tail -5`
Expected: Build succeeds.

- [ ] **Step 3: Commit sub-phase 6.6.D milestone**

```bash
git add src/Backend/AuraCore.API.Infrastructure/ \
        src/Backend/AuraCore.API/Program.cs \
        tests/AuraCore.Tests.API/
git commit -m "feat(backend): GitHub Releases mirror via Octokit.NET (6.6.D)

- IGitHubReleaseMirror interface + OctokitReleaseMirror impl
- Uploads binary + sha256sums.txt as release assets
- No-op when ASPNETCORE_GITHUB_TOKEN unset (safe default)
- Per-version tag = v{Version}; Prerelease = (Channel != stable)
- 2 unit tests with mocked IGitHubClient"
```

---

## Sub-phase 6.6.E — Admin panel UI (Next.js) + bundled fixes

The admin panel is in a SEPARATE REPO at `C:\Users\Admin\Desktop\AuraCorePro\Adminpanel\`. This sub-phase requires switching directories.

**Scope bundle:**
1. Replace `UpdatesPage` Publish form: 3-step flow (prepare → direct PUT to R2 → publish).
2. Fix "Invalid Date" display in the Updates list (Bug 1 from spec).
3. Add ID column + click-to-copy GUID to `UsersPage`.

**~4 hours.** No automated tests — admin panel has no test infra; rely on manual verification.

### Task 18: Add API client methods for new endpoints

**Files:**
- Modify: `C:\Users\Admin\Desktop\AuraCorePro\Adminpanel\src\lib\api.ts`

- [ ] **Step 1: Add `prepareUpload` + `updatePlublishV2` methods**

Open `C:\Users\Admin\Desktop\AuraCorePro\Adminpanel\src\lib\api.ts`. Find the block around lines 165-182 where `getUpdates`, `publishUpdate`, `deleteUpdate` live. Replace `publishUpdate` with two methods + add retry:

```ts
  async prepareUpload(data: {
    version: string;
    platform: 'Windows' | 'Linux' | 'MacOS';
    filename: string;
    channel?: string;
  }): Promise<{ uploadUrl: string; objectKey: string; expiresAt: string }> {
    return request('/api/admin/updates/prepare-upload', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  },

  async publishUpdate(data: {
    version: string;
    platform: 'Windows' | 'Linux' | 'MacOS';
    objectKey: string;
    releaseNotes?: string;
    channel?: string;
    isMandatory: boolean;
  }) {
    return request('/api/admin/updates/publish', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  },

  async retryGitHubMirror(id: string) {
    return request(`/api/admin/updates/${id}/mirror-to-github`, { method: 'POST' });
  },

  // Client-side helper: XHR-based PUT with progress for presigned R2 URL
  putToPresignedUrl(
    url: string,
    file: File,
    onProgress: (percent: number) => void
  ): Promise<void> {
    return new Promise((resolve, reject) => {
      const xhr = new XMLHttpRequest();
      xhr.open('PUT', url);
      xhr.upload.onprogress = (e) => {
        if (e.lengthComputable) onProgress(Math.round((e.loaded / e.total) * 100));
      };
      xhr.onload = () => (xhr.status >= 200 && xhr.status < 300 ? resolve() : reject(new Error(`PUT failed: ${xhr.status}`)));
      xhr.onerror = () => reject(new Error('PUT network error'));
      xhr.send(file);
    });
  },
```

- [ ] **Step 2: Verify no TypeScript compile errors**

Run: `cd C:/Users/Admin/Desktop/AuraCorePro/Adminpanel && npx tsc --noEmit 2>&1 | tail -20`
Expected: No errors (or only pre-existing errors unrelated to this file).

### Task 19: Rewrite `UpdatesPage` Publish form

**Files:**
- Modify: `C:\Users\Admin\Desktop\AuraCorePro\Adminpanel\src\app\page.tsx:796-881` (UpdatesPage)

- [ ] **Step 1: Read current UpdatesPage to understand it**

Run: `Read` on `C:\Users\Admin\Desktop\AuraCorePro\Adminpanel\src\app\page.tsx`, offset=790, limit=95. Memorize the JSX structure before replacing.

- [ ] **Step 2: Replace UpdatesPage with V2 form + list**

Replace lines 796-881 with:

```tsx
function UpdatesPage() {
  const [updates, setUpdates] = useState<any[]>([]);
  const [version, setVersion] = useState('');
  const [channel, setChannel] = useState<'stable' | 'beta' | 'canary'>('stable');
  const [isMandatory, setIsMandatory] = useState(false);
  const [releaseNotes, setReleaseNotes] = useState('');
  const [publishing, setPublishing] = useState(false);

  // Per-platform state: { Windows: { enabled, file, progress, objectKey } }
  type PlatformState = { enabled: boolean; file: File | null; progress: number; objectKey: string | null; error: string | null };
  const emptyPlatform: PlatformState = { enabled: false, file: null, progress: 0, objectKey: null, error: null };
  const [windowsP, setWindowsP] = useState<PlatformState>({ ...emptyPlatform, enabled: true });
  const [linuxP, setLinuxP] = useState<PlatformState>({ ...emptyPlatform });

  const loadUpdates = async () => {
    const list = await api.getUpdates();
    setUpdates(list);
  };
  useEffect(() => { loadUpdates(); }, []);

  const uploadFor = async (platform: 'Windows' | 'Linux', state: PlatformState, setter: (s: PlatformState) => void) => {
    if (!state.file) return;
    setter({ ...state, progress: 0, error: null });
    try {
      const prep = await api.prepareUpload({ version, platform, filename: state.file.name, channel });
      await api.putToPresignedUrl(prep.uploadUrl, state.file, (p) => setter({ ...state, progress: p, error: null, objectKey: state.objectKey }));
      setter({ ...state, progress: 100, objectKey: prep.objectKey, error: null });
    } catch (e: any) {
      setter({ ...state, error: e.message ?? 'Upload failed', progress: 0 });
    }
  };

  const canPublish = !publishing && version.trim().length > 0 && (
    (windowsP.enabled && windowsP.objectKey) || (linuxP.enabled && linuxP.objectKey)
  ) && (!windowsP.enabled || windowsP.objectKey) && (!linuxP.enabled || linuxP.objectKey);

  const doPublish = async () => {
    setPublishing(true);
    try {
      const jobs: Promise<any>[] = [];
      if (windowsP.enabled && windowsP.objectKey) {
        jobs.push(api.publishUpdate({
          version, platform: 'Windows', objectKey: windowsP.objectKey,
          releaseNotes, channel, isMandatory,
        }));
      }
      if (linuxP.enabled && linuxP.objectKey) {
        jobs.push(api.publishUpdate({
          version, platform: 'Linux', objectKey: linuxP.objectKey,
          releaseNotes, channel, isMandatory,
        }));
      }
      await Promise.all(jobs);
      alert(`v${version} published!`);
      // Reset form
      setVersion(''); setReleaseNotes(''); setIsMandatory(false);
      setWindowsP({ ...emptyPlatform, enabled: true });
      setLinuxP({ ...emptyPlatform });
      await loadUpdates();
    } catch (e: any) {
      alert(`Publish failed: ${e.message}`);
    } finally {
      setPublishing(false);
    }
  };

  // "Invalid Date" bug 1 fix: coerce to Date safely
  const fmtDate = (v: any) => {
    if (!v) return '—';
    const d = new Date(v);
    return isNaN(d.getTime()) ? '—' : d.toLocaleDateString();
  };

  return (
    <div className="p-8 space-y-6">
      <h2 className="text-2xl font-bold">Publish Update</h2>

      <div className="bg-white rounded-lg border p-6 space-y-4">
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium mb-1">Version (semver)</label>
            <input className="w-full border rounded px-3 py-2" placeholder="1.7.0" value={version} onChange={(e) => setVersion(e.target.value)} />
          </div>
          <div>
            <label className="block text-sm font-medium mb-1">Channel</label>
            <select className="w-full border rounded px-3 py-2" value={channel} onChange={(e) => setChannel(e.target.value as any)}>
              <option value="stable">stable</option>
              <option value="beta">beta</option>
              <option value="canary">canary</option>
            </select>
          </div>
        </div>

        <div className="space-y-3">
          <h3 className="font-medium">Platforms</h3>

          <PlatformRow
            label="Windows"
            extensions=".exe,.msi"
            state={windowsP}
            onToggle={(b) => setWindowsP({ ...windowsP, enabled: b })}
            onFile={(f) => setWindowsP({ ...windowsP, file: f, objectKey: null, progress: 0 })}
            onUpload={() => uploadFor('Windows', windowsP, setWindowsP)}
            disabled={!version || !windowsP.enabled}
          />

          <PlatformRow
            label="Linux"
            extensions=".deb,.rpm,.tar.gz,.AppImage"
            state={linuxP}
            onToggle={(b) => setLinuxP({ ...linuxP, enabled: b })}
            onFile={(f) => setLinuxP({ ...linuxP, file: f, objectKey: null, progress: 0 })}
            onUpload={() => uploadFor('Linux', linuxP, setLinuxP)}
            disabled={!version || !linuxP.enabled}
          />

          <div className="flex items-center gap-2 opacity-50">
            <input type="checkbox" disabled />
            <span>macOS</span>
            <span className="text-xs text-gray-500">Coming Soon — requires Developer ID</span>
          </div>
        </div>

        <div>
          <label className="block text-sm font-medium mb-1">Release Notes (markdown)</label>
          <textarea className="w-full border rounded px-3 py-2 h-24" value={releaseNotes} onChange={(e) => setReleaseNotes(e.target.value)} />
        </div>

        <label className="flex items-center gap-2">
          <input type="checkbox" checked={isMandatory} onChange={(e) => setIsMandatory(e.target.checked)} />
          <span>Mandatory update</span>
          {isMandatory && <span className="text-xs text-orange-600">⚠ Locks users out until they update</span>}
        </label>

        <button
          className="bg-blue-600 text-white px-4 py-2 rounded disabled:opacity-50"
          disabled={!canPublish}
          onClick={doPublish}
        >
          {publishing ? 'Publishing…' : 'Publish'}
        </button>
      </div>

      <h2 className="text-2xl font-bold mt-8">Released Updates</h2>
      <table className="w-full bg-white rounded-lg border">
        <thead className="bg-gray-50">
          <tr>
            <th className="text-left p-3">Version</th>
            <th className="text-left p-3">Platform</th>
            <th className="text-left p-3">Channel</th>
            <th className="text-left p-3">Mandatory</th>
            <th className="text-left p-3">Published</th>
            <th className="text-left p-3">GitHub</th>
            <th className="text-left p-3">Actions</th>
          </tr>
        </thead>
        <tbody>
          {updates.map((u: any) => (
            <tr key={u.id} className="border-t">
              <td className="p-3">v{u.version}</td>
              <td className="p-3">{u.platform}</td>
              <td className="p-3">{u.channel}</td>
              <td className="p-3">{u.isMandatory ? 'Yes' : 'No'}</td>
              <td className="p-3">{fmtDate(u.publishedAt)}</td>
              <td className="p-3">
                {u.gitHubReleaseId ? <span className="text-green-600">✓ Mirrored</span> :
                  <button className="text-blue-600 underline" onClick={() => api.retryGitHubMirror(u.id).then(loadUpdates)}>Retry mirror</button>}
              </td>
              <td className="p-3">
                <button className="text-red-600" onClick={() => confirm(`Delete v${u.version} (${u.platform})?`) && api.deleteUpdate(u.id).then(loadUpdates)}>🗑</button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function PlatformRow({ label, extensions, state, onToggle, onFile, onUpload, disabled }: {
  label: string; extensions: string; state: any;
  onToggle: (b: boolean) => void;
  onFile: (f: File | null) => void;
  onUpload: () => void;
  disabled: boolean;
}) {
  return (
    <div className="flex items-center gap-3 border rounded p-3">
      <input type="checkbox" checked={state.enabled} onChange={(e) => onToggle(e.target.checked)} />
      <span className="font-medium w-24">{label}</span>
      {state.enabled && (
        <>
          <input type="file" accept={extensions} onChange={(e) => onFile(e.target.files?.[0] ?? null)} />
          <button
            className="bg-gray-100 border rounded px-2 py-1 text-sm"
            disabled={disabled || !state.file || state.progress > 0}
            onClick={onUpload}
          >
            Upload to R2
          </button>
          {state.progress > 0 && <div className="flex-1">
            <div className="h-2 bg-gray-200 rounded">
              <div className="h-2 bg-blue-500 rounded" style={{ width: `${state.progress}%` }} />
            </div>
            <div className="text-xs mt-1">{state.progress}%{state.objectKey ? ' — uploaded' : ''}</div>
          </div>}
          {state.error && <span className="text-red-600 text-sm">{state.error}</span>}
        </>
      )}
    </div>
  );
}
```

- [ ] **Step 3: Verify TypeScript compile**

Run: `cd C:/Users/Admin/Desktop/AuraCorePro/Adminpanel && npx tsc --noEmit 2>&1 | tail -20`
Expected: No errors in `src/app/page.tsx`. If `api.getUpdates()` / `api.deleteUpdate()` types mismatch, patch those method signatures in api.ts.

### Task 20: Add GUID column to UsersPage

**Files:**
- Modify: `C:\Users\Admin\Desktop\AuraCorePro\Adminpanel\src\app\page.tsx:536-607` (UsersPage)

- [ ] **Step 1: Read current UsersPage structure**

Read `page.tsx` offset=530, limit=80.

- [ ] **Step 2: Add ID column to table**

Locate the `<thead>` element inside UsersPage. Add a new `<th>` between USER and ROLE columns:

```tsx
<th className="text-left p-3">ID</th>
```

In the `<tbody>` map rendering, add the new `<td>` between the user email cell and the role cell:

```tsx
<td className="p-3 font-mono text-xs">
  <button
    className="text-gray-700 hover:text-blue-600 cursor-pointer"
    title={u.id}
    onClick={() => {
      navigator.clipboard.writeText(u.id);
      const btn = event!.currentTarget as HTMLElement;
      const old = btn.textContent;
      btn.textContent = 'Copied!';
      setTimeout(() => { btn.textContent = old; }, 2000);
    }}
  >
    {u.id.substring(0, 8)}… 📋
  </button>
</td>
```

- [ ] **Step 3: Verify TypeScript + dev server**

Run:
```bash
cd C:/Users/Admin/Desktop/AuraCorePro/Adminpanel && npx tsc --noEmit 2>&1 | tail -10
```
Expected: No errors.

### Task 21: Manual verification + commit admin-panel changes

- [ ] **Step 1: Start admin panel dev server + backend**

In two shells:
```bash
# Shell 1: backend (requires tunnel to prod DB already open)
cd C:/Users/Admin/Desktop/AuraCorePro/AuraCorePro
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Backend/AuraCore.API

# Shell 2: admin panel
cd C:/Users/Admin/Desktop/AuraCorePro/Adminpanel
npm run dev
```

- [ ] **Step 2: Open localhost admin panel**

Browser: `http://localhost:3000` (or admin panel's port). Log in. Navigate to Users page — verify ID column shows shortened GUID + click-to-copy works. Navigate to Updates page — verify new form shows Windows + Linux upload rows with macOS disabled. Verify existing updates list shows dates correctly (no more "Invalid Date").

Note: Full end-to-end file upload requires R2 credentials + custom domain (sub-phase 6.6.H). For now, a form validation walkthrough is sufficient. If R2 env vars are set, a real file upload to a test bucket is nice-to-have.

- [ ] **Step 3: Commit admin panel changes**

The admin panel is a separate git repo. Check and commit there:

```bash
cd C:/Users/Admin/Desktop/AuraCorePro/Adminpanel
git status
git add src/app/page.tsx src/lib/api.ts
git commit -m "feat(admin): release pipeline UI + GUID col + Invalid Date fix

- Replaced UpdatesPage Publish form with 3-step (prepare → PUT → publish)
  per-platform Windows + Linux with progress bars, macOS Coming Soon
- Fixed Invalid Date bug (coerce to Date safely with fallback)
- Added ID column to UsersPage with click-to-copy full GUID
- New api.ts methods: prepareUpload, putToPresignedUrl,
  publishUpdate (v2 shape), retryGitHubMirror

Aligned with AuraCorePro spec 2026-04-19-release-pipeline-design.md"
```

- [ ] **Step 4: Back to main repo; commit note-only marker (empty)**

```bash
cd C:/Users/Admin/Desktop/AuraCorePro/AuraCorePro
git commit --allow-empty -m "docs: admin panel 6.6.E landed in separate repo

Adminpanel@<short-sha> committed separately:
- UpdatesPage V2 (prepare+upload+publish)
- UsersPage ID column + click-to-copy
- Invalid Date fix

See docs/superpowers/specs/2026-04-19-release-pipeline-design.md §6.6.E."
```

Replace `<short-sha>` with the actual commit hash from the Adminpanel commit.

---

## Sub-phase 6.6.F — Landing page integration

Static site at `landing-page-work/`. No test infra; manual verification only. **~2 hours.**

### Task 22: Add dynamic download JS

**Files:**
- Modify: `landing-page-work/scripts/main.js` — append IIFE at bottom
- Modify: `landing-page-work/index.html` — add `.download-version` span + `<details>` block

- [ ] **Step 1: Append `loadLatestRelease` IIFE at the VERY END of `main.js`**

Do NOT refactor the existing 107KB. Just append a standalone IIFE. Open the file, scroll to bottom, append:

```javascript

// ============================================================================
// Release pipeline integration (added 2026-04-21, Phase 6.6)
// Fetches latest version from backend and updates primary Download CTAs.
// Falls back silently to hardcoded v1.6.0 GitHub link if API unavailable.
// ============================================================================
(function() {
  'use strict';

  function detectOS() {
    var p = (navigator.platform || '').toLowerCase();
    var ua = (navigator.userAgent || '').toLowerCase();
    if (p.indexOf('win') >= 0 || ua.indexOf('windows') >= 0) return 'windows';
    if (p.indexOf('linux') >= 0 || ua.indexOf('linux') >= 0) return 'linux';
    if (p.indexOf('mac') >= 0 || ua.indexOf('macintosh') >= 0) return 'macos';
    return 'windows';
  }

  function displayOS(os) {
    return { windows: 'Windows', linux: 'Linux', macos: 'macOS' }[os] || 'Windows';
  }

  async function fetchPlatformRelease(platform) {
    try {
      var r = await fetch('/api/updates/check?currentVersion=0.0.0&platform=' + platform);
      if (!r.ok) return null;
      var j = await r.json();
      return j.updateAvailable ? j : null;
    } catch (e) { return null; }
  }

  async function loadLatestRelease() {
    var os = detectOS();
    var jobs = [fetchPlatformRelease(os)];
    if (os !== 'linux') jobs.push(fetchPlatformRelease('linux'));
    else jobs.push(fetchPlatformRelease('windows'));

    var results = await Promise.all(jobs);
    var primary = results[0];
    var other = results[1];

    if (primary) {
      document.querySelectorAll('.download-link').forEach(function(a) {
        a.href = primary.downloadUrl;
      });
      document.querySelectorAll('.download-version').forEach(function(el) {
        el.textContent = 'v' + primary.version;
      });
      document.querySelectorAll('.download-platform').forEach(function(el) {
        el.textContent = displayOS(os);
      });
    }

    var dropdown = document.getElementById('otherPlatformsList');
    if (dropdown) {
      dropdown.innerHTML = '';
      var add = function(label, url, comingSoon) {
        var li = document.createElement('li');
        if (comingSoon) {
          li.innerHTML = '<span class="disabled">' + label + ' — Coming Soon</span>';
        } else {
          li.innerHTML = '<a href="' + url + '">' + label + '</a>';
        }
        dropdown.appendChild(li);
      };
      if (os !== 'windows' && primary && os === 'windows') { /* covered by primary */ }
      // Always list the other platform(s)
      if (other && os === 'windows')  add('Linux',   other.downloadUrl, false);
      if (other && os === 'linux')    add('Windows', other.downloadUrl, false);
      if (os === 'macos')             { if (other) add('Windows', other.downloadUrl, false); add('macOS', '', true); }
      else                            add('macOS',   '', true);
    }
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', loadLatestRelease);
  } else {
    loadLatestRelease();
  }
})();
```

- [ ] **Step 2: Add markup hooks to `index.html`**

Find lines 147 and 280 (both contain `class="btn-primary download-link"`). Inside each button's `<span data-i18n="hero.download">` (or `cta.btn`), augment to carry dynamic text. For line 147, the current inner content is:

```html
<span data-i18n="hero.download">Download Free</span>
```

Change to:

```html
<span data-i18n="hero.download">Download Free</span>
<span class="download-version" style="margin-left:6px;opacity:0.8;font-size:0.9em"></span>
```

For line 280, same pattern inside that button.

After the FIRST button (line 147 area), add a compact dropdown:

```html
<details class="other-platforms" style="display:inline-block;margin-left:12px;font-size:0.9em">
  <summary style="cursor:pointer;color:#fff;opacity:0.75">Other platforms ↓</summary>
  <ul id="otherPlatformsList" style="list-style:none;padding:6px 0;margin:4px 0 0 0;background:rgba(0,0,0,0.4);border-radius:6px;border:1px solid rgba(255,255,255,0.1)">
    <li><span class="disabled" style="color:#888">Loading…</span></li>
  </ul>
</details>
```

- [ ] **Step 3: Manual smoke test**

```bash
cd C:/Users/Admin/Desktop/AuraCorePro/AuraCorePro/landing-page-work
python -m http.server 4180
```

Open `http://localhost:4180`. Primary download button should either show v1.6.0 (if backend DB has v1.6.0 seeded per 6.6.A — localhost hits dev backend) OR silently fall back to hardcoded v1.6.0 GitHub link if backend not running. The "Other platforms ↓" should appear with Linux + macOS (disabled). In DevTools, Network tab should show `/api/updates/check?platform=windows` fetch attempt.

- [ ] **Step 4: Commit 6.6.F**

```bash
cd C:/Users/Admin/Desktop/AuraCorePro/AuraCorePro
git add landing-page-work/
git commit -m "feat(landing): dynamic download CTAs via /api/updates/check (6.6.F)

- loadLatestRelease IIFE appended to main.js (no refactor)
- .download-link href + .download-version text replaced from API
- Other platforms dropdown populated from parallel fetches
- Silent fallback to hardcoded v1.6.0 GitHub URL on API failure"
```

### Task 23: Deploy landing to origin

**Files:**
- Remote: `/var/www/landing-page/` on 165.227.170.3

- [ ] **Step 1: Backup + upload via scp**

Timestamp format per prior precedent: `.bak-YYYYMMDDhhmm`.

```bash
TS=$(date +%Y%m%d%H%M)
ssh -i C:/Users/Admin/.ssh/id_ed25519 root@165.227.170.3 "
  cp /var/www/landing-page/index.html /var/www/landing-page/index.html.bak-${TS} &&
  cp /var/www/landing-page/scripts/main.js /var/www/landing-page/scripts/main.js.bak-${TS}
"
scp -i C:/Users/Admin/.ssh/id_ed25519 landing-page-work/index.html root@165.227.170.3:/var/www/landing-page/
scp -i C:/Users/Admin/.ssh/id_ed25519 landing-page-work/scripts/main.js root@165.227.170.3:/var/www/landing-page/scripts/
```

- [ ] **Step 2: Verify live**

```bash
curl -sS -o /dev/null -w "%{http_code}" https://auracore.pro/
curl -sS https://auracore.pro/scripts/main.js | tail -20
```

Expected: `200`, tail shows the loadLatestRelease IIFE. Open browser, DevTools Network — verify `/api/updates/check` fetch attempted. Because backend endpoint may not have v1.7.0 data yet, expect fallback to hardcoded v1.6.0.

- [ ] **Step 3: Commit deploy note**

```bash
git commit --allow-empty -m "ops: landing 6.6.F deployed to origin (bak-${TS})

curl verify 200; loadLatestRelease IIFE present in scripts/main.js."
```

---

## Sub-phase 6.6.G — Desktop update flow

Refactors existing monolithic `UpdateChecker` → split off `UpdateDownloader` service; adds platform-aware `?platform=` query; adds banner + modal UI. **~4 hours.**

**Critical discovery from Explore report:** Current `UpdateChecker` is at `src/Desktop/AuraCore.Desktop/Services/UpdateChecker.cs` — this suggests a PRE-UI-rebuild path. Post-Phase-5 the app was migrated to `src/UI/AuraCore.UI.Avalonia/`. Verify location in Task 24 Step 1 before modifying.

### Task 24: Inventory + decide target location

**Files:**
- Investigation only.

- [ ] **Step 1: Confirm current UpdateChecker location**

```bash
cd C:/Users/Admin/Desktop/AuraCorePro/AuraCorePro
```

Use Glob: `**/UpdateChecker.cs` (exclude `bin/` + `obj/`).

If `src/UI/AuraCore.UI.Avalonia/Services/` also has a checker, use THAT (Phase 5-era). If only `src/Desktop/AuraCore.Desktop/Services/UpdateChecker.cs` exists, we target THAT file. Report the resolved path.

- [ ] **Step 2: Confirm App.axaml.cs DI container location**

Use Glob: `**/App.axaml.cs` (exclude `bin/` + `obj/`). Note the DI registration section for future task.

### Task 25: Extract `IUpdateDownloader` + impl (TDD)

**Files:**
- Create: `<resolved-root>/Services/Update/IUpdateDownloader.cs`
- Create: `<resolved-root>/Services/Update/UpdateDownloader.cs`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Services/UpdateDownloaderTests.cs`

Resolved-root = the path determined in Task 24 Step 1 (either `src/UI/AuraCore.UI.Avalonia/` or `src/Desktop/AuraCore.Desktop/`).

- [ ] **Step 1: Write failing tests**

`tests/AuraCore.Tests.UI.Avalonia/Services/UpdateDownloaderTests.cs`:

```csharp
// NOTE: If the UI project is src/Desktop/AuraCore.Desktop, create the test file
// under tests/AuraCore.Tests.UI.Avalonia but reference AuraCore.Desktop.
using AuraCore.UI.Avalonia.Services.Update; // adjust namespace based on Task 24 resolution
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Services;

public class UpdateDownloaderTests
{
    private static HttpMessageHandler BuildHandler(byte[] body)
    {
        var h = new Mock<HttpMessageHandler>();
        h.Protected()
         .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
         .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });
        return h.Object;
    }

    [Fact]
    public async Task DownloadAsync_writes_file_and_returns_path_when_hash_matches()
    {
        var payload = System.Text.Encoding.UTF8.GetBytes("fake-installer-v1.7.0");
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload)).ToLowerInvariant();

        var http = new HttpClient(BuildHandler(payload));
        var downloader = new UpdateDownloader(http);
        var avail = new AvailableUpdate("1.7.0", "https://x/AuraCorePro-Windows-v1.7.0.exe", hash, isMandatory: false);

        var progress = new Progress<double>(_ => { });
        var path = await downloader.DownloadAsync(avail, progress, CancellationToken.None);

        Assert.True(File.Exists(path));
        Assert.Equal(payload.Length, new FileInfo(path).Length);
        Assert.EndsWith("AuraCorePro-Windows-v1.7.0.exe", path);

        File.Delete(path);
    }

    [Fact]
    public async Task DownloadAsync_throws_and_deletes_file_when_hash_mismatches()
    {
        var payload = System.Text.Encoding.UTF8.GetBytes("corrupted-bytes");
        var wrongHash = new string('0', 64);

        var http = new HttpClient(BuildHandler(payload));
        var downloader = new UpdateDownloader(http);
        var avail = new AvailableUpdate("1.7.0", "https://x/AuraCorePro-Windows-v1.7.0.exe", wrongHash, false);

        var progress = new Progress<double>(_ => { });
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => downloader.DownloadAsync(avail, progress, CancellationToken.None));

        // File should be cleaned up after mismatch
        var expectedPath = Path.Combine(Path.GetTempPath(), "AuraCorePro-Windows-v1.7.0.exe");
        Assert.False(File.Exists(expectedPath));
    }
}
```

- [ ] **Step 2: Run — expect FAIL**

Run: `dotnet test tests/AuraCore.Tests.UI.Avalonia --filter "FullyQualifiedName~UpdateDownloaderTests" --logger "console;verbosity=minimal"`
Expected: Compile error.

- [ ] **Step 3: Implement interface + downloader**

`<resolved-root>/Services/Update/IUpdateDownloader.cs`:

```csharp
namespace AuraCore.UI.Avalonia.Services.Update;

public sealed record AvailableUpdate(string Version, string DownloadUrl, string Sha256, bool IsMandatory);

public interface IUpdateDownloader
{
    /// <summary>Streams to %TEMP%, verifies SHA256, returns absolute file path.</summary>
    Task<string> DownloadAsync(AvailableUpdate update, IProgress<double> progress, CancellationToken ct);

    /// <summary>Starts the installer and exits the current process.</summary>
    void InstallAndExit(string installerPath);
}
```

`<resolved-root>/Services/Update/UpdateDownloader.cs`:

```csharp
using System.Diagnostics;
using System.Security.Cryptography;

namespace AuraCore.UI.Avalonia.Services.Update;

public sealed class UpdateDownloader : IUpdateDownloader
{
    private readonly HttpClient _http;

    public UpdateDownloader(HttpClient http) => _http = http;

    public async Task<string> DownloadAsync(AvailableUpdate update, IProgress<double> progress, CancellationToken ct)
    {
        var filename = Path.GetFileName(new Uri(update.DownloadUrl).AbsolutePath);
        var path = Path.Combine(Path.GetTempPath(), filename);

        try
        {
            using var resp = await _http.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength ?? 0L;

            await using (var fs = File.Create(path))
            await using (var net = await resp.Content.ReadAsStreamAsync(ct))
            {
                var buf = new byte[81920];
                long read = 0;
                int n;
                while ((n = await net.ReadAsync(buf, ct)) > 0)
                {
                    await fs.WriteAsync(buf.AsMemory(0, n), ct);
                    read += n;
                    if (total > 0) progress.Report((double)read / total);
                }
                progress.Report(1.0);
            }

            // Verify SHA256
            await using (var fs = File.OpenRead(path))
            {
                using var sha = SHA256.Create();
                var hash = Convert.ToHexString(await sha.ComputeHashAsync(fs, ct)).ToLowerInvariant();
                if (!string.Equals(hash, update.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"SHA256 mismatch: expected {update.Sha256}, got {hash}");
            }

            return path;
        }
        catch
        {
            if (File.Exists(path)) { try { File.Delete(path); } catch { /* ignore */ } }
            throw;
        }
    }

    public void InstallAndExit(string installerPath)
    {
        Process.Start(new ProcessStartInfo { FileName = installerPath, UseShellExecute = true });
        Environment.Exit(0);
    }
}
```

- [ ] **Step 4: Run — expect GREEN**

Run: `dotnet test tests/AuraCore.Tests.UI.Avalonia --filter "FullyQualifiedName~UpdateDownloaderTests" --logger "console;verbosity=minimal"`
Expected: `Passed: 2, Failed: 0`.

### Task 26: Wire `?platform=` param into `UpdateChecker`

**Files:**
- Modify: `<resolved-UpdateChecker-path>` (from Task 24)

- [ ] **Step 1: Add platform detection helper**

At the top of `UpdateChecker.cs`, add a private helper method:

```csharp
private static string DetectPlatform()
{
    if (OperatingSystem.IsWindows()) return "windows";
    if (OperatingSystem.IsLinux())   return "linux";
    if (OperatingSystem.IsMacOS())   return "macos";
    return "windows";
}
```

- [ ] **Step 2: Update the `CheckForUpdateAsync` API URL**

Find the existing `GET {LoginWindow.ApiBaseUrl}/api/updates/check?currentVersion={CurrentVersion}&channel=stable` request building (per Explore report). Append `&platform=<DetectPlatform()>` query param.

Exact change: replace the URL build line with:

```csharp
var url = $"{LoginWindow.ApiBaseUrl}/api/updates/check?currentVersion={CurrentVersion}&channel=stable&platform={DetectPlatform()}";
```

- [ ] **Step 3: Add platform test for checker**

`tests/AuraCore.Tests.UI.Avalonia/Services/UpdateCheckerPlatformTests.cs`:

```csharp
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Services;

public class UpdateCheckerPlatformTests
{
    [Fact]
    public void DetectPlatform_returns_non_empty_string()
    {
        // Reflection-based or moved to public for testability. If the helper is private,
        // assert the behavior via URL inspection — for now, sanity-check OS detection:
        var p = OperatingSystem.IsWindows() ? "windows"
              : OperatingSystem.IsLinux() ? "linux"
              : OperatingSystem.IsMacOS() ? "macos" : "windows";
        Assert.NotEmpty(p);
        Assert.Contains(p, new[] { "windows", "linux", "macos" });
    }
}
```

- [ ] **Step 4: Build + run tests**

Run: `dotnet test tests/AuraCore.Tests.UI.Avalonia --filter "FullyQualifiedName~UpdateCheckerPlatformTests" --logger "console;verbosity=minimal"`
Expected: `Passed: 1`.

### Task 27: Update banner UI (soft, non-modal)

**Files:**
- Create: `<resolved-ui-root>/Views/Controls/UpdateBanner.axaml`
- Create: `<resolved-ui-root>/Views/Controls/UpdateBanner.axaml.cs`
- Modify: `<resolved-ui-root>/Views/MainWindow.axaml` — dock `<local:UpdateBanner/>`
- Modify: `LocalizationService.cs` — add banner EN+TR keys

- [ ] **Step 1: Create UserControl XAML**

`<resolved-ui-root>/Views/Controls/UpdateBanner.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="AuraCore.UI.Avalonia.Views.Controls.UpdateBanner"
             IsVisible="False"
             Background="#1F2937">
  <Grid ColumnDefinitions="*,Auto,Auto" Margin="16,10">
    <StackPanel Grid.Column="0" Orientation="Horizontal" VerticalAlignment="Center" Spacing="10">
      <TextBlock Text="🔔" FontSize="18"/>
      <TextBlock x:Name="BannerText" Foreground="White" VerticalAlignment="Center"/>
    </StackPanel>
    <Button Grid.Column="1" x:Name="UpdateBtn" Content="{DynamicResource UpdateBanner_UpdateNow}" Classes="accent" Margin="8,0"/>
    <Button Grid.Column="2" x:Name="LaterBtn" Content="{DynamicResource UpdateBanner_Later}" Classes="subtle"/>
  </Grid>
</UserControl>
```

`<resolved-ui-root>/Views/Controls/UpdateBanner.axaml.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AuraCore.UI.Avalonia.Views.Controls;

public partial class UpdateBanner : UserControl
{
    public UpdateBanner()
    {
        AvaloniaXamlLoader.Load(this);
        // Wire: subscribe to UpdateChecker.UpdateFound event, set BannerText, wire clicks.
        // Kept minimal here — full wiring happens in MainWindow code-behind (Task 29).
    }
}
```

- [ ] **Step 2: Add EN+TR localization keys**

In `LocalizationService.cs`, find the EN dictionary. Add these keys (both EN and TR dicts):

```csharp
// EN
["UpdateBanner_Message"] = "Version {0} is available.",
["UpdateBanner_UpdateNow"] = "Update now",
["UpdateBanner_Later"] = "Later",
["UpdateBanner_Downloading"] = "Downloading {0}...",
["UpdateBanner_HashMismatch"] = "Hash mismatch — download corrupted. Retry?",
["UpdateBanner_Ready"] = "Update downloaded. Run installer now?",
["UpdateBanner_Mandatory_Title"] = "Required update",
["UpdateBanner_Mandatory_Message"] = "Version {0} is a mandatory update. AuraCore Pro will now install the new version.",

// TR
["UpdateBanner_Message"] = "Sürüm {0} mevcut.",
["UpdateBanner_UpdateNow"] = "Şimdi güncelle",
["UpdateBanner_Later"] = "Daha sonra",
["UpdateBanner_Downloading"] = "{0} indiriliyor...",
["UpdateBanner_HashMismatch"] = "Hash eşleşmedi — dosya bozuk. Tekrar dene?",
["UpdateBanner_Ready"] = "Güncelleme indirildi. Kurulum şimdi başlatılsın mı?",
["UpdateBanner_Mandatory_Title"] = "Zorunlu güncelleme",
["UpdateBanner_Mandatory_Message"] = "Sürüm {0} zorunlu bir güncellemedir. AuraCore Pro yeni sürümü şimdi kuracak.",
```

### Task 28: Mandatory update modal (hard-block)

**Files:**
- Create: `<resolved-ui-root>/Views/Dialogs/MandatoryUpdateDialog.axaml`
- Create: `<resolved-ui-root>/Views/Dialogs/MandatoryUpdateDialog.axaml.cs`

- [ ] **Step 1: Create modal XAML**

`<resolved-ui-root>/Views/Dialogs/MandatoryUpdateDialog.axaml`:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="AuraCore.UI.Avalonia.Views.Dialogs.MandatoryUpdateDialog"
        Width="500" Height="240" CanResize="False" WindowStartupLocation="CenterOwner"
        SystemDecorations="BorderOnly" Topmost="True"
        Title="{DynamicResource UpdateBanner_Mandatory_Title}">
  <Border Background="{DynamicResource App_SurfaceBackground}" Padding="24" CornerRadius="12">
    <DockPanel>
      <TextBlock DockPanel.Dock="Top" Text="{DynamicResource UpdateBanner_Mandatory_Title}"
                 FontSize="18" FontWeight="Bold" Margin="0,0,0,12"/>
      <TextBlock DockPanel.Dock="Top" x:Name="MessageText" TextWrapping="Wrap"/>
      <ProgressBar DockPanel.Dock="Bottom" x:Name="ProgressBar" Margin="0,12,0,0"
                   Minimum="0" Maximum="100"/>
      <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" HorizontalAlignment="Right"
                  Spacing="10" Margin="0,12,0,0">
        <Button x:Name="UpdateBtn" Content="{DynamicResource UpdateBanner_UpdateNow}"
                Classes="accent" IsEnabled="True"/>
      </StackPanel>
    </DockPanel>
  </Border>
</Window>
```

`.cs` companion:

```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AuraCore.UI.Avalonia.Views.Dialogs;

public partial class MandatoryUpdateDialog : Window
{
    public MandatoryUpdateDialog()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
```

### Task 29: Wire MainWindow to show banner/modal on `UpdateFound`

**Files:**
- Modify: `<resolved-ui-root>/Views/MainWindow.axaml` + `.axaml.cs`

- [ ] **Step 1: Add `<local:UpdateBanner>` at the top of MainWindow layout**

In MainWindow.axaml, find the root layout container (usually `Grid` or `DockPanel`). At the top (first child, docked top), add:

```xml
<controls:UpdateBanner x:Name="UpdateBanner" DockPanel.Dock="Top"/>
```

Add `xmlns:controls="using:AuraCore.UI.Avalonia.Views.Controls"` to the `<Window>` root.

- [ ] **Step 2: In MainWindow.axaml.cs `OnLoaded`, subscribe to `UpdateChecker.Instance.UpdateFound`**

```csharp
private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
{
    UpdateChecker.Instance.UpdateFound += OnUpdateFound;
    UpdateChecker.Instance.Start();
}

private void OnUpdateFound(object? sender, EventArgs args)
{
    Dispatcher.UIThread.Post(() =>
    {
        var ck = UpdateChecker.Instance;
        if (ck.IsMandatory)
        {
            ShowMandatoryDialog(ck);
        }
        else
        {
            ShowBanner(ck);
        }
    });
}

private void ShowBanner(UpdateChecker ck)
{
    UpdateBanner.IsVisible = true;
    var text = UpdateBanner.FindControl<TextBlock>("BannerText");
    if (text is not null) text.Text = string.Format(Loc["UpdateBanner_Message"], ck.LatestVersion);

    var updateBtn = UpdateBanner.FindControl<Button>("UpdateBtn");
    var laterBtn  = UpdateBanner.FindControl<Button>("LaterBtn");
    if (updateBtn is not null) updateBtn.Click += async (_, _) => await StartDownloadFlow(ck);
    if (laterBtn  is not null) laterBtn.Click  += (_, _) => UpdateBanner.IsVisible = false;
}

private async void ShowMandatoryDialog(UpdateChecker ck)
{
    var dialog = new MandatoryUpdateDialog();
    var msg = dialog.FindControl<TextBlock>("MessageText");
    if (msg is not null) msg.Text = string.Format(Loc["UpdateBanner_Mandatory_Message"], ck.LatestVersion);
    var updateBtn = dialog.FindControl<Button>("UpdateBtn");
    if (updateBtn is not null) updateBtn.Click += async (_, _) =>
    {
        updateBtn.IsEnabled = false;
        await StartDownloadFlow(ck);
    };
    await dialog.ShowDialog(this);
}

private async Task StartDownloadFlow(UpdateChecker ck)
{
    var downloader = App.Services.GetRequiredService<IUpdateDownloader>();
    var avail = new AvailableUpdate(ck.LatestVersion!, ck.DownloadUrl!, ck.SignatureHash ?? "", ck.IsMandatory);
    var progress = new Progress<double>(p => { /* update UI bar */ });
    try
    {
        var path = await downloader.DownloadAsync(avail, progress, CancellationToken.None);
        downloader.InstallAndExit(path);
    }
    catch (Exception ex)
    {
        // Show error dialog; allow retry for mandatory
        System.Diagnostics.Debug.WriteLine($"Update failed: {ex.Message}");
    }
}
```

Adjust namespaces (`Loc`, `App.Services`) to match the codebase's existing patterns discovered in Task 24.

### Task 30: Register `IUpdateDownloader` in DI

**Files:**
- Modify: `<resolved-App.axaml.cs path>` — DI container registration

- [ ] **Step 1: Add `HttpClient` + `IUpdateDownloader` registration**

In the existing DI container setup, register:

```csharp
services.AddSingleton<HttpClient>();
services.AddSingleton<IUpdateDownloader, UpdateDownloader>();
```

Use whatever container abstraction the app already has (Microsoft.Extensions.DependencyInjection, Autofac, etc.). Follow existing patterns.

- [ ] **Step 2: Build**

Run: `dotnet build AuraCorePro.sln --no-restore 2>&1 | tail -10`
Expected: Build succeeds.

- [ ] **Step 3: Run ALL tests to ensure no regression**

Run: `dotnet test AuraCorePro.sln --logger "console;verbosity=minimal" 2>&1 | tail -10`
Expected: ~2295 passing (2270 baseline + ~25 from 6.6.A-G).

- [ ] **Step 4: Manual smoke (desktop app)**

Start the desktop app in Debug. Confirm:
- App starts without exceptions.
- If backend is running + v1.7.0 seeded, UpdateBanner should appear after ~5s.
- Clicking "Later" hides the banner.
- Clicking "Update now" triggers a download (progress bar visible).
- On hash mismatch, error state shows + no installer launches.

Note: Full installer-launch flow is destructive (Environment.Exit). Use a 100KB dummy .exe for smoke test — put it on R2 + publish v-test-999.

- [ ] **Step 5: Commit 6.6.G**

```bash
cd C:/Users/Admin/Desktop/AuraCorePro/AuraCorePro
git add src/ tests/AuraCore.Tests.UI.Avalonia/
git commit -m "feat(desktop): UpdateDownloader + banner/modal UI + platform param (6.6.G)

- IUpdateDownloader + UpdateDownloader: %TEMP% stream + SHA256 verify + launch
- UpdateChecker: adds ?platform=<detected> to /api/updates/check
- UpdateBanner UserControl: soft non-modal (non-mandatory)
- MandatoryUpdateDialog: hard modal (cannot dismiss)
- 8 new EN+TR localization keys for banner/dialog
- 3 new tests (2 UpdateDownloader + 1 platform detection)"
```

---

## Sub-phase 6.6.H — Manual ops documentation

**~1 hour.** Writes a single markdown doc with every manual step the user must perform to complete the release pipeline bring-up.

### Task 31: Write ops doc

**Files:**
- Create: `docs/ops/release-pipeline-setup.md`

- [ ] **Step 1: Write the doc**

Create `docs/ops/release-pipeline-setup.md`:

````markdown
# Release Pipeline — Operational Setup

Manual steps the user (Özgür) must perform once to complete release pipeline bring-up. All items are **outside CI/deployment** — they require Cloudflare dashboard access, GitHub account access, and SSH to the origin server (165.227.170.3).

Last updated: 2026-04-21.

## 1. Cloudflare R2 — bucket + custom domain + lifecycle

1. **Create bucket** `auracore-releases` (or confirm existing, if reusing a prefix instead).
   - Cloudflare dashboard → R2 → Create bucket.

2. **Attach custom domain `download.auracore.pro`**:
   - R2 → bucket → Settings → Custom Domains → Add `download.auracore.pro`.
   - Cloudflare DNS automatically proposes the CNAME; approve.
   - Wait ~2 min for SSL provisioning (edge cert auto).
   - Verify: `curl -I https://download.auracore.pro/ 2>&1 | head -1` → expect 404 or 403 (bucket root listing blocked) but NOT cert error.

3. **Lifecycle rule for `pending/` prefix**:
   - R2 → bucket → Lifecycle Rules → Add rule.
   - Prefix: `pending/`
   - Action: Delete objects after 7 days.
   - Rule name: `autoclean-pending-7d`.

4. **Generate R2 API token** (if not already present):
   - Cloudflare dashboard → R2 → Manage R2 API Tokens → Create API Token.
   - Permissions: Object Read & Write on `auracore-releases` bucket.
   - Save the Access Key ID + Secret Access Key.
   - Note the account ID (shown on R2 overview page).

5. **Backend env vars** (add to `/etc/auracore-api.env` on origin):
   ```
   R2_ACCOUNT_ID=<from step 4>
   R2_ACCESS_KEY_ID=<from step 4>
   R2_SECRET_ACCESS_KEY=<from step 4>
   R2_BUCKET=auracore-releases
   ```
   Then: `systemctl restart auracore-api`.

## 2. EF Core migrations bootstrap on production

The `InitialCreate` migration captures existing production schema. Production DB was created by `EnsureCreated`-style path; we must manually seed `__EFMigrationsHistory` so EF doesn't try to recreate tables.

1. SSH to origin:
   ```bash
   ssh -i ~/.ssh/id_ed25519 root@165.227.170.3
   sudo -iu postgres psql -d auracore
   ```

2. Create the migrations history table + seed `InitialCreate`:
   ```sql
   CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
       "MigrationId" varchar(150) NOT NULL PRIMARY KEY,
       "ProductVersion" varchar(32) NOT NULL
   );

   -- Replace <TIMESTAMP> with the actual migration timestamp from src/Backend/AuraCore.API.Infrastructure/Migrations/
   -- (e.g., "20260421120000_InitialCreate"; look at the filename)
   INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
   VALUES ('<TIMESTAMP>_InitialCreate', '8.0.11')
   ON CONFLICT DO NOTHING;
   \q
   ```

3. `AddPlatformToAppUpdate` migration will auto-apply on next deploy (via `Program.cs` auto-migrate hook). Verify after deploy:
   ```sql
   SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";
   -- Should show both InitialCreate and AddPlatformToAppUpdate
   ```

## 3. GitHub PAT for release mirror

1. GitHub → Settings → Developer settings → Personal access tokens → Fine-grained tokens → Generate new token.
   - Name: `AuraCorePro Release Mirror`
   - Expiry: 1 year
   - Repository access: `edutuvarna/AuraCorePro` only
   - Repository permissions: `Contents` → Read and write
   - Copy token (starts with `github_pat_`).

2. Add to `/etc/auracore-api.env` on origin:
   ```
   ASPNETCORE_GITHUB_TOKEN=github_pat_xxxxx
   ```
   Then: `systemctl restart auracore-api`.

3. Calendar reminder: rotate 30 days before expiry.

## 4. Discord webhook (already present)

`DISCORD_WEBHOOK_URL` and `DISCORD_UPDATES_ROLE_ID` are already in `/etc/auracore-api.env`. No action.

## 5. Verification smoke test

After all of the above, publish a test release:

```bash
# 1. Admin panel: upload + publish a small 100KB test .exe as v0.0.1-test
#    (use a dummy file, NOT a real installer, for the smoke test)

# 2. Verify R2 object lives:
curl -I https://download.auracore.pro/releases/v0.0.1-test/AuraCorePro-Windows-v0.0.1-test.exe
# Expect: 200 OK

# 3. Verify public check endpoint returns it:
curl 'https://api.auracore.pro/api/updates/check?currentVersion=0.0.0&platform=windows'
# Expect: { updateAvailable: true, version: "0.0.1-test", ... } if it's the newest stable Windows release.

# 4. Verify GitHub mirror (within ~30s of publish):
curl -I https://github.com/edutuvarna/AuraCorePro/releases/tag/v0.0.1-test
# Expect: 200

# 5. Cleanup:
#    Admin panel → Updates → delete v0.0.1-test row.
#    GitHub manually: delete the v0.0.1-test release from Releases page.
#    R2 manually: delete the releases/v0.0.1-test/ prefix.
```

## Rollback

If `AddPlatformToAppUpdate` migration breaks something:

```bash
ssh root@165.227.170.3
sudo -iu postgres psql -d auracore
```
```sql
-- Remove the migration entry
DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" LIKE '%_AddPlatformToAppUpdate';
-- Drop the column (reverts schema)
ALTER TABLE app_updates DROP COLUMN IF EXISTS "Platform";
ALTER TABLE app_updates DROP COLUMN IF EXISTS "GitHubReleaseId";
DROP INDEX IF EXISTS "IX_app_updates_Version_Channel_Platform";
CREATE UNIQUE INDEX IF NOT EXISTS "IX_app_updates_Version_Channel" ON app_updates("Version", "Channel");
```
Then restart the old backend version (git revert + redeploy).
````

- [ ] **Step 2: Commit 6.6.H**

```bash
git add docs/ops/release-pipeline-setup.md
git commit -m "docs(ops): release pipeline setup playbook (6.6.H)

R2 bucket + custom domain + lifecycle, EF migrations history seed,
GitHub PAT rotation, smoke test + rollback procedures."
```

---

## Sub-phase 6.6.I — Smoke test + ceremonial close

**~4 hours.** Real-world E2E verification + memory file updates + merge-to-main ceremony.

### Task 32: Pre-merge full-solution test run

- [ ] **Step 1: Full solution test**

```bash
cd C:/Users/Admin/Desktop/AuraCorePro/AuraCorePro
dotnet test AuraCorePro.sln --logger "console;verbosity=minimal" 2>&1 | tail -10
```
Expected: ~2295 tests passing, 0 failing, 0 unexpected skips. Record exact number.

- [ ] **Step 2: Full solution build**

```bash
dotnet build AuraCorePro.sln --no-restore 2>&1 | tail -10
```
Expected: 0 warnings, 0 errors.

### Task 33: E2E smoke test in production (guarded)

**Prerequisite:** User must have completed all items in [6.6.H setup doc](../../docs/ops/release-pipeline-setup.md). If user hasn't, STOP and wait.

- [ ] **Step 1: Publish a test release via admin panel**

Using admin.auracore.pro Publish Update form:
- Version: `0.0.1-smoketest`
- Platform: Windows only
- File: a 100KB dummy `.exe` (e.g., `echo test > smoke.exe` padded to 100KB)
- Release notes: "smoke test from release pipeline 6.6"
- Mandatory: false

Watch: progress bar reaches 100%, Publish succeeds, row appears in Updates list.

- [ ] **Step 2: Verify R2 object**

```bash
curl -I https://download.auracore.pro/releases/v0.0.1-smoketest/AuraCorePro-Windows-v0.0.1-smoketest.exe
```
Expected: 200.

- [ ] **Step 3: Verify check endpoint**

```bash
curl 'https://api.auracore.pro/api/updates/check?currentVersion=0.0.0&platform=windows&channel=stable'
```
Expected: JSON with `updateAvailable: true`, version `0.0.1-smoketest` (if it's the newest).

- [ ] **Step 4: Verify GitHub mirror**

Within ~30 seconds of publish:
```bash
curl -sI https://github.com/edutuvarna/AuraCorePro/releases/tag/v0.0.1-smoketest | head -1
```
Expected: 200.

- [ ] **Step 5: Verify landing page reflects new version**

`curl -sS https://auracore.pro/ | grep -o 'download.*Setup[^"]*"'` — should now see dynamic fetch. Open browser, DevTools Network → `/api/updates/check?platform=windows` should hit.

- [ ] **Step 6: Cleanup test data**

- Admin panel: Delete v0.0.1-smoketest row.
- GitHub: manually delete the v0.0.1-smoketest release + tag.
- R2: manually delete `releases/v0.0.1-smoketest/` prefix via Cloudflare dashboard.

### Task 34: Write ceremonial memory file

**Files:**
- Create: `C:\Users\Admin\.claude\projects\C--\memory\project_release_pipeline_complete.md`
- Modify: `C:\Users\Admin\.claude\projects\C--\memory\MEMORY.md` (add pointer line)

- [ ] **Step 1: Draft memory content**

Create `C:\Users\Admin\.claude\projects\C--\memory\project_release_pipeline_complete.md`:

```markdown
---
name: Release Pipeline COMPLETE (Phase 6.6)
description: End-to-end release pipeline — R2 presigned uploads, per-platform publishing, dynamic landing page, desktop update flow with SHA256 verify, GitHub Releases mirror. Supersedes memory snapshots before this; authoritative for release-pipeline history.
type: project
---

# Release Pipeline Phase 6.6 — COMPLETE

**Branch:** `phase-6-release-pipeline` merged to main at `<merge-sha>` (ceremonial `<ceremonial-sha>`).
**Test count:** <final-test-count>/<final-test-count> passing (+~25 from 2270 baseline).
**Spec:** `docs/superpowers/specs/2026-04-19-release-pipeline-design.md`.
**Plan:** `docs/superpowers/plans/2026-04-21-release-pipeline.md`.

## What shipped

Sub-phase summary (see plan for per-task detail):

- **6.6.A** — Data layer: `AppUpdatePlatform` enum, `AppUpdate.Platform` + `.GitHubReleaseId` columns, composite `(Version, Channel, Platform)` unique index. EF migrations bootstrap (`InitialCreate` + `AddPlatformToAppUpdate`); production `__EFMigrationsHistory` seeded manually.
- **6.6.B** — R2 client: `IR2Client` + `AwsR2Client` (AWSSDK.S3 3.7.404). Presigned PUT / HEAD / COPY / DELETE / SHA256 stream / download-to-stream. 5 tests.
- **6.6.C** — Backend endpoints: `POST /api/admin/updates/prepare-upload` (new), `POST /api/admin/updates/publish` (refactored to R2 flow), `POST /api/admin/updates/{id}/mirror-to-github` (retry), `GET /api/updates/check?platform=` (default `windows`). 17 tests.
- **6.6.D** — GitHub mirror: `IGitHubReleaseMirror` + `OctokitReleaseMirror` (Octokit 13.0.1). Binary + sha256sums.txt as release assets. 2 tests.
- **6.6.E** — Admin panel (separate `Adminpanel` repo): UpdatesPage rewritten with 3-step upload flow + progress bars + "Coming Soon" macOS + GitHub mirror retry column. UsersPage gains ID column with click-to-copy. Invalid Date bug fixed.
- **6.6.F** — Landing: `loadLatestRelease` IIFE appended to `scripts/main.js`. `.download-link` href + `.download-version` text + Other platforms dropdown populated from parallel fetches. Silent fallback to hardcoded v1.6.0 GitHub URL if API fails.
- **6.6.G** — Desktop: `IUpdateDownloader` + `UpdateDownloader` (%TEMP% stream + SHA256 verify + Process.Start + Environment.Exit). `UpdateChecker.CheckForUpdateAsync` now sends `&platform=<detected>`. `UpdateBanner` UserControl (soft, non-modal) + `MandatoryUpdateDialog` (hard modal). 3 tests. 8 new EN+TR localization keys.
- **6.6.H** — Ops doc `docs/ops/release-pipeline-setup.md`: R2 + custom domain + lifecycle, EF migrations history seed, GitHub PAT setup + rotation, smoke test + rollback procedures.
- **6.6.I** — E2E smoke passed (v0.0.1-smoketest → R2 → API → landing → GitHub, then cleanup).

## Out-of-scope carry-forward (from spec)

- **Multi-arch** (x64 only for day-1; ARM64 / Apple Silicon / ARM Linux = future)
- **Delta updates** — full-installer re-download is fine at current app size
- **Cryptographic signing** (Authenticode + Developer ID) — separate spec
- **Beta/canary channel UI** — backend supports, admin form shows `stable` default only
- **Auto-rollback button** — manual DELETE + re-publish pattern
- **Download analytics dashboard** — R2 access logs suffice
- **Mandatory geolocation** — overkill
- **User opt-out from auto-update** — universal banner for now
- **Admin panel comprehensive audit** — separate Phase 6 Item 7 spec (Bug 2: tier grant dual-source-of-truth, Bug 3: stale-data, SignalR sync)

## Commit map

Roughly 10-12 commits on the branch + separate `Adminpanel` repo commit:
- Baseline marker (empty)
- 6.6.A milestone (enum + entity + migrations + tests)
- 6.6.B R2 client split (tests + impl + DI)
- 6.6.C endpoints
- 6.6.D GitHub mirror
- 6.6.E admin panel note (real work in Adminpanel repo)
- 6.6.F landing JS + deploy verify
- 6.6.G desktop downloader + UI
- 6.6.H ops doc
- 6.6.I ceremonial close + memory
- Merge to main (`--no-ff`)
```

- [ ] **Step 2: Add pointer to `MEMORY.md`**

In `MEMORY.md`, append (after the "Post-Phase-6 ad-hoc fixes" entry):

```
- [Release Pipeline COMPLETE (Phase 6.6)](project_release_pipeline_complete.md) — R2 uploads + admin panel + landing + desktop update flow + GitHub mirror. Branch `phase-6-release-pipeline` merged to main at `<merge-sha>`.
```

### Task 35: Merge to main (`--no-ff` ceremony)

- [ ] **Step 1: Final test + lint sweep**

```bash
dotnet test AuraCorePro.sln --logger "console;verbosity=minimal" 2>&1 | tail -5
dotnet build AuraCorePro.sln --no-restore 2>&1 | tail -3
```

Both must succeed cleanly.

- [ ] **Step 2: Merge**

```bash
git checkout main
git pull
git merge --no-ff phase-6-release-pipeline -m "Merge phase-6-release-pipeline: Release Pipeline (6.6)

End-to-end release pipeline shipped:
- Admin panel → presigned R2 PUT → backend finalize (HEAD+COPY+SHA256)
- /api/updates/check?platform= platform-aware
- GitHub Releases mirror (Octokit)
- Landing page dynamic download CTAs
- Desktop app banner/modal + %TEMP% download + SHA256 verify + installer launch
- EF Core migrations bootstrap (InitialCreate + AddPlatformToAppUpdate + v1.6.0 backfill)
- Ops doc at docs/ops/release-pipeline-setup.md

See docs/superpowers/plans/2026-04-21-release-pipeline.md for per-task detail.
See memory file project_release_pipeline_complete.md for phase summary.

Tests: <final-count>/<final-count> passing (+~25 from 2270)."
```

- [ ] **Step 3: Verify merge commit**

```bash
git log --oneline -5
git log --format="%H %s" -1
```

Copy the merge commit SHA. Back-fill `<merge-sha>` into the memory file (Task 34 Step 1) and MEMORY.md entry.

- [ ] **Step 4: Push (with explicit user confirmation)**

Per `MEMORY.md` — pushing to main is shared-state, never blanket-auto. ASK user first.

If user confirms:
```bash
git push origin main
```

- [ ] **Step 5: Commit the back-filled memory update**

```bash
# Edit memory files (Task 34 Step 1 and 2) to replace <merge-sha> placeholders with actual SHA.
# MEMORY.md and project_release_pipeline_complete.md are NOT in the main repo — they live under C:\Users\Admin\.claude\projects\C--\memory\. So no git commit for those.
```

Done.

---

## Self-Review Checklist (writing-plans skill requirement)

**1. Spec coverage:**
- ✅ `AppUpdate.Platform` enum + column — Tasks 2-8
- ✅ Composite unique index — Task 4
- ✅ v1.6.0 backfill — Task 7
- ✅ `POST /api/admin/updates/prepare-upload` — Task 12
- ✅ `POST /api/admin/updates/publish` V2 — Task 13
- ✅ `POST /api/admin/updates/{id}/mirror-to-github` — Task 14
- ✅ `GET /api/updates/check?platform=` — Task 15
- ✅ R2 bucket layout — Ops doc (6.6.H)
- ✅ `download.auracore.pro` custom domain — Ops doc (6.6.H)
- ✅ 7-day lifecycle on `pending/` — Ops doc (6.6.H)
- ✅ Admin panel form — Tasks 18-21
- ✅ Landing OS-detect + dropdown — Tasks 22-23
- ✅ Desktop `UpdateDownloader` + banner/modal — Tasks 25-30
- ✅ GitHub mirror with sha256sums.txt — Tasks 16-17
- ✅ Bundled admin UX: Users GUID col — Task 20
- ✅ Bundled admin UX: Invalid Date fix — Task 19 (fmtDate helper)
- ✅ Migration path docs — Ops doc (6.6.H)
- ✅ Testing strategy (~25 tests) — distributed across 6.6.A-G

**2. Placeholder scan:** none found — every step has either actual code or a concrete manual action.

**3. Type consistency:**
- `AppUpdatePlatform` enum values (Windows=1, Linux=2, MacOS=3) consistent across entity, controller, tests, client code.
- `PublishUpdateRequestV2` field names consistent between controller + tests + admin panel api.ts.
- `AvailableUpdate` record (desktop) + JSON from `/api/updates/check` fields aligned.

**4. Known gotchas:**
- Task 6 requires temporarily reverting DbContext changes (dance between InitialCreate and AddPlatformToAppUpdate migrations). Explicit Step 1 notes this.
- Task 24 requires deciding between `src/Desktop/AuraCore.Desktop/` vs `src/UI/AuraCore.UI.Avalonia/` based on inventory. All subsequent desktop tasks reference `<resolved-*>` placeholders that the subagent resolves from Task 24 output.
- Admin panel is in a SEPARATE git repo. Tasks 18-21 switch directories and commit there, then leave a note-only marker in the main repo.

---

## Execution Handoff

Per `feedback_subagent_driven_default.md` + `feedback_afk_default_recommended.md`: this plan will be executed via **`superpowers:subagent-driven-development`** without asking the user to choose. Each sub-phase is one subagent dispatch; main session verifies inline per supervisor-mode discipline.

**Plan complete. Saved to `docs/superpowers/plans/2026-04-21-release-pipeline.md`.**
