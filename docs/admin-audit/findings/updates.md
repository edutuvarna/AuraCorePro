# Updates Audit Findings

**Tab:** Updates
**URL path:** `https://admin.auracore.pro/` (SPA, sidebar nav → "Updates")
**Audit date:** 2026-04-22
**Auditor:** subagent-6
**Time spent:** ~2 hours

## Source files audited

- Frontend TSX (source on origin, March 27 snapshot): `/root/admin-panel/src/app/page.tsx` lines 799–889
- Frontend TSX (deployed — 6.6.E version, April 21): `/var/www/admin-panel/_next/static/chunks/app/page-9bf9edb4333e55cf.js`
- Frontend API client: `/root/admin-panel/src/lib/api.ts` lines 161–179
- Backend controller (local repo): `src/Backend/AuraCore.API/Controllers/Admin/AdminUpdateController.cs` (282 lines, 5 endpoints)
- Backend controller (backup, April 12): `/root/auracore-src-backup-final-202604122153/AuraCore.API/Controllers/Admin/AdminUpdateController.cs` (96 lines, 2 endpoints)
- Backend entity: `src/Backend/AuraCore.API.Domain/Entities/AppUpdate.cs`
- DbContext config: `src/Backend/AuraCore.API.Infrastructure/Data/AuraCoreDbContext.cs` lines 97–108
- EF migration: `src/Backend/AuraCore.API.Infrastructure/Migrations/20260421025345_AddPlatformToAppUpdate.cs`
- Deployed DLL: `/var/www/auracore-api/AuraCore.API.dll` (built 2026-04-14)

## Summary

- **1 critical** — 6.6.E backend never deployed; PrepareUpload (405) and RetryGitHubMirror (404) missing from prod DLL
- **1 high** — `AddPlatformToAppUpdate` migration not applied: `Platform` + `GitHubReleaseId` columns missing from prod DB; `IX_app_updates_Version_Channel_Platform` unique index absent (CTP-9 confirmed)
- **2 medium** — delete confirmation shows `(undefined)` platform; no frontend validation when zero platforms selected before Publish
- **1 low** — no audit log for Publish/Delete/RetryGitHubMirror (CTP-2 confirmed); R2 + GitHub env vars not in prod env file

Axes covered: functional, code+DB sync, security, UX, mobile, drift.

---

## Findings

### F-1 [CRITICAL] 6.6.E backend never deployed — PrepareUpload (405) and RetryGitHubMirror (404) unreachable in prod DLL

**Axis:** drift, functional
**Baseline bug ref:** B-4 (rollback artifact)

**Symptom:** The 6.6.E three-step upload flow (prepare → XHR PUT to R2 → publish) is fully implemented in the local repo's `AdminUpdateController.cs` (282 lines), and the deployed frontend JS (April 21 build) calls these endpoints. However the deployed DLL was built on **2026-04-14** — before 6.6.E shipped. PrepareUpload returns HTTP 405 (route doesn't exist; `prepare-upload` is misinterpreted as a guid segment). RetryGitHubMirror returns HTTP 404 (route not registered at all).

**Reproduction steps:**
1. `curl -s -X POST "https://api.auracore.pro/api/admin/updates/prepare-upload" -H "Authorization: Bearer <admin-jwt>" -H "Content-Type: application/json" -d '{"version":"1.9.9","platform":1,"filename":"test.exe","channel":"stable"}'`
   → HTTP 405 (`Allow: DELETE`) — route not found; falls back to `[HttpDelete("{id:guid}")]` matching failing because "prepare-upload" is not a guid.
2. `curl -X POST "https://api.auracore.pro/api/admin/updates/<valid-update-id>/mirror-to-github" -H "Authorization: Bearer <admin-jwt>"`
   → HTTP 404 — route not registered in DLL.
3. DLL state machine check: `strings /var/www/auracore-api/AuraCore.API.dll | grep AdminUpdateController` → only `<Publish>d__2`, `<List>d__3`, `<Delete>d__4`, `<SendDiscordChangelogAsync>d__5` present. No `PrepareUpload`, no `RetryGitHubMirror`, no `MirrorToGitHubInBackgroundAsync`.

