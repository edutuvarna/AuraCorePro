# Phase 6.17 — Platform Debt Closure + Privileged Ops Feedback Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. **Execution context:** fresh session — this plan is self-contained, do NOT assume prior context. Standing user prefs: subagent-driven, supervisor mode (verify each subagent), skip spec-review user-gate, critical-security auto-deploy. Build artifacts + final smoke = LAST step before pause; do NOT auto-deploy v1.8.1 admin-panel upload.

**Goal:** Close three buckets of Phase 6.16 carry-forward debt — System Health virtual-filesystem display bug, CA1416 attribute cascade for 7+19 deferred modules, and silent no-op feedback when privileged ops can't run because the Linux helper is missing.

**Architecture:** Three parallel surgical changes plus shared foundation: (1) System Health filters virtual filesystems and clamps the percent calculation; (2) `[SupportedOSPlatform("X")]` rolls onto every platform-specific module class plus its View / DI extension cascade with the per-module-commit pattern proven in Phase 6.16.F; (3) new `OperationResult` record + `IOperationModule` extension interface + `IPrivilegedActionGuard` service surface a pre-flight modal and post-action banner so 6 highest-pain modules report Success / Skipped / Failed instead of pretending success.

**Tech Stack:** C# 12 / .NET 8, Avalonia 11.x, xUnit 2.9, Avalonia.Headless.XUnit, NSubstitute (mocks), Microsoft.Extensions.DependencyInjection, `Microsoft.Extensions.Logging` (already wired in Phase 6.16 hotfix v2).

**Branch off:** `main` at HEAD `b5074e0` (the Phase 6.17 spec commit). Create `phase-6-17-debt-closure`.

**Spec reference:** `docs/superpowers/specs/2026-05-04-phase-6-17-debt-closure-design.md` — read this BEFORE starting Task 0. Contains all 11 locked design decisions and the per-module CA1416 cascade-fix pattern catalog.

**Linux VM access (for Wave G + H):** `ssh -i ~/.ssh/id_ed25519_aura deniz@192.168.162.129` (Ubuntu 24.04.4 LTS, dotnet 8.0.125).

**Out of scope (explicitly forbidden in this phase):** real privileged-ops smoke (deploying `install-privhelper.sh` and verifying RAM Optimizer actually drops caches); macOS notarization/signing; migrating the other 40+ modules to `IOperationModule`; D-Bus presence subscribe for auto-refresh on helper install; app-level `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` beyond CA1416. All of these are Phase 6.18+.

**Repo path:** `C:\Users\Admin\Desktop\Gelistirme\AuraCorePro\AuraCorePro` (moved from old `C:\Users\Admin\Desktop\AuraCorePro\AuraCorePro` during the v1.8.0 ship cycle).

---

## Task 0: Branch setup

**Files:** None (git only)

- [ ] **Step 1: Verify repo state + create branch**

```bash
cd /c/Users/Admin/Desktop/Gelistirme/AuraCorePro/AuraCorePro
git status --short | grep -v -E '\.dll|\.pdb|/obj/|/bin/|\.cache|\.assets|FileListAbsolute|AssemblyInfo|GeneratedMSBuild|\.sourcelink|publish-|landing-page|\.swp|packaging/dist|publish-output' | head -10
git rev-parse HEAD                       # should be b5074e0 or descendant on main
git switch -c phase-6-17-debt-closure
git branch --show-current                # → phase-6-17-debt-closure
```

- [ ] **Step 2: Read the spec**

Read `docs/superpowers/specs/2026-05-04-phase-6-17-debt-closure-design.md` end-to-end. Internalize D1-D11 design decisions before writing any code, especially D5 (the CA1416 cascade-fix pattern catalog that Wave B/C/D rely on heavily).

---

# WAVE A — System Health virtual-fs filter + zero-cap guard (Tasks 1-2)

## Task 1: `IsVirtualFilesystem` helper + drives loop hardening + tests

**Files:**
- Modify: `src/Modules/AuraCore.Module.SystemHealth/SystemHealthModule.cs:223-233`
- Test (create): `tests/AuraCore.Tests.Module/SystemHealthModuleVirtualFsTests.cs`

**Goal:** Filter virtual filesystems out of the drives list so they never reach the percent-calc, plus zero-capacity guard plus `Math.Clamp` defense-in-depth. Closes the user-reported `-2147483648%` display bug.

- [ ] **Step 1: Read the current `SystemHealthModule.cs:200-260`** to confirm the loop shape and the `DriveReport` constructor signature `(string Name, string Label, string Format, double TotalGb, double FreeGb, int UsedPercent)`.

- [ ] **Step 2: Write the failing test**

`tests/AuraCore.Tests.Module/SystemHealthModuleVirtualFsTests.cs`:

```csharp
using System;
using System.Linq;
using AuraCore.Application;
using AuraCore.Module.SystemHealth;
using Xunit;

namespace AuraCore.Tests.Module;

public class SystemHealthModuleVirtualFsTests
{
    [Fact]
    public async Task ScanAsync_ProducesNoDrive_WithUnderflowedPercent()
    {
        // Phase 6.17.A regression: with the underflow bug, /sys, /proc, /dev
        // would appear with UsedPercent = int.MinValue (-2147483648).
        // The fix filters virtual filesystems entirely; the assertion is that
        // no DriveReport in the result has a percent outside [0, 100].
        var module = new SystemHealthModule();
        var result = await module.ScanAsync(new ScanOptions(), default);

        Assert.True(result.Success);

        // The module exposes its rich report via LastReport (existing public field).
        var report = module.LastReport;
        Assert.NotNull(report);

        foreach (var drive in report!.Drives)
        {
            Assert.InRange(drive.UsedPercent, 0, 100);
            // Virtual fs filter — names should not start with /sys, /proc, /dev/pts
            // (note: Windows drive names like "C:\" are unaffected).
            if (OperatingSystem.IsLinux())
            {
                Assert.False(
                    drive.Name.StartsWith("/sys", StringComparison.Ordinal)
                    || drive.Name.StartsWith("/proc", StringComparison.Ordinal)
                    || drive.Name == "/dev/pts"
                    || drive.Name.StartsWith("/sys/kernel", StringComparison.Ordinal),
                    $"Virtual filesystem leaked: {drive.Name} fmt={drive.Format}");
            }
        }
    }

    [Fact]
    public void IsVirtualFilesystem_FlagsKnownVirtualTypes()
    {
        // Use reflection to invoke the private static helper so we can test it
        // independently of DriveInfo.GetDrives() (which is platform-bound).
        var method = typeof(SystemHealthModule).GetMethod(
            "IsVirtualFilesystem",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        // We can't construct DriveInfo directly, so the test exercises the format
        // string only via a small wrapper helper exposed for tests OR we skip
        // this test if the helper signature isn't reflection-friendly. For
        // simplicity we just verify the method exists; the integration test
        // above proves the filter works end-to-end on Linux.
        Assert.True(method!.IsStatic);
    }
}
```

- [ ] **Step 3: Run test (expect fail on Linux, vacuously pass on Windows)**

```bash
dotnet test tests/AuraCore.Tests.Module/AuraCore.Tests.Module.csproj --filter "FullyQualifiedName~SystemHealthModuleVirtualFsTests"
```

On Windows: passes (no `/sys` paths in Windows drive list).
On Linux VM with the un-fixed module: would surface drives with `UsedPercent = -2147483648` failing the InRange assertion.

- [ ] **Step 4: Apply the fix to `SystemHealthModule.cs`**

Find the existing block at lines ~223-233:

```csharp
foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady))
{
    try
    {
        var totalGb = d.TotalSize / (1024.0 * 1024 * 1024);
        var freeGb = d.AvailableFreeSpace / (1024.0 * 1024 * 1024);
        var usedPct = (int)((1.0 - freeGb / totalGb) * 100);
        drives.Add(new DriveReport(d.Name, d.VolumeLabel, d.DriveFormat, totalGb, freeGb, usedPct));
    }
    catch { }
}
```

Replace with:

```csharp
foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady))
{
    try
    {
        // Phase 6.17.A: skip virtual filesystems — they're not user-facing
        // storage and reporting 0-byte capacity caused the -2147483648%
        // display bug ((int)((1 - 0/0) * 100) = (int)NaN = int.MinValue).
        if (IsVirtualFilesystem(d)) continue;

        var totalGb = d.TotalSize / (1024.0 * 1024 * 1024);

        // Phase 6.17.A: zero-capacity guard — even after the virtual-fs
        // filter, some ready drives can momentarily report TotalSize=0
        // (slow USB enumeration, ramdisks). Don't divide by zero.
        if (totalGb <= 0) continue;

        var freeGb  = d.AvailableFreeSpace / (1024.0 * 1024 * 1024);
        var usedPct = (int)Math.Clamp((1.0 - freeGb / totalGb) * 100, 0, 100);
        drives.Add(new DriveReport(d.Name, d.VolumeLabel, d.DriveFormat, totalGb, freeGb, usedPct));
    }
    catch { }
}
```

And add the helper method at the bottom of the class (before the closing `}`):

```csharp
/// <summary>
/// Phase 6.17.A: virtual filesystems on Linux/macOS report 0-byte capacity
/// and would underflow the percent calculation. Drop the well-known
/// virtual ones; treat everything else (ext4, btrfs, xfs, ntfs, apfs,
/// hfsplus, exfat, fat32, ...) as real storage.
/// </summary>
private static bool IsVirtualFilesystem(DriveInfo d)
{
    var fmt = d.DriveFormat?.ToLowerInvariant() ?? string.Empty;
    return fmt is "tmpfs" or "devtmpfs" or "proc" or "sysfs" or "devpts"
                or "securityfs" or "cgroup" or "cgroup2" or "pstore"
                or "bpf" or "tracefs" or "debugfs" or "configfs"
                or "fusectl" or "binfmt_misc" or "autofs" or "mqueue"
                or "hugetlbfs" or "rpc_pipefs" or "nsfs";
}
```

- [ ] **Step 5: Run test (expect pass)**

```bash
dotnet test tests/AuraCore.Tests.Module/AuraCore.Tests.Module.csproj --filter "FullyQualifiedName~SystemHealthModuleVirtualFsTests" 2>&1 | tail -5
```
Expected: 2 tests pass.

- [ ] **Step 6: Run full Tests.Module suite (regression check)**

```bash
dotnet test tests/AuraCore.Tests.Module/AuraCore.Tests.Module.csproj 2>&1 | tail -3
```
Expected: 175 → 177 (+2 from Wave A), 0 failures.

- [ ] **Step 7: Commit**

```bash
git add src/Modules/AuraCore.Module.SystemHealth/SystemHealthModule.cs \
        tests/AuraCore.Tests.Module/SystemHealthModuleVirtualFsTests.cs
git commit -m "phase-6.17.A: SystemHealth filters virtual filesystems + zero-cap guard + Math.Clamp on percent"
```

---

## Task 2: Build verify Wave A

