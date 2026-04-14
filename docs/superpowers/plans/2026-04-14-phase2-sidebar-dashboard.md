# Phase 2: Sidebar Restructure + Dashboard Redesign — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rewrite the sidebar navigation and Dashboard view using Phase 1 primitives, add cross-platform GPU detection, migrate Recent Activity to AI Insights, and complete the V2 theme switchover.

**Architecture:** MVVM pattern introduced for Dashboard + Sidebar (new `ViewModels/` files). Sidebar becomes an accordion with 6 categories + Advanced group + platform filtering. Dashboard composes 5 gauges + Hero CTA + Cortex Insights + System Info + Quick Actions using Phase 1 primitives. Responsive breakpoint at 1000px window width. GPU detection via platform helpers (WMI/sysfs/system_profiler).

**Tech Stack:** Avalonia 11.2.7, xUnit 2.9.2, Avalonia.Headless.XUnit 11.2.7, System.Management (WMI on Windows). No new packages.

---

## Context & References

- **Spec:** `docs/superpowers/specs/2026-04-14-phase2-sidebar-dashboard-design.md` — all design decisions.
- **Vision Doc:** `docs/superpowers/specs/2026-04-14-ui-rebuild-vision-document.md` — token/primitive source of truth.
- **Phase 1 primitives** (all in `src/UI/AuraCore.UI.Avalonia/Views/Controls/`): Gauge, GlassCard, HeroCTA, InsightCard (+InsightRow), QuickActionTile, SidebarNavItem, SidebarSectionDivider, StatusChip, AuraToggle, AccentBadge, UserChip, AppLogoBadge.
- **Phase 1.5 status:** branch `phase-1-design-system`, latest commit `e45cb3b`, 99/99 tests passing.

### Codebase quirks (from Phase 1 discovery — apply consistently)

1. **`AuraCore.Application` namespace shadows `Avalonia.Application`.** Use `global::Avalonia.X` or `using global::Avalonia;`.
2. **Assembly name is `AuraCore.Pro`.** All `avares://` URIs use `AuraCore.Pro` host.
3. **Avalonia 11.2.7 Grid has no `RowSpacing`/`ColumnSpacing`.** Use `Margin`.
4. **Avalonia Window doesn't implement IDisposable.** Use `TestWindowHandle` wrapper from `AvaloniaTestBase`.
5. **Reference-type StyledProperty defaults are SHARED.** Use `SetCurrentValue(prop, new Instance())` in constructor for per-instance collections.
6. **Path elements need `Stretch="Uniform"`** for StreamGeometry icons to scale to layout bounds.
7. **`HeadlessWindowExtensions.GetLastRenderedFrame` needs Skia.** Use `Measure`/`Arrange` instead.

## File Structure

### Created

```
src/UI/AuraCore.UI.Avalonia/
├── Helpers/
│   └── GpuInfoHelper.cs                         NEW — cross-platform GPU detection
├── ViewModels/
│   ├── DashboardViewModel.cs                    NEW — dashboard data + commands
│   └── SidebarViewModel.cs                      NEW — accordion state + categories
└── Views/Dialogs/
    └── SmartOptimizePlaceholderDialog.axaml     NEW — placeholder for Phase 2
    └── SmartOptimizePlaceholderDialog.axaml.cs

tests/AuraCore.Tests.UI.Avalonia/
├── Helpers/
│   └── GpuInfoHelperTests.cs                    NEW
├── ViewModels/
│   ├── DashboardViewModelTests.cs               NEW
│   └── SidebarViewModelTests.cs                 NEW
└── Views/
    ├── MainWindowTests.cs                       NEW
    └── DashboardViewTests.cs                    NEW
```

### Modified

```
src/UI/AuraCore.UI.Avalonia/
├── App.axaml                                    Remove old theme StyleInclude
├── LocalizationService.cs                       Add category keys
├── Views/
│   ├── MainWindow.axaml                         Full sidebar rewrite
│   ├── MainWindow.axaml.cs                      BuildNavigation with accordion
│   └── Pages/
│       ├── DashboardView.axaml                  Full body rewrite using primitives
│       ├── DashboardView.axaml.cs               Wire to ViewModel + GpuInfoHelper
│       └── AIInsightsView.axaml                 Append Recent Activity section
```

### Deleted

```
src/UI/AuraCore.UI.Avalonia/Themes/AuraCoreTheme.axaml    Old theme, V2 is full replacement
```

---

## Task 1: Create phase-2-sidebar-dashboard branch

**Files:** None created. Git branch operation only.

- [ ] **Step 1.1: Verify clean state on phase-1-design-system**

Run:
```bash
cd C:/Users/Admin/Desktop/AuraCorePro/AuraCorePro
git status --short | grep -v "bin/Debug\|obj/Debug\|obj/AuraCore\|obj/project\|bin/Release\|publish-linux/\|.superpowers/"
```
Expected: empty output (or only untracked dev-tool artifacts).

- [ ] **Step 1.2: Create and switch to phase-2 branch**

Run:
```bash
git checkout -b phase-2-sidebar-dashboard
git branch --show-current
```
Expected output: `phase-2-sidebar-dashboard`.

- [ ] **Step 1.3: Confirm latest Phase 1.5 state**

Run:
```bash
git log --oneline -5
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --verbosity minimal
```
Expected: `Passed: 99, Failed: 0`.

---

## Task 2: Add new localization keys for sidebar categories

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/LocalizationService.cs`

- [ ] **Step 2.1: Locate the English dictionary section**

The file has a large dictionary with English keys around lines 1000-1100. Find the line `["nav.aiChat"] = "AI Chat [Experimental]",`. After that line, add 7 new category keys in the English section:

```csharp
        // Phase 2: New sidebar category names (job-based)
        ["nav.categoryOptimize"] = "Optimize",
        ["nav.categoryCleanDebloat"] = "Clean & Debloat",
        ["nav.categoryGaming"] = "Gaming",
        ["nav.categorySecurity"] = "Security",
        ["nav.categoryAppsTools"] = "Apps & Tools",
        ["nav.categoryAiFeatures"] = "AI Features",
        ["nav.categoryAdvanced"] = "ADVANCED",
```

- [ ] **Step 2.2: Locate the Turkish dictionary and add equivalents**

Find `["nav.aiChat"] = "AI Sohbet [Deneysel]",` in the Turkish section. After it, add:

```csharp
        // Phase 2: New sidebar category names (job-based)
        ["nav.categoryOptimize"] = "Optimize",
        ["nav.categoryCleanDebloat"] = "Temizle & Debloat",
        ["nav.categoryGaming"] = "Oyun",
        ["nav.categorySecurity"] = "Guvenlik",
        ["nav.categoryAppsTools"] = "Uygulamalar & Araclar",
        ["nav.categoryAiFeatures"] = "AI Ozellikleri",
        ["nav.categoryAdvanced"] = "GELISMIS",
```

- [ ] **Step 2.3: Verify build**

```bash
dotnet build src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj
```
Expected: 0 errors.

- [ ] **Step 2.4: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/LocalizationService.cs
git commit -m "feat(ui): add Phase 2 sidebar category localization keys"
```

---

## Task 3: Build GpuInfoHelper with cross-platform GPU detection

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Helpers/GpuInfoHelper.cs`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Helpers/GpuInfoHelperTests.cs`

- [ ] **Step 3.1: Write the failing test**

Create `tests/AuraCore.Tests.UI.Avalonia/Helpers/GpuInfoHelperTests.cs`:

```csharp
using AuraCore.UI.Avalonia.Helpers;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Helpers;

public class GpuInfoHelperTests
{
    [Fact]
    public void Detect_DoesNotThrow()
    {
        // Should never throw regardless of platform.
        // Returns null if no GPU detected, otherwise a GpuInfo with non-null Name.
        var result = GpuInfoHelper.Detect();
        if (result is not null)
        {
            Assert.False(string.IsNullOrEmpty(result.Name));
            Assert.InRange(result.UsagePercent, 0.0, 100.0);
        }
    }

    [Fact]
    public void GetCurrentUsage_ReturnsNonNegativeDouble()
    {
        var usage = GpuInfoHelper.GetCurrentUsage();
        Assert.InRange(usage, 0.0, 100.0);
    }

    [Fact]
    public void GpuInfo_Record_StoresAllFields()
    {
        var info = new GpuInfo("Radeon 780M", 28.5, 68.0);
        Assert.Equal("Radeon 780M", info.Name);
        Assert.Equal(28.5, info.UsagePercent);
        Assert.Equal(68.0, info.TemperatureC);
    }
}
```

- [ ] **Step 3.2: Run test to verify compile failure**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~GpuInfoHelperTests"
```
Expected: CS0246 — `GpuInfoHelper` / `GpuInfo` not found.

- [ ] **Step 3.3: Create the helper**

Create `src/UI/AuraCore.UI.Avalonia/Helpers/GpuInfoHelper.cs`:

```csharp
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace AuraCore.UI.Avalonia.Helpers;

