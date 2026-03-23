# Aura Core Pro — Module Improvement Notes

## Status: Active Development (March 2026)

> **IMPORTANT:** All features must support both Windows 10 (Build 19041+) and Windows 11.

---

## ✅ COMPLETED FEATURES

### Authentication & Backend
- [x] JWT Auth (register/login/refresh) with tier in response
- [x] TOTP 2FA — RFC 6238, QR URI, setup/verify/validate/disable
- [x] Login Rate Limiting — 5 fails/15min per IP
- [x] Admin API — Dashboard stats, user CRUD, subscription grant/revoke
- [x] SetupController — promote-admin (first-run only)
- [x] Health endpoint with DB check
- [x] License validation with tier
- [x] Auto-migrate on startup

### Admin Panel (Next.js on Netlify)
- [x] Dashboard (6 stats + conversion funnel)
- [x] Users (search/delete/revoke)
- [x] Payments (history + pending crypto)
- [x] Subscriptions (grant/reset password)
- [x] Security (2FA setup with QR, enable/disable)
- [x] Server Health (latency check)
- [x] 15-min inactivity auto-logout
- [x] Glassmorphism dark theme

### Desktop App — Core
- [x] 20+ module pages with full functionality
- [x] Onboarding Wizard (6 steps)
- [x] Theme Toggle (System/Light/Dark)
- [x] Notification Center
- [x] PDF Health Report (QuestPDF)
- [x] Keyboard Shortcuts
- [x] Full Localization System (EN/TR) — 180+ keys, all pages

### Dashboard
- [x] Real-time CPU/RAM/Disk/Health with auto-refresh
- [x] Colored stat icons (CPU blue, RAM purple, Disk amber, Health green)
- [x] Thicker progress bars with per-stat colors
- [x] Quick Actions with icon buttons
- [x] System Info section
- [x] AI Tip (personalized suggestion)

### Space Analyzer
- [x] Drill-down treemap (click folder → scan inside)
- [x] Breadcrumb navigation
- [x] File type distribution (top 10 extensions)
- [x] "Open in Explorer" per folder
- [x] Color-coded folders

### Disk Health Monitor
- [x] WMI SMART data
- [x] Temperature/wear/power-on hours
- [x] Health badges

### Startup Optimizer
- [x] Registry-based scan (HKCU/HKLM Run, StartupApproved)
- [x] Toggle enable/disable per app
- [x] Impact classification (High/Medium/Low)
- [x] Summary card

### App Installer
- [x] WinGet bundles (presets)
- [x] Custom Bundles — create/delete/install user collections
- [x] Search apps
- [x] Installed apps (filter, export/import)
- [x] Update checking + bulk update
- [x] Bulk install/uninstall

### Gaming Mode
- [x] One-click toggle
- [x] Power plan switching
- [x] Process priority boost
- [x] Auto-detect game launch (GameWatcher)
- [x] Per-game profiles (GameProfileStore)

### RAM Optimizer
- [x] Per-process memory trend (sparkline, 12-sample)
- [x] Memory leak detection (>75% increasing + >5MB growth)
- [x] Process list with category badges
- [x] Optimize button

### Bloatware Removal
- [x] Community bloatware scores (50+ entries)
- [x] Risk badges
- [x] Scan + remove

### Background Scheduler
- [x] Scheduled Registry Scan + Bloatware + Storage

---

## 🔲 REMAINING IMPROVEMENTS

### Junk Cleaner
- [ ] Checkbox per individual file — granular control
- [x] Risk level badges per category (Safe/Low/Medium/High)
- [ ] Exclude list — protect specific folders/files
- [ ] History log — what was cleaned and when
- [ ] AI recommendation — suggest based on disk pressure

### System Health
- [ ] Mini sparkline charts (last 60s CPU/RAM)
- [ ] CPU per-core breakdown
- [x] GPU info (WMI Win32_VideoController)
- [x] Battery health (laptops)
- [ ] Compare snapshots over time

### RAM Optimizer
- [ ] Auto-optimize on threshold (RAM > 85%)
- [ ] "Boost" button — aggressive mode
- [ ] Process whitelist/blacklist
- [ ] Historical RAM graph (last hour)

### Gaming Mode
- [ ] GPU optimization (Nvidia/AMD specific tweaks)
- [ ] Network QoS priority for game traffic
- [ ] Tray icon when active
- [x] Timer — "Gaming Mode active for 2h 15m"
- [ ] Disable Windows Update during gaming

### Bloatware Removal
- [x] Search/filter by app name
- [x] "Select All Safe" button
- [ ] Re-install option via WinGet
- [ ] Block removed apps from returning after updates

### Network Optimizer
- [ ] "Best DNS for me" auto-detect (ping all presets)
- [ ] Before/after latency comparison
- [ ] Bandwidth per process
- [ ] Wi-Fi signal strength meter
- [x] "Fix common issues" one-click

### App Installer
- [ ] App icons from WinGet/MS Store
- [ ] Per-app installation progress
- [ ] Queue system
- [ ] Dependency detection

### Storage Compression
- [ ] Per-folder compression progress bar
- [ ] Before/after size comparison
- [ ] Disk space trend graph

### Registry Optimizer
- [ ] Deeper scanning (COM/OLE entries)
- [ ] Backup history with dates
- [ ] Registry size monitor

### Dashboard
- [ ] Mini sparkline charts in stat cards
- [x] Recent activity log
- [x] Predicted disk full date

### General UX
- [ ] Undo last action (where possible)
- [ ] Status bar at bottom
- [ ] More keyboard shortcuts

---

## 🚀 NEXT MILESTONES

### Infrastructure
- [ ] Azure backend deploy
- [ ] Namecheap domain + Cloudflare DNS
- [ ] Landing page (auracore.pro)

### Monetization
- [ ] Stripe real SDK integration
- [ ] BTC/USDT payment flow
- [ ] Feature gating enforcement

### Advanced
- [ ] Plugin SDK samples
- [ ] Email service (SendGrid)
- [ ] Redis caching
- [ ] Auto-updater

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