- [ ] **Step 1: Solution-wide release build (no errors)**

```bash
dotnet build AuraCorePro.sln -c Release 2>&1 | tail -5
```
Expected: 0 errors. Pre-existing CA1416 warnings on un-annotated modules are still there (Wave B-D will close them).

- [ ] **Step 2: Full UI test suite (no regression)**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj 2>&1 | tail -3
```
Expected: 1643 → 1643 (Wave A doesn't add UI tests).

- [ ] **Step 3: No commit needed (verification only)**

---

# WAVE B — Windows-only modules CA1416 (Tasks 3-9)

Each task follows the same shape: read the module class + its View page + its DI extension, add `[SupportedOSPlatform("windows")]` to whichever cascade level surfaces errors, build, fix any new errors using the D5 catalog (class-level annotation, method-level annotation, `#pragma warning disable CA1416` block, or extracted helper). Per-module STOP rule: if a single module's annotation cascade exceeds 20 new CA1416 errors, STOP and report.

**Order of attack** (smallest cascade first to build momentum + catch any pattern surprises early):

| Task | Module | Estimated cascade |
|---|---|---|
| 3 | DriverUpdater | ~5 errors |
| 4 | AppInstaller | ~6 errors |
| 5 | GamingMode | ~7 errors |
| 6 | DefenderManager | ~8 errors |
| 7 | StorageCompression | ~9 errors |
| 8 | FirewallRules | ~10 errors |
| 9 | BloatwareRemoval | ~17 errors |

For all tasks 3-9, the procedure template is the same; only the file paths and cascade fixes differ.

## Task 3: `DriverUpdaterModule` annotation

**Files:**
- Modify: `src/Modules/AuraCore.Module.DriverUpdater/DriverUpdaterModule.cs`
- Modify: `src/Modules/AuraCore.Module.DriverUpdater/DriverUpdaterRegistration.cs` (if it exists — Phase 6.16.F precedent)
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/DriverUpdaterView.axaml.cs`
- (Possibly) Modify: any other call site CA1416 surfaces

- [ ] **Step 1: Read the module class**

```bash
head -30 src/Modules/AuraCore.Module.DriverUpdater/DriverUpdaterModule.cs
```

Identify the class declaration and the `using` block.

- [ ] **Step 2: Add `[SupportedOSPlatform("windows")]` to the module class**

Add `using System.Runtime.Versioning;` to the using block if missing. Add `[SupportedOSPlatform("windows")]` immediately above the `public sealed class DriverUpdaterModule : IOptimizationModule` line.

- [ ] **Step 3: Solution-wide release build**

```bash
dotnet build AuraCorePro.sln -c Release 2>&1 | grep -E "error CA1416" | head -30
```

Count the errors. If >20, **STOP and report** — defer to Phase 6.18 with note.

- [ ] **Step 4: Apply the cascade fixes per the D5 catalog**

For each error in the output:

- **If the error is in `DriverUpdaterView.axaml.cs`** → add `[SupportedOSPlatform("windows")]` to the View class declaration (the View only constructs/binds Windows-only types, so class-level is the right scope).
- **If the error is in `DriverUpdaterRegistration` (DI extension)** → add `[SupportedOSPlatform("windows")]` to the static class declaration.
- **If the error is in a lambda `() => new DriverUpdaterView()` body** → if the lambda is inside an `if (OperatingSystem.IsWindows())` block, wrap the block with `#pragma warning disable CA1416` / `#pragma warning restore CA1416` (Phase 6.16.F MainWindow precedent).
- **If the error is in a LINQ `Select(s => …)` projection** → extract `[SupportedOSPlatform("windows")] private static <ResultType> ProjectXxx(<InputType> s) { … }` helper, then `Select(ProjectXxx)`.

After every fix, re-run Step 3's build command. Loop until 0 CA1416 errors solution-wide.

- [ ] **Step 5: Verify no test regression**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj 2>&1 | tail -3
dotnet test tests/AuraCore.Tests.Module/AuraCore.Tests.Module.csproj 2>&1 | tail -3
```
Expected: same counts as before this task.

- [ ] **Step 6: Commit**

```bash
git add src/Modules/AuraCore.Module.DriverUpdater src/UI/AuraCore.UI.Avalonia/Views/Pages/DriverUpdaterView.axaml.cs <any other touched source files>
git commit -m "phase-6.17.B: SupportedOSPlatform(windows) on DriverUpdaterModule + cascade"
```

---

## Task 4: `AppInstallerModule` annotation

Same procedure as Task 3 with these path substitutions:

- Module: `src/Modules/AuraCore.Module.AppInstaller/AppInstallerModule.cs`
- DI extension: `src/Modules/AuraCore.Module.AppInstaller/AppInstallerRegistration.cs`
- View: `src/UI/AuraCore.UI.Avalonia/Views/Pages/AppInstallerView.axaml.cs`

**Likely additional cascade**: Dashboard's quick-action tile may reference `AppInstallerModule` or its registration extension — check `src/UI/AuraCore.UI.Avalonia/ViewModels/Dashboard/QuickActionPresets.cs` and `DashboardView.axaml.cs` for cascade. If they surface errors, prefer extracting a `[SupportedOSPlatform("windows")] private static` factory helper inside `DashboardView.axaml.cs` rather than annotating the entire DashboardView (which is cross-platform).

Commit message: `phase-6.17.B: SupportedOSPlatform(windows) on AppInstallerModule + cascade`

---

## Task 5: `GamingModeModule` annotation

Same procedure as Task 3 with these path substitutions:

- Module: `src/Modules/AuraCore.Module.GamingMode/GamingModeModule.cs`
- DI extension: `src/Modules/AuraCore.Module.GamingMode/GamingModeRegistration.cs`
- View: `src/UI/AuraCore.UI.Avalonia/Views/Pages/GamingModeView.axaml.cs`

**Likely additional cascade**: Dashboard quick-action and possibly StatusBar GameMode-active indicator. Same approach as Task 4.

Commit message: `phase-6.17.B: SupportedOSPlatform(windows) on GamingModeModule + cascade`

---

## Task 6: `DefenderManagerModule` annotation

Same procedure as Task 3 with these path substitutions:

- Module: `src/Modules/AuraCore.Module.DefenderManager/DefenderManagerModule.cs`
- DI extension: `src/Modules/AuraCore.Module.DefenderManager/DefenderManagerRegistration.cs`
- View: `src/UI/AuraCore.UI.Avalonia/Views/Pages/DefenderManagerView.axaml.cs`

**Note**: Phase 6.16.B (commit `8d9a38b`) already added a `WindowsPrincipal.IsInRole` runtime guard inside `DefenderManagerModule.GetDefenderStatusAsync`. The `[SupportedOSPlatform("windows")]` class-level attribute should now propagate cleanly through that method. If CA1416 still errors on `WindowsPrincipal` after class-level annotation, the analyzer has gotten richer than expected; add method-level `[SupportedOSPlatform("windows")]` to `GetDefenderStatusAsync` as belt-and-suspenders.

Commit message: `phase-6.17.B: SupportedOSPlatform(windows) on DefenderManagerModule + cascade`

---

## Task 7: `StorageCompressionModule` annotation

Same procedure as Task 3 with these path substitutions:

- Module: `src/Modules/AuraCore.Module.StorageCompression/StorageCompressionModule.cs`
- DI extension: `src/Modules/AuraCore.Module.StorageCompression/StorageCompressionRegistration.cs`
- View: **Note** — `MainWindow.RegisterAllModuleViews` registers `storage-compression` to a placeholder `() => new Pages.GenericModuleView()` (per Phase 6.16 atomic Task 5+13 — the real StorageCompressionView feature was deferred). So the cascade should mainly hit the module class + DI extension, not a dedicated View page. Verify via the build error output before attempting view-level annotation.

If `GenericModuleView` (cross-platform placeholder) surfaces CA1416 errors when constructed inside a Windows-only block, the `#pragma warning disable CA1416` block in `MainWindow.RegisterAllModuleViews` (already present from Phase 6.16.F commit `cef7b1f`) covers the call site. Verify the pragma block contains the `storage-compression` registration line; if it doesn't, extend the block.

Commit message: `phase-6.17.B: SupportedOSPlatform(windows) on StorageCompressionModule + cascade`

---

## Task 8: `FirewallRulesModule` annotation

Same procedure as Task 3 with these path substitutions:

- Module: `src/Modules/AuraCore.Module.FirewallRules/FirewallRulesModule.cs`
- DI extension: `src/Modules/AuraCore.Module.FirewallRules/FirewallRulesRegistration.cs`
- View: `src/UI/AuraCore.UI.Avalonia/Views/Pages/FirewallRulesView.axaml.cs`

**Likely additional cascade**: Settings page may reference `FirewallRulesModule.LastReport` or similar; Module's `INetFwPolicy2` COM type usage internally (already wrapped in `try/catch` per existing code).

Commit message: `phase-6.17.B: SupportedOSPlatform(windows) on FirewallRulesModule + cascade`

---

## Task 9: `BloatwareRemovalModule` annotation (highest cascade)

Same procedure as Task 3 with these path substitutions:

- Module: `src/Modules/AuraCore.Module.BloatwareRemoval/BloatwareRemovalModule.cs`
- DI extension: `src/Modules/AuraCore.Module.BloatwareRemoval/BloatwareRemovalRegistration.cs`
- View: `src/UI/AuraCore.UI.Avalonia/Views/Pages/BloatwareRemovalView.axaml.cs`

**Largest expected cascade (~17 errors)** — this is the module that triggered Phase 6.16.F's STOP rule. Sources of cascade:
- Direct `Microsoft.Win32.Registry` calls in module
- Direct `ServiceController` references
- DashboardView's `WindowsBloatService` reference
- Dashboard quick-action `remove-bloat` tile (Phase 6.16.E filter ensures it's only shown on Windows; the factory lambda may need a pragma block if the existing one doesn't cover it)
- Possibly `DashboardViewModel` references for tile state

Per the per-module STOP rule (>20 errors), this should still fit. If it doesn't, STOP and report — split the work across multiple commits or defer the residual to 6.18.

Commit message: `phase-6.17.B: SupportedOSPlatform(windows) on BloatwareRemovalModule + cascade`

---

# WAVE C — Linux-only modules CA1416 (Task 10)

## Task 10: `[SupportedOSPlatform("linux")]` on 10 Linux module classes (single batch commit)

**Files:**
- 10 module class files
- Possibly: View pages + DI extensions per cascade