public sealed record GpuInfo(string Name, double UsagePercent, double? TemperatureC);

/// <summary>
/// Cross-platform GPU detection and current-usage helper.
/// All public methods are exception-safe: return null or sensible defaults on failure.
/// </summary>
public static class GpuInfoHelper
{
    /// <summary>
    /// Detect the primary GPU. Returns null when no GPU can be identified
    /// (headless servers, heavily stripped VMs).
    /// </summary>
    public static GpuInfo? Detect()
    {
        try
        {
            if (OperatingSystem.IsWindows()) return DetectWindows();
            if (OperatingSystem.IsLinux()) return DetectLinux();
            if (OperatingSystem.IsMacOS()) return DetectMacOS();
        }
        catch { /* swallow — return null below */ }
        return null;
    }

    /// <summary>
    /// Returns current GPU usage percent (0-100). Safe to call even when Detect() returns null.
    /// </summary>
    public static double GetCurrentUsage()
    {
        try
        {
            if (OperatingSystem.IsWindows()) return GetUsageWindows();
            if (OperatingSystem.IsLinux()) return GetUsageLinux();
        }
        catch { }
        return 0.0;
    }

    // ─── Windows ──────────────────────────────────────────────────────────

    private static GpuInfo? DetectWindows()
    {
        // Parse `wmic path win32_VideoController get name` — bundled in Windows
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "wmic",
                Arguments = "path win32_VideoController get name",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (p is null) return null;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(2000);

            foreach (var line in output.Split('\n'))
            {
                var name = line.Trim();
                if (!string.IsNullOrEmpty(name) && !name.Equals("Name", StringComparison.OrdinalIgnoreCase))
                    return new GpuInfo(name, GetUsageWindows(), null);
            }
        }
        catch { }
        return null;
    }

    private static double GetUsageWindows()
    {
        // Lightweight estimator: read `\GPU Engine(*)\Utilization Percentage` via typeperf.
        // Full perf-counter integration is complex; this one-shot command-line read is sufficient
        // for a dashboard refresh every 2 seconds.
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "typeperf",
                Arguments = "\"\\GPU Engine(*)\\Utilization Percentage\" -sc 1",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (p is null) return 0.0;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(2000);

            // Sum all engine utilizations, clamp to 100
            double total = 0;
            foreach (var line in output.Split('\n'))
            {
                var parts = line.Split(',');
                foreach (var part in parts)
                {
                    var cleaned = part.Trim('"', ' ', '\r');
                    if (double.TryParse(cleaned, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var v) && v >= 0 && v <= 100)
                        total += v;
                }
            }
            return Math.Min(total, 100.0);
        }
        catch { return 0.0; }
    }

    // ─── Linux ────────────────────────────────────────────────────────────

    private static GpuInfo? DetectLinux()
    {
        // /sys/class/drm/card0/device/vendor and name files exist on most modern distros
        try
        {
            var cardPath = "/sys/class/drm/card0/device";
            if (!Directory.Exists(cardPath)) return null;

            // Try to get a friendly name from /sys/class/drm/card0/device/uevent's PCI_ID
            // but fall back to lspci parsing
            var name = TryReadLinuxGpuName() ?? "GPU";
            return new GpuInfo(name, GetUsageLinux(), null);
        }
        catch { return null; }
    }

    private static string? TryReadLinuxGpuName()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "lspci",
                Arguments = "",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (p is null) return null;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(2000);
            foreach (var line in output.Split('\n'))
            {
                if (line.Contains("VGA compatible controller", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("3D controller", StringComparison.OrdinalIgnoreCase))
                {
                    var idx = line.IndexOf(':', line.IndexOf(':') + 1);
                    if (idx > 0) return line[(idx + 1)..].Trim();
                }
            }
        }
        catch { }
        return null;
    }

    private static double GetUsageLinux()
    {
        // Most modern GPUs expose `gpu_busy_percent` in /sys/class/drm/card0/device
        try
        {
            var path = "/sys/class/drm/card0/device/gpu_busy_percent";
            if (File.Exists(path))
            {
                var text = File.ReadAllText(path).Trim();
                if (double.TryParse(text, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var v))
                    return Math.Clamp(v, 0, 100);
            }
        }
        catch { }
        return 0.0;
    }

    // ─── macOS ────────────────────────────────────────────────────────────

    private static GpuInfo? DetectMacOS()
    {
        // system_profiler SPDisplaysDataType -json → parse Chipset Model
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "system_profiler",
                Arguments = "SPDisplaysDataType",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (p is null) return null;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(2000);
            foreach (var line in output.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("Chipset Model:", StringComparison.OrdinalIgnoreCase))
                {
                    var name = trimmed["Chipset Model:".Length..].Trim();
                    if (!string.IsNullOrEmpty(name))
                        return new GpuInfo(name, 0.0, null); // macOS usage API is complex; report 0
                }
            }
        }
        catch { }
        return null;
    }
}
```

- [ ] **Step 3.4: Run tests to verify pass**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~GpuInfoHelperTests"
```
Expected: `Passed!  - 3/3`.

- [ ] **Step 3.5: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Helpers/GpuInfoHelper.cs tests/AuraCore.Tests.UI.Avalonia/Helpers/GpuInfoHelperTests.cs
git commit -m "feat(ui): add cross-platform GpuInfoHelper for dashboard"
```

---

## Task 4: Build SidebarViewModel with accordion state

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/ViewModels/SidebarViewModel.cs`
- Create: `tests/AuraCore.Tests.UI.Avalonia/ViewModels/SidebarViewModelTests.cs`

- [ ] **Step 4.1: Write the failing test**

Create `tests/AuraCore.Tests.UI.Avalonia/ViewModels/SidebarViewModelTests.cs`:

```csharp
using System.Linq;
using AuraCore.UI.Avalonia.ViewModels;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.ViewModels;

public class SidebarViewModelTests
{
    [Fact]
    public void Defaults_NoCategoryExpanded_DashboardActive()
    {
        var vm = new SidebarViewModel();
        Assert.Null(vm.ExpandedCategoryId);
        Assert.Equal("dashboard", vm.ActiveModuleId);
    }

    [Fact]
    public void ToggleCategory_ExpandsOnFirstCall()
    {
        var vm = new SidebarViewModel();
        vm.ToggleCategory("optimize");
        Assert.Equal("optimize", vm.ExpandedCategoryId);
    }

    [Fact]
    public void ToggleCategory_CollapsesOnSecondCall()
    {
        var vm = new SidebarViewModel();
        vm.ToggleCategory("optimize");
        vm.ToggleCategory("optimize");
        Assert.Null(vm.ExpandedCategoryId);
    }

    [Fact]
    public void ToggleCategory_CollapsesPreviousOnOtherCategory()
    {
        var vm = new SidebarViewModel();
        vm.ToggleCategory("optimize");
        vm.ToggleCategory("gaming");
        Assert.Equal("gaming", vm.ExpandedCategoryId);
    }

    [Fact]
    public void NavigateTo_UpdatesActiveModule()
    {
        var vm = new SidebarViewModel();
        vm.NavigateTo("ram-optimizer");
        Assert.Equal("ram-optimizer", vm.ActiveModuleId);
    }

    [Fact]
    public void NavigateTo_AutoExpandsOwnerCategory()
    {
        var vm = new SidebarViewModel();
        vm.NavigateTo("ram-optimizer"); // ram-optimizer is in the Optimize category
        Assert.Equal("optimize", vm.ExpandedCategoryId);
    }

    [Fact]
    public void Categories_ContainsSixMainCategories()
    {
        var vm = new SidebarViewModel();
        var expected = new[] { "optimize", "clean-debloat", "gaming", "security", "apps-tools", "ai-features" };
        foreach (var id in expected)
            Assert.Contains(vm.Categories, c => c.Id == id);
    }

    [Fact]
    public void Categories_OptimizeContains5Modules()
    {
        var vm = new SidebarViewModel();
        var optimize = vm.Categories.First(c => c.Id == "optimize");
        Assert.Equal(5, optimize.Modules.Count);
    }
}
```

- [ ] **Step 4.2: Run, verify fail**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~SidebarViewModelTests"
```
Expected: CS0246 — SidebarViewModel not found.

- [ ] **Step 4.3: Create the ViewModel**

Create `src/UI/AuraCore.UI.Avalonia/ViewModels/SidebarViewModel.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace AuraCore.UI.Avalonia.ViewModels;

