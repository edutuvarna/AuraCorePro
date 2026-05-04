# AuraCore Pro — Changelog

All notable changes to this project will be documented in this file.

---

## v1.8.1 — Phase 6.17 (2026-05-04)

### Fixed
- System Health storage drives no longer show `-2147483648%` on Linux virtual filesystems. Virtual filesystems (`tmpfs`, `proc`, `sysfs`, `devpts`, `securityfs`, `cgroup`, etc.) are filtered from the user-facing list; remaining drives have a zero-capacity guard plus `Math.Clamp(0, 100)` defense-in-depth on the percent calculation.
- Privileged operations (RAM Optimizer, Junk Cleaner, Systemd Manager, Swap Optimizer, Package Cleaner, Journal Cleaner) now surface a clear "Privilege helper required" diagnostic with copyable install command instead of silently no-oping when the privilege helper isn't installed.
- `PrivilegeHelperMissingBanner` now appears at app startup on Linux when the helper isn't installed (was only surfacing after a privileged op had already failed).

### Changed
- `[SupportedOSPlatform("windows")]` attribute now applied to all 7 previously-deferred Windows-only module classes (DefenderManager, FirewallRules, AppInstaller, DriverUpdater, GamingMode, StorageCompression, BloatwareRemoval) plus their View pages and DI registration extensions. CA1416 analyzer clean across the entire Release build.
- `[SupportedOSPlatform("linux")]` applied to all 10 Linux-only module classes (Systemd / Swap / Package / Journal / SnapFlatpak / Kernel / LinuxAppInstaller / Cron / Grub / Docker[+macos]).
- `[SupportedOSPlatform("macos")]` applied to all 9 macOS-only module classes — the build-hygiene prerequisite for the eventual macOS notarized release.
- 6 modules now expose an `IOperationModule.RunOperationAsync` returning `OperationResult` with explicit `Success / Skipped / Failed` status + reason + remediation. The legacy `OptimizeAsync` shape is preserved for the other 40+ modules; opt-in migration to Phase 6.18+.
- New `PrivilegeHelperRequiredDialog` modal mirrors the existing `UnavailableModuleView` UX (title + reason + copyable remediation + Try Again + Close + 5 EN+TR loc keys).
- Post-action banner on each adopting module View shows green/amber/red feedback for Success/Skipped/Failed (3 EN+TR loc keys: `op.result.success`, `op.result.skipped`, `op.result.failed`).

### Carry-forward to Phase 6.18+
- Real privileged-ops smoke (deploy `install-privhelper.sh` and verify RAM Optimizer actually drops caches, Package Cleaner actually removes orphans, etc.)
- Migrate the other 40+ modules to `IOperationModule.RunOperationAsync` incrementally
- Replace file-existence sentinel for helper presence with real D-Bus presence probe (Tmds.DBus session-bus query) + NameOwnerChanged auto-refresh
- macOS implementation of the privilege-helper analog (XPC service + signed entitlements) — gated on Mac hardware
- App-level `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` beyond CA1416 (nullability, etc.)
- System Health stale-data warning — distinguish "drive momentarily not ready" from "drive doesn't exist"
- Centralize the `FormatBytes` helper that's currently duplicated across 6 module Views from Wave F adoption

---

## [1.8.0] — 2026-04-28

Major desktop release covering Phase 5.5 finishing items and Phase 6.1–6.6 (deep-link routing, pixel-regression infra, light/dark/system theme, full Turkish localization, release pipeline integration), plus the Session 23 ML training + LLM integration work that was originally drafted for v1.8.0 in April. Ships Windows + Linux self-contained binaries (1.2 GB each, includes ML/LLM models). macOS notarization is still blocked on hardware and remains in Phase 6 carry-forward.

### Phase 6.6 — Release pipeline desktop integration (April 19–22, 2026)
- `UpdateDownloader` background service with Polly retry + SHA256 verification against `app_updates.SignatureHash`
- Active update banner (non-mandatory) and full-screen modal (mandatory) — Alt+F4 / Esc / Win key all hardened so a mandatory update cannot be bypassed
- Per-platform update channel: desktop sends `?platform=windows` or `?platform=linux` to `/api/updates/check`
- Empty-hash fail-fast guard: client refuses to apply an update whose `SignatureHash` is empty (security regression prevention)
- Banner event-handler unsubscribe on dismiss (no leak), update notification breadcrumb wired through `UpdateInfo`
- `AuraCorePro-Setup.exe` and `auracorepro_<ver>_amd64.deb` distribution paths verified via 100 KB dummy-installer smoke test (Phase 6.6.E)

