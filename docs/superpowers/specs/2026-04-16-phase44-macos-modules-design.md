# Phase 4.4 — macOS Modules Feature Completion

**Date:** 2026-04-16
**Branch:** `phase-4.4-macos-modules` (from `phase-4.3-linux-modules` HEAD `c63a4cd`)
**Status:** Design approved, executing autonomously (user delegated decisions during absence)

---

## 1. Context

Phase 4.3 shipped 6 Linux modules in one session (HEAD `c63a4cd`, 1144/1144 tests). Phase 4.4 applies the same patterns to macOS. User delegated full decision authority for this session.

**Scope reduction from prior roadmap:** Vision Doc §10 targeted "6 macOS modules" but the source modules directory contains only 5 macOS-specific engine scaffolds (`DnsFlusher`, `MacAppInstaller`, `PurgeableSpaceManager`, `SpotlightManager`, `XcodeCleaner`). The 6th slot is deferred to post-phase retro (same principle as Phase 4.3 §2.3 "7th Linux module deferred"). A 6-module target is a rounding goal, not a hard contract — shipping 5 polished modules beats rushing 6.

**Already-shipped macOS modules (NOT Phase 4.4 scope):**
- `BrewManager`, `DefaultsOptimizer`, `LaunchAgentManager`, `TimeMachineManager` — migrated in Phase 4.1 cross-platform batch
- `DockerCleaner` — Phase 4.3.3, engine is `Linux | MacOS` flagged (UI already shipped for Linux path; macOS sidebar exposure deferred to Phase 5)

---

## 2. Scope — 5 modules

Each ships with:
- View (Phase 4.0 shell: ModuleHeader + optional StatRow + GlassCards)
- ViewModel (MVVM, sealed-engine adapter pattern)
- DI registration (concrete + `IOptimizationModule` alias)
- Sidebar entry (macOS platform filter)
- `MainWindow.CreateModuleView` route
- Localization EN+TR with proper Turkish diacritics (ğ, ü, ş, ı, İ, ö, ç)
- Tests: ~6 view smoke + ~15-22 VM unit + ~30-50 locale parity + 1 DI smoke

**Engines (DO NOT modify unless additive exposure needed):**

| Module | Engine size | Models | Report shape |
|--------|-------------|--------|--------------|
| DnsFlusher | 5.6 KB | `DnsFlusherReport` (tiny) | flushed status + resolver info |
| PurgeableSpaceManager | 8.2 KB | `PurgeableReport` | purgeable bytes + breakdown |
| SpotlightManager | 8.6 KB | `SpotlightReport`, `SpotlightVolumeInfo` | index state + per-volume + exclusions |
| XcodeCleaner | 10.1 KB | `XcodeCleanerReport`, `XcodeCacheCategory` | per-category sizes (DerivedData, Archives, Simulators, Device Support) |
| MacAppInstaller | 8.0 KB + 29.6 KB bundles | `MacAppBundle`, `MacBundleApp`, `MacPackageSource` | curated catalog |

---

## 3. Per-module UX design

User directive (2026-04-16): "top 0.1% designer perspective, innovation". Each module gets module-idiomatic UX rather than cookie-cutter.

### 3.1 DNS Flusher (`dns-flusher`)

**Philosophy:** The trivial case deserves the most polished UI. This is a one-action module and should feel instant, confidence-inspiring, and delightful.

- **Auto-scan on load** (reads current DNS resolver state — no user action needed)
- **NO Scan button**
- **NO StatRow** (only one piece of data — where does it go? → ModuleHeader subtitle or status text)
- **Hero Flush button** — large, primary accent, full-width within its card
- **Current state display** above button:
  - "Active DNS: 8.8.8.8, 1.1.1.1" (from `scutil --dns` parsing)
  - "Last flushed: 2 minutes ago" or "Never flushed this session"
- **Success feedback**: on flush completion, briefly swap button to "✓ Flushed" with StatusSuccessBrush, then back to idle. No modal.
- Privilege warning card at bottom.

Sidebar: **Apps & Tools** (macOS)

### 3.2 Purgeable Space Manager (`purgeable-space`)

**Philosophy:** "Purgeable" is a macOS-specific concept most users don't understand. Education-first UX plus visual decomposition.

- **Auto-scan on load** (reads `diskutil info / df -h` + APFS purgeable query)
- **NO Scan button** (data is cheap)
- **Hero visualization**: horizontal stacked bar chart showing "Used (real) / Purgeable / Free" proportions. Color-coded (TextPrimaryBrush / StatusWarningBrush / StatusSuccessBrush).
- **3-stat row below** with the same numbers in absolute bytes
- **"What is purgeable space?" help card** — explains iCloud sync local cache, Time Machine local snapshots, Photos thumbnails, etc.
- **Per-category Purge actions** (when report exposes categories): "Purge Snapshots / Purge iCloud Cache / Purge All"
- Engine additive extension likely needed: per-category breakdown if Report only has aggregate

Sidebar: **Clean & Debloat** (macOS)

### 3.3 Spotlight Manager (`spotlight-manager`)

**Philosophy:** Power-user control over indexing. Currently complex because `mdutil` + exclusions + per-volume state is three orthogonal concerns. Unify them.