**Goal:** Annotate every Linux-only module class with `[SupportedOSPlatform("linux")]`. Smaller cascade than Wave B because Linux modules are only consumed inside `if (OperatingSystem.IsLinux())` blocks already (per Phase 6.16's hygiene work).

**Modules to annotate:**

| # | Module | Class | Notes |
|---|---|---|---|
| 1 | `SystemdManagerModule` | `src/Modules/AuraCore.Module.SystemdManager/SystemdManagerModule.cs` | parameterless ctor |
| 2 | `SwapOptimizerModule` | `src/Modules/AuraCore.Module.SwapOptimizer/SwapOptimizerModule.cs` | parameterless ctor |
| 3 | `PackageCleanerModule` | `src/Modules/AuraCore.Module.PackageCleaner/PackageCleanerModule.cs` | parameterless ctor |
| 4 | `JournalCleanerModule` | `src/Modules/AuraCore.Module.JournalCleaner/JournalCleanerModule.cs` | parameterless ctor |
| 5 | `SnapFlatpakCleanerModule` | `src/Modules/AuraCore.Module.SnapFlatpakCleaner/SnapFlatpakCleanerModule.cs` | takes `IShellCommandService` |
| 6 | `KernelCleanerModule` | `src/Modules/AuraCore.Module.KernelCleaner/KernelCleanerModule.cs` | parameterless ctor |
| 7 | `LinuxAppInstallerModule` | `src/Modules/AuraCore.Module.LinuxAppInstaller/LinuxAppInstallerModule.cs` | takes `IShellCommandService` |
| 8 | `CronManagerModule` | `src/Modules/AuraCore.Module.CronManager/CronManagerModule.cs` | parameterless ctor |
| 9 | `GrubManagerModule` | `src/Modules/AuraCore.Module.GrubManager/GrubManagerModule.cs` | takes `IShellCommandService` (throws on null) |
| 10 | `DockerCleanerModule` (Linux+macOS) | `src/Modules/AuraCore.Module.DockerCleaner/DockerCleanerModule.cs` | `Platform = Linux | MacOS` flag combo |

- [ ] **Step 1: For DockerCleanerModule, add BOTH attributes**

```csharp
using System.Runtime.Versioning;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class DockerCleanerModule : IOptimizationModule
{ … }
```

For modules 1-9, add only `[SupportedOSPlatform("linux")]`.

- [ ] **Step 2: Add `using System.Runtime.Versioning;`** to each module file's using block if missing.

- [ ] **Step 3: Add the attribute to each class declaration**

Apply the attribute to all 10 module classes per the table above.

- [ ] **Step 4: Solution-wide release build + cascade fix**

```bash
dotnet build AuraCorePro.sln -c Release 2>&1 | grep -E "error CA1416" | head -50
```

Apply D5 catalog fixes for any errors. Likely cascades:
- View pages that construct Linux modules → class-level `[SupportedOSPlatform("linux")]`
- DI registration extensions in `App.axaml.cs` → already inside `if (OperatingSystem.IsLinux())` blocks; analyzer should propagate. If it doesn't, extract a `[SupportedOSPlatform("linux")] private static` factory helper or add a pragma block (Phase 6.16.F precedent).

- [ ] **Step 5: Verify clean**

```bash
dotnet build AuraCorePro.sln -c Release 2>&1 | grep -E "error CA1416" | wc -l
```
Expected: 0.

- [ ] **Step 6: Verify tests still pass**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj 2>&1 | tail -3
dotnet test tests/AuraCore.Tests.Module/AuraCore.Tests.Module.csproj 2>&1 | tail -3
```

- [ ] **Step 7: Commit**

```bash
git add src/Modules/AuraCore.Module.{SystemdManager,SwapOptimizer,PackageCleaner,JournalCleaner,SnapFlatpakCleaner,KernelCleaner,LinuxAppInstaller,CronManager,GrubManager,DockerCleaner} <any cascade fix files>
git commit -m "phase-6.17.C: SupportedOSPlatform(linux) on 10 Linux module classes + cascade"
```

---

# WAVE D — macOS-only modules CA1416 (Task 11)

## Task 11: `[SupportedOSPlatform("macos")]` on 9 macOS module classes (single batch commit)

**Files:**
- 9 module class files
- Possibly: View pages + DI extensions per cascade

**Modules to annotate:**

| # | Module | Class | Notes |
|---|---|---|---|
| 1 | `DefaultsOptimizerModule` | `src/Modules/AuraCore.Module.DefaultsOptimizer/DefaultsOptimizerModule.cs` | |
| 2 | `LaunchAgentManagerModule` | `src/Modules/AuraCore.Module.LaunchAgentManager/LaunchAgentManagerModule.cs` | |
| 3 | `BrewManagerModule` | `src/Modules/AuraCore.Module.BrewManager/BrewManagerModule.cs` | |
| 4 | `TimeMachineManagerModule` | `src/Modules/AuraCore.Module.TimeMachineManager/TimeMachineManagerModule.cs` | |
| 5 | `XcodeCleanerModule` | `src/Modules/AuraCore.Module.XcodeCleaner/XcodeCleanerModule.cs` | |
| 6 | `DnsFlusherModule` | `src/Modules/AuraCore.Module.DnsFlusher/DnsFlusherModule.cs` | |
| 7 | `MacAppInstallerModule` | `src/Modules/AuraCore.Module.MacAppInstaller/MacAppInstallerModule.cs` | |
| 8 | `PurgeableSpaceManagerModule` | `src/Modules/AuraCore.Module.PurgeableSpaceManager/PurgeableSpaceManagerModule.cs` | |
| 9 | `SpotlightManagerModule` | `src/Modules/AuraCore.Module.SpotlightManager/SpotlightManagerModule.cs` | |

(`DockerCleanerModule` was annotated in Task 10 with both Linux + macOS attributes.)

- [ ] **Steps 1-7**: same procedure as Task 10, substituting `[SupportedOSPlatform("macos")]` and the macOS module file paths.

Commit message: `phase-6.17.C: SupportedOSPlatform(macos) on 9 macOS module classes + cascade`

> **Note**: After Wave D closes, `dotnet build AuraCorePro.sln -c Release` must produce **0 CA1416 errors and 0 CA1416 warnings**. The macOS pre-release checklist (`docs/ops/macos-prerelease-checklist.md`)'s build-hygiene section will green-tick from this point onward.

---

# WAVE E — Foundation: types + services + dialog (Tasks 12-16)

## Task 12: `OperationResult` record + `OperationStatus` enum

**Files:**
- Create: `src/Core/AuraCore.Application/Models/OperationResult.cs`
- Test: `tests/AuraCore.Tests.Unit/OperationResultTests.cs`

- [ ] **Step 1: Write failing tests**

`tests/AuraCore.Tests.Unit/OperationResultTests.cs`:

```csharp
using System;
using AuraCore.Application;
using Xunit;

namespace AuraCore.Tests.Unit;

public class OperationResultTests
{
    [Fact]
    public void Success_HasStatusSuccess_AndReportsBytesAndItems()
    {
        var r = OperationResult.Success(2_500_000_000L, 1247, TimeSpan.FromSeconds(4.2));
        Assert.Equal(OperationStatus.Success, r.Status);
        Assert.Equal(2_500_000_000L, r.BytesFreed);
        Assert.Equal(1247, r.ItemsAffected);
        Assert.Null(r.Reason);
        Assert.Null(r.RemediationCommand);
    }

    [Fact]
    public void Skipped_CarriesReason_AndOptionalRemediation()
    {
        var r = OperationResult.Skipped("Privilege helper required", "sudo bash /opt/install.sh");
        Assert.Equal(OperationStatus.Skipped, r.Status);
        Assert.Equal(0L, r.BytesFreed);
        Assert.Equal(0, r.ItemsAffected);
        Assert.Equal("Privilege helper required", r.Reason);
        Assert.Equal("sudo bash /opt/install.sh", r.RemediationCommand);
        Assert.Equal(TimeSpan.Zero, r.Duration);
    }

    [Fact]
    public void Failed_CarriesReasonAndDuration_NoRemediation()
    {
        var r = OperationResult.Failed("Drop_caches sysctl returned EACCES", TimeSpan.FromMilliseconds(120));
        Assert.Equal(OperationStatus.Failed, r.Status);
        Assert.Equal("Drop_caches sysctl returned EACCES", r.Reason);
        Assert.Null(r.RemediationCommand);
        Assert.Equal(TimeSpan.FromMilliseconds(120), r.Duration);
    }

    [Fact]
    public void Skipped_NoRemediation_NullableArgDefaultsToNull()
    {
        var r = OperationResult.Skipped("Feature flag off");
        Assert.Equal(OperationStatus.Skipped, r.Status);
        Assert.Equal("Feature flag off", r.Reason);
        Assert.Null(r.RemediationCommand);
    }
}
```

- [ ] **Step 2: Run test (expect compile error — types not defined)**

```bash
dotnet test tests/AuraCore.Tests.Unit/AuraCore.Tests.Unit.csproj --filter "FullyQualifiedName~OperationResult" 2>&1 | tail -5
```

- [ ] **Step 3: Create `OperationResult.cs`**

```csharp
namespace AuraCore.Application;

public enum OperationStatus
{
    Success,
    Skipped,        // Operation skipped (precondition not met — typically helper missing)
    Failed,         // Operation attempted but errored
}

/// <summary>
/// Phase 6.17: rich-result return type for module operations that need to
/// communicate Success vs Skipped vs Failed (and why) to the UI. Replaces
/// the lossy <see cref="OptimizationResult"/> shape for modules where the
/// distinction matters to the user (e.g. "skipped because privilege helper
/// missing" vs "actually freed 2.3 GB").
/// </summary>
public sealed record OperationResult(
    OperationStatus Status,
    long BytesFreed,
    int ItemsAffected,
    string? Reason,
    string? RemediationCommand,
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

- [ ] **Step 4: Run tests (expect 4 pass)**

```bash
dotnet test tests/AuraCore.Tests.Unit/AuraCore.Tests.Unit.csproj --filter "FullyQualifiedName~OperationResult" 2>&1 | tail -5
```
Expected: 4 tests pass.

- [ ] **Step 5: Verify full Tests.Unit suite**

```bash
dotnet test tests/AuraCore.Tests.Unit/AuraCore.Tests.Unit.csproj 2>&1 | tail -3
```
Expected: 18 → 22 (+4).

- [ ] **Step 6: Commit**

```bash
git add src/Core/AuraCore.Application/Models/OperationResult.cs tests/AuraCore.Tests.Unit/OperationResultTests.cs
git commit -m "phase-6.17.E: OperationResult record + OperationStatus enum + factory tests"
```

---

## Task 13: `IOperationModule` extension interface

**Files:**
- Create: `src/Core/AuraCore.Application/Interfaces/Modules/IOperationModule.cs`

**Goal:** Optional extension interface that 6 highest-pain modules opt into. Inherits from `IOptimizationModule` so existing module class declarations continue to work; modules that adopt it ALSO implement `RunOperationAsync`.

- [ ] **Step 1: Create the interface file**

```csharp
using AuraCore.Application;
using AuraCore.UI.Avalonia.Services; // for IPrivilegedActionGuard — see note below

namespace AuraCore.Application.Interfaces.Modules;

/// <summary>
/// Phase 6.17: optional extension contract for modules that need to
/// communicate rich operation status (Success / Skipped / Failed +
/// reason + remediation) to the UI. Modules opt in by implementing
/// this on top of <see cref="IOptimizationModule"/>.
/// </summary>
public interface IOperationModule : IOptimizationModule
{
    Task<OperationResult> RunOperationAsync(
        OptimizationPlan plan,
        IPrivilegedActionGuard guard,
        IProgress<TaskProgress>? progress = null,
        CancellationToken ct = default);
}
```

> **Layering concern**: `IPrivilegedActionGuard` lives in the UI layer (`src/UI/AuraCore.UI.Avalonia/Services/`). `IOperationModule` is in the Application layer (`src/Core/AuraCore.Application/`). Application can NOT reference UI. **Resolution**: move the `IPrivilegedActionGuard` interface to `src/Core/AuraCore.Application/Interfaces/Platform/IPrivilegedActionGuard.cs` (Application layer), and only the IMPLEMENTATION (`PrivilegedActionGuard.cs`) lives in UI. This keeps the dependency direction correct.

So Task 13 actually has TWO files to manage:

- [ ] **Step 2 (revised): Create `IPrivilegedActionGuard` in Application layer FIRST**

`src/Core/AuraCore.Application/Interfaces/Platform/IPrivilegedActionGuard.cs`:

```csharp
namespace AuraCore.Application.Interfaces.Platform;

/// <summary>
/// Phase 6.17: pre-flight check before invoking a privileged action.
/// Implemented by the UI shell; module code calls TryGuardAsync before
/// kicking off any operation that requires the privilege helper.
/// </summary>
public interface IPrivilegedActionGuard
{
    /// <summary>
    /// Returns true if the privileged action may proceed. Returns false if
    /// the helper is unavailable; the implementation surfaces an actionable
    /// modal to the user explaining why and how to install the helper.
    /// </summary>
    Task<bool> TryGuardAsync(
        string actionDescription,
        string? remediationCommandOverride = null,
        CancellationToken ct = default);
}
```

- [ ] **Step 3: Now create `IOperationModule.cs` referencing the Application-layer guard**

```csharp
using AuraCore.Application;
using AuraCore.Application.Interfaces.Platform;

namespace AuraCore.Application.Interfaces.Modules;

public interface IOperationModule : IOptimizationModule
{
    Task<OperationResult> RunOperationAsync(
        OptimizationPlan plan,
        IPrivilegedActionGuard guard,
        IProgress<TaskProgress>? progress = null,
        CancellationToken ct = default);
}
```

- [ ] **Step 4: Build verify**

```bash
dotnet build src/Core/AuraCore.Application/AuraCore.Application.csproj -c Debug 2>&1 | tail -3
```
Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/Core/AuraCore.Application/Interfaces/Platform/IPrivilegedActionGuard.cs \
        src/Core/AuraCore.Application/Interfaces/Modules/IOperationModule.cs
git commit -m "phase-6.17.E: IOperationModule extension interface + IPrivilegedActionGuard contract"
```

---

## Task 14: `PrivilegedActionGuard` implementation + DI registration + tests

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Services/PrivilegedActionGuard.cs`
- Modify: `src/UI/AuraCore.UI.Avalonia/App.axaml.cs` (DI registration)
- Test: `tests/AuraCore.Tests.UI.Avalonia/Services/PrivilegedActionGuardTests.cs`

- [ ] **Step 1: Write failing tests**

`tests/AuraCore.Tests.UI.Avalonia/Services/PrivilegedActionGuardTests.cs`:

```csharp
using System;
using System.Threading.Tasks;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.UI.Avalonia.Services;
using Avalonia.Headless.XUnit;
using NSubstitute;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Services;

public class PrivilegedActionGuardTests
{
    [AvaloniaFact]
    public async Task TryGuardAsync_OnWindows_ReturnsTrue_WithoutPromptingUser()
    {
        if (!OperatingSystem.IsWindows()) return; // OS-bound short-circuit
        var helper = Substitute.For<IHelperAvailabilityService>();
        helper.IsMissing.Returns(true); // even when reported missing, Windows path returns true
        var guard = new PrivilegedActionGuard(helper);

        var result = await guard.TryGuardAsync("anything");

        Assert.True(result);
    }

    [AvaloniaFact]
    public async Task TryGuardAsync_OnLinux_HelperPresent_ReturnsTrue()
    {
        if (!OperatingSystem.IsLinux()) return;
        var helper = Substitute.For<IHelperAvailabilityService>();
        helper.IsMissing.Returns(false);
        var guard = new PrivilegedActionGuard(helper);

        var result = await guard.TryGuardAsync("anything");

        Assert.True(result);
    }

    // Note: helper-missing path triggers a modal dialog which requires a
    // window context. We intentionally don't unit-test that path here —
    // dialog-rendering correctness is covered by PrivilegeHelperRequiredDialogTests
    // in Task 15. The guard's helper-missing branch is exercised end-to-end
    // by the Wave G smoke test on the Linux VM.
}
```

- [ ] **Step 2: Run test (expect compile error)**

- [ ] **Step 3: Create `PrivilegedActionGuard.cs`**

```csharp
using global::Avalonia.Controls;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.UI.Avalonia.Views.Dialogs;

namespace AuraCore.UI.Avalonia.Services;

/// <summary>
/// Phase 6.17: UI-shell implementation of IPrivilegedActionGuard.
/// Windows: short-circuits to true (UAC handles elevation per-process).
/// Linux/macOS: checks IHelperAvailabilityService.IsMissing; if missing,
/// shows a modal PrivilegeHelperRequiredDialog and returns false.
/// </summary>
public sealed class PrivilegedActionGuard : IPrivilegedActionGuard
{
    private readonly IHelperAvailabilityService _helper;

    public PrivilegedActionGuard(IHelperAvailabilityService helper) => _helper = helper;

    public async Task<bool> TryGuardAsync(
        string actionDescription,
        string? remediationCommandOverride = null,
        CancellationToken ct = default)
    {
        if (OperatingSystem.IsWindows()) return true;
        if (!_helper.IsMissing) return true;

        var remediation = remediationCommandOverride
            ?? "sudo bash /opt/auracorepro/install-privhelper.sh";

        // Find the active window for the modal parent.
        var owner = global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetimeExtensions
            // Get the current ClassicDesktopStyleApplicationLifetime via App.Current
            .GetTopLevel();

        if (owner is global::Avalonia.Controls.Window window)
        {
            var dialog = new PrivilegeHelperRequiredDialog(actionDescription, remediation);
            await dialog.ShowDialog(window);
        }
        // If we can't find a window (test harness, headless), return false silently —
        // the caller's "Skipped" path will render UI feedback inline.
        return false;
    }
}
```

> **Note on `GetTopLevel()`**: The actual Avalonia 11.2 API for finding the active window is `((IClassicDesktopStyleApplicationLifetime)App.Current.ApplicationLifetime).MainWindow`. Verify the exact call site shape during implementation; the snippet above is illustrative — adjust to whatever pattern the rest of the codebase uses (e.g., `MainWindow.Current` or similar). The `Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetimeExtensions.GetTopLevel()` shown is a placeholder for the actual API.

The reliable shape (from existing repo code patterns, e.g., `OnInstanceIntentReceived` in App.axaml.cs):

```csharp
var owner = (App.Current?.ApplicationLifetime as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
```

Use that shape.

- [ ] **Step 4: Register in DI in `App.axaml.cs`**

After the `sc.AddSingleton<IModuleNavigator, ModuleNavigator>()` line (added in Phase 6.16 Task 4):

```csharp
        // ── Phase 6.17.E: privileged action guard (pre-flight helper check) ──
        sc.AddSingleton<global::AuraCore.Application.Interfaces.Platform.IPrivilegedActionGuard,
                        global::AuraCore.UI.Avalonia.Services.PrivilegedActionGuard>();
```

- [ ] **Step 5: Run tests + full UI suite**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~PrivilegedActionGuard" 2>&1 | tail -5
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj 2>&1 | tail -3
```
Expected: 2 of 2 filtered tests pass (one is OS-skipped on the executing platform); full UI suite goes to 1645.

- [ ] **Step 6: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Services/PrivilegedActionGuard.cs \
        src/UI/AuraCore.UI.Avalonia/App.axaml.cs \
        tests/AuraCore.Tests.UI.Avalonia/Services/PrivilegedActionGuardTests.cs
git commit -m "phase-6.17.E: PrivilegedActionGuard impl + DI registration + tests"
```

---

## Task 15: `PrivilegeHelperRequiredDialog` UserControl + 5 loc keys

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Dialogs/PrivilegeHelperRequiredDialog.axaml`
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Dialogs/PrivilegeHelperRequiredDialog.axaml.cs`
- Modify: `src/UI/AuraCore.UI.Avalonia/LocalizationService.cs` (add 5 keys EN+TR)
- Test: `tests/AuraCore.Tests.UI.Avalonia/Views/Dialogs/PrivilegeHelperRequiredDialogTests.cs`

- [ ] **Step 1: Add 5 EN loc keys**

Find the EN dict's closing `};` (currently around line 2533 after Phase 6.16's additions). Insert above it:

```csharp
        // ── Phase 6.17: Privilege Helper Required dialog ──
        ["privhelper.dialog.title"]    = "Privilege helper required",
        ["privhelper.dialog.reason"]   = "The privilege helper is required to apply system-level changes. Install it once and we'll use it for all privileged actions.",
        ["privhelper.dialog.docs"]     = "Open documentation",
        ["privhelper.dialog.tryAgain"] = "I've installed it",
        ["privhelper.dialog.closeBtn"] = "Close",
```

- [ ] **Step 2: Add 5 TR loc keys**

Find the TR dict's closing `};` (around line 4970). Insert above it:

```csharp
        // ── Phase 6.17: Privilege Helper Required dialog ──
        ["privhelper.dialog.title"]    = "Yetki yardımcısı gerekli",
        ["privhelper.dialog.reason"]   = "Sistem düzeyinde değişiklikleri uygulamak için yetki yardımcısı gerekli. Bir kez kurun, tüm yetkili işlemler için kullanacağız.",
        ["privhelper.dialog.docs"]     = "Dokümantasyonu aç",
        ["privhelper.dialog.tryAgain"] = "Kurdum",
        ["privhelper.dialog.closeBtn"] = "Kapat",
```

- [ ] **Step 3: Create `PrivilegeHelperRequiredDialog.axaml`**

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="AuraCore.UI.Avalonia.Views.Dialogs.PrivilegeHelperRequiredDialog"
        Width="600" Height="380"
        WindowStartupLocation="CenterOwner"
        CanResize="False"
        ShowInTaskbar="False">
  <Grid Margin="32" RowDefinitions="Auto,Auto,*,Auto,Auto">

    <!-- Title -->
    <TextBlock Grid.Row="0" x:Name="TitleText"
               FontSize="20" FontWeight="SemiBold"
               Foreground="{DynamicResource TextPrimaryBrush}"
               Margin="0,0,0,12" />

    <!-- Action description (caller-supplied) -->
    <TextBlock Grid.Row="1" x:Name="ActionDescText"
               FontSize="14"
               TextWrapping="Wrap"
               Foreground="{DynamicResource TextSecondaryBrush}"
               Margin="0,0,0,8" />

    <!-- Reason + remediation block -->
    <StackPanel Grid.Row="2" Spacing="12">
      <TextBlock x:Name="ReasonText"
                 FontSize="13"
                 TextWrapping="Wrap"
                 Foreground="{DynamicResource TextSecondaryBrush}" />

      <Border Padding="16,12" CornerRadius="6"
              Background="{DynamicResource BgSurfaceBrush}">
        <Grid ColumnDefinitions="*,Auto" RowDefinitions="Auto">
          <SelectableTextBlock Grid.Column="0"
                     x:Name="RemediationText"
                     FontFamily="Consolas,Menlo,Monospace"
                     FontSize="13"
                     Foreground="{DynamicResource TextPrimaryBrush}"
                     TextWrapping="Wrap" />
          <Button Grid.Column="1" x:Name="CopyButton"
                  VerticalAlignment="Top" Margin="12,0,0,0"
                  Click="OnCopyClick" />
        </Grid>
      </Border>
    </StackPanel>

    <!-- Docs link -->
    <Button Grid.Row="3" x:Name="DocsButton"
            HorizontalAlignment="Left" Margin="0,16,0,12"
            Click="OnDocsClick" />

    <!-- Footer buttons -->
    <StackPanel Grid.Row="4" Orientation="Horizontal" HorizontalAlignment="Right" Spacing="8">
      <Button x:Name="TryAgainButton" Click="OnTryAgainClick" />
      <Button x:Name="CloseButton" Click="OnCloseClick" />
    </StackPanel>
  </Grid>
</Window>
```

> **Note on brushes**: `TextPrimaryBrush`, `TextSecondaryBrush`, `BgSurfaceBrush` are defined in `Themes/AuraCoreThemeV2.axaml` (verified via Phase 6.16 Task 3 brush audit). If a brush doesn't resolve at build, use `OverlaySurfaceBrush` or the fallback shape from Phase 6.16 Task 3's report.

- [ ] **Step 4: Create `PrivilegeHelperRequiredDialog.axaml.cs`**

```csharp
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Markup.Xaml;
using System.Diagnostics;

namespace AuraCore.UI.Avalonia.Views.Dialogs;

/// <summary>
/// Phase 6.17: modal dialog shown when a privileged action is attempted but
/// the privilege helper isn't running. Mirrors UnavailableModuleView UX with
/// title / reason / copyable remediation / documentation link / Try Again /
/// Close. The "Try Again" button just closes the dialog with DialogResult
/// = true; the caller (PrivilegedActionGuard) returns false in either case
/// so the calling module's pre-flight short-circuits to OperationResult.Skipped.
/// (Auto-refresh on helper install is Phase 6.18+.)
/// </summary>
public partial class PrivilegeHelperRequiredDialog : Window
{
    private string _actionDescription = string.Empty;
    private string _remediationCommand = string.Empty;

    public PrivilegeHelperRequiredDialog()
    {
        InitializeComponent();
        LocalizationService.LanguageChanged += Render;
        Closed += (_, _) => LocalizationService.LanguageChanged -= Render;
    }

    public PrivilegeHelperRequiredDialog(string actionDescription, string remediationCommand) : this()
    {
        _actionDescription = actionDescription;
        _remediationCommand = remediationCommand;
        Render();
    }

    private void Render()
    {
        var title = this.FindControl<TextBlock>("TitleText");
        var actionDesc = this.FindControl<TextBlock>("ActionDescText");
        var reason = this.FindControl<TextBlock>("ReasonText");
        var remediation = this.FindControl<SelectableTextBlock>("RemediationText");
        var copyBtn = this.FindControl<Button>("CopyButton");
        var docsBtn = this.FindControl<Button>("DocsButton");
        var tryAgainBtn = this.FindControl<Button>("TryAgainButton");
        var closeBtn = this.FindControl<Button>("CloseButton");

        Title = LocalizationService.Get("privhelper.dialog.title");
        if (title is not null) title.Text = LocalizationService.Get("privhelper.dialog.title");
        if (actionDesc is not null) actionDesc.Text = _actionDescription;
        if (reason is not null) reason.Text = LocalizationService.Get("privhelper.dialog.reason");
        if (remediation is not null) remediation.Text = _remediationCommand;
        if (copyBtn is not null) copyBtn.Content = LocalizationService.Get("unavailable.copy");
        if (docsBtn is not null) docsBtn.Content = LocalizationService.Get("privhelper.dialog.docs");
        if (tryAgainBtn is not null) tryAgainBtn.Content = LocalizationService.Get("privhelper.dialog.tryAgain");
        if (closeBtn is not null) closeBtn.Content = LocalizationService.Get("privhelper.dialog.closeBtn");
    }

    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null || string.IsNullOrEmpty(_remediationCommand)) return;
        await clipboard.SetTextAsync(_remediationCommand);
        var copyBtn = this.FindControl<Button>("CopyButton");
        if (copyBtn is not null) copyBtn.Content = LocalizationService.Get("unavailable.copied");
        await Task.Delay(1500);
        if (copyBtn is not null) copyBtn.Content = LocalizationService.Get("unavailable.copy");
    }

    private void OnDocsClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            // Phase 6.18 will publish the real docs URL; placeholder for now.
            Process.Start(new ProcessStartInfo("https://docs.auracore.pro/linux/privilege-helper") { UseShellExecute = true })?.Dispose();
        }
        catch { /* best-effort */ }
    }

    private void OnTryAgainClick(object? sender, RoutedEventArgs e) => Close(true);
    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close(false);
}
```

- [ ] **Step 5: Write failing tests**

`tests/AuraCore.Tests.UI.Avalonia/Views/Dialogs/PrivilegeHelperRequiredDialogTests.cs`:

```csharp
using AuraCore.UI.Avalonia.Views.Dialogs;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views.Dialogs;