### Phase 6.4 — Full Turkish localization sweep (April 18–22, 2026)
- All 67 XAML view files localized (EN + TR), zero hardcoded UI strings remaining
- ~560+ new translation key pairs added to `LocalizationService` (dual EN/TR `Dictionary<string, string>` with `LanguageChanged` event)
- Localized: LoginWindow, MainWindow, SettingsView, DashboardView, AIFeaturesView, all AI sections, 7 Controls + 5 Dialogs, 7 core module views (×4 batches), Defender / DiskHealth / DnsBenchmark / DnsFlusher / DriverUpdater / EnvVars / FileShredder, Admin / AppInstaller / Autorun / Battery / Bloatware / CategoryClean / DefaultsOptimizer, ServiceManager / SpaceAnalyzer / StartupOptimizer / SwapOptimizer / SymlinkManager / SystemHealth / TweakList, Upgrade / WakeOnLan + 5 Linux modules, final 9 Linux + macOS modules
- Code-behind hardcoded-string scanner (companion to XAML scanner) catches Turkish literals slipping through C# `.cs` files
- XAML hardcoded-string regression scanner (`HardcodedStringScannerTests`) gates all new view files; grandfather list emptied
- 3 hardcoded TR strings caught and migrated mid-session in `InsightsSection` code-behind

### Phase 6.3 — Light / Dark / System theme switcher (April 18, 2026)
- `ThemeService` 3-state refactor: `AppTheme { Dark, Light, System }` with `EffectiveVariant` resolution
- System mode reads `PlatformSettings.GetColorValues().ThemeVariant` (falls back to Dark on unknown / Linux)
- Theme persistence at `%LOCALAPPDATA%/AuraCorePro/theme.pref`
- New `<ResourceDictionary x:Key="Light">` sibling in `AuraCoreThemeV2.axaml` — 27 parity tokens (off-white backgrounds, pure white cards, inverted text, Tailwind-600 semantic colors, dimmed glow shadows)
- Settings UI: 3-option RadioButton group (`ThemeSystemRb`/`ThemeLightRb`/`ThemeDarkRb`) replaces old 2-state button
- Cold-start flash avoided: `ThemeService.Initialize()` owns the variant before `MainWindow.Show()` (removed hardcoded `App.axaml RequestedThemeVariant="Dark"`)
- Light gallery pixel-regression goldens added (4 total: 2 Dark + 2 Light per gallery view)

### Phase 6.2 — Pixel regression infrastructure (April 18, 2026)
- `Verify.Xunit 28.1.0` + `Verify.ImageSharp 4.0.0` wired to Avalonia.Headless + Skia (`UseSkia()` + `UseHeadlessDrawing=false`) for deterministic view snapshots
- `PixelRegressionHarness.RenderViewAsync<TView>(w, h)` → captures rendered frame as PNG bytes
- `PixelVerify.Verify(png)` routes goldens to `tests/.../goldens/` via `.UseDirectory("../goldens")`
- Test-only `DesignSystemGallery` UserControl (typography ramp + accent/semantic swatches + StatusChip / AccentBadge variants + corner-radii scale + tinted status surfaces); 2 sizes × 2 themes = 4 goldens
- Caught a real regression first run: 10 semantic tint tokens had hex alpha byte in trailing position instead of Avalonia's leading `#AARRGGBB` — fixed in companion commit
- `.gitignore` rule for `*.received.{png,txt}`; `goldens/README.md` documents accept workflow (Verify.Tool CLI / manual rename / `VERIFY_AutoVerify`)

### Phase 6.1 — Deep-link URL routing (April 18, 2026)
- `auracorepro://module/<id>?...` URL scheme with idempotent HKCU auto-install via `UrlSchemeRegistrar`
- `UrlSchemeHandler.Parse` with **46-case security test suite** covering injection, escaping, malformed schemes
- `ModuleIdsRegistry` as single source of truth for deep-link module IDs (no string drift)
- Per-user single-instance lock via `InstanceMutex` (no global lock — multi-user systems supported)
- `UrlGatewayServer` Named Pipe with current-user ACL — second-launch routes URL to running instance
- `UrlGatewayClient` send-and-exit path
- `Win32Interop.FocusWindowByTitle` via P/Invoke (raises window when URL arrives)
- `MainWindow` dispatches pending launch URL via `INavigationService` after RootView is ready