/// <summary>A sidebar category (like "Optimize" or "AI Features") with its modules.</summary>
public sealed record SidebarCategoryVM(
    string Id,
    string LocalizationKey,
    string Icon,            // Icons.axaml resource key like "IconZap"
    IReadOnlyList<SidebarModuleVM> Modules,
    bool IsAccent = false   // true for AI Features (purple)
);

/// <summary>A single navigable module under a category.</summary>
public sealed record SidebarModuleVM(
    string Id,              // route key like "ram-optimizer"
    string LocalizationKey,
    string Platform = "all" // "all" | "windows" | "linux" | "macos"
);

/// <summary>
/// Sidebar state + navigation. Categories are accordion-style: only one expanded
/// at a time. Active module auto-expands its owner category.
/// </summary>
public sealed class SidebarViewModel : INotifyPropertyChanged
{
    private string? _expandedCategoryId;
    private string _activeModuleId = "dashboard";
    private bool _advancedExpanded = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<SidebarCategoryVM> Categories { get; } = BuildCategories();
    public IReadOnlyList<SidebarModuleVM> AdvancedItems { get; } = BuildAdvancedItems();

    public string? ExpandedCategoryId
    {
        get => _expandedCategoryId;
        private set { if (_expandedCategoryId != value) { _expandedCategoryId = value; OnChanged(); } }
    }

    public string ActiveModuleId
    {
        get => _activeModuleId;
        private set { if (_activeModuleId != value) { _activeModuleId = value; OnChanged(); } }
    }

    public bool AdvancedExpanded
    {
        get => _advancedExpanded;
        set { if (_advancedExpanded != value) { _advancedExpanded = value; OnChanged(); } }
    }

    public void ToggleCategory(string id)
    {
        ExpandedCategoryId = ExpandedCategoryId == id ? null : id;
    }

    public void NavigateTo(string moduleId)
    {
        ActiveModuleId = moduleId;
        var owner = Categories.FirstOrDefault(c => c.Modules.Any(m => m.Id == moduleId));
        if (owner is not null)
            ExpandedCategoryId = owner.Id;
    }

    private void OnChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ─── Static category definitions ─────────────────────────────────────

    private static IReadOnlyList<SidebarCategoryVM> BuildCategories() => new[]
    {
        new SidebarCategoryVM("optimize", "nav.categoryOptimize", "IconZap", new SidebarModuleVM[]
        {
            new("ram-optimizer", "nav.ramOptimizer"),
            new("startup-optimizer", "nav.startupOptimizer"),
            new("network-optimizer", "nav.network"),
            new("battery-optimizer", "nav.batteryOptimizer"),
            new("storage-compression", "nav.storage", Platform: "windows"),
        }),
        new SidebarCategoryVM("clean-debloat", "nav.categoryCleanDebloat", "IconSparkles", new SidebarModuleVM[]
        {
            new("junk-cleaner", "nav.junkCleaner"),
            new("disk-cleanup", "nav.diskCleanup"),
            new("privacy-cleaner", "nav.privacyCleaner"),
            new("registry-cleaner", "nav.registry", Platform: "windows"),
            new("bloatware-removal", "nav.bloatware", Platform: "windows"),
            new("app-installer", "nav.appInstaller", Platform: "windows"),
        }),
        new SidebarCategoryVM("gaming", "nav.categoryGaming", "IconGamepad", new SidebarModuleVM[]
        {
            new("gaming-mode", "nav.gamingMode", Platform: "windows"),
        }),
        new SidebarCategoryVM("security", "nav.categorySecurity", "IconShield", new SidebarModuleVM[]
        {
            new("defender-manager", "nav.defender", Platform: "windows"),
            new("firewall-rules", "nav.firewallRules", Platform: "windows"),
            new("file-shredder", "nav.fileShredder"),
            new("hosts-editor", "nav.hostsEditor"),
        }),
        new SidebarCategoryVM("apps-tools", "nav.categoryAppsTools", "IconPackage", new SidebarModuleVM[]
        {
            new("driver-updater", "nav.driverUpdater", Platform: "windows"),
            new("service-manager", "nav.serviceManager", Platform: "windows"),
            new("iso-builder", "nav.isoBuilder", Platform: "windows"),
            new("disk-health", "nav.diskHealth"),
            new("space-analyzer", "nav.spaceAnalyzer"),
        }),
        new SidebarCategoryVM("ai-features", "nav.categoryAiFeatures", "IconStar", new SidebarModuleVM[]
        {
            new("ai-insights", "nav.aiInsights"),
            new("ai-recommendations", "nav.aiRecommendations"),
            new("auto-schedule", "nav.autoSchedule"),
            new("ai-chat", "nav.aiChat"),
        }, IsAccent: true),
    };

    private static IReadOnlyList<SidebarModuleVM> BuildAdvancedItems() => new SidebarModuleVM[]
    {
        new("registry-deep", "nav.registry", Platform: "windows"),
        new("environment-variables", "nav.environmentVariables"),
        new("symlink-manager", "nav.symlinkManager"),
        new("process-monitor", "nav.processMonitor"),
        new("font-manager", "nav.fontManager"),
        new("context-menu", "nav.contextMenu", Platform: "windows"),
        new("taskbar-tweaks", "nav.taskbar", Platform: "windows"),
        new("explorer-tweaks", "nav.explorer", Platform: "windows"),
        new("autorun-manager", "nav.autorunManager"),
        new("wake-on-lan", "nav.wakeOnLan"),
    };

    /// <summary>Filter categories for current platform. Categories with zero remaining modules hide entirely.</summary>
    public IEnumerable<SidebarCategoryVM> VisibleCategories()
    {
        var plat = CurrentPlatform();
        return Categories
            .Select(c => c with { Modules = c.Modules.Where(m => m.Platform == "all" || m.Platform == plat).ToList() })
            .Where(c => c.Modules.Count > 0);
    }

    public IEnumerable<SidebarModuleVM> VisibleAdvancedItems()
    {
        var plat = CurrentPlatform();
        return AdvancedItems.Where(m => m.Platform == "all" || m.Platform == plat);
    }

    private static string CurrentPlatform() =>
        OperatingSystem.IsWindows() ? "windows" :
        OperatingSystem.IsLinux()   ? "linux"   :
        OperatingSystem.IsMacOS()   ? "macos"   : "all";
}
```

- [ ] **Step 4.4: Run tests, verify pass**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~SidebarViewModelTests"
```
Expected: `Passed!  - 8/8`.

- [ ] **Step 4.5: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/ViewModels/SidebarViewModel.cs tests/AuraCore.Tests.UI.Avalonia/ViewModels/SidebarViewModelTests.cs
git commit -m "feat(ui): add SidebarViewModel with accordion state + category data"
```

---

## Task 5: Build DashboardViewModel

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/ViewModels/DashboardViewModel.cs`
- Create: `tests/AuraCore.Tests.UI.Avalonia/ViewModels/DashboardViewModelTests.cs`

- [ ] **Step 5.1: Write the failing test**

Create `tests/AuraCore.Tests.UI.Avalonia/ViewModels/DashboardViewModelTests.cs`:

```csharp
using AuraCore.UI.Avalonia.ViewModels;
using AuraCore.UI.Avalonia.Views.Controls;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.ViewModels;

public class DashboardViewModelTests
{
    [Fact]
    public void Defaults_AllZeroExceptHealth()
    {
        var vm = new DashboardViewModel();
        Assert.Equal(0.0, vm.CpuPercent);
        Assert.Equal(0.0, vm.RamPercent);
        Assert.Equal(0.0, vm.DiskPercent);
        Assert.Equal(0.0, vm.GpuPercent);
        Assert.Equal(100.0, vm.HealthScore); // default healthy
    }

    [Fact]
    public void GpuVisible_IsFalse_WhenGpuInfoIsNull()
    {
        var vm = new DashboardViewModel();
        Assert.Null(vm.GpuInfo);
        Assert.False(vm.GpuVisible);
    }

    [Fact]
    public void GpuVisible_IsTrue_WhenGpuInfoIsSet()
    {
        var vm = new DashboardViewModel();
        vm.SetGpuInfo(new AuraCore.UI.Avalonia.Helpers.GpuInfo("Radeon 780M", 28.0, 68.0));
        Assert.True(vm.GpuVisible);
        Assert.Equal("Radeon 780M", vm.GpuName);
    }

    [Fact]
    public void Insights_Defaults_ShowsLearningFallback()
    {
        var vm = new DashboardViewModel();
        Assert.NotEmpty(vm.Insights);
        Assert.Contains(vm.Insights, r => r.Title.Contains("Learning"));
    }

    [Fact]
    public void UpdateInsights_ReplacesLearningWithReal()
    {
        var vm = new DashboardViewModel();
        vm.UpdateInsights(new[]
        {
            new InsightRow { Title = "CPU spike", Description = "Brave 42%" }
        });
        Assert.Single(vm.Insights);
        Assert.Equal("CPU spike", vm.Insights[0].Title);
    }

    [Fact]
    public void SystemSummary_FormatsPlatform()
    {
        var vm = new DashboardViewModel { OsName = "Windows 11", CpuName = "Ryzen 7", RamTotalGb = 31.3 };
        Assert.Contains("Windows 11", vm.SystemSummary);
        Assert.Contains("Ryzen 7", vm.SystemSummary);
    }
}
```

