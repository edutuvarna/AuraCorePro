# AuraCore Pro — Changelog

Desktop app release notes for AuraCore Pro.

---

## [Unreleased]

Work completed since v1.8.1 that will ship in the next release.

### AI
- AI metrics now sample the top processes by memory, so memory-leak detection receives real per-process data.
- New process and network inspector: lists outbound TCP connections per process and checks each executable's digital signature. Unsigned programs running from temporary folders with active connections are flagged.
- AI Recommendations now come from live system data instead of placeholder cards, and each card links to the relevant module.
- AI Chat now receives real CPU, RAM, disk, top-process and alert data instead of anomaly scores.

### Module fixes
- **Snap & Flatpak Cleaner**: removing old revisions no longer risks uninstalling the active app.
- **Kernel Cleaner**: kernel versions are now sorted numerically, so the newest kernel is never pre-selected for removal.
- **Registry Optimizer**: backups now cover only the keys being changed, instead of attempting to export the entire HKLM hive.
- **Storage Compression**: rollback now uncompresses only the folders AuraCore compressed.
- **Environment Variables**: paths containing `%VARIABLES%` are no longer flagged as missing.
- **Hosts Editor**: IPv6 entries are handled correctly.
- **Network Optimizer**: DNS preset switching works, including adapters with spaces in their names.
- **Driver Updater**: checks for updates against the Windows Update catalog, no longer flags Microsoft's built-in drivers as outdated, and backs drivers up before installing.
- **Junk Cleaner**: Recycle Bin cleaning now actually empties the Recycle Bin.
- **DNS Benchmark**: can apply the fastest DNS server it finds.
- **Context Menu, Explorer Tweaks, Taskbar Tweaks**: rollback now restores the previous settings.
- **Disk Cleanup**: duplicate files are sent to the Recycle Bin instead of being permanently deleted.
- **Service Manager, Network Monitor, Package Cleaner, Launch Agent Manager, App Installer, Firewall Rules**: the pages now use their module logic directly, and missing actions were added.
- Several modules that need administrator/root rights now run their actions through the privileged helper instead of failing silently.

### Installer & languages
- New Windows installer that bundles everything needed to run (no separate .NET install).
- German, Spanish and French translations added (machine-translated with DeepL), alongside English and Turkish.

---

## [1.8.1] — 2026-05-04

### Fixed
- System Health no longer shows invalid percentages for Linux virtual filesystems; they are hidden from the drive list.
- RAM Optimizer, Junk Cleaner, Systemd Manager, Swap Optimizer, Package Cleaner and Journal Cleaner now explain when the privilege helper is missing (with a copyable install command) instead of silently doing nothing.
- The "privilege helper missing" banner now appears at startup on Linux.

### Changed
- Six modules now show a clear success / skipped / failed banner after each action.
- New dialog explaining how to install the privilege helper.

---

## [1.8.0] — 2026-04-28

### Added
- Light, Dark and System theme options.
- `auracorepro://` links that open a specific module, including in an already-running window.
- Complete Turkish translation of every page.
- Experimental local AI Chat running on-device models.
- Background update downloads with checksum verification; mandatory updates can't be skipped.
- Space Analyzer folder drill-down; SMART disk health on the Dashboard; new Service Manager.

### Distribution
- Windows and Linux builds. macOS was not shipped in this release.

### Known issues
- The AI Chat can give inaccurate answers about module details.

---

## [1.7.0] — 2026-04-02

### Added
- Redesigned interface: compact window, glass-style cards and refreshed module pages.
- AI Insights page: health score, CPU/RAM anomaly alerts, disk-full forecast and memory-leak detection, all computed locally.
- PDF export for System Health.
- Gaming Mode auto-detects 68+ games.
- Auto-Schedule for running modules on a timer.
- App Installer with search, installed and updates tabs, preset bundles and an install queue.
- Battery report, File Shredder (3-pass overwrite).
- RAM Optimizer auto-optimize and Boost; Junk Cleaner per-file selection and exclusions; Network Optimizer latency comparison.
- Status bar.

### Fixed
- Stability fixes across Process Monitor, File Shredder, Battery Optimizer, Disk Cleanup, Gaming Mode, Autorun Manager and several page timers.

---

## [1.6.0] — 2026-03-31

### Added
- System Health live monitor.
- ISO Builder: standalone ISO and bootable USB creation.
- Crash reporting, delta updates and faster driver export.
- 8 new modules.
- Linux and macOS packaging (.deb, tarball, AppImage, .app).

### Changed
- UI polish across 15+ pages and a more compact layout.

---

## [1.5.0] — 2026-03-26

### Added
- **Battery Optimizer**: battery health, power plans, drain analysis and battery report (English and Turkish).

---

## [1.4.0] — 2026-03-26

### Added
- **Privacy Cleaner**: browser data, recent files, jump lists, thumbnails, prefetch, clipboard and DNS cache.

---

## [1.3.0] — 2026-03-25

### Added
- **Disk Cleanup Pro** and **Defender Manager** modules.
- ISO Builder templates.
- Full Turkish translation.

---

## [1.2.0] — 2026-03-23

### Changed
- Updated logo.
- Improved installer.

---

## [1.1.0] — 2026-03-22

- First release.
