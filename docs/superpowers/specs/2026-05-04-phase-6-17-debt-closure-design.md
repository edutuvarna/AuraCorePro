# Phase 6.17 — Platform Debt Closure + Privileged Ops Feedback — Design

**Date:** 2026-05-04
**Author:** Brainstormed in Phase 6.17 session (post-v1.8.0 ship)
**Status:** Locked, ready for `superpowers:writing-plans` skill
**Predecessor:** [Phase 6.16 Linux Platform Awareness](./2026-04-28-phase-6-16-linux-platform-awareness-design.md)
**Trigger:** v1.8.0 shipped on 2026-04-28 (origin/main `09128f3`, tag `9cf201a`). Linux VM smoke surfaced three remaining bugs: System Health storage drives showing `-2147483648%` on virtual filesystems; CA1416 attribute cascade for 7 Windows-only modules deferred from 6.16.F due to a 62-error blast radius; privileged operations (RAM Optimizer, Junk Cleaner) silently no-op when the privilege helper isn't running, leaving the user with no feedback.

## Goal

Close three buckets of carry-forward debt from Phase 6.16:

1. **System Health display bug** — virtual filesystems (`/sys`, `/proc`, `/dev`, `/sys/kernel/security`, `/dev/pts`, `/run`) report 0-byte total, causing `(int)((1.0 - 0/0) * 100) = (int)NaN = int.MinValue` displayed as `-2147483648%`.
2. **`[SupportedOSPlatform("windows")]` cascade** — DefenderManager / FirewallRules / AppInstaller / DriverUpdater / GamingMode / StorageCompression / BloatwareRemoval module classes still lack the attribute. Plus Linux modules need `[SupportedOSPlatform("linux")]` and macOS modules need `[SupportedOSPlatform("macos")]` (the macOS pre-release checklist's prerequisite).
3. **Privileged-ops feedback loop** — operations that require the privilege helper currently fail silently when the helper is missing. RAM Optimizer flashes a "running" indicator for ~1s then returns to ready state with zero visible change. User has no way to tell whether the operation actually did anything.

After Phase 6.17 closes: System Health renders correctly across all platforms; CA1416 analyzer flow is complete (0 errors, all platform-specific modules explicitly annotated); privileged actions surface a clear actionable diagnostic when the helper is missing instead of silent no-op; 6 highest-pain Linux/Windows modules return rich `OperationResult` so post-action banners can show `Success / Skipped / Failed` with reasons + remediation.

## Non-goals

- **`install-privhelper.sh` deploy + real privileged-ops end-to-end smoke** — Phase 6.18 (user explicit). 6.17 makes the *feedback path* correct; 6.18 verifies the *operation path* with the helper actually deployed.
- **macOS notarization / signing** — Mac hardware blocker remains; reserved for the dedicated macOS phase once hardware arrives. Phase 6.17 only adds the `[SupportedOSPlatform("macos")]` attributes (the build-hygiene prerequisite that the macOS pre-release checklist already lists).
- **All 50 modules refactored to `OperationResult`-typed return** — only the 6 highest-pain modules adopt the new shape (RAM Optimizer, Junk Cleaner, SystemdManager, SwapOptimizer, PackageCleaner, JournalCleaner). The other 40+ modules keep their existing `OptimizationResult` shape; incremental opt-in migration to Phase 6.18+.
- **D-Bus presence subscribe** for auto-refresh on helper install. UnavailableModuleView's "Try Again" already supports manual re-check; auto-refresh is Phase 6.18+.
- **App-level `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`** beyond CA1416 (nullability, etc.) — broader cleanup, separate phase.

## Audit findings reference

This spec consolidates evidence from the Phase 6.16 Linux VM smoke session:

### System Health virtual-fs underflow

`src/Modules/AuraCore.Module.SystemHealth/SystemHealthModule.cs:227-230`:

```csharp
var totalGb = d.TotalSize / (1024.0 * 1024 * 1024);     // 0.0 for /sys, /proc
var freeGb  = d.AvailableFreeSpace / (1024.0 * 1024 * 1024);  // 0.0
var usedPct = (int)((1.0 - freeGb / totalGb) * 100);    // (int)NaN = int.MinValue
drives.Add(new DriveReport(d.Name, d.VolumeLabel, d.DriveFormat, totalGb, freeGb, usedPct));
```

Two issues compounded:
- No guard against `totalGb <= 0` before division
- Virtual filesystems shouldn't be in the user-facing storage list at all (Linux mounts `/sys`, `/proc`, `/dev`, `/dev/pts`, `/run`, `/sys/kernel/security` as `tmpfs` / `proc` / `sysfs` / `devpts` / `securityfs` — not real storage devices)

### CA1416 cascade — 7 deferred Windows modules

Phase 6.16.F closure report (commit `cef7b1f`) flagged: `DefenderManagerModule`, `FirewallRulesModule`, `AppInstallerModule`, `DriverUpdaterModule`, `GamingModeModule`, `StorageCompressionModule`, `BloatwareRemovalModule`. Annotating all 7 in Phase 6.16.F triggered **62 new CA1416 errors** in dependent code paths (Views, DI extensions, DashboardView call sites). The 6.16.F closure rule "STOP if >50 new errors" forced deferral.

Per-class error contribution (estimated from 6.16.F transcript):
- DefenderManagerModule → ~8 errors (DefenderManagerView + DI extension)
- FirewallRulesModule → ~10 errors (FirewallRulesView + Settings page reference)
- AppInstallerModule → ~6 errors (AppInstallerView + Dashboard quick-action)
- DriverUpdaterModule → ~5 errors (DriverUpdaterView)
- GamingModeModule → ~7 errors (GamingModeView + Dashboard tile)
- StorageCompressionModule → ~9 errors (StorageCompressionView + GenericModuleView placeholder + DashboardView)
- BloatwareRemovalModule → ~17 errors (BloatwareRemovalView is the largest — direct registry calls + service controller + DashboardView quick-action)

Approach Phase 6.17.B uses: **per-module commits** with the same fix patterns Phase 6.16.F applied successfully:
1. Class-level `[SupportedOSPlatform("windows")]` on the module class itself
2. Class-level annotation on the corresponding View page (which only constructs and consumes the Windows-only module)
3. Class-level annotation on the DI registration extension class
4. Method-level annotation on factory delegates that return Windows-only types
5. `#pragma warning disable CA1416` ... `#pragma warning restore CA1416` block when a lambda factory's body can't be analyzer-traced (Phase 6.16.F precedent: MainWindow's RegisterAllModuleViews Windows block)
6. Extract Windows-only LINQ projections into `[SupportedOSPlatform("windows")] private static` helpers when annotation can't propagate through closures (Phase 6.16.F precedent: ServiceManagerEngine.ProjectEntry)

Each module gets its own commit. After every commit, run `dotnet build AuraCorePro.sln -c Release 2>&1 | grep "error CA1416"` to confirm clean. If a single module's annotation cascade exceeds 20 new errors, STOP and report (this matches the 6.16.F STOP rule, scaled for 7 separate modules instead of all-at-once).

### Privileged-ops silent no-op

User-reported: "RAM optimizer'a tıklıyorum, kısa bir şey oluyor (~1s), hiçbir şey değişmiyor." Reproducer:

1. Sidebar → RAM Optimizer
2. "Optimize Now" button
3. Visual: brief "running" state, returns to "ready" state
4. Actual: `IShellCommandService.RunPrivilegedAsync(echo 3 > /proc/sys/vm/drop_caches)` invoked
5. Helper not running on VM (we never deployed `install-privhelper.sh`) → D-Bus connection fails inside `LinuxShellCommandService` → caught silently by the module's `try/catch` → returns "success" `OptimizationResult`
6. UI shows generic success toast → user thinks it worked

Two compounding problems:
- **Pre-flight check missing**: The "Optimize Now" button never asks "is the helper actually running?" before kicking off the operation. Should have a guard.
- **Result shape lossy**: `OptimizationResult` has only `Success: bool` + `BytesFreed: long` + `Duration: TimeSpan`. No way to distinguish "actually freed 2.3 GB" from "skipped because helper missing" from "failed because /proc/sys/vm/drop_caches doesn't exist on this kernel".

## Locked design decisions

### D1 — `OperationResult` record (new, additive)

Lives in `src/Core/AuraCore.Application/Models/OperationResult.cs` (next to existing `OptimizationResult`):

```csharp
namespace AuraCore.Application;

public enum OperationStatus
{
    Success,
    Skipped,        // Operation skipped (precondition not met — typically helper missing)
    Failed,         // Operation attempted but errored
}

public sealed record OperationResult(
    OperationStatus Status,
    long BytesFreed,
    int ItemsAffected,
    string? Reason,                 // Human-readable, localizable key OR raw string
    string? RemediationCommand,     // Copy-pasteable shell line, null if N/A
    TimeSpan Duration)
{
    public static OperationResult Success(long bytesFreed, int itemsAffected, TimeSpan duration) =>
        new(OperationStatus.Success, bytesFreed, itemsAffected, null, null, duration);

    public static OperationResult Skipped(string reason, string? remediationCommand = null) =>
        new(OperationStatus.Skipped, 0, 0, reason, remediationCommand, TimeSpan.Zero);

    public static OperationResult Failed(string reason, TimeSpan duration) =>
        new(OperationStatus.Failed, 0, 0, reason, null, duration);
}
```

This is **additive** — modules that adopt it gain a new method `Task<OperationResult> RunOperationAsync(...)`. The existing `Task<OptimizationResult> OptimizeAsync(...)` keeps working for the other 40+ modules. ViewModel/View binding consumes whichever the module provides.

### D2 — `IPrivilegedActionGuard` interface (new, UI-layer service)

Lives in `src/UI/AuraCore.UI.Avalonia/Services/IPrivilegedActionGuard.cs`:

```csharp
namespace AuraCore.UI.Avalonia.Services;

public interface IPrivilegedActionGuard
{
    /// <summary>
    /// Pre-flight check before invoking a privileged action. If the privilege
    /// helper is unavailable, surfaces an actionable modal dialog explaining
    /// the requirement and how to install the helper, then returns false.
    /// Returns true if the helper is available and the caller should proceed.
    /// </summary>
    Task<bool> TryGuardAsync(
        string actionDescription,
        string? remediationCommandOverride = null,
        CancellationToken ct = default);
}

internal sealed class PrivilegedActionGuard : IPrivilegedActionGuard
{
    private readonly IHelperAvailabilityService _helper;

    public PrivilegedActionGuard(IHelperAvailabilityService helper) => _helper = helper;

    public async Task<bool> TryGuardAsync(
        string actionDescription,
        string? remediationCommandOverride = null,
        CancellationToken ct = default)
    {
        // Windows: privilege model is UAC elevation — guard is a no-op (Process.Start
        // with elevation handles it). Only Linux/macOS need the helper-presence check.
        if (OperatingSystem.IsWindows()) return true;

        if (!_helper.IsMissing) return true;

        var remediation = remediationCommandOverride
            ?? "sudo bash /opt/auracorepro/install-privhelper.sh";

        var dialog = new Views.Dialogs.PrivilegeHelperRequiredDialog(actionDescription, remediation);
        var top = global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetimeExtensions.GetTopLevel();
        if (top is global::Avalonia.Controls.Window window)
            await dialog.ShowDialog(window);
        return false;
    }
}
```

DI registration in `App.axaml.cs`: `sc.AddSingleton<IPrivilegedActionGuard, PrivilegedActionGuard>();`

### D3 — `PrivilegeHelperRequiredDialog` UserControl (new modal)

Mirrors `UnavailableModuleView`'s visual structure (consistent UX) but as a modal `Window`:

- Title: localized "Privilege helper required"
- Body: `actionDescription` (e.g. "RAM cache flush requires root access via the privilege helper.")
- Reason block: "The privilege helper service is not detected. Install it once with the command below; the app will use it for all privileged operations."
- Code block with `remediationCommand` + Copy button
- Documentation link button (opens `https://docs.auracore.pro/linux/privilege-helper` — placeholder URL, real URL Phase 6.18)
- Close button
- "I've installed it, try again" button (closes dialog + emits a re-check event the caller can subscribe to)

XAML at `src/UI/AuraCore.UI.Avalonia/Views/Dialogs/PrivilegeHelperRequiredDialog.axaml(.cs)`. New 5 localization keys (EN+TR):
- `privhelper.dialog.title` — "Privilege helper required"
- `privhelper.dialog.reason` — "The privilege helper is required to apply system-level changes. Install it once and we'll use it for all privileged actions."
- `privhelper.dialog.docs` — "Open documentation"
- `privhelper.dialog.tryAgain` — "I've installed it"
- `privhelper.dialog.closeBtn` — "Close"

### D4 — System Health virtual-fs filtering + zero-guard

Two changes in `src/Modules/AuraCore.Module.SystemHealth/SystemHealthModule.cs:223-233`:

```csharp
foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady))
{
    try
    {
        // Phase 6.17.A: skip virtual filesystems — they're not user-facing storage
        // and reporting 0-byte capacity caused the -2147483648% display bug.
        if (IsVirtualFilesystem(d)) continue;

        var totalGb = d.TotalSize / (1024.0 * 1024 * 1024);

        // Phase 6.17.A: zero-capacity guard — even after virtual-fs filter, some
        // ready drives can momentarily report TotalSize=0 (slow USB enumeration,
        // ramdisks). Don't divide by zero.
        if (totalGb <= 0) continue;

        var freeGb  = d.AvailableFreeSpace / (1024.0 * 1024 * 1024);
        var usedPct = (int)Math.Clamp((1.0 - freeGb / totalGb) * 100, 0, 100);
        drives.Add(new DriveReport(d.Name, d.VolumeLabel, d.DriveFormat, totalGb, freeGb, usedPct));
    }
    catch { }
}

private static bool IsVirtualFilesystem(DriveInfo d)
{
    // .NET DriveInfo.DriveFormat returns the kernel-reported filesystem type on Linux.
    // Drop the well-known virtual ones; treat everything else (ext4, btrfs, xfs,
    // ntfs, apfs, hfsplus, exfat, fat32, ...) as real storage.
    var fmt = d.DriveFormat?.ToLowerInvariant() ?? string.Empty;
    return fmt is "tmpfs" or "devtmpfs" or "proc" or "sysfs" or "devpts"
                or "securityfs" or "cgroup" or "cgroup2" or "pstore"
                or "bpf" or "tracefs" or "debugfs" or "configfs"
                or "fusectl" or "binfmt_misc" or "autofs" or "mqueue"
                or "hugetlbfs" or "rpc_pipefs" or "nsfs";
}
```

`Math.Clamp(..., 0, 100)` is a defense-in-depth — even if a future floating-point edge case slips through, the displayed percent is bounded and never an integer-underflow scream.

### D5 — `[SupportedOSPlatform]` rollout strategy (Wave B/C/D)

Per-attribute commit, per-module. After each commit, full Release build with CA1416-as-error to confirm clean. The 6.16.F fix patterns reused exactly:

| Cascade pattern | When to apply | Phase 6.16.F precedent |
|---|---|---|
| Class-level `[SupportedOSPlatform("X")]` on module class | Always | All 5 successful 6.16.F annotations |
| Class-level on View page | View page exclusively constructs/binds the Windows-only module | AutorunManagerView, RegistryOptimizerView, TweakListView in 6.16.F |
| Class-level on `<Module>Registration` static class | DI extension factories trip CA1416 cascade | All 5 successful 6.16.F DI extension annotations |
| Method-level on factory delegate returning Windows-only type | MainWindow.CreateTweakListView in 6.16.F | One precedent already in repo |
| `#pragma warning disable CA1416` block | Lambda body that closes over Windows-only types and analyzer can't trace through closure | MainWindow.RegisterAllModuleViews Windows block in 6.16.F |
| Extract `[SupportedOSPlatform("windows")] private static` helper | LINQ projection inside a Task.Run can't be annotated as a lambda | ServiceManagerEngine.ProjectEntry, StartupOptimizerView.ScanReg in 6.16.F |

For Linux module annotations (Wave C), `[SupportedOSPlatform("linux")]` may also surface CA1416 cascade but the dependency graph is smaller (these modules are only consumed in `if (OperatingSystem.IsLinux())` blocks already). For macOS (Wave D), same — small graph.

**Per-module cascade STOP rule**: if a single module's annotation produces >20 new CA1416 errors solution-wide, STOP and report. This is the per-module analog of 6.16.F's >50 solution-wide rule, scaled because we're going one module at a time instead of all 7 in one shot.

### D6 — Module adoption of `OperationResult` (Wave F)

6 modules adopt `IOperationModule` extension contract additively:

```csharp
// New optional interface — modules opt in by implementing it on top of IOptimizationModule
public interface IOperationModule : IOptimizationModule
{
    /// <summary>
    /// Phase 6.17: rich-result entry point for the module's primary action.
    /// Replaces the lossy OptimizeAsync return for modules where Skipped vs
    /// Failed vs Success vs (which reason) matters to the user.
    /// </summary>
    Task<OperationResult> RunOperationAsync(
        OptimizationPlan plan,
        IPrivilegedActionGuard guard,
        IProgress<TaskProgress>? progress = null,
        CancellationToken ct = default);
}
```

The 6 adopting modules:

| Module | Privileged op | Action description (loc key) | Remediation |
|---|---|---|---|
| `RamOptimizerModule` | `echo 3 > /proc/sys/vm/drop_caches` (Linux) / `EmptyWorkingSet` Win API (Windows — no helper needed) | "Flush RAM caches" | (default) |
| `JunkCleanerModule` (cross-platform) | `apt clean` / `dnf clean` / Windows tmp folder cleanup (Windows: no helper) | "Delete cached package files" | (default) |
| `SystemdManagerModule` (Linux) | `systemctl enable/disable/mask/unmask` | "Modify systemd service state" | (default) |
| `SwapOptimizerModule` (Linux) | `swapon` / `swapoff` / `sysctl vm.swappiness=N` | "Modify swap configuration" | (default) |
| `PackageCleanerModule` (Linux) | `apt autoremove` / `dnf clean` / `pacman -Sc` | "Remove orphaned packages" | (default) |
| `JournalCleanerModule` (Linux) | `journalctl --vacuum-size=...` | "Vacuum journal logs" | (default) |

The View/ViewModel for each updates the post-action UI:

```
[Success]   "Freed 2.3 GB across 1,247 items in 4.2s."
[Skipped]   "Skipped: privilege helper required. [Install command]"
[Failed]    "Failed: <reason>. [Try again]"
```

Visual: small banner below the action button, color-coded (green/amber/red), auto-dismisses after 20 seconds OR replaced by next operation result. Banner uses 3 new loc keys: `op.result.success` (with `{0}` bytes formatted, `{1}` items, `{2}` duration), `op.result.skipped` (with `{0}` reason), `op.result.failed` (with `{0}` reason).

### D7 — `PrivilegeHelperMissingBanner` Linux validation (Wave G)

Existing component at `src/UI/AuraCore.UI.Avalonia/Views/Banners/PrivilegeHelperMissingBanner.axaml(.cs)` (Phase 5.2.0 Task 11). MainWindow already wires `_helperAvailability.PropertyChanged` → `SyncBannerVisibility()` (line 84-94 of MainWindow). Wave G's job:

1. **Smoke verify on Linux VM**: launch the app on the VM (helper NOT installed), capture screenshot of MainWindow top edge — does the banner render? If yes, what does it say? Is the install command shown?
2. **If banner missing**: read `IHelperAvailabilityService.IsBannerVisible` flag, trace why it's not true at startup on Linux. Likely culprits:
   - `HelperAvailabilityService` constructor doesn't probe helper at startup; only on first failed `RunPrivilegedAsync` call. → Fix: probe at startup via D-Bus presence check.
   - `IsBannerVisible` defaults to false and only flips on `ReportMissing()` call. → Fix: probe at startup.
3. **Banner copy update**: if the existing banner just says "Privilege helper missing" without an actionable command, extend it to include the install command + Copy button (matches `PrivilegeHelperRequiredDialog` style).
4. **Localization audit**: ensure banner has TR translation.

Wave G's output is one verification document (`docs/superpowers/phase-6-17-banner-verify.md`) + any fix commits needed.

### D8 — Single Phase 6.17, 8 sub-waves (mirrors Phase 6.16 cadence)

| Sub-wave | Title | Effort |
|---|---|---|
| **6.17.A** | System Health: virtual-fs filter + zero-capacity guard + Math.Clamp + tests | 0.5d |
| **6.17.B** | `[SupportedOSPlatform("windows")]` on 7 deferred modules — per-module commits with cascade fix | 1.5d |
| **6.17.C** | `[SupportedOSPlatform("linux")]` on Linux modules + cascade fix | 0.5d |
| **6.17.D** | `[SupportedOSPlatform("macos")]` on macOS modules + cascade fix | 0.5d |
| **6.17.E** | `OperationResult` record + `IOperationModule` extension + `IPrivilegedActionGuard` interface + impl + DI registration + `PrivilegeHelperRequiredDialog` modal + 5 EN+TR loc keys | 1d |
| **6.17.F** | 6 modules adopt `IOperationModule.RunOperationAsync` + view post-action banner + 3 EN+TR result loc keys | 1.5d |
| **6.17.G** | `PrivilegeHelperMissingBanner` Linux smoke verify + fix wiring if missing + verify-doc commit | 0.5d |
| **6.17.H** | Tests + Linux VM smoke matrix update + CHANGELOG + Phase 6.17 closure | 0.5d |

**Total: ~6.5 days solo.** Single phase, atomic merge to main when closed.

After Phase 6.17 closes:
- v1.8.1 (or v1.9.0 if breaking) cross-publish + admin panel upload
- Carry-forward to Phase 6.18 surfaces in Wave H closure commit

### D9 — Test coverage

| Layer | Before | After | Δ |
|---|---|---|---|
| Backend | 233 | 233 | +0 (no API changes) |
| Tests.Unit | 18 | ~24 | +6 (`OperationResult` record factory tests + `IsVirtualFilesystem` helper test if extracted there) |
| Tests.Module | 175 | ~190 | +15 (6 modules × 2-3 tests each: RunOperationAsync Success / Skipped / Failed paths) |
| Tests.UI.Avalonia | 1643 | ~1660 | +17 (`PrivilegedActionGuard` tests, `PrivilegeHelperRequiredDialog` view tests, System Health virtual-fs filter test, sidebar regression check) |
| Mobile | 32 | 32 | +0 |
| Admin-panel | 89 | 89 | +0 |
| **Total** | **1836** | **~1874** | **+38** |

VM smoke (Wave G + H) is integration-level; documented in commit message + matrix file.

### D10 — Risks

- **CA1416 cascade larger than budgeted (Wave B/C/D)** — the 7 deferred modules' cumulative cascade was 62 errors in 6.16.F; per-module commits should each contribute <20. If a module triggers >20, STOP for that one and report — defer to 6.18 with a clear note. Mitigation: 6.16.F's pattern catalog (D5) covers every cascade shape we've seen.
- **`Math.Clamp` may mask future bugs** — if total/free calculation regresses, it'll silently report 100% instead of crashing. Mitigation: keep the structured logging on the `catch` arm (capture exception type + drive name) so prod telemetry surfaces drift.
- **`HelperAvailabilityService` startup probe may slow app launch** — D-Bus connection check adds time. Mitigation: probe asynchronously in background, banner appears as soon as the probe completes (typical D-Bus connect: 50-200ms).
- **`PrivilegeHelperRequiredDialog` may show on Windows by accident** — the guard's first check is `if (OperatingSystem.IsWindows()) return true;` — Windows never reaches the dialog. Test path includes Windows-OS execution to verify.
- **6 modules' `RunOperationAsync` may break existing UI bindings** — we're keeping the legacy `OptimizeAsync` method too, so old code paths unaffected. New paths bind to `RunOperationAsync`. Only the modules that opt in get the new shape.
- **`PrivilegeHelperMissingBanner` already-broken on Linux** — Wave G's smoke is the verification. If the banner is genuinely broken, Wave G fix budget is 0.5d which should accommodate a wiring repair. If it's a deeper issue (D-Bus presence check architecture), defer the auto-probe to 6.18 and just verify the existing manual-banner path lights up correctly when a privileged op fails.

### D11 — Carry-forward → Phase 6.18+

- **`install-privhelper.sh` deployment + real privileged-ops smoke test** on Linux VM (RAM optimizer drops caches measurably, package cleaner removes orphans actually, journal cleaner vacuums logs)
- **Migrate the other 40+ modules** to `IOperationModule.RunOperationAsync` incrementally (no breaking change pressure)
- **D-Bus presence subscribe** so `PrivilegeHelperMissingBanner` and `UnavailableModuleView` auto-refresh when helper is installed/removed without app restart
- **macOS implementation** of the privilege-helper analog (XPC service + signed entitlements + install pkg) — gated on Mac hardware
- **CI gate** automating macOS pre-release checklist build-hygiene + sidebar-correctness sections
- **App-level `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`** — broader cleanup beyond CA1416 (nullability, etc.)
- **System Health stale-data warning** — distinguish "drive momentarily not ready" from "drive doesn't exist", show last-known-good values with a stale indicator

## Architecture per sub-wave

### 6.17.A — System Health virtual-fs filter

**Files:**
- Modify: `src/Modules/AuraCore.Module.SystemHealth/SystemHealthModule.cs` — add `IsVirtualFilesystem` private static helper, filter virtual fs in drives loop, zero-capacity guard, Math.Clamp on percent
- Test: `tests/AuraCore.Tests.Module/SystemHealthModuleVirtualFsTests.cs` — new file with 3 tests:
  - `Drives_ExcludesVirtualFilesystems` — assert tmpfs/proc/sysfs are excluded from result
  - `UsedPercent_ZeroCapacity_DoesNotUnderflow` — feed a synthetic drive with TotalSize=0 (via internal accessor or factory injection), assert it's filtered out (not in drive list at all per the new guard)
  - `UsedPercent_BoundedByClamp` — synthetic drive with ridiculous values returns 0 ≤ pct ≤ 100

### 6.17.B — Windows-only modules CA1416 (7 modules, 7 commits)

**Order of attack** (smallest cascade first to build momentum):
1. DriverUpdater (~5 errors) — view + module class
2. AppInstaller (~6 errors) — view + module class + Dashboard quick-action call
3. GamingMode (~7 errors) — view + module class + Dashboard tile binding
4. DefenderManager (~8 errors) — view + module class + DI extension
5. StorageCompression (~9 errors) — view (placeholder GenericModuleView) + module class + DashboardView reference
6. FirewallRules (~10 errors) — view + module class + DI extension + Settings page reference
7. BloatwareRemoval (~17 errors) — view + module class + Dashboard quick-action call site + DashboardView's WindowsBloatService reference

For each module: read the file, add `using System.Runtime.Versioning;` if missing, add `[SupportedOSPlatform("windows")]` at class declaration, build, fix cascade per D5 patterns, commit.

### 6.17.C — Linux-only modules CA1416

10 module classes get `[SupportedOSPlatform("linux")]`:
- `SystemdManagerModule`, `SwapOptimizerModule`, `PackageCleanerModule`, `JournalCleanerModule`, `SnapFlatpakCleanerModule`, `KernelCleanerModule`, `LinuxAppInstallerModule`, `CronManagerModule`, `GrubManagerModule`
- `DockerCleanerModule` is `Linux | MacOS` flag combo — annotate with both: `[SupportedOSPlatform("linux"), SupportedOSPlatform("macos")]`

Plus their View pages and DI extensions per the D5 catalog.

### 6.17.D — macOS-only modules CA1416

9 module classes get `[SupportedOSPlatform("macos")]`:
- `DefaultsOptimizerModule`, `LaunchAgentManagerModule`, `BrewManagerModule`, `TimeMachineManagerModule`, `XcodeCleanerModule`, `DnsFlusherModule`, `MacAppInstallerModule`, `PurgeableSpaceManagerModule`, `SpotlightManagerModule`

Plus their View pages and DI extensions per the D5 catalog.

### 6.17.E — Foundation: types, services, dialog

**Files:**
- Create `src/Core/AuraCore.Application/Models/OperationResult.cs` — record + enum + factories
- Create `src/Core/AuraCore.Application/Interfaces/Modules/IOperationModule.cs` — extension interface inheriting `IOptimizationModule`
- Create `src/UI/AuraCore.UI.Avalonia/Services/IPrivilegedActionGuard.cs` — interface
- Create `src/UI/AuraCore.UI.Avalonia/Services/PrivilegedActionGuard.cs` — implementation
- Create `src/UI/AuraCore.UI.Avalonia/Views/Dialogs/PrivilegeHelperRequiredDialog.axaml` + `.axaml.cs`
- Modify `src/UI/AuraCore.UI.Avalonia/LocalizationService.cs` — add 5 dialog loc keys (EN+TR)
- Modify `src/UI/AuraCore.UI.Avalonia/App.axaml.cs` — register `IPrivilegedActionGuard` in DI
- Tests: `tests/AuraCore.Tests.Unit/OperationResultTests.cs` (factory methods), `tests/AuraCore.Tests.UI.Avalonia/Services/PrivilegedActionGuardTests.cs` (Windows short-circuit + helper-present + helper-missing branches), `tests/AuraCore.Tests.UI.Avalonia/Views/Dialogs/PrivilegeHelperRequiredDialogTests.cs` (renders title/reason/remediation correctly)

### 6.17.F — 6 modules adopt `IOperationModule`

For each of RamOptimizer / JunkCleaner / SystemdManager / SwapOptimizer / PackageCleaner / JournalCleaner:

1. Modify the module class — implement `IOperationModule.RunOperationAsync`. The default implementation calls the existing `OptimizeAsync` and converts the result, OR inlines the new logic with explicit guard + result construction. New logic preferred — gives us the granular Skipped/Failed paths.
2. Modify the corresponding View — wire the post-action banner. The button click handler calls `_module.RunOperationAsync(plan, _guard, ...)`, awaits result, updates banner.
3. Add 2-3 module tests verifying each path: Success returns `OperationStatus.Success` with non-zero `BytesFreed`/`ItemsAffected`; Skipped path (helper missing or pre-flight fails) returns `OperationStatus.Skipped` with reason; Failed path (mock the underlying shell to throw) returns `OperationStatus.Failed`.

### 6.17.G — `PrivilegeHelperMissingBanner` Linux smoke

**Workflow** (executor follows):
1. SSH to Linux VM (`192.168.162.129`), launch the freshly-built app, screenshot MainWindow top edge.
2. Inspect: does the banner render? If yes, document copy + verify install command is shown + verify Copy button present.
3. If banner doesn't render, root-cause: read `HelperAvailabilityService.IsBannerVisible` evaluation, trace startup flow.
4. Apply minimal fix to make banner visible at startup on Linux when helper isn't running.
5. Localization audit: TR translation present?
6. Commit verification doc + any fix commits.

### 6.17.H — Phase close

- Run full Release build solution-wide: `dotnet build AuraCorePro.sln -c Release` — expect 0 errors, 0 CA1416 warnings
- Run all test suites — expect ~1874 passing, 0 failures
- Update Linux VM smoke matrix (`docs/superpowers/phase-6-16-vm-verify-matrix.md`) with new column "6.17 verification" — re-tick rows for 6 modules that adopted `IOperationModule` to confirm post-action banner shows correctly
- CHANGELOG entry under v1.8.1 (or v1.9.0)
- Merge to main, ceremonial commit, force-push v1.8.1 tag (since v1.8.1 was never published yet, force-push is safe)

## Testing summary

See D9 above. ~1874 total after Phase 6.17 closure.

## Continuity

- Standing user prefs preserved: subagent-driven, supervisor mode (controller verifies each subagent), skip spec-review user-gate, critical-security auto-deploy, AFK + autonomous decision-locking on ambiguous design points.
- v1.8.0 already shipped (admin panel upload by user 2026-04-29) — Phase 6.17 ships as v1.8.1 / v1.9.0 successor.
- Phase 6.18 carry-forward documented in D11.

## Self-review notes

- **Placeholders**: documentation URL `https://docs.auracore.pro/linux/privilege-helper` is a placeholder — Phase 6.18 ships actual docs. Acceptable for 6.17 ship because the dialog still shows the install command + has a Copy button — link is supplementary. Otherwise no placeholders.
- **Internal consistency**: D1 OperationResult record matches D6 module adoption signature. D2 IPrivilegedActionGuard's TryGuardAsync return matches the if-branch flow described in 6.17.F (`if (!await guard.TryGuardAsync(...)) return OperationResult.Skipped(...)`). D3 dialog 5 loc keys match the D5 + D6 banner additions (3 result keys), totaling 8 new keys (within Phase 6.16's 9-key precedent for the UnavailableModuleView). D4 zero-guard order matches D8 wave A scope.
- **Scope check**: 8 sub-waves, ~22 tasks (counting per-module commits in B/C/D as separate tasks). Comparable to Phase 6.16 (8 waves, 28 tasks). Single phase, no further decomposition needed.
- **Ambiguity check**: "modules that adopt OperationResult" is exactly 6 specific modules listed in D6 — no ambiguity. "Per-module CA1416 STOP rule" is explicit (>20 new errors → STOP) per D5. Banner-fix scope in Wave G is bounded to "make it visible at startup on Linux when helper missing"; deeper auto-refresh is explicit non-goal.
- **Defaults documented**: every D-decision records the choice + the rationale. The guard's Windows short-circuit (return true on Windows) is explicit per D2 + D10 risks.