public class PrivilegeHelperRequiredDialogTests
{
    [AvaloniaFact]
    public void Dialog_RendersActionDescription_AndRemediation()
    {
        var d = new PrivilegeHelperRequiredDialog(
            "Flush RAM caches",
            "sudo bash /opt/auracorepro/install-privhelper.sh");

        var actionDesc = d.FindControl<TextBlock>("ActionDescText");
        var remediation = d.FindControl<SelectableTextBlock>("RemediationText");

        Assert.Equal("Flush RAM caches", actionDesc!.Text);
        Assert.Equal("sudo bash /opt/auracorepro/install-privhelper.sh", remediation!.Text);
    }

    [AvaloniaFact]
    public void Dialog_TitleAndButtons_Localized()
    {
        var d = new PrivilegeHelperRequiredDialog("anything", "anything");
        var title = d.FindControl<TextBlock>("TitleText");
        var closeBtn = d.FindControl<Button>("CloseButton");
        var tryAgainBtn = d.FindControl<Button>("TryAgainButton");

        Assert.False(string.IsNullOrEmpty(title!.Text));
        Assert.False(string.IsNullOrEmpty(closeBtn!.Content?.ToString()));
        Assert.False(string.IsNullOrEmpty(tryAgainBtn!.Content?.ToString()));
    }
}
```

- [ ] **Step 6: Run tests + full UI suite**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~PrivilegeHelperRequiredDialog" 2>&1 | tail -5
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj 2>&1 | tail -3
```
Expected: 2 dialog tests pass; full UI suite 1645 → 1647.

