# Phase 6.16 — Linux Platform Awareness + Module Audit Wave — Design

**Date:** 2026-04-28
**Author:** Brainstormed in Phase 6.16 session (post-v1.8.0-tag, pre-release)
**Status:** Locked, ready for `superpowers:writing-plans` skill
**Predecessor:** [Phase 6.15.6 Sentry Backend Observability](../../../memory/project_phase_6_item_15_6_sentry_backend.md)
**Trigger:** v1.8.0 desktop release blocked. User-led VM smoke test on Ubuntu 24.04 revealed 7+ hard crashes, 4+ silent dashboard-fallbacks, 3+ broken renders, and 11+ Windows-centric UI strings shown to Linux users. v1.8.0 zips built and SHA256-hashed but **NOT uploaded to admin panel** — release on hold pending this phase.

## Goal

Make AuraCorePro genuinely cross-platform on Linux at runtime — every module either works on the current platform OR shows a clear, actionable "unavailable" view. No hard crashes. No silent dashboard-fallbacks. No Windows-centric strings shown to non-Windows users. Once Phase 6.16 closes, v1.8.0 desktop release ships with confidence (re-tagged on the fixed commit, force-pushed to git tag, then admin-panel uploaded for end users).

## Non-goals

- **No new modules.** Every module that exists today either works or gracefully reports unavailable. No new Linux-native modules in this phase.
- **No macOS smoke testing in this phase.** macOS notarization remains blocked on Apple Developer hardware (Phase 6 roadmap Item 6 unchanged). However, Phase 6.16 produces a **macOS pre-release gate checklist** document (Wave H) so when macOS hardware arrives, the same kind of disaster doesn't recur.
- **No architectural overhaul of `IOptimizationModule`.** Use C# default interface members to evolve the contract additively — every existing module continues to compile and ship.
- **No backward-compat shim for missing `[SupportedOSPlatform]` attributes.** New CA1416 escalation will surface real issues; we fix them in this phase, not next.
- **No retry / install-helper-on-the-fly UX** in this phase. UnavailableModuleView shows the install command; user runs it. Auto-install is Phase 6.17+.
- **No JIT-on-helper-restart auto-refresh.** If user installs the helper and the module re-renders fresh next time they open it, that's enough; we don't subscribe to D-Bus presence events in this phase.

## Audit findings reference

This spec is built on top of 4 parallel audit reports run during the brainstorming session. Key findings consolidated below; full evidence is in the brainstorming session transcript.

### 4 categories of structural failure on Linux

1. **NavigationService dispatch incomplete** — `MainWindow.axaml.cs:SetActiveContent` switch (lines 410-469) is a hardcoded 40+ case `module-id → view-factory` table. Linux modules (`systemd-manager`, `swap-optimizer`, `package-cleaner`, `cron-manager`) are absent from the switch — fall through to `_ => new Pages.DashboardView()`. **This is the root cause of the user-reported "click goes to dashboard" symptom on these 4 modules.**

2. **Unguarded Windows-only API calls** that crash on Linux at module-load or scan time:
   - `Modules/AuraCore.Module.AutorunManager/AutorunManagerModule.cs` lines 25-224 — direct `Registry.LocalMachine` / `Registry.CurrentUser` / `RegistryKey.OpenBaseKey` without any platform guard. ScanAsync invoked → throws `PlatformNotSupportedException`.
   - `Modules/AuraCore.Module.RegistryOptimizer/RegistryOptimizerModule.cs` lines 86-389 — 6 unguarded scanners + 9 unguarded write operations. Same pattern.
   - `Modules/AuraCore.Module.ContextMenu/ContextMenuModule.cs` lines 137-158 — 4 unguarded Registry accesses. Sidebar marks the module Windows-only, but module class is registered unconditionally — analyzer-invisible crash.
   - `Modules/AuraCore.Module.DefenderManager/DefenderManagerModule.cs` lines 361-363 — `WindowsPrincipal.IsInRole()` + `WindowsIdentity.GetCurrent()` without guard. CA1416 catches.
   - `Desktop/AuraCore.Desktop/Services/Scheduler/BackgroundScheduler.cs` lines 147-169 — `[DllImport("user32.dll")] GetLastInputInfo` invoked on every 60s timer tick. **Worst severity** because it's not module-bound; runs while app is open regardless of which view is active. Linux throws `EntryPointNotFoundException`.
   - `UI/AuraCore.UI.Avalonia/Views/Pages/StartupOptimizerView.axaml.cs` lines 37-64 — Registry access inside `Task.Run`; method-entry guard exists at line 29 but doesn't propagate into the Task delegate.
   - `UI/AuraCore.UI.Avalonia/Views/Pages/ServiceManagerView.axaml.cs` lines 39-44 — `ServiceController` ctor + property access inside `Task.Run`; same pattern.

3. **View-level null-module silent crashes** on cross-platform modules whose ViewModels weren't built defensively:
   - `Views/CategoryCleanView.cs` ctor accepts a null `IOptimizationModule` and exits early (`if (module is null) return;`) — UI elements never bound, `Loaded` event still subscribes RunScan handler which dereferences null `_module` → silent NRE → broken render. **This is the root cause of the user-reported "Junk Cleaner / Disk Cleanup / Privacy Cleaner berbat görünümü" symptom.**

