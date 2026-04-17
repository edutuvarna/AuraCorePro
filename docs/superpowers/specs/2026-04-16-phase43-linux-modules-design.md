# Phase 4.3 — Linux Modules Feature Completion

**Date:** 2026-04-16
**Branch:** `phase-4.3-linux-modules` (from `phase-4-module-pattern` HEAD `cf1a930`)
**Author:** Deniz Özgür + Claude Opus 4.6
**Status:** Design approved, ready for sub-phase decomposition

---

## 1. Context

Phase 4.0–4.2 completed the UI refactor wave: 38 existing modules migrated to the Phase 4.0 shell pattern (`ModuleHeader` + `StatRow` + `GlassCard`). Branch `phase-4-module-pattern` sits at HEAD `cf1a930` with 663/663 tests green.

Vision Document §10 scoped Phase 4.3 as **feature development** — 7 new Linux modules building on the validated shell. The original framing assumed greenfield work (engine + IPC + UI per module).

**Discovery during this brainstorm:** `src/Modules/` already contains working engine scaffolds for 6 of the 7 candidates:

| Module | Engine code | Models | Note |
|--------|-------------|--------|------|
| `AuraCore.Module.JournalCleaner` | 7,081 bytes | `JournalReport.cs` | journalctl vacuum + boot enumeration |
| `AuraCore.Module.KernelCleaner` | 13,258 bytes | `KernelInfo.cs`, `KernelReport.cs` | old kernel package removal (apt/dnf/pacman) |
| `AuraCore.Module.DockerCleaner` | 10,778 bytes | `DockerReport.cs` | prune containers/images/volumes/networks |
| `AuraCore.Module.GrubManager` | 14,366 bytes | — | GRUB entries, default boot, update-grub |
| `AuraCore.Module.LinuxAppInstaller` | 8,084 + 25,684 bytes bundles | 3 model types | curated Linux app catalog (apt/dnf/flatpak/snap sources) |
| `AuraCore.Module.SnapFlatpakCleaner` | 8,518 bytes | — | snap/flatpak cache + unused runtime cleanup |

All 6 implement `IOptimizationModule` with `ScanAsync` / `OptimizeAsync`, use the shared `ProcessRunner` helper for shell invocations, declare `Platform => SupportedPlatform.Linux`, and register via an `AddXxxModule()` extension method. No IPC layer is required — engines execute in-process in the UI app.

**What is missing for all 6:**
1. UI view (XAML) using the Phase 4.0 shell
2. ViewModel (proper MVVM, not code-behind hybrid)
3. DI registration in `App.axaml.cs`
4. Sidebar entry (with Linux platform filter)
5. Module route in `MainWindow.CreateModuleView()`
6. Localization keys (EN + TR)
7. Tests (View smoke + ViewModel unit + i18n parity)

Phase 4.3 therefore shifts from "greenfield feature dev" to **UI completion + DI + tests** for 6 existing engine-ready modules. The 7th candidate slot remains open but deferred — see §2.3.

---

## 2. Scope

### 2.1 In scope

**6 modules**, each shipped with:
- `Views/Pages/<Module>View.axaml` + `.axaml.cs` (thin MVVM, ≤ 30 lines code-behind)
- `ViewModels/<Module>ViewModel.cs` (INPC, `DelegateCommand` commands, cancellation, error states)
- `App.axaml.cs` DI registration via existing `AddXxxModule()` extensions
- `SidebarViewModel` entry with `Platform="Linux"` filter (hidden on Windows/macOS)
- `MainWindow.CreateModuleView()` switch case
- `Locales/en.json` + `Locales/tr.json` keys (title, subtitle, labels, action buttons, status messages)
- View smoke tests (ctor, render-in-window, header title) — ~3 per module
- ViewModel unit tests (commands, scan success, scan error, optimize flow, cancellation) — ~6-10 per module
- Localization parity tests (EN/TR key alignment) — 2 per module