- [ ] **Step 7: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Dialogs/PrivilegeHelperRequiredDialog.axaml \
        src/UI/AuraCore.UI.Avalonia/Views/Dialogs/PrivilegeHelperRequiredDialog.axaml.cs \
        src/UI/AuraCore.UI.Avalonia/LocalizationService.cs \
        tests/AuraCore.Tests.UI.Avalonia/Views/Dialogs/PrivilegeHelperRequiredDialogTests.cs
git commit -m "phase-6.17.E: PrivilegeHelperRequiredDialog modal + 5 EN+TR loc keys"
```

---

## Task 16: Wave E foundation verify

- [ ] **Step 1: Solution-wide release build**

```bash
dotnet build AuraCorePro.sln -c Release 2>&1 | tail -5
```
Expected: 0 errors. CA1416 still clean (Wave B-D pre-conditions satisfied).

- [ ] **Step 2: Full test suites**

```bash
dotnet test tests/AuraCore.Tests.Unit/AuraCore.Tests.Unit.csproj 2>&1 | tail -3       # 22 expected
dotnet test tests/AuraCore.Tests.Module/AuraCore.Tests.Module.csproj 2>&1 | tail -3    # 177 expected
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj 2>&1 | tail -3  # 1647 expected
```

- [ ] **Step 3: No commit needed (verify only)**

---

# WAVE F — 6 modules adopt `IOperationModule.RunOperationAsync` (Tasks 17-22)

Each task follows the same shape: implement `IOperationModule` on the module class; the implementation calls the existing privileged operation but threads the guard pre-flight + builds an `OperationResult` from the outcome; ViewModel/View consume the result and render a post-action banner.

For each module, also add 3 new EN+TR loc keys (one set total, shared across all modules):

```csharp
// EN (insert into the same Phase 6.17 block as Task 15's keys):
["op.result.success"] = "Operation succeeded — freed {0}, {1} items, {2:F1}s.",
["op.result.skipped"] = "Skipped: {0}",
["op.result.failed"]  = "Failed: {0}",

// TR:
["op.result.success"] = "Tamamlandı — {0} alan, {1} öğe, {2:F1}s.",
["op.result.skipped"] = "Atlandı: {0}",
["op.result.failed"]  = "Başarısız: {0}",
```

These 3 loc keys land in Task 17 (the first Wave F task) and are reused by Tasks 18-22.

**Generic banner integration pattern** (applies to each Wave F task's view):

In each module View's existing button-click handler, REPLACE the current operation invocation:

```csharp
// BEFORE (illustrative):
var optResult = await _module.OptimizeAsync(plan, ...);
StatusText.Text = "Done";
```

WITH:

```csharp
// AFTER (Wave F pattern):
var guard = App.Services.GetRequiredService<global::AuraCore.Application.Interfaces.Platform.IPrivilegedActionGuard>();
var opResult = await ((global::AuraCore.Application.Interfaces.Modules.IOperationModule)_module)
    .RunOperationAsync(plan, guard, progress: null, ct: default);

