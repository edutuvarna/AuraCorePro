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

   -- Migration timestamp from src/Backend/AuraCore.API.Infrastructure/Migrations/
   INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
   VALUES ('20260421025305_InitialCreate', '8.0.11')
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

## 6. Rollback

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

## 7. Known runtime gaps (operator must be aware)

### 7.1 `signatureHash` database invariant

Every `AppUpdates` row with `updateAvailable=true` **MUST** have a non-empty 64-character lowercase hex `SignatureHash`.

**Impact:** v1.6.0 clients cannot self-update to v1.7.0+ without this hash. Desktop clients will error: "Update has no signature hash; aborting for safety."

**How to compute SHA256 of an installer:**
- Windows: `certutil -hashfile C:\path\to\AuraCorePro-Setup.exe SHA256`
- Linux: `sha256sum AuraCorePro-Setup-Linux.deb`
- macOS: `shasum -a 256 AuraCorePro-Setup.dmg`

**How to fix:** Paste the 64-character hex output (no spaces, lowercase) into the `SignatureHash` column. The backend publish flow (6.6.C) auto-computes this from the R2 object, so manual SQL updates should rarely be needed—but if you do need to patch a row:

```sql
UPDATE app_updates
SET "SignatureHash" = '<64-char-hex>'
WHERE "Version" = '1.7.0' AND "Platform" = 'Windows';
```

### 7.2 Mandatory update dialog — Alt+F4 limitation

`MandatoryUpdateDialog` overrides `OnClosing` to cancel the close unless `MarkInstalling()` has been called. This blocks:
- Alt+F4 window close
- Window close button
- Traditional OS close gestures

**Known limitation:** Killing the process via Task Manager or `pkill` still exits the app (expected behavior).

**Documented gap:** No follow-up needed unless enterprise compliance demands kill-resistance; current implementation is sufficient for consumer desktop.

### 7.3 HttpClient User-Agent shared

The desktop app reuses one `HttpClient` singleton across:
- Installer downloads (via `UpdateDownloader`)
- Cloud AI model downloads

**Default User-Agent:** `AuraCorePro/1.0 (+https://auracore.pro)`

Both flows use this UA. Relevant when grepping Cloudflare/origin logs or if WAF rules need per-UA tuning.

### 7.4 Legacy `DownloadUpdateAsync` method in UpdateChecker

Old `UpdateChecker.DownloadUpdateAsync` (lines ~207-260) is **dead code**—no longer called by the new banner/modal flow (6.6.G).

- Old code wrote to `%TEMP%\AuraCorePro-Updates\` subdirectory
- New `UpdateDownloader` writes directly to `%TEMP%` root

**Cleanup debt:** Remove the dead method in a future sweep (not blocking release).

### 7.5 Smoke-test .exe SHA256 computation

For Task 33 E2E smoke test, the operator must compute SHA256 of the dummy test .exe **before uploading**:

```bash
# Windows
certutil -hashfile smoke.exe SHA256

# Linux/macOS
sha256sum smoke.exe
# or
shasum -a 256 smoke.exe
```

Paste the 64-character hex output into the admin panel's publish form. The backend (6.6.C) auto-computes from the R2 object on successful upload, so manual override is typically unnecessary—just upload and let it compute.
