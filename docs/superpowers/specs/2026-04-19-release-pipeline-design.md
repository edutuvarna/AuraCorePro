# Release Pipeline Design — R2 + Admin Panel + Landing + Desktop Update

**Status:** Spec approved (user, 2026-04-19). Next: writing-plans + implementation in fresh session.

**Context:** Mevcut `AdminUpdateController` ve `UpdateController` altyapısı var — `AppUpdate` entity, Postgres tablosu, Discord webhook, public `GET /api/updates/check` endpoint'i. Eksik olan: (a) admin publish sırasında binary dosyanın R2'ye otomatik upload edilmesi (şu an manuel URL paste), (b) landing page download button'larının publish edilen son versiyonu otomatik yansıtması (şu an hardcoded v1.6.0 GitHub link), (c) desktop app'in yeni versiyonu algılayıp indirme + yükleme akışı çalıştırması, (d) GitHub Releases mirror, (e) `download.auracore.pro` custom domain.

**Baseline:** Main HEAD `3af42d6` (Phase 6.5 merge + post-phase hotfixes). 2270/2270 passing. Target after release pipeline ships: ~2290 passing (~+20 new tests for endpoints + client flow + migration).

## Scope

### In scope

1. `AppUpdate` entity + Postgres tablosuna `Platform` enum kolonu (Windows / Linux / macOS)
2. Backend: yeni `POST /api/admin/updates/prepare-upload` (presigned R2 URL mint) + mevcut `POST /api/admin/updates/publish` genişletilmiş (R2 objesini HEAD + GET + SHA256 compute + row insert)
3. Backend: `GET /api/updates/check` endpoint'ine `?platform=` query parameter (default=windows)
4. R2 bucket layout + `download.auracore.pro` custom domain + 7-day lifecycle rule on `pending/` prefix
5. Admin panel (Next.js): "Publish Update" form — version, channel, platform checkbox'ları, per-platform file upload alanı (presigned PUT + progress bar), release notes, mandatory toggle
6. Landing page: `scripts/main.js`'e `fetch('/api/updates/check?platform=...')` + OS-detect primary button + "Other platforms" dropdown
7. Desktop app: yeni `UpdateDownloader` service — banner/modal UI, arka plan indirme + SHA256 verify + installer başlatma
8. GitHub Releases mirror (async, fire-and-forget): binary'ler + release notes + `sha256sums.txt` asset
9. Migration: EF migration + v1.6.0 backfill row (existing GitHub URL'i `BinaryUrl` olarak)
10. **Bundled admin UX fix**: admin.auracore.pro/users sayfasında GUID kolonu + copy-to-clipboard (tier değişikliği için GUID lookup operasyonel sürtüşmesini giderir — sub-phase 6.6.E içinde)

### Out of scope (gelecek iş — spec creep koruyucu)

- **Multi-arch**: x64 dışı (ARM64 Windows, Apple Silicon macOS, ARM Linux). Aynı pipeline'a eklenir ama ayrı release rows, ayrı R2 object keys. Day 1'de ship etmiyoruz.
- **Delta / incremental updates**: Chrome tarzı diff patch indirme. Full installer re-download yeterli AuraCore boyutunda.
- **Cryptographic signing**: Windows Authenticode + macOS Developer ID ayrı spec. Phase 6 Item 6 macOS notarization için; Windows için ayrı bir Authenticode spec'i olabilir. Şimdilik `SignatureHash = SHA256` yeterli (integrity check, not authenticity).
- **Beta/canary channel UI**: Backend `channel` field destekliyor, admin panel şimdilik sadece `stable`'ı form default'u olarak gösteriyor. Beta channel ship istenirse UI genişletilir.
- **Auto-rollback**: "Rollback v1.8.0 to v1.7.5" one-click button. Bunun yerine: admin DELETE /api/admin/updates/{id} mevcut + yeni `IsLatest` flag değiştirme — ama bu karmaşıklık ekler, YAGNI.
- **Download analytics dashboard**: Kaç kişi v1.7.0 indirdi, platform dağılımı. Cloudflare R2 erişim logları ZATEN tutuluyor, ayrı dashboard için ayrı bir iş.
- **Mandatory update'lerin partial rollout**: "Sadece AB kullanıcılarına zorla, ABD'ye opsiyonel". Geolocation gerekiyor, overkill.
- **Auto-update de-activation** (user opt-out): "Ben otomatik güncelleme istemiyorum" toggle. Kod basit ama UX tasarımı ayrı bir iş. Şimdilik herkes banner görür.

## Architecture

### Top-level flow

```
Admin Panel ─── (1) POST /api/admin/updates/prepare-upload
                    {version, platform, filename}
                    ↓
                Backend: duplicate version check + extension whitelist
                         + mint presigned R2 PUT URL (10-min TTL)
                         + R2 object key: pending/<uuid>-<filename>
                    ↓
                response: {uploadUrl, objectKey}

            ─── (2) PUT <uploadUrl> (browser → R2 direct)
                    XHR with progress bar

            ─── (3) POST /api/admin/updates/publish
                    {version, platform, objectKey, releaseNotes,
                     isMandatory, channel}
                    ↓
                Backend:
                  - HEAD r2://<bucket>/<objectKey> → verify + size
                  - Copy R2 object from pending/ to releases/v{ver}/{canonicalName}
                  - GET r2://.../releases/v{ver}/{canonicalName} → SHA256 compute
                  - INSERT AppUpdate row {Platform, BinaryUrl, SignatureHash, ...}
                  - Fire Discord webhook (existing)
                  - Fire-and-forget Task: GitHub Releases mirror
                    ↓
                AppUpdate committed, PublishedAt = NOW

Public consumers:

Landing (auracore.pro) ── GET /api/updates/check?currentVersion=0.0.0&platform=windows
                         (detectOS via navigator.platform)
                          ↓
                       .download-link[href] güncellenir
                       dropdown Linux/macOS (coming soon) seçeneklerini listeler

Desktop App ────────── (on startup) GET /api/updates/check?currentVersion=X.Y.Z&platform=<os>
                         ↓
                     banner/modal (mandatory'e göre)
                     user "Şimdi güncelle" → GET <BinaryUrl>
                     → %TEMP%\<file> → SHA256 verify → Process.Start(installer)
                     → Environment.Exit(0)
```

### Data model

**`AppUpdate` entity değişiklikleri** (`src/Backend/AuraCore.API.Domain/Entities/AppUpdate.cs`):

```csharp
public enum AppUpdatePlatform
{
    Windows = 1,
    Linux = 2,
    MacOS = 3,
}

public class AppUpdate
{
    public Guid Id { get; set; }
    public string Version { get; set; }            // "1.7.0"
    public string Channel { get; set; }            // "stable" | "beta" | "canary"
    public AppUpdatePlatform Platform { get; set; } // NEW
    public string? ReleaseNotes { get; set; }      // markdown
    public string BinaryUrl { get; set; }          // https://download.auracore.pro/releases/v1.7.0/AuraCorePro-Windows-v1.7.0.exe
    public string SignatureHash { get; set; }      // SHA256 hex
    public bool IsMandatory { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
}
```

**Postgres index değişiklikleri:**

- Drop existing `(Version, Channel)` unique constraint (if present)
- Add `(Version, Channel, Platform)` composite unique constraint — aynı versiyonu 3 platformda ayrı publish edebilmek için

**EF migration (adı: `AddPlatformToAppUpdate`):**

```csharp
migrationBuilder.AddColumn<int>(
    name: "Platform",
    table: "AppUpdates",
    type: "integer",
    nullable: false,
    defaultValue: 1);  // existing rows default to Windows

migrationBuilder.CreateIndex(
    name: "IX_AppUpdates_Version_Channel_Platform",
    table: "AppUpdates",
    columns: new[] { "Version", "Channel", "Platform" },
    unique: true);
```

**v1.6.0 backfill (migration'ın up() sonuna):**

```csharp
migrationBuilder.Sql(@"
    INSERT INTO ""AppUpdates""
    (""Id"", ""Version"", ""Channel"", ""Platform"", ""ReleaseNotes"", ""BinaryUrl"",
     ""SignatureHash"", ""IsMandatory"", ""PublishedAt"")
    SELECT gen_random_uuid(), '1.6.0', 'stable', 1,
           'Legacy Windows release migrated from GitHub.',
           'https://github.com/edutuvarna/AuraCorePro/releases/download/v1.6.0/AuraCorePro-Setup.exe',
           '', false, '2026-01-15T00:00:00Z'::timestamptz
    WHERE NOT EXISTS (
        SELECT 1 FROM ""AppUpdates"" WHERE ""Version"" = '1.6.0' AND ""Platform"" = 1
    );
");
```

### R2 bucket yapısı

```
bucket: auracore-releases (mevcut bucket'ta alt klasör ya da yeni bucket — kullanıcı kararı)
├─ releases/
│  ├─ v1.7.0/
│  │  ├─ AuraCorePro-Windows-v1.7.0.exe
│  │  └─ AuraCorePro-Linux-v1.7.0.deb
│  ├─ v1.7.1/
│  │  └─ ...
│  └─ v1.6.0/                         (opsiyonel; backfill GitHub'a yönlendiriyor, kopyalanmıyor)
└─ pending/                           (geçici upload dizini)
   └─ <uuid>-<original-filename>
   # lifecycle rule: 7 gün sonra otomatik silme
   # Cloudflare dashboard → R2 → bucket → Lifecycle Rules
```

**Custom domain: `download.auracore.pro`**
- Cloudflare dashboard → R2 → bucket → Settings → Custom Domains → Add `download.auracore.pro`
- DNS otomatik CNAME önerisi — Cloudflare DNS'te tek tık onay
- SSL otomatik (Cloudflare edge cert)
- Public-read policy; direct URL format: `https://download.auracore.pro/releases/v1.7.0/AuraCorePro-Windows-v1.7.0.exe`

**R2 credential'ları:** backend'de env variables olarak (`R2_ACCOUNT_ID`, `R2_ACCESS_KEY_ID`, `R2_SECRET_ACCESS_KEY`, `R2_BUCKET`). `/etc/auracore-api.env`'de, `appsettings.json`'da değil (secret leak koruması).

### Backend endpoints

#### Yeni: `POST /api/admin/updates/prepare-upload`

```csharp
[HttpPost("prepare-upload")]
[Authorize(Roles = "admin")]
public async Task<IActionResult> PrepareUpload([FromBody] PrepareUploadRequest req, CancellationToken ct)
{
    // Validate
    if (string.IsNullOrWhiteSpace(req.Version) || !SemverRegex.IsMatch(req.Version))
        return BadRequest(new { error = "Invalid version (semver: X.Y.Z)" });

    if (!Enum.IsDefined(typeof(AppUpdatePlatform), req.Platform))
        return BadRequest(new { error = "Invalid platform" });

    var ext = Path.GetExtension(req.Filename).ToLowerInvariant();
    var allowedExts = req.Platform switch
    {
        AppUpdatePlatform.Windows => new[] { ".exe", ".msi" },
        AppUpdatePlatform.Linux => new[] { ".deb", ".rpm", ".tar.gz", ".AppImage" },
        AppUpdatePlatform.MacOS => new[] { ".dmg", ".pkg" },
        _ => Array.Empty<string>()
    };
    if (!allowedExts.Contains(ext))
        return BadRequest(new { error = $"Invalid extension for {req.Platform}: {ext}" });

    // Check duplicate
    var channel = req.Channel ?? "stable";
    var duplicate = await _db.AppUpdates.AnyAsync(u =>
        u.Version == req.Version && u.Channel == channel && u.Platform == req.Platform, ct);
    if (duplicate)
        return Conflict(new { error = $"v{req.Version} already exists for {req.Platform} in channel {channel}" });

    // Mint presigned URL
    var objectKey = $"pending/{Guid.NewGuid():N}-{Path.GetFileName(req.Filename)}";
    var presignedUrl = await _r2.GeneratePresignedPutUrlAsync(objectKey,
        TimeSpan.FromMinutes(10), maxSizeBytes: 500 * 1024 * 1024, ct);

    return Ok(new { uploadUrl = presignedUrl, objectKey });
}

public record PrepareUploadRequest(string Version, AppUpdatePlatform Platform, string Filename, string? Channel);
```

#### Değişiyor: `POST /api/admin/updates/publish`

```csharp
[HttpPost("publish")]
[Authorize(Roles = "admin")]
public async Task<IActionResult> Publish([FromBody] PublishUpdateRequestV2 req, CancellationToken ct)
{
    // Validate (same shape as before plus objectKey + platform)
    if (string.IsNullOrWhiteSpace(req.Version) || string.IsNullOrWhiteSpace(req.ObjectKey))
        return BadRequest(new { error = "Version and ObjectKey are required" });

    var channel = req.Channel ?? "stable";

    // Duplicate check (race condition guard — prepare-upload already checked but time may have passed)
    var duplicate = await _db.AppUpdates.AnyAsync(u =>
        u.Version == req.Version && u.Channel == channel && u.Platform == req.Platform, ct);
    if (duplicate)
        return Conflict(new { error = $"v{req.Version} already exists" });

    // Verify R2 object exists + size sane
    var headResp = await _r2.HeadObjectAsync(req.ObjectKey, ct);
    if (headResp is null)
        return BadRequest(new { error = "Object not uploaded yet — PUT to uploadUrl first" });
    if (headResp.Size < 10_000 || headResp.Size > 500_000_000)
        return BadRequest(new { error = $"Invalid size: {headResp.Size} bytes" });

    // Copy from pending/ to releases/v{ver}/<canonical>
    var canonicalName = $"AuraCorePro-{req.Platform}-v{req.Version}{Path.GetExtension(req.ObjectKey)}";
    var finalKey = $"releases/v{req.Version}/{canonicalName}";
    await _r2.CopyObjectAsync(req.ObjectKey, finalKey, ct);
    await _r2.DeleteObjectAsync(req.ObjectKey, ct);  // cleanup pending/ version

    // Compute SHA256 from final location
    var sha256 = await _r2.ComputeSha256Async(finalKey, ct);

    // Insert
    var update = new AppUpdate {
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

    // Fire-and-forget: Discord (existing) + GitHub mirror (new)
    _ = SendDiscordChangelogAsync(update);
    _ = MirrorToGitHubAsync(update, finalKey);

    return Ok(new { message = $"v{update.Version} ({update.Platform}) published", update });
}

public record PublishUpdateRequestV2(
    string Version,
    AppUpdatePlatform Platform,
    string ObjectKey,
    string? ReleaseNotes,
    string? Channel,
    bool IsMandatory
);
```

#### Değişiyor: `GET /api/updates/check`

- `?platform=windows|linux|macos` query parameter eklenir (default=windows for back-compat)
- DB query filter'ı `.Where(u => u.Platform == platform)` ekler

### Bundled admin panel UX fix — Users page GUID display

Aynı sub-phase 6.6.E admin panel iş paketine dahildir (scope drift değil, tek paket):

**Problem:** `admin.auracore.pro/users` sayfasında kullanıcı listesi email/role/tier/joined kolonlarını gösteriyor, ama **GUID (Id)** görünmüyor. Admin tier değişikliği yapmak istediğinde (backend endpoint'leri GUID bekliyor) GUID'yi elde etmek için DB'ye manuel `psql` sorgusu çekmek zorunda kalıyor — operasyonel sürtüşme.

**Fix:** Users table'ına GUID kolonu + copy-to-clipboard butonu:

- **Yeni kolon: "ID"** — shortened GUID gösterir (ilk 8 karakter + ellipsis, örn. `d36c4b70…`)
- **Hover tooltip:** Full GUID'yi tooltip ile göster
- **Click-to-copy:** Shortened GUID hücresine tıklayınca full GUID clipboard'a kopyalanır, 2sn'lik "Copied!" toast gösterilir
- Mevcut Actions kolonundaki disable + delete ikonları dokunulmaz

**Layout (mevcut + yeni kolon):**
```
USER                         ID           ROLE    TIER    JOINED       ACTIONS
baconungabunga@gmail.com     d36c4b70… 📋 user    FREE    4/20/2026    🚫 🗑
sql'--@test.com              b82f1e55… 📋 user    FREE    4/13/2026    🚫 🗑
```

**Implementation:** Next.js component değişikliği tek dosyada (`admin-panel/src/pages/users/...` veya exported static'teki ilgili JS bundle — karar sub-phase 6.6.E'de netleşir, admin panel repo'sunu incelendiğinde). Backend `GET /api/admin/users` endpoint'i zaten `Id`'yi dönüyor (büyük ihtimalle; implementation sırasında doğrulanır), sadece frontend rendering eksik.

**Test:** Manuel — admin panel'e gir, Users'ı aç, ID kolonunu gör, tıkla kopyala, başka bir yerde paste ile doğrula.

---

### Admin panel flow (Next.js)

**New page:** `admin.auracore.pro/updates/publish`

Form alanları:
1. **Version** (text, required, semver regex pattern client-side validate)
2. **Channel** (dropdown: stable / beta / canary, default stable)
3. **Platforms** (checkbox group):
   - [x] Windows (always enabled, pre-checked)
   - [x] Linux
   - [ ] macOS (disabled, label: "Coming Soon — requires Developer ID")
4. **Per-checked-platform**: file upload alanı + progress bar
   - JS: her platform için paralel flow:
     1. `POST /api/admin/updates/prepare-upload` → `{ uploadUrl, objectKey }` al
     2. XHR `PUT uploadUrl` + dosya body + `progress` event listener → UI'da %X gösteri
     3. Upload biter → localState'e `{platform, objectKey}` kaydet
5. **Release Notes** (markdown textarea)
6. **Mandatory** (toggle, default false, yanında warning: "Kullanıcıyı update yapana kadar uygulamadan kilitler — sadece kritik fix'ler için")
7. **[Publish]** button (disabled until all checked platforms finish upload):
   - Her platform için `POST /api/admin/updates/publish` çağrısı (paralel)
   - Hepsi 200 → "Released!" toast + redirect to `/updates` list
   - Herhangi biri fail → toast error + detay göster, yarıda kalmış partial state ok (lifecycle rule 7 günde cleanup)

### Landing integration

**`landing-page-work/scripts/main.js`** değişiklikleri:

```js
// Sayfa yüklenince OS tespit + latest version fetch
function detectOS() {
  const p = navigator.platform.toLowerCase();
  const ua = navigator.userAgent.toLowerCase();
  if (p.includes('win') || ua.includes('windows')) return 'windows';
  if (p.includes('linux') || ua.includes('linux')) return 'linux';
  if (p.includes('mac') || ua.includes('macintosh')) return 'macos';
  return 'windows';  // default
}

async function loadLatestRelease() {
  const os = detectOS();
  try {
    // Parallel: primary OS + Linux (for dropdown)
    const [primary, linux] = await Promise.all([
      fetch(`/api/updates/check?currentVersion=0.0.0&platform=${os}`).then(r => r.json()),
      os !== 'linux'
        ? fetch('/api/updates/check?currentVersion=0.0.0&platform=linux').then(r => r.json())
        : Promise.resolve(null)
    ]);

    // Primary button
    if (primary.updateAvailable) {
      document.querySelectorAll('.download-link').forEach(a => a.href = primary.binaryUrl);
      document.querySelectorAll('.download-version').forEach(el => el.textContent = `v${primary.version}`);
      document.querySelectorAll('.download-platform').forEach(el => el.textContent = displayOS(os));
    }

    // "Other platforms" dropdown
    populateOtherPlatformsDropdown(os, primary, linux);
  } catch (err) {
    console.warn('Release API unavailable, using fallback GitHub link', err);
    // .download-link href'i HTML'de zaten hardcoded v1.6.0 GitHub — fallback olarak kalır
  }
}

function displayOS(os) {
  return { windows: 'Windows', linux: 'Linux', macos: 'macOS' }[os];
}

document.addEventListener('DOMContentLoaded', loadLatestRelease);
```

**HTML değişiklikleri (`index.html`):**

- `<a href=".../v1.6.0/...">` → class eklenir: `<a class="download-link" href=".../v1.6.0/...">` (fallback URL korunur)
- Primary button içinde: `<span class="download-platform">Windows</span> <span class="download-version">v1.6.0</span>` gibi dinamik metin için span'ler eklenir
- Primary button altına: `<details><summary>Other platforms ↓</summary><ul id="otherPlatformsList"></ul></details>`
- JS `populateOtherPlatformsDropdown` bu `<ul>`'yi doldurur: Windows (link), Linux (link), macOS (disabled, "Coming Soon — macOS Developer ID required")

### Desktop client update flow

**Yeni service:** `src/UI/AuraCore.UI.Avalonia/Services/Update/UpdateDownloader.cs`

```csharp
public interface IUpdateDownloader
{
    /// <summary>
    /// Downloads the installer to %TEMP%, verifies SHA256, returns local path.
    /// </summary>
    Task<string> DownloadAsync(AvailableUpdate update, IProgress<double> progress, CancellationToken ct);

    /// <summary>
    /// Launches the installer and exits the current app.
    /// </summary>
    void InstallAndExit(string installerPath);
}
```

**`UpdateChecker` değişiklikleri:**

1. Startup: `_ = Task.Run(CheckAsync)` 24h cadence
2. `CheckAsync` → `GET /api/updates/check?currentVersion={current}&platform={detected}` (hangi OS'tayız?)
3. Response `updateAvailable=true` → `Dispatcher.UIThread.Post(ShowUpdateBanner)`
4. Banner/modal UI (mandatory flag'e göre):
   - Non-mandatory: `MainWindow` header altında banner `UpdateBanner.axaml` — "v1.7.0 mevcut. [Şimdi güncelle] [Daha sonra]"
   - Mandatory: Modal dialog, user başka pencere açamaz — "Bu güncelleme zorunlu. [Şimdi güncelle]" (daha sonra butonu yok; `Environment.Exit(0)` dismissede)
5. "Şimdi güncelle" → `UpdateDownloader.DownloadAsync`:
   - HttpClient GET `BinaryUrl`, stream → `%TEMP%\AuraCorePro-Setup-v1.7.0.exe`
   - Progress bar dialog
   - İndirme biter → SHA256 compute → `SignatureHash` ile karşılaştır
   - Uyuşmazsa: dosyayı sil, error dialog "Hash mismatch, retry?" + restart
6. Download başarılı → confirm dialog: "İndirme tamamlandı (vX.Y.Z). Yükleyiciyi şimdi çalıştır? [Evet] [Daha sonra]"
7. Evet → `Process.Start(installerPath)` + `Environment.Exit(0)`

**Test stratejisi:**
- `UpdateDownloader` constructor'a `HttpClient` inject + sampler için test mock'u
- `UpdateCheckerTests`: mock API cevabına göre banner göster/gösterme
- `UpdateDownloaderTests`: mock HttpClient, mock progress, verify SHA256 check + file cleanup on mismatch

### GitHub mirror

**Yeni:** `src/Backend/AuraCore.API/Services/GitHubReleaseMirror.cs`

```csharp
public interface IGitHubReleaseMirror
{
    Task MirrorAsync(AppUpdate primaryUpdate, List<AppUpdate> allPlatformsForThisVersion, CancellationToken ct);
}
```

Akış:
1. Octokit.NET client (`ASPNETCORE_GITHUB_TOKEN` env var, fine-grained PAT scope=`contents:write` for edutuvarna/AuraCorePro)
2. `POST /repos/edutuvarna/AuraCorePro/releases`:
   - `tag_name`: `v{version}`
   - `name`: `AuraCore Pro v{version}`
   - `body`: primaryUpdate.ReleaseNotes (markdown)
3. Her platform binary için:
   - R2'den stream (`GetObjectAsync`) → byte[] 
   - GitHub release asset upload (`UploadAsset`)
4. `sha256sums.txt` generate ve asset olarak upload:
   ```
   {sha256}  AuraCorePro-Windows-v1.7.0.exe
   {sha256}  AuraCorePro-Linux-v1.7.0.deb
   ```
5. Admin panel list'te her row yanında `GitHubSynced` boolean göster (publish sonrası async tamamlandığında true olur; DB'ye yeni kolon eklenebilir veya runtime check ile belirlenir — **karar: yeni kolon `AppUpdate.GitHubReleaseId` (nullable string)**)

**Failure handling:** Mirror throw ederse ana publish yine de başarılı. Admin panel banner: "GitHub mirror failed, click to retry" (manual retry endpoint: `POST /api/admin/updates/{id}/mirror-to-github`).

**Açıkça yönetilmeyen:** Eski release'leri auto-delete/edit. Mirror sadece yeni release'ler için.

### Migration path

1. **EF migration**: `AddPlatformToAppUpdate` → `Platform` kolonu + unique index + v1.6.0 seed SQL
2. **Manuel adım (user)**: R2 bucket'ta `pending/` prefix'ine 7-day lifecycle rule ekle; `download.auracore.pro` custom domain bağla
3. **Manuel adım (user)**: GitHub PAT oluştur (fine-grained, `contents:write` scope), `ASPNETCORE_GITHUB_TOKEN` olarak `/etc/auracore-api.env` dosyasına ekle + systemd restart
4. **First release (v1.7.0 publish)**: admin panel'den normal akış. Discord webhook + GitHub mirror otomatik tetiklenir; manual verification: https://github.com/edutuvarna/AuraCorePro/releases'te görünmeli
5. **Landing cold-cache**: Cloudflare edge cache (30 dk) nedeniyle ilk fetch sonrası yeni JS + yeni API response yansıması 30 dk'ya kadar gecikebilir. Purge gerekirse `wrangler cache purge` veya Cloudflare dashboard

## Testing strategy

**Backend unit tests** (~8 yeni test):
- `PrepareUpload`: duplicate version reddet, geçersiz extension reddet, geçerli request mint URL döner
- `Publish`: duplicate race guard, R2 HEAD fail → 400, size <10KB → 400, SHA256 compute doğru, copy from pending/ to releases/ doğru, Discord + GitHub fire-and-forget tetiklenir
- `Check`: `?platform=` filter çalışıyor, platform belirtilmezse windows default

**Backend integration tests** (~3 yeni test):
- Full prepare → mock R2 upload → publish → DB row görünür + Discord mock çağrıldı
- v1.6.0 backfill migration up+down roundtrip

**Desktop client tests** (~4 yeni test):
- `UpdateChecker`: API `updateAvailable=true` → banner gösterilir
- `UpdateChecker`: IsMandatory=true → modal, "Daha sonra" disabled
- `UpdateDownloader`: SHA256 mismatch → file silinir, exception throw edilir
- `UpdateDownloader`: başarılı akış → installer path döner, `Process.Start` çağrısı verify edilir

**Landing tests:** Manual — preview sunucusunda OS spoofing ile Windows/Linux/macOS user-agent simülasyonu, primary button doğru URL'i gösteriyor mu kontrol.

**E2E smoke test (manuel, release sonrası):**
- Admin panel'den v1.7.0 publish (fake installer, 100KB test .exe)
- https://download.auracore.pro/releases/v1.7.0/AuraCorePro-Windows-v1.7.0.exe erişilebilir
- https://auracore.pro/ open → primary button link yeni URL'e güncellemiş
- Desktop app (dev build, versiyon 1.6.5 mock) → banner görmeli
- GitHub https://github.com/edutuvarna/AuraCorePro/releases/v1.7.0 sayfa var + binary'ler + sha256sums.txt

## Open questions / known risks

- **R2 bucket isim kararı**: `auracore-releases` yeni bucket mi, yoksa mevcut AI models bucket'ta `releases/` alt klasör mü? **Karar kullanıcı tarafında**, implementation config-driven (env var `R2_BUCKET`).
- **GitHub PAT rotation**: fine-grained PAT default 1 yıl expire. Expire öncesi rotation prosedürü: yeni PAT oluştur → `/etc/auracore-api.env` güncelle → systemd restart. 1 yıllık dev-ops TODO — takvime koyulmalı.
- **R2 SDK**: AWS SDK for .NET (`AWSSDK.S3` NuGet) — R2 S3-compatible, native destekler. Veya `Minio.NET`. Tavsiye: `AWSSDK.S3` (tek dev-dep, industry standart).
- **Octokit.NET** yeni NuGet — backend csproj'e eklenir (~10MB dep).
- **Network failure mid-upload**: presigned URL 10-dk TTL, admin panel retry'da yeni URL ister (prepare endpoint tekrar çağrılır; backend race-check bypass şu an yok → admin panel same-session içinde yeni prepare de yapabiliyor, duplicate DB row create etmiyor çünkü publish finalize'de check var).
- **Concurrent publish attempts**: iki admin aynı anda v1.7.0 publish yapsa, DB composite unique constraint'i ilkinin kazanmasını garanti eder, ikincisi 409 Conflict alır. Safe.

## Decision log (WHY chose what — gelecek reviewer için)

| Karar | Seçilmiş | Reddedilen alternatif | Neden |
|---|---|---|---|
| Upload mimarisi | Hybrid (prepare→upload→finalize) | Backend-proxied, Direct-to-R2 pure | A'nın güvenilirliği (backend-side SHA256, validation) + B'nin hızı (zero backend bandwidth, native progress bar). Industry standart (AWS, Dropbox, Slack, GitHub). |
| Platform scope day 1 | Windows + Linux ship, macOS "Coming Soon" | Windows-only başla | Linux gerçek durumda hazır: 8 modül non-stub, `build-linux.sh` çalışıyor, privilege helper var. Day 1 ship + sonraki refactor zahmetinden kurtulmak için. |
| Desktop update UX | Active download + confirm (B) | Passive notification, Silent auto-install, Install-on-next-launch | Modern desktop standardı (VS, Cursor, Discord). System optimizer trust zorunlu → silent NO, install-on-exit Avalonia'da güvenilir değil. |
| Public download (no auth gate) | Evet | Pro-only download | Conversion killer; indirme ücretsiz olmalı, gating app-içi tier check'te zaten var. |
| GitHub mirror | Full (binary + notes + sha256sums) | Binary-only, No mirror | Trust factor (sistem optimizer için kritik), SEO, developer discoverability, zero-cost fallback CDN. |
| v1.6.0 backfill | Evet, migration'da seed | Skip | UpdateChecker bugünden çalışır, landing tutarlı state, history complete. |
| Landing download UX | OS-detect primary + "Other platforms" dropdown | 3 equal buttons, single download + OS list | Modern pattern (Cursor/VS/Discord), %95 user'ın istediği OS için 1-click. |
| `IsMandatory` mekanizması | Korunuyor (zaten çalışıyor) | Yeni flag / kaldır | Mevcut sistem OK, security/critical hotfix için araç gerekli. |

## Non-goals

- Yeni test framework kurulumu (mevcut xUnit yeterli)
- Yeni monitoring/telemetry altyapısı (mevcut Discord webhook yeterli)
- Admin panel'in tamamen yeniden yazılması (mevcut Next.js static export'a form eklenir)
- R2 SDK dışında başka cloud provider desteği (S3, Azure Blob) — R2 commit, multi-cloud out of scope

---

## Implementation sub-phases (next-session writing-plans önizleme)

Plan yazımı sırasında kullanılacak sub-wave bölümleri için:

1. **6.6.A — Data layer:** EF migration + entity update + v1.6.0 backfill + unit test
2. **6.6.B — R2 client:** `IR2Client` abstraction + `AwsR2Client` impl + presigned URL + HEAD/GET/COPY/DELETE + SHA256 helper + test (mock S3)
3. **6.6.C — Backend endpoints:** `prepare-upload` yeni + `publish` refactor + `check` platform param + integration tests
4. **6.6.D — GitHub mirror:** Octokit service + async task + env var + retry endpoint + test
5. **6.6.E — Admin panel UI:** Next.js form + presigned-PUT upload + progress bar + multi-platform checkbox + publish wiring **+ bundled: Users page GUID column + copy-to-clipboard**
6. **6.6.F — Landing integration:** JS fetch + OS-detect + dropdown + deploy to origin
7. **6.6.G — Desktop update flow:** `UpdateDownloader` + banner/modal UI + `UpdateChecker` platform param + SHA256 verify + installer launch + tests
8. **6.6.H — Manual ops docs:** R2 custom domain + lifecycle rule + GitHub PAT setup + env var cheatsheet → `docs/ops/release-pipeline-setup.md`
9. **6.6.I — E2E smoke + ceremonial close:** test release (v1.7.0-rc1 fake), verify tüm akış + memory + merge

Tahmini effort: sub-phase başına 2-4 saat, toplam ~20-30 saat (2-3 session'a yayılır).

---

**Spec sonu.** Writing-plans skill'i sonraki session'da invoke edilir, bu dokümana referans verir, 9 sub-phase'i task'lara böler.