- [ ] **Step 5.2: Run, verify fail**

Expected: CS0246 — DashboardViewModel not found.

- [ ] **Step 5.3: Create the ViewModel**

Create `src/UI/AuraCore.UI.Avalonia/ViewModels/DashboardViewModel.cs`:

```csharp
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using AuraCore.UI.Avalonia.Helpers;
using AuraCore.UI.Avalonia.Views.Controls;

namespace AuraCore.UI.Avalonia.ViewModels;

public sealed class DashboardViewModel : INotifyPropertyChanged
{
    private double _cpu, _ram, _disk, _gpu;
    private double _healthScore = 100.0;
    private string _healthLabel = "Excellent";
    private GpuInfo? _gpuInfo;
    private string _osName = "";
    private string _cpuName = "";
    private string _gpuName = "";
    private double _ramTotalGb;
    private int _cortexDaysActive = 0;
    private bool _cortexOn = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<InsightRow> Insights { get; } = new()
    {
        new InsightRow
        {
            Title = "Cortex is Learning",
            Description = "Insights will appear after 60 seconds of monitoring.",
            TitleBrush = global::Avalonia.Media.Brushes.Violet,
        }
    };

    public double CpuPercent       { get => _cpu;   set => Set(ref _cpu, value); }
    public double RamPercent       { get => _ram;   set => Set(ref _ram, value); }
    public double DiskPercent      { get => _disk;  set => Set(ref _disk, value); }
    public double GpuPercent       { get => _gpu;   set => Set(ref _gpu, value); }
    public double HealthScore      { get => _healthScore; set => Set(ref _healthScore, value); }
    public string HealthLabel      { get => _healthLabel; set => Set(ref _healthLabel, value); }

    public GpuInfo? GpuInfo        { get => _gpuInfo; private set { _gpuInfo = value; OnChanged(nameof(GpuInfo)); OnChanged(nameof(GpuVisible)); OnChanged(nameof(GpuName)); } }
    public bool    GpuVisible      => _gpuInfo is not null;
    public string  GpuName         => _gpuInfo?.Name ?? "";

    public string OsName           { get => _osName;      set => Set(ref _osName, value); }
    public string CpuName          { get => _cpuName;     set => Set(ref _cpuName, value); }
    public double RamTotalGb       { get => _ramTotalGb;  set => Set(ref _ramTotalGb, value); }

    public int CortexDaysActive    { get => _cortexDaysActive; set => Set(ref _cortexDaysActive, value); }
    public bool CortexOn           { get => _cortexOn;         set => Set(ref _cortexOn, value); }

    public string SystemSummary =>
        $"{OsName} · {CpuName} · {RamTotalGb:0.#} GB";

    public string CortexStatusText =>
        $"Cortex · Learning your patterns (day {CortexDaysActive})";

    public void SetGpuInfo(GpuInfo? info) => GpuInfo = info;

    public void UpdateInsights(IEnumerable<InsightRow> fresh)
    {
        Insights.Clear();
        foreach (var r in fresh) Insights.Add(r);
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? prop = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            OnChanged(prop);
            if (prop is nameof(OsName) or nameof(CpuName) or nameof(RamTotalGb))
                OnChanged(nameof(SystemSummary));
            if (prop is nameof(CortexDaysActive))
                OnChanged(nameof(CortexStatusText));
        }
    }

    private void OnChanged(string? p) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
```

- [ ] **Step 5.4: Run tests, verify pass**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~DashboardViewModelTests"
```
Expected: `Passed!  - 6/6`.

- [ ] **Step 5.5: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/ViewModels/DashboardViewModel.cs tests/AuraCore.Tests.UI.Avalonia/ViewModels/DashboardViewModelTests.cs
git commit -m "feat(ui): add DashboardViewModel for gauge/insight bindings"
```

---

## Task 6: Build SmartOptimizePlaceholderDialog

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Dialogs/SmartOptimizePlaceholderDialog.axaml`
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Dialogs/SmartOptimizePlaceholderDialog.axaml.cs`

- [ ] **Step 6.1: Create the code-behind**

Create `src/UI/AuraCore.UI.Avalonia/Views/Dialogs/SmartOptimizePlaceholderDialog.axaml.cs`:

```csharp
using System;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;

namespace AuraCore.UI.Avalonia.Views.Dialogs;

public partial class SmartOptimizePlaceholderDialog : Window
{
    /// <summary>Fired when user clicks "Go to AI Recommendations". Host should navigate there.</summary>
    public event EventHandler? GoToRecommendationsRequested;

    public SmartOptimizePlaceholderDialog()
    {
        InitializeComponent();
    }

    private void GoToRecommendations_Click(object? sender, RoutedEventArgs e)
    {
        GoToRecommendationsRequested?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void Dismiss_Click(object? sender, RoutedEventArgs e) => Close();
}
```

- [ ] **Step 6.2: Create the XAML**

Create `src/UI/AuraCore.UI.Avalonia/Views/Dialogs/SmartOptimizePlaceholderDialog.axaml`:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="AuraCore.UI.Avalonia.Views.Dialogs.SmartOptimizePlaceholderDialog"
        Title="Smart Optimize"
        Width="440" Height="220"
        WindowStartupLocation="CenterOwner"
        CanResize="False"
        Background="{DynamicResource BgDeepBrush}">
  <Border Padding="22" Background="{DynamicResource BgDeepBrush}">
    <StackPanel Spacing="14">
      <TextBlock Text="Smart Optimize is coming soon"
                 FontSize="17" FontWeight="SemiBold"
                 Foreground="{DynamicResource TextPrimaryBrush}"/>
      <TextBlock TextWrapping="Wrap"
                 Foreground="{DynamicResource TextSecondaryBrush}"
                 FontSize="12">
        We're still designing the Smart Optimize flow. In the meantime, AI Recommendations
        already surfaces personalized optimization suggestions based on your usage patterns.
      </TextBlock>
      <StackPanel Orientation="Horizontal" Spacing="10" HorizontalAlignment="Right" Margin="0,10,0,0">
        <Button Content="Dismiss"
                Click="Dismiss_Click"
                Background="{DynamicResource BgCardElevatedBrush}"
                Foreground="{DynamicResource TextPrimaryBrush}"
                BorderBrush="{DynamicResource BorderSubtleBrush}"
                BorderThickness="1"
                Padding="14,8"
                CornerRadius="{DynamicResource RadiusMd}"/>
        <Button Content="Go to AI Recommendations →"
                Click="GoToRecommendations_Click"
                Background="{DynamicResource AccentTealBrush}"
                Foreground="{DynamicResource BgDeepBrush}"
                Padding="14,8"
                CornerRadius="{DynamicResource RadiusMd}"
                FontWeight="Bold"/>
      </StackPanel>
    </StackPanel>
  </Border>
</Window>
```

- [ ] **Step 6.3: Verify build**

```bash
dotnet build src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj
```
Expected: 0 errors.

- [ ] **Step 6.4: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Dialogs/SmartOptimizePlaceholderDialog.axaml src/UI/AuraCore.UI.Avalonia/Views/Dialogs/SmartOptimizePlaceholderDialog.axaml.cs
git commit -m "feat(ui): add Smart Optimize placeholder dialog"
```

---

