# AuraCore Pro — Changelog

All notable changes to this project will be documented in this file.

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