**Expected behavior:** "Upload to R2" button calls PrepareUpload → returns presigned URL → frontend PUTs directly to R2 → Publish endpoint finalizes. Retry mirror button calls `POST /{id}/mirror-to-github`.

**Actual behavior:** Every attempt to use the upload flow returns a non-200 response. The "Upload to R2" step fails immediately (405). The "Retry mirror" button silently fails (404 not surfaced to admin — see F-3 / existing UI error handling).

**Root cause:**
- `AdminUpdateController.cs` in local repo: 282 lines — full 6.6.E implementation with `PrepareUpload`, `Publish` (V2 requiring `ObjectKey`), `List`, `Delete`, `RetryGitHubMirror`.
- `/var/www/auracore-api/AuraCore.API.dll`: built 2026-04-14 from a pre-6.6.E codebase that had only `Publish` (V1 taking `DownloadUrl`), `List`, `Delete`.
- The admin panel frontend was rebuilt and deployed on 2026-04-21 (per JS chunk timestamp) but the backend was NOT rebuilt or redeployed. The frontend and backend are now on incompatible API contracts.
- Additionally, `IR2Client` and `IGitHubReleaseMirror` interfaces referenced in the 6.6.E controller are not present in the deployed DLL — they were never registered in the IoC container. Even if routes existed, DI resolution would fail with a 500.

**CTP-6 note:** This is NOT the "stripped by rollback" pattern (CTP-6). The Updates tab controller was greenfield post-rollback — the backup has a 96-line pre-6.6.E version (simple URL-based publish, no R2/GitHub). The 282-line 6.6.E local repo is new code that simply was never deployed.

**DB state verification:**
```sql
-- Confirm DLL mismatch is backend-side (DB has correct rows but old shape)
SELECT "Version", "Channel", "SignatureHash", "IsMandatory", "PublishedAt"
FROM app_updates ORDER BY "PublishedAt" DESC;
-- Returns 3 rows with no Platform or GitHubReleaseId columns
-- Consistent with pre-migration schema
```

**Fix suggestion:**
- Rebuild and redeploy the backend DLL from the current local repo (includes IR2Client, IGitHubReleaseMirror services).
- Apply the `AddPlatformToAppUpdate` migration before or simultaneously with the redeploy (see F-2).
- Also set R2 and GitHub env vars in `/etc/auracore-api.env` before redeploying (per ops doc `docs/ops/release-pipeline-setup.md`).

**Risk if unfixed:**
- The entire 6.6.E release pipeline feature is non-functional in production. Admin cannot upload any new update through the panel.
- The "Upload to R2" button returns an opaque error to the admin with no recovery path.
- The Publish (V2) endpoint also doesn't exist — even direct API calls would fail.

---

### F-2 [HIGH] `AddPlatformToAppUpdate` EF migration not applied — `Platform` and `GitHubReleaseId` columns absent from prod DB (CTP-9 confirmed)

**Axis:** code-db-sync, drift
**Cross-tab pattern ref:** CTP-9 (EF unique index migration gap — confirmed for `app_updates`)

**Symptom:** The deployed admin panel shows a "Platform" column header in the Updates table but all rows show blank platform values. The `List` API response returns no `platform` or `gitHubReleaseId` fields. The `__EFMigrationsHistory` table is empty — no EF migrations have ever been applied to the production DB.

**Reproduction steps:**
1. `psql -c "\d app_updates"` → columns: Id, Version, Channel, ReleaseNotes, BinaryUrl, SignatureHash, IsMandatory, PublishedAt, delta_url, delta_size. No Platform, no GitHubReleaseId.
2. `SELECT indexname FROM pg_indexes WHERE tablename='app_updates';` → only `app_updates_pkey`. The unique index `IX_app_updates_Version_Channel_Platform` is absent.
3. `SELECT "MigrationId" FROM "__EFMigrationsHistory";` → 0 rows.
4. `GET /api/admin/updates` → response objects contain no `platform` or `gitHubReleaseId` keys.
5. Updates table in admin panel → Platform column blank for all rows.

**Expected behavior:** `app_updates` table has `Platform integer`, `GitHubReleaseId varchar(64)`, and composite unique index on `(Version, Channel, Platform)`.