### Phase 5.5.x — UX finishing items (April 12–18, 2026)
- **Space Analyzer**: tree-expansion drill-down with lazy children loading (no blocking on large filesystems)
- **System Health**: intro card + localization
- **Disk Health**: real SMART data wired into `DiskHealthSummaryCard`; sidebar entry removed (now Dashboard widget); WMI worst-temp wiring in `DiskHealthScanner`
- **Symlink Manager**: find-vs-create UI split + create action
- **QuickAction tile collection** + Bloatware preset
- **ServiceManager** new `AuraCore.Module.ServiceManager` + row context menu
- **Defender Manager**: 7 write actions wired via `IShellCommandService`
- **Driver Updater**: scan + export via `IShellCommandService`
- **Driver/Defender/Service Ops** implementations with action-validator + timeout
- `ActionWhitelist.Windows` with 13 whitelisted action ids
- Named Pipe server: full whitelist + ACL + dispatcher implementation
- **AI Chat**: wired `IAuraCoreLLM.ReloadAsync` into `ChatSection` UI (debt-A1)
- **First-run UAC install prompt** for Windows privileged helper (debt-A2)
- **Cleaner consolidation** decision + subtitle disambiguation
- **GrubManager** 3 deferred sudo hits migrated via 3 new grub sub-actions (debt-B5)
- DI bootstrap in test harness un-skipped 6 pilot-view render tests (debt-B2)

### Session 23 (April 4, 2026) — ML Training Pipeline + LLM Integration

#### ML Training Pipeline (ml-training/ directory)
- Real data collection: 1000 telemetry samples from `LocalMetricDb` (SQLite)
- Synthetic data augmentation: 10,000 samples with realistic distributions
- Parameter optimization: grid search across 3 ML.NET models (threshold/sensitivity tuning, false positive reduction)
- ONNX autoencoder: PyTorch custom anomaly detection model (F1=0.876, 78KB), exported to ONNX format
- 6 LLM models fine-tuned with LoRA adapters:
  - TinyLlama 1.1B, Phi-2 2.7B, Phi-3 Mini 3.8B
  - Mistral 7B, Llama 3 8B, Llama 2 13B, Phi-3 Medium 14B
- Cloud GPU training on DigitalOcean H200 droplet (6 models trained in 32 minutes)
- GGUF Q4 quantized exports for all 7 models (optimized for local inference)
- LLM dataset: 964 bilingual examples (EN/TR) covering AuraCore modules and system optimization

#### C# AI Integration
- `OnnxAnomalyDetector`: ONNX Runtime inference integrated into ML.NET pipeline
- `LlmInferenceEngine`: LLamaSharp 0.26.0 for local LLM inference (CPU + GPU support)
- `AIConfigProvider`: model path resolution and configuration management
- AI Chat UI: conversational interface with chat history persistence
- Settings model selector: 7 models listed with real RAM usage measurements

#### Platform Bug Fixes (17 bugs)
- 8 Linux/macOS platform module bug fixes across multiple modules
- Platform-specific guards and fallbacks improved

### Testing
- **2212 desktop tests passing** (Unit 9 + Integration 15 + Module 158 + Platform 397 + Simulation 1 + UI.Avalonia 1632)
- Full backend suite: 233 passing (separate API project; not bundled in desktop release)
- 7 pre-existing CA1416 warnings (`ServiceController` + `RegistryKey` reachable on non-Windows code paths) — carry-forward to a future platform-guard sweep, no functional impact

### Known Issues / Carry-forward
- LLM dataset accuracy needs improvement (hallucination on module details) — Phase 6.16+ retraining
- macOS distribution still blocked on Apple Developer notarization hardware (Phase 6 roadmap Item 6 unchanged)
- 7 CA1416 platform-API warnings on non-Windows builds (cosmetic; APIs throw at runtime if reached, but currently guarded by view-level platform checks)

### Distribution
- **Windows**: `AuraCorePro-1.8.0-win-x64.zip` (~488 MB compressed, 1.2 GB extracted) — extract anywhere, run `AuraCore.Pro.exe`
- **Linux**: `AuraCorePro-1.8.0-linux-x64.zip` or `auracorepro_1.8.0_amd64.deb` — `.zip` extracts + `chmod +x AuraCore.Pro && ./AuraCore.Pro`; `.deb` installs to `/usr/lib/auracorepro/` with `auracorepro` launcher in `PATH`
- **macOS**: not shipped this release (notarization blocker)

---

## [1.7.0] — 2026-04-02

### Session 20 (April 1, 2026) — UI/UX Premium Overhaul