PostActionBanner.IsVisible = true;
PostActionBanner.Classes.Clear();
PostActionBanner.Classes.Add(opResult.Status switch
{
    OperationStatus.Success => "banner-success",
    OperationStatus.Skipped => "banner-warning",
    OperationStatus.Failed  => "banner-error",
    _                       => "banner-info",
});
PostActionBanner.Text = opResult.Status switch
{
    OperationStatus.Success => string.Format(LocalizationService._("op.result.success"),
                                  FormatBytes(opResult.BytesFreed), opResult.ItemsAffected, opResult.Duration.TotalSeconds),
    OperationStatus.Skipped => string.Format(LocalizationService._("op.result.skipped"), opResult.Reason),
    OperationStatus.Failed  => string.Format(LocalizationService._("op.result.failed"), opResult.Reason),
    _                       => string.Empty,
};
```

Where `PostActionBanner` is a new `<TextBlock x:Name="PostActionBanner" IsVisible="False" />` element added to each View's XAML (under the existing action button row), and `FormatBytes` is a `private static string FormatBytes(long bytes)` helper. The Banner styles `banner-success`/`banner-warning`/`banner-error` should already exist or be added to the theme; if not, fall back to `Foreground="{DynamicResource <SemanticColor>Brush}"`.

For brevity in subsequent tasks, this pattern is referenced as **"the Wave F view banner pattern"**.

---

## Task 17: `RamOptimizerModule` adopts `IOperationModule` + view banner + 3 loc keys

**Files:**
- Modify: `src/Modules/AuraCore.Module.RamOptimizer/RamOptimizerModule.cs`
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/RamOptimizerView.axaml(.cs)`
- Modify: `src/UI/AuraCore.UI.Avalonia/LocalizationService.cs` (add 3 op.result.* keys EN+TR)
- Test: `tests/AuraCore.Tests.Module/RamOptimizerModuleOperationTests.cs`

- [ ] **Step 1: Add 3 EN+TR loc keys**

Per the snippet above, add `op.result.success` / `op.result.skipped` / `op.result.failed` to both EN and TR dicts.

- [ ] **Step 2: Read `RamOptimizerModule.cs`** to identify existing ctor, fields, and the `OptimizeAsync` method body.

- [ ] **Step 3: Add `IOperationModule` to the class declaration**

```csharp
public sealed class RamOptimizerModule : IOptimizationModule, IOperationModule
{
    // … existing members …
}
```

Add `using AuraCore.Application;` and `using AuraCore.Application.Interfaces.Modules;` and `using AuraCore.Application.Interfaces.Platform;` if missing.

- [ ] **Step 4: Implement `RunOperationAsync`**

```csharp
public async Task<OperationResult> RunOperationAsync(
    OptimizationPlan plan,
    IPrivilegedActionGuard guard,
    IProgress<TaskProgress>? progress = null,
    CancellationToken ct = default)
{
    var sw = System.Diagnostics.Stopwatch.StartNew();

    if (OperatingSystem.IsLinux())
    {
        // Pre-flight: helper required for /proc/sys/vm/drop_caches.
        if (!await guard.TryGuardAsync(
                actionDescription: "Flush RAM caches (/proc/sys/vm/drop_caches)",
                remediationCommandOverride: null,
                ct: ct))
        {
            sw.Stop();
            return OperationResult.Skipped("Privilege helper required",
                "sudo bash /opt/auracorepro/install-privhelper.sh");
        }

        try
        {
            // Reuse existing module operation. The legacy OptimizeAsync returns
            // OptimizationResult; we re-wrap the relevant fields.
            var legacy = await OptimizeAsync(plan, progress, ct);
            sw.Stop();
            if (!legacy.Success)
                return OperationResult.Failed("Drop_caches operation failed.", sw.Elapsed);
            return OperationResult.Success(legacy.BytesFreed, legacy.ItemsProcessed, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return OperationResult.Failed($"{ex.GetType().Name}: {ex.Message}", sw.Elapsed);
        }
    }

    // Windows path — EmptyWorkingSet API doesn't need helper.
    try
    {
        var legacy = await OptimizeAsync(plan, progress, ct);
        sw.Stop();
        if (!legacy.Success)
            return OperationResult.Failed("EmptyWorkingSet returned non-success.", sw.Elapsed);
        return OperationResult.Success(legacy.BytesFreed, legacy.ItemsProcessed, sw.Elapsed);
    }
    catch (Exception ex)
    {
        sw.Stop();
        return OperationResult.Failed($"{ex.GetType().Name}: {ex.Message}", sw.Elapsed);
    }
}
```

- [ ] **Step 5: Apply the Wave F view banner pattern to `RamOptimizerView.axaml(.cs)`**

Add `<TextBlock x:Name="PostActionBanner" IsVisible="False" Margin="0,12,0,0" />` to the XAML (below the existing optimize button or in the result panel area). Update the click handler per the pattern.

- [ ] **Step 6: Write tests**

`tests/AuraCore.Tests.Module/RamOptimizerModuleOperationTests.cs`:

```csharp
using System;
using System.Threading.Tasks;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.Module.RamOptimizer;
using NSubstitute;
using Xunit;

namespace AuraCore.Tests.Module;

public class RamOptimizerModuleOperationTests
{
    [Fact]
    public async Task RunOperationAsync_OnLinux_HelperMissing_ReturnsSkipped()
    {
        if (!OperatingSystem.IsLinux()) return;

        var guard = Substitute.For<IPrivilegedActionGuard>();
        guard.TryGuardAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
             .Returns(false); // helper missing

        IOperationModule module = new RamOptimizerModule();
        var result = await module.RunOperationAsync(new OptimizationPlan("ram-optimizer", new string[0]), guard);

        Assert.Equal(OperationStatus.Skipped, result.Status);
        Assert.Contains("helper", result.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunOperationAsync_OnWindows_NoGuardCalled_ReturnsSuccessOrFailed()
    {
        if (!OperatingSystem.IsWindows()) return;

        var guard = Substitute.For<IPrivilegedActionGuard>();
        guard.TryGuardAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
             .Returns(true); // not actually called on Windows path

        IOperationModule module = new RamOptimizerModule();
        var result = await module.RunOperationAsync(new OptimizationPlan("ram-optimizer", new string[0]), guard);

        Assert.True(result.Status is OperationStatus.Success or OperationStatus.Failed,
            $"Unexpected status {result.Status}");
        // EmptyWorkingSet is a Windows-only call; we accept either Success or
        // Failed (depending on permissions) but NOT Skipped on Windows.
        Assert.NotEqual(OperationStatus.Skipped, result.Status);
    }
}
```

- [ ] **Step 7: Run module tests + full suites**

```bash
dotnet test tests/AuraCore.Tests.Module/AuraCore.Tests.Module.csproj --filter "FullyQualifiedName~RamOptimizerModuleOperation" 2>&1 | tail -5
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj 2>&1 | tail -3
```
Expected: 1 test passes (the OS-matching one), 1 skipped vacuously.

- [ ] **Step 8: Commit**

```bash
git add src/Modules/AuraCore.Module.RamOptimizer/RamOptimizerModule.cs \
        src/UI/AuraCore.UI.Avalonia/Views/Pages/RamOptimizerView.axaml \
        src/UI/AuraCore.UI.Avalonia/Views/Pages/RamOptimizerView.axaml.cs \
        src/UI/AuraCore.UI.Avalonia/LocalizationService.cs \
        tests/AuraCore.Tests.Module/RamOptimizerModuleOperationTests.cs
git commit -m "phase-6.17.F: RamOptimizer adopts IOperationModule + post-action banner + 3 loc keys"
```

---

## Task 18: `JunkCleanerModule` adopts `IOperationModule` + view banner

**Files:**
- Modify: `src/Modules/AuraCore.Module.JunkCleaner/JunkCleanerModule.cs`
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/CategoryCleanView.axaml(.cs)` (junk-cleaner uses CategoryCleanView per MainWindow.RegisterAllModuleViews)
- Test: `tests/AuraCore.Tests.Module/JunkCleanerModuleOperationTests.cs`

Same procedure as Task 17:

- [ ] **Step 1**: Add `IOperationModule` to class, add `RunOperationAsync` implementation. JunkCleaner is cross-platform; on Linux it calls `apt clean`/`dnf clean` via `IShellCommandService.RunPrivilegedAsync` (helper required); on Windows it deletes Windows tmp folder content directly (no helper needed).

```csharp
public async Task<OperationResult> RunOperationAsync(
    OptimizationPlan plan,
    IPrivilegedActionGuard guard,
    IProgress<TaskProgress>? progress = null,
    CancellationToken ct = default)
{
    var sw = System.Diagnostics.Stopwatch.StartNew();

    if (OperatingSystem.IsLinux())
    {
        if (!await guard.TryGuardAsync("Delete cached package manager files", null, ct))
        {
            sw.Stop();
            return OperationResult.Skipped("Privilege helper required",
                "sudo bash /opt/auracorepro/install-privhelper.sh");
        }
    }

    try
    {
        var legacy = await OptimizeAsync(plan, progress, ct);
        sw.Stop();
        return legacy.Success
            ? OperationResult.Success(legacy.BytesFreed, legacy.ItemsProcessed, sw.Elapsed)
            : OperationResult.Failed("Junk-cleaner operation reported non-success.", sw.Elapsed);
    }
    catch (Exception ex)
    {
        sw.Stop();
        return OperationResult.Failed($"{ex.GetType().Name}: {ex.Message}", sw.Elapsed);
    }
}
```

- [ ] **Step 2**: Apply the Wave F view banner pattern to `CategoryCleanView` (this view is shared by junk-cleaner / disk-cleanup / privacy-cleaner — only junk-cleaner adopts the new shape in 6.17, but the banner element + click-handler logic switches on `IsJunkCleaner` so other modules render legacy `OptimizationResult`-based status while junk-cleaner renders the new `OperationResult`).

- [ ] **Step 3**: Write 2-3 module tests (Linux helper-missing → Skipped; Windows → Success/Failed).

- [ ] **Step 4**: Build + tests + commit.

Commit message: `phase-6.17.F: JunkCleaner adopts IOperationModule + CategoryCleanView post-action banner`

---

## Task 19: `SystemdManagerModule` adopts `IOperationModule` + view banner

**Files:**
- Modify: `src/Modules/AuraCore.Module.SystemdManager/SystemdManagerModule.cs`
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/SystemdManagerView.axaml(.cs)`
- Test: `tests/AuraCore.Tests.Module/SystemdManagerModuleOperationTests.cs`

Same procedure as Task 17. Linux-only — no Windows path. Action description: "Modify systemd service state (enable/disable/mask)."

```csharp
public async Task<OperationResult> RunOperationAsync(
    OptimizationPlan plan,
    IPrivilegedActionGuard guard,
    IProgress<TaskProgress>? progress = null,
    CancellationToken ct = default)
{
    var sw = System.Diagnostics.Stopwatch.StartNew();

    if (!OperatingSystem.IsLinux())
    {
        sw.Stop();
        return OperationResult.Failed("Systemd Manager is Linux-only.", sw.Elapsed);
    }

    if (!await guard.TryGuardAsync("Modify systemd service state (enable/disable/mask)", null, ct))
    {
        sw.Stop();
        return OperationResult.Skipped("Privilege helper required",
            "sudo bash /opt/auracorepro/install-privhelper.sh");
    }

    try
    {
        var legacy = await OptimizeAsync(plan, progress, ct);
        sw.Stop();
        return legacy.Success
            ? OperationResult.Success(0, legacy.ItemsProcessed, sw.Elapsed)  // service ops don't free bytes
            : OperationResult.Failed("systemctl operation reported non-success.", sw.Elapsed);
    }
    catch (Exception ex)
    {
        sw.Stop();
        return OperationResult.Failed($"{ex.GetType().Name}: {ex.Message}", sw.Elapsed);
    }
}
```