**Common infrastructure (one-time work):**
- A shared `ModuleViewModelBase` in `ViewModels/Base/` with the Scan→Report→Optimize pattern boilerplate (CancellationTokenSource, IsBusy, ProgressText, ScanCommand, OptimizeCommand, ErrorMessage). Candidates demonstrated during first sub-phase, extracted if used by ≥ 3 modules.
- Localization key conventions (`<moduleId>.title`, `<moduleId>.subtitle`, `<moduleId>.action.scan`, etc.)
- Standard smoke test template mirroring Phase 4.0 `ScheduleSectionTests`

### 2.2 Out of scope (forward-referenced)

- macOS modules (Phase 4.4 — 6 modules: DNS Flusher, Purgeable Space, Spotlight Manager, Xcode Cleaner, Mac App Installer, +1)
- V1 theme bridge (`AuraCoreTheme.axaml`) removal — Phase 4.5 after Payment/Upgrade/Settings/Onboarding migrate
- `StatRow` responsive narrow mode — Phase 5 (bundled with AIFeaturesView narrow mode)
- Module consolidation ideas (Journal+Kernel+Snap/Flatpak → "Linux System Cleanup" umbrella) — Phase 5 consolidation sprint
- Font Manager removal — Phase 5
- 7th Linux module (firewall/logrotate/timeshift/gamemode/etc.) — re-evaluate after 4.3 ships; not blocking

### 2.3 The 7th candidate — deferred decision

Vision Doc §10 listed 7 Linux modules; we have engines for 6. Potential 7th candidates:

| Candidate | Category | Value | Complexity |
|-----------|----------|-------|------------|
| UFW / firewalld Manager | Advanced | Parallels Windows Firewall Rules | Medium — engine doesn't exist |
| Log Rotate Manager | Clean & Debloat | System log retention tuning | Low — small engine |
| Timeshift Manager | Advanced | Backup/restore points | High — safety-critical |
| GameMode Integration | Apps & Tools | Gaming perf, parallels Gaming Mode | Low — small wrapper |
| Repository Manager | Advanced | apt/dnf sources management | Medium |

Deferred to post-4.3 re-evaluation because:
1. Adding a 7th triggers greenfield engine work (+50% scope)
2. 6 modules is already a ~2-week delivery (one sub-phase per module)
3. The "7 modules" number in Vision Doc is a rounding target, not a hard contract
4. GameMode + Log Rotate are the strongest candidates if the count matters — both small engines; can be a follow-on micro-phase 4.3.7

**Decision:** ship 6, re-evaluate 7th during Phase 4.3 retro.

### 2.4 Explicit non-goals

- **No engine refactor.** Engines as they exist pass their current contract (`IOptimizationModule`); UI binds to them. If a bug is discovered during UI work, fix narrowly — no cleanup sweeps.
- **No new primitives.** Phase 4.0's `ModuleHeader` / `StatRow` / `StatCard` + `GlassCard` cover all 6 layouts. One possible exception flagged inline: `AppBundleCard` for `LinuxAppInstaller` catalog UI — decision deferred to 4.3.5.
- **No sidebar redesign.** Entries slot into existing `Clean & Debloat` / `Apps & Tools` / `Advanced` categories with no structural change.
- **No new IPC.** Engines run in-process (same as existing Linux modules per `IOptimizationModule` contract). The `AuraCore.IPC.Contracts` project serves privileged-service RPC, not optimization engines.

---

## 3. Vision Document alignment

Phase 4.3 completes the Wave-2 feature-dev portion of the §10 roadmap:

```
Wave 1 (Refactor) — DONE
  ├── 4.0 Pattern + pilot (cf1a930)
  ├── 4.1 Cross-platform migration
  ├── 4.2 Remaining Windows
  └── 4.5 V1 leak cleanup

Wave 2 (Feature dev) — Phase 4.3 (this) + Phase 4.4
  ├── 4.3 Linux modules (6 modules, 6 sub-phases)
  └── 4.4 macOS modules (6 modules, ~6 sub-phases)

Wave 3 (Polish) — Phase 5
```

