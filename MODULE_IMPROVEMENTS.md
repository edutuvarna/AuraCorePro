# Aura Core Pro — Module Improvement Notes

## Status: Active Development (April 2026)

> **IMPORTANT:** All features must support both Windows 10 (Build 19041+) and Windows 11.

---

## ✅ COMPLETED FEATURES

### Authentication & Backend
- [x] JWT Auth (register/login/refresh) with tier in response
- [x] TOTP 2FA — RFC 6238, QR URI, setup/verify/validate/disable
- [x] Login Rate Limiting — 3 fails/30min per IP + email-based rate limit
- [x] Registration Rate Limiting — 3/hour per IP
- [x] Admin API — Dashboard stats, user CRUD, subscription grant/revoke
- [x] SetupController — promote-admin (first-run only, setup token required)
- [x] Health endpoint with DB check (minimal info in production)
- [x] License validation with tier + transaction-based device count
- [x] Auto-migrate on startup
- [x] Email validation — regex + XSS prevention
- [x] Password strength — min 10 chars, 2+ character types
- [x] Security headers — HSTS, X-Frame-Options, X-Content-Type, XSS-Protection
- [x] CORS restricted — production'da belirli domainler
- [x] Request size limit — 5MB max body
- [x] Pagination limits — max 100 per page
- [x] TOTP rate limiting — 5 attempts/15min per email
- [x] Account enumeration prevention — generic responses
- [x] Stripe TRY currency support + guest checkout
- [x] Input validation — CrashReport/Telemetry string length limits

### Admin Panel (Next.js, static export, deployed)
- [x] Dashboard (6 stats + conversion funnel)
- [x] Users (search/delete/revoke) — cascade delete with all related records
- [x] Payments (history + pending crypto)
- [x] Subscriptions (grant/reset password)
- [x] Licenses (list/revoke/activate)
- [x] Updates (publish/delete)
- [x] Devices (list/stats)
- [x] Crash Reports (list/detail/stats/delete)
- [x] Telemetry (list/stats/event-types)
- [x] Audit Log (login attempts/stats)
- [x] IP Whitelist (CRUD)
- [x] Configuration (feature flags: maintenance mode, registrations, telemetry, crash reports, auto-update)
- [x] Security (2FA setup with QR code, enable/disable)
- [x] Server Health (latency check)
- [x] 15-min inactivity auto-logout
- [x] Glassmorphism dark theme

### Desktop App — Core
- [x] 27 module pages with full functionality
- [x] Premium 900x600 compact window — glassmorphic dark design
- [x] Onboarding Wizard (6 steps)
- [x] Theme Toggle (System/Light/Dark)
- [x] Notification Center
- [x] PDF Health Report (QuestPDF) — export button in SystemHealth
- [x] Keyboard Shortcuts
- [x] Full Localization System (EN/TR) — 180+ keys, all pages

### Dashboard
- [x] Real-time CPU/RAM/Disk/Uptime with auto-refresh
- [x] Glassmorphic KPI cards with SVG DrawingImage icons
- [x] Gradient progress bars (blue, purple, amber)
- [x] Quick Actions with icon buttons
- [x] System Info section (key-value rows with separators)
- [x] Recent Activity log
- [x] Live badge with pulsing dot

### Gaming Mode
- [x] One-click toggle
- [x] Power plan switching
- [x] Process priority boost
- [x] Auto-detect game launch — 68+ known games, 5s polling, auto-activate/deactivate
- [x] Per-game profiles (GameProfileStore)
- [x] Timer — "Gaming Mode active for 2h 15m"

### App Installer (Avalonia)
- [x] 3 tabs: Search, Installed, Updates
- [x] Winget search/install/uninstall
- [x] Update checking + individual/bulk update
- [x] Status bar feedback

### Auto-Schedule
- [x] 6 predefined tasks (Junk, RAM, Registry, Privacy, Disk, Health)
- [x] ComboBox interval picker (1h, 6h, 12h, daily, weekly)
- [x] DispatcherTimer per task with enable/disable toggle
- [x] Last run / next run display

### File Shredder
- [x] Multi-pass overwrite (1/3/7 pass options)
- [x] Random filename before delete
- [x] File picker with multi-select
- [x] Progress tracking per file

### Driver Updater
- [x] WMI driver scan with age analysis
- [x] Problem detection (unsigned, missing, outdated)
- [x] Driver backup
- [x] pnputil /scan-devices + Windows Update integration
- [x] Open Device Manager shortcut