4. **Localization + Quick Actions Windows-centric** — 11 localization keys + 1 dashboard tile show Windows-specific text to Linux users. Most egregious examples:
   - `quickaction.removebloat.label = "Remove Windows bloat"` shown on Linux Dashboard.
   - `login.subtitle = "AI Powered Windows Intelligence"` shown to every user before they sign in.
   - `set.websiteLink = "auracore.pro - Windows Optimization SaaS"` shown in Settings on every platform.
   - `settings.tagline = "AI Powered Windows Intelligence"` shown in Settings on every platform.
   - `startup.subtitleShort = "Manage programs that run at Windows startup"` — Startup Optimizer module's own subtitle, currently declared `platform="all"` but is Windows-only.
   - `hosts.subtitle = "Edit the Windows hosts file"` — Hosts Editor is genuinely cross-platform (Linux has `/etc/hosts`, macOS too); copy is Windows-centric.

### Sidebar declaration mismatches (2)

- `SidebarViewModel.cs:123` — `Module("startup-optimizer", "nav.startupOptimizer")` defaults to `"all"`. Should be `"windows"` (uses Registry.Run).
- `SidebarViewModel.cs:227` — `Module("autorun-manager", "nav.autorunManager")` defaults to `"all"`. Should be `"windows"` (uses Registry deep scan).

### CA1416 false sense of safety

`dotnet build` shows 15-19 CA1416 warnings, but the audit found ~30 unguarded Windows-only API uses. CA1416 misses many because:
- `Microsoft.Win32.Registry` static class methods aren't always annotated.
- `[DllImport]` `extern` declarations don't propagate analysis to call sites.
- Module classes lack `[SupportedOSPlatform("windows")]` attributes — analyzer can't infer scope.

**Phase 6.16 fixes the build hygiene** so the next platform-incompat creep is caught at compile-time, not in user's VM smoke test.

## Locked design decisions

### D1 — `IPlatformAwareModule` interface shape

Two-layer additive contract on existing `IOptimizationModule` via C# default interface members. No breaking change.

```csharp
public interface IOptimizationModule
{
    // ... existing members ...

    /// <summary>
    /// Existing: declarative platform support enum. Source of truth.
    /// </summary>
    SupportedPlatform Platform => SupportedPlatform.Windows;

    /// <summary>
    /// Phase 6.16: fast sync platform check derived from Platform enum.
    /// Used by SidebarViewModel.VisibleCategories() — no async overhead during sidebar render.
    /// </summary>
    bool IsPlatformSupported => Platform switch
    {
        SupportedPlatform.Windows => OperatingSystem.IsWindows(),
        SupportedPlatform.Linux   => OperatingSystem.IsLinux(),
        SupportedPlatform.MacOS   => OperatingSystem.IsMacOS(),
        SupportedPlatform.All     => true,
        _                         => true,
    };

    /// <summary>
    /// Phase 6.16: slow async runtime check. Returns rich result.
    /// Used by NavigationService BEFORE rendering view — catches runtime issues
    /// (helper daemon not running, tool not installed, feature disabled) and produces
    /// an actionable UnavailableModuleView instead of crashing.
    /// Default: Available on all supported platforms (modules opt in by overriding).
    /// </summary>
    Task<ModuleAvailability> CheckRuntimeAvailabilityAsync(CancellationToken ct = default)
        => Task.FromResult(IsPlatformSupported
            ? ModuleAvailability.Available
            : ModuleAvailability.WrongPlatform(Platform));
}

public enum AvailabilityCategory
{
    Available,
    WrongPlatform,        // Module is for a different OS family (e.g. Linux module on Windows)
    HelperNotRunning,     // auracore-privhelper D-Bus daemon not active on Linux
    ToolNotInstalled,     // External CLI tool missing (systemctl, apt, brew, docker, etc.)
    FeatureDisabled,      // Module declared off via config or feature flag
    BackendUnreachable,   // Reserved for future cloud-backed modules
}

public sealed record ModuleAvailability(
    bool IsAvailable,
    AvailabilityCategory Category,
    string? Reason,
    string? RemediationCommand)
{
    public static ModuleAvailability Available => new(true, AvailabilityCategory.Available, null, null);
    public static ModuleAvailability WrongPlatform(SupportedPlatform supports) =>
        new(false, AvailabilityCategory.WrongPlatform,
            $"This module supports {supports} only.", null);
    public static ModuleAvailability HelperNotRunning(string remediationCommand) =>
        new(false, AvailabilityCategory.HelperNotRunning,
            "Privilege helper (auracore-privhelper) not detected.", remediationCommand);
    public static ModuleAvailability ToolNotInstalled(string toolName, string? remediationCommand) =>
        new(false, AvailabilityCategory.ToolNotInstalled,
            $"Required tool '{toolName}' not found on this system.", remediationCommand);
    public static ModuleAvailability FeatureDisabled(string reason) =>
        new(false, AvailabilityCategory.FeatureDisabled, reason, null);
}
```

### D2 — UnavailableModuleView UX

Hybrid model:

- **WrongPlatform** → not visible in sidebar at all. `SidebarViewModel` consults `IsPlatformSupported` (sync) and excludes. User never sees the module on the wrong OS.
- **HelperNotRunning / ToolNotInstalled / FeatureDisabled / BackendUnreachable** → sidebar entry IS visible (because `IsPlatformSupported` is true), but clicking it yields a **full-page `UnavailableModuleView`** with:
  - Module title (so user knows what they tried to open)
  - Category-specific icon + heading ("Privilege helper required" / "Tool not installed")
  - Reason (one-sentence explanation)
  - **Remediation command** in a copyable code block (`sudo apt install ...` or path to `install.sh`)
  - "Try again" button — re-runs `CheckRuntimeAvailabilityAsync`
  - Optional "Documentation" link (Phase 6.17)