Per Phase 4.0 spec §3 ("Wave order is non-negotiable"), the refactor-first rule held: we didn't build new features on an un-refactored shell. Phase 4.3 is the payoff — new modules ship directly on the validated pattern.

---

## 4. Architecture decisions

### 4.1 UI pattern — MVVM (not code-behind hybrid)

Phase 4.0-4.2 preserved code-behind for 38 migrated modules because rewriting them was out of scope. Several use the "hidden TextBlock + `{Binding #ElementName.Text}`" pattern (e.g., `PackageCleanerView`).

**New Phase 4.3 modules follow proper MVVM** (the pattern established in Phase 3 for `AIFeaturesViewModel`, `ChatOptInDialogViewModel`, etc.):

- ViewModel exposes INPC properties
- View binds to VM via `DataContext`
- `.axaml.cs` contains only `InitializeComponent()` + `CreateVM()` DI helper
- Commands via a small `DelegateCommand` / `DelegateCommand<T>` (same helper used in Phase 3 AIFeaturesViewModel)

Justification:
- Greenfield code; no migration artifact to preserve
- Proper MVVM makes VM unit tests ergonomic (no Avalonia headless required for logic tests)
- Pattern consistency with Phase 3 AI features (the last greenfield wave)

### 4.2 Shell — Phase 4.0 pattern, no variants

All 6 use the flat-header shell:

```xml
<UserControl ...>
  <ScrollViewer>
    <StackPanel Spacing="16">
      <controls:ModuleHeader Title="..." Subtitle="...">
        <controls:ModuleHeader.Actions>
          <!-- scan/optimize buttons -->
        </controls:ModuleHeader.Actions>
      </controls:ModuleHeader>

      <!-- optional: stats -->
      <controls:StatRow>
        <controls:StatCard Label="..." Value="..." />
      </controls:StatRow>

      <!-- primary content -->
      <controls:GlassCard>
        <!-- module-specific body -->
      </controls:GlassCard>

      <!-- optional: secondary card (warnings, logs, history) -->
      <controls:GlassCard>...</controls:GlassCard>
    </StackPanel>
  </ScrollViewer>
</UserControl>
```

Layout variants (hero, toolbar) are NOT introduced in Phase 4.3. Every module's scan/optimize pattern fits the flat header.

### 4.3 V2 token discipline

Same rules as Phase 4.0:
- Colors via `DynamicResource TextPrimaryBrush / TextMutedBrush / BorderCardBrush / StatusSuccessBrush / StatusWarningBrush / StatusErrorBrush / AccentPrimaryBrush / BgCardBrush`
- Radii via `RadiusSm / RadiusMd / RadiusLg`
- Font sizes via `FontSizeBody / FontSizeBodySmall / FontSizeLabel / FontSizeHeading`
- No raw hex, no `#FFFFFF Opacity="X"` patterns, no V1 bridge keys

### 4.4 Engine integration

ViewModels receive `IOptimizationModule` instances via DI. Since the catalog registers all modules as `IOptimizationModule`, the VM needs a way to select *its* module.

**Approach:** inject the concrete type. Each engine class has a `public sealed class` declaration; VMs receive it directly:

```csharp
public class JournalCleanerViewModel(JournalCleanerModule engine, ILocalizationService loc) : ViewModelBase
{
    // ...
}
```

DI registration in `App.axaml.cs` registers both flavors:

```csharp
services.AddJournalCleanerModule();              // IOptimizationModule registration
services.AddSingleton<JournalCleanerModule>();   // concrete type for VM injection
services.AddTransient<JournalCleanerViewModel>();
```

This is the pattern existing modules use (verified via `PackageCleanerRegistration` et al.). Sub-phase plans will audit and correct if any module registers only as interface.

### 4.5 Platform filter

Sidebar entries declare `Platform = "Linux"`. `SidebarViewModel` already applies `OperatingSystem.IsLinux()` filtering (same logic used in Phase 4.1 for Systemd/Swap/Cron/Package). On Windows/macOS the 6 new entries are invisible.