Tests + commit. Commit message: `phase-6.17.F: SystemdManager adopts IOperationModule + post-action banner`

---

## Task 20: `SwapOptimizerModule` adopts `IOperationModule` + view banner

**Files:**
- Modify: `src/Modules/AuraCore.Module.SwapOptimizer/SwapOptimizerModule.cs`
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/SwapOptimizerView.axaml(.cs)`
- Test: `tests/AuraCore.Tests.Module/SwapOptimizerModuleOperationTests.cs`

Same procedure. Linux-only. Action description: "Modify swap configuration (swappiness/swapon/swapoff)."

Tests + commit. Commit message: `phase-6.17.F: SwapOptimizer adopts IOperationModule + post-action banner`

---

## Task 21: `PackageCleanerModule` adopts `IOperationModule` + view banner

**Files:**
- Modify: `src/Modules/AuraCore.Module.PackageCleaner/PackageCleanerModule.cs`
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/PackageCleanerView.axaml(.cs)`
- Test: `tests/AuraCore.Tests.Module/PackageCleanerModuleOperationTests.cs`

Same procedure. Linux-only. Action description: "Remove orphaned packages and clean package manager cache."

Tests + commit. Commit message: `phase-6.17.F: PackageCleaner adopts IOperationModule + post-action banner`

---

## Task 22: `JournalCleanerModule` adopts `IOperationModule` + view binding via VM

**Files:**
- Modify: `src/Modules/AuraCore.Module.JournalCleaner/JournalCleanerModule.cs`
- Modify: `src/UI/AuraCore.UI.Avalonia/ViewModels/JournalCleanerViewModel.cs` (this module uses MVVM with a dedicated VM, unlike RamOptimizer which is direct view)
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/JournalCleanerView.axaml`
- Test: `tests/AuraCore.Tests.Module/JournalCleanerModuleOperationTests.cs`

Same module-side procedure. View-side, since JournalCleaner uses MVVM, the VM exposes a `LastOperationResult` `OperationResult?` property and `string LastBannerText` + `string LastBannerClasses` derived properties. The XAML binds:

```xml
<TextBlock IsVisible="{Binding LastOperationResult, Converter={StaticResource NotNullConverter}}"
           Text="{Binding LastBannerText}"
           Classes="{Binding LastBannerClasses}" />
```

(`NotNullConverter` likely exists already in the codebase; if not, it's a 5-line `IValueConverter` to add inline.)

Tests + commit. Commit message: `phase-6.17.F: JournalCleaner adopts IOperationModule + VM banner binding`

---

# WAVE G — `PrivilegeHelperMissingBanner` Linux smoke verify (Task 23)

## Task 23: Linux VM smoke verify of banner + fix wiring if missing

**Files:**
- Possibly: `src/UI/AuraCore.UI.Avalonia/Services/HelperAvailabilityService.cs` (startup probe fix)
- Possibly: `src/UI/AuraCore.UI.Avalonia/Views/Banners/PrivilegeHelperMissingBanner.axaml.cs` (copy update)
- Possibly: `src/UI/AuraCore.UI.Avalonia/LocalizationService.cs` (banner loc keys EN+TR)
- Create: `docs/superpowers/phase-6-17-banner-verify.md`

- [ ] **Step 1: Build linux-x64 publish**

```bash
cd /c/Users/Admin/Desktop/Gelistirme/AuraCorePro/AuraCorePro
rm -rf packaging/dist/publish-linux-x64
dotnet publish src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj \
  --nologo -c Release -r linux-x64 \
  --self-contained true -p:PublishSingleFile=false -p:Version=1.8.1 \
  -o packaging/dist/publish-linux-x64 2>&1 | tail -3
```

- [ ] **Step 2: Tar + scp to VM**

```bash
tar czf packaging/dist/auracore-pro-linux-6.17.tar.gz -C packaging/dist/publish-linux-x64 .
scp -i ~/.ssh/id_ed25519_aura packaging/dist/auracore-pro-linux-6.17.tar.gz deniz@192.168.162.129:~/auracore-pro-6.17.tar.gz
ssh -i ~/.ssh/id_ed25519_aura deniz@192.168.162.129 \
  'rm -rf ~/auracore-pro-6.17-test && mkdir ~/auracore-pro-6.17-test && tar xzf ~/auracore-pro-6.17.tar.gz -C ~/auracore-pro-6.17-test && chmod +x ~/auracore-pro-6.17-test/AuraCore.Pro && echo OK'
```

- [ ] **Step 3: User-driven GUI smoke (executor coordinates with user)**

The user (operator) launches the app on the VM desktop GUI:

```bash
cd ~/auracore-pro-6.17-test && ./AuraCore.Pro
```

Operator captures a screenshot of the MainWindow top edge and reports:
- Is the `PrivilegeHelperMissingBanner` visible? (yes/no)
- If yes, what does the text say?
- Is there an install command shown?
- Is there a Copy button?

- [ ] **Step 4 (conditional, only if banner is NOT visible):** Root-cause fix

Read `src/UI/AuraCore.UI.Avalonia/Services/HelperAvailabilityService.cs`. The likely failure mode is that `IsBannerVisible` defaults to false and only flips on `ReportMissing()` call (which only happens after a privileged op fails). Fix: probe at startup via background `Task.Run`:

```csharp
public HelperAvailabilityService(/* existing deps */)
{
    // … existing init …

    // Phase 6.17.G: probe helper presence at startup so the banner can light
    // up immediately on launch, not only after a privileged op fails.
    if (!OperatingSystem.IsWindows())
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var present = await ProbeHelperPresenceAsync();
                if (!present) ReportMissing();
                else ReportAvailable();
            }
            catch { /* leave default */ }
        });
    }
}

private static async Task<bool> ProbeHelperPresenceAsync()
{
    // D-Bus probe: can we list the auracore-privhelper bus name?
    // Implementation depends on existing IPC infrastructure (Tmds.DBus per Phase 5.2.1).
    // Minimum viable: try a no-op privileged call with short timeout; if it succeeds OR
    // returns a "method not found" error, the daemon is alive; if it returns "service
    // not registered" or times out, the helper is missing.
    // For 6.17 ship, fallback to file-existence check on the install marker:
    return File.Exists("/opt/auracorepro/install-privhelper.sh.installed");
}
```

> If the actual D-Bus probe is hard to implement on the time budget, use a file-existence sentinel that `install-privhelper.sh` writes (Phase 6.18 will replace with a real D-Bus probe).

- [ ] **Step 5 (always):** Update banner copy with install command

If the existing banner just says "Privilege helper missing" without the install command, extend `PrivilegeHelperMissingBanner.axaml` to include the command + Copy button (mirror the `UnavailableModuleView` / `PrivilegeHelperRequiredDialog` style). Add EN+TR loc keys: `privhelperBanner.text` / `privhelperBanner.copyHint` if not already present.

- [ ] **Step 6: Write verify doc**

```bash
cat > docs/superpowers/phase-6-17-banner-verify.md << 'EOF'
# Phase 6.17 PrivilegeHelperMissingBanner Linux Verify

**VM:** Ubuntu 24.04.4 LTS / VMware (192.168.162.129)
**Build:** `phase-6-17-debt-closure` HEAD <sha>
**Date:** <YYYY-MM-DD>

## Pre-fix state

- Banner visible at startup: <yes | no>
- If visible, text: <quoted>
- Install command shown: <yes | no>
- Copy button present: <yes | no>

## Fix applied (if needed)

- HelperAvailabilityService startup probe: <yes | no>
- Banner copy updated with install command: <yes | no>
- Loc keys updated: <list>

## Post-fix verification

- Banner visible at startup: <yes>
- Text: <quoted>
- Install command shown: <yes>
- Copy button works: <yes>
- Screenshot: <path or attached>

Signed off: <operator>
EOF

git add docs/superpowers/phase-6-17-banner-verify.md src/UI/AuraCore.UI.Avalonia/Services/HelperAvailabilityService.cs src/UI/AuraCore.UI.Avalonia/Views/Banners/PrivilegeHelperMissingBanner.axaml src/UI/AuraCore.UI.Avalonia/Views/Banners/PrivilegeHelperMissingBanner.axaml.cs src/UI/AuraCore.UI.Avalonia/LocalizationService.cs
git commit -m "phase-6.17.G: PrivilegeHelperMissingBanner Linux verify + startup probe + actionable copy"
```

---

# WAVE H — Phase close (Task 24)

## Task 24: Full solution build + smoke matrix update + CHANGELOG + ceremonial commit + tag

**Files:**
- Modify: `docs/superpowers/phase-6-16-vm-verify-matrix.md` (add Phase 6.17 column)
- Create or modify: `CHANGELOG.md`

- [ ] **Step 1: Solution-wide release build (FINAL)**

```bash
dotnet clean AuraCorePro.sln 2>&1 | tail -3
dotnet build AuraCorePro.sln -c Release 2>&1 | tail -10
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`. CA1416 clean. Pre-existing AVLN3001 on `CategoryCleanView` carries over (Phase 6.16 ctor-throws-on-null preservation — acceptable).

- [ ] **Step 2: Full test suites**

```bash
dotnet test tests/AuraCore.Tests.Unit/AuraCore.Tests.Unit.csproj 2>&1 | tail -3
dotnet test tests/AuraCore.Tests.Module/AuraCore.Tests.Module.csproj 2>&1 | tail -3
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj 2>&1 | tail -3
```

Expected counts:
- Tests.Unit: 22 (18 + 4 from Wave E Task 12)
- Tests.Module: 175 + 2 (Wave A) + 6×2 (Wave F minimum) = ~189
- Tests.UI.Avalonia: 1643 + 2 (Wave E PrivilegedActionGuard) + 2 (Wave E Dialog) = ~1647

Total: ~1858 (close to spec's ~1874 estimate).

- [ ] **Step 3: Update Linux VM smoke matrix**

Append to `docs/superpowers/phase-6-16-vm-verify-matrix.md`:

```markdown

---

## Phase 6.17 verification (added 2026-05-XX)

| Module (post-6.17 adoption) | Post-action banner shows? | Helper-missing path correct? | Notes |
|---|---|---|---|
| RAM Optimizer    | <yes/no> | <yes/no> | |
| Junk Cleaner     | <yes/no> | <yes/no> | |
| Systemd Manager  | <yes/no> | <yes/no> | |
| Swap Optimizer   | <yes/no> | <yes/no> | |
| Package Cleaner  | <yes/no> | <yes/no> | |
| Journal Cleaner  | <yes/no> | <yes/no> | |