This is better than fully hiding because:
- Users discover that AuraCorePro CAN do the thing, they just need to install something.
- The remediation command makes it actionable.
- Once they install + click "Try again", the module loads fresh.

### D3 — Localization strategy: pure platform-neutral rewrite

11 keys to change. No platform-conditional ternary, no key duplication, no runtime resolution. Just better copywriting that reads correctly on every platform.

| Key | Old (Windows-centric) | New (platform-neutral) |
|---|---|---|
| `login.subtitle` | "AI Powered Windows Intelligence" | "AI-Powered System Intelligence" |
| `set.websiteLink` | "auracore.pro - Windows Optimization SaaS" | "auracore.pro - Cross-platform System Optimization" |
| `settings.tagline` | "AI Powered Windows Intelligence" | "AI-Powered System Intelligence" |
| `settings.description` | "...Windows optimization suite featuring 27+ modules..." | "...Cross-platform optimization suite featuring 27+ modules..." |
| `quickaction.removebloat.label` | "Remove Windows bloat" | (filtered out on non-Windows; on Windows label stays "Remove Windows bloat") |
| `dc.subtitle` | "Windows deep clean — system caches + duplicates + empty folders" | "Deep clean — system caches, duplicates, empty folders" |
| `hosts.subtitle` | "Edit the Windows hosts file..." | "Edit the system hosts file (block domains, set custom DNS mappings)" |
| `symlink.adminWarning` | "Creating symbolic links requires administrator privileges on Windows." | "On Windows, creating symbolic links requires administrator privileges. On Linux/macOS, requires write access to the target directory." |
| `onb.welcomeDesc` | "...Windows optimization toolkit..." | "...Cross-platform optimization toolkit..." |
| `onb.customizeTitle` | "Customize Your Windows" | "Customize Your System" |
| `onb.customizeDetail` | "All tweaks work on both Windows 10 and Windows 11." | "Modules adapt to your platform — Windows tweaks on Windows, systemd controls on Linux, defaults editing on macOS." |

For the `quickaction.removebloat` Dashboard tile, the **fix is not localization but platform-filter** — the tile is removed from the Dashboard's QuickActions collection on non-Windows. See Wave E for code changes.

For the Startup Optimizer subtitle "Manage programs that run at Windows startup": this module is being moved to `platform="windows"` (Wave D), so the subtitle becomes accurate-by-context (only Windows users see it).

### D4 — CA1416 enforcement: per-project warnings-as-errors + module class attributes

Add to `src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj`:

```xml
<PropertyGroup>
  <MSBuildWarningsAsErrors>CA1416</MSBuildWarningsAsErrors>
</PropertyGroup>
```

Same to:
- `src/Desktop/AuraCore.Desktop.Services/AuraCore.Desktop.Services.csproj`
- `src/Modules/AuraCore.Module.*/AuraCore.Module.*.csproj` (all 30+ module projects)
- `src/Plugins/AuraCore.Plugin.SDK/AuraCore.Plugin.SDK.csproj`

Then add `[SupportedOSPlatform("windows")]` to Windows-only module classes to enable richer analyzer flow. Modules getting attributes:
- `AuraCore.Module.AutorunManager`
- `AuraCore.Module.RegistryOptimizer`
- `AuraCore.Module.ContextMenu`
- `AuraCore.Module.DefenderManager`
- `AuraCore.Module.FirewallRules`
- `AuraCore.Module.ServiceManager`
- `AuraCore.Module.AppInstaller`
- `AuraCore.Module.DriverUpdater`
- `AuraCore.Module.IsoBuilder`
- `AuraCore.Module.GamingMode`
- `AuraCore.Module.StorageCompression`
- `AuraCore.Module.Bloatware`
- `AuraCore.Module.Tweaks`
- `AuraCore.Module.TaskbarTweaks`
- `AuraCore.Module.ExplorerTweaks`

After adding attributes, the build will surface MORE CA1416 warnings (which are now errors). Wave B fixes guard them. By end of Phase 6.16, `dotnet build -c Release` exits clean with **0 CA1416 warnings/errors**.

### D5 — Single Phase 6.16, 8 sub-waves