Module routes in `MainWindow.CreateModuleView()` are unconditional — users on Windows can't navigate to a Linux-only module via the sidebar, and deep-link attempts route to `Dashboard` via the existing fallback (fixed in `7312711`).

### 4.6 Tier

All 6 default to `UserTier.Free` (matching existing Linux modules per Phase 3 archaeology — only Windows modules carry Pro-tier locks). `TierService.GetProModules()` is NOT extended.

### 4.7 Localization

Each module adds ~10-15 keys per locale following the convention:

```
<moduleId>.title
<moduleId>.subtitle
<moduleId>.action.scan
<moduleId>.action.optimize
<moduleId>.action.cancel
<moduleId>.status.idle
<moduleId>.status.scanning
<moduleId>.status.done
<moduleId>.status.error
<moduleId>.stat.<label>.title
<moduleId>.help.<topic>
```

Both `en.json` and `tr.json` stay in lockstep (LocalizationPhase3Tests pattern enforces parity).

---

## 5. Per-module overview

Brief summary of each module's UI concept. Detailed designs live in each sub-phase spec (4.3.1-4.3.6).

### 5.1 Journal Cleaner (`journal-cleaner`)
- **Category:** Clean & Debloat
- **Engine:** `JournalctlModule` — `journalctl --disk-usage` scan + `--vacuum-size` / `--vacuum-time` optimize variants
- **UI:** 3-stat row (Current Usage / File Count / Oldest Entry) + action buttons (Vacuum 500 MB, Vacuum 1 GB, Vacuum 7 days, Vacuum 30 days)
- **Risk:** Low — journalctl vacuum is reversible (just truncates old data)
- **Complexity:** Simplest — perfect pilot

### 5.2 Snap/Flatpak Cleaner (`snap-flatpak-cleaner`)
- **Category:** Clean & Debloat
- **Engine:** `SnapFlatpakCleanerModule` — snap cache + flatpak unused runtimes
- **UI:** 2-stat row (Snap Cache / Flatpak Cache) + action (Clean Snap, Clean Flatpak, Clean Both)
- **Risk:** Low — caches re-generate on next app launch
- **Complexity:** Low-medium

### 5.3 Docker Cleaner (`docker-cleaner`)
- **Category:** Advanced
- **Engine:** `DockerCleanerModule` — `docker system prune` with filter variants
- **UI:** 4-stat row (Images / Containers / Volumes / Build Cache) + action buttons (Prune All, Prune Images, Prune Volumes, Prune Build Cache) + warning card about active containers
- **Risk:** Medium — prune removes named volumes if detached; requires confirmation UX
- **Complexity:** Medium

### 5.4 Kernel Cleaner (`kernel-cleaner`)
- **Category:** Clean & Debloat
- **Engine:** `KernelCleanerModule` — list old kernels (apt/dnf/pacman), mark for removal (keep current + fallback)
- **UI:** Active kernel stat + list of removable kernels (checkbox per item) + action (Remove Selected) + safety warning
- **Risk:** High — removing wrong kernel bricks boot; engine keeps active + fallback by default
- **Complexity:** Medium-high

### 5.5 Linux App Installer (`linux-app-installer`)
- **Category:** Apps & Tools
- **Engine:** `LinuxAppInstallerModule` + `LinuxAppBundles.cs` (25 KB curated catalog across apt/dnf/flatpak/snap sources)
- **UI:** Bundle grid (Essentials / Media / Dev / Gaming categories) with per-app toggle, "Install Selected" action — likely needs an `AppBundleCard` primitive
- **Risk:** Medium — installing packages mutates system state; sudo required
- **Complexity:** High — largest UI footprint

### 5.6 GRUB Manager (`grub-manager`)
- **Category:** Advanced
- **Engine:** `GrubManagerModule` — list entries, set default (`grub-set-default`), regenerate config (`update-grub`)
- **UI:** Entry list with radio selection + default indicator + action (Set Default, Regenerate Config) + safety warnings
- **Risk:** Very high — boot loader; misconfiguration bricks boot
- **Complexity:** High — UX must emphasize safety

---

## 6. Sub-phase decomposition