### Battery Optimizer
- [x] Battery health monitoring (WMI)
- [x] Power plan listing + switching (Power Saver auto-switch)
- [x] High drain app detection
- [x] Battery report (powercfg /batteryreport) + browser open
- [x] Monitor timeout + standby optimization

### Other Modules (all functional)
- [x] Space Analyzer — drill-down, breadcrumbs, color-coded
- [x] Disk Health — WMI SMART, temp/wear/power-on
- [x] Startup Optimizer — registry scan, toggle, impact badges
- [x] RAM Optimizer — EmptyWorkingSet, memory trend, leak detection
- [x] Bloatware Removal — AppX scan + remove, risk badges
- [x] Registry Optimizer — scan + fix (HKCU + HKLM with elevation)
- [x] Network Optimizer — DNS switch, traffic stats, adapter info, benchmark
- [x] Junk Cleaner — category scan + clean
- [x] Disk Cleanup Pro — deep clean, duplicate finder
- [x] Storage Compression — compact.exe integration
- [x] Process Monitor — list, kill, suspend/resume, CPU tracking
- [x] Hosts Editor — view/add/remove entries, backup
- [x] Environment Variables — PATH editor, user vars
- [x] Firewall Rules — list, enable/disable rules
- [x] Font Manager — browse with live preview
- [x] Wake-on-LAN — send magic packets
- [x] Symlink Manager — create/scan symlinks
- [x] Defender Manager — protection status, firewall, signatures
- [x] ISO Builder — 12-step wizard (Windows only)

### Security Hardening (Production)
- [x] Nginx: security headers, server_tokens off, 5min WebSocket timeout
- [x] Kestrel: localhost-only binding (127.0.0.1:5000)
- [x] UFW firewall: only 22, 80, 443 open
- [x] PostgreSQL: localhost only, scram-sha-256 auth
- [x] Fail2ban active, unattended upgrades active
- [x] SSL: Let's Encrypt, TLS 1.2+1.3 only
- [x] Pentest passed: 23 tests + DDoS resilience

---

## 🔲 REMAINING IMPROVEMENTS

### Junk Cleaner
- [x] Checkbox per individual file — granular control
- [x] Risk level badges per category (Safe/Low/Medium/High)
- [x] Exclude list — protect specific folders/files
- [x] History log — what was cleaned and when
- [x] AI recommendation — suggest based on disk pressure

### System Health
- [x] Mini sparkline charts (last 60s CPU/RAM)
- [x] CPU per-core breakdown
- [x] GPU info (WMI Win32_VideoController)
- [x] Battery health (laptops)
- [x] PDF export
- [ ] Compare snapshots over time

### RAM Optimizer
- [x] Auto-optimize on threshold (RAM > 85%)
- [x] "Boost" button — aggressive mode
- [x] Process whitelist/blacklist
- [x] Historical RAM graph (last hour)

### Gaming Mode
- [x] Auto-detect game launch (68+ games)
- [x] GPU optimization (Nvidia/AMD specific tweaks)
- [x] Network QoS priority for game traffic
- [ ] Tray icon when active
- [x] Timer — "Gaming Mode active for 2h 15m"
- [x] Disable Windows Update during gaming

### Bloatware Removal
- [x] Search/filter by app name
- [x] "Select All Safe" button
- [x] Re-install option via WinGet
- [x] Block removed apps from returning after updates

### Network Optimizer
- [x] "Best DNS for me" auto-detect (ping all presets)
- [x] Before/after latency comparison
- [x] Bandwidth per process
- [x] Wi-Fi signal strength meter
- [x] "Fix common issues" one-click

### App Installer
- [ ] App icons from WinGet/MS Store
- [x] Per-app installation progress
- [x] Queue system
- [x] Dependency detection
- [x] Preset bundles (Gaming, Development, Office)

### Dashboard
- [x] Mini sparkline charts in stat cards
- [x] Recent activity log
- [x] Predicted disk full date

### General UX
- [ ] Undo last action (where possible)
- [x] Status bar at bottom
- [ ] Page transition animations

---

## ✅ COMPLETED MILESTONES