Following the Phase 6.13 / 6.15 cadence. Sub-waves are sequential because each builds on the previous (you can't fix CA1416 enforcement before fixing the actual unguarded code, etc.).

| Sub-wave | Title | Effort |
|---|---|---|
| **6.16.A** | Architectural foundation — `IPlatformAwareModule` defaults + `ModuleAvailability` record + `UnavailableModuleView` Avalonia UserControl + NavigationService extension | 1.5 d |
| **6.16.B** | Hard crash guards — AutorunManager, RegistryOptimizer, ContextMenu, DefenderManager, BackgroundScheduler, StartupOptimizerView, ServiceManagerView | 1.5 d |
| **6.16.C** | Silent fail fixes — Linux modules in NavigationService dispatch table, CategoryCleanView null-handling, IHelperAvailabilityService integration in 9 Linux modules | 1 d |
| **6.16.D** | Sidebar declarations — `startup-optimizer` + `autorun-manager` → `"windows"`, plus full sidebar audit for declaration mismatches | 0.5 d |
| **6.16.E** | Localization sweep — 11 keys + Dashboard QuickActions platform filter + StartupOptimizer subtitle (cascades from D) + FirewallRulesView XAML hardcoded label | 0.5 d |
| **6.16.F** | CA1416 enforcement — `MSBuildWarningsAsErrors` per csproj + `[SupportedOSPlatform]` attributes on 15 Windows-only modules | 0.5 d |
| **6.16.G** | Linux VM re-verify — every module on Ubuntu 24.04 VM, no crashes, no silent dashboard fallback, UnavailableModuleView shows for unsupported runtime conditions, smoke screenshots collected | 0.5 d |
| **6.16.H** | macOS pre-release gate doc — `docs/ops/macos-prerelease-checklist.md` | 0.5 d |

**Total: 6-7 days solo.** Subagent-driven execution can compress this somewhat by parallelizing module guards in Wave B.

After Phase 6.16 closes:
- Force-push v1.8.0 git tag to the new HEAD (safe — tag never published via admin panel, no users impacted).
- Re-run `cross-publish.ps1 -Version 1.8.0` for fresh zips.
- Re-compute SHA256.
- THEN admin-panel upload + publish (Phase C from v1.8.0 release plan).

### D6 — macOS pre-release gate doc

`docs/ops/macos-prerelease-checklist.md` — markdown checklist operator runs before any macOS release. Items:

```
## Build hygiene
- [ ] dotnet build AuraCorePro.sln -c Release exits with 0 CA1416 warnings
- [ ] All Windows-only modules have [SupportedOSPlatform("windows")] attribute
- [ ] All Linux-only modules have [SupportedOSPlatform("linux")] attribute
- [ ] All macOS-only modules have [SupportedOSPlatform("macos")] attribute (NEW for macOS release)

## Sidebar correctness
- [ ] Every module declared in SidebarViewModel has correct platform value
- [ ] On macOS, run app in headless test harness (Avalonia.Headless), assert sidebar items count matches expected macOS module list
- [ ] No "Windows" / "Linux" leakage in macOS-visible labels (run grep against built binary's localization dict)

## Runtime smoke (real Mac required)
- [ ] App launches without crash (cold start to MainWindow visible)
- [ ] Click each macOS module — opens the module view, no crash
- [ ] Click each cross-platform module — opens the module view, no crash
- [ ] Each Linux- and Windows-only module is hidden (sidebar scan)
- [ ] Each module marked "RuntimeUnavailable" shows UnavailableModuleView with actionable remediation (e.g. "brew install X")
- [ ] BackgroundScheduler runs for 3 minutes without throwing (timer-tick trap)

## macOS-specific
- [ ] Apple Developer ID signature applied to .app bundle
- [ ] Notarization request submitted to Apple, approval received
- [ ] DMG created, smoke-tested on a separate clean macOS VM/machine
- [ ] Gatekeeper assessment passes (`spctl -a` exits 0)

## Localization
- [ ] All 11 platform-neutral keys verified on macOS UI
- [ ] No "Windows" / "Linux" string leaked to a macOS-visible UI surface
- [ ] Onboarding flow tested end-to-end on macOS

## CHANGELOG + version
- [ ] CHANGELOG entry added for macOS support
- [ ] Version bumped (likely v1.9.0 with macOS as headline feature)
- [ ] All version-bump locations updated (per Phase 6.15.7 lessons + same 11+ locations)

## Distribution
- [ ] cross-publish.ps1 OR build-macos.sh produces signed .dmg artifact
- [ ] R2 upload via admin panel: macOS platform tile enabled (currently "Coming Soon" in admin UI — Phase 6.17 prep)
- [ ] GitHub Releases mirror includes macOS artifact
- [ ] Landing page OS-detect serves macOS DMG to mac visitors

Sign-off: __________ (operator)  Date: __________
```

The checklist is **operator-driven** in this phase. Future Phase 6.17+ may automate parts (CI gate runs the build hygiene check, Avalonia.Headless test runs the sidebar correctness check).

### D7 — Default interface members on `IOptimizationModule`

Adding via C# 8 default interface members. Existing modules that don't override these get sane defaults. **Zero breaking change** to existing implementations.

The `Platform` enum stays as the declarative source. `IsPlatformSupported` is a derived computed property. `CheckRuntimeAvailabilityAsync` defaults to `Available` if `IsPlatformSupported` is true.

Modules opt-in to richer behavior by overriding `CheckRuntimeAvailabilityAsync`. For Linux modules requiring helper:

```csharp
// Modules/AuraCore.Module.SystemdManager/SystemdManagerModule.cs
public sealed class SystemdManagerModule : IOptimizationModule
{
    private readonly IHelperAvailabilityService _helper;
    private readonly IShellCommandService _shell;
    public SupportedPlatform Platform => SupportedPlatform.Linux;

    public SystemdManagerModule(IHelperAvailabilityService helper, IShellCommandService shell)
    { _helper = helper; _shell = shell; }

    public async Task<ModuleAvailability> CheckRuntimeAvailabilityAsync(CancellationToken ct = default)
    {
        if (!OperatingSystem.IsLinux())
            return ModuleAvailability.WrongPlatform(SupportedPlatform.Linux);

        if (_helper.IsMissing)
            return ModuleAvailability.HelperNotRunning(
                "sudo bash /opt/auracorepro/install-privhelper.sh");

        if (!await _shell.CommandExistsAsync("systemctl", ct))
            return ModuleAvailability.ToolNotInstalled("systemctl",
                "Switch to a systemd-based Linux distribution.");

        return ModuleAvailability.Available;
    }

    // ... existing ScanAsync, RunAsync etc.
}
```

### D8 — Sidebar filter integration

`SidebarViewModel.VisibleCategories()` currently does:

```csharp
private static bool ShouldShow(SidebarItem item)
    => item.Platform switch
       {
           "all"     => true,
           "windows" => OperatingSystem.IsWindows(),
           "linux"   => OperatingSystem.IsLinux(),
           "macos"   => OperatingSystem.IsMacOS(),
           _         => false,
       };
```

This stays — it operates on the **declarative `SidebarItem.Platform` string**. The change is:
- `Module(...)` factory function matches the `IOptimizationModule.Platform` enum (audit Wave D verifies every entry).
- For modules where DI did NOT register them on this platform (Windows-only modules on Linux), the `_moduleMap` lookup at navigation time returns null — but they were never visible in sidebar in the first place because of `ShouldShow` filter. Defense-in-depth.

### D9 — NavigationService rewrite

Phase 6.1 introduced `INavigationService` for deep-link URL routing. We extend it:

```csharp
public interface INavigationService
{
    // Existing (Phase 6.1):
    Task NavigateToAsync(string moduleId, IReadOnlyDictionary<string, string>? args = null);

    // Phase 6.16: Surface the in-memory dispatch table (replaces SetActiveContent's hardcoded switch)
    void RegisterModuleView(string moduleId, Func<IServiceProvider, UserControl> viewFactory);
}

internal sealed class NavigationService : INavigationService
{
    private readonly Dictionary<string, Func<IServiceProvider, UserControl>> _viewFactories = new();
    private readonly Dictionary<string, IOptimizationModule> _moduleMap;
    private readonly IServiceProvider _services;

    public async Task NavigateToAsync(string moduleId, IReadOnlyDictionary<string, string>? args = null)
    {
        if (!_moduleMap.TryGetValue(moduleId, out var module))
        {
            // Module not registered on this platform (DI exclusion). Show "Module not found" overlay.
            await _shell.SetActiveContent(new UnavailableModuleView(
                moduleId,
                ModuleAvailability.WrongPlatform(SupportedPlatform.All)));
            return;
        }

        // Phase 6.16: gating check before view instantiation
        var availability = await module.CheckRuntimeAvailabilityAsync();
        if (!availability.IsAvailable)
        {
            await _shell.SetActiveContent(new UnavailableModuleView(module.DisplayName, availability));
            return;
        }

        if (_viewFactories.TryGetValue(moduleId, out var factory))
        {
            var view = factory(_services);
            await _shell.SetActiveContent(view);
        }
        else
        {
            // Should not happen if Wave C registers all modules; defensive log + fallback
            _logger.LogError("No view factory for module {ModuleId}", moduleId);
            await _shell.SetActiveContent(new DashboardView());
        }
    }
}
```

In `App.axaml.cs` startup, register all view factories per platform:

```csharp
if (OperatingSystem.IsLinux())
{
    nav.RegisterModuleView("systemd-manager",     sp => sp.GetRequiredService<SystemdManagerView>());
    nav.RegisterModuleView("swap-optimizer",      sp => sp.GetRequiredService<SwapOptimizerView>());
    nav.RegisterModuleView("package-cleaner",     sp => sp.GetRequiredService<PackageCleanerView>());
    nav.RegisterModuleView("cron-manager",        sp => sp.GetRequiredService<CronManagerView>());
    // ... rest
}
```

`MainWindow.SetActiveContent` gets a tiny shim that just delegates to `INavigationService` and sets `ContentArea.Content`.

### D10 — `CategoryCleanView` null-module hardening

Currently:

```csharp
public CategoryCleanView(IOptimizationModule? module)
{
    InitializeComponent();
    _module = module;
    if (module is null) return;  // ← silently breaks
    PageTitle.Text = module.DisplayName;
    // ...
    Loaded += async (s, e) => await RunScan();  // ← still subscribed even when _module null
}
```

Fix: refactor so `CategoryCleanView` is never instantiated with a null module. NavigationService is responsible for surfacing UnavailableModuleView when the module is missing or runtime-unavailable. CategoryCleanView's ctor becomes:

```csharp
public CategoryCleanView(IOptimizationModule module)
{
    InitializeComponent();
    _module = module ?? throw new ArgumentNullException(nameof(module));
    PageTitle.Text = module.DisplayName;
    // ... rest unchanged
    Loaded += async (s, e) => await RunScan();
}
```

This is a small but important defensive change — NavigationService is now the single gating point; CategoryCleanView trusts its inputs.

### D11 — Test coverage

Per-module unit tests added to each `tests/AuraCore.Tests.Module/<ModuleName>Tests.cs`:

```csharp
public class SystemdManagerModuleTests
{
    [Fact]
    public void IsPlatformSupported_OnLinux_ReturnsTrue()
    {
        // Note: this test is RuntimeInformation-bound; on CI we run separate Win + Linux jobs
        var module = new SystemdManagerModule(/* mocks */);
        Assert.Equal(OperatingSystem.IsLinux(), module.IsPlatformSupported);
    }

    [Fact]
    public async Task CheckRuntimeAvailabilityAsync_HelperMissing_ReturnsHelperNotRunning()
    {
        var helper = Substitute.For<IHelperAvailabilityService>();
        helper.IsMissing.Returns(true);
        var module = new SystemdManagerModule(helper, Substitute.For<IShellCommandService>());

        var result = await module.CheckRuntimeAvailabilityAsync();

        Assert.False(result.IsAvailable);
        Assert.Equal(AvailabilityCategory.HelperNotRunning, result.Category);
    }

    [Fact]
    public async Task CheckRuntimeAvailabilityAsync_SystemctlMissing_ReturnsToolNotInstalled()
    {
        var helper = Substitute.For<IHelperAvailabilityService>();
        helper.IsMissing.Returns(false);
        var shell = Substitute.For<IShellCommandService>();
        shell.CommandExistsAsync("systemctl", Arg.Any<CancellationToken>()).Returns(false);
        var module = new SystemdManagerModule(helper, shell);

        var result = await module.CheckRuntimeAvailabilityAsync();

        Assert.False(result.IsAvailable);
        Assert.Equal(AvailabilityCategory.ToolNotInstalled, result.Category);
    }
}
```

Plus `tests/AuraCore.Tests.UI.Avalonia/SidebarViewModelTests.cs`:

```csharp
[Fact]
public void OnLinux_LinuxOnlyAndCrossPlatformModulesVisible_WindowsOnlyHidden()
{
    // Use a test override of OperatingSystem.Is* via a platform-detector seam
    var sidebar = new SidebarViewModel(/* with platform=linux */);
    var visible = sidebar.VisibleAdvancedItems().Select(m => m.ModuleId).ToList();

    Assert.Contains("systemd-manager", visible);    // Linux-only: visible
    Assert.Contains("symlink-manager", visible);    // Cross-platform: visible
    Assert.DoesNotContain("autorun-manager", visible);  // Windows-only (after Wave D fix): hidden
    Assert.DoesNotContain("startup-optimizer", visible); // Windows-only (after Wave D fix): hidden
}
```

Plus integration in Wave G: VM smoke test on Ubuntu 24.04. Each module clicked, no crashes, screenshot evidence filed.

Test budget: backend ~233 → ~233 (no API changes). Avalonia UI ~1632 → ~1700 (+~70: per-module availability tests). Mobile 32 → 32. Total full suite ~2280 → ~2350.

### D12 — Granular module-by-module audit closure

Wave G includes a per-module pass-fail matrix that the executor records. Format:

```
| Module          | Click on Linux | Crash? | Render OK? | Available?      | Notes |
|-----------------|----------------|--------|------------|-----------------|-------|
| Dashboard       | ✓              | No     | OK         | Always          |       |
| Junk Cleaner    | ✓              | No     | OK         | Always          | Wave C fix verified |
| RAM Optimizer   | ✓              | No     | OK         | Always          | Wave B fix verified |
| Network         | ✓              | No     | OK         | Always          |       |
| Battery         | ✓              | No     | OK         | Always          |       |
| Systemd Manager | ✓              | No     | OK         | HelperReq UI    | Helper not installed → UnavailableModuleView shown |
| Defender Mgr    | (not visible)  | -      | -          | Hidden (Win)    | Sidebar hides correctly |
| ... (every module) ... |
```

This matrix is appended to the Wave G commit message as evidence. v1.8.0 release blocker is lifted only when 100% of modules pass.

## Architecture per sub-wave

### 6.16.A — Architectural foundation

**Files:**
- Modify `src/Application/AuraCore.Application/Optimization/IOptimizationModule.cs` — add `IsPlatformSupported`, `CheckRuntimeAvailabilityAsync`, `ModuleAvailability` record, `AvailabilityCategory` enum (default interface members)
- Create `src/Application/AuraCore.Application/Optimization/ModuleAvailability.cs` — record + factory methods
- Create `src/UI/AuraCore.UI.Avalonia/Views/UnavailableModuleView.axaml` + `.axaml.cs` — full-page UnavailableModuleView with module name, category icon, reason text, copyable remediation command, Try-Again button
- Modify `src/UI/AuraCore.UI.Avalonia/Services/NavigationService.cs` — add `RegisterModuleView`, refactor `NavigateToAsync` with availability check
- Modify `src/UI/AuraCore.UI.Avalonia/Views/MainWindow.axaml.cs` — strip the 60-line `SetActiveContent` switch, replace with `INavigationService` delegation
- Add `tests/AuraCore.Tests.UI.Avalonia/Services/NavigationServiceAvailabilityTests.cs` — 4-5 tests covering each `AvailabilityCategory`

### 6.16.B — Hard crash guards (8 files)

Each fix is small (3-5 lines added). Pattern: early `if (!OperatingSystem.IsWindows()) return ...` at the top of the entry point.

Files:
1. `src/Modules/AuraCore.Module.AutorunManager/AutorunManagerModule.cs` — `ScanAsync` line 20: add platform guard returning empty `ScanResult`. Same for `OptimizeAsync`/`ApplyChangeAsync`.
2. `src/Modules/AuraCore.Module.RegistryOptimizer/RegistryOptimizerModule.cs` — `ScanAsync`, `FixIssueAsync`, etc.
3. `src/Modules/AuraCore.Module.ContextMenu/ContextMenuModule.cs` — `ScanAsync`.
4. `src/Modules/AuraCore.Module.DefenderManager/DefenderManagerModule.cs` — wrap `WindowsPrincipal` block at line 361 in `if (OperatingSystem.IsWindows()) { ... } else { isAdmin = false; }`.
5. `src/Desktop/AuraCore.Desktop/Services/Scheduler/BackgroundScheduler.cs` — `GetIdleTime()` guard at line 147.
6. `src/UI/AuraCore.UI.Avalonia/Views/Pages/StartupOptimizerView.axaml.cs` — guard inside `Task.Run` (currently only at method entry).
7. `src/UI/AuraCore.UI.Avalonia/Views/Pages/ServiceManagerView.axaml.cs` — same.
8. (Possible) `src/UI/AuraCore.UI.Avalonia/Helpers/NativeMemory.cs` — add `[SupportedOSPlatform("windows")]` attribute (not a guard fix; analyzer hint).

For each: add or extend the unit test asserting "on non-Windows, returns empty result without throwing".

### 6.16.C — Silent fail fixes

**View factory registration (NavigationService):**
- `App.axaml.cs` Linux block — register view factories for `systemd-manager`, `swap-optimizer`, `package-cleaner`, `cron-manager`, `journal-cleaner`, `snap-flatpak-cleaner`, `kernel-cleaner`, `grub-manager`, `docker-cleaner`, `linux-app-installer`.
- Same for Windows (re-register what was previously hardcoded in MainWindow switch).
- macOS placeholder (registers nothing yet; Wave H carry-forward).

**Helper integration in 9 Linux modules:**
Each gets `IHelperAvailabilityService` constructor-injected and overrides `CheckRuntimeAvailabilityAsync`:

| Module | Tool to detect | Remediation hint |
|---|---|---|
| `SystemdManagerModule` | `systemctl` | "Switch to a systemd-based distribution" |
| `SwapOptimizerModule` | `swapon` (always present on Linux) — only helper check | "sudo bash /opt/auracorepro/install-privhelper.sh" |
| `PackageCleanerModule` | `apt`, `dnf`, `pacman`, or `zypper` (any one) | "Install one of: apt, dnf, pacman, zypper" |
| `CronManagerModule` | `crontab` | "sudo apt install cron" |
| `JournalCleanerModule` | `journalctl` | "Switch to a systemd-based distribution" |
| `SnapFlatpakCleanerModule` | `snap` or `flatpak` (either one) | "sudo snap install snap" or "sudo apt install flatpak" |
| `KernelCleanerModule` | `apt-get` (Debian/Ubuntu specific) — gracefully degrade for non-apt distros | "Currently supports apt-based distributions only" |
| `GrubManagerModule` | `update-grub` | "sudo apt install grub-pc" |
| `DockerCleanerModule` | `docker` | "https://docs.docker.com/engine/install/" |

**`CategoryCleanView` ctor null-handling:** change parameter from `IOptimizationModule?` to `IOptimizationModule` (non-nullable). Throw `ArgumentNullException` if null. NavigationService never calls with null because it pre-checks via `_moduleMap.TryGetValue`.

### 6.16.D — Sidebar declarations

Two-line fix in `src/UI/AuraCore.UI.Avalonia/ViewModels/SidebarViewModel.cs`:

- Line 123: `Module("startup-optimizer", "nav.startupOptimizer")` → `Module("startup-optimizer", "nav.startupOptimizer", "windows")`
- Line 227: `Module("autorun-manager", "nav.autorunManager")` → `Module("autorun-manager", "nav.autorunManager", "windows")`

Plus a full audit pass: read entire `SidebarViewModel.cs`, cross-reference each `Module(...)` call against the corresponding `IOptimizationModule.Platform` enum value, fix any other mismatches found.

Add a test: `SidebarViewModelDeclarationTests.cs` — for each module in the sidebar, assert the sidebar's declared `Platform` string matches the module's `Platform` enum.

### 6.16.E — Localization sweep

Update 11 keys in `LocalizationService.cs` (both EN and TR dictionaries) per the table in D3.

Modify `src/UI/AuraCore.UI.Avalonia/ViewModels/Dashboard/QuickActionPresets.Windows.cs`:
- Rename method from `Windows()` to `Default()` (the name was misleading — it's the default tile set, NOT Windows-only)
- Inside the new `Default()`, add platform filter:
  ```csharp
  var tiles = new List<QuickActionTileVM>
  {
      new("quick-cleanup", _localization["quickaction.quickcleanup.label"], quickCleanup),
      new("optimize-ram",  _localization["quickaction.optimizeram.label"], optimizeRam),
  };
  if (OperatingSystem.IsWindows())
      tiles.Add(new("remove-bloat", _localization["quickaction.removebloat.label"], removeBloat));
  return tiles;
  ```
- Update caller in `DashboardViewModel.cs` line 62 to call `QuickActionPresets.Default(...)`.

Modify `src/UI/AuraCore.UI.Avalonia/Views/Pages/FirewallRulesView.axaml`:
- Line 11: replace hardcoded `Subtitle="Manage Windows Firewall inbound and outbound rules"` with bound localization key.

Add hardcoded-string scanner test enhancement: extend `HardcodedStringScannerTests` to flag the words "Windows" / "Windows 10" / "Windows 11" / "Linux" / "macOS" / "Ubuntu" appearing in non-platform-keyed localization values, except when the surrounding key has a platform suffix (`*.windows`, `*.linux`, etc.).

### 6.16.F — CA1416 enforcement

For each csproj that contains module / view / service code:
1. Add `<MSBuildWarningsAsErrors>CA1416</MSBuildWarningsAsErrors>` to first `<PropertyGroup>`.
2. For Windows-only module classes, add `[SupportedOSPlatform("windows")]` attribute at class declaration.

Order of operations: Wave B fixes first (so the build doesn't break when we flip warnings-to-errors). Then Wave F flip. Then verify clean.

If the flip surfaces NEW issues that Wave B didn't catch (which is its purpose), Wave F includes follow-up fixes.

### 6.16.G — Linux VM re-verify

**Workflow** (executor follows):
1. SSH to user's Linux VM (`192.168.162.129` per Phase 6.15.7 session)
2. Pull latest `phase-6-16-linux-platform-awareness` branch (or main if already merged)
3. `dotnet publish` for linux-x64
4. Launch on VM desktop GUI
5. Click each module in sidebar order; record:
   - Did the app crash? (severity 1 → blocker)
   - Did sidebar entry render? (severity 2 — ideally hidden if Windows-only)
   - Did module render its expected content OR UnavailableModuleView? (severity 3 — must be one or the other; never blank/dashboard-fallback)
   - Screenshot for evidence
6. Fill the Wave G matrix table (D12 above)
7. If any module fails: bug filed back to Wave B/C/D as needed, re-cycle

End state: 100% modules pass-or-show-UnavailableModuleView. Then Phase 6.16 closes.

### 6.16.H — macOS pre-release gate doc

Just write the markdown file per D6. Commit. No code changes.

## Testing summary

| Layer | Before | After | Δ |
|---|---|---|---|
| Backend (xunit) | 233 | 233 | +0 (no backend changes) |
| UI.Avalonia | 1632 | ~1700 | +~70 (NavigationService + per-module availability + sidebar declaration consistency) |
| Module | 158 | ~180 | +~22 (per-module CheckRuntimeAvailabilityAsync + IsPlatformSupported × ~9 Linux modules + 5 Windows-only modules) |
| Mobile (jest) | 32 | 32 | +0 |
| Admin-panel (vitest) | 89 | 89 | +0 |
| **Total** | **2144** | **~2235** | **+~91** |

VM smoke test in Wave G is integration-level — not a unit test. Documented in commit message as evidence matrix.

## Risks

- **CA1416 enforcement could surface a long tail** — if Wave F flip produces 50+ new errors that Wave B didn't anticipate, schedule may slip. Mitigation: do Wave F flip on a sub-branch first, count the errors, scope the fix work, decide between (a) finish in Wave F or (b) defer to a Phase 6.17 follow-up.
- **`CheckRuntimeAvailabilityAsync` is now in the navigation hot-path** — slow IsAvailable methods (e.g., shell-command-exists check) can delay sidebar response. Mitigation: cache the `ModuleAvailability` per session (modules can opt-in to invalidation when state changes — Phase 6.17 carry-forward).
- **`UnavailableModuleView` localization** — the 4 categories × EN+TR = 8 new localization keys. Ensure these don't get back-fed into the same Windows-centric trap. Mitigation: use platform-neutral copy from day 1.
- **`MSBuildWarningsAsErrors` interaction with `Directory.Build.props`** — if a global `TreatWarningsAsErrors=true` is already set, the per-project additive enforcement might conflict. Mitigation: read `Directory.Build.props` early in Wave F; reconcile.
- **VM smoke test can't catch macOS** — Wave G is Linux only; the macOS analog (Wave H) is just a checklist. The first real macOS smoke test will be when notarization happens. Acceptable risk — macOS isn't shipping in this phase.

## Carry-forward → Phase 6.17+

- **Auto-detect helper-installed and refresh module status** (D-Bus presence subscribe). Today: user installs helper, then clicks "Try again" on UnavailableModuleView.
- **Cache `CheckRuntimeAvailabilityAsync` results** with selective invalidation (helper-installed event, settings change, etc.).
- **First-run install wizard for Linux helper** (offer to run `install.sh` from app on first launch).
- **macOS implementation** of the privilege-helper analog (XPC service + signed entitlements).
- **CI gate** automating the macOS pre-release checklist (Phase 6.18+ aspiration).
- **App-level `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`** — broader cleanup beyond CA1416 (nullability, etc.) — separate phase.
- **Deferred from previous phases** (unchanged from 6.15.6 carry-forward): FCM env activation, FcmService cache singleton, TOTP backup codes, force-logout-all, mobile incident-response feature pack, iOS port, Play Store migration, Sentry expansion to admin-panel + mobile + desktop, claude-mem keepalive.

## Continuity

- Brainstorming completed autonomously per user's "AFK + default + ultrathinking + no compromise" instruction.
- All 13 design decisions locked above (D1-D12 + sidebar/cross-platform commitments).
- Spec written. Self-review next, then `superpowers:writing-plans` skill invocation in this same session.
- v1.8.0 release HOLD — admin-panel upload deferred until Phase 6.16 closes.

## Self-review notes

- **Placeholders:** none. Every reference is concrete (file paths, line numbers, exact API names).
- **Internal consistency:** D1 interface design matches D7 default-interface-members commitment. D2 UnavailableModuleView UX matches D9 NavigationService dispatch flow. D3 localization rewrite list matches Wave E scope. D4 CA1416 enforcement is sequenced after D5 Wave B (guards before flip). D9 NavigationService rewrite resolves the user-reported "dashboard fallback" symptom by replacing the hardcoded switch.
- **Scope check:** 8 sub-waves, ~6-7 days, single phase, single spec. Comparable to Phase 6.13 (UX Polish) and Phase 6.15 (Mobile Polish + Web Cleanup) in scope. No further decomposition needed.
- **Ambiguity check:** "Module not visible" vs "Module visible but unavailable" semantics are explicit per D2. "Wrong platform" → fully hidden. "Runtime issue" → visible with UnavailableModuleView. No ambiguity.
- **Defaults documented:** every D-decision records the choice and the rationale, so future Claude (or human reviewer) can understand why each path was taken.