Each module gets its own sub-phase with brainstorm → spec → plan → implement cycle. The 6 sub-phases are sequenced easy → hard to validate the pattern on low-risk modules first.

| Sub-phase | Module | Risk | Pattern validation | Engine size |
|-----------|--------|------|---------------------|-------------|
| **4.3.1** | Journal Cleaner | Low | Establishes `ModuleViewModelBase` if used again | 7 KB |
| **4.3.2** | Snap/Flatpak Cleaner | Low | Reuses Journal's VM base pattern | 8 KB |
| **4.3.3** | Docker Cleaner | Medium | First 4-stat row + prune confirmation UX | 11 KB |
| **4.3.4** | Kernel Cleaner | High | First checkbox-list UI on Phase 4.0 shell | 13 KB |
| **4.3.5** | Linux App Installer | Medium | First bundle-grid UX → may introduce `AppBundleCard` | 8 KB + 25 KB data |
| **4.3.6** | GRUB Manager | Very high | Safety-first UX, radio-select list | 14 KB |

### 6.1 Sub-phase workflow (per module)

Each sub-phase:
1. **Brainstorm** (30 min) — read engine, design VM surface + view layout, decide localization keys
2. **Spec** (writing) — short `2026-04-XX-phase43N-<module>-design.md` describing VM props, view structure, tests
3. **Plan** (writing-plans skill) — produces detailed task-by-task plan
4. **Implement** (subagent-driven) — VM + View + tests + DI + sidebar + localization in one shot, test suite green
5. **Validation** — `dotnet test` full suite green, targeted `dotnet test --filter` green, visual QA deferred to Linux VM session at end of Phase 4.3

### 6.2 Halt conditions

Sub-phases run sequentially. Any of these halts progression:
- Full test suite regresses beyond new module's tests
- `ModuleViewModelBase` needs a breaking change that retro-affects earlier sub-phases
- Engine bug discovered that requires cross-sub-phase coordination
- User review of a completed sub-phase flags a pattern problem

When halted, the fix is applied, earlier sub-phases are verified unaffected, then progress resumes.

---

## 7. Testing strategy

### 7.1 Level 1 — ViewModel unit tests (per module, ~6-10)

Standard suite for every module:
- `Ctor_DoesNotThrow_WithValidDependencies`
- `ScanCommand_InvokesEngineScan`
- `ScanCommand_PopulatesReport_OnSuccess`
- `ScanCommand_SetsError_OnException`
- `OptimizeCommand_RespectsCancellation`
- `OptimizeCommand_UpdatesProgress`
- `OptimizeCommand_ReportsFreedBytes_OnSuccess`
- `ErrorMessage_ClearsOnNewScan`

Uses `Moq` (already in test project) to fake the engine. Runs fast — no Avalonia headless.

### 7.2 Level 2 — View smoke tests (per module, ~3)

Mirrors Phase 4.0 `ScheduleSectionTests`:
- `Ctor_DoesNotThrow`
- `RenderInWindow_DoesNotCrash_OnLoaded`
- `ModuleHeader_HasExpectedTitle`

Uses `AvaloniaFact` + `AvaloniaTestBase.RenderInWindow` pattern.

### 7.3 Level 3 — Localization parity (per module, ~2)

- `<Module>Locale_EN_HasAllKeys`
- `<Module>Locale_TR_MirrorsEN`

Reads `en.json` + `tr.json`, asserts every EN key has a TR counterpart and vice versa.

### 7.4 Level 4 — DI smoke test (aggregated, 1 per sub-phase)

Extends existing `DependencyInjectionSmokeTests.cs`:

```csharp
[AvaloniaFact]
public void Phase43_JournalCleaner_Resolves()
{
    var sp = BuildServiceProvider();
    sp.GetRequiredService<JournalCleanerViewModel>().Should().NotBeNull();
}
```

### 7.5 Level 5 — Manual visual QA (end of Phase 4.3, single session)

