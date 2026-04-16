# Phase 5 — Polish + Cross-Cutting Infra Umbrella Design

**Date:** 2026-04-16
**Branch:** `phase-5-polish` (from `phase-4.4-macos-modules` HEAD `a2062c9`)
**Status:** Design approved (2026-04-16 interactive brainstorm). Executing autonomously per user delegation.

---

## 1. Context

Phases 1–4 shipped the full module surface: design system, sidebar + dashboard, AI features consolidation, 38-module refactor wave, 6 new Linux modules, 5 new macOS modules. Solution sits at 1558/1558 green on branch `phase-4.4-macos-modules` HEAD `a2062c9`. Wave 2 (feature dev) is CLOSED.

Phase 5 closes Wave 3 (Polish) of the Vision Doc §10 roadmap. Original Vision Doc targeted ~1 week for Phase 5. Actual scope now ~3–4 weeks because Phase 3 debt (11 items) + Phase 4.2 QA feature ideas (9 items) + Phase 4.3/4.4 privilege-IPC gap (~11 modules affected) accumulated during the refactor and feature waves.

Phase 5 is **too large for a single implementation plan**. This umbrella spec decomposes it into 5 waves, each with its own brainstorm → mini-spec → plan → implement cycle. Pattern matches Phase 4.3 and Phase 4.4.

---

## 2. Scope — 5 waves

### 5.1 Cleanup wave — mechanical (≈3–5 days)

- Payment view V1 → V2 token migration
- Upgrade view V1 → V2 token migration
- Settings view V1 → V2 token migration (largest; 207 LOC)
- Onboarding view V1 → V2 token migration
- V1 key safety sweep (repo-wide grep, residual cleanup)
- `AuraCoreTheme.axaml` delete + `App.axaml` StyleInclude removal
- FontManager **soft-hide** (sidebar entry + MainWindow route only; files, module, tests, localization keys ALL KEPT — user decision 2026-04-16, revertible)
- Windows `app-installer` sidebar category fix (Clean & Debloat → Apps & Tools)

### 5.2 Cross-cutting privilege IPC — ship-blocker (≈6–8 days)

**5.2.1 Linux polkit + systemd helper** (affects 6 modules: Journal, Snap/Flatpak, Docker, Kernel, LinuxAppInstaller, GRUB)

- Define `IShellCommandService` abstraction (+ `PrivilegedCommand` + `ShellResult` records)
- .NET 8 self-contained helper daemon `auracore-privhelper` exposing D-Bus service
- polkit `.policy` file with whitelisted action IDs (journalctl-vacuum, apt-get-autoremove, docker-prune, etc.)
- Linux impl of `IShellCommandService` calls the daemon over D-Bus
- Migrate 6 Linux module engines from `ProcessRunner.RunAsync("sudo", ...)` to `IShellCommandService.RunPrivilegedAsync(...)`

**5.2.2 macOS SMAppService + XPC helper** (affects 5 modules: DNS Flusher, Purgeable Space, Spotlight, Xcode Cleaner, Mac App Installer)

- macOS impl of `IShellCommandService` calls the XPC helper
- `com.auracore.privhelper` helper tool bundled in `Contents/Library/LaunchDaemons/`
- `SMAppService` install flow on first privileged action (macOS 13+)
- Notarization story documented (ship prep)
- Dev/test fallback: `sudo` direct invocation when helper not installed
- Migrate 5 macOS module engines to `IShellCommandService`

**Both sub-waves share the `IShellCommandService` abstraction.** The Windows impl stays delegated to the existing `AuraCore.PrivilegedService` IPC. Engines and VMs are platform-agnostic — only the DI-registered concrete impl changes.

### 5.3 Narrow mode wave — responsive (≈3–4 days)

- `INarrowModeService` observes Window.Bounds, exposes `IsNarrowMode` INPC
- `AIFeaturesView` narrow: <1000 px → 1-col stack + 80 px icon-only detail sidebar (Phase 3 §4.7 gap)
- `StatRow` narrow: <900 px → 1-col stack (affects 34+ modules)
- Module pages narrow audit pass (11 Phase 4.3/4.4 modules + 5 pilots)
- `BoundsToIsNarrowModeConverter` + `UniformGrid.Columns` responsive binding
- Responsive tests per module (wide + narrow variant smoke)

### 5.4 Phase 3 debt polish (≈3–4 days)

11 small fixes, batched 3–4 per commit:

1. `TextTransform="Uppercase"` value converter (AIFeatureCard kicker)
2. CORTEX status i18n wiring (`CortexAmbientService.ComputeStatusText`)
3. `SidebarCategoryVM.Badge` XAML binding (replace `IsAccent` flag)
4. Smart Optimize CTA deep-link to Recommendations section
5. Dashboard "Cortex is monitoring..." subtitle localization
6. HSTS preload directive cleanup (Nginx config)
7. AIFeaturesView back-nav discoverability ("← Back to Overview" relabel)
8. AIFeatureCard "View details →" affordance
9. ChatOptInDialog Step 2 polish (recommendation hero + progress clarity)
10. `IAuraCoreLLM.ReloadAsync` hook (hot-swap model without restart)
11. Old localization key cleanup (`nav.ai.insights` et al.)

`AppSettings` INPC + thread-safety is absorbed into 5.1 (the cleanup wave touches the same file during V2 migration).

### 5.5 QA feature ideas (≈4–6 days)

9 user-flagged items from Phase 4.2 QA sweep:

1. Cleaner consolidation — JunkCleaner + DiskCleanup merge decision (user-gated mid-impl)
2. Bloatware default Windows preset (one-click quick action)
3. Disk Health → Dashboard layout change (remove from Apps & Tools)
4. System Health clarity (subtitle + intro card OR repurpose)
5. Space Analyzer file-path drill-down (tree expansion)
6. Driver Updater write capability — needs 5.2 privilege IPC
7. Defender Manager write capability — needs 5.2 privilege IPC
8. Service Manager write capability — needs 5.2 privilege IPC
9. Symlink Manager UX polish

**Why 5.5 comes after 5.2:** items 6, 7, 8 need the privilege IPC landed first. Items 1–5, 9 can proceed anytime.

### 5.6 Milestone + memory (≈1 day)

Empty ceremonial commit closing Phase 5. Memory files updated: new `project_ui_rebuild_phase_5_complete.md`, index updated to mark Wave 3 CLOSED.

---

## 3. Wave sequencing (non-negotiable)

```
5.1 Cleanup          → clean token namespace (V2-only)
  ↓
5.2 Privilege IPC    → unblock Linux/macOS modules (ship-blocker)
  ↓
5.3 Narrow mode      → responsive sweep on clean tokens
  ↓
5.4 Phase 3 polish   → 11 small fixes on stable shell
  ↓
5.5 QA features      → feature work (items 6-8 gated by 5.2 being done)
  ↓
5.6 Milestone        → close Phase 5, Wave 3 CLOSED
```

Rationale: clean → unblock → responsive → polish → features. Each wave hardens the base for the next.

---

## 4. Architecture decisions (umbrella-level)

### 4.1 Sub-wave pattern (inherited from Phase 4.3/4.4)

Each wave:
1. Mini brainstorm (short — umbrella spec already locks most decisions)
2. Wave-specific mini-spec (`2026-04-XX-phase5-N-<topic>-design.md`)
3. Plan via writing-plans skill
4. Subagent-driven implementation
5. Supervisor verification (build + test + hex/diacritic audit)
6. Wave milestone commit (empty, ceremonial)

### 4.2 Branch strategy

Single branch `phase-5-polish` from `a2062c9`. All waves commit to this branch. Final branch ready for review/merge decision.

### 4.3 Test discipline

- Full solution `dotnet test` green after every sub-wave milestone
- No test regression tolerated (carry-forward from Phase 4.3/4.4 discipline)
- Expected growth: baseline 1558 → ~1900+ at Phase 5 close

### 4.4 V2 token discipline continues

After 5.1 closes, `AuraCoreTheme.axaml` is deleted. Any new XAML must use V2 tokens only. Raw hex remains forbidden.

### 4.5 Localization discipline

Every new UI-facing string gets EN + TR keys with proper Turkish diacritics (ğ, ü, ş, ı, İ, ö, ç). Supervisor diacritic audit runs on each locale block.

### 4.6 IShellCommandService abstraction contract

Defined in 5.2. Fixed interface surface:

```csharp
namespace AuraCore.Application.Interfaces.Platform;

public interface IShellCommandService
{
    Task<ShellResult> RunPrivilegedAsync(PrivilegedCommand command, CancellationToken ct = default);
}

public sealed record PrivilegedCommand(
    string Id,           // whitelist action id
    string Executable,
    string[] Arguments,
    int TimeoutSeconds = 60);

public sealed record ShellResult(
    bool Success,
    int ExitCode,
    string Stdout,
    string Stderr,
    PrivilegeAuthResult AuthResult);

public enum PrivilegeAuthResult
{
    AlreadyAuthorized,
    Prompted,
    Denied,
    HelperMissing
}
```