- **Auto-scan on load**
- **Optional "Refresh" button** (user may want to check status after external mdutil command) — small, not prominent
- **Stat row** (3 cards): "Index State" (Indexing / Ready / Disabled), "Indexed Volumes" (N), "Exclusions" (N paths)
- **Volumes list** (GlassCard 1): per-volume with indexed/disabled chip per row (like Docker's per-category). Click to toggle.
- **Exclusions as chip-list** (GlassCard 2): each excluded path shows as removable chip (× button). Add via "+ Add Folder" button (opens native folder picker).
- **Rebuild index** action (Danger-ish, takes disk time): separate bottom card with time estimate "~15 min on a 512 GB SSD"
- Engine additive: if engine can't add/remove exclusions today, scope to read-only display + defer mutations to Phase 5

Sidebar: **Advanced** (macOS)

### 3.4 Xcode Cleaner (`xcode-cleaner`)

**Philosophy:** Developer power-user module with mixed risk. Mirror Docker 4.3.3 Layout A.

- **Auto-scan on load** (`du` on ~/Library/Developer/Xcode/* — fast)
- **NO Scan button** — auto-refresh is sufficient
- **4-stat row**: DerivedData / Archives / iOS Simulators / Device Support (by bytes)
- **Safe Cleanup hero**: DerivedData + Archives (rebuild cost: minutes; no device re-pairing needed)
- **Granular Control**: per-category buttons for individual prune
- **Danger Zone**: iOS Simulators (re-download hours) + Device Support (re-pair required). Ack checkbox gates Apply.
- Engine additive: per-category Reclaimable fields if engine only exposes total

Sidebar: **Clean & Debloat** (macOS) — it's a cache cleaner

### 3.5 Mac App Installer (`mac-app-installer`)

**Philosophy:** Mirror Linux App Installer (Phase 4.3.5, accordion + live search). macOS-specific source types.

- **Auto-scan on load** — check installed state across ~125 apps
- **Manual Scan button stays** (scan is slow — `brew list --cask` + `mdfind "kMDItemKind=Application"` + App Store query per app)
- **Live search TextBox** in header (no scan button for search — TextChanged pattern)
- **StatRow 3 cards**: Total / Installed / Available
- **Accordion bundles** (~10 curated bundles from `MacAppBundles.cs`)
- **Per-app row** innovations:
  - **Source pill with brand color**: App Store (blue), Homebrew Cask (orange), DMG (grey)
  - **Install button inline per app** when preferred UX, OR batch "Install Selected" like Linux — decide during sub-phase based on engine ergonomics
- **Sticky action footer**: "N selected · App Store: X, Cask: Y, DMG: Z" + "Install Selected"
- Engine additive: track per-source counts like Snap/Flatpak

Sidebar: **Apps & Tools** (macOS)

---

## 4. Sequencing

Easy → hard, matching Phase 4.3 ordering philosophy:

1. **4.4.1 DNS Flusher** — simplest, validates macOS auto-scan pattern
2. **4.4.2 Purgeable Space Manager** — introduces hero-viz element (stacked bar chart)
3. **4.4.3 Spotlight Manager** — chip-list exclusions UX (new element)
4. **4.4.4 Xcode Cleaner** — Layout A proven from Docker, multi-category
5. **4.4.5 Mac App Installer** — largest scope, accordion + live search, end last

---

## 5. Architecture (inherited from Phase 4.3)

All established patterns from Phase 4.3 umbrella + sub-phases apply:
- Sealed-engine adapter interface per module (`IXxxEngine` + `XxxEngineAdapter`)
- DI: concrete + `IOptimizationModule` alias, Transient VM registration
- Shared `ViewModels/Shared/DelegateCommands.cs`
- Sidebar platform filter via `if (OperatingSystem.IsMacOS())` blocks in matching `Build<Category>` methods
- Localization: EN+TR in `LocalizationService.cs`, Turkish diacritics non-negotiable
- Tests: Theory-based locale parity + smoke + unit + DI

Engine additive extensions when Report lacks per-category data (same rule as 4.3): additive fields only, no contract breaks.

---

## 6. Testing target

| Source | Tests |
|--------|-------|
| 5 × VM unit (~15-22 each) | ~85-100 |
| 5 × View smoke (~6 each) | ~30 |
| 5 × Locale parity (Theory-multiplied) | ~130-180 |
| 5 × DI smoke | 5 |
| Baseline (Phase 4.3 close) | 1144 |
| **Phase 4.4 delivery target** | **~1400+ green** |

Target ±150 is acceptable — Theory test multipliers inflate counts.

---

## 7. Known debt carried

Same as Phase 4.3 plus:
- **macOS privilege escalation** — same gap as Linux. `sudo`-requiring operations (DNS flush, Spotlight rebuild, etc.) fail without elevation. Phase 5 work.
- **Mac App Installer uninstall** — same deferral as Linux App Installer (install-only in v1)
- **Spotlight exclusion mutations** — if engine doesn't support add/remove today, scoped to read-only display; mutation is Phase 5 work
- **6th macOS module** — deferred per §1

---

## 8. Out of scope

- Windows modules (nothing in Phase 4.4)
- DockerCleaner macOS sidebar exposure (Phase 5)
- V1 bridge removal (Phase 4.5)
- macOS VM validation — manual QA deferred until next in-person session
- 6th module (§1)

---

## 9. Rollback

Sub-phase granularity. Revert individual commits or whole branch to `c63a4cd`.

---

## 10. Sign-off

User delegated authority for Phase 4.4 decisions during absence (2026-04-16 message). Design approved by autonomous decision based on Phase 4.3 patterns + top-tier designer perspective. Implementation proceeds without per-gate approval; user will review the merged work on return.
