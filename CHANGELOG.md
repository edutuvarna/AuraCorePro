# AuraCore Pro — Changelog

All notable client-facing improvements, new modules, fixes, and architectural upgrades for AuraCore Pro desktop are documented in this file.

---

## [1.8.2] — 2026-11-15

Major milestone release delivering complete 48-module anti-placebo realization, local-first Grounded AI Guardian, 5-language internationalization, and a modern self-contained Inno Setup installer.

### Grounded AI Guardian & Local Intelligence
- **Local-First Telemetry Pipeline**: Real-time process sampling captures memory working sets and active CPU/RAM/Disk metrics with 100% local processing — zero data transmitted to the cloud.
- **Continuous Memory Leak Detection**: Integrated ML.NET IID Change Point detector analyzes multi-snapshot memory progression across running processes to catch memory leaks early.
- **Network Behavior & Signature Inspector**: Correlates active outbound TCP connections per process with Authenticode digital signature and publisher verification. Flags high-risk unsigned executables running from temporary directories with active remote connections.
- **Hallucination-Free AI Engine**: Context injection guarantees AI recommendations and chat responses cite verified OS telemetry without synthetic metrics.

### 48-Module Forensic Remediation & Anti-Placebo Architecture
- **Driver Updater Overhaul**: Replaced heuristic scanning with genuine Windows Update Agent (WUA) catalog querying. Excluded Microsoft in-box 2006 drivers from false-positive outdated lists; added pre-update driver backup and authentic rollback via `pnputil /export-driver`.
- **Destructive Command Elimination**:
  - `SnapFlatpakCleaner`: Fixed revision parser to retain `--revision` flags, preventing accidental uninstallation of active Snap packages.
  - `KernelCleaner`: Implemented natural numeric segment version comparator, preventing newest active kernels from being marked for deletion.
  - `RegistryOptimizer`: Replaced full root hive exports with targeted per-subkey `.reg` backups and safe rollbacks.
  - `StorageCompression`: Corrected inverted rollback filter to only uncompress directories compressed during the current session.
  - `EnvironmentVariables`: Added `%VAR%` expansion before existence checks to safeguard valid system path variables.
- **UI-to-Module Rewiring**: Rewired decoupled views (`ServiceManager`, `NetworkMonitor`, `PackageCleaner`, `LaunchAgentManager`, `AppInstaller`, `FirewallRules`, `DnsBenchmark`) directly into domain engines.
- **Safe Rollback Journaling**: Implemented pre-modification state capture for shell customizations (`ContextMenu`, `ExplorerTweaks`, `TaskbarTweaks`) with graceful explorer restart via `SHChangeNotify`.
- **Privileged Helper Elevation**: Plumbed `IShellCommandService` through `GamingMode`, `JunkCleaner` (Win32 `SHEmptyRecycleBinW`), `JournalCleaner`, `SwapOptimizer`, and `FontManager`.
- **Recycle Bin Duplicate Protection**: Routed `DiskCleanup` duplicate deletions to the Windows Recycle Bin to prevent permanent data loss.

### Packaging & Internationalization
- **Modern Inno Setup Installer**: Complete self-contained installer (`AuraCorePro-Setup-1.8.0.exe`, 147.66 MB) with multi-resolution branding assets, automated privileged service installation, and clean uninstallation.
- **5-Language Localization**: Full translation coverage across English, Turkish, German, Spanish, and French powered by DeepL Neural MT, featuring a multi-lingual setup wizard.

---

## [1.8.1] — 2026-05-04

Platform debt closure, platform support attribute enforcement, and privileged feedback UX.

### Fixed
- **System Health Storage Drives**: Virtual filesystems (`tmpfs`, `proc`, `sysfs`, `cgroup`) filtered from storage listings; added zero-capacity guard and `Math.Clamp` defense-in-depth on percent calculations.
- **Privileged Operations Diagnostics**: Operations requiring elevation (RAM Optimizer, Junk Cleaner, Systemd Manager, Swap Optimizer, Package Cleaner, Journal Cleaner) now display clear diagnostics with copyable remediation commands when the privileged helper is absent.
- **Startup Probe**: `PrivilegeHelperMissingBanner` surfaces on application launch when the privileged helper daemon is not detected.