Platform impls: `WindowsShellCommandService` (delegates to `AuraCore.PrivilegedService` IPC + direct for non-privileged), `LinuxShellCommandService` (D-Bus to `auracore-privhelper`), `MacOSShellCommandService` (XPC to `com.auracore.privhelper`), `InProcessShellCommandService` (test/dev fallback — direct `ProcessRunner` call, no elevation).

### 4.7 Engine migration pattern (for 5.2)

Existing engines call `ProcessRunner.RunAsync("sudo", ...)` directly. Migration:

```csharp
// Before
var r = await ProcessRunner.RunAsync("sudo", "-n journalctl --vacuum-size=500M", ct);

// After
var r = await _shellCommandService.RunPrivilegedAsync(
    new PrivilegedCommand(
        Id: "journalctl-vacuum-size",
        Executable: "journalctl",
        Arguments: new[] { "--vacuum-size=500M" },
        TimeoutSeconds: 60),
    ct);
```

Engine public surface unchanged. VMs unaffected. Only DI wiring + engine internals change.

---

## 5. Known debt retired this phase

When Phase 5 closes:

- V1 theme bridge DELETED (was alive since Phase 2)
- FontManager hidden from sidebar (files kept per 2026-04-16 user decision)
- Windows app-installer sidebar category corrected
- All 11 Phase 3 debt items closed
- Linux + macOS privilege escalation unblocked
- All 9 QA feature ideas addressed (or explicitly deferred with memory note)
- Narrow mode responsive

---

## 6. Known debt carried forward (NOT Phase 5 scope)

- **macOS VM validation** — end-to-end QA on real macOS requires in-person session; deferred.
- **Linux VM validation** — same, end-to-end QA on Ubuntu 24.04 VM deferred.
- **6th macOS module** — post-phase retro decision.
- **Visual regression testing** (screenshot diff) — Phase 1 known debt, still deferred.
- **Light theme** — Vision Doc §12, still deferred.
- **Localization beyond EN/TR** — Vision Doc §12, still deferred.
- **Mobile/tablet layouts** — Vision Doc §12.
- **Deep-link URL routing** (e.g., `auracore://ai-features/insights`) — beyond 5.4 debt item 11.
- **macOS notarization + code signing pipeline** — documented in 5.2.2, executed at ship prep.

---

## 7. Success criteria

Phase 5 closes when:

- [ ] All 5 waves complete, each with its own milestone commit
- [ ] `AuraCoreTheme.axaml` file deleted; no V1 brush references in XAML
- [ ] FontManager hidden from sidebar + MainWindow route; files intact
- [ ] `IShellCommandService` abstraction shipped + 11 engines migrated (6 Linux + 5 macOS)
- [ ] `auracore-privhelper` Linux daemon + macOS XPC helper tool buildable
- [ ] All 11 Phase 3 debt items addressed
- [ ] 9 QA feature ideas addressed or explicitly deferred
- [ ] Narrow mode responsive across AIFeaturesView, StatRow, 16+ modules
- [ ] Full solution tests green (expected ~1900+ at close)
- [ ] Memory file + MEMORY.md index updated
- [ ] Branch ready for review/merge

---

## 8. Rollback strategy

Each wave has its own milestone commit. Revert granularity:

- Revert individual commit within a wave
- Revert a whole wave back to its parent milestone
- Worst case: `git reset --hard a2062c9` returns to Phase 4.4 close

Branch stays live until user approves Phase 5 milestone. No forced pushes.

---

## 9. Open questions deferred to sub-wave specs

Minor decisions punted:

1. Whether `JunkCleaner` + `DiskCleanup` consolidate (5.5 item 1 — needs user input mid-implementation)
2. Exact polkit action ID naming convention (5.2.1 decision)
3. Whether macOS helper ships as separate binary or embedded in main bundle (5.2.2 decision — affects size)
4. Narrow mode threshold exact px (5.3 — currently targeting 900/1000 per Phase 3 spec)
5. Whether "System Health" module gets repurposed or kept as-is (5.5 item 4)

None affect umbrella architecture.

---

## 10. Sign-off

Design approved 2026-04-16 via interactive brainstorm (3 sections + 3 questions answered). Visual companion used for 5.2 privilege IPC architecture diagram. User directive reinforced: skip spec review gate (per `feedback_skip_spec_review.md` memory), proceed directly to plan writing + execution.

Next: sub-wave 5.1 mini-spec → plan → implement cycle.