#### UI/UX (62 changes)
- Window resized to 900x600 compact (from 1100x720), min 780x520
- Sidebar: 220px to 190px, gradient background, OrbitalLogo restored
- Glassmorphic card system: rgba semi-transparent backgrounds, gradient bars
- Radius smoothing: Sm 4>6, Md 8>10, Lg 10>12, Xl 14>16
- 20 new semantic theme tokens (WarningBg, ErrorBg, InfoBg, SuccessBg, AccentBg + Border variants)
- Dashboard fully redesigned: glassmorphic KPI cards, SVG DrawingImage icons
- 36 module pages premium redesign: glassmorphic cards, gradient bars, icon badges
- 44 view files bulk font/spacing/color update
- 18 files hardcoded colors converted to DynamicResource theme tokens
- Nav section labels: uppercase, 8px Bold
- Avatar user badge: gradient avatar + tier text
- Sidebar icon update + KPI SVG icons (CPU/RAM/Disk/Uptime)

#### New Features (10)
- PDF Export: SystemHealth export button with QuestPDF integration
- Gaming Mode Auto-Detect: 68+ game recognition, 5s polling, auto-activation
- Auto-Schedule: 6 task cards, ComboBox interval (1h to weekly), DispatcherTimer
- App Installer: Avalonia view (3 tabs: Search/Installed/Updates), winget integration
- Battery Report: powercfg /batteryreport with browser open
- Onboarding View: compact font/spacing theme-compatible
- File Shredder: 3-pass overwrite, random rename, delete (full implementation)
- Driver Updater: pnputil /scan-devices + Windows Update trigger
- Battery Optimizer: Power Saver switch, monitor timeout, standby settings
- Registry Optimizer: HKLM fix attempt, elevation graceful fail

#### Backend Security (24 fixes)
- JWT secret: hardcoded default removed, env var required
- Email validation: regex format + XSS character rejection
- Password strength: min 10 chars, 2+ character types
- Login rate limit: 3 attempts/30min + email-based rate limit
- Registration rate limit: 3/hour/IP
- Account enumeration prevention: generic response + artificial delay
- TOTP rate limiting: 5/15min per email
- Input validation: CrashReport/Telemetry string length limits
- Request size limit: Kestrel 5MB max body
- Security headers: X-Content-Type, X-Frame, XSS-Protection, HSTS, Referrer, Permissions
- CORS: production restricted to specific domains
- Pagination limit: pageSize max 100
- Setup token: AURACORE_SETUP_TOKEN env var check
- Stripe env vars: secret key + webhook secret from env
- Stripe guest checkout: token-free website payment
- Stripe TRY currency: currency parameter + TL pricing
- User delete foreign key fix: cascade delete across all related tables
- X-Forwarded-For: IP spoofing fixed
- JWT claims: null check + Guid.TryParse
- License race condition: transaction-based atomic check

#### Pentest (23 tests passed)
- Port scan (nmap): SSH, HTTP, HTTPS open; PostgreSQL closed
- SSL/TLS: TLS 1.2+1.3, weak ciphers disabled
- JWT manipulation: algorithm none, role tampering, token replay blocked
- IDOR + privilege escalation blocked
- Business logic: tier manipulation, path traversal, SSRF blocked
- DDoS resilience: 50 parallel requests, brute force, 1MB payload handled

#### Bug Fixes (54)
- ProcessMonitor: ConcurrentDictionary, division by zero guard, kill tree, HasExited
- FileShredder: Path null check, catch logging
- BatteryOptimizer: JsonDocument using, RunPS 30s timeout
- DiskCleanup: hardcoded C: path replaced with dynamic detection
- GamingMode: ProcessThread iteration safety (.ToList snapshot)
- AutorunManager: ExtractExePath bounds check
- RamOptimizer, RegistryOptimizer, HostsEditor, AppInstaller: catch logging improvements
- LoginWindow: async fire-and-forget with try/catch, thread-safety lock
- PaymentView: Process.Start null-safe, poll error logging
- IsoBuilder: 10x FirstOrDefault! null check fixes
- DashboardView, ProcessMonitorView, OrbitalLogo, SystemHealthView, NetworkOptimizerView: timer cleanup, _initialized guards
- CategoryCleanView: nullable module, FindResource fallback
- SchedulerView: Unloaded cleanup, timer order fix
- MainWindow: lock symbol consistency
- SettingsView: "Turkce" to "Turkce" display fix

---

### Session 21 (April 2, 2026) — Landing Page + AI Engine Core

#### Landing Page Modernization (deployed to production)
- Monolithic HTML refactored to modular structure (CSS/JS separate files)
- Clean URL routing: .html extensions removed (/privacy, /terms)
- Nginx rewrite rules with 301 redirects
- CSS pseudo-element logos replaced with animated orbital SVG logo (3 orbits, glow pulse, prefers-reduced-motion)
- 27 module emojis replaced with Lucide SVG icons (54 replacements EN+TR)
- 11 feature card emojis replaced with Lucide SVG icons
- Tablet breakpoint added (769-1024px)
- Privacy/Terms pages updated with SVG logo + clean URL links
- Favicon: SVG + PNG dual support
- Canonical tag added
- Module count normalized to 27 everywhere
- Download link updated to v1.6.0
- API_URL set to https://api.auracore.pro
- Turkish SmartScreen text fixed