Deferred until Phase 4.3.6 milestone. Boot a Linux VM (Ubuntu 24.04 or Fedora 40), launch the built app, exercise each module's golden path:
- Scan → populated stats
- Optimize (low-stakes items) → progress bar → freed bytes displayed
- Cancel mid-optimize → clean cancellation, no partial state corruption
- Error path (e.g., `journalctl` not installed) → error card displayed, no crash

Visual regression tooling (screenshot diff) remains deferred per Phase 4.0 known debt — not introduced here.

### 7.6 Test count targets

| Source | Tests |
|--------|-------|
| 6 × VM unit tests | ~48-60 |
| 6 × View smoke tests | ~18 |
| 6 × Localization parity | ~12 |
| 6 × DI smoke | 6 |
| Baseline (Phase 4 refactor wave close) | 663 |
| **Phase 4.3 delivery target** | **~747-759 green** |

**Hard rule:** no test may fail at any sub-phase milestone commit. Full suite is green before each sub-phase closes.

---

## 8. Per-module deliverables template

Every sub-phase delivers exactly this set. Sub-phase plans template off this list.

### 8.1 Code

```
src/UI/AuraCore.UI.Avalonia/
├── Views/Pages/<Module>View.axaml                          [new]
├── Views/Pages/<Module>View.axaml.cs                       [new, ≤ 30 lines]
├── ViewModels/<Module>ViewModel.cs                         [new]
├── App.axaml.cs                                            [edit: register VM + engine concrete]
└── Locales/en.json + tr.json                               [edit: add ~12 keys each]

src/UI/AuraCore.UI.Avalonia/ViewModels/
└── SidebarViewModel.cs                                     [edit: add entry, Platform=Linux]

src/UI/AuraCore.UI.Avalonia/Views/
└── MainWindow.axaml.cs                                     [edit: CreateModuleView switch case]
```

### 8.2 Tests

```
tests/AuraCore.Tests.UI.Avalonia/
├── ViewModels/<Module>ViewModelTests.cs                    [new, ~6-10 tests]
├── Views/<Module>ViewTests.cs                              [new, ~3 tests]
├── Localization/<Module>LocaleParityTests.cs               [new, ~2 tests]
└── DependencyInjectionSmokeTests.cs                        [edit: 1 new fact]
```

### 8.3 Commit template

```
feat(phase-4.3.N): <Module> UI + VM + DI wiring

- View using Phase 4.0 shell (ModuleHeader + optional StatRow + GlassCard)
- Proper MVVM ViewModel with cancellable scan/optimize commands
- DI registered, sidebar entry (Linux platform filter), localization EN+TR
- ~N tests added (VM unit + view smoke + i18n + DI)

Engine: existing AuraCore.Module.<Module> (<size> bytes, unchanged)

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
```

---

## 9. Success criteria

Phase 4.3 closes when all 6 sub-phases are complete and:

- [ ] 6 modules fully shippable on Linux (UI + VM + DI + sidebar + i18n + tests)
- [ ] Full solution ~663 → ~747-759 tests green, zero regressions
- [ ] No raw hex or V1 bridge keys in any new XAML
- [ ] Each `.axaml.cs` ≤ 30 lines (thin MVVM)
- [ ] All 6 sidebar entries hidden on Windows/macOS (verified via `SidebarViewModelPlatformFilterTests` extension)
- [ ] Each module demos a working scan + optimize cycle on an Ubuntu 24.04 VM (manual QA at 4.3.6 close)
- [ ] Per-module commit + Phase 4.3 milestone commit
- [ ] Memory file updated with Phase 4.3 close + Phase 4.4/5 hand-off

---

## 10. Branch / commit strategy

Branch `phase-4.3-linux-modules` from `phase-4-module-pattern` HEAD `cf1a930`.

Per-sub-phase structure:
- Sub-phase brainstorm produces a short design doc (committed as a single commit)
- Sub-phase plan file (`.superpowers/plans/<YYYY-MM-DD>-phase43N-<module>.md`) — committed with plan
- Sub-phase implementation — 1 atomic commit per task or a single batched commit per sub-phase (subagent-driven default)
- Sub-phase milestone — empty ceremonial commit closing the sub-phase