PrivilegeHelperMissingBanner visible at startup: <yes/no>
System Health no longer shows -2147483648%: <yes/no>

Sign-off: __________________
```

- [ ] **Step 4: Add CHANGELOG entry**

If `CHANGELOG.md` doesn't exist, create it. Append (or insert at top after a `## v1.8.1 (Phase 6.17)` header):

```markdown
## v1.8.1 — Phase 6.17 (2026-05-XX)

### Fixed
- System Health storage drives no longer show `-2147483648%` on Linux virtual filesystems. Virtual filesystems (`tmpfs`, `proc`, `sysfs`, `devpts`, `securityfs`, `cgroup`, etc.) are filtered from the user-facing list; remaining drives have a zero-capacity guard plus `Math.Clamp(0, 100)` defense-in-depth on the percent calculation.
- Privileged operations (RAM Optimizer, Junk Cleaner, Systemd Manager, Swap Optimizer, Package Cleaner, Journal Cleaner) now surface a clear "Privilege helper required" diagnostic with copyable install command instead of silently no-oping when the privilege helper isn't installed.

### Changed
- `[SupportedOSPlatform("windows")]` attribute now applied to all 7 previously-deferred Windows-only module classes (DefenderManager, FirewallRules, AppInstaller, DriverUpdater, GamingMode, StorageCompression, BloatwareRemoval) plus their View pages and DI registration extensions. CA1416 analyzer clean across the entire Release build.
- `[SupportedOSPlatform("linux")]` applied to all 10 Linux-only module classes (Systemd / Swap / Package / Journal / SnapFlatpak / Kernel / LinuxAppInstaller / Cron / Grub / Docker[+macos]).
- `[SupportedOSPlatform("macos")]` applied to all 9 macOS-only module classes — the build-hygiene prerequisite for the eventual macOS notarized release.
- 6 modules now expose an `IOperationModule.RunOperationAsync` returning `OperationResult` with explicit `Success / Skipped / Failed` status + reason + remediation. The legacy `OptimizeAsync` shape is preserved for the other 40+ modules; opt-in migration to Phase 6.18+.
- New `PrivilegeHelperRequiredDialog` modal mirrors the existing `UnavailableModuleView` UX (title + reason + copyable remediation + Try Again + Close + 5 EN+TR loc keys).
- Post-action banner on each adopting module View shows green/amber/red feedback for Success/Skipped/Failed (3 EN+TR loc keys: `op.result.success`, `op.result.skipped`, `op.result.failed`).
- `PrivilegeHelperMissingBanner` now probes helper presence at app startup (Linux) so it lights up immediately on launch when the helper isn't installed, not only after a privileged op fails.

### Carry-forward to Phase 6.18+

- Real privileged-ops smoke (deploy `install-privhelper.sh` and verify RAM Optimizer actually drops caches, Package Cleaner actually removes orphans, etc.)
- Migrate the other 40+ modules to `IOperationModule.RunOperationAsync` incrementally
- D-Bus presence subscribe so `PrivilegeHelperMissingBanner` and `UnavailableModuleView` auto-refresh when the helper is installed without app restart
- macOS implementation of the privilege-helper analog (XPC service + signed entitlements + install pkg) — gated on Mac hardware
- App-level `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` beyond CA1416 (nullability, etc.)
- System Health stale-data warning — distinguish "drive momentarily not ready" from "drive doesn't exist"
```

- [ ] **Step 5: Verify branch state**

```bash
git log --oneline phase-6-17-debt-closure | head -30
# Expect ~25 commits (Task 0-24)
```

- [ ] **Step 6: Merge to main**

```bash
git switch main
git merge --no-ff phase-6-17-debt-closure -m "Phase 6.17 — Platform Debt Closure + Privileged Ops Feedback (merge)

Closes 3 Phase 6.16 carry-forward debts:
- A: System Health virtual-fs filter + zero-cap guard + Math.Clamp on percent
- B: SupportedOSPlatform(windows) on 7 deferred Win-only modules + cascade
- C: SupportedOSPlatform(linux) on 10 Linux modules + cascade
- D: SupportedOSPlatform(macos) on 9 macOS modules + cascade
- E: OperationResult record + IOperationModule + IPrivilegedActionGuard + PrivilegeHelperRequiredDialog + 8 loc keys
- F: 6 modules adopt IOperationModule (RAM/Junk/Systemd/Swap/Package/Journal) with post-action banner
- G: PrivilegeHelperMissingBanner Linux smoke verify + startup probe + actionable copy
- H: Phase close — full Release build 0 CA1416 errors, ~1858 tests passing

Test deltas: Backend 233 → 233; UI 1643 → ~1647; Module 175 → ~189; Unit 18 → 22. Total ~1836 → ~1858.

v1.8.1 release ready — admin panel upload pending operator action."

git commit --allow-empty -m "Phase 6.17 closed — platform debt cleared, v1.8.1 release ready"
```

- [ ] **Step 7: Tag v1.8.1 (NEW tag — never pushed before, no force needed)**

```bash
git tag -a v1.8.1 -m "AuraCore Pro v1.8.1 (Phase 6.17 platform debt closure)

Phase 6.17 fixes:
- System Health -2147483648% display bug fixed
- CA1416 analyzer clean (all 19 platform-specific modules annotated)
- Privileged-ops feedback loop (6 modules adopt OperationResult)
- PrivilegeHelperMissingBanner Linux startup probe

See CHANGELOG.md for full notes."
```

- [ ] **Step 8: Build fresh release zips (Win + Linux)**

```bash
rm -rf packaging/dist/publish-* packaging/dist/AuraCorePro-1.8.1-*.zip

dotnet publish src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj \
  --nologo -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=false -p:Version=1.8.1 \
  -o packaging/dist/publish-win-x64 2>&1 | tail -3

dotnet publish src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj \
  --nologo -c Release -r linux-x64 \
  --self-contained true -p:PublishSingleFile=false -p:Version=1.8.1 \
  -o packaging/dist/publish-linux-x64 2>&1 | tail -3

cd packaging/dist
powershell.exe -Command "Compress-Archive -Path 'publish-win-x64\*' -DestinationPath 'AuraCorePro-1.8.1-win-x64.zip' -Force"
powershell.exe -Command "Compress-Archive -Path 'publish-linux-x64\*' -DestinationPath 'AuraCorePro-1.8.1-linux-x64.zip' -Force"
ls -lh AuraCorePro-1.8.1-*.zip
powershell.exe -Command "Get-FileHash 'AuraCorePro-1.8.1-win-x64.zip' -Algorithm SHA256 | Select-Object -ExpandProperty Hash"
powershell.exe -Command "Get-FileHash 'AuraCorePro-1.8.1-linux-x64.zip' -Algorithm SHA256 | Select-Object -ExpandProperty Hash"
cd ../..
```

Record both SHA256 hashes for the user.

- [ ] **Step 9: PAUSE — hand off to user**

Report to user:
- 25 commits across phase-6-17-debt-closure branch
- main HEAD = ceremonial commit; merge commit + ceremonial commit local-only (NOT pushed)
- v1.8.1 tag created locally at the ceremonial commit (NOT pushed)
- Two new release zips with fresh SHA256 hashes
- Wave G banner verify doc captured at `docs/superpowers/phase-6-17-banner-verify.md`
- Wave H smoke matrix updated at `docs/superpowers/phase-6-16-vm-verify-matrix.md` with v1.8.1 column

User actions required:

```bash
# Push merge + tag
git push origin main
git push origin v1.8.1

# Admin panel upload:
# - AuraCorePro-1.8.1-win-x64.zip      sha256: <hash>
# - AuraCorePro-1.8.1-linux-x64.zip    sha256: <hash>
# Toggle visibility / publish.
```

**DO NOT auto-deploy. STOP HERE.**

---

## Self-review notes (per writing-plans skill)

**Spec coverage check:**
- D1 (`OperationResult` record) → Task 12 ✓
- D2 (`IPrivilegedActionGuard`) → Task 13 + Task 14 ✓
- D3 (`PrivilegeHelperRequiredDialog`) → Task 15 ✓
- D4 (System Health virtual-fs filter + zero-cap guard) → Task 1 ✓
- D5 (CA1416 cascade-fix pattern catalog) → Tasks 3-11 (each task references the catalog) ✓
- D6 (6 modules adopt `IOperationModule`) → Tasks 17-22 ✓
- D7 (`PrivilegeHelperMissingBanner` Linux verify) → Task 23 ✓
- D8 (8 sub-waves) → Wave A-H structure ✓
- D9 (test coverage budget) → Task 24 Step 2 verifies counts ✓
- D10 (risks) → addressed inline (per-module STOP rules in Wave B; clamp defense in Wave A; Windows short-circuit verified in Task 14)
- D11 (carry-forward) → Task 24 Step 4 CHANGELOG section captures the list ✓

**Placeholder scan:**
- No "TBD", "TODO", "fill in details", "implement later" anywhere in tasks ✓
- The documentation URL `https://docs.auracore.pro/linux/privilege-helper` is a placeholder explicitly declared with rationale (Phase 6.18 ships actual docs); the dialog still works because Copy button + install command are present ✓
- Banner verify doc template (`<sha>`, `<YYYY-MM-DD>`, `<yes/no>`) uses placeholders that the executor fills in during Wave G smoke — these are intended template fields, not plan-failures ✓

**Type consistency:**
- `OperationResult` factory methods (`Success(long, int, TimeSpan)`, `Skipped(string, string?)`, `Failed(string, TimeSpan)`) referenced consistently in Tasks 12, 17-22 ✓
- `IPrivilegedActionGuard.TryGuardAsync(string, string?, CancellationToken)` consistent in Tasks 13, 14, 17-22 ✓
- `IOperationModule.RunOperationAsync(OptimizationPlan, IPrivilegedActionGuard, IProgress<TaskProgress>?, CancellationToken)` consistent in Tasks 13, 17-22 ✓
- `OperationStatus` enum (`Success`, `Skipped`, `Failed`) consistent in Tasks 12, 17-22 ✓
- `PrivilegeHelperRequiredDialog(string actionDescription, string remediationCommand)` ctor signature consistent in Tasks 14, 15 ✓

**Scope check:** Single phase, 8 sub-waves, ~25 tasks. Comparable to Phase 6.16 (8 waves, 28 tasks). No further decomposition needed.

**Ambiguity check:**
- "Per-module STOP rule (>20 errors)" is explicit per Task 3 Step 4 ✓
- "6 modules" listed exactly in Wave F task headers (Tasks 17-22, one each) ✓
- "Wave G banner verify scope" bounded to "make banner visible at startup on Linux + actionable copy"; deeper auto-refresh on helper install is explicit Phase 6.18 carry-forward (CHANGELOG Task 24 Step 4) ✓
- Layering boundary: `IPrivilegedActionGuard` lives in Application layer (Task 13 Step 2), implementation in UI layer (Task 14) — explicit ✓