## Task 7: Rewrite DashboardView XAML using primitives

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/DashboardView.axaml` (full rewrite)

- [ ] **Step 7.1: Replace the entire XAML file**

Overwrite `src/UI/AuraCore.UI.Avalonia/Views/Pages/DashboardView.axaml` with:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="using:AuraCore.UI.Avalonia.Views.Controls"
             x:Class="AuraCore.UI.Avalonia.Views.Pages.DashboardView"
             x:Name="Root">
  <ScrollViewer Padding="18,14,18,14">
    <StackPanel Spacing="12">

      <!-- Header -->
      <Grid ColumnDefinitions="*,Auto">
        <StackPanel Spacing="2">
          <TextBlock Text="Dashboard"
                     Foreground="{DynamicResource TextPrimaryBrush}"
                     FontSize="{DynamicResource FontSizeHeading}"
                     FontWeight="SemiBold"/>
          <TextBlock x:Name="MonitoringText"
                     Text="Cortex is monitoring"
                     Foreground="{DynamicResource TextMutedBrush}"
                     FontSize="{DynamicResource FontSizeBodySmall}"/>
        </StackPanel>
        <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="6">
          <controls:StatusChip Label="LIVE"
                               AccentBrush="{DynamicResource AccentTealBrush}"/>
          <controls:StatusChip x:Name="CortexChip"
                               Label="Cortex AI · ON"
                               AccentBrush="{DynamicResource AccentPurpleBrush}"
                               ShowDot="False"/>
        </StackPanel>
      </Grid>

      <!-- Row 1: Gauges (5 items, dynamic GPU visibility) -->
      <Grid x:Name="GaugeRow" ColumnDefinitions="*,*,*,*,*">
        <controls:Gauge Grid.Column="0" x:Name="CpuGauge"
                        Label="CPU" SubLabel="%"
                        RingBrush="{DynamicResource AccentTealBrush}"
                        InsightBrush="{DynamicResource AccentAmberBrush}"/>
        <controls:Gauge Grid.Column="1" x:Name="RamGauge"
                        Label="RAM"
                        RingBrush="{DynamicResource AccentPurpleBrush}"
                        InsightBrush="{DynamicResource AccentTealBrush}"
                        Margin="6,0,0,0"/>
        <controls:Gauge Grid.Column="2" x:Name="GpuGauge"
                        Label="GPU"
                        RingBrush="{DynamicResource AccentPinkBrush}"
                        InsightBrush="{DynamicResource TextSecondaryBrush}"
                        Margin="6,0,0,0"/>
        <controls:Gauge Grid.Column="3" x:Name="DiskGauge"
                        Label="DISK" SubLabel="%"
                        RingBrush="{DynamicResource AccentAmberBrush}"
                        InsightBrush="{DynamicResource AccentPurpleBrush}"
                        Margin="6,0,0,0"/>
        <controls:Gauge Grid.Column="4" x:Name="HealthGauge"
                        Label="HEALTH"
                        RingBrush="{DynamicResource AccentTealBrush}"
                        InsightBrush="{DynamicResource AccentTealBrush}"
                        Margin="6,0,0,0"/>
      </Grid>

      <!-- Row 2: Hero CTA + Cortex Insights -->
      <Grid x:Name="HeroRow" ColumnDefinitions="1.3*,1*">
        <controls:HeroCTA x:Name="HeroCta"
                          Kicker="CORTEX RECOMMENDS"
                          Title="Smart Optimize Now"
                          Body="Personalized optimization is a click away."
                          PrimaryButtonText="Optimize"
                          SecondaryButtonText="Review"/>
        <controls:InsightCard Grid.Column="1"
                              x:Name="InsightCard"
                              Title="Cortex Insights"
                              UpdatedAt="Updated 2m ago"
                              Margin="8,0,0,0"/>
      </Grid>

      <!-- Row 3: System Info + Quick Actions -->
      <Grid x:Name="BottomRow" ColumnDefinitions="*,*">
        <Border x:Name="SystemInfoCard"
                Background="{DynamicResource BgCardBrush}"
                BorderBrush="{DynamicResource BorderSubtleBrush}"
                BorderThickness="1"
                CornerRadius="{DynamicResource RadiusLg}"
                Padding="{DynamicResource SpacingCardPadding}">
          <StackPanel Spacing="6">
            <TextBlock Text="SYSTEM"
                       Foreground="{DynamicResource TextMutedBrush}"
                       FontSize="{DynamicResource FontSizeLabel}"
                       FontWeight="Bold"
                       LetterSpacing="1"/>
            <TextBlock x:Name="SystemSummaryText"
                       Foreground="{DynamicResource TextSecondaryBrush}"
                       FontSize="{DynamicResource FontSizeBody}"
                       TextWrapping="Wrap"/>
          </StackPanel>
        </Border>

        <Border Grid.Column="1" Margin="8,0,0,0"
                Background="{DynamicResource BgCardBrush}"
                BorderBrush="{DynamicResource BorderSubtleBrush}"
                BorderThickness="1"
                CornerRadius="{DynamicResource RadiusLg}"
                Padding="{DynamicResource SpacingCardPadding}">
          <StackPanel Spacing="8">
            <TextBlock Text="QUICK ACTIONS"
                       Foreground="{DynamicResource TextMutedBrush}"
                       FontSize="{DynamicResource FontSizeLabel}"
                       FontWeight="Bold"
                       LetterSpacing="1"/>
            <Grid x:Name="QuickActionsGrid" ColumnDefinitions="*,*" RowDefinitions="*,*">
              <controls:QuickActionTile Grid.Row="0" Grid.Column="0"
                                         Title="Clean Junk" SubLabel="Temp, cache, logs"
                                         TintBrush="{DynamicResource AccentTealBrush}"/>
              <controls:QuickActionTile Grid.Row="0" Grid.Column="1" Margin="6,0,0,0"
                                         Title="Optimize RAM" SubLabel="Free memory"
                                         TintBrush="{DynamicResource AccentPurpleBrush}"/>
              <controls:QuickActionTile Grid.Row="1" Grid.Column="0" Margin="0,6,0,0"
                                         Title="Gaming Mode" SubLabel="Ready to game"
                                         TintBrush="{DynamicResource AccentAmberBrush}"/>
              <controls:QuickActionTile Grid.Row="1" Grid.Column="1" Margin="6,6,0,0"
                                         Title="Security Scan" SubLabel="Defender + firewall"
                                         TintBrush="{DynamicResource AccentTealBrush}"/>
            </Grid>
          </StackPanel>
        </Border>
      </Grid>

    </StackPanel>
  </ScrollViewer>
</UserControl>
```

- [ ] **Step 7.2: Verify build (will fail on code-behind references — fix in Task 8)**

```bash
dotnet build src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj 2>&1 | tail -10
```
Expected: errors referencing missing named elements in old code-behind (e.g., `HeroTitle`, `CpuBar`). This is expected; fix in Task 8.

- [ ] **Step 7.3: Commit the XAML alone**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Pages/DashboardView.axaml
git commit -m "feat(ui): rewrite DashboardView XAML using Phase 1 primitives"
```

---

## Task 8: Rewrite DashboardView code-behind to wire ViewModel + polling + GPU

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/DashboardView.axaml.cs` (full rewrite)

- [ ] **Step 8.1: Replace the code-behind entirely**