Phase 4.3 milestone at end — empty commit labeled `milestone: Phase 4.3 Linux Modules COMPLETE`.

Branch merges to `main` only after Phase 4.3 + Phase 4.4 both close (Wave 2 boundary). Interim merges to `phase-4-module-pattern` at each sub-phase milestone are acceptable if Phase 4.4 starts in a separate branch.

---

## 11. Rollback strategy

Sub-phase granularity enables safe revert points:

- Revert a single module: `git revert` the sub-phase's commits → removes View/VM/tests/DI/sidebar/i18n cleanly. Engine (pre-existing) remains untouched.
- Revert the whole phase: `git reset --hard cf1a930` returns to Phase 4 refactor-wave close.
- Worst case (pattern failure after sub-phase 4.3.4): redesign `ModuleViewModelBase` in a fresh brainstorm, amend sub-phase 4.3.1-4.3.4 specs, re-execute.

Branch stays live until user approves the Phase 4.3 milestone. No forced pushes, no amends of committed sub-phase milestones.

---

## 12. Known debt carried forward

These remain open in Phase 5 or later:

1. **Narrow-mode responsiveness for module pages** — Phase 4.3 views assume ≥ 1000 px. Phase 5 polish (with AIFeaturesView + StatRow narrow mode).
2. **Visual regression tooling** — still deferred (Phase 4.0 known debt).
3. **V1 theme bridge** — alive until Phase 4.5 (Payment/Upgrade/Settings/Onboarding migration).
4. **Consolidation debate** — Journal + Kernel + Snap/Flatpak as a single "Linux System Cleanup" umbrella is a user-surfaced Phase 5 idea (QA feedback memory `project_feature_ideas_qa_2026_04_16.md`). Phase 4.3 ships them separate.
5. **7th Linux module** — post-4.3 decision, see §2.3.
6. **`ModuleViewModelBase` primitive** — may be extracted during 4.3.1-4.3.3 if repetition justifies. If only two modules share the pattern, it stays inline.
7. **Linux privilege escalation** — pre-existing architectural gap. `AuraCore.PrivilegedService` is Windows-only (Registry write IPC). Linux engines invoke `ProcessRunner.RunAsync(...)` directly, inheriting the user's privileges. Operations requiring sudo (kernel-image removal, update-grub, docker prune without docker-group membership, snap install, flatpak remove-unused) will fail with "permission denied" when the user runs AuraCore as non-root. **Phase 4.3 scope:** every sub-phase must (a) surface privilege-related errors with actionable copy (e.g., "Run AuraCore with sudo or add yourself to the `docker` group"), (b) mark high-sudo-requirement actions with a shield icon + tooltip, and (c) NOT attempt to silently escalate. **Out of scope:** a cross-platform `IShellCommandService` that routes privileged calls through a Linux systemd helper (polkit rules). That is a dedicated Linux-privilege-architecture project, likely between Phase 4.4 and Phase 5.

---

## 13. Open questions deferred to sub-phase specs

Each sub-phase answers its own:
1. Exact stat list per module (Journal: 3? 4?)
2. Action button labels (localization wording — TR idiom for "prune" / "vacuum")
3. Error copy + retry affordance
4. Progress UX — linear bar vs step-by-step list
5. Confirmation dialog threshold (Docker prune: always? > threshold? never?)
6. List UX for checkbox modules (Kernel Cleaner) — virtualization needed?
7. App catalog grid layout (`LinuxAppInstaller`) — flat grid vs categorized tabs

None affect the umbrella architecture.

---

## 14. Sign-off

Design approved by Deniz Özgür on 2026-04-16 during Phase 4.3 brainstorm session. Key decision: ship 6 modules (engines already exist), defer 7th to post-phase retro. Architecture: MVVM + Phase 4.0 shell + platform filter. Sequencing: easy → hard (Journal → Snap/Flatpak → Docker → Kernel → LinuxAppInstaller → GRUB).

Next: sub-phase 4.3.1 (Journal Cleaner) — brainstorm → spec → plan → implement cycle.