### Landing Page Modernization (Session 21 — April 2, 2026)
- [x] Clean URL routing (/privacy, /terms — Nginx rewrite + 301 redirect)
- [x] Animated orbital SVG logo (3 orbits, glow pulse, prefers-reduced-motion)
- [x] Modular file structure (CSS/JS separated from HTML)
- [x] 27 module emojis → Lucide SVG icons (54 replacements EN+TR)
- [x] 11 feature card emojis → Lucide SVG icons
- [x] Tablet breakpoint (769-1024px)
- [x] Privacy/Terms pages updated (SVG logo + clean URL links)
- [x] Module count fixed: 27 everywhere (was 30+/30/24/20+)
- [x] Download link: v1.5.0 → v1.6.0 → v1.7.0
- [x] API_URL set to https://api.auracore.pro
- [x] Canonical tag + SVG favicon
- [x] Turkish SmartScreen text fixed
- [x] Deployed to production (auracore.pro)

### AI Engine Core — Plan 1/2 (Session 21 — April 2, 2026)
- [x] AuraCore.Engine.AIAnalyzer project (ML.NET 3.0.1 + SQLite)
- [x] MetricBuffer — thread-safe ring buffer (900 samples, 30min window)
- [x] LocalMetricDb — SQLite (daily_metrics, ai_events tables)
- [x] UserProfileStore — persistent user profile (user_profile, profile_snapshots tables)
- [x] ProfileLearner — auto-learns user behavior (EMA, typical apps, confidence scoring)
- [x] AnomalyDetector — ML.NET SR-CNN (CPU/RAM anomaly detection, unsupervised)
- [x] DiskForecaster — ML.NET SSA (disk usage prediction, 30-day forecast)
- [x] MemoryLeakDetector — ML.NET IID Change Point (process memory leak detection)
- [x] AIAnalyzerEngine — orchestrator (coordinates all models + profile learning)
- [x] IAIAnalyzerEngine interface + AddAIAnalyzer() DI registration
- [x] 35 unit tests, all passing

## ✅ COMPLETED MILESTONES (Session 22 — April 2, 2026)

### AI Engine UI & Sync — Plan 2/2
- [x] DashboardView → AI Engine integration (Push + AnalyzeAsync every 60s)
- [x] Dashboard AI badges (CPU anomaly, RAM anomaly, disk prediction)
- [x] AI Insights page (6 sections: score, alerts, disk forecast, memory analysis, profile, status)
- [x] Sidebar navigation: AI Insights menu item
- [x] AI Consent dialog (post-onboarding, Allow/Decline)
- [x] Settings page: AI Telemetry toggle
- [x] Backend API: POST /api/telemetry/ai-metrics + GET global averages
- [x] MetricSyncService (consent-controlled, daily sync)

### Admin Panel Backend (6 new controller groups)
- [x] CrashReport list/detail/stats/delete endpoints
- [x] Telemetry list/stats/event-types endpoints
- [x] Device list/stats endpoints
- [x] Audit Log (login attempts) list/stats endpoints
- [x] IP Whitelist CRUD endpoints
- [x] Revenue chart endpoint

### v1.7.0 Release
- [x] Changelog (Session 20+21+22 features)
- [x] Version bump: 1.6.0 → 1.7.0
- [ ] Installer rebuild
- [ ] GitHub Release
- [ ] Landing page download link update (v1.7.0)

## 🚀 NEXT MILESTONES

### Backend Completion
- [ ] Password reset flow (/api/auth/password/forgot + /reset)
- [ ] Email service (Resend integration — key already configured)

### Advanced (Future)
- [ ] AI Phase 2: Claude API integration (Pro tier — natural language queries)
- [ ] AI Phase 2: AIRecommenderEngine (smart suggestions based on UserProfile)
- [ ] AI Phase 2: Model self-improvement pipeline (backend aggregate → param updates)
- [ ] Plugin SDK samples
- [ ] Redis caching
- [ ] Auto-updater (desktop app)
- [ ] Enterprise Admin Panel (Phase 2)

---

## 🏢 ENTERPRISE ADMIN PANEL (Phase 2 — after first Enterprise customers)

### Core Features
- [ ] Team management — invite members, assign roles (admin/manager/user)
- [ ] Sub-accounts — hierarchical user structure
- [ ] Device fleet management — see which employee uses which PC
- [ ] Bulk license assignment
- [ ] Usage reports — module usage per team member
- [ ] Single invoice, multiple seats
- [ ] Audit log — who did what, when
- [ ] Custom branding — company logo in desktop app
- [ ] SSO integration (Azure AD / Google Workspace)

### Timeline
- Phase 1 (Launch): "Contact Sales" → manual handling via support@auracore.pro
- Phase 2 (5-10 Enterprise customers): Build self-service Enterprise panel

---

## 🟡 STRIPE BULGARISTAN KDV KAYDI
**Durum:** Şu an gerekli DEĞİL — ciro €51.130 altında