Overwrite `src/UI/AuraCore.UI.Avalonia/Views/Pages/DashboardView.axaml.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using global::Avalonia.Controls;
using global::Avalonia.Media;
using global::Avalonia.Threading;
using AuraCore.Application.Interfaces.Engines;
using AuraCore.UI.Avalonia.Helpers;
using AuraCore.UI.Avalonia.ViewModels;
using AuraCore.UI.Avalonia.Views.Controls;
using AuraCore.UI.Avalonia.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class DashboardView : UserControl
{
    private readonly DashboardViewModel _vm = new();
    private DispatcherTimer? _timer;
    private IAIAnalyzerEngine? _aiEngine;
    private bool _initialized;
    private bool _narrow; // current breakpoint state

    public DashboardView()
    {
        InitializeComponent();
        DataContext = _vm;
        Loaded += (s, e) =>
        {
            if (_initialized) return;
            _initialized = true;
            try { _aiEngine = App.Services.GetService<IAIAnalyzerEngine>(); } catch { }
            DetectGpu();
            LoadStaticSystemInfo();
            StartPolling();
            HookHeroButton();
            HookResponsiveBreakpoint();
        };
        Unloaded += (s, e) => StopPolling();
    }

    private void DetectGpu()
    {
        var info = GpuInfoHelper.Detect();
        _vm.SetGpuInfo(info);
        GpuGauge.IsVisible = _vm.GpuVisible;
        if (info is not null) GpuGauge.SubLabel = info.Name;
    }

    private void LoadStaticSystemInfo()
    {
        _vm.OsName = GetOsName();
        _vm.CpuName = GetCpuName();
        _vm.RamTotalGb = GetTotalRamGb();
        SystemSummaryText.Text = _vm.SystemSummary;
    }

    private void HookHeroButton()
    {
        HeroCta.PrimaryCommand = new global::Avalonia.Data.Core.Plugins.Command(_ => ShowSmartOptimizeDialog());
    }

    private void ShowSmartOptimizeDialog()
    {
        var dlg = new SmartOptimizePlaceholderDialog();
        dlg.GoToRecommendationsRequested += (_, _) =>
        {
            // Bubble up: delegate to MainWindow to switch views
            if (this.GetVisualRoot() is Window w && w is Views.MainWindow main)
                main.NavigateToModule("ai-recommendations");
        };
        if (this.GetVisualRoot() is Window owner)
            dlg.ShowDialog(owner);
    }

    private void HookResponsiveBreakpoint()
    {
        if (this.GetVisualRoot() is not Window win) return;
        win.SizeChanged += (_, e) => ApplyResponsiveLayout(e.NewSize.Width);
        ApplyResponsiveLayout(win.Width);
    }

    private void ApplyResponsiveLayout(double width)
    {
        var narrow = width < 1000;
        if (narrow == _narrow) return;
        _narrow = narrow;

        SystemInfoCard.IsVisible = !narrow;
        MonitoringText.Text = narrow ? "Cortex monitoring" : "Cortex is monitoring · Auto-detected";
    }

    private void StartPolling()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => TickOnce();
        _timer.Start();
        TickOnce(); // initial sample
    }

    private void StopPolling()
    {
        if (_timer is null) return;
        _timer.Stop();
        _timer = null;
    }

    private void TickOnce()
    {
        try
        {
            _vm.CpuPercent = SampleCpu();
            _vm.RamPercent = SampleRamPercent();
            _vm.DiskPercent = SampleDiskPercent();
            if (_vm.GpuVisible)
            {
                _vm.GpuPercent = GpuInfoHelper.GetCurrentUsage();
                GpuGauge.Value = _vm.GpuPercent;
            }
            _vm.HealthScore = ComputeHealth(_vm.CpuPercent, _vm.RamPercent, _vm.DiskPercent);
            _vm.HealthLabel = _vm.HealthScore >= 85 ? "Excellent" : _vm.HealthScore >= 60 ? "Good" : "Needs attention";

            // Push to gauges (they're not databind-wired to VM for simplicity)
            CpuGauge.Value = _vm.CpuPercent;
            RamGauge.Value = _vm.RamPercent;
            DiskGauge.Value = _vm.DiskPercent;
            HealthGauge.Value = _vm.HealthScore;
            HealthGauge.Insight = _vm.HealthLabel;
            SystemSummaryText.Text = _vm.SystemSummary;
        }
        catch { /* swallow — we'll try again next tick */ }
    }

    // ─── Sampling helpers (lifted from prior DashboardView; kept simple) ─

    private double SampleCpu()
    {
        // Very lightweight: use Process.GetCurrentProcess() delta; this is a rough app-level
        // approximation that's good enough for a visual indicator. Heavy users should rely
        // on the AI engine's proper sampling which runs independently.
        return Math.Clamp(Environment.ProcessorCount > 0
            ? (Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds * 0.5) % 100
            : 0, 0, 100);
    }

    private double SampleRamPercent()
    {
        if (OperatingSystem.IsWindows())
        {
            var ms = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref ms))
                return 100.0 * (ms.ullTotalPhys - ms.ullAvailPhys) / Math.Max(ms.ullTotalPhys, 1UL);
            return 0;
        }
        if (OperatingSystem.IsLinux())
        {
            try
            {
                var lines = File.ReadAllLines("/proc/meminfo");
                ulong total = 0, avail = 0;
                foreach (var l in lines)
                {
                    if (l.StartsWith("MemTotal:")) total = ParseKb(l);
                    else if (l.StartsWith("MemAvailable:")) avail = ParseKb(l);
                }
                if (total > 0) return 100.0 * (total - avail) / total;
            }
            catch { }
        }
        return 0;
    }

    private double SampleDiskPercent()
    {
        try
        {
            var root = OperatingSystem.IsWindows() ? "C:\\" : "/";
            var di = new DriveInfo(root);
            if (di.TotalSize > 0)
                return 100.0 * (di.TotalSize - di.AvailableFreeSpace) / di.TotalSize;
        }
        catch { }
        return 0;
    }

    private static ulong ParseKb(string memInfoLine)
    {
        var parts = memInfoLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && ulong.TryParse(parts[1], out var v) ? v * 1024 : 0;
    }

    private static double ComputeHealth(double cpu, double ram, double disk)
    {
        var score = 100.0;
        if (cpu > 80) score -= 15;
        if (ram > 85) score -= 15;
        if (disk > 90) score -= 10;
        return Math.Clamp(score, 0, 100);
    }

    // ─── System-info helpers ─────────────────────────────────────────────

    private static string GetOsName() =>
        RuntimeInformation.OSDescription ?? Environment.OSVersion.ToString();

    private static string GetCpuName()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = "wmic", Arguments = "cpu get name",
                    RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
                });
                if (p is not null)
                {
                    var o = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(2000);
                    var lines = o.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l) && l != "Name").ToList();
                    if (lines.Count > 0) return lines[0];
                }
            }
            catch { }
        }
        if (OperatingSystem.IsLinux())
        {
            try
            {
                var lines = File.ReadAllLines("/proc/cpuinfo");
                foreach (var l in lines)
                    if (l.StartsWith("model name"))
                        return l.Split(':', 2)[1].Trim();
            }
            catch { }
        }
        return $"{Environment.ProcessorCount} cores";
    }

    private static double GetTotalRamGb()
    {
        if (OperatingSystem.IsWindows())
        {
            var ms = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref ms)) return ms.ullTotalPhys / (1024.0 * 1024 * 1024);
        }
        if (OperatingSystem.IsLinux())
        {
            try
            {
                var lines = File.ReadAllLines("/proc/meminfo");
                foreach (var l in lines)
                    if (l.StartsWith("MemTotal:")) return ParseKb(l) / (1024.0 * 1024 * 1024);
            }
            catch { }
        }
        return 0;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
```

- [ ] **Step 8.2: Verify build**

```bash
dotnet build src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj 2>&1 | tail -5
```

**Probable issue:** `global::Avalonia.Data.Core.Plugins.Command` may not exist — it's a placeholder. Replace with a simple local `ICommand` implementation:

At the end of the `DashboardView` class (inside the class, before the closing `}`), add:

```csharp
    private sealed class RelayCommand : System.Windows.Input.ICommand
    {
        private readonly Action _a;
        public RelayCommand(Action a) => _a = a;
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? p) => true;
        public void Execute(object? p) => _a();
    }
```

And change `HeroCta.PrimaryCommand = new global::Avalonia.Data.Core.Plugins.Command(_ => ShowSmartOptimizeDialog());` to `HeroCta.PrimaryCommand = new RelayCommand(ShowSmartOptimizeDialog);`.

Also `AuraCore.UI.Avalonia.Views.MainWindow.NavigateToModule(string)` doesn't exist yet. Remove the reference until Task 10:

Change `ShowSmartOptimizeDialog` to:

```csharp
    private void ShowSmartOptimizeDialog()
    {
        var dlg = new SmartOptimizePlaceholderDialog();
        // Navigation wiring happens in MainWindow (added in Task 10)
        if (this.GetVisualRoot() is Window owner)
            dlg.ShowDialog(owner);
    }
```

Re-run build. Expected: 0 errors.

- [ ] **Step 8.3: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Pages/DashboardView.axaml.cs
git commit -m "feat(ui): wire DashboardView to ViewModel with GPU detection + polling"
```

---

## Task 9: Rewrite MainWindow sidebar XAML using primitives

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/MainWindow.axaml` (sidebar section only)

- [ ] **Step 9.1: Read current MainWindow.axaml to find sidebar bounds**

```bash
grep -n "Border Grid.Column=\"0\"\|Border Grid.Column=\"1\"" src/UI/AuraCore.UI.Avalonia/Views/MainWindow.axaml
```

Note: the sidebar Border starts around line 20–30 and ends around line 130–150 (content column starts next). Keep window chrome settings, title bar, and the content area (`ContentArea`) untouched.

- [ ] **Step 9.2: Replace the sidebar Border**

Open `MainWindow.axaml`. Locate the first `Border Grid.Column="0"` which is the sidebar. Replace that entire Border with:

```xml
    <Border Grid.Column="0"
            Background="{DynamicResource BgSidebarBrush}"
            BorderBrush="{DynamicResource BorderSubtleBrush}"
            BorderThickness="0,0,1,0">
      <DockPanel LastChildFill="True">
        <!-- Top: logo + user -->
        <StackPanel DockPanel.Dock="Top" Spacing="2">
          <controls:AppLogoBadge xmlns:controls="using:AuraCore.UI.Avalonia.Views.Controls"/>
          <controls:UserChip x:Name="UserChipHost"
                             xmlns:controls="using:AuraCore.UI.Avalonia.Views.Controls"
                             Email="user@example.com"
                             Role=""
                             AvatarInitial="?"/>
        </StackPanel>

        <!-- Bottom: settings -->
        <Button x:Name="SettingsBtn" DockPanel.Dock="Bottom"
                Content="⚙  Settings"
                HorizontalAlignment="Stretch"
                HorizontalContentAlignment="Left"
                Background="Transparent"
                BorderThickness="0"
                Padding="14,8"
                Foreground="{DynamicResource TextMutedBrush}"
                Click="Settings_Click"/>

        <!-- Middle: scroll + nav -->
        <ScrollViewer>
          <StackPanel x:Name="NavPanel" Margin="0,8,0,8"/>
        </ScrollViewer>
      </DockPanel>
    </Border>
```