### Changed
- `[SupportedOSPlatform]` attributes enforced across all platform-specific module classes (Windows, Linux, macOS) for clean cross-platform build hygiene.
- 6 modules adopted `IOperationModule.RunOperationAsync` returning structured `OperationResult` (`Success / Skipped / Failed`) with contextual status banners.
- Added `PrivilegeHelperRequiredDialog` modal with copyable remediation steps.

---

## [1.8.0] — 2026-04-28

Major desktop release introducing theme customization, deep-link URL routing, Turkish localization, and local ML models.

### New Features
- **Light / Dark / System Theme Switcher**: 3-state theme engine supporting System detection, Dark, and Light variants with 27 design tokens and persisted user preferences.
- **Deep-Link URL Routing**: Custom `auracorepro://module/<id>` scheme handler with single-instance mutex and named-pipe dispatch to bring existing windows into focus.
- **Full Turkish Localization**: Complete translation sweep across all 67 XAML view files with zero hardcoded UI strings.
- **Local Machine Learning Models**: Quantized GGUF anomaly detection models running entirely locally on CPU/GPU via LLamaSharp and ML.NET.
- **Background Update Downloader**: Automatic update checking with SHA-256 binary validation and non-intrusive update banners.

### UX Improvements
- **Space Analyzer**: Tree-expansion drill-down with lazy directory loading for high-capacity drives.
- **Disk Health**: SMART diagnostics and drive temperature monitoring integrated into the main dashboard.
- **Service Manager**: Searchable Windows service management with start/stop/restart controls.

---

## [1.7.0] — 2026-04-02

Comprehensive UI/UX premium redesign and module capability expansion.

### UI/UX Overhaul
- **Compact Window Layout**: Responsive 900x600 layout optimized for high-DPI displays.
- **Glassmorphic Styling**: Semi-transparent card backdrops, gradient accent bars, and WinUI3-style KPI cards.
- **Navigation Redesign**: Streamlined sidebar with category grouping and status indicators.

### Features & Module Enhancements
- **PDF System Report Export**: One-click system diagnostic export via QuestPDF.
- **Gaming Mode Auto-Detection**: Process monitoring recognizes 68+ popular games to automatically engage performance profiles, adjust thread priority, and pause Windows updates.
- **Automated Task Scheduler**: Configurable automated maintenance intervals (hourly, daily, weekly).
- **RAM Optimizer**: Auto-optimize threshold trigger (>85%), one-click Boost, and historical memory usage sparklines.
- **App Installer**: Curated preset bundles (Gaming, Development, Office) with queue management.
- **Junk Cleaner Enhancements**: Granular per-category checkboxes and exclusion lists.
- **Network Optimizer**: Automatic fastest DNS detection and before/after latency comparison.

---

## [1.6.0] — 2026-03-31

Core desktop implementation and WinUI3 aesthetic parity.

### Improvements
- Replaced all placeholder module pages with authentic views and responsive layouts.
- Live System Health telemetry monitor for real-time CPU, RAM, and storage utilization.
- Cross-platform distribution packaging for Linux and macOS.

---

## [1.5.0] — 2026-03-24

### Added
- **Battery Optimizer Module**: Battery health assessment, real-time discharge rate monitoring, background process power drain analysis, and power plan switching.

---

## [1.4.0] — 2026-03-18

### Added
- **Privacy Cleaner Module**: Cleans browser caches (Chrome, Edge, Firefox), recent document histories, jump lists, explorer thumbnails, and clipboard contents.

---

## [1.3.0] — 2026-03-12

### Added
- **Disk Cleanup Pro**: Deep scanning for temporary files, crash dumps, and duplicate files.
- **Defender Manager**: Management interface for Windows Defender real-time protection, cloud delivery, and quick scan triggers.
- **ISO Builder**: Tooling for generating streamlined unattended Windows setup media.

---

## [1.2.0] — 2026-03-05

### Improvements
- Refined self-contained setup installer.
- Updated vector branding and orbital application logo.
- System stability enhancements across background monitor threads.

---

## [1.1.0] — 2026-02-28

- Initial public foundation release of AuraCore Pro desktop optimization suite.