**Actual behavior:** Table has neither column, no unique index (only PK). All existing rows (v1.1.0, v1.2.0, v1.5.0) have no platform association.

**Root cause:**
- Migration `20260421025345_AddPlatformToAppUpdate.cs` exists in local repo and adds: `Platform` column, `GitHubReleaseId` column, drops old `IX_app_updates_Version_Channel` index, creates `IX_app_updates_Version_Channel_Platform` unique index, and inserts v1.6.0 Windows backfill row.
- Migration was never applied to prod DB (`__EFMigrationsHistory` is empty — DB was bootstrapped via raw DDL in a `20260421025305_InitialCreate.cs` equivalent but no subsequent migrations ran).
- Consequence: the unique deduplication guard referenced in `AdminUpdateController.cs:93-95` (the race-guard re-check at Publish time) correctly does an application-level AnyAsync check, but the DB-level unique index doesn't back it up. Two concurrent Publish calls for the same (Version, Channel, Platform) would result in duplicate rows if the AnyAsync check doesn't protect them in time.
- Additionally, the v1.6.0 Windows backfill row (the migration's seed INSERT) was never created. Desktop clients on v1.6.0 checking for updates will find no canonical record.

**DB state verification:**
```sql
-- CTP-9 check for app_updates
SELECT tablename, indexname, indexdef
FROM pg_indexes WHERE tablename='app_updates' ORDER BY indexname;
-- Result: only app_updates_pkey (Id). No IX_app_updates_Version_Channel_Platform.

-- __EFMigrationsHistory check
SELECT COUNT(*) FROM "__EFMigrationsHistory";
-- Result: 0
```

**Fix suggestion:**
- Apply the migration: `dotnet ef database update AddPlatformToAppUpdate` (from the API project root with the DB connection string).
- Alternatively, apply manually:
  ```sql
  ALTER TABLE app_updates ADD COLUMN "Platform" integer NOT NULL DEFAULT 1;
  ALTER TABLE app_updates ADD COLUMN "GitHubReleaseId" varchar(64);
  DROP INDEX IF EXISTS "IX_app_updates_Version_Channel";
  CREATE UNIQUE INDEX "IX_app_updates_Version_Channel_Platform"
    ON app_updates ("Version", "Channel", "Platform");
  INSERT INTO app_updates ("Id","Version","Channel","Platform","ReleaseNotes","BinaryUrl","SignatureHash","IsMandatory","PublishedAt","GitHubReleaseId")
  SELECT gen_random_uuid(),'1.6.0','stable',1,'Legacy Windows release migrated from GitHub.',
    'https://github.com/edutuvarna/AuraCorePro/releases/download/v1.6.0/AuraCorePro-Setup.exe',
    '',false,'2026-01-15T00:00:00Z'::timestamptz,NULL
  WHERE NOT EXISTS (SELECT 1 FROM app_updates WHERE "Version"='1.6.0' AND "Platform"=1);
  ```
- Coordinate with F-1 fix (backend redeploy) — apply migration first, then deploy new DLL.

**Risk if unfixed:**
- Admin cannot publish any update via the 6.6.E flow (would runtime-crash on `Platform` column not found even if F-1 were fixed).
- No DB-level deduplication guard for duplicate (Version, Channel, Platform) rows.
- v1.6.0 backfill row absent — desktop auto-update checker cannot serve a canonical download URL for v1.6.0.

---

### F-3 [MEDIUM] Delete confirmation dialog shows `(undefined)` for platform — missing Platform in API response

**Axis:** UX, code-db-sync

**Symptom:** Clicking the delete (trash) button on any update row shows a browser `confirm()` dialog: `"Delete v1.5.0 (undefined)?"`. The platform is `undefined` because the API response does not include the `platform` field.

**Reproduction steps:**
1. Navigate to Updates tab
2. Click any trash icon (🗑) button on a release row
3. Observe: `confirm("Delete v1.5.0 (undefined)?")` dialog appears
4. Expected: `confirm("Delete v1.5.0 (Windows)?")`

**Root cause:**
- The deployed `List` endpoint response (built April 14) does not include `platform` (column doesn't exist in DB — F-2).
- The deployed frontend's delete handler reads `u.platform` from the list data item. Since `u.platform` is `undefined`, it interpolates as `(undefined)`.
- Even after F-1 and F-2 are fixed, the `List` endpoint must project the `Platform` field for the confirm dialog to work correctly.
- Confirmed live: JS console check shows delete confirmation text is `"Delete v1.5.0 (undefined)?"` for all three existing rows.

**Fix suggestion:**
- Fixing F-1 (deploy new backend) + F-2 (apply migration) together resolves this automatically — the `List` endpoint will start returning `platform` once the column exists and the DLL is updated.
- No additional frontend change needed; the interpolation is correct once `u.platform` is populated.

**Risk if unfixed:**
- Admin confusion when deleting updates — the platform context is missing, increasing the risk of deleting the wrong platform's release by mistake.

---

### F-4 [MEDIUM] No frontend validation when all platform checkboxes unchecked before clicking Publish

**Axis:** UX, functional

**Symptom:** The "Publish Update" form has three platform checkboxes (Windows checked by default, Linux unchecked, macOS Coming Soon/disabled). If admin unchecks Windows (the only non-disabled checkbox) and clicks "Publish", no validation error appears, no alert fires, and no network request is made. The button silently does nothing.

**Reproduction steps:**
1. On Updates tab, uncheck the Windows checkbox
2. Fill in Version "1.9.9", leave Release Notes empty
3. Click "Publish"
4. Observe: no alert, no toast, no error message, no network request fired to `prepare-upload`

**Expected behavior:** Frontend shows a validation error: "Select at least one platform".

**Actual behavior:** Silent no-op. Admin has no feedback on why Publish did nothing.

**Root cause:**
- The deployed frontend's Publish flow iterates over selected platform entries. If no platforms are selected, the loop body never executes and no API call is made — but the UI does not surface this as an error.
- `page.tsx` (deployed chunk): the Publish handler checks `selectedPlatforms.length > 0` implicitly (the loop only fires per platform) but does not add an explicit validation message path for the zero-platforms case.
- No `window.alert()` intercept detected during testing; no React state update triggered for the error message `<p>` element.

**Fix suggestion:**
- Add a guard at the top of the Publish click handler:
  ```tsx
  if (selectedPlatforms.length === 0) {
    setMsg('Select at least one platform before publishing.');
    return;
  }
  ```

**Risk if unfixed:**
- Admin confusion: clicks Publish, nothing happens, no explanation. User may repeatedly click thinking the app is broken or the upload hasn't completed.

---

### F-5 [LOW] No audit log for Publish, Delete, RetryGitHubMirror (CTP-2 confirmed for Updates)

**Axis:** security
**Cross-tab pattern ref:** CTP-2 (missing audit log — confirmed in Subscriptions, Users, Licenses, Payments, Devices; now Updates)

**Symptom:** Admin can publish, delete, and retry GitHub mirror for update releases with no entry written to any audit table.

**Root cause:**
- No `admin_audit_log` table exists in the production DB (established by CTP-2 across all prior tabs).
- `AdminUpdateController.cs` has no audit log write in any endpoint (`Publish`, `Delete`, `RetryGitHubMirror`).
- Publishing a mandatory update (forcing all clients to update) is a high-impact admin action — it should be logged with actor, timestamp, version, channel, platform, and isMandatory flag.

**Fix suggestion:**
- Part of the global CTP-2 fix: add `admin_audit_log` table. Wire into Updates controller: log Publish (with full update metadata) and Delete (with version/platform). RetryGitHubMirror can log at lower priority.

**Risk if unfixed:**
- Admin can publish a malicious or accidental mandatory update forcing all clients to self-update with no audit trail.
- No way to attribute a published release to a specific admin account.

---

### F-6 [LOW] R2 and GitHub token env vars absent from `/etc/auracore-api.env` — Publish will fail at IR2Client level

**Axis:** drift, functional

**Symptom:** Even after F-1 (backend redeploy) and F-2 (migration applied), the new `AdminUpdateController` will fail at the `PrepareUpload` step because `IR2Client` needs Cloudflare R2 credentials (`R2_ACCOUNT_ID`, `R2_ACCESS_KEY_ID`, `R2_SECRET_ACCESS_KEY`, `R2_BUCKET`) and `IGitHubReleaseMirror` needs `GITHUB_TOKEN` — none of which are in `/etc/auracore-api.env`.

**Reproduction steps:**
```bash
grep 'R2_\|GITHUB\|AWS_' /etc/auracore-api.env
# Result: (empty — no R2 or GitHub env vars present)
grep 'DISCORD' /etc/auracore-api.env
# Result: DISCORD_UPDATES_ROLE_ID=<HIDDEN>, DISCORD_WEBHOOK_URL=<HIDDEN>
# Discord is set; R2 and GitHub are not.
```

**Root cause:**
- The ops doc `docs/ops/release-pipeline-setup.md` specifies that operators must set R2 and GitHub credentials before the pipeline goes live.
- These were not set when the backend was initially configured.
- This is a known pre-launch gap per Phase 6.6 release-pipeline-complete memory note: "Ops doc at docs/ops/release-pipeline-setup.md — operator must set R2/GitHub env vars + seed `__EFMigrationsHistory` before E2E smoke."

**Fix suggestion:**
- Add to `/etc/auracore-api.env` (per ops doc):
  ```
  R2_ACCOUNT_ID=<cloudflare account id>
  R2_ACCESS_KEY_ID=<r2 access key>
  R2_SECRET_ACCESS_KEY=<r2 secret>
  R2_BUCKET=auracore-releases
  GITHUB_TOKEN=<github PAT with releases:write>
  ```
- Restart `auracore-api` service after setting.
- Pre-launch smoke test: call `PrepareUpload` with a valid token and verify a presigned URL is returned.

**Risk if unfixed:**
- Upload flow returns 500 at IR2Client level (DI registration fails or R2 credentials missing). Frontend would show a generic error toast with no actionable message.
- GitHub mirror silently does nothing (the `MirrorAsync` returns null when token is empty — by design per 6.6.E code review — but the "Retry mirror" button state never updates).

---

## Axis-by-axis coverage

### 1. Functional

- **List view:** Renders correctly. Three existing rows (v1.1.0, v1.2.0, v1.5.0) display Version, Channel, Mandatory, Published date. Platform column blank (F-2). GitHub mirror status column ("Retry mirror" button present for all rows).
- **Publish action (3-step flow):** BROKEN — PrepareUpload (405), Publish V2 (endpoint missing from DLL), RetryGitHubMirror (404). None of the 6.6.E flow endpoints are reachable. (F-1)
- **Delete action:** UI trigger works (trash button + confirm() dialog). Backend DELETE endpoint present in DLL and confirmed functional via DLL strings. Confirm dialog shows `(undefined)` for platform (F-3).
- **No-platform-selected Publish:** Silent no-op — no validation error surfaced to admin. (F-4)
- **Empty state:** Not tested (table always has 3 rows). From source: `EmptyState` with Zap icon renders when `updates.length === 0` — standard pattern, no finding.
- **Bug 3 (Refresh):** No Refresh button on Updates tab — not applicable.

### 2. Code + DB sync

- **API response shape vs frontend expectation:**
  - Deployed DLL returns: `{id, version, channel, releaseNotes, binaryUrl, isMandatory, signatureHash, publishedAt}` (8 fields, no platform, no gitHubReleaseId).
  - 6.6.E frontend expects: `{platform, gitHubReleaseId, publishedAt}` additional fields — Platform column blank in UI, GitHub column shows "Retry mirror" for all rows regardless of mirror state (because `gitHubReleaseId` is always undefined → all rows appear un-mirrored).
- **Migration gap (CTP-9):** `Platform` + `GitHubReleaseId` columns not in DB; `IX_app_updates_Version_Channel_Platform` unique index absent. DB has only PK index. (F-2)
- **SignatureHash invariant:** All 3 existing rows have `SignatureHash = ''` (empty string). This is a known pre-existing gap (pre-6.6.E rows didn't go through R2 SHA256 computation). Desktop `UpdateDownloader.DownloadAsync` fails fast on empty hash. These rows cannot serve as valid self-update targets. This is pre-existing, not a regression introduced by 6.6.E.

```sql
-- Verify DB state
SELECT "Version", "Channel", "SignatureHash" = '' AS empty_hash, "IsMandatory", "PublishedAt"
FROM app_updates ORDER BY "PublishedAt" DESC;
-- v1.5.0 | stable | t | t | 2026-03-26T18:14:35Z
-- v1.2.0 | stable | t | t | 2026-03-23T16:43:58Z
-- v1.1.0 | stable | t | t | 2026-03-22T06:38:16Z
-- All three rows have empty_hash=true — pre-existing, not a 6.6.E regression.
```

### 3. Security

- **Authorization:** `[Authorize(Roles = "admin")]` at class level on `AdminUpdateController` ✓. All routes require admin JWT. Confirmed: `GET /api/admin/updates` without JWT → 401.
- **IDOR:** Update IDs are GUIDs. Single-tenant system. No user-scoped mutation paths. No IDOR risk.
- **CSRF:** Stateless JWT — no CSRF surface.
- **XSS:** Release Notes are displayed in the Updates table as plain text (`{u.releaseNotes}`). React default escaping applies. No `dangerouslySetInnerHTML`.
- **SQL injection:** All queries use EF Core parameterized LINQ — no raw SQL in `AdminUpdateController`. No injection risk.
- **Rate limit:** No rate limiting on admin endpoints (consistent with all prior tabs).
- **Audit log:** Missing for Publish, Delete, RetryGitHubMirror — CTP-2 confirmed (F-5).
- **Nginx basic auth bypass:** Same as all prior tabs — `api.auracore.pro` has no basic auth, but JWT `[Authorize(Roles = "admin")]` gates all endpoints. Direct curl requires valid admin JWT; no bypass without it.
- **Mandatory update footgun:** No confirmation before clicking "Publish" for a mandatory update. Admin could accidentally publish a mandatory release that forces all clients to update without a second confirmation step. Low severity for now (upload flow is broken anyway per F-1), but worth flagging for the fix phase.

### 4. UX

- **Loading state:** No loading spinner while `getUpdates()` fetches on mount. Standard pattern across all tabs.
- **Error state:** Network error path: `api.getUpdates()` catch returns `[]` on error — silent empty table shown. No toast or error message. PrepareUpload failures bubble through the XHR progress handler (not tested live since endpoint is 405), but per source code review, error is shown in a `statusText` div per-platform. Needs post-F-1-fix re-test.
- **Empty state:** `EmptyState` with Zap icon + "No updates published" + "Click 'Publish Update' to create one" — correct.
- **Destructive confirmation:** Delete has `confirm()` dialog ✓ (CTP-4 compliant). Platform shows `(undefined)` (F-3). No confirmation before Publish with `isMandatory=true` — low risk currently, should be addressed in fix phase.
- **`isMandatory` warning:** The checkbox label is "Mandatory update" with no inline warning about client-side impact. Acceptable for now.
- **`fmtDate` / Invalid Date:** All three existing rows display correct dates (`3/26/2026`, etc.) using `new Date(u.publishedAt).toLocaleDateString()`. `publishedAt` is always populated (DB default `now()`). No "Invalid Date" observed for the `u.publishedAt` field. The `fmtDate` helper (in the 6.6.E source) is only used for `publishedAt` — with a non-null DB default, this is safe. No Invalid Date regression observed.
- **macOS "Coming Soon" disabled checkbox:** Correctly disabled in UI — admin cannot interact with it. State does not persist (checkbox is controlled by React state, not persisted to backend). Acceptable.
- **Refresh button:** Not present on Updates tab. Bug 3 not applicable.

### 5. Mobile

CTP-3 applies (same root layout as all tabs — fixed 260px sidebar, no responsive breakpoints):

- **1024px:** Usable. Form (2-col grid + file inputs + textarea + checkboxes) fits. Table (7 columns: Version, Platform, Channel, Mandatory, Published, GitHub, Actions) is tight but scrollable.
- **768px:** Sidebar 260px + content 508px — form and table cramped but visible.
- **375px:** Sidebar 260px + content 115px — essentially unusable (main horizontal overflow confirmed).
- **320px:** Confirmed via JS: `mainScrollWidth=681 > mainClientWidth=298`, `sidebarWidth=260`. Table overflows. The Publish form's 2-column grid (`grid grid-cols-2`) would stack to two items at 298px content width — likely visually broken but not tested at form level.
- **Tap target for file input:** The Windows file input (`<input type="file">`) rendered as a button — tap target size not confirmed at 44px minimum on mobile. Likely below threshold.

The CTP-3 root-layout fix (collapsible sidebar at ≤768px) resolves mobile issues for this tab as it does for all tabs. No Updates-specific mobile finding beyond CTP-3.

### 6. Deployment drift

**Summary of drift state (most significant finding in this tab):**

| Component | Local repo | Deployed |
|---|---|---|
| Backend DLL | 282-line 6.6.E controller (`PrepareUpload`, `Publish V2`, `List`, `Delete`, `RetryGitHubMirror`) | 14 April DLL — pre-6.6.E (only `Publish V1`, `List`, `Delete`). `IR2Client` and `IGitHubReleaseMirror` absent. |
| DB schema | `app_updates` with `Platform`, `GitHubReleaseId`, composite unique index | `app_updates` without `Platform` or `GitHubReleaseId`; only PK index. |
| Frontend JS | Source at origin is March 27 (old URL-based form) | Deployed chunk is April 21 (6.6.E three-step flow) |
| EF Migrations | `__EFMigrationsHistory` has 0 rows | Same — no migrations ever applied |

**Key insight:** The frontend was successfully rebuilt and deployed on April 21 (6.6.E). The backend was NOT. The result is a frontend/backend contract mismatch where the frontend sends API calls that either don't exist in the DLL or rely on DB columns that don't exist.

**CTP-6 scope note:** Updates tab is **greenfield post-rollback** — not "stripped by rollback." The backup (`/root/auracore-src-backup-final-202604122153`) has 96-line pre-6.6.E controller (simple URL-based, no R2/GitHub). The 282-line 6.6.E local repo is entirely new code written after the rollback. The issue is not rollback stripping but rather a failed backend deployment step in the 6.6.E release.

---

## CTP-9 on `app_updates` — confirmed MISSING

```sql
SELECT tablename, indexname, indexdef
FROM pg_indexes WHERE tablename='app_updates' ORDER BY indexname;
```

Result: only `app_updates_pkey`. The composite unique index `IX_app_updates_Version_Channel_Platform` declared in `AuraCoreDbContext.cs:103` is NOT present in production. CTP-9 pattern confirmed for `app_updates`. This is a direct consequence of the migration not being applied (F-2).

---

## CTP-6 scope note — Updates tab is GREENFIELD POST-ROLLBACK

- Backup (April 12): 96-line pre-6.6.E `AdminUpdateController` — simple URL-based Publish + List only.
- Local repo (current): 282-line 6.6.E `AdminUpdateController` — entirely new code.
- Deployed DLL (April 14): pre-6.6.E state (96-line equivalent).
- The Updates tab is not "stripped" — it is a new feature that was implemented but not deployed. This differs from other tabs (Licenses, Devices, Payments) where the rollback removed code that previously existed.

---

## Questions for user

1. **Deployment priority:** F-1 (backend redeploy) + F-2 (migration apply) are both required before the Updates tab works at all. Are these in scope for Phase 6 Item 8 (batch fix), or should they be treated as a hotfix (since the tab appears functional in the UI but is completely broken in the backend)?

2. **v1.6.0 backfill:** The migration seeds a v1.6.0 Windows row with empty SignatureHash. Should this row be given a real SHA256 hash post-migration (the binary is available at `https://github.com/edutuvarna/AuraCorePro/releases/download/v1.6.0/AuraCorePro-Setup.exe`) or left as-is with the known empty-hash limitation?

3. **Mandatory update confirmation:** Should there be a second confirmation step (beyond the existing "isMandatory" checkbox) before publishing a mandatory release — e.g., a modal warning "This will force all clients to update immediately. Continue?"?