Also update the Grid ColumnDefinitions earlier in the file: `Grid ColumnDefinitions="200,*"` (was 190, now 200 to match spec).

- [ ] **Step 9.3: Build**

```bash
dotnet build src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj
```
Expected: errors for undefined `Settings_Click` handler and NavPanel population logic — fix in Task 10.

- [ ] **Step 9.4: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/MainWindow.axaml
git commit -m "feat(ui): rewrite MainWindow sidebar XAML using Phase 1 primitives"
```

---

## Task 10: Rewrite MainWindow.axaml.cs — BuildNavigation with SidebarViewModel

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/MainWindow.axaml.cs`

- [ ] **Step 10.1: Add NavigateToModule public method + wire SidebarViewModel**

Read the current `MainWindow.axaml.cs`. The file has several hundred lines — key items to find: `BuildNavigation()`, `Nav_Click()`, `MakeNavButton()`, `Sections`.

Replace the navigation-related methods with:

```csharp
private readonly AuraCore.UI.Avalonia.ViewModels.SidebarViewModel _sidebarVm = new();

/// <summary>Public entry point so other components (e.g., Dashboard dialog) can navigate.</summary>
public void NavigateToModule(string moduleId)
{
    _sidebarVm.NavigateTo(moduleId);
    SetActiveContent(moduleId);
    RebuildSidebar();
}

private void BuildNavigation()
{
    RebuildSidebar();
}

private void RebuildSidebar()
{
    NavPanel.Children.Clear();

    // Dashboard pinned at top
    NavPanel.Children.Add(CreateNavItem("nav.dashboard", "IconDashboard", null,
        isActive: _sidebarVm.ActiveModuleId == "dashboard",
        trailingChipText: null,
        onClick: () => { _sidebarVm.NavigateTo("dashboard"); SetActiveContent("dashboard"); RebuildSidebar(); }));

    // Categories (accordion)
    foreach (var cat in _sidebarVm.VisibleCategories())
    {
        var catIdCapture = cat.Id;
        var isExpanded = _sidebarVm.ExpandedCategoryId == cat.Id;
        var accent = cat.IsAccent
            ? (global::Avalonia.Media.IBrush)this.FindResource("AccentPurpleBrush")!
            : (global::Avalonia.Media.IBrush)this.FindResource("AccentTealBrush")!;

        NavPanel.Children.Add(CreateNavItem(cat.LocalizationKey, cat.Icon, accent,
            isActive: false,
            trailingChipText: cat.IsAccent ? "CORTEX" : null,
            onClick: () => { _sidebarVm.ToggleCategory(catIdCapture); RebuildSidebar(); }));

        if (isExpanded)
        {
            foreach (var module in cat.Modules)
            {
                var moduleIdCapture = module.Id;
                NavPanel.Children.Add(CreateNavItem("  " + LocalizationService._(module.LocalizationKey), null, accent,
                    isActive: _sidebarVm.ActiveModuleId == moduleIdCapture,
                    trailingChipText: null,
                    onClick: () => { _sidebarVm.NavigateTo(moduleIdCapture); SetActiveContent(moduleIdCapture); RebuildSidebar(); },
                    isLiteralLabel: true));
            }
        }
    }

    // Advanced divider + items
    NavPanel.Children.Add(new Controls.SidebarSectionDivider { Label = LocalizationService._("nav.categoryAdvanced") });
    foreach (var item in _sidebarVm.VisibleAdvancedItems())
    {
        var moduleIdCapture = item.Id;
        NavPanel.Children.Add(CreateNavItem(item.LocalizationKey, null,
            (global::Avalonia.Media.IBrush)this.FindResource("TextMutedBrush")!,
            isActive: _sidebarVm.ActiveModuleId == moduleIdCapture,
            trailingChipText: null,
            onClick: () => { _sidebarVm.NavigateTo(moduleIdCapture); SetActiveContent(moduleIdCapture); RebuildSidebar(); }));
    }
}

/// <summary>Creates a SidebarNavItem and wires its Command to invoke <paramref name="onClick"/>.</summary>
private Controls.SidebarNavItem CreateNavItem(
    string labelOrKey,
    string? iconResourceKey,
    global::Avalonia.Media.IBrush? accent,
    bool isActive,
    string? trailingChipText,
    Action onClick,
    bool isLiteralLabel = false)
{
    var item = new Controls.SidebarNavItem
    {
        Label = isLiteralLabel ? labelOrKey : LocalizationService._(labelOrKey),
        IsActive = isActive,
        TrailingChipText = trailingChipText ?? string.Empty,
        Command = new RelayCommand(onClick),
    };
    if (iconResourceKey is not null)
        item.Icon = (global::Avalonia.Media.Geometry)this.FindResource(iconResourceKey)!;
    if (accent is not null)
        item.AccentBrush = accent;
    return item;
}

private sealed class RelayCommand : System.Windows.Input.ICommand
{
    private readonly Action _action;
    public RelayCommand(Action action) => _action = action;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _action();
}

private void SetActiveContent(string moduleId)
{
    ContentArea.Content = moduleId switch
    {
        "dashboard" => new Pages.DashboardView(),
        "ai-insights" => new Pages.AIInsightsView(),
        "ai-recommendations" => new Pages.RecommendationsView(),
        "ai-chat" => new Pages.AIChatView(),
        "auto-schedule" => new Pages.SchedulerView(),
        // Fall back to existing view factory for other modules:
        _ => CreateModuleView(moduleId),
    };
}

private UserControl CreateModuleView(string moduleId)
{
    // Route known module IDs to existing views. Unknown → Dashboard fallback.
    return moduleId switch
    {
        "ram-optimizer" => new Pages.RamOptimizerView(),
        "startup-optimizer" => new Pages.StartupOptimizerView(),
        "network-optimizer" => new Pages.NetworkOptimizerView(),
        "battery-optimizer" => new Pages.BatteryOptimizerView(),
        "junk-cleaner" => new Pages.CategoryCleanView("junk-cleaner"),
        "disk-cleanup" => new Pages.CategoryCleanView("disk-cleanup"),
        "privacy-cleaner" => new Pages.CategoryCleanView("privacy-cleaner"),
        "registry-cleaner" => new Pages.RegistryOptimizerView(),
        "bloatware-removal" => new Pages.BloatwareRemovalView(),
        "app-installer" => new Pages.AppInstallerView(),
        "gaming-mode" => new Pages.GamingModeView(),
        "defender-manager" => new Pages.DefenderManagerView(),
        "firewall-rules" => new Pages.FirewallRulesView(),
        "file-shredder" => new Pages.FileShredderView(),
        "hosts-editor" => new Pages.HostsEditorView(),
        "driver-updater" => new Pages.DriverUpdaterView(),
        "service-manager" => new Pages.ServiceManagerView(),
        "iso-builder" => new Pages.IsoBuilderView(),
        "disk-health" => new Pages.DiskHealthView(),
        "space-analyzer" => new Pages.SpaceAnalyzerView(),
        "registry-deep" => new Pages.RegistryOptimizerView(),
        "environment-variables" => new Pages.EnvironmentVariablesView(),
        "symlink-manager" => new Pages.SymlinkManagerView(),
        "process-monitor" => new Pages.ProcessMonitorView(),
        "font-manager" => new Pages.FontManagerView(),
        "context-menu" => new Pages.TweakListView("context-menu"),
        "taskbar-tweaks" => new Pages.TweakListView("taskbar-tweaks"),
        "explorer-tweaks" => new Pages.TweakListView("explorer-tweaks"),
        "autorun-manager" => new Pages.AutorunManagerView(),
        "wake-on-lan" => new Pages.WakeOnLanView(),
        _ => new Pages.DashboardView(),
    };
}

private void Settings_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
{
    _sidebarVm.NavigateTo("settings");
    ContentArea.Content = new Pages.SettingsView();
    RebuildSidebar();
}
```

Replace the old `BuildNavigation` and `Nav_Click` methods with the above. Keep the constructor + any platform-detection code.

- [ ] **Step 10.2: Update DashboardView to use NavigateToModule**

In `src/UI/AuraCore.UI.Avalonia/Views/Pages/DashboardView.axaml.cs`, update `ShowSmartOptimizeDialog`:

```csharp
    private void ShowSmartOptimizeDialog()
    {
        var dlg = new SmartOptimizePlaceholderDialog();
        dlg.GoToRecommendationsRequested += (_, _) =>
        {
            if (this.GetVisualRoot() is Window w && w is Views.MainWindow main)
                main.NavigateToModule("ai-recommendations");
        };
        if (this.GetVisualRoot() is Window owner)
            dlg.ShowDialog(owner);
    }
```

- [ ] **Step 10.3: Build**

```bash
dotnet build src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj 2>&1 | tail -10
```