#### AI Engine Core (Plan 1/2) — 35 tests passing
- AuraCore.Engine.AIAnalyzer project created (ML.NET 3.0.1 + SQLite)
- IAIAnalyzerEngine interface + DI registration
- MetricBuffer: thread-safe ring buffer (900 samples, 30min window)
- LocalMetricDb: SQLite persistence (daily_metrics, ai_events tables)
- UserProfileStore: persistent user behavior profile (user_profile, profile_snapshots)
- ProfileLearner: auto-learns user behavior (EMA, typical apps, confidence scoring)
- AnomalyDetector: ML.NET SR-CNN (CPU/RAM anomaly detection, unsupervised)
- DiskForecaster: ML.NET SSA (disk usage prediction, 30-day forecast)
- MemoryLeakDetector: ML.NET IID Change Point (process memory leak detection)
- AIAnalyzerEngine: orchestrator coordinating all models + profile learning

---

### Session 22 (April 2, 2026) — AI UI + Module Improvements + Admin

#### AI Engine UI (Plan 2/2)
- DashboardView AI Engine integration (Push + AnalyzeAsync every 60s)
- Dashboard AI badges: CPU anomaly, RAM anomaly, disk prediction
- AI Insights page: 6 sections (score, alerts, disk forecast, memory analysis, profile, status)
- Sidebar navigation: AI Insights menu item
- AI Consent dialog (post-onboarding, Allow/Decline)
- Settings page: AI Telemetry toggle
- Backend API: POST /api/telemetry/ai-metrics + GET global averages
- MetricSyncService: consent-controlled, daily sync

#### Module Improvements
- Dashboard: mini sparkline charts in stat cards
- System Health: CPU per-core breakdown
- RAM Optimizer: auto-optimize on threshold (>85%), Boost button, process whitelist/blacklist, historical RAM graph
- Junk Cleaner: granular per-file checkboxes, exclude list, history log, AI recommendation
- Gaming Mode: GPU optimization, Network QoS priority, session badge, Windows Update pause during gaming
- Network Optimizer: auto-DNS detection, before/after latency comparison, bandwidth per process, Wi-Fi signal meter
- App Installer: preset bundles (Gaming/Dev/Office), queue system, per-app progress, dependency detection
- Bloatware Removal: re-install via WinGet, block removed apps from returning
- Status bar at bottom of application

#### Admin Panel Backend (6 new controller groups)
- CrashReport: list/detail/stats/delete endpoints
- Telemetry: list/stats/event-types endpoints
- Device: list/stats endpoints
- Audit Log (login attempts): list/stats endpoints
- IP Whitelist: CRUD endpoints
- Revenue chart endpoint

#### Forgot Password Flow
- Standalone /forgot-password page (EN/TR, glassmorphic, 2-step: email → code → new password)
- POST /api/auth/password/forgot: Resend email with 6-digit code, 10min TTL
- POST /api/auth/password/reset: code validation + password strength check
- password_reset_codes database table
- Desktop app URL updated to clean URL

#### Synthetic Data
- Backend seed: 100 synthetic metric records on API startup (realistic distributions)
- Client bootstrap: 7-day disk forecast data on first launch
- Automatic cleanup when real data accumulates

#### AI Localization
- 56 new LocalizationService keys (EN/TR)
- AI Insights page fully localized
- Dashboard AI badges localized

#### Release
- Installer rebuilt with Inno Setup (self-contained Avalonia, no runtime needed)
- GitHub Release v1.7.0 with AuraCorePro-Setup.exe
- Landing page download links updated to v1.7.0
- Discord server launched with changelog webhook integration

---

## [1.6.0] — 2026-03-31

### Session 19 — UI Polish (Avalonia Compact Layout)
- AuraCoreTheme: radius, padding, margin, font adjustments for compact layout
- MainWindow: 1280x820 to 1100x720, sidebar 260 to 220px
- DashboardView rewritten with WinUI3-style accent stripe cards
- 44 view files bulk update
- AI Agent Workflow setup

### Session 17-18 — Core Implementation
- All 20 module placeholders replaced with real views
- WinUI3 full parity achieved
- Cross-platform Phase 2-3 completed
- Packaging: .deb, tarball, AppImage, .app bundle
- 8 new modules added