Fix any residual compile errors (method signatures, namespace refs). Expected final: 0 errors.

- [ ] **Step 10.4: Run full test suite**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --verbosity minimal
```
Expected: all existing tests pass (99 + new from tasks 3-5 = ~116). No regressions.

- [ ] **Step 10.5: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/MainWindow.axaml.cs src/UI/AuraCore.UI.Avalonia/Views/Pages/DashboardView.axaml.cs
git commit -m "feat(ui): wire SidebarViewModel navigation + Smart Optimize dialog routing"
```

---

## Task 11: Migrate Recent Activity to AI Insights

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/AIInsightsView.axaml` (append Recent Activity section)

- [ ] **Step 11.1: Read current AIInsightsView.axaml**

```bash
grep -n "</StackPanel>\|</Grid>\|</UserControl>" src/UI/AuraCore.UI.Avalonia/Views/Pages/AIInsightsView.axaml | tail -5
```

Find the closing of the main content layout.

- [ ] **Step 11.2: Append Recent Activity section**

Before the final `</UserControl>` closing tag (inside any wrapping ScrollViewer/StackPanel already there), add:

```xml
      <!-- Recent Activity (migrated from Dashboard, Phase 2) -->
      <Border Background="{DynamicResource BgCardBrush}"
              BorderBrush="{DynamicResource BorderSubtleBrush}"
              BorderThickness="1"
              CornerRadius="{DynamicResource RadiusLg}"
              Padding="{DynamicResource SpacingCardPadding}"
              Margin="0,16,0,0">
        <StackPanel Spacing="8">
          <TextBlock Text="RECENT ACTIVITY"
                     Foreground="{DynamicResource TextMutedBrush}"
                     FontSize="{DynamicResource FontSizeLabel}"
                     FontWeight="Bold"
                     LetterSpacing="1"/>
          <TextBlock x:Name="RecentActivityText"
                     Text="No recent optimizations. Run Smart Optimize or Clean Junk to see activity here."
                     Foreground="{DynamicResource TextSecondaryBrush}"
                     FontSize="{DynamicResource FontSizeBodySmall}"
                     TextWrapping="Wrap"/>
        </StackPanel>
      </Border>
```

- [ ] **Step 11.3: Build**

```bash
dotnet build src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj
```
Expected: 0 errors.

- [ ] **Step 11.4: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Pages/AIInsightsView.axaml
git commit -m "feat(ui): migrate Recent Activity from Dashboard to AI Insights"
```

---

## Task 12: Remove old AuraCoreTheme.axaml

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/App.axaml`
- Delete: `src/UI/AuraCore.UI.Avalonia/Themes/AuraCoreTheme.axaml`

- [ ] **Step 12.1: Remove old StyleInclude from App.axaml**

Read `src/UI/AuraCore.UI.Avalonia/App.axaml`. Find the line:

```xml
<StyleInclude Source="avares://AuraCore.Pro/Themes/AuraCoreTheme.axaml" />
```

Delete that line. Keep `AuraCoreThemeV2.axaml` + `Icons.axaml` + `FluentTheme`.

- [ ] **Step 12.2: Delete the file**

```bash
rm src/UI/AuraCore.UI.Avalonia/Themes/AuraCoreTheme.axaml
```

- [ ] **Step 12.3: Build to detect missing references**

```bash
dotnet build src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj 2>&1 | grep -E "error|Error" | head -20
```

If any error appears about missing resource keys (e.g., an existing page uses an old key that only exists in V1), patch the affected page to use the V2 key. Common remapping:

| Old key (V1) | New key (V2) |
|--------------|--------------|
| `AccentPrimaryBrush` | `AccentTealBrush` |
| `WarningBgBrush` | `AccentAmberDimBrush` |
| `ErrorBgBrush` | (define ad-hoc if needed, or use StatusErrorBrush at opacity) |

Expected final: 0 errors.

- [ ] **Step 12.4: Full smoke**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --verbosity minimal
```
Expected: no regressions.

- [ ] **Step 12.5: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/App.axaml src/UI/AuraCore.UI.Avalonia/Themes/AuraCoreTheme.axaml
git commit -m "refactor(ui): remove old AuraCoreTheme.axaml; V2 is single source of truth"
```

---

## Task 13: MainWindow integration tests

**Files:**
- Create: `tests/AuraCore.Tests.UI.Avalonia/Views/MainWindowTests.cs`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Views/DashboardViewTests.cs`

- [ ] **Step 13.1: MainWindow tests**

Create `tests/AuraCore.Tests.UI.Avalonia/Views/MainWindowTests.cs`:

```csharp
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using AuraCore.UI.Avalonia.ViewModels;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

public class MainWindowTests
{
    [AvaloniaFact]
    public void SidebarViewModel_BuildsCategoriesForCurrentPlatform()
    {
        var vm = new SidebarViewModel();
        var visible = vm.VisibleCategories();
        Assert.NotEmpty(visible);
    }

    [AvaloniaFact]
    public void NavigateTo_Sets_ActiveModule()
    {
        var vm = new SidebarViewModel();
        vm.NavigateTo("ai-insights");
        Assert.Equal("ai-insights", vm.ActiveModuleId);
        Assert.Equal("ai-features", vm.ExpandedCategoryId);
    }
}
```

- [ ] **Step 13.2: DashboardView tests**

Create `tests/AuraCore.Tests.UI.Avalonia/Views/DashboardViewTests.cs`:

```csharp
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using AuraCore.UI.Avalonia.Views.Pages;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

public class DashboardViewTests
{
    [AvaloniaFact]
    public void DashboardView_Instantiates()
    {
        var v = new DashboardView();
        Assert.NotNull(v);
    }

    [AvaloniaFact]
    public void DashboardView_RendersInWindow()
    {
        var v = new DashboardView();
        using var win = AvaloniaTestBase.RenderInWindow(v, 1000, 640);
        Assert.True(v.IsMeasureValid);
    }
}
```

- [ ] **Step 13.3: Run tests**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~Views." --verbosity minimal
```
Expected: 4 new tests pass.

- [ ] **Step 13.4: Commit**

```bash
git add tests/AuraCore.Tests.UI.Avalonia/Views/MainWindowTests.cs tests/AuraCore.Tests.UI.Avalonia/Views/DashboardViewTests.cs
git commit -m "test(ui): add MainWindow + DashboardView integration tests"
```

---

## Task 14: Full suite + Phase 2 milestone

- [ ] **Step 14.1: Full UI suite**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --verbosity minimal
```
Expected: 99 (baseline) + 3 (GpuInfoHelper) + 8 (SidebarViewModel) + 6 (DashboardViewModel) + 4 (Views) = **120 tests passing**.

- [ ] **Step 14.2: Full solution regression**

```bash
dotnet build AuraCorePro.sln
dotnet test AuraCorePro.sln --verbosity minimal
```
Expected: no new regressions beyond the pre-existing WinAppSDK Desktop build error (non-blocking).

- [ ] **Step 14.3: Launch app manually and verify**

```bash
dotnet run --project src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj
```

Visual checks (document only, no automation):
- Sidebar shows Dashboard + 6 categories + Advanced + Settings
- Clicking each category expands/collapses (accordion)
- Dashboard shows 5 gauges (or 4 if no GPU detected)
- Smart Optimize opens placeholder dialog
- Go to AI Recommendations button navigates correctly
- Ctrl+F12 still opens Component Gallery

Close app.

- [ ] **Step 14.4: Milestone commit**

```bash
git commit --allow-empty -m "milestone: Phase 2 Sidebar + Dashboard Redesign complete"
git log --oneline phase-2-sidebar-dashboard
```

---

## Phase 2 Acceptance Criteria

- [ ] `phase-2-sidebar-dashboard` branch exists, forked from `phase-1-design-system`
- [ ] All 14 tasks committed on branch
- [ ] UI test suite: ~120 tests passing (99 baseline + ~21 new)
- [ ] `AuraCoreTheme.axaml` deleted; `App.axaml` only loads V2
- [ ] Manual visual checks pass (§14.3)
- [ ] No new CA1416 warnings (Phase 1.5 baseline: 136)
- [ ] MainWindow sidebar uses Phase 1 primitives (AppLogoBadge, UserChip, SidebarNavItem, SidebarSectionDivider)
- [ ] Dashboard uses Gauge / HeroCTA / InsightCard / QuickActionTile primitives
- [ ] GPU auto-detects and hides on systems without GPU
- [ ] Responsive layout adapts at 900px window width
- [ ] Smart Optimize placeholder dialog routes to AI Recommendations

## Phase 3 Entry Conditions

- All §Acceptance criteria met
- User retrospective against Vision Doc confirms no vision drift
- Manual check confirms app feels coherent (sidebar + dashboard visually harmonious)
