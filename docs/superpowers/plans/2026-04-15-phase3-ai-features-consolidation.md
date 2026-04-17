# Phase 3: AI Features Consolidation — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Consolidate 4 AI views (Insights, Recommendations, Schedule, Chat) into a single `AIFeaturesView` with hero + 2×2 overview grid + hybrid drill-in; introduce R2 download service so Chat works end-to-end; bundle Phase 2.5 carry-overs (tier locking + missing sidebar modules + visual verify `bccd8ed`) since they touch the same sidebar files.

**Architecture:** New `AIFeaturesView.axaml` with `AIFeaturesViewModel` managing two modes (Overview / Detail). Four `Section` UserControls (one per feature) mounted in detail pane. New `Services/AI/` layer with `IModelCatalog`, `IModelDownloadService`, `IInstalledModelStore`, `ICortexAmbientService`, `ITierService`. Existing `IAuraCoreLLM` + `IAIAnalyzerEngine` preserved untouched. Existing AI view code-behind logic preserved — files moved/renamed only, `.axaml` refreshed with Phase 1 primitives.

**Tech Stack:** Avalonia 11.2.7, xUnit 2.9.2, Avalonia.Headless.XUnit 11.2.7, `HttpClient` (built-in), `Microsoft.Extensions.DependencyInjection` (already in project). No new NuGet packages.

---

## Context & References

- **Spec:** `docs/superpowers/specs/2026-04-15-phase3-ai-features-consolidation-design.md` — authoritative. Spec sections referenced throughout this plan use §X.Y notation.
- **Vision Doc:** `docs/superpowers/specs/2026-04-14-ui-rebuild-vision-document.md` — tokens, primitives, brand.
- **Phase 2 spec + memory:** `docs/superpowers/specs/2026-04-14-phase2-sidebar-dashboard-design.md` + `project_ui_rebuild_phase_2_complete.md` (memory).
- **Phase 2 plan:** `docs/superpowers/plans/2026-04-14-phase2-sidebar-dashboard.md` — pattern reference.
- **Brainstorming mockups:** `.superpowers/brainstorm/617-1776221090/content/*.html` — non-normative visual references.

### Plan-level decisions (discovered during implementation prep)

These clarify spec ambiguities based on actual codebase inspection. All three approved by user 2026-04-15:

1. **AIModelSettings replacement** — Existing `src/UI/AuraCore.UI.Avalonia/AIModelSettings.cs` has a 7-model hardcoded list with different IDs. Phase 3 **deletes** this file and its SettingsView picker; new `IModelCatalog` + `IAppSettings.ActiveChatModelId` are the replacement. SettingsView model dropdown removed (Settings > Models page is Phase 4+). See Task 16.

2. **AIConsentDialog stays separate** — `src/UI/AuraCore.UI.Avalonia/Views/Dialogs/AIConsentDialog.axaml` is a global telemetry consent dialog, **different concern** from our new `ChatOptInDialog` (experimental chat opt-in). Both coexist; no rename or merge.

3. **Code-behind preservation for section "ViewModels"** — Spec §4.1 lists "rename AIInsightsViewModel → InsightsSectionViewModel" etc. but these ViewModel files **do not exist** in the codebase — AI views use code-behind pattern. Plan reinterprets: rename partial class + move `.axaml[.cs]` files; no new ViewModel files introduced for sections. Spec's "preserve ViewModels / preserve AI logic" intent is honored by preserving code-behind logic intact.

### Codebase quirks (from Phase 2 discovery — apply consistently)

1. **`AuraCore.Application` namespace shadows `Avalonia.Application`.** Use `global::Avalonia.X` or `using global::Avalonia;`.
2. **Assembly name is `AuraCore.Pro`.** All `avares://` URIs use `AuraCore.Pro` host.
3. **Avalonia 11.2.7 Grid has no `RowSpacing` / `ColumnSpacing`.** Use `Margin`.
4. **Avalonia Window doesn't implement IDisposable.** Use `TestWindowHandle` wrapper from `AvaloniaTestBase`.
5. **Reference-type StyledProperty defaults are SHARED.** Use `SetCurrentValue(prop, new Instance())` in constructor for per-instance collections.
6. **`Path` elements need `Stretch="Uniform"`** for StreamGeometry icons to scale to layout bounds.
7. **`HeadlessWindowExtensions.GetLastRenderedFrame` needs Skia.** Use `Measure`/`Arrange` instead.
8. **DI composition root** is `src/UI/AuraCore.UI.Avalonia/App.axaml.cs` → `OnFrameworkInitializationCompleted()`. All new services register there.
9. **Theme V2 brushes are `ThemeVariant.Dark`-scoped.** Resource lookups must pass `ActualThemeVariant ?? ThemeVariant.Dark` explicitly (Phase 2 fix `442518f`).
10. **Path module dirs already DI-registered** in App.axaml.cs (Linux/macOS modules covered) — only View navigation wiring needed.

---

## File Structure

### Created

```
src/UI/AuraCore.UI.Avalonia/
├── Services/
│   └── AI/                                             NEW directory
│       ├── IModelCatalog.cs                            NEW — interface
│       ├── ModelCatalog.cs                             NEW — 8 hardcoded models
│       ├── ModelDescriptor.cs                          NEW — record + enums
│       ├── IModelDownloadService.cs                    NEW — interface + DownloadProgress record
│       ├── ModelDownloadService.cs                     NEW — R2 HTTP GET + size verify
│       ├── IInstalledModelStore.cs                     NEW — interface
│       ├── InstalledModelStore.cs                      NEW — disk enumeration
│       ├── ICortexAmbientService.cs                    NEW — interface + CortexActiveness
│       ├── CortexAmbientService.cs                     NEW — state aggregation
│       ├── ITierService.cs                             NEW — interface + UserTier enum
│       └── TierService.cs                              NEW — tier locking logic
├── ViewModels/
│   ├── AIFeatureCardVM.cs                              NEW — per-card overview data
│   └── AIFeaturesViewModel.cs                          NEW — Overview/Detail mode + navigation
├── Views/Controls/
│   └── AIFeatureCard.axaml[.cs]                        NEW — reusable card UserControl
├── Views/Pages/
│   └── AIFeaturesView.axaml[.cs]                       NEW — unified container
├── Views/Pages/AI/                                     NEW directory
│   ├── InsightsSection.axaml[.cs]                      MOVED from AIInsightsView
│   ├── RecommendationsSection.axaml[.cs]               MOVED from RecommendationsView
│   ├── ScheduleSection.axaml[.cs]                      MOVED from SchedulerView
│   └── ChatSection.axaml[.cs]                          MOVED from AIChatView
└── Views/Dialogs/
    ├── ChatOptInDialog.axaml[.cs]                      NEW — 2-step experimental ack
    ├── ModelManagerDialog.axaml[.cs]                   NEW — OptIn + Manage modes
    └── TierUpgradePlaceholderDialog.axaml[.cs]         NEW — tier-locked click placeholder

tests/AuraCore.Tests.UI.Avalonia/
├── Services/
│   └── AI/
│       ├── ModelCatalogTests.cs                        NEW
│       ├── InstalledModelStoreTests.cs                 NEW
│       ├── ModelDownloadServiceTests.cs                NEW
│       ├── CortexAmbientServiceTests.cs                NEW
│       └── TierServiceTests.cs                         NEW
├── ViewModels/
│   ├── AIFeaturesViewModelTests.cs                     NEW
│   ├── ChatOptInDialogViewModelTests.cs                NEW
│   └── ModelManagerDialogViewModelTests.cs             NEW
└── Views/
    ├── AIFeaturesViewTests.cs                          NEW
    ├── AIFeatureCardTests.cs                           NEW
    ├── InsightsSectionTests.cs                         NEW
    ├── RecommendationsSectionTests.cs                  NEW
    ├── ScheduleSectionTests.cs                         NEW
    ├── ChatSectionTests.cs                             NEW
    ├── ChatOptInDialogTests.cs                         NEW
    └── ModelManagerDialogTests.cs                      NEW
```

### Modified

```
src/UI/AuraCore.UI.Avalonia/
├── App.axaml.cs                                        DI registrations for new services + VMs
├── Themes/Icons.axaml                                  Add 7 new Lucide icons
├── LocalizationService.cs                              Add all Phase 3 EN/TR keys (§9)
├── IAppSettings.cs (or existing settings class)        Add 7 new properties (§5.1)
├── ViewModels/
│   ├── DashboardViewModel.cs                           Subscribe to ICortexAmbientService; Smart Optimize CTA state
│   ├── SidebarViewModel.cs                             Single AI Features link; IsLocked; missing modules
│   └── [StatusBarViewModel if exists, or inline in MainWindow]  Ripple subscribe
└── Views/
    ├── MainWindow.axaml[.cs]                           Route AI Features click to AIFeaturesView; accordion sub-items removed
    ├── Controls/
    │   └── SidebarNavItem.axaml[.cs]                   Add IsLocked DP + visual states
    └── Pages/
        ├── DashboardView.axaml[.cs]                    Cortex Insights card conditional; Smart Optimize CTA text/state
        └── SettingsView.axaml[.cs]                     Remove AI model picker (deleted AIModelSettings)
```

### Deleted

```
src/UI/AuraCore.UI.Avalonia/
├── AIModelSettings.cs                                  Replaced by IModelCatalog + IAppSettings
├── Views/Pages/
│   ├── AIInsightsView.axaml[.cs]                       Moved to Pages/AI/InsightsSection.*
│   ├── RecommendationsView.axaml[.cs]                  Moved to Pages/AI/RecommendationsSection.*
│   ├── SchedulerView.axaml[.cs]                        Moved to Pages/AI/ScheduleSection.*
│   └── AIChatView.axaml[.cs]                           Moved to Pages/AI/ChatSection.*
└── Views/Dialogs/
    └── SmartOptimizePlaceholderDialog.axaml[.cs]       CTA now routes directly to AIFeaturesView
```

### Preserved (do NOT touch)

```
src/UI/AuraCore.UI.Avalonia/
├── AIConsentSettings.cs                                Global telemetry consent (separate concern)
└── Views/Dialogs/
    └── AIConsentDialog.axaml[.cs]                      Global telemetry consent dialog

src/Engines/AuraCore.Engine.AIAnalyzer/                 AI logic — preserve entirely
```

---

## Task 1: Pre-flight — Visual verify, baseline tests, branch creation

**Files:** None created. Manual verification + git operations.

- [ ] **Step 1.1: Kill any running AuraCore.Pro.exe**

The Phase 2 memory notes that build errors sometimes came from file locks on a running app. Close it before touching the codebase.

Run (Windows PowerShell or Bash):
```bash
cd "C:/Users/Admin/Desktop/AuraCorePro/AuraCorePro"
# Windows:
taskkill /F /IM AuraCore.Pro.exe 2>nul || echo "no running instance"
# Linux/macOS:
pkill -f AuraCore.Pro || echo "no running instance"
```
Expected: "no running instance" or SUCCESS.

- [ ] **Step 1.2: Confirm current branch is `phase-2-sidebar-dashboard`**

Run:
```bash
git branch --show-current
```
Expected: `phase-2-sidebar-dashboard`.

- [ ] **Step 1.3: Baseline test run to verify 283 green before changes**

Run:
```bash
dotnet test AuraCorePro.sln --configuration Debug --verbosity minimal 2>&1 | tail -20
```
Expected: `Passed: 283, Failed: 0` across all test projects.

If not 283: stop and investigate. Something regressed since Phase 2 memory was written.

- [ ] **Step 1.4: Build app and launch for visual verify of bccd8ed**

Run:
```bash
dotnet run --project src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj --configuration Debug
```

Manual check:
1. Login screen → no crash, dashboard renders.
2. Scroll to Quick Actions row on dashboard.
3. Verify 4 tiles arranged as 2×2 grid, each tile **stretches to fill its grid cell** (not hugged to the left).
4. Icons present: Clean (sparkles), Optimize (rotate-ccw), Gaming (gamepad), Security (shield).
5. GPU gauge shows discrete GPU name (e.g., "RTX 4070") in footer, not integrated GPU name.
6. SYSTEM card shows 5 rows: OS / CPU / GPU / RAM / Uptime.

If anything fails: STOP. Create a Phase 2.5 hotfix commit before Phase 3 code. Do NOT proceed with Phase 3 implementation on broken Phase 2 base.

- [ ] **Step 1.5: Close app cleanly**

Close the app window or Ctrl+C the `dotnet run` process.

- [ ] **Step 1.6: Create phase-3-ai-features branch**

Run:
```bash
git checkout -b phase-3-ai-features
git branch --show-current
```
Expected: `phase-3-ai-features`.

- [ ] **Step 1.7: Record starting HEAD for reference**

Run:
```bash
git log --oneline -1 > /tmp/phase3-start-commit.txt
cat /tmp/phase3-start-commit.txt
```
Expected: something like `5ec797b docs(specs): add Phase 3...`

---

## Task 2: Add 7 new Lucide icons to Icons.axaml

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Themes/Icons.axaml`

Phase 3 needs 7 new icons (§4.8). All are Lucide line-based SVG path strings, 2px stroke. Add them to the existing `Themes/Icons.axaml` following the established `<StreamGeometry x:Key="IconX">...</StreamGeometry>` pattern.

- [ ] **Step 2.1: Locate existing Icons.axaml structure**

Run:
```bash
head -30 src/UI/AuraCore.UI.Avalonia/Themes/Icons.axaml
```
Expected: XML with `<ResourceDictionary>` root and multiple `<StreamGeometry x:Key="IconXXX">...</StreamGeometry>` entries.

Note the existing icon naming pattern (e.g., `IconSparkles`, `IconRotateCcw`, etc.) and placement (typically alphabetical or grouped).

- [ ] **Step 2.2: Add `IconSparklesFilled` (filled variant of IconSparkles)**

Edit `src/UI/AuraCore.UI.Avalonia/Themes/Icons.axaml`. Find the existing `IconSparkles` entry. Add immediately after it:

```xml
<!-- Lucide sparkles (filled) - AI Features Insights -->
<StreamGeometry x:Key="IconSparklesFilled">M12 3l1.91 5.8L19 10.5l-5.09 1.7L12 18l-1.91-5.8L5 10.5l5.09-1.7L12 3z M5 3L5.8 4.8L7.5 5.5L5.8 6.2L5 8L4.2 6.2L2.5 5.5L4.2 4.8L5 3z M19 13l.8 1.8L21.5 15.5L19.8 16.2L19 18L18.2 16.2L16.5 15.5L18.2 14.8L19 13z</StreamGeometry>
```

- [ ] **Step 2.3: Add `IconLightbulb`**

```xml
<!-- Lucide lightbulb - AI Recommendations -->
<StreamGeometry x:Key="IconLightbulb">M15 14c.2-1 .7-1.7 1.5-2.5 1-.9 1.5-2.2 1.5-3.5A6 6 0 0 0 6 8c0 1 .2 2.2 1.5 3.5.7.7 1.3 1.5 1.5 2.5 M9 18h6 M10 22h4</StreamGeometry>
```

- [ ] **Step 2.4: Add `IconCalendarClock`**

```xml
<!-- Lucide calendar-clock - Smart Schedule -->
<StreamGeometry x:Key="IconCalendarClock">M21 7.5V6a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h3.5 M16 2v4 M8 2v4 M3 10h5 M17.5 17.5L16 16.25V14 M22 16a6 6 0 1 1-12 0 6 6 0 0 1 12 0z</StreamGeometry>
```

- [ ] **Step 2.5: Add `IconMessageSquare`**

```xml
<!-- Lucide message-square - AI Chat -->
<StreamGeometry x:Key="IconMessageSquare">M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z</StreamGeometry>
```

- [ ] **Step 2.6: Add `IconDownload`**

```xml
<!-- Lucide download - Model download -->
<StreamGeometry x:Key="IconDownload">M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4 M7 10l5 5 5-5 M12 15V3</StreamGeometry>
```

- [ ] **Step 2.7: Add `IconWarningTriangleFilled`**

```xml
<!-- Lucide triangle-alert (filled) - Experimental warnings -->
<StreamGeometry x:Key="IconWarningTriangleFilled">M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z M12 9v4 M12 17h.01</StreamGeometry>
```

- [ ] **Step 2.8: Add `IconLock`**

```xml
<!-- Lucide lock - Tier locking -->
<StreamGeometry x:Key="IconLock">M19 11H5a2 2 0 0 0-2 2v7a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7a2 2 0 0 0-2-2z M7 11V7a5 5 0 0 1 10 0v4</StreamGeometry>
```

- [ ] **Step 2.9: Build to verify XAML parses**

Run:
```bash
dotnet build src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj --configuration Debug --verbosity minimal
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 2.10: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Themes/Icons.axaml
git commit -m "feat(ui): add 7 new icons for Phase 3 (sparkles-filled, lightbulb, calendar-clock, message-square, download, warning-triangle-filled, lock)"
```

---

## Task 3: Extend IAppSettings with Phase 3 properties

**Files:**
- Modify: likely `src/UI/AuraCore.UI.Avalonia/AppSettings.cs` or `IAppSettings.cs` (verify file location first)

Spec §5.1 requires 7 new IAppSettings properties. These persist Phase 3 toggle state + chat opt-in state.

- [ ] **Step 3.1: Locate the IAppSettings interface / class**

Run:
```bash
grep -rln "interface IAppSettings\|class AppSettings" src/UI/AuraCore.UI.Avalonia/ --include="*.cs" | grep -v "bin\|obj"
```

If file found: note the path for next steps.
If no file: the settings might be stored differently (e.g., via `AIConsentSettings` static pattern). Check Phase 2 memory and sidebar code for how Phase 2 settings are persisted. In that case, create `src/UI/AuraCore.UI.Avalonia/AppSettings.cs` with the same static pattern as `AIModelSettings.cs` uses.

- [ ] **Step 3.2: Write failing test for new properties**

Create `tests/AuraCore.Tests.UI.Avalonia/AppSettingsPhase3Tests.cs` (adjust path if AppSettings is at a different namespace):

```csharp
using AuraCore.UI.Avalonia;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia;

public class AppSettingsPhase3Tests
{
    [Fact]
    public void InsightsEnabled_Default_IsTrue()
    {
        var settings = new AppSettings();
        Assert.True(settings.InsightsEnabled);
    }

    [Fact]
    public void RecommendationsEnabled_Default_IsTrue()
    {
        var settings = new AppSettings();
        Assert.True(settings.RecommendationsEnabled);
    }

    [Fact]
    public void ScheduleEnabled_Default_IsTrue()
    {
        var settings = new AppSettings();
        Assert.True(settings.ScheduleEnabled);
    }

    [Fact]
    public void ChatEnabled_Default_IsFalse()
    {
        var settings = new AppSettings();
        Assert.False(settings.ChatEnabled);
    }

    [Fact]
    public void ChatOptInAcknowledged_Default_IsFalse()
    {
        var settings = new AppSettings();
        Assert.False(settings.ChatOptInAcknowledged);
    }

    [Fact]
    public void ActiveChatModelId_Default_IsNull()
    {
        var settings = new AppSettings();
        Assert.Null(settings.ActiveChatModelId);
    }

    [Fact]
    public void AIFirstEnabledAt_Default_IsNull()
    {
        var settings = new AppSettings();
        Assert.Null(settings.AIFirstEnabledAt);
    }
}
```

- [ ] **Step 3.3: Run test to verify it fails**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~AppSettingsPhase3Tests" --verbosity minimal
```
Expected: FAIL with errors about missing properties or missing `AppSettings` class.

- [ ] **Step 3.4: Add the 7 new properties to AppSettings**

If the class exists, add these properties. If it doesn't exist, create `src/UI/AuraCore.UI.Avalonia/AppSettings.cs`:

```csharp
using System.Text.Json;

namespace AuraCore.UI.Avalonia;

/// <summary>
/// Application-level user preferences persisted to disk.
/// Phase 3 extensions: AI feature toggles, chat opt-in state, active model tracking, learning-day anchor.
/// </summary>
public class AppSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AuraCorePro", "app_settings.json");

    // Phase 3 — AI feature toggles
    public bool InsightsEnabled { get; set; } = true;
    public bool RecommendationsEnabled { get; set; } = true;
    public bool ScheduleEnabled { get; set; } = true;
    public bool ChatEnabled { get; set; } = false; // opt-in

    // Phase 3 — Chat opt-in flow state
    public bool ChatOptInAcknowledged { get; set; } = false;
    public string? ActiveChatModelId { get; set; } = null;

    // Phase 3 — Learning-day anchor for CORTEX status chip
    public DateTime? AIFirstEnabledAt { get; set; } = null;

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            // Corruption → safe defaults
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (dir != null) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // Best-effort persistence; log in real impl
        }
    }
}
```

If AppSettings already existed as an interface + implementation, add the 7 properties to both the interface and the implementing class, preserving existing fields.

- [ ] **Step 3.5: Run test to verify it passes**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~AppSettingsPhase3Tests" --verbosity minimal
```
Expected: `Passed: 7, Failed: 0`.

- [ ] **Step 3.6: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/AppSettings.cs tests/AuraCore.Tests.UI.Avalonia/AppSettingsPhase3Tests.cs
git commit -m "feat(settings): add Phase 3 AI toggle state + chat opt-in + active model properties to AppSettings"
```

---

## Task 4: Create ModelDescriptor record + ModelTier/SpeedClass enums

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Services/AI/ModelDescriptor.cs`

Spec §6.1 defines the model metadata record.

- [ ] **Step 4.1: Create Services/AI directory**

Run:
```bash
mkdir -p src/UI/AuraCore.UI.Avalonia/Services/AI
```

- [ ] **Step 4.2: Create ModelDescriptor.cs with enums + record**

Create `src/UI/AuraCore.UI.Avalonia/Services/AI/ModelDescriptor.cs`:

```csharp
namespace AuraCore.UI.Avalonia.Services.AI;

/// <summary>
/// Capability tier of an AI model, tied to approximate RAM requirements.
/// </summary>
public enum ModelTier
{
    Lite,      // < 4 GB RAM
    Standard,  // 4-8 GB RAM
    Advanced,  // 16 GB RAM
    Heavy      // 32+ GB RAM
}

/// <summary>
/// Rough inference speed class for a model on typical hardware.
/// </summary>
public enum SpeedClass
{
    Fast,
    Medium,
    Slow
}

/// <summary>
/// Metadata for an AI model available in the AuraCore catalog.
/// Used by IModelCatalog; localized display via DescriptionKey.
/// </summary>
public record ModelDescriptor(
    string Id,
    string DisplayName,
    string Filename,
    long SizeBytes,
    long EstimatedRamBytes,
    ModelTier Tier,
    SpeedClass Speed,
    bool IsRecommended,
    string DescriptionKey);
```

- [ ] **Step 4.3: Build to verify compilation**

Run:
```bash
dotnet build src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj --configuration Debug --verbosity minimal
```
Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 4.4: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Services/AI/ModelDescriptor.cs
git commit -m "feat(ai): add ModelDescriptor record + ModelTier/SpeedClass enums"
```

---

## Task 5: Create IModelCatalog interface + ModelCatalog implementation (8 models)

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Services/AI/IModelCatalog.cs`
- Create: `src/UI/AuraCore.UI.Avalonia/Services/AI/ModelCatalog.cs`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Services/AI/ModelCatalogTests.cs`

Spec §6.1 enumerates all 8 models.

- [ ] **Step 5.1: Write failing tests**

Create `tests/AuraCore.Tests.UI.Avalonia/Services/AI/ModelCatalogTests.cs`:

```csharp
using AuraCore.UI.Avalonia.Services.AI;
using System.Linq;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Services.AI;

public class ModelCatalogTests
{
    [Fact]
    public void All_ReturnsExactly8Models()
    {
        var catalog = new ModelCatalog();
        Assert.Equal(8, catalog.All.Count);
    }

    [Fact]
    public void All_ContainsExpectedIds()
    {
        var catalog = new ModelCatalog();
        var ids = catalog.All.Select(m => m.Id).ToHashSet();
        Assert.Contains("tinyllama", ids);
        Assert.Contains("phi3-mini-q4km", ids);
        Assert.Contains("phi2", ids);
        Assert.Contains("phi3-mini", ids);
        Assert.Contains("mistral-7b", ids);
        Assert.Contains("llama31-8b", ids);
        Assert.Contains("phi3-medium", ids);
        Assert.Contains("qwen25-32b", ids);
    }

    [Fact]
    public void Phi3MiniQ4KM_IsRecommended()
    {
        var catalog = new ModelCatalog();
        var recommended = catalog.All.Where(m => m.IsRecommended).ToList();
        Assert.Single(recommended);
        Assert.Equal("phi3-mini-q4km", recommended[0].Id);
    }

    [Fact]
    public void HeavyTier_RequiresAtLeast32GbRam()
    {
        var catalog = new ModelCatalog();
        var heavy = catalog.All.Where(m => m.Tier == ModelTier.Heavy);
        Assert.All(heavy, m => Assert.True(m.EstimatedRamBytes >= 32L * 1024 * 1024 * 1024));
    }

    [Fact]
    public void FindById_ExistingId_ReturnsDescriptor()
    {
        var catalog = new ModelCatalog();
        var model = catalog.FindById("phi3-mini-q4km");
        Assert.NotNull(model);
        Assert.Equal("Phi-3 Mini Q4KM", model!.DisplayName);
    }

    [Fact]
    public void FindById_UnknownId_ReturnsNull()
    {
        var catalog = new ModelCatalog();
        var model = catalog.FindById("does-not-exist");
        Assert.Null(model);
    }

    [Fact]
    public void FindByFilename_ExistingFile_ReturnsDescriptor()
    {
        var catalog = new ModelCatalog();
        var model = catalog.FindByFilename("auracore-tinyllama.gguf");
        Assert.NotNull(model);
        Assert.Equal("tinyllama", model!.Id);
    }

    [Fact]
    public void FindByFilename_UnknownFile_ReturnsNull()
    {
        var catalog = new ModelCatalog();
        var model = catalog.FindByFilename("random-model.gguf");
        Assert.Null(model);
    }

    [Fact]
    public void AllFilenames_StartWithAuracorePrefix()
    {
        var catalog = new ModelCatalog();
        Assert.All(catalog.All, m => Assert.StartsWith("auracore-", m.Filename));
    }

    [Fact]
    public void AllFilenames_EndWithGguf()
    {
        var catalog = new ModelCatalog();
        Assert.All(catalog.All, m => Assert.EndsWith(".gguf", m.Filename));
    }
}
```

- [ ] **Step 5.2: Run tests to verify they fail**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~ModelCatalogTests" --verbosity minimal
```
Expected: build error about missing `ModelCatalog` class.

- [ ] **Step 5.3: Create IModelCatalog interface**

Create `src/UI/AuraCore.UI.Avalonia/Services/AI/IModelCatalog.cs`:

```csharp
namespace AuraCore.UI.Avalonia.Services.AI;

/// <summary>
/// Read-only catalog of AI models known to AuraCore.
/// Hardcoded in Phase 3; future phases may load from remote manifest.
/// </summary>
public interface IModelCatalog
{
    IReadOnlyList<ModelDescriptor> All { get; }
    ModelDescriptor? FindById(string id);
    ModelDescriptor? FindByFilename(string filename);
}
```

- [ ] **Step 5.4: Create ModelCatalog implementation with all 8 models**

Create `src/UI/AuraCore.UI.Avalonia/Services/AI/ModelCatalog.cs`:

```csharp
namespace AuraCore.UI.Avalonia.Services.AI;

/// <summary>
/// Hardcoded 8-model catalog per Phase 3 spec §6.1.
/// Sizes in bytes are approximate (±1% rounding acceptable for UI display).
/// </summary>
public sealed class ModelCatalog : IModelCatalog
{
    private const long GB = 1024L * 1024 * 1024;

    private static readonly IReadOnlyList<ModelDescriptor> _models = new[]
    {
        new ModelDescriptor(
            Id: "tinyllama",
            DisplayName: "TinyLlama",
            Filename: "auracore-tinyllama.gguf",
            SizeBytes: (long)(2.1 * GB),
            EstimatedRamBytes: 2L * GB,
            Tier: ModelTier.Lite,
            Speed: SpeedClass.Fast,
            IsRecommended: false,
            DescriptionKey: "modelManager.model.tinyllama.description"),

        new ModelDescriptor(
            Id: "phi3-mini-q4km",
            DisplayName: "Phi-3 Mini Q4KM",
            Filename: "auracore-phi3-mini-q4km.gguf",
            SizeBytes: (long)(2.3 * GB),
            EstimatedRamBytes: 3L * GB,
            Tier: ModelTier.Lite,
            Speed: SpeedClass.Fast,
            IsRecommended: true,
            DescriptionKey: "modelManager.model.phi3-mini-q4km.description"),

        new ModelDescriptor(
            Id: "phi2",
            DisplayName: "Phi-2",
            Filename: "auracore-phi2.gguf",
            SizeBytes: (long)(5.3 * GB),
            EstimatedRamBytes: 6L * GB,
            Tier: ModelTier.Standard,
            Speed: SpeedClass.Medium,
            IsRecommended: false,
            DescriptionKey: "modelManager.model.phi2.description"),

        new ModelDescriptor(
            Id: "phi3-mini",
            DisplayName: "Phi-3 Mini",
            Filename: "auracore-phi3-mini.gguf",
            SizeBytes: (long)(7.3 * GB),
            EstimatedRamBytes: 8L * GB,
            Tier: ModelTier.Standard,
            Speed: SpeedClass.Medium,
            IsRecommended: false,
            DescriptionKey: "modelManager.model.phi3-mini.description"),

        new ModelDescriptor(
            Id: "mistral-7b",
            DisplayName: "Mistral 7B",
            Filename: "auracore-mistral-7b.gguf",
            SizeBytes: 14L * GB,
            EstimatedRamBytes: 16L * GB,
            Tier: ModelTier.Advanced,
            Speed: SpeedClass.Slow,
            IsRecommended: false,
            DescriptionKey: "modelManager.model.mistral-7b.description"),

        new ModelDescriptor(
            Id: "llama31-8b",
            DisplayName: "Llama 3.1 8B",
            Filename: "auracore-llama31-8b.gguf",
            SizeBytes: 15L * GB,
            EstimatedRamBytes: 18L * GB,
            Tier: ModelTier.Advanced,
            Speed: SpeedClass.Slow,
            IsRecommended: false,
            DescriptionKey: "modelManager.model.llama31-8b.description"),

        new ModelDescriptor(
            Id: "phi3-medium",
            DisplayName: "Phi-3 Medium",
            Filename: "auracore-phi3-medium.gguf",
            SizeBytes: (long)(26.6 * GB),
            EstimatedRamBytes: 32L * GB,
            Tier: ModelTier.Heavy,
            Speed: SpeedClass.Slow,
            IsRecommended: false,
            DescriptionKey: "modelManager.model.phi3-medium.description"),

        new ModelDescriptor(
            Id: "qwen25-32b",
            DisplayName: "Qwen 2.5 32B",
            Filename: "auracore-qwen25-32b.gguf",
            SizeBytes: (long)(62.5 * GB),
            EstimatedRamBytes: 70L * GB,
            Tier: ModelTier.Heavy,
            Speed: SpeedClass.Slow,
            IsRecommended: false,
            DescriptionKey: "modelManager.model.qwen25-32b.description"),
    };

    public IReadOnlyList<ModelDescriptor> All => _models;

    public ModelDescriptor? FindById(string id) =>
        _models.FirstOrDefault(m => m.Id == id);

    public ModelDescriptor? FindByFilename(string filename) =>
        _models.FirstOrDefault(m => m.Filename == filename);
}
```

- [ ] **Step 5.5: Run tests to verify they pass**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~ModelCatalogTests" --verbosity minimal
```
Expected: `Passed: 10, Failed: 0`.

- [ ] **Step 5.6: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Services/AI/IModelCatalog.cs src/UI/AuraCore.UI.Avalonia/Services/AI/ModelCatalog.cs tests/AuraCore.Tests.UI.Avalonia/Services/AI/ModelCatalogTests.cs
git commit -m "feat(ai): add IModelCatalog + ModelCatalog with 8 hardcoded models"
```

---

## Task 6: Create IInstalledModelStore + InstalledModelStore

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Services/AI/IInstalledModelStore.cs`
- Create: `src/UI/AuraCore.UI.Avalonia/Services/AI/InstalledModelStore.cs`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Services/AI/InstalledModelStoreTests.cs`

Spec §6.3 + §6.4. Enumerates `.gguf` files on disk, resolves to ModelIds via catalog lookup.

- [ ] **Step 6.1: Write failing tests**

Create `tests/AuraCore.Tests.UI.Avalonia/Services/AI/InstalledModelStoreTests.cs`:

```csharp
using AuraCore.UI.Avalonia.Services.AI;
using System.IO;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Services.AI;

public class InstalledModelStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly IModelCatalog _catalog;

    public InstalledModelStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "auracore-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _catalog = new ModelCatalog();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void Enumerate_EmptyDir_ReturnsEmpty()
    {
        var store = new InstalledModelStore(_catalog, _tempDir);
        Assert.Empty(store.Enumerate());
    }

    [Fact]
    public void Enumerate_MissingDir_ReturnsEmpty()
    {
        var missing = Path.Combine(_tempDir, "nonexistent");
        var store = new InstalledModelStore(_catalog, missing);
        Assert.Empty(store.Enumerate());
    }

    [Fact]
    public void Enumerate_KnownGgufFile_ReturnsInstalledModel()
    {
        var path = Path.Combine(_tempDir, "auracore-tinyllama.gguf");
        File.WriteAllText(path, "fake-gguf");

        var store = new InstalledModelStore(_catalog, _tempDir);
        var installed = store.Enumerate();

        Assert.Single(installed);
        Assert.Equal("tinyllama", installed[0].ModelId);
    }

    [Fact]
    public void Enumerate_UnknownGgufFile_Ignored()
    {
        var path = Path.Combine(_tempDir, "random-model.gguf");
        File.WriteAllText(path, "fake-gguf");

        var store = new InstalledModelStore(_catalog, _tempDir);
        Assert.Empty(store.Enumerate());
    }

    [Fact]
    public void Enumerate_NonGgufFile_Ignored()
    {
        File.WriteAllText(Path.Combine(_tempDir, "readme.txt"), "hi");
        File.WriteAllText(Path.Combine(_tempDir, "auracore-tinyllama.zip"), "data");

        var store = new InstalledModelStore(_catalog, _tempDir);
        Assert.Empty(store.Enumerate());
    }

    [Fact]
    public void IsInstalled_Installed_ReturnsTrue()
    {
        File.WriteAllText(Path.Combine(_tempDir, "auracore-phi2.gguf"), "fake");
        var store = new InstalledModelStore(_catalog, _tempDir);
        Assert.True(store.IsInstalled("phi2"));
    }

    [Fact]
    public void IsInstalled_NotInstalled_ReturnsFalse()
    {
        var store = new InstalledModelStore(_catalog, _tempDir);
        Assert.False(store.IsInstalled("phi2"));
    }

    [Fact]
    public void GetFile_Installed_ReturnsFileInfo()
    {
        var path = Path.Combine(_tempDir, "auracore-phi2.gguf");
        File.WriteAllText(path, "fake");
        var store = new InstalledModelStore(_catalog, _tempDir);

        var file = store.GetFile("phi2");

        Assert.NotNull(file);
        Assert.Equal(path, file!.FullName);
    }

    [Fact]
    public void GetFile_NotInstalled_ReturnsNull()
    {
        var store = new InstalledModelStore(_catalog, _tempDir);
        Assert.Null(store.GetFile("phi2"));
    }
}
```

- [ ] **Step 6.2: Run tests to verify they fail**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~InstalledModelStoreTests" --verbosity minimal
```
Expected: build error.

- [ ] **Step 6.3: Create IInstalledModelStore + record**

Create `src/UI/AuraCore.UI.Avalonia/Services/AI/IInstalledModelStore.cs`:

```csharp
using System.IO;

namespace AuraCore.UI.Avalonia.Services.AI;

/// <summary>
/// Represents a model file present on disk, resolved against the catalog.
/// </summary>
public record InstalledModel(
    string ModelId,
    FileInfo File,
    long SizeBytes,
    DateTime DownloadedAt);

/// <summary>
/// Enumerates locally-installed models (gguf files in the install directory).
/// Phase 3 read-only; Phase 4+ adds DeleteAsync.
/// </summary>
public interface IInstalledModelStore
{
    IReadOnlyList<InstalledModel> Enumerate();
    bool IsInstalled(string modelId);
    FileInfo? GetFile(string modelId);
}
```

- [ ] **Step 6.4: Create InstalledModelStore implementation**

Create `src/UI/AuraCore.UI.Avalonia/Services/AI/InstalledModelStore.cs`:

```csharp
using System.IO;

namespace AuraCore.UI.Avalonia.Services.AI;

public sealed class InstalledModelStore : IInstalledModelStore
{
    private readonly IModelCatalog _catalog;
    private readonly string _installDir;

    public InstalledModelStore(IModelCatalog catalog, string? installDir = null)
    {
        _catalog = catalog;
        _installDir = installDir ?? DefaultInstallDir();
    }

    public static string DefaultInstallDir() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AuraCorePro", "models");

    public IReadOnlyList<InstalledModel> Enumerate()
    {
        if (!Directory.Exists(_installDir))
            return Array.Empty<InstalledModel>();

        var results = new List<InstalledModel>();
        foreach (var file in Directory.EnumerateFiles(_installDir, "auracore-*.gguf"))
        {
            var info = new FileInfo(file);
            var descriptor = _catalog.FindByFilename(info.Name);
            if (descriptor is null) continue; // orphan file — ignore

            results.Add(new InstalledModel(
                ModelId: descriptor.Id,
                File: info,
                SizeBytes: info.Length,
                DownloadedAt: info.CreationTimeUtc));
        }
        return results;
    }

    public bool IsInstalled(string modelId) =>
        GetFile(modelId) is not null;

    public FileInfo? GetFile(string modelId)
    {
        var descriptor = _catalog.FindById(modelId);
        if (descriptor is null) return null;

        var path = Path.Combine(_installDir, descriptor.Filename);
        return File.Exists(path) ? new FileInfo(path) : null;
    }
}
```

- [ ] **Step 6.5: Run tests to verify they pass**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~InstalledModelStoreTests" --verbosity minimal
```
Expected: `Passed: 9, Failed: 0`.

- [ ] **Step 6.6: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Services/AI/IInstalledModelStore.cs src/UI/AuraCore.UI.Avalonia/Services/AI/InstalledModelStore.cs tests/AuraCore.Tests.UI.Avalonia/Services/AI/InstalledModelStoreTests.cs
git commit -m "feat(ai): add IInstalledModelStore + InstalledModelStore (disk enumeration)"
```

---

## Task 7: Create IModelDownloadService + ModelDownloadService

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Services/AI/IModelDownloadService.cs`
- Create: `src/UI/AuraCore.UI.Avalonia/Services/AI/ModelDownloadService.cs`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Services/AI/ModelDownloadServiceTests.cs`

Spec §6.2. HTTP GET with progress, size verification, User-Agent, atomic rename on success.

- [ ] **Step 7.1: Write failing tests**

Create `tests/AuraCore.Tests.UI.Avalonia/Services/AI/ModelDownloadServiceTests.cs`:

```csharp
using AuraCore.UI.Avalonia.Services.AI;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Services.AI;

public class ModelDownloadServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ModelDescriptor _testModel;

    public ModelDownloadServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "auracore-dl-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _testModel = new ModelDescriptor(
            Id: "tinyllama",
            DisplayName: "TinyLlama",
            Filename: "auracore-tinyllama.gguf",
            SizeBytes: 16, // 16 bytes of fake content
            EstimatedRamBytes: 2L * 1024 * 1024 * 1024,
            Tier: ModelTier.Lite,
            Speed: SpeedClass.Fast,
            IsRecommended: false,
            DescriptionKey: "modelManager.model.tinyllama.description");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private HttpClient StubHttp(byte[] content, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new HttpClient(new StubHandler(content, status));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly byte[] _content;
        private readonly HttpStatusCode _status;
        public StubHandler(byte[] content, HttpStatusCode status) { _content = content; _status = status; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = new HttpResponseMessage(_status);
            if (_status == HttpStatusCode.OK)
            {
                var stream = new MemoryStream(_content);
                response.Content = new StreamContent(stream);
                response.Content.Headers.ContentLength = _content.Length;
            }
            return Task.FromResult(response);
        }
    }

    [Fact]
    public async Task DownloadAsync_Success_CreatesGgufFile()
    {
        var content = Encoding.ASCII.GetBytes("0123456789abcdef"); // 16 bytes matching SizeBytes
        var http = StubHttp(content);
        var settings = new ModelDownloadSettings("https://example.com", _tempDir, 30, 4, "Test/1.0");
        var svc = new ModelDownloadService(http, settings);

        var file = await svc.DownloadAsync(_testModel, new Progress<DownloadProgress>(), CancellationToken.None);

        Assert.True(File.Exists(file.FullName));
        Assert.EndsWith(".gguf", file.FullName);
        Assert.Equal(16, new FileInfo(file.FullName).Length);
    }

    [Fact]
    public async Task DownloadAsync_Success_DeletesPartialTempFile()
    {
        var content = Encoding.ASCII.GetBytes("0123456789abcdef");
        var http = StubHttp(content);
        var settings = new ModelDownloadSettings("https://example.com", _tempDir, 30, 4, "Test/1.0");
        var svc = new ModelDownloadService(http, settings);

        await svc.DownloadAsync(_testModel, new Progress<DownloadProgress>(), CancellationToken.None);

        var tempPath = Path.Combine(_tempDir, _testModel.Filename + ".download");
        Assert.False(File.Exists(tempPath));
    }

    [Fact]
    public async Task DownloadAsync_SizeMismatch_ThrowsAndDeletesFile()
    {
        var content = Encoding.ASCII.GetBytes("short"); // 5 bytes, expected 16
        var http = StubHttp(content);
        var settings = new ModelDownloadSettings("https://example.com", _tempDir, 30, 4, "Test/1.0");
        var svc = new ModelDownloadService(http, settings);

        await Assert.ThrowsAsync<ModelSizeMismatchException>(() =>
            svc.DownloadAsync(_testModel, new Progress<DownloadProgress>(), CancellationToken.None));

        var path = Path.Combine(_tempDir, _testModel.Filename);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task DownloadAsync_Cancelled_DeletesPartialFile()
    {
        var content = new byte[1024 * 1024]; // 1 MB stream - enough to allow cancellation
        var http = StubHttp(content);
        var settings = new ModelDownloadSettings("https://example.com", _tempDir, 30, 4, "Test/1.0");
        var svc = new ModelDownloadService(http, settings);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // pre-cancelled

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            svc.DownloadAsync(_testModel, new Progress<DownloadProgress>(), cts.Token));

        var tempPath = Path.Combine(_tempDir, _testModel.Filename + ".download");
        Assert.False(File.Exists(tempPath));
    }

    [Fact]
    public async Task DownloadAsync_HttpError_Throws()
    {
        var http = StubHttp(Array.Empty<byte>(), HttpStatusCode.Forbidden);
        var settings = new ModelDownloadSettings("https://example.com", _tempDir, 30, 4, "Test/1.0");
        var svc = new ModelDownloadService(http, settings);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            svc.DownloadAsync(_testModel, new Progress<DownloadProgress>(), CancellationToken.None));
    }

    [Fact]
    public async Task DownloadAsync_ReportsProgress()
    {
        var content = new byte[16];
        var http = StubHttp(content);
        var settings = new ModelDownloadSettings("https://example.com", _tempDir, 30, 4, "Test/1.0");
        var svc = new ModelDownloadService(http, settings);

        var progressReports = new List<DownloadProgress>();
        var progress = new Progress<DownloadProgress>(p => progressReports.Add(p));

        await svc.DownloadAsync(_testModel, progress, CancellationToken.None);

        await Task.Delay(100); // allow progress callbacks to flush
        Assert.NotEmpty(progressReports);
    }
}
```

- [ ] **Step 7.2: Run tests to verify they fail**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~ModelDownloadServiceTests" --verbosity minimal
```
Expected: build errors about missing types.

- [ ] **Step 7.3: Create IModelDownloadService + types**

Create `src/UI/AuraCore.UI.Avalonia/Services/AI/IModelDownloadService.cs`:

```csharp
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AuraCore.UI.Avalonia.Services.AI;

/// <summary>
/// Progress snapshot reported during a download.
/// </summary>
public record DownloadProgress(
    long BytesReceived,
    long TotalBytes,
    double BytesPerSecond,
    TimeSpan? EstimatedTimeRemaining);

/// <summary>
/// Configuration for the download service. Read from appsettings.json.
/// </summary>
public record ModelDownloadSettings(
    string BaseUrl,
    string InstallDirectory,
    int TimeoutMinutes,
    int BufferKb,
    string UserAgent);

/// <summary>
/// Thrown when the downloaded file size does not match the catalog's declared size.
/// </summary>
public sealed class ModelSizeMismatchException : Exception
{
    public long ExpectedBytes { get; }
    public long ActualBytes { get; }
    public ModelSizeMismatchException(long expected, long actual)
        : base($"Model size mismatch: expected {expected} bytes, got {actual} bytes.")
    { ExpectedBytes = expected; ActualBytes = actual; }
}

/// <summary>
/// Downloads model files from the catalog base URL with progress reporting.
/// Phase 3 minimum: no resume, no checksum. Size verification only.
/// </summary>
public interface IModelDownloadService
{
    Task<FileInfo> DownloadAsync(
        ModelDescriptor model,
        IProgress<DownloadProgress> progress,
        CancellationToken ct);
}
```

- [ ] **Step 7.4: Create ModelDownloadService implementation**

Create `src/UI/AuraCore.UI.Avalonia/Services/AI/ModelDownloadService.cs`:

```csharp
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace AuraCore.UI.Avalonia.Services.AI;

public sealed class ModelDownloadService : IModelDownloadService
{
    private const long SIZE_TOLERANCE_BYTES = 1024 * 1024; // ±1 MB per spec §6.2

    private readonly HttpClient _http;
    private readonly ModelDownloadSettings _settings;

    public ModelDownloadService(HttpClient http, ModelDownloadSettings settings)
    {
        _http = http;
        _settings = settings;

        // Ensure User-Agent is set (spec Risk R1 — Bot Fight Mode bypass)
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(_settings.UserAgent);
        }
    }

    public async Task<FileInfo> DownloadAsync(
        ModelDescriptor model,
        IProgress<DownloadProgress> progress,
        CancellationToken ct)
    {
        Directory.CreateDirectory(_settings.InstallDirectory);

        var url = $"{_settings.BaseUrl.TrimEnd('/')}/{model.Filename}";
        var finalPath = Path.Combine(_settings.InstallDirectory, model.Filename);
        var tempPath = finalPath + ".download";

        // Clean up any stale temp file from previous failed attempt
        if (File.Exists(tempPath)) File.Delete(tempPath);

        try
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? model.SizeBytes;
            long receivedBytes = 0;
            var stopwatch = Stopwatch.StartNew();

            await using var networkStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using var fileStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);

            var buffer = new byte[_settings.BufferKb * 1024];
            int bytesRead;
            var lastReport = TimeSpan.Zero;

            while ((bytesRead = await networkStream.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
                receivedBytes += bytesRead;

                // Throttle progress reporting to ~10/sec to avoid UI spam
                var now = stopwatch.Elapsed;
                if ((now - lastReport).TotalMilliseconds >= 100)
                {
                    var bps = receivedBytes / Math.Max(0.001, now.TotalSeconds);
                    TimeSpan? eta = null;
                    if (bps > 0 && totalBytes > receivedBytes)
                        eta = TimeSpan.FromSeconds((totalBytes - receivedBytes) / bps);

                    progress.Report(new DownloadProgress(receivedBytes, totalBytes, bps, eta));
                    lastReport = now;
                }
            }

            await fileStream.FlushAsync(ct).ConfigureAwait(false);
            fileStream.Close();

            // Final progress report
            progress.Report(new DownloadProgress(receivedBytes, totalBytes, 0, TimeSpan.Zero));

            // Size verification per spec §6.2
            var actualSize = new FileInfo(tempPath).Length;
            if (Math.Abs(actualSize - model.SizeBytes) > SIZE_TOLERANCE_BYTES)
            {
                File.Delete(tempPath);
                throw new ModelSizeMismatchException(model.SizeBytes, actualSize);
            }

            // Atomic rename .download → .gguf
            if (File.Exists(finalPath)) File.Delete(finalPath);
            File.Move(tempPath, finalPath);

            return new FileInfo(finalPath);
        }
        catch
        {
            // Any failure (cancellation, http error, size mismatch) → clean up temp file
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            throw;
        }
    }
}
```

- [ ] **Step 7.5: Run tests to verify they pass**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~ModelDownloadServiceTests" --verbosity minimal
```
Expected: `Passed: 6, Failed: 0`.

- [ ] **Step 7.6: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Services/AI/IModelDownloadService.cs src/UI/AuraCore.UI.Avalonia/Services/AI/ModelDownloadService.cs tests/AuraCore.Tests.UI.Avalonia/Services/AI/ModelDownloadServiceTests.cs
git commit -m "feat(ai): add IModelDownloadService + ModelDownloadService (R2 HTTP GET + size verify + progress)"
```

---

## Task 8: Create ICortexAmbientService + CortexAmbientService

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Services/AI/ICortexAmbientService.cs`
- Create: `src/UI/AuraCore.UI.Avalonia/Services/AI/CortexAmbientService.cs`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Services/AI/CortexAmbientServiceTests.cs`

Spec §5.2. Aggregates feature toggle state, fires PropertyChanged when state changes, tracks learning-day.

- [ ] **Step 8.1: Write failing tests**

Create `tests/AuraCore.Tests.UI.Avalonia/Services/AI/CortexAmbientServiceTests.cs`:

```csharp
using AuraCore.UI.Avalonia;
using AuraCore.UI.Avalonia.Services.AI;
using System.ComponentModel;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Services.AI;

public class CortexAmbientServiceTests
{
    private AppSettings FreshSettings(
        bool insights = false, bool recs = false,
        bool schedule = false, bool chat = false,
        DateTime? firstEnabled = null)
    => new()
    {
        InsightsEnabled = insights,
        RecommendationsEnabled = recs,
        ScheduleEnabled = schedule,
        ChatEnabled = chat,
        AIFirstEnabledAt = firstEnabled,
    };

    [Fact]
    public void AllOff_ReportsPaused_WhenAIFirstEnabledAtSet()
    {
        var settings = FreshSettings(firstEnabled: DateTime.UtcNow.AddDays(-2));
        var svc = new CortexAmbientService(settings);

        Assert.Equal(CortexActiveness.Paused, svc.Activeness);
        Assert.False(svc.AnyFeatureEnabled);
    }

    [Fact]
    public void AllOff_ReportsReady_WhenAIFirstEnabledAtNull()
    {
        var settings = FreshSettings();
        var svc = new CortexAmbientService(settings);

        Assert.Equal(CortexActiveness.Ready, svc.Activeness);
    }

    [Fact]
    public void AnyOn_ReportsActive()
    {
        var settings = FreshSettings(insights: true);
        var svc = new CortexAmbientService(settings);

        Assert.Equal(CortexActiveness.Active, svc.Activeness);
        Assert.True(svc.AnyFeatureEnabled);
    }

    [Fact]
    public void EnabledFeatureCount_CountsCorrectly()
    {
        var settings = FreshSettings(insights: true, recs: true);
        var svc = new CortexAmbientService(settings);

        Assert.Equal(2, svc.EnabledFeatureCount);
        Assert.Equal(4, svc.TotalFeatureCount);
    }

    [Fact]
    public void LearningDay_Null_ReturnsZero()
    {
        var settings = FreshSettings();
        var svc = new CortexAmbientService(settings);

        Assert.Equal(0, svc.LearningDay);
    }

    [Fact]
    public void LearningDay_TwoDaysAgo_ReturnsTwo()
    {
        var settings = FreshSettings(firstEnabled: DateTime.UtcNow.AddDays(-2));
        var svc = new CortexAmbientService(settings);

        // Should be ~2 (allow ±1 for clock granularity around day boundaries)
        Assert.InRange(svc.LearningDay, 1, 3);
    }

    [Fact]
    public void LearningDay_FutureTimestamp_ClampedToZero()
    {
        var settings = FreshSettings(firstEnabled: DateTime.UtcNow.AddDays(5));
        var svc = new CortexAmbientService(settings);

        Assert.Equal(0, svc.LearningDay);
    }

    [Fact]
    public void RecomputeState_AfterEnableFlag_FiresPropertyChanged()
    {
        var settings = FreshSettings();
        var svc = new CortexAmbientService(settings);

        var changed = new List<string>();
        ((INotifyPropertyChanged)svc).PropertyChanged += (_, e) =>
            changed.Add(e.PropertyName ?? "");

        settings.InsightsEnabled = true;
        svc.Refresh();

        Assert.Contains(nameof(ICortexAmbientService.AnyFeatureEnabled), changed);
        Assert.Contains(nameof(ICortexAmbientService.Activeness), changed);
    }

    [Fact]
    public void Refresh_FirstEnable_StampsAIFirstEnabledAt()
    {
        var settings = FreshSettings();
        Assert.Null(settings.AIFirstEnabledAt);

        settings.InsightsEnabled = true;
        var svc = new CortexAmbientService(settings);
        svc.Refresh();

        Assert.NotNull(settings.AIFirstEnabledAt);
    }

    [Fact]
    public void Refresh_SubsequentEnable_DoesNotOverwriteAIFirstEnabledAt()
    {
        var original = DateTime.UtcNow.AddDays(-5);
        var settings = FreshSettings(insights: true, firstEnabled: original);
        var svc = new CortexAmbientService(settings);

        settings.RecommendationsEnabled = true;
        svc.Refresh();

        Assert.Equal(original, settings.AIFirstEnabledAt);
    }

    [Fact]
    public void AggregatedStatusText_Active_ContainsLearningDay()
    {
        var settings = FreshSettings(insights: true, firstEnabled: DateTime.UtcNow.AddDays(-2));
        var svc = new CortexAmbientService(settings);

        Assert.Contains("day", svc.AggregatedStatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AggregatedStatusText_Paused_SaysPaused()
    {
        var settings = FreshSettings(firstEnabled: DateTime.UtcNow.AddDays(-1));
        var svc = new CortexAmbientService(settings);

        Assert.Contains("paused", svc.AggregatedStatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AggregatedStatusText_Ready_SaysReady()
    {
        var settings = FreshSettings();
        var svc = new CortexAmbientService(settings);

        Assert.Contains("ready", svc.AggregatedStatusText, StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 8.2: Create ICortexAmbientService interface**

Create `src/UI/AuraCore.UI.Avalonia/Services/AI/ICortexAmbientService.cs`:

```csharp
using System.ComponentModel;

namespace AuraCore.UI.Avalonia.Services.AI;

/// <summary>
/// Aggregated activeness state of CORTEX across all AI features.
/// </summary>
public enum CortexActiveness
{
    /// <summary>At least one feature enabled.</summary>
    Active,
    /// <summary>All features disabled, but at least one has been enabled before.</summary>
    Paused,
    /// <summary>No feature has ever been enabled on this install.</summary>
    Ready
}

/// <summary>
/// Aggregates AI feature toggle state for display across the app (dashboard, status bar, AI Features page).
/// Subscribers bind to PropertyChanged events. Owners call <see cref="Refresh"/> after settings mutations.
/// </summary>
public interface ICortexAmbientService : INotifyPropertyChanged
{
    bool AnyFeatureEnabled { get; }
    int EnabledFeatureCount { get; }
    int TotalFeatureCount { get; }
    int LearningDay { get; }
    CortexActiveness Activeness { get; }
    string AggregatedStatusText { get; }

    /// <summary>
    /// Recomputes state from current settings and fires PropertyChanged.
    /// Call after any feature toggle change.
    /// </summary>
    void Refresh();
}
```

- [ ] **Step 8.3: Create CortexAmbientService implementation**

Create `src/UI/AuraCore.UI.Avalonia/Services/AI/CortexAmbientService.cs`:

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AuraCore.UI.Avalonia.Services.AI;

public sealed class CortexAmbientService : ICortexAmbientService
{
    private readonly AppSettings _settings;

    private bool _anyFeatureEnabled;
    private int _enabledFeatureCount;
    private int _learningDay;
    private CortexActiveness _activeness;
    private string _aggregatedStatusText = "";

    public CortexAmbientService(AppSettings settings)
    {
        _settings = settings;
        Recompute();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool AnyFeatureEnabled => _anyFeatureEnabled;
    public int EnabledFeatureCount => _enabledFeatureCount;
    public int TotalFeatureCount => 4;
    public int LearningDay => _learningDay;
    public CortexActiveness Activeness => _activeness;
    public string AggregatedStatusText => _aggregatedStatusText;

    public void Refresh()
    {
        var prevAny = _anyFeatureEnabled;
        var prevCount = _enabledFeatureCount;
        var prevDay = _learningDay;
        var prevActiveness = _activeness;
        var prevText = _aggregatedStatusText;

        // Stamp AIFirstEnabledAt on first transition to enabled
        var anyNow = _settings.InsightsEnabled || _settings.RecommendationsEnabled
                     || _settings.ScheduleEnabled || _settings.ChatEnabled;
        if (anyNow && _settings.AIFirstEnabledAt is null)
        {
            _settings.AIFirstEnabledAt = DateTime.UtcNow;
            _settings.Save();
        }

        Recompute();

        if (prevAny != _anyFeatureEnabled) Fire(nameof(AnyFeatureEnabled));
        if (prevCount != _enabledFeatureCount) Fire(nameof(EnabledFeatureCount));
        if (prevDay != _learningDay) Fire(nameof(LearningDay));
        if (prevActiveness != _activeness) Fire(nameof(Activeness));
        if (prevText != _aggregatedStatusText) Fire(nameof(AggregatedStatusText));
    }

    private void Recompute()
    {
        _enabledFeatureCount = BoolToInt(_settings.InsightsEnabled)
                             + BoolToInt(_settings.RecommendationsEnabled)
                             + BoolToInt(_settings.ScheduleEnabled)
                             + BoolToInt(_settings.ChatEnabled);
        _anyFeatureEnabled = _enabledFeatureCount > 0;

        _learningDay = ComputeLearningDay(_settings.AIFirstEnabledAt);
        _activeness = ComputeActiveness();
        _aggregatedStatusText = ComputeStatusText();
    }

    private static int BoolToInt(bool b) => b ? 1 : 0;

    private static int ComputeLearningDay(DateTime? firstEnabledAt)
    {
        if (firstEnabledAt is null) return 0;
        var days = (int)(DateTime.UtcNow - firstEnabledAt.Value).TotalDays;
        return Math.Max(0, days);
    }

    private CortexActiveness ComputeActiveness()
    {
        if (_anyFeatureEnabled) return CortexActiveness.Active;
        if (_settings.AIFirstEnabledAt is not null) return CortexActiveness.Paused;
        return CortexActiveness.Ready;
    }

    private string ComputeStatusText() => _activeness switch
    {
        CortexActiveness.Active => $"Active · Learning day {Math.Max(1, _learningDay)}",
        CortexActiveness.Paused => "Paused",
        _ => "Ready to start",
    };

    private void Fire([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
```

> **Note on localization:** `ComputeStatusText` returns hardcoded English for now. Task 50 (localization) wires it to `LocalizationService` — the service will hold a `Func<CortexActiveness, int, string>` formatter injected at construction time, swapped for the localized version. For Task 8, hardcoded English is acceptable since tests check substring match.

- [ ] **Step 8.4: Run tests to verify they pass**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~CortexAmbientServiceTests" --verbosity minimal
```
Expected: `Passed: 13, Failed: 0`.

- [ ] **Step 8.5: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Services/AI/ICortexAmbientService.cs src/UI/AuraCore.UI.Avalonia/Services/AI/CortexAmbientService.cs tests/AuraCore.Tests.UI.Avalonia/Services/AI/CortexAmbientServiceTests.cs
git commit -m "feat(ai): add ICortexAmbientService + CortexAmbientService (state aggregation + PropertyChanged + learning-day)"
```

---

## Task 9: Create ITierService + TierService + UserTier enum

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Services/AI/ITierService.cs`
- Create: `src/UI/AuraCore.UI.Avalonia/Services/AI/TierService.cs`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Services/AI/TierServiceTests.cs`

Spec §8.2 + Risk R8. Conservative defaults since pre-Phase-2 mapping may be lost: all current modules = Free, `admin-panel` = Admin.

- [ ] **Step 9.1: Git archaeology for old TierService code**

Run:
```bash
git log --all --oneline -- "*Tier*" 2>&1 | head -20
git log --all --oneline -- "*ApplyTierLocking*" 2>&1 | head -10
```

If results found: inspect them with `git show <commit>` for original logic. Carry forward any module → required-tier mappings you find.

If no results: proceed with the conservative minimal mapping below.

- [ ] **Step 9.2: Write failing tests**

Create `tests/AuraCore.Tests.UI.Avalonia/Services/AI/TierServiceTests.cs`:

```csharp
using AuraCore.UI.Avalonia.Services.AI;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Services.AI;

public class TierServiceTests
{
    [Fact]
    public void IsModuleLocked_AdminTier_AllUnlocked()
    {
        var svc = new TierService();
        Assert.False(svc.IsModuleLocked("admin-panel", UserTier.Admin));
        Assert.False(svc.IsModuleLocked("junk-cleaner", UserTier.Admin));
    }

    [Fact]
    public void IsModuleLocked_FreeTier_AdminPanelIsLocked()
    {
        var svc = new TierService();
        Assert.True(svc.IsModuleLocked("admin-panel", UserTier.Free));
    }

    [Fact]
    public void IsModuleLocked_ProTier_AdminPanelIsLocked()
    {
        var svc = new TierService();
        Assert.True(svc.IsModuleLocked("admin-panel", UserTier.Pro));
    }

    [Fact]
    public void IsModuleLocked_EnterpriseTier_AdminPanelIsLocked()
    {
        var svc = new TierService();
        Assert.True(svc.IsModuleLocked("admin-panel", UserTier.Enterprise));
    }

    [Fact]
    public void IsModuleLocked_FreeTier_BasicModulesUnlocked()
    {
        var svc = new TierService();
        Assert.False(svc.IsModuleLocked("junk-cleaner", UserTier.Free));
        Assert.False(svc.IsModuleLocked("ram-optimizer", UserTier.Free));
        Assert.False(svc.IsModuleLocked("dashboard", UserTier.Free));
    }

    [Fact]
    public void IsModuleLocked_UnknownModule_DefaultsToUnlocked()
    {
        var svc = new TierService();
        Assert.False(svc.IsModuleLocked("brand-new-module", UserTier.Free));
    }

    [Fact]
    public void GetRequiredTier_AdminPanel_ReturnsAdmin()
    {
        var svc = new TierService();
        Assert.Equal(UserTier.Admin, svc.GetRequiredTier("admin-panel"));
    }

    [Fact]
    public void GetRequiredTier_UnmappedModule_ReturnsFree()
    {
        var svc = new TierService();
        Assert.Equal(UserTier.Free, svc.GetRequiredTier("random-module"));
    }
}
```

- [ ] **Step 9.3: Create ITierService + UserTier**

Create `src/UI/AuraCore.UI.Avalonia/Services/AI/ITierService.cs`:

```csharp
namespace AuraCore.UI.Avalonia.Services.AI;

/// <summary>
/// User license tier. Determines which modules are unlocked.
/// </summary>
public enum UserTier
{
    Free,
    Pro,
    Enterprise,
    Admin
}

/// <summary>
/// Determines whether a module is accessible to a given user tier.
/// Phase 3: conservative minimal mapping (admin-panel requires Admin).
/// Future: load mapping from license / server.
/// </summary>
public interface ITierService
{
    bool IsModuleLocked(string moduleKey, UserTier userTier);
    UserTier GetRequiredTier(string moduleKey);
}
```

- [ ] **Step 9.4: Create TierService implementation**

Create `src/UI/AuraCore.UI.Avalonia/Services/AI/TierService.cs`:

```csharp
namespace AuraCore.UI.Avalonia.Services.AI;

public sealed class TierService : ITierService
{
    /// <summary>
    /// Module → required tier mapping. Unlisted modules default to Free (unlocked for everyone).
    /// Carry-forward of pre-Phase-2 ApplyTierLocking mapping (if recovered from git history,
    /// replace this dictionary). Otherwise, conservative Phase 3 seed values.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, UserTier> _requiredTiers =
        new Dictionary<string, UserTier>(StringComparer.OrdinalIgnoreCase)
        {
            ["admin-panel"] = UserTier.Admin,
            // Add more mappings here as premium features are gated.
        };

    public bool IsModuleLocked(string moduleKey, UserTier userTier)
    {
        if (userTier == UserTier.Admin) return false; // admins see everything

        var required = GetRequiredTier(moduleKey);
        return (int)userTier < (int)required;
    }

    public UserTier GetRequiredTier(string moduleKey)
    {
        return _requiredTiers.TryGetValue(moduleKey, out var tier)
            ? tier
            : UserTier.Free;
    }
}
```

- [ ] **Step 9.5: Run tests to verify they pass**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~TierServiceTests" --verbosity minimal
```
Expected: `Passed: 8, Failed: 0`.

- [ ] **Step 9.6: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Services/AI/ITierService.cs src/UI/AuraCore.UI.Avalonia/Services/AI/TierService.cs tests/AuraCore.Tests.UI.Avalonia/Services/AI/TierServiceTests.cs
git commit -m "feat(ai): add ITierService + TierService with conservative mapping (admin-panel = Admin tier)"
```

---

## Task 10: Register Phase 3 services + HttpClient in DI

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/App.axaml.cs`

Spec §4.9. Add registrations after existing module registrations in `OnFrameworkInitializationCompleted`.

- [ ] **Step 10.1: Locate registration block in App.axaml.cs**

Open `src/UI/AuraCore.UI.Avalonia/App.axaml.cs`. Find `OnFrameworkInitializationCompleted()` and scroll past the `if (OperatingSystem.IsMacOS())` module block. The new registrations go **after** all module registrations but **before** `_services = sc.BuildServiceProvider()`.

Run this to find the build line:
```bash
grep -n "BuildServiceProvider" src/UI/AuraCore.UI.Avalonia/App.axaml.cs
```
Expected: one line, note the line number.

- [ ] **Step 10.2: Add Phase 3 service registrations**

Edit `App.axaml.cs`. Just before `_services = sc.BuildServiceProvider();`, insert:

```csharp
        // ── Phase 3: AI Features services ──
        // Model catalog + installed models (singletons — read-only / cross-session state)
        sc.AddSingleton<global::AuraCore.UI.Avalonia.Services.AI.IModelCatalog,
                        global::AuraCore.UI.Avalonia.Services.AI.ModelCatalog>();
        sc.AddSingleton<global::AuraCore.UI.Avalonia.Services.AI.IInstalledModelStore>(
            sp => new global::AuraCore.UI.Avalonia.Services.AI.InstalledModelStore(
                sp.GetRequiredService<global::AuraCore.UI.Avalonia.Services.AI.IModelCatalog>()));

        // App settings (single instance — loaded from disk at startup)
        sc.AddSingleton<global::AuraCore.UI.Avalonia.AppSettings>(
            _ => global::AuraCore.UI.Avalonia.AppSettings.Load());

        // Ambient CORTEX state aggregator
        sc.AddSingleton<global::AuraCore.UI.Avalonia.Services.AI.ICortexAmbientService,
                        global::AuraCore.UI.Avalonia.Services.AI.CortexAmbientService>();

        // Tier service for sidebar IsLocked
        sc.AddSingleton<global::AuraCore.UI.Avalonia.Services.AI.ITierService,
                        global::AuraCore.UI.Avalonia.Services.AI.TierService>();

        // HttpClient for model downloads — configured with User-Agent to bypass Bot Fight Mode
        sc.AddSingleton<global::System.Net.Http.HttpClient>(_ =>
        {
            var client = new global::System.Net.Http.HttpClient
            {
                Timeout = TimeSpan.FromMinutes(30),
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AuraCorePro/1.0 (+https://auracore.pro)");
            return client;
        });

        // Download settings (consumed by ModelDownloadService)
        sc.AddSingleton<global::AuraCore.UI.Avalonia.Services.AI.ModelDownloadSettings>(_ =>
            new global::AuraCore.UI.Avalonia.Services.AI.ModelDownloadSettings(
                BaseUrl: "https://models.auracore.pro",
                InstallDirectory: global::AuraCore.UI.Avalonia.Services.AI.InstalledModelStore.DefaultInstallDir(),
                TimeoutMinutes: 30,
                BufferKb: 256,
                UserAgent: "AuraCorePro/1.0 (+https://auracore.pro)"));

        sc.AddTransient<global::AuraCore.UI.Avalonia.Services.AI.IModelDownloadService,
                        global::AuraCore.UI.Avalonia.Services.AI.ModelDownloadService>();
        // ── end Phase 3 ──
```

Note: `global::` prefixes defend against the `AuraCore.Application` namespace shadow (Phase 1 quirk #1).

- [ ] **Step 10.3: Build to verify registrations compile**

Run:
```bash
dotnet build src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj --configuration Debug --verbosity minimal
```
Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 10.4: Smoke test — resolve services from App.Services**

Run the app briefly to verify DI graph resolves:
```bash
dotnet run --project src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj --configuration Debug &
sleep 3
# Windows: taskkill /F /IM AuraCore.Pro.exe
# Linux: pkill -f AuraCore.Pro
```
Expected: app launches without DI resolution errors. Dashboard renders.

Alternatively write a one-off test that resolves each service:

```csharp
// tests/AuraCore.Tests.UI.Avalonia/DependencyInjectionSmokeTests.cs
using AuraCore.UI.Avalonia.Services.AI;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia;

public class DependencyInjectionSmokeTests
{
    [Fact]
    public void AllPhase3Services_ResolveFromContainer()
    {
        // Emulate App.axaml.cs registrations
        var sc = new ServiceCollection();
        sc.AddSingleton<IModelCatalog, ModelCatalog>();
        sc.AddSingleton<IInstalledModelStore>(sp => new InstalledModelStore(sp.GetRequiredService<IModelCatalog>()));
        sc.AddSingleton<AppSettings>(_ => new AppSettings());
        sc.AddSingleton<ICortexAmbientService, CortexAmbientService>();
        sc.AddSingleton<ITierService, TierService>();
        sc.AddSingleton(new ModelDownloadSettings("https://test", Path.GetTempPath(), 30, 256, "Test/1.0"));
        sc.AddSingleton<System.Net.Http.HttpClient>(_ => new System.Net.Http.HttpClient());
        sc.AddTransient<IModelDownloadService, ModelDownloadService>();

        using var sp = sc.BuildServiceProvider();

        Assert.NotNull(sp.GetRequiredService<IModelCatalog>());
        Assert.NotNull(sp.GetRequiredService<IInstalledModelStore>());
        Assert.NotNull(sp.GetRequiredService<AppSettings>());
        Assert.NotNull(sp.GetRequiredService<ICortexAmbientService>());
        Assert.NotNull(sp.GetRequiredService<ITierService>());
        Assert.NotNull(sp.GetRequiredService<IModelDownloadService>());
    }
}
```

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~DependencyInjectionSmokeTests" --verbosity minimal
```
Expected: `Passed: 1, Failed: 0`.

- [ ] **Step 10.5: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/App.axaml.cs tests/AuraCore.Tests.UI.Avalonia/DependencyInjectionSmokeTests.cs
git commit -m "feat(di): register Phase 3 AI services (catalog, download, installed store, ambient, tier, settings)"
```

---

## Task 11: Delete AIModelSettings + update SettingsView (remove model picker)

**Files:**
- Delete: `src/UI/AuraCore.UI.Avalonia/AIModelSettings.cs`
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/SettingsView.axaml[.cs]`

Per reconciliation decision 1. `AIModelSettings` is replaced by `IModelCatalog` + `IAppSettings.ActiveChatModelId`; its settings picker in SettingsView is removed (Settings > Models page is Phase 4+).

- [ ] **Step 11.1: Inspect SettingsView.axaml for the model picker block**

Run:
```bash
grep -n "AIModelSettings\|ModelDropdown\|model" src/UI/AuraCore.UI.Avalonia/Views/Pages/SettingsView.axaml.cs
```

Note line numbers of code that references `AIModelSettings`. Those need removal.

Also run:
```bash
grep -n "Model\|model" src/UI/AuraCore.UI.Avalonia/Views/Pages/SettingsView.axaml
```

Find the XAML section for the model dropdown (likely a `<ComboBox>` or similar).

- [ ] **Step 11.2: Remove AIModelSettings references from SettingsView.axaml.cs**

Open `src/UI/AuraCore.UI.Avalonia/Views/Pages/SettingsView.axaml.cs`. Delete:
1. The line `AIModelSettings.Load();` (around line 40 per earlier inspection).
2. The loop that populates the dropdown from `AIModelSettings.AvailableModels` (lines ~53-60).
3. The save handler block using `AIModelSettings.Save(model.Key)` (lines ~128-134).

Replace the dropdown population with a simple info text (rendered in next step), or remove the handler entirely if the dropdown XAML is also removed.

- [ ] **Step 11.3: Remove model picker from SettingsView.axaml**

Delete the `<ComboBox>` (or equivalent control) used for model selection. Replace with a read-only info row pointing users to AI Features:

```xml
<!-- Replaces the removed AI model picker -->
<Border Classes="info-card" Margin="0,12,0,0">
  <StackPanel Orientation="Horizontal" Spacing="10">
    <PathIcon Data="{StaticResource IconSparklesFilled}" Width="16" Height="16"
              Foreground="{DynamicResource AccentPurple}" />
    <TextBlock VerticalAlignment="Center"
               Text="AI models are managed in AI Features → Chat. Open Chat to download or switch models."
               TextWrapping="Wrap" />
  </StackPanel>
</Border>
```

- [ ] **Step 11.4: Delete AIModelSettings.cs**

Run:
```bash
git rm src/UI/AuraCore.UI.Avalonia/AIModelSettings.cs
```

- [ ] **Step 11.5: Remove any remaining references**

Run:
```bash
grep -rn "AIModelSettings" src/ --include="*.cs" --include="*.axaml" 2>&1 | grep -v "bin/\|obj/"
```
Expected: no results. If any remain, remove them (likely in a `using` statement or unused import).

- [ ] **Step 11.6: Build to verify compilation**

Run:
```bash
dotnet build src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj --configuration Debug --verbosity minimal
```
Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 11.7: Run full suite to verify no regressions**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --verbosity minimal 2>&1 | tail -5
```
Expected: all previous tests still pass.

- [ ] **Step 11.8: Commit**

```bash
git add -A src/UI/AuraCore.UI.Avalonia/Views/Pages/SettingsView.axaml src/UI/AuraCore.UI.Avalonia/Views/Pages/SettingsView.axaml.cs
git commit -m "refactor(settings): delete AIModelSettings + remove model picker (moved to AIFeaturesView > Chat)"
```

---

## Task 12: Create AIFeatureCardVM

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/ViewModels/AIFeatureCardVM.cs`
- Create: `tests/AuraCore.Tests.UI.Avalonia/ViewModels/AIFeatureCardVMTests.cs`

Spec §4.4. Per-card data + toggle state, bound to AppSettings.

- [ ] **Step 12.1: Write failing tests**

Create `tests/AuraCore.Tests.UI.Avalonia/ViewModels/AIFeatureCardVMTests.cs`:

```csharp
using AuraCore.UI.Avalonia.ViewModels;
using System.ComponentModel;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.ViewModels;

public class AIFeatureCardVMTests
{
    [Fact]
    public void Construct_WithInitialValues_ExposesThem()
    {
        var vm = new AIFeatureCardVM(
            key: "insights",
            title: "Cortex Insights",
            accentColor: "AccentPurple",
            iconKey: "IconSparklesFilled",
            isChatExperimental: false);

        Assert.Equal("insights", vm.Key);
        Assert.Equal("Cortex Insights", vm.Title);
        Assert.Equal("AccentPurple", vm.AccentColor);
        Assert.Equal("IconSparklesFilled", vm.IconKey);
        Assert.False(vm.IsChatExperimental);
    }

    [Fact]
    public void IsEnabled_DefaultFalse_CanBeSet()
    {
        var vm = new AIFeatureCardVM("insights", "T", "A", "I", false);
        Assert.False(vm.IsEnabled);

        vm.IsEnabled = true;

        Assert.True(vm.IsEnabled);
    }

    [Fact]
    public void IsEnabled_Change_FiresPropertyChanged()
    {
        var vm = new AIFeatureCardVM("insights", "T", "A", "I", false);
        var fired = new List<string>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => fired.Add(e.PropertyName ?? "");

        vm.IsEnabled = true;

        Assert.Contains(nameof(AIFeatureCardVM.IsEnabled), fired);
    }

    [Fact]
    public void PreviewSummary_Settable()
    {
        var vm = new AIFeatureCardVM("insights", "T", "A", "I", false);
        vm.PreviewSummary = "3 active";

        Assert.Equal("3 active", vm.PreviewSummary);
    }

    [Fact]
    public void HighlightText_NullableSettable()
    {
        var vm = new AIFeatureCardVM("insights", "T", "A", "I", false);
        vm.HighlightText = "Spike detected";
        vm.HighlightIcon = "⚠";

        Assert.Equal("Spike detected", vm.HighlightText);
        Assert.Equal("⚠", vm.HighlightIcon);
    }

    [Fact]
    public void IsChatExperimental_TrueForChat()
    {
        var vm = new AIFeatureCardVM("chat", "Chat", "AccentPink", "IconMessageSquare", true);
        Assert.True(vm.IsChatExperimental);
    }
}
```

- [ ] **Step 12.2: Create AIFeatureCardVM**

Create `src/UI/AuraCore.UI.Avalonia/ViewModels/AIFeatureCardVM.cs`:

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace AuraCore.UI.Avalonia.ViewModels;

/// <summary>
/// View-model for a single feature card shown in the AIFeaturesView overview grid.
/// Four instances: Insights, Recommendations, Schedule, Chat.
/// </summary>
public sealed class AIFeatureCardVM : INotifyPropertyChanged
{
    public AIFeatureCardVM(
        string key,
        string title,
        string accentColor,
        string iconKey,
        bool isChatExperimental)
    {
        Key = key;
        Title = title;
        AccentColor = accentColor;
        IconKey = iconKey;
        IsChatExperimental = isChatExperimental;
    }

    /// <summary>Stable identifier: "insights" | "recommendations" | "schedule" | "chat".</summary>
    public string Key { get; }

    /// <summary>Localized title displayed in the card.</summary>
    public string Title { get; }

    /// <summary>Resource key for the accent color brush (e.g. "AccentPurple").</summary>
    public string AccentColor { get; }

    /// <summary>Resource key for the icon geometry.</summary>
    public string IconKey { get; }

    /// <summary>True only for the Chat card — shows EXPERIMENTAL badge.</summary>
    public bool IsChatExperimental { get; }

    // Observable properties:
    private bool _isEnabled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProp(ref _isEnabled, value);
    }

    private string _previewSummary = "";
    public string PreviewSummary
    {
        get => _previewSummary;
        set => SetProp(ref _previewSummary, value);
    }

    private string? _highlightText;
    public string? HighlightText
    {
        get => _highlightText;
        set => SetProp(ref _highlightText, value);
    }

    private string? _highlightIcon;
    public string? HighlightIcon
    {
        get => _highlightIcon;
        set => SetProp(ref _highlightIcon, value);
    }

    /// <summary>Command fired when the card body (not the toggle) is clicked.
    /// Wired by AIFeaturesViewModel to navigate to the detail section.</summary>
    public ICommand? NavigateToDetail { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetProp<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
```

- [ ] **Step 12.3: Run tests to verify they pass**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~AIFeatureCardVMTests" --verbosity minimal
```
Expected: `Passed: 6, Failed: 0`.

- [ ] **Step 12.4: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/ViewModels/AIFeatureCardVM.cs tests/AuraCore.Tests.UI.Avalonia/ViewModels/AIFeatureCardVMTests.cs
git commit -m "feat(vm): add AIFeatureCardVM (per-card overview data + toggle state)"
```

---

## Task 13: Create AIFeaturesViewModel (Overview/Detail mode + navigation)

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/ViewModels/AIFeaturesViewModel.cs`
- Create: `tests/AuraCore.Tests.UI.Avalonia/ViewModels/AIFeaturesViewModelTests.cs`

Spec §4.2. Owns 4 AIFeatureCardVMs, handles Overview/Detail mode switching, wires toggle → AppSettings → CortexAmbientService.Refresh.

- [ ] **Step 13.1: Write failing tests**

Create `tests/AuraCore.Tests.UI.Avalonia/ViewModels/AIFeaturesViewModelTests.cs`:

```csharp
using AuraCore.UI.Avalonia;
using AuraCore.UI.Avalonia.Services.AI;
using AuraCore.UI.Avalonia.ViewModels;
using System.ComponentModel;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.ViewModels;

public class AIFeaturesViewModelTests
{
    private AIFeaturesViewModel CreateVM(
        bool insights = true, bool recs = true,
        bool schedule = true, bool chat = false)
    {
        var settings = new AppSettings
        {
            InsightsEnabled = insights,
            RecommendationsEnabled = recs,
            ScheduleEnabled = schedule,
            ChatEnabled = chat,
        };
        var ambient = new CortexAmbientService(settings);
        return new AIFeaturesViewModel(settings, ambient);
    }

    [Fact]
    public void Initialize_StartsInOverviewMode()
    {
        var vm = CreateVM();
        Assert.Equal(AIFeaturesViewMode.Overview, vm.Mode);
        Assert.True(vm.IsOverview);
        Assert.False(vm.IsDetail);
        Assert.Equal("overview", vm.ActiveSection);
    }

    [Fact]
    public void NavigateToSection_ChangesMode()
    {
        var vm = CreateVM();
        vm.NavigateToSection.Execute("insights");

        Assert.Equal(AIFeaturesViewMode.Detail, vm.Mode);
        Assert.Equal("insights", vm.ActiveSection);
    }

    [Fact]
    public void NavigateToOverview_ReturnsToGrid()
    {
        var vm = CreateVM();
        vm.NavigateToSection.Execute("insights");

        vm.NavigateToOverview.Execute(null);

        Assert.Equal(AIFeaturesViewMode.Overview, vm.Mode);
        Assert.Equal("overview", vm.ActiveSection);
    }

    [Fact]
    public void FourCards_Exist()
    {
        var vm = CreateVM();

        Assert.NotNull(vm.InsightsCard);
        Assert.NotNull(vm.RecommendationsCard);
        Assert.NotNull(vm.ScheduleCard);
        Assert.NotNull(vm.ChatCard);
        Assert.Equal("insights", vm.InsightsCard.Key);
        Assert.Equal("recommendations", vm.RecommendationsCard.Key);
        Assert.Equal("schedule", vm.ScheduleCard.Key);
        Assert.Equal("chat", vm.ChatCard.Key);
    }

    [Fact]
    public void ChatCard_IsChatExperimental_True()
    {
        var vm = CreateVM();
        Assert.True(vm.ChatCard.IsChatExperimental);
        Assert.False(vm.InsightsCard.IsChatExperimental);
    }

    [Fact]
    public void TogglingCard_UpdatesSettings()
    {
        var vm = CreateVM(insights: true);
        vm.InsightsCard.IsEnabled = false;

        Assert.False(vm.InsightsCard.IsEnabled);
        // Settings flag flipped via internal wire-up
    }

    [Fact]
    public void HeroStatusText_AllEnabled_ContainsActive()
    {
        var vm = CreateVM(insights: true);

        Assert.Contains("Active", vm.HeroStatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HeroStatusText_AllDisabled_ContainsReadyOrPaused()
    {
        var vm = CreateVM(insights: false, recs: false, schedule: false, chat: false);

        var text = vm.HeroStatusText.ToLowerInvariant();
        Assert.True(text.Contains("ready") || text.Contains("paused"));
    }

    [Fact]
    public void NavigateToSection_FiresPropertyChanged_ForModeAndIsOverview()
    {
        var vm = CreateVM();
        var fired = new List<string>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => fired.Add(e.PropertyName ?? "");

        vm.NavigateToSection.Execute("insights");

        Assert.Contains(nameof(AIFeaturesViewModel.Mode), fired);
        Assert.Contains(nameof(AIFeaturesViewModel.IsOverview), fired);
        Assert.Contains(nameof(AIFeaturesViewModel.IsDetail), fired);
        Assert.Contains(nameof(AIFeaturesViewModel.ActiveSection), fired);
    }

    [Fact]
    public void ActiveSectionView_CachedAcrossNavigation()
    {
        var vm = CreateVM();
        vm.NavigateToSection.Execute("insights");
        var view1 = vm.ActiveSectionView;

        vm.NavigateToOverview.Execute(null);
        vm.NavigateToSection.Execute("insights");
        var view2 = vm.ActiveSectionView;

        // Same instance — state preserved across back-and-forth
        Assert.Same(view1, view2);
    }
}
```

- [ ] **Step 13.2: Create AIFeaturesViewMode enum + AIFeaturesViewModel**

Create `src/UI/AuraCore.UI.Avalonia/ViewModels/AIFeaturesViewModel.cs`:

```csharp
using AuraCore.UI.Avalonia.Services.AI;
using global::Avalonia.Controls;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace AuraCore.UI.Avalonia.ViewModels;

public enum AIFeaturesViewMode { Overview, Detail }

/// <summary>
/// Primary view-model for AIFeaturesView (spec §4.2).
/// Owns the 4 feature cards, manages Overview/Detail mode, wires toggle changes to settings + ambient service.
/// Section UserControl instances are cached to preserve state across navigation.
/// </summary>
public sealed class AIFeaturesViewModel : INotifyPropertyChanged
{
    private readonly AppSettings _settings;
    private readonly ICortexAmbientService _ambient;
    private readonly Dictionary<string, UserControl> _sectionViewCache = new();

    public AIFeaturesViewModel(AppSettings settings, ICortexAmbientService ambient)
    {
        _settings = settings;
        _ambient = ambient;

        InsightsCard = new AIFeatureCardVM(
            key: "insights",
            title: "Cortex Insights",
            accentColor: "AccentPurple",
            iconKey: "IconSparklesFilled",
            isChatExperimental: false) { IsEnabled = settings.InsightsEnabled };

        RecommendationsCard = new AIFeatureCardVM(
            key: "recommendations",
            title: "Recommendations",
            accentColor: "AccentTeal",
            iconKey: "IconLightbulb",
            isChatExperimental: false) { IsEnabled = settings.RecommendationsEnabled };

        ScheduleCard = new AIFeatureCardVM(
            key: "schedule",
            title: "Smart Schedule",
            accentColor: "AccentAmber",
            iconKey: "IconCalendarClock",
            isChatExperimental: false) { IsEnabled = settings.ScheduleEnabled };

        ChatCard = new AIFeatureCardVM(
            key: "chat",
            title: "Chat",
            accentColor: "AccentPink",
            iconKey: "IconMessageSquare",
            isChatExperimental: true) { IsEnabled = settings.ChatEnabled };

        WireToggleHandlers();

        NavigateToSection = new DelegateCommand<string>(OnNavigateToSection);
        NavigateToOverview = new DelegateCommand<object?>(_ => SetMode(AIFeaturesViewMode.Overview, "overview"));

        // Propagate ambient PropertyChanged → hero status text
        _ambient.PropertyChanged += (_, _) => OnPropertyChanged(nameof(HeroStatusText));
    }

    public AIFeatureCardVM InsightsCard { get; }
    public AIFeatureCardVM RecommendationsCard { get; }
    public AIFeatureCardVM ScheduleCard { get; }
    public AIFeatureCardVM ChatCard { get; }

    private AIFeaturesViewMode _mode = AIFeaturesViewMode.Overview;
    public AIFeaturesViewMode Mode
    {
        get => _mode;
        private set
        {
            if (_mode == value) return;
            _mode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsOverview));
            OnPropertyChanged(nameof(IsDetail));
        }
    }

    public bool IsOverview => _mode == AIFeaturesViewMode.Overview;
    public bool IsDetail => _mode == AIFeaturesViewMode.Detail;

    private string _activeSection = "overview";
    public string ActiveSection
    {
        get => _activeSection;
        private set
        {
            if (_activeSection == value) return;
            _activeSection = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ActiveSectionView));
        }
    }

    public string HeroStatusText => _ambient.AggregatedStatusText;

    /// <summary>
    /// Lazily-created, cached UserControl for the active section.
    /// Cache key = section name. Null when in Overview mode.
    /// </summary>
    public UserControl? ActiveSectionView
    {
        get
        {
            if (_mode == AIFeaturesViewMode.Overview) return null;
            return GetOrCreateSectionView(_activeSection);
        }
    }

    public ICommand NavigateToSection { get; }
    public ICommand NavigateToOverview { get; }

    /// <summary>
    /// Factory that creates section UserControls on demand.
    /// Swapped by the View's code-behind (via DI) before it's first accessed.
    /// </summary>
    public Func<string, UserControl>? SectionViewFactory { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void WireToggleHandlers()
    {
        InsightsCard.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AIFeatureCardVM.IsEnabled))
            {
                _settings.InsightsEnabled = InsightsCard.IsEnabled;
                _settings.Save();
                _ambient.Refresh();
            }
        };
        RecommendationsCard.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AIFeatureCardVM.IsEnabled))
            {
                _settings.RecommendationsEnabled = RecommendationsCard.IsEnabled;
                _settings.Save();
                _ambient.Refresh();
            }
        };
        ScheduleCard.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AIFeatureCardVM.IsEnabled))
            {
                _settings.ScheduleEnabled = ScheduleCard.IsEnabled;
                _settings.Save();
                _ambient.Refresh();
            }
        };
        ChatCard.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AIFeatureCardVM.IsEnabled))
            {
                _settings.ChatEnabled = ChatCard.IsEnabled;
                _settings.Save();
                _ambient.Refresh();
            }
        };
    }

    private void OnNavigateToSection(string? section)
    {
        if (string.IsNullOrEmpty(section)) return;
        if (section == "overview")
        {
            SetMode(AIFeaturesViewMode.Overview, "overview");
            return;
        }
        SetMode(AIFeaturesViewMode.Detail, section);
    }

    private void SetMode(AIFeaturesViewMode mode, string section)
    {
        ActiveSection = section;
        Mode = mode;
    }

    private UserControl GetOrCreateSectionView(string section)
    {
        if (_sectionViewCache.TryGetValue(section, out var existing))
            return existing;

        if (SectionViewFactory is null)
        {
            // Fallback — empty placeholder. Real factory wired by View's code-behind.
            var placeholder = new UserControl();
            _sectionViewCache[section] = placeholder;
            return placeholder;
        }

        var view = SectionViewFactory(section);
        _sectionViewCache[section] = view;
        return view;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// Minimal ICommand implementation with a typed parameter.
/// Use instead of pulling in a full MVVM framework.
/// </summary>
public sealed class DelegateCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public DelegateCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;
    public void Execute(object? parameter) => _execute((T?)parameter);
    public event EventHandler? CanExecuteChanged;
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
```

- [ ] **Step 13.3: Run tests to verify they pass**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~AIFeaturesViewModelTests" --verbosity minimal
```
Expected: `Passed: 10, Failed: 0`. If any fail related to `ActiveSectionView` caching, ensure `SectionViewFactory` returns a new `UserControl` instance for each distinct `section` key (the test relies on cache — same section returns same instance).

- [ ] **Step 13.4: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/ViewModels/AIFeaturesViewModel.cs tests/AuraCore.Tests.UI.Avalonia/ViewModels/AIFeaturesViewModelTests.cs
git commit -m "feat(vm): add AIFeaturesViewModel (Overview/Detail modes + 4 cards + cached section views)"
```

---

## Task 14: Create AIFeatureCard UserControl (reusable card)

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Controls/AIFeatureCard.axaml`
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Controls/AIFeatureCard.axaml.cs`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Views/AIFeatureCardTests.cs`

Spec §4.4. GlassCard-based card, binds to `AIFeatureCardVM`. One instance per feature in the overview grid.

- [ ] **Step 14.1: Write failing test**

Create `tests/AuraCore.Tests.UI.Avalonia/Views/AIFeatureCardTests.cs`:

```csharp
using AuraCore.UI.Avalonia.ViewModels;
using AuraCore.UI.Avalonia.Views.Controls;
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

public class AIFeatureCardTests : AvaloniaTestBase
{
    [AvaloniaFact]
    public void AIFeatureCard_BindsToViewModel_RendersTitle()
    {
        var vm = new AIFeatureCardVM("insights", "Cortex Insights", "AccentPurple", "IconSparklesFilled", false)
        {
            IsEnabled = true,
            PreviewSummary = "3 active · 1 warning",
        };

        var card = new AIFeatureCard { DataContext = vm };
        using var window = new TestWindowHandle(card);

        card.Measure(new global::Avalonia.Size(400, 300));
        card.Arrange(new global::Avalonia.Rect(0, 0, 400, 300));

        var title = card.FindControl<TextBlock>("PART_Title");
        Assert.NotNull(title);
        Assert.Equal("Cortex Insights", title!.Text);
    }

    [AvaloniaFact]
    public void AIFeatureCard_ExperimentalCard_ShowsBadge()
    {
        var vm = new AIFeatureCardVM("chat", "Chat", "AccentPink", "IconMessageSquare", isChatExperimental: true);
        var card = new AIFeatureCard { DataContext = vm };
        using var window = new TestWindowHandle(card);

        card.Measure(new global::Avalonia.Size(400, 300));
        card.Arrange(new global::Avalonia.Rect(0, 0, 400, 300));

        var badge = card.FindControl<Control>("PART_ExperimentalBadge");
        Assert.NotNull(badge);
        Assert.True(badge!.IsVisible);
    }

    [AvaloniaFact]
    public void AIFeatureCard_NonExperimental_BadgeHidden()
    {
        var vm = new AIFeatureCardVM("insights", "T", "AccentPurple", "IconSparklesFilled", isChatExperimental: false);
        var card = new AIFeatureCard { DataContext = vm };
        using var window = new TestWindowHandle(card);

        card.Measure(new global::Avalonia.Size(400, 300));
        card.Arrange(new global::Avalonia.Rect(0, 0, 400, 300));

        var badge = card.FindControl<Control>("PART_ExperimentalBadge");
        Assert.False(badge?.IsVisible ?? true);
    }
}
```

- [ ] **Step 14.2: Create AIFeatureCard.axaml**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/AIFeatureCard.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="using:AuraCore.UI.Avalonia.Views.Controls"
             xmlns:vm="using:AuraCore.UI.Avalonia.ViewModels"
             x:Class="AuraCore.UI.Avalonia.Views.Controls.AIFeatureCard"
             x:DataType="vm:AIFeatureCardVM"
             Margin="6">

  <controls:GlassCard Padding="14">
    <Grid RowDefinitions="Auto,Auto,Auto,Auto" ColumnDefinitions="*,Auto">

      <!-- Accent kicker (icon + key uppercase) -->
      <StackPanel Grid.Row="0" Grid.Column="0" Orientation="Horizontal" Spacing="6">
        <PathIcon Width="12" Height="12"
                  Data="{DynamicResource IconSparklesFilled}" />
        <TextBlock x:Name="PART_Kicker"
                   Text="{Binding Title, Converter={x:Static StringConverters.ToUpperInvariant}}"
                   FontSize="9"
                   Foreground="{DynamicResource AccentPurple}"
                   FontWeight="Bold"
                   Classes="label" />
      </StackPanel>

      <!-- Toggle switch (top-right) -->
      <controls:AuraToggle Grid.Row="0" Grid.RowSpan="2" Grid.Column="1"
                           x:Name="PART_Toggle"
                           IsChecked="{Binding IsEnabled, Mode=TwoWay}"
                           HorizontalAlignment="Right"
                           VerticalAlignment="Top" />

      <!-- Experimental badge (chat only) -->
      <controls:AccentBadge Grid.Row="1" Grid.Column="0"
                            x:Name="PART_ExperimentalBadge"
                            Text="EXPERIMENTAL"
                            Accent="Pink"
                            Margin="0,4,0,0"
                            IsVisible="{Binding IsChatExperimental}" />

      <!-- Title -->
      <TextBlock Grid.Row="2" Grid.Column="0" Grid.ColumnSpan="2"
                 x:Name="PART_Title"
                 Text="{Binding Title}"
                 FontSize="15"
                 FontWeight="SemiBold"
                 Foreground="{DynamicResource TextPrimary}"
                 Margin="0,8,0,0" />

      <!-- Preview summary -->
      <TextBlock Grid.Row="3" Grid.Column="0" Grid.ColumnSpan="2"
                 x:Name="PART_Preview"
                 Text="{Binding PreviewSummary}"
                 FontSize="11"
                 Foreground="{DynamicResource TextSecondary}"
                 Margin="0,3,0,0"
                 TextWrapping="Wrap" />

    </Grid>
  </controls:GlassCard>
</UserControl>
```

- [ ] **Step 14.3: Create AIFeatureCard.axaml.cs**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/AIFeatureCard.axaml.cs`:

```csharp
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Markup.Xaml;
using AuraCore.UI.Avalonia.ViewModels;

namespace AuraCore.UI.Avalonia.Views.Controls;

public partial class AIFeatureCard : UserControl
{
    public AIFeatureCard()
    {
        InitializeComponent();
        PointerPressed += OnCardClick;
    }

    private void OnCardClick(object? sender, PointerPressedEventArgs e)
    {
        // Toggle handles its own pointer events; ignore when origin is the toggle.
        if (e.Source is Control src && FindAncestor<AuraToggle>(src) is not null)
            return;

        if (DataContext is AIFeatureCardVM vm && vm.NavigateToDetail?.CanExecute(vm.Key) == true)
            vm.NavigateToDetail.Execute(vm.Key);
    }

    private static T? FindAncestor<T>(Control start) where T : Control
    {
        Control? current = start;
        while (current is not null)
        {
            if (current is T match) return match;
            current = current.Parent as Control;
        }
        return null;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 14.4: Run tests to verify they pass**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~AIFeatureCardTests" --verbosity minimal
```
Expected: `Passed: 3, Failed: 0`.

- [ ] **Step 14.5: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Controls/AIFeatureCard.axaml src/UI/AuraCore.UI.Avalonia/Views/Controls/AIFeatureCard.axaml.cs tests/AuraCore.Tests.UI.Avalonia/Views/AIFeatureCardTests.cs
git commit -m "feat(ui): add AIFeatureCard UserControl (GlassCard + toggle + optional EXPERIMENTAL badge)"
```

---

## Task 15: Create AIFeaturesView.axaml shell (hero + 2×2 overview grid)

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Pages/AIFeaturesView.axaml`
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Pages/AIFeaturesView.axaml.cs`

Spec §4.2 + §4.3. Container view: hero (status-aware) + Overview (grid) + Detail (sidebar + content). Detail section views come in later tasks — for now, the grid is functional, Detail pane is a placeholder.

- [ ] **Step 15.1: Create AIFeaturesView.axaml**

Create `src/UI/AuraCore.UI.Avalonia/Views/Pages/AIFeaturesView.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="using:AuraCore.UI.Avalonia.Views.Controls"
             xmlns:vm="using:AuraCore.UI.Avalonia.ViewModels"
             x:Class="AuraCore.UI.Avalonia.Views.Pages.AIFeaturesView"
             x:DataType="vm:AIFeaturesViewModel">

  <UserControl.Styles>
    <!-- Hero gradient background -->
    <Style Selector="Border.cortex-hero">
      <Setter Property="CornerRadius" Value="12" />
      <Setter Property="Padding" Value="22,18" />
      <Setter Property="Margin" Value="0,0,0,16" />
      <Setter Property="Background">
        <LinearGradientBrush StartPoint="0%,0%" EndPoint="100%,100%">
          <GradientStop Offset="0" Color="#26B088FF" />
          <GradientStop Offset="0.6" Color="#1400D4AA" />
          <GradientStop Offset="1" Color="#00000000" />
        </LinearGradientBrush>
      </Setter>
      <Setter Property="BorderBrush" Value="#38B088FF" />
      <Setter Property="BorderThickness" Value="1" />
    </Style>
  </UserControl.Styles>

  <DockPanel Margin="20,16">

    <!-- CORTEX Hero -->
    <Border DockPanel.Dock="Top" Classes="cortex-hero">
      <Grid ColumnDefinitions="*,Auto" RowDefinitions="Auto,Auto,Auto">
        <TextBlock Grid.Row="0" Grid.Column="0"
                   Text="✦ CORTEX"
                   FontSize="10"
                   FontWeight="SemiBold"
                   Foreground="{DynamicResource AccentPurple}" />

        <controls:StatusChip Grid.Row="0" Grid.Column="1"
                             x:Name="PART_HeroStatusChip"
                             Text="{Binding HeroStatusText}"
                             Accent="Teal" />

        <TextBlock Grid.Row="1" Grid.Column="0" Grid.ColumnSpan="2"
                   x:Name="PART_HeroTitle"
                   Text="AI Features"
                   FontSize="22"
                   FontWeight="SemiBold"
                   Foreground="{DynamicResource TextPrimary}"
                   Margin="0,6,0,0" />

        <TextBlock Grid.Row="2" Grid.Column="0" Grid.ColumnSpan="2"
                   Text="Intelligent monitoring, predictions, and automation"
                   FontSize="11"
                   Foreground="{DynamicResource TextSecondary}"
                   Margin="0,4,0,0" />
      </Grid>
    </Border>

    <!-- Overview / Detail swap -->
    <Panel>

      <!-- Overview mode: 2×2 grid of AIFeatureCards -->
      <UniformGrid Rows="2" Columns="2"
                   x:Name="PART_OverviewGrid"
                   IsVisible="{Binding IsOverview}">
        <controls:AIFeatureCard DataContext="{Binding InsightsCard}" />
        <controls:AIFeatureCard DataContext="{Binding RecommendationsCard}" />
        <controls:AIFeatureCard DataContext="{Binding ScheduleCard}" />
        <controls:AIFeatureCard DataContext="{Binding ChatCard}" />
      </UniformGrid>

      <!-- Detail mode: sidebar + content -->
      <Grid ColumnDefinitions="120,*"
            x:Name="PART_DetailRoot"
            IsVisible="{Binding IsDetail}">

        <StackPanel Grid.Column="0" x:Name="PART_SectionNav" Spacing="2" Margin="0,8,8,0">
          <Button Content="Overview" Classes="section-nav-item"
                  Command="{Binding NavigateToOverview}" />
          <Button Content="Insights" Classes="section-nav-item"
                  Command="{Binding NavigateToSection}"
                  CommandParameter="insights" />
          <Button Content="Recommendations" Classes="section-nav-item"
                  Command="{Binding NavigateToSection}"
                  CommandParameter="recommendations" />
          <Button Content="Schedule" Classes="section-nav-item"
                  Command="{Binding NavigateToSection}"
                  CommandParameter="schedule" />
          <Button Content="Chat ⚠" Classes="section-nav-item"
                  Command="{Binding NavigateToSection}"
                  CommandParameter="chat" />
        </StackPanel>

        <ContentControl Grid.Column="1"
                        x:Name="PART_SectionContent"
                        Content="{Binding ActiveSectionView}" />
      </Grid>
    </Panel>

  </DockPanel>
</UserControl>
```

- [ ] **Step 15.2: Create AIFeaturesView.axaml.cs**

Create `src/UI/AuraCore.UI.Avalonia/Views/Pages/AIFeaturesView.axaml.cs`:

```csharp
using global::Avalonia.Controls;
using global::Avalonia.Markup.Xaml;
using AuraCore.UI.Avalonia.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class AIFeaturesView : UserControl
{
    public AIFeaturesView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Resolve VM from DI (or let DataContext be set externally by caller)
        if (DataContext is not AIFeaturesViewModel vm)
        {
            try
            {
                var settings = App.Services.GetRequiredService<AppSettings>();
                var ambient = App.Services.GetRequiredService<Services.AI.ICortexAmbientService>();
                vm = new AIFeaturesViewModel(settings, ambient);
                DataContext = vm;
            }
            catch
            {
                return; // design-time or DI not available
            }
        }

        // Wire the section-view factory — returns section UserControls on demand.
        // Sections are registered by their dedicated tasks (20-24). Until those
        // are implemented, placeholder UserControls are returned.
        vm.SectionViewFactory = CreateSectionView;

        // Wire card click → navigate
        foreach (var card in new[] { vm.InsightsCard, vm.RecommendationsCard, vm.ScheduleCard, vm.ChatCard })
        {
            card.NavigateToDetail = vm.NavigateToSection;
        }
    }

    /// <summary>
    /// Returns the UserControl for a given section. Tasks 20-24 replace the placeholder
    /// branches with real section controls via DI lookup.
    /// </summary>
    private UserControl CreateSectionView(string section)
    {
        // Phase 3 placeholder — Tasks 20-24 wire real sections here.
        // Example of wiring (after those tasks):
        //   "insights" => App.Services.GetRequiredService<Views.Pages.AI.InsightsSection>(),
        //   "recommendations" => App.Services.GetRequiredService<Views.Pages.AI.RecommendationsSection>(),
        //   "schedule" => App.Services.GetRequiredService<Views.Pages.AI.ScheduleSection>(),
        //   "chat" => App.Services.GetRequiredService<Views.Pages.AI.ChatSection>(),
        return section switch
        {
            _ => new UserControl { Content = new TextBlock { Text = $"[{section}] placeholder — wired in Task 20+" } },
        };
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 15.3: Add styles for `.section-nav-item` to theme**

Open `src/UI/AuraCore.UI.Avalonia/Themes/AuraCoreThemeV2.axaml`. Find the styles section (near the end of the file). Add:

```xml
<!-- AIFeaturesView section navigation buttons -->
<Style Selector="Button.section-nav-item">
  <Setter Property="Padding" Value="10,6" />
  <Setter Property="HorizontalAlignment" Value="Stretch" />
  <Setter Property="HorizontalContentAlignment" Value="Left" />
  <Setter Property="FontSize" Value="11" />
  <Setter Property="Background" Value="Transparent" />
  <Setter Property="BorderThickness" Value="0,0,0,0" />
  <Setter Property="Foreground" Value="{DynamicResource TextSecondary}" />
  <Setter Property="Cursor" Value="Hand" />
</Style>
<Style Selector="Button.section-nav-item:pointerover /template/ ContentPresenter">
  <Setter Property="Background" Value="#10FFFFFF" />
</Style>
```

- [ ] **Step 15.4: Register AIFeaturesView in DI**

Edit `src/UI/AuraCore.UI.Avalonia/App.axaml.cs`. In the Phase 3 block added in Task 10, add:

```csharp
        sc.AddTransient<global::AuraCore.UI.Avalonia.Views.Pages.AIFeaturesView>();
        sc.AddTransient<global::AuraCore.UI.Avalonia.ViewModels.AIFeaturesViewModel>();
```

- [ ] **Step 15.5: Build to verify XAML parses**

Run:
```bash
dotnet build src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj --configuration Debug --verbosity minimal
```
Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 15.6: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Pages/AIFeaturesView.axaml src/UI/AuraCore.UI.Avalonia/Views/Pages/AIFeaturesView.axaml.cs src/UI/AuraCore.UI.Avalonia/Themes/AuraCoreThemeV2.axaml src/UI/AuraCore.UI.Avalonia/App.axaml.cs
git commit -m "feat(ui): add AIFeaturesView shell (hero + 2x2 overview + detail sidebar)"
```

---

## Task 16: AIFeaturesView XAML binding tests

**Files:**
- Create: `tests/AuraCore.Tests.UI.Avalonia/Views/AIFeaturesViewTests.cs`

Smoke test that hero renders, grid renders 4 cards, drill-in swap works.

- [ ] **Step 16.1: Write the tests**

Create `tests/AuraCore.Tests.UI.Avalonia/Views/AIFeaturesViewTests.cs`:

```csharp
using AuraCore.UI.Avalonia;
using AuraCore.UI.Avalonia.Services.AI;
using AuraCore.UI.Avalonia.ViewModels;
using AuraCore.UI.Avalonia.Views.Controls;
using AuraCore.UI.Avalonia.Views.Pages;
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

public class AIFeaturesViewTests : AvaloniaTestBase
{
    private AIFeaturesViewModel BuildVM()
    {
        var settings = new AppSettings { InsightsEnabled = true };
        var ambient = new CortexAmbientService(settings);
        return new AIFeaturesViewModel(settings, ambient);
    }

    [AvaloniaFact]
    public void Hero_Renders_WithTitleAndStatusChip()
    {
        var vm = BuildVM();
        var view = new AIFeaturesView { DataContext = vm };
        using var window = new TestWindowHandle(view);

        view.Measure(new global::Avalonia.Size(1100, 700));
        view.Arrange(new global::Avalonia.Rect(0, 0, 1100, 700));

        Assert.NotNull(view.FindControl<TextBlock>("PART_HeroTitle"));
        Assert.NotNull(view.FindControl<StatusChip>("PART_HeroStatusChip"));
    }

    [AvaloniaFact]
    public void OverviewGrid_Renders_FourCards()
    {
        var vm = BuildVM();
        var view = new AIFeaturesView { DataContext = vm };
        using var window = new TestWindowHandle(view);

        view.Measure(new global::Avalonia.Size(1100, 700));
        view.Arrange(new global::Avalonia.Rect(0, 0, 1100, 700));

        var grid = view.FindControl<UniformGrid>("PART_OverviewGrid");
        Assert.NotNull(grid);
        Assert.True(grid!.IsVisible);
        Assert.Equal(4, grid.Children.Count);
        Assert.All(grid.Children, c => Assert.IsType<AIFeatureCard>(c));
    }

    [AvaloniaFact]
    public void DetailRoot_Hidden_WhenInOverviewMode()
    {
        var vm = BuildVM();
        var view = new AIFeaturesView { DataContext = vm };
        using var window = new TestWindowHandle(view);

        view.Measure(new global::Avalonia.Size(1100, 700));
        view.Arrange(new global::Avalonia.Rect(0, 0, 1100, 700));

        var detailRoot = view.FindControl<Grid>("PART_DetailRoot");
        Assert.False(detailRoot!.IsVisible);
    }

    [AvaloniaFact]
    public void DetailRoot_Visible_AfterNavigateToSection()
    {
        var vm = BuildVM();
        var view = new AIFeaturesView { DataContext = vm };
        using var window = new TestWindowHandle(view);

        vm.NavigateToSection.Execute("insights");

        view.Measure(new global::Avalonia.Size(1100, 700));
        view.Arrange(new global::Avalonia.Rect(0, 0, 1100, 700));

        var detailRoot = view.FindControl<Grid>("PART_DetailRoot");
        var overviewGrid = view.FindControl<UniformGrid>("PART_OverviewGrid");
        Assert.True(detailRoot!.IsVisible);
        Assert.False(overviewGrid!.IsVisible);
    }

    [AvaloniaFact]
    public void BackToOverview_ReshowsGrid()
    {
        var vm = BuildVM();
        var view = new AIFeaturesView { DataContext = vm };
        using var window = new TestWindowHandle(view);

        vm.NavigateToSection.Execute("insights");
        vm.NavigateToOverview.Execute(null);

        view.Measure(new global::Avalonia.Size(1100, 700));
        view.Arrange(new global::Avalonia.Rect(0, 0, 1100, 700));

        var overviewGrid = view.FindControl<UniformGrid>("PART_OverviewGrid");
        Assert.True(overviewGrid!.IsVisible);
    }
}
```

- [ ] **Step 16.2: Run tests**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~AIFeaturesViewTests" --verbosity minimal
```
Expected: `Passed: 5, Failed: 0`.

- [ ] **Step 16.3: Commit**

```bash
git add tests/AuraCore.Tests.UI.Avalonia/Views/AIFeaturesViewTests.cs
git commit -m "test(ui): add AIFeaturesView XAML binding tests (hero, grid, detail swap)"
```

---

## Task 17: Add IsLocked dependency property to SidebarNavItem

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Controls/SidebarNavItem.axaml`
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Controls/SidebarNavItem.axaml.cs`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Views/SidebarNavItemLockTests.cs`

Spec §8.2. Adds `IsLocked` StyledProperty and visual states (opacity + lock icon + NotAllowed cursor).

- [ ] **Step 17.1: Write failing tests**

Create `tests/AuraCore.Tests.UI.Avalonia/Views/SidebarNavItemLockTests.cs`:

```csharp
using AuraCore.UI.Avalonia.Views.Controls;
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

public class SidebarNavItemLockTests : AvaloniaTestBase
{
    [AvaloniaFact]
    public void IsLocked_DefaultsToFalse()
    {
        var item = new SidebarNavItem();
        Assert.False(item.IsLocked);
    }

    [AvaloniaFact]
    public void IsLocked_Set_ReflectsInProperty()
    {
        var item = new SidebarNavItem { IsLocked = true };
        Assert.True(item.IsLocked);
    }

    [AvaloniaFact]
    public void IsLocked_Set_AddsLockedPseudoclass()
    {
        var item = new SidebarNavItem();
        using var window = new TestWindowHandle(item);

        item.IsLocked = true;

        // Check for :locked pseudoclass on classes
        Assert.Contains(":locked", item.Classes);
    }

    [AvaloniaFact]
    public void IsLocked_False_NoLockedPseudoclass()
    {
        var item = new SidebarNavItem();
        using var window = new TestWindowHandle(item);

        Assert.DoesNotContain(":locked", item.Classes);
    }

    [AvaloniaFact]
    public void IsLocked_Set_LockIconVisible()
    {
        var item = new SidebarNavItem { IsLocked = true };
        using var window = new TestWindowHandle(item);

        item.Measure(new global::Avalonia.Size(200, 40));
        item.Arrange(new global::Avalonia.Rect(0, 0, 200, 40));

        var lockIcon = item.FindControl<Control>("PART_LockIcon");
        Assert.NotNull(lockIcon);
        Assert.True(lockIcon!.IsVisible);
    }

    [AvaloniaFact]
    public void IsLocked_False_LockIconHidden()
    {
        var item = new SidebarNavItem();
        using var window = new TestWindowHandle(item);

        item.Measure(new global::Avalonia.Size(200, 40));
        item.Arrange(new global::Avalonia.Rect(0, 0, 200, 40));

        var lockIcon = item.FindControl<Control>("PART_LockIcon");
        Assert.False(lockIcon?.IsVisible ?? true);
    }
}
```

- [ ] **Step 17.2: Add IsLocked property to SidebarNavItem.axaml.cs**

Open `src/UI/AuraCore.UI.Avalonia/Views/Controls/SidebarNavItem.axaml.cs`. Add the new StyledProperty and a handler that manages the `:locked` pseudoclass:

```csharp
// Add near the other StyledProperty declarations
public static readonly global::Avalonia.StyledProperty<bool> IsLockedProperty =
    global::Avalonia.AvaloniaProperty.Register<SidebarNavItem, bool>(nameof(IsLocked));

public bool IsLocked
{
    get => GetValue(IsLockedProperty);
    set => SetValue(IsLockedProperty, value);
}

// In static constructor (or add one if missing) — OR in the instance constructor:
static SidebarNavItem()
{
    IsLockedProperty.Changed.AddClassHandler<SidebarNavItem>((ctrl, e) =>
    {
        ctrl.PseudoClasses.Set(":locked", e.NewValue is true);
    });
}
```

> If a static constructor already exists, merge the `IsLockedProperty.Changed.AddClassHandler` call into it. Do not duplicate.

- [ ] **Step 17.3: Add lock icon to SidebarNavItem.axaml**

Open `src/UI/AuraCore.UI.Avalonia/Views/Controls/SidebarNavItem.axaml`. Find the template's root grid (probably has `ColumnDefinitions="Auto,*,Auto"` or similar). Append a lock icon element that is visible only when `:locked`:

```xml
<!-- Added at the end of the main grid, right-aligned -->
<PathIcon Grid.Column="2"
          x:Name="PART_LockIcon"
          Data="{DynamicResource IconLock}"
          Width="12" Height="12"
          Margin="0,0,6,0"
          VerticalAlignment="Center"
          Foreground="{DynamicResource TextDisabled}"
          IsVisible="False" />
```

Then add styles within `UserControl.Styles`:

```xml
<Style Selector="SidebarNavItem:locked">
  <Setter Property="Opacity" Value="0.5" />
  <Setter Property="Cursor" Value="No" />
</Style>
<Style Selector="SidebarNavItem:locked /template/ PathIcon#PART_LockIcon">
  <Setter Property="IsVisible" Value="True" />
</Style>
```

> Exact column index depends on the existing template structure. Verify with `Read` and adjust. The key intent: lock icon appears right-aligned, only when `:locked` pseudoclass is set.

- [ ] **Step 17.4: Run tests**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~SidebarNavItemLockTests" --verbosity minimal
```
Expected: `Passed: 6, Failed: 0`.

- [ ] **Step 17.5: Run full UI test suite to verify no regressions**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --verbosity minimal 2>&1 | tail -5
```
Expected: all prior SidebarNavItem tests still pass.

- [ ] **Step 17.6: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Controls/SidebarNavItem.axaml src/UI/AuraCore.UI.Avalonia/Views/Controls/SidebarNavItem.axaml.cs tests/AuraCore.Tests.UI.Avalonia/Views/SidebarNavItemLockTests.cs
git commit -m "feat(ui): add IsLocked DP to SidebarNavItem (opacity + lock icon + :locked pseudoclass)"
```

---

## Task 18: Update SidebarViewModel — single "AI Features" link, remove accordion sub-items

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/ViewModels/SidebarViewModel.cs`

Spec §8.1. The AI Features category previously held 4 accordion items (Insights, Recommendations, Scheduler, Chat). Collapse to a single item pointing at `AIFeaturesView`.

- [ ] **Step 18.1: Read current SidebarViewModel.BuildCategories**

Run:
```bash
grep -n "ai\|AI\|Insights\|Recommendations\|Scheduler\|Chat" src/UI/AuraCore.UI.Avalonia/ViewModels/SidebarViewModel.cs
```

Note line numbers of the AI Features category + its 4 sub-items.

- [ ] **Step 18.2: Collapse AI category to single item**

Edit `SidebarViewModel.cs`. Find the AI Features category block (will look something like):

```csharp
new SidebarCategory
{
    Key = "ai-features",
    LabelKey = "nav.category.aiFeatures",
    Icon = "IconSparkles",
    AccentColor = "AccentPurple",
    Modules = new List<SidebarModule>
    {
        new() { Key = "ai-insights", LabelKey = "nav.ai.insights", View = nameof(AIInsightsView) },
        new() { Key = "ai-recommendations", LabelKey = "nav.ai.recommendations", View = nameof(RecommendationsView) },
        new() { Key = "ai-scheduler", LabelKey = "nav.ai.scheduler", View = nameof(SchedulerView) },
        new() { Key = "ai-chat", LabelKey = "nav.ai.chat", View = nameof(AIChatView) },
    },
    // ...
},
```

Replace with:

```csharp
new SidebarCategory
{
    Key = "ai-features",
    LabelKey = "nav.aiFeatures.title",
    Icon = "IconSparklesFilled",
    AccentColor = "AccentPurple",
    Badge = "CORTEX",
    Modules = new List<SidebarModule>
    {
        new() { Key = "ai-features", LabelKey = "nav.aiFeatures.title", View = nameof(global::AuraCore.UI.Avalonia.Views.Pages.AIFeaturesView) },
    },
},
```

> Exact shape depends on `SidebarCategory` + `SidebarModule` record definitions. If the `Badge` property doesn't exist on `SidebarCategory` yet, add it (string, nullable). Then surface it in the sidebar UI rendering (separate from category label).

- [ ] **Step 18.3: Add Badge property to SidebarCategory (if missing)**

Run:
```bash
grep -n "class SidebarCategory\|record SidebarCategory" src/UI/AuraCore.UI.Avalonia/ViewModels/SidebarViewModel.cs
```

If `Badge` is missing, add to the record/class:

```csharp
public string? Badge { get; init; } // e.g. "CORTEX"
```

- [ ] **Step 18.4: Update sidebar XAML rendering to show badge (if not yet supported)**

Open `src/UI/AuraCore.UI.Avalonia/Views/MainWindow.axaml` (or wherever sidebar categories are rendered). Where a category is rendered, add an `AccentBadge` control bound to `Badge`:

```xml
<controls:AccentBadge Text="{Binding Badge}"
                      Accent="Purple"
                      IsVisible="{Binding Badge, Converter={x:Static ObjectConverters.IsNotNull}}"
                      VerticalAlignment="Center"
                      Margin="6,0,0,0" />
```

> Detail depends on the existing sidebar template. If sidebar currently doesn't support badges, add it as a minimal new element next to the category label.

- [ ] **Step 18.5: Write a test confirming the new structure**

Create `tests/AuraCore.Tests.UI.Avalonia/ViewModels/SidebarViewModelPhase3Tests.cs`:

```csharp
using AuraCore.UI.Avalonia.ViewModels;
using System.Linq;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.ViewModels;

public class SidebarViewModelPhase3Tests
{
    [Fact]
    public void AIFeaturesCategory_HasSingleModule()
    {
        var vm = new SidebarViewModel();
        vm.BuildCategories(); // or however Phase 2 initializes it

        var aiCategory = vm.Categories.FirstOrDefault(c => c.Key == "ai-features");
        Assert.NotNull(aiCategory);
        Assert.Single(aiCategory!.Modules);
        Assert.Equal("ai-features", aiCategory.Modules[0].Key);
    }

    [Fact]
    public void AIFeaturesCategory_HasCORTEXBadge()
    {
        var vm = new SidebarViewModel();
        vm.BuildCategories();

        var aiCategory = vm.Categories.First(c => c.Key == "ai-features");
        Assert.Equal("CORTEX", aiCategory.Badge);
    }
}
```

> Adapt to whatever `SidebarViewModel`'s actual public API is — Phase 2 plan should have established the shape.

- [ ] **Step 18.6: Run tests**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~SidebarViewModelPhase3Tests" --verbosity minimal
```
Expected: `Passed: 2, Failed: 0`.

- [ ] **Step 18.7: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/ViewModels/SidebarViewModel.cs src/UI/AuraCore.UI.Avalonia/Views/MainWindow.axaml tests/AuraCore.Tests.UI.Avalonia/ViewModels/SidebarViewModelPhase3Tests.cs
git commit -m "refactor(sidebar): collapse AI accordion to single AI Features link + CORTEX badge"
```

---

## Task 19: Wire sidebar "AI Features" click → AIFeaturesView navigation

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/MainWindow.axaml.cs`

MainWindow's existing `NavigateToModule` (Phase 2) needs a route for the `"ai-features"` key to `AIFeaturesView`. Since the view class is already DI-registered, wiring is minimal.

- [ ] **Step 19.1: Inspect NavigateToModule**

Run:
```bash
grep -n "NavigateToModule\|nameof.*View" src/UI/AuraCore.UI.Avalonia/Views/MainWindow.axaml.cs | head -20
```

Expected: existing switch/map on module key to view type.

- [ ] **Step 19.2: Add AI Features route**

In `NavigateToModule` (or equivalent routing method), add the `"ai-features"` case:

```csharp
case "ai-features":
    ShowContent(App.Services.GetRequiredService<global::AuraCore.UI.Avalonia.Views.Pages.AIFeaturesView>());
    break;
```

If the method uses reflection / `nameof(...)`, ensure `nameof(AIFeaturesView)` resolves to `"AIFeaturesView"` and the module's `View` property matches (set via Task 18).

- [ ] **Step 19.3: Manual smoke test**

```bash
dotnet run --project src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj --configuration Debug
```

1. Log in.
2. In sidebar, find "✦ AI Features [CORTEX]" single link (no accordion chevron).
3. Click it.
4. AIFeaturesView renders: hero at top, 2×2 card grid below.
5. Click any card — detail mode opens (section nav on left, placeholder on right).
6. Click "Overview" in section nav — grid returns.
7. Close app.

If anything fails: stop, fix, retry before committing.

- [ ] **Step 19.4: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/MainWindow.axaml.cs
git commit -m "feat(nav): route sidebar 'AI Features' click to AIFeaturesView"
```

---

## Task 20: Move SchedulerView → ScheduleSection (pilot section refactor)

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Pages/AI/` (directory)
- Move: `src/UI/AuraCore.UI.Avalonia/Views/Pages/SchedulerView.axaml` → `Views/Pages/AI/ScheduleSection.axaml`
- Move: `src/UI/AuraCore.UI.Avalonia/Views/Pages/SchedulerView.axaml.cs` → `Views/Pages/AI/ScheduleSection.axaml.cs`

Schedule chosen as pilot because it's the simplest AI section (no chat history, no external download dependency). Establishes the refactor template for Tasks 21-23.

- [ ] **Step 20.1: Create target directory**

Run:
```bash
mkdir -p src/UI/AuraCore.UI.Avalonia/Views/Pages/AI
```

- [ ] **Step 20.2: Move files with `git mv` (preserves history)**

Run:
```bash
git mv src/UI/AuraCore.UI.Avalonia/Views/Pages/SchedulerView.axaml src/UI/AuraCore.UI.Avalonia/Views/Pages/AI/ScheduleSection.axaml
git mv src/UI/AuraCore.UI.Avalonia/Views/Pages/SchedulerView.axaml.cs src/UI/AuraCore.UI.Avalonia/Views/Pages/AI/ScheduleSection.axaml.cs
```

- [ ] **Step 20.3: Rename class + update namespace**

Open `src/UI/AuraCore.UI.Avalonia/Views/Pages/AI/ScheduleSection.axaml.cs`. Change:

```csharp
namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class SchedulerView : UserControl
```

to:

```csharp
namespace AuraCore.UI.Avalonia.Views.Pages.AI;

public partial class ScheduleSection : UserControl
```

Then open `src/UI/AuraCore.UI.Avalonia/Views/Pages/AI/ScheduleSection.axaml`. Change:

```xml
<UserControl xmlns="..."
             x:Class="AuraCore.UI.Avalonia.Views.Pages.SchedulerView"
             ...>
```

to:

```xml
<UserControl xmlns="..."
             x:Class="AuraCore.UI.Avalonia.Views.Pages.AI.ScheduleSection"
             ...>
```

- [ ] **Step 20.4: Refresh XAML with Phase 1 primitives (visual-only)**

Inside `ScheduleSection.axaml`, replace ad-hoc XAML with Phase 1 primitives following the section template (spec §4.5). Keep existing event handlers and x:Name'd controls intact so the code-behind still works.

Example structure (adjust based on existing content):

```xml
<UserControl ...>
  <DockPanel>
    <!-- Section header -->
    <Border DockPanel.Dock="Top" Padding="0,0,0,12">
      <Grid ColumnDefinitions="*,Auto,Auto">
        <TextBlock Grid.Column="0"
                   Text="Smart Schedule"
                   FontSize="16"
                   FontWeight="SemiBold"
                   Foreground="{DynamicResource TextPrimary}" />
        <controls:StatusChip Grid.Column="1"
                             Text="{Binding StatusText, FallbackValue='3 patterns'}"
                             Accent="Amber"
                             Margin="0,0,8,0" />
        <controls:AuraToggle Grid.Column="2"
                             x:Name="EnabledToggle"
                             IsChecked="{Binding IsEnabled, FallbackValue=True}" />
      </Grid>
    </Border>

    <!-- Paused overlay (shown when IsEnabled=false) -->
    <Panel IsVisible="{Binding !IsEnabled}"
           DockPanel.Dock="Top">
      <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center" Spacing="8">
        <TextBlock Text="This feature is paused" Foreground="{DynamicResource TextSecondary}" />
        <Button Content="Enable" Classes="primary" x:Name="EnableButton" />
      </StackPanel>
    </Panel>

    <!-- Active content — use GlassCard / InsightCard for schedule entries -->
    <ScrollViewer IsVisible="{Binding IsEnabled, FallbackValue=True}">
      <StackPanel x:Name="ScheduleContent" Spacing="10">
        <!-- Preserve existing runtime-populated content; wire new wrappers around it -->
      </StackPanel>
    </ScrollViewer>
  </DockPanel>
</UserControl>
```

**Preserve existing code-behind logic.** Do not delete handlers or engine wiring. Only wrap visually with Phase 1 primitives.

- [ ] **Step 20.5: Update any `using ... SchedulerView` references**

Run:
```bash
grep -rn "SchedulerView" src/ --include="*.cs" --include="*.axaml" 2>&1 | grep -v "bin/\|obj/"
```

For each hit (e.g., sidebar references from Task 18, tests from Phase 2):
- If it's a `nameof(SchedulerView)` → change to `nameof(global::AuraCore.UI.Avalonia.Views.Pages.AI.ScheduleSection)`.
- If it's a `new SchedulerView()` → replace with `new ScheduleSection()`.
- If it's a `using` → update namespace.
- Tests that reference the old view need rename.

- [ ] **Step 20.6: Register ScheduleSection in DI**

Edit `src/UI/AuraCore.UI.Avalonia/App.axaml.cs`. In the Phase 3 block, add:

```csharp
sc.AddTransient<global::AuraCore.UI.Avalonia.Views.Pages.AI.ScheduleSection>();
```

Remove any existing `SchedulerView` registration.

- [ ] **Step 20.7: Wire into AIFeaturesView factory**

Edit `src/UI/AuraCore.UI.Avalonia/Views/Pages/AIFeaturesView.axaml.cs`. In `CreateSectionView`, update:

```csharp
return section switch
{
    "schedule" => App.Services.GetRequiredService<global::AuraCore.UI.Avalonia.Views.Pages.AI.ScheduleSection>(),
    _ => new UserControl { Content = new TextBlock { Text = $"[{section}] placeholder — wired in Task 20+" } },
};
```

- [ ] **Step 20.8: Build**

Run:
```bash
dotnet build src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj --configuration Debug --verbosity minimal
```
Expected: `Build succeeded`.

- [ ] **Step 20.9: Run full test suite**

Run:
```bash
dotnet test AuraCorePro.sln --verbosity minimal 2>&1 | tail -10
```
Expected: all tests pass (any Phase 2 tests referring to old SchedulerView now reference ScheduleSection after Step 20.5 updates).

- [ ] **Step 20.10: Manual smoke test**

Launch app → AI Features → click Schedule card → verify ScheduleSection renders in detail pane with same data/behavior as before.

- [ ] **Step 20.11: Commit**

```bash
git add -A src/UI/AuraCore.UI.Avalonia/Views/Pages/AI/ src/UI/AuraCore.UI.Avalonia/App.axaml.cs src/UI/AuraCore.UI.Avalonia/Views/Pages/AIFeaturesView.axaml.cs
git commit -m "refactor(ui): move SchedulerView -> Views/Pages/AI/ScheduleSection + refresh XAML with Phase 1 primitives"
```

---

## Task 21: Move AIInsightsView → InsightsSection

Same template as Task 20. Summary only — repeat steps 20.1-20.11 with these substitutions:

- Source: `Views/Pages/AIInsightsView.axaml[.cs]`
- Target: `Views/Pages/AI/InsightsSection.axaml[.cs]`
- Old class: `AIInsightsView` → `InsightsSection`
- Namespace: `AuraCore.UI.Avalonia.Views.Pages` → `AuraCore.UI.Avalonia.Views.Pages.AI`

Additional for Insights:

- [ ] **Step 21.a: Preserve Recent Activity section (Phase 2 migration).** The Insights view already contains Recent Activity (moved there during Phase 2). Keep that block intact — just wrap it in a `GlassCard` following the section template.

- [ ] **Step 21.b: Update AIFeaturesView factory** to route `"insights"` to `InsightsSection`.

- [ ] **Step 21.c: Register in DI:**

```csharp
sc.AddTransient<global::AuraCore.UI.Avalonia.Views.Pages.AI.InsightsSection>();
```

- [ ] **Step 21.d: Run full test suite to verify no regressions.**

- [ ] **Step 21.e: Commit:**

```bash
git add -A
git commit -m "refactor(ui): move AIInsightsView -> Views/Pages/AI/InsightsSection + refresh XAML"
```

---

## Task 22: Move RecommendationsView → RecommendationsSection

Same template as Task 20. Substitutions:

- Source: `Views/Pages/RecommendationsView.axaml[.cs]`
- Target: `Views/Pages/AI/RecommendationsSection.axaml[.cs]`
- Old class: `RecommendationsView` → `RecommendationsSection`
- Namespace: `...Pages` → `...Pages.AI`

Additional for Recommendations:

- [ ] **Step 22.a: Top section becomes `HeroCTA` primitive** when there are 2+ pending recommendations (batch apply). For 0-1 pending, show a `GlassCard` with "No actions pending".

- [ ] **Step 22.b: Each recommendation card** uses `GlassCard` + two buttons (Apply, Dismiss), following spec §4.6.

- [ ] **Step 22.c: Update AIFeaturesView factory** to route `"recommendations"` to `RecommendationsSection`.

- [ ] **Step 22.d: Register in DI:**

```csharp
sc.AddTransient<global::AuraCore.UI.Avalonia.Views.Pages.AI.RecommendationsSection>();
```

- [ ] **Step 22.e: Run tests + manual smoke.**

- [ ] **Step 22.f: Commit:**

```bash
git add -A
git commit -m "refactor(ui): move RecommendationsView -> Views/Pages/AI/RecommendationsSection + refresh XAML"
```

---

## Task 23: Move AIChatView → ChatSection (with permanent warning banner)

Same template as Task 20. Substitutions:

- Source: `Views/Pages/AIChatView.axaml[.cs]`
- Target: `Views/Pages/AI/ChatSection.axaml[.cs]`
- Old class: `AIChatView` → `ChatSection`
- Namespace: `...Pages` → `...Pages.AI`

**Chat-specific additions:**

- [ ] **Step 23.a: Add permanent warning banner at top** (inside section header or immediately below it). Banner text bound to localization key `aiFeatures.chat.warningBanner`. Background `AccentAmber` at 10% opacity.

```xml
<Border DockPanel.Dock="Top"
        Background="#1AF59E0B"
        BorderBrush="#4DF59E0B"
        BorderThickness="0,0,0,1"
        Padding="12,8">
  <StackPanel Orientation="Horizontal" Spacing="8">
    <PathIcon Data="{DynamicResource IconWarningTriangleFilled}"
              Width="14" Height="14"
              Foreground="{DynamicResource AccentAmber}" />
    <TextBlock Text="⚠ Experimental — CORTEX Chat may produce inaccurate outputs. Verify before applying any suggestion."
               FontSize="10"
               Foreground="{DynamicResource AccentAmber}"
               TextWrapping="Wrap" />
  </StackPanel>
</Border>
```

- [ ] **Step 23.b: Add model chip placeholder in header row** (below banner). This is a clickable chip showing the active model name; the dropdown behavior is wired in Task 28. For now, just a static placeholder:

```xml
<Grid ColumnDefinitions="*,Auto,Auto" DockPanel.Dock="Top" Margin="0,8,0,12">
  <TextBlock Grid.Column="0" Text="✦ CORTEX Chat" FontSize="14" FontWeight="SemiBold" />
  <Button Grid.Column="1"
          x:Name="ModelChip"
          Classes="model-chip"
          Content="⚙ No model selected ▾"
          FontSize="10" />
  <controls:AuraToggle Grid.Column="2" x:Name="ChatToggle"
                       IsChecked="{Binding IsEnabled}" />
</Grid>
```

- [ ] **Step 23.c: Disable message input + send button when `ChatEnabled=false` OR `ActiveChatModelId is null`.** The chat toggle wire-up (Task 29) opens the opt-in dialog which sets both.

- [ ] **Step 23.d: Preserve all IAuraCoreLLM + IAIAnalyzerEngine wiring** from the old code-behind. This is the AI logic that must not regress.

- [ ] **Step 23.e: Update AIFeaturesView factory** to route `"chat"` to `ChatSection`.

- [ ] **Step 23.f: Register in DI:**

```csharp
sc.AddTransient<global::AuraCore.UI.Avalonia.Views.Pages.AI.ChatSection>();
```

- [ ] **Step 23.g: Run tests + manual smoke.**

- [ ] **Step 23.h: Commit:**

```bash
git add -A
git commit -m "refactor(ui): move AIChatView -> Views/Pages/AI/ChatSection + warning banner + model chip placeholder"
```

---

## Task 24: Remove SmartOptimizePlaceholderDialog + update Dashboard CTA

**Files:**
- Delete: `src/UI/AuraCore.UI.Avalonia/Views/Dialogs/SmartOptimizePlaceholderDialog.axaml[.cs]`
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/DashboardView.axaml.cs`
- Modify: `src/UI/AuraCore.UI.Avalonia/ViewModels/DashboardViewModel.cs`

Spec §2.5. CTA now routes directly to AIFeaturesView > Recommendations instead of showing a placeholder dialog.

- [ ] **Step 24.1: Delete the placeholder dialog**

Run:
```bash
git rm src/UI/AuraCore.UI.Avalonia/Views/Dialogs/SmartOptimizePlaceholderDialog.axaml
git rm src/UI/AuraCore.UI.Avalonia/Views/Dialogs/SmartOptimizePlaceholderDialog.axaml.cs
```

- [ ] **Step 24.2: Update DashboardView.axaml.cs Smart Optimize click handler**

Find the handler (grep for `SmartOptimize`):

```bash
grep -n "SmartOptimize\|smart-optimize" src/UI/AuraCore.UI.Avalonia/Views/Pages/DashboardView.axaml.cs
```

Replace the dialog-open code with navigation to AIFeaturesView > Recommendations. The exact mechanism depends on how Phase 2 exposes `NavigateToModule`:

```csharp
private void OnSmartOptimizeClick(object? sender, RoutedEventArgs e)
{
    // Find the MainWindow and call its NavigateToModule
    if (global::Avalonia.VisualTree.VisualExtensions.FindAncestorOfType<Views.MainWindow>(this) is { } main)
    {
        main.NavigateToModule("ai-features");
        // Then advance AI Features to Recommendations detail:
        if (main.FindControl<ContentControl>("MainContentHost")?.Content is AIFeaturesView aiView
            && aiView.DataContext is ViewModels.AIFeaturesViewModel vm)
        {
            vm.NavigateToSection.Execute("recommendations");
        }
    }
}
```

> Replace `MainContentHost` with the actual content control name used in MainWindow. The pattern: navigate to the AI Features route, then programmatically switch its internal mode to Recommendations.

- [ ] **Step 24.3: Remove any DI registration for SmartOptimizePlaceholderDialog**

Run:
```bash
grep -rn "SmartOptimizePlaceholder" src/ --include="*.cs" --include="*.axaml" 2>&1 | grep -v "bin/\|obj/"
```
Expected: no results. If any, remove them.

- [ ] **Step 24.4: Build + run tests**

```bash
dotnet build src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj --configuration Debug --verbosity minimal
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --verbosity minimal 2>&1 | tail -5
```

A Phase 2 test may reference `SmartOptimizePlaceholderDialog` — delete that specific test or update it to expect navigation to AIFeaturesView instead.

- [ ] **Step 24.5: Commit**

```bash
git add -A
git commit -m "refactor(dashboard): route Smart Optimize CTA to AIFeaturesView > Recommendations (delete placeholder dialog)"
```

---

## Task 25: Create ChatOptInDialogViewModel

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/ViewModels/ChatOptInDialogViewModel.cs`
- Create: `tests/AuraCore.Tests.UI.Avalonia/ViewModels/ChatOptInDialogViewModelTests.cs`

Spec §7.1. State machine: Step 1 (ack) → Step 2 (model select). Handles resume from prior ack.

- [ ] **Step 25.1: Write failing tests**

Create `tests/AuraCore.Tests.UI.Avalonia/ViewModels/ChatOptInDialogViewModelTests.cs`:

```csharp
using AuraCore.UI.Avalonia;
using AuraCore.UI.Avalonia.ViewModels;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.ViewModels;

public class ChatOptInDialogViewModelTests
{
    [Fact]
    public void Initialize_Unacknowledged_StartsAtStep1()
    {
        var settings = new AppSettings { ChatOptInAcknowledged = false };
        var vm = new ChatOptInDialogViewModel(settings);

        Assert.Equal(1, vm.CurrentStep);
        Assert.True(vm.IsStep1);
        Assert.False(vm.IsStep2);
    }

    [Fact]
    public void Initialize_Acknowledged_SkipsToStep2()
    {
        var settings = new AppSettings { ChatOptInAcknowledged = true };
        var vm = new ChatOptInDialogViewModel(settings);

        Assert.Equal(2, vm.CurrentStep);
        Assert.True(vm.IsStep2);
        Assert.False(vm.IsStep1);
    }

    [Fact]
    public void ContinueFromStep1_SetsAcknowledgedAndAdvances()
    {
        var settings = new AppSettings { ChatOptInAcknowledged = false };
        var vm = new ChatOptInDialogViewModel(settings);

        vm.ContinueFromStep1.Execute(null);

        Assert.True(settings.ChatOptInAcknowledged);
        Assert.Equal(2, vm.CurrentStep);
    }

    [Fact]
    public void CancelFromStep1_LeavesAcknowledgedFalse()
    {
        var settings = new AppSettings { ChatOptInAcknowledged = false };
        var vm = new ChatOptInDialogViewModel(settings);

        var wasCompleteCalled = false;
        vm.RequestClose = _ => wasCompleteCalled = true;
        vm.CancelFromStep1.Execute(null);

        Assert.False(settings.ChatOptInAcknowledged);
        Assert.True(wasCompleteCalled);
    }

    [Fact]
    public void CompleteFromStep2_SetsActiveModelAndEnablesChat()
    {
        var settings = new AppSettings { ChatOptInAcknowledged = true };
        var vm = new ChatOptInDialogViewModel(settings);

        vm.CompleteFromStep2("phi3-mini-q4km");

        Assert.Equal("phi3-mini-q4km", settings.ActiveChatModelId);
        Assert.True(settings.ChatEnabled);
    }

    [Fact]
    public void CancelFromStep2_KeepsAcknowledgedTrueButChatDisabled()
    {
        var settings = new AppSettings { ChatOptInAcknowledged = true };
        var vm = new ChatOptInDialogViewModel(settings);

        vm.CancelFromStep2.Execute(null);

        Assert.True(settings.ChatOptInAcknowledged);
        Assert.False(settings.ChatEnabled);
        Assert.Null(settings.ActiveChatModelId);
    }
}
```

- [ ] **Step 25.2: Create ChatOptInDialogViewModel**

Create `src/UI/AuraCore.UI.Avalonia/ViewModels/ChatOptInDialogViewModel.cs`:

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace AuraCore.UI.Avalonia.ViewModels;

/// <summary>
/// State machine for the 2-step chat opt-in flow (spec §7.1).
/// Step 1: experimental acknowledgment; Step 2: delegates to ModelManagerDialogViewModel.
/// </summary>
public sealed class ChatOptInDialogViewModel : INotifyPropertyChanged
{
    private readonly AppSettings _settings;

    public ChatOptInDialogViewModel(AppSettings settings)
    {
        _settings = settings;

        // Resume logic: if previously acknowledged, skip straight to step 2
        _currentStep = settings.ChatOptInAcknowledged ? 2 : 1;

        ContinueFromStep1 = new DelegateCommand<object?>(_ => OnContinueFromStep1());
        CancelFromStep1   = new DelegateCommand<object?>(_ => Close(accepted: false));
        CancelFromStep2   = new DelegateCommand<object?>(_ => Close(accepted: false));
    }

    private int _currentStep;
    public int CurrentStep
    {
        get => _currentStep;
        private set
        {
            if (_currentStep == value) return;
            _currentStep = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsStep1));
            OnPropertyChanged(nameof(IsStep2));
        }
    }

    public bool IsStep1 => _currentStep == 1;
    public bool IsStep2 => _currentStep == 2;

    public ICommand ContinueFromStep1 { get; }
    public ICommand CancelFromStep1 { get; }
    public ICommand CancelFromStep2 { get; }

    /// <summary>Invoked by owner to trigger dialog close with the given accepted flag.</summary>
    public Action<bool>? RequestClose { get; set; }

    /// <summary>
    /// Called by the hosted ModelManagerDialog when the user completes model selection + download.
    /// Writes ActiveChatModelId + ChatEnabled to settings, then closes the dialog.
    /// </summary>
    public void CompleteFromStep2(string modelId)
    {
        _settings.ActiveChatModelId = modelId;
        _settings.ChatEnabled = true;
        _settings.Save();
        Close(accepted: true);
    }

    private void OnContinueFromStep1()
    {
        _settings.ChatOptInAcknowledged = true;
        _settings.Save();
        CurrentStep = 2;
    }

    private void Close(bool accepted) => RequestClose?.Invoke(accepted);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
```

- [ ] **Step 25.3: Run tests**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~ChatOptInDialogViewModelTests" --verbosity minimal
```
Expected: `Passed: 6, Failed: 0`.

- [ ] **Step 25.4: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/ViewModels/ChatOptInDialogViewModel.cs tests/AuraCore.Tests.UI.Avalonia/ViewModels/ChatOptInDialogViewModelTests.cs
git commit -m "feat(vm): add ChatOptInDialogViewModel (2-step state machine + resume)"
```

---

## Task 26: Create ChatOptInDialog.axaml (Step 1 UI)

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Dialogs/ChatOptInDialog.axaml`
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Dialogs/ChatOptInDialog.axaml.cs`

Spec §7.1 Step 1 visual. Step 2 content (ModelManagerDialog embedded) wired in Task 29.

- [ ] **Step 26.1: Create ChatOptInDialog.axaml**

Create `src/UI/AuraCore.UI.Avalonia/Views/Dialogs/ChatOptInDialog.axaml`:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:controls="using:AuraCore.UI.Avalonia.Views.Controls"
        xmlns:vm="using:AuraCore.UI.Avalonia.ViewModels"
        x:Class="AuraCore.UI.Avalonia.Views.Dialogs.ChatOptInDialog"
        x:DataType="vm:ChatOptInDialogViewModel"
        Title="CORTEX Chat — Experimental"
        Width="540" Height="420"
        CanResize="False"
        WindowStartupLocation="CenterOwner"
        SystemDecorations="BorderOnly">

  <Border Background="{DynamicResource BgSurface}"
          BorderBrush="{DynamicResource BorderEmphasis}"
          BorderThickness="1"
          CornerRadius="12">

    <Grid RowDefinitions="Auto,*,Auto" Margin="24">

      <!-- Header -->
      <StackPanel Grid.Row="0" Spacing="4">
        <TextBlock Text="✦ CORTEX CHAT — STEP 1 of 2"
                   FontSize="10"
                   FontWeight="SemiBold"
                   Foreground="{DynamicResource AccentPurple}"
                   Classes="label" />
        <TextBlock Text="⚠ Experimental Feature"
                   FontSize="18"
                   FontWeight="SemiBold"
                   Foreground="{DynamicResource AccentAmber}" />
      </StackPanel>

      <!-- Step 1: Acknowledgment -->
      <ScrollViewer Grid.Row="1" Margin="0,16,0,16"
                    IsVisible="{Binding IsStep1}">
        <StackPanel Spacing="10">
          <TextBlock Text="CORTEX Chat uses a local AI model that may produce inaccurate or misleading outputs. Always verify any suggestions before applying them to your system."
                     FontSize="12"
                     Foreground="{DynamicResource TextPrimary}"
                     TextWrapping="Wrap"
                     LineHeight="18" />
          <TextBlock Text="This feature is under active development."
                     FontSize="12"
                     Foreground="{DynamicResource TextSecondary}"
                     TextWrapping="Wrap" />
        </StackPanel>
      </ScrollViewer>

      <!-- Step 2 content host — filled by code-behind in Task 29 -->
      <ContentControl Grid.Row="1"
                      x:Name="PART_Step2Host"
                      Margin="0,16,0,16"
                      IsVisible="{Binding IsStep2}" />

      <!-- Action buttons — Step 1 only -->
      <StackPanel Grid.Row="2" Orientation="Horizontal"
                  HorizontalAlignment="Right" Spacing="10"
                  IsVisible="{Binding IsStep1}">
        <Button Content="Cancel"
                Command="{Binding CancelFromStep1}"
                Padding="16,8" />
        <Button Content="I understand, continue"
                Command="{Binding ContinueFromStep1}"
                Classes="primary"
                Padding="16,8" />
      </StackPanel>
    </Grid>
  </Border>
</Window>
```

- [ ] **Step 26.2: Create ChatOptInDialog.axaml.cs**

Create `src/UI/AuraCore.UI.Avalonia/Views/Dialogs/ChatOptInDialog.axaml.cs`:

```csharp
using global::Avalonia.Controls;
using global::Avalonia.Markup.Xaml;
using AuraCore.UI.Avalonia.ViewModels;

namespace AuraCore.UI.Avalonia.Views.Dialogs;

public partial class ChatOptInDialog : Window
{
    public ChatOptInDialog()
    {
        InitializeComponent();
    }

    public ChatOptInDialog(ChatOptInDialogViewModel vm) : this()
    {
        DataContext = vm;
        vm.RequestClose = accepted => Close(accepted);
    }

    /// <summary>
    /// Replaces Step 2 placeholder with a provided content control (typically ModelManagerDialog in OptIn mode).
    /// Wired by Task 29.
    /// </summary>
    public void MountStep2Content(Control content)
    {
        var host = this.FindControl<ContentControl>("PART_Step2Host");
        if (host is not null) host.Content = content;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 26.3: Register in DI**

Edit `App.axaml.cs` Phase 3 block:

```csharp
sc.AddTransient<global::AuraCore.UI.Avalonia.Views.Dialogs.ChatOptInDialog>();
sc.AddTransient<global::AuraCore.UI.Avalonia.ViewModels.ChatOptInDialogViewModel>();
```

- [ ] **Step 26.4: Smoke test — open dialog manually via F-key shortcut**

(Optional, for manual verification only.) Add a temporary keybinding in MainWindow to open the dialog, launch, press key, verify Step 1 renders. Remove binding before commit.

- [ ] **Step 26.5: Build**

```bash
dotnet build src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj --configuration Debug --verbosity minimal
```

- [ ] **Step 26.6: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Dialogs/ChatOptInDialog.axaml src/UI/AuraCore.UI.Avalonia/Views/Dialogs/ChatOptInDialog.axaml.cs src/UI/AuraCore.UI.Avalonia/App.axaml.cs
git commit -m "feat(ui): add ChatOptInDialog (Step 1 warning + Step 2 content host placeholder)"
```

---

## Task 27: Create ModelListItemVM + ModelManagerDialogViewModel

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/ViewModels/ModelListItemVM.cs`
- Create: `src/UI/AuraCore.UI.Avalonia/ViewModels/ModelManagerDialogViewModel.cs`
- Create: `tests/AuraCore.Tests.UI.Avalonia/ViewModels/ModelManagerDialogViewModelTests.cs`

Spec §7.2. Dialog VM with OptIn/Manage modes, RAM-aware selection, download state.

- [ ] **Step 27.1: Write failing tests**

Create `tests/AuraCore.Tests.UI.Avalonia/ViewModels/ModelManagerDialogViewModelTests.cs`:

```csharp
using AuraCore.UI.Avalonia.Services.AI;
using AuraCore.UI.Avalonia.ViewModels;
using System.Linq;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.ViewModels;

public class ModelManagerDialogViewModelTests
{
    private static readonly long InstalledRam = 32L * 1024 * 1024 * 1024; // 32 GB system

    private ModelManagerDialogViewModel CreateVM(
        ModelManagerDialogMode mode = ModelManagerDialogMode.OptIn,
        params string[] installedIds)
    {
        var catalog = new ModelCatalog();
        var installed = new FakeInstalledStore(catalog, installedIds);
        return new ModelManagerDialogViewModel(catalog, installed, mode, physicalRamBytes: InstalledRam);
    }

    private sealed class FakeInstalledStore : IInstalledModelStore
    {
        private readonly IModelCatalog _catalog;
        private readonly HashSet<string> _ids;
        public FakeInstalledStore(IModelCatalog c, IEnumerable<string> ids) { _catalog = c; _ids = new HashSet<string>(ids); }
        public IReadOnlyList<InstalledModel> Enumerate() =>
            _catalog.All.Where(m => _ids.Contains(m.Id))
                .Select(m => new InstalledModel(m.Id, new FileInfo(m.Filename), m.SizeBytes, DateTime.UtcNow))
                .ToList();
        public bool IsInstalled(string modelId) => _ids.Contains(modelId);
        public FileInfo? GetFile(string modelId) => _ids.Contains(modelId) ? new FileInfo(modelId + ".gguf") : null;
    }

    [Fact]
    public void OptInMode_StartsWithNoSelection()
    {
        var vm = CreateVM();
        Assert.Null(vm.SelectedModel);
        Assert.False(vm.CanDownload);
    }

    [Fact]
    public void SelectingModel_EnablesCanDownload()
    {
        var vm = CreateVM();
        vm.SelectedModel = vm.Models.First(m => m.Model.Id == "phi3-mini-q4km");

        Assert.True(vm.CanDownload);
    }

    [Fact]
    public void OptInMode_TitleSaysChoose()
    {
        var vm = CreateVM(mode: ModelManagerDialogMode.OptIn);
        Assert.Contains("Choose", vm.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ManageMode_TitleSaysManage()
    {
        var vm = CreateVM(mode: ModelManagerDialogMode.Manage);
        Assert.Contains("Manage", vm.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ManageMode_MarksInstalledModels()
    {
        var vm = CreateVM(mode: ModelManagerDialogMode.Manage, "phi2", "tinyllama");

        var installed = vm.Models.Where(m => m.IsInstalled).Select(m => m.Model.Id).ToList();
        Assert.Equal(2, installed.Count);
        Assert.Contains("phi2", installed);
        Assert.Contains("tinyllama", installed);
    }

    [Fact]
    public void ManageMode_CanDownload_FalseForAlreadyInstalled()
    {
        var vm = CreateVM(mode: ModelManagerDialogMode.Manage, "phi2");
        vm.SelectedModel = vm.Models.First(m => m.Model.Id == "phi2");

        Assert.False(vm.CanDownload); // already installed
    }

    [Fact]
    public void InsufficientRam_DisablesHeavyTierSelection()
    {
        // System with only 16 GB RAM
        var catalog = new ModelCatalog();
        var installed = new FakeInstalledStore(catalog, Array.Empty<string>());
        var vm = new ModelManagerDialogViewModel(catalog, installed, ModelManagerDialogMode.OptIn,
            physicalRamBytes: 16L * 1024 * 1024 * 1024);

        var heavy = vm.Models.Where(m => m.Model.Tier == ModelTier.Heavy);
        Assert.All(heavy, m => Assert.False(m.IsSelectable));
        Assert.All(heavy, m => Assert.NotNull(m.DisabledReason));
    }

    [Fact]
    public void SufficientRam_EnablesAllTiers()
    {
        var vm = CreateVM(); // 32 GB system
        Assert.All(vm.Models.Where(m => m.Model.Tier != ModelTier.Heavy),
            m => Assert.True(m.IsSelectable));
    }
}
```

- [ ] **Step 27.2: Create ModelListItemVM**

Create `src/UI/AuraCore.UI.Avalonia/ViewModels/ModelListItemVM.cs`:

```csharp
using AuraCore.UI.Avalonia.Services.AI;

namespace AuraCore.UI.Avalonia.ViewModels;

/// <summary>
/// Per-row view-model for the ModelManagerDialog's model list.
/// </summary>
public sealed class ModelListItemVM
{
    public ModelListItemVM(ModelDescriptor model, bool isInstalled, bool isSelectable, string? disabledReason)
    {
        Model = model;
        IsInstalled = isInstalled;
        IsSelectable = isSelectable;
        DisabledReason = disabledReason;
    }

    public ModelDescriptor Model { get; }
    public bool IsInstalled { get; }
    public bool IsSelectable { get; }
    public string? DisabledReason { get; }

    // Derived properties for XAML binding convenience:
    public string SizeDisplay => FormatGb(Model.SizeBytes);
    public string RamDisplay => "~" + FormatGb(Model.EstimatedRamBytes);
    public string SpeedDisplay => Model.Speed.ToString().ToUpperInvariant();
    public string TierDisplay => Model.Tier.ToString();
    public bool IsRecommended => Model.IsRecommended;

    private static string FormatGb(long bytes)
    {
        const double GB = 1024d * 1024 * 1024;
        var gb = bytes / GB;
        return gb >= 10 ? $"{gb:F0} GB" : $"{gb:F1} GB";
    }
}
```

- [ ] **Step 27.3: Create ModelManagerDialogMode + ModelManagerDialogViewModel**

Create `src/UI/AuraCore.UI.Avalonia/ViewModels/ModelManagerDialogViewModel.cs`:

```csharp
using AuraCore.UI.Avalonia.Services.AI;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AuraCore.UI.Avalonia.ViewModels;

public enum ModelManagerDialogMode { OptIn, Manage }

/// <summary>
/// View-model for the model selection / management dialog (spec §7.2).
/// Supports two modes: OptIn (first-time chat setup) and Manage (switch/download more).
/// </summary>
public sealed class ModelManagerDialogViewModel : INotifyPropertyChanged
{
    private readonly IModelCatalog _catalog;
    private readonly IInstalledModelStore _installed;
    private readonly IModelDownloadService? _downloader;
    private readonly long _physicalRamBytes;

    public ModelManagerDialogViewModel(
        IModelCatalog catalog,
        IInstalledModelStore installed,
        ModelManagerDialogMode mode,
        IModelDownloadService? downloader = null,
        long? physicalRamBytes = null)
    {
        _catalog = catalog;
        _installed = installed;
        _downloader = downloader;
        _physicalRamBytes = physicalRamBytes ?? DetectPhysicalRam();
        Mode = mode;

        Models = BuildItems();

        DownloadCommand = new DelegateCommand<object?>(async _ => await DownloadAsync(), _ => CanDownload);
        CancelDownloadCommand = new DelegateCommand<object?>(_ => _downloadCts?.Cancel());
        CancelDialogCommand = new DelegateCommand<object?>(_ => RequestClose?.Invoke(null));
    }

    public ModelManagerDialogMode Mode { get; }
    public IReadOnlyList<ModelListItemVM> Models { get; }

    public string Title => Mode == ModelManagerDialogMode.OptIn
        ? "Choose your AI model"
        : "Manage AI Models";

    public string Subtitle => Mode == ModelManagerDialogMode.OptIn
        ? "Please select a model to download. Models are pulled from AuraCore cloud."
        : "Download additional models or switch active model.";

    private ModelListItemVM? _selectedModel;
    public ModelListItemVM? SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (ReferenceEquals(_selectedModel, value)) return;
            _selectedModel = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanDownload));
            (DownloadCommand as DelegateCommand<object?>)?.RaiseCanExecuteChanged();
        }
    }

    public bool CanDownload =>
        _selectedModel is not null &&
        _selectedModel.IsSelectable &&
        !_selectedModel.IsInstalled &&
        _downloader is not null &&
        _activeDownload is null;

    private DownloadProgress? _activeDownload;
    public DownloadProgress? ActiveDownload
    {
        get => _activeDownload;
        private set
        {
            _activeDownload = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDownloading));
            OnPropertyChanged(nameof(CanDownload));
        }
    }

    public bool IsDownloading => _activeDownload is not null;

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set { _errorMessage = value; OnPropertyChanged(); }
    }

    public ICommand DownloadCommand { get; }
    public ICommand CancelDownloadCommand { get; }
    public ICommand CancelDialogCommand { get; }

    /// <summary>Invoked with selected ModelDescriptor on success, or null on cancel.</summary>
    public Action<ModelDescriptor?>? RequestClose { get; set; }

    private CancellationTokenSource? _downloadCts;

    private async Task DownloadAsync()
    {
        if (_selectedModel is null || _downloader is null) return;

        ErrorMessage = null;
        _downloadCts = new CancellationTokenSource();
        var progress = new Progress<DownloadProgress>(p => ActiveDownload = p);

        try
        {
            await _downloader.DownloadAsync(_selectedModel.Model, progress, _downloadCts.Token);
            ActiveDownload = null;
            RequestClose?.Invoke(_selectedModel.Model);
        }
        catch (OperationCanceledException)
        {
            ActiveDownload = null;
            // stay in dialog — user can pick another model
        }
        catch (ModelSizeMismatchException)
        {
            ActiveDownload = null;
            ErrorMessage = "Downloaded file is corrupted (size mismatch). Please try again.";
        }
        catch (System.Net.Http.HttpRequestException httpEx) when (httpEx.Message.Contains("403"))
        {
            ActiveDownload = null;
            ErrorMessage = "Download blocked by server. Please contact support.";
        }
        catch (Exception ex)
        {
            ActiveDownload = null;
            ErrorMessage = $"Couldn't reach models.auracore.pro. Check your connection. ({ex.GetType().Name})";
        }
    }

    private IReadOnlyList<ModelListItemVM> BuildItems()
    {
        return _catalog.All.Select(m =>
        {
            var isInstalled = _installed.IsInstalled(m.Id);
            var hasEnoughRam = _physicalRamBytes >= m.EstimatedRamBytes;
            var selectable = hasEnoughRam;
            string? reason = null;
            if (!hasEnoughRam)
            {
                var needGb = Math.Round((double)m.EstimatedRamBytes / (1024 * 1024 * 1024), 0);
                reason = $"Needs {needGb} GB RAM";
            }
            return new ModelListItemVM(m, isInstalled, selectable, reason);
        }).ToList();
    }

    private static long DetectPhysicalRam()
    {
        try
        {
            // Best-effort RAM detection cross-platform
            var info = global::System.GC.GetGCMemoryInfo();
            return info.TotalAvailableMemoryBytes;
        }
        catch { return 16L * 1024 * 1024 * 1024; } // safe fallback
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
```

- [ ] **Step 27.4: Run tests**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~ModelManagerDialogViewModelTests" --verbosity minimal
```
Expected: `Passed: 8, Failed: 0`.

- [ ] **Step 27.5: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/ViewModels/ModelListItemVM.cs src/UI/AuraCore.UI.Avalonia/ViewModels/ModelManagerDialogViewModel.cs tests/AuraCore.Tests.UI.Avalonia/ViewModels/ModelManagerDialogViewModelTests.cs
git commit -m "feat(vm): add ModelManagerDialogViewModel + ModelListItemVM (OptIn/Manage modes, RAM-aware)"
```

---

## Task 28: Create ModelManagerDialog.axaml

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Dialogs/ModelManagerDialog.axaml`
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Dialogs/ModelManagerDialog.axaml.cs`

Spec §7.2. Shared layout for both OptIn + Manage modes: tiered list, size/RAM/speed columns, footer stats, primary action.

- [ ] **Step 28.1: Create ModelManagerDialog.axaml**

Create `src/UI/AuraCore.UI.Avalonia/Views/Dialogs/ModelManagerDialog.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="using:AuraCore.UI.Avalonia.Views.Controls"
             xmlns:vm="using:AuraCore.UI.Avalonia.ViewModels"
             x:Class="AuraCore.UI.Avalonia.Views.Dialogs.ModelManagerDialog"
             x:DataType="vm:ModelManagerDialogViewModel">

  <UserControl.Styles>
    <Style Selector="Border.model-row">
      <Setter Property="Padding" Value="10" />
      <Setter Property="CornerRadius" Value="6" />
      <Setter Property="Background" Value="#0AFFFFFF" />
      <Setter Property="BorderBrush" Value="#10FFFFFF" />
      <Setter Property="BorderThickness" Value="1" />
      <Setter Property="Margin" Value="0,2" />
      <Setter Property="Cursor" Value="Hand" />
    </Style>
    <Style Selector="Border.model-row.selected">
      <Setter Property="Background" Value="#1400D4AA" />
      <Setter Property="BorderBrush" Value="#7F00D4AA" />
    </Style>
    <Style Selector="Border.model-row.disabled">
      <Setter Property="Opacity" Value="0.45" />
      <Setter Property="Cursor" Value="No" />
    </Style>
  </UserControl.Styles>

  <Grid RowDefinitions="Auto,*,Auto,Auto" Margin="0">

    <!-- Header -->
    <StackPanel Grid.Row="0" Spacing="4" Margin="0,0,0,12">
      <TextBlock Text="✦ CORTEX — STEP 2 of 2"
                 FontSize="9"
                 FontWeight="SemiBold"
                 Foreground="{DynamicResource AccentPurple}" />
      <TextBlock Text="{Binding Title}" FontSize="16" FontWeight="SemiBold"
                 Foreground="{DynamicResource TextPrimary}" />
      <TextBlock Text="{Binding Subtitle}" FontSize="11"
                 Foreground="{DynamicResource TextSecondary}"
                 TextWrapping="Wrap" />
    </StackPanel>

    <!-- Model list (grouped by tier) -->
    <ScrollViewer Grid.Row="1" MaxHeight="400">
      <ItemsControl ItemsSource="{Binding Models}">
        <ItemsControl.ItemTemplate>
          <DataTemplate DataType="vm:ModelListItemVM">
            <Border Classes="model-row"
                    Tapped="OnRowTapped"
                    Tag="{Binding}">
              <Grid ColumnDefinitions="Auto,*,Auto,Auto,Auto">
                <!-- Radio-like selection indicator -->
                <Border Grid.Column="0" Width="14" Height="14"
                        CornerRadius="7"
                        Background="Transparent"
                        BorderBrush="{DynamicResource BorderEmphasis}"
                        BorderThickness="1"
                        Margin="0,0,10,0" />
                <!-- Name + description -->
                <StackPanel Grid.Column="1" Spacing="2">
                  <StackPanel Orientation="Horizontal" Spacing="6">
                    <TextBlock Text="{Binding Model.DisplayName}" FontSize="11" FontWeight="SemiBold"
                               Foreground="{DynamicResource TextPrimary}" />
                    <Border Background="{DynamicResource AccentTeal}"
                            CornerRadius="3" Padding="4,1"
                            IsVisible="{Binding IsRecommended}">
                      <TextBlock Text="RECOMMENDED" FontSize="7" FontWeight="Bold"
                                 Foreground="{DynamicResource BgDeep}" />
                    </Border>
                    <Border Background="{DynamicResource AccentTealDim}"
                            CornerRadius="3" Padding="4,1"
                            IsVisible="{Binding IsInstalled}">
                      <TextBlock Text="INSTALLED" FontSize="7" FontWeight="Bold"
                                 Foreground="{DynamicResource AccentTeal}" />
                    </Border>
                  </StackPanel>
                  <TextBlock Text="{Binding DisabledReason, FallbackValue=''}"
                             FontSize="9" Foreground="{DynamicResource AccentAmber}"
                             IsVisible="{Binding DisabledReason, Converter={x:Static ObjectConverters.IsNotNull}}" />
                </StackPanel>
                <!-- Size / RAM / Speed columns -->
                <TextBlock Grid.Column="2" Text="{Binding SizeDisplay}"
                           FontSize="10" FontWeight="SemiBold"
                           Foreground="{DynamicResource TextPrimary}"
                           MinWidth="55" TextAlignment="Right" Margin="10,0" />
                <TextBlock Grid.Column="3" Text="{Binding RamDisplay}"
                           FontSize="10" FontWeight="SemiBold"
                           Foreground="{DynamicResource AccentPurple}"
                           MinWidth="55" TextAlignment="Right" Margin="0,0,10,0" />
                <Border Grid.Column="4" Padding="6,2"
                        CornerRadius="3"
                        Background="#14FFFFFF">
                  <TextBlock Text="{Binding SpeedDisplay}"
                             FontSize="8" FontWeight="Bold"
                             Foreground="{DynamicResource TextSecondary}" />
                </Border>
              </Grid>
            </Border>
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>
    </ScrollViewer>

    <!-- Download progress / error row -->
    <StackPanel Grid.Row="2" Margin="0,12,0,0" Spacing="4"
                IsVisible="{Binding IsDownloading}">
      <TextBlock Text="{Binding ActiveDownload, StringFormat='Downloading · {0:F1}% · {0:F2} MB/s'}"
                 FontSize="10" />
      <ProgressBar Minimum="0" Maximum="1"
                   Value="{Binding ActiveDownload.BytesReceived, FallbackValue=0}" />
    </StackPanel>

    <TextBlock Grid.Row="2" Text="{Binding ErrorMessage}"
               FontSize="10" Foreground="{DynamicResource StatusError}"
               TextWrapping="Wrap" Margin="0,8,0,0"
               IsVisible="{Binding ErrorMessage, Converter={x:Static ObjectConverters.IsNotNull}}" />

    <!-- Action buttons -->
    <Grid Grid.Row="3" ColumnDefinitions="*,Auto,Auto" Margin="0,12,0,0">
      <TextBlock Grid.Column="0" VerticalAlignment="Center"
                 FontSize="9" Foreground="{DynamicResource TextMuted}"
                 x:Name="StatsLine" />
      <Button Grid.Column="1" Content="Cancel"
              Command="{Binding CancelDialogCommand}"
              Padding="14,8" Margin="0,0,8,0" />
      <Button Grid.Column="2" Content="Download &amp; use"
              Command="{Binding DownloadCommand}"
              Classes="primary" Padding="14,8" />
    </Grid>

  </Grid>
</UserControl>
```

- [ ] **Step 28.2: Create ModelManagerDialog.axaml.cs**

Create `src/UI/AuraCore.UI.Avalonia/Views/Dialogs/ModelManagerDialog.axaml.cs`:

```csharp
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Markup.Xaml;
using AuraCore.UI.Avalonia.ViewModels;

namespace AuraCore.UI.Avalonia.Views.Dialogs;

public partial class ModelManagerDialog : UserControl
{
    public ModelManagerDialog()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public ModelManagerDialog(ModelManagerDialogViewModel vm) : this()
    {
        DataContext = vm;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ModelManagerDialogViewModel vm)
        {
            UpdateStats(vm);
        }
    }

    private void UpdateStats(ModelManagerDialogViewModel vm)
    {
        var stats = this.FindControl<TextBlock>("StatsLine");
        if (stats is null) return;
        try
        {
            var free = new System.IO.DriveInfo(System.IO.Path.GetPathRoot(AppContext.BaseDirectory)!).AvailableFreeSpace;
            var freeGb = free / (1024d * 1024 * 1024);
            var ramInfo = System.GC.GetGCMemoryInfo();
            var ramGb = ramInfo.TotalAvailableMemoryBytes / (1024d * 1024 * 1024);
            stats.Text = $"Disk free: {freeGb:F0} GB · Your RAM: {ramGb:F0} GB";
        }
        catch { stats.Text = ""; }
    }

    private void OnRowTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border b && b.Tag is ModelListItemVM item && DataContext is ModelManagerDialogViewModel vm)
        {
            if (!item.IsSelectable) return;
            if (item.IsInstalled && vm.Mode == ModelManagerDialogMode.OptIn)
            {
                // Already installed in OptIn mode → close with this model selected
                vm.SelectedModel = item;
                return;
            }
            vm.SelectedModel = item;

            // Update selection visual state
            foreach (var child in (this.LogicalChildren.ToList()))
            {
                // Simple approach: rebuild classes on all rows
            }
            b.Classes.Set("selected", true);
        }
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 28.3: Register in DI**

Edit `App.axaml.cs` Phase 3 block:

```csharp
sc.AddTransient<global::AuraCore.UI.Avalonia.Views.Dialogs.ModelManagerDialog>();
sc.AddTransient<global::AuraCore.UI.Avalonia.ViewModels.ModelManagerDialogViewModel>();
```

- [ ] **Step 28.4: Build**

```bash
dotnet build src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj --configuration Debug --verbosity minimal
```

- [ ] **Step 28.5: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Dialogs/ModelManagerDialog.axaml src/UI/AuraCore.UI.Avalonia/Views/Dialogs/ModelManagerDialog.axaml.cs src/UI/AuraCore.UI.Avalonia/App.axaml.cs
git commit -m "feat(ui): add ModelManagerDialog (tiered list, size/RAM/speed columns, progress, error states)"
```

---

## Task 29: Wire Step 2 — mount ModelManagerDialog inside ChatOptInDialog

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Dialogs/ChatOptInDialog.axaml.cs`

Spec §7.1. When CurrentStep transitions 1 → 2, inject a ModelManagerDialog (OptIn mode) into the Step 2 content host. Completion callback closes the outer dialog.

- [ ] **Step 29.1: Update ChatOptInDialog.axaml.cs with Step 2 wiring**

Edit `src/UI/AuraCore.UI.Avalonia/Views/Dialogs/ChatOptInDialog.axaml.cs`. Replace the existing file with:

```csharp
using global::Avalonia.Controls;
using global::Avalonia.Markup.Xaml;
using AuraCore.UI.Avalonia.Services.AI;
using AuraCore.UI.Avalonia.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace AuraCore.UI.Avalonia.Views.Dialogs;

public partial class ChatOptInDialog : Window
{
    private ModelManagerDialog? _managerDialog;

    public ChatOptInDialog()
    {
        InitializeComponent();
    }

    public ChatOptInDialog(ChatOptInDialogViewModel vm) : this()
    {
        DataContext = vm;
        vm.RequestClose = accepted => Close(accepted);

        // If already on Step 2 (user previously acknowledged), mount immediately.
        if (vm.IsStep2) MountStep2();

        vm.PropertyChanged += OnVMPropertyChanged;
    }

    private void OnVMPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatOptInDialogViewModel.CurrentStep)
            && DataContext is ChatOptInDialogViewModel vm
            && vm.IsStep2
            && _managerDialog is null)
        {
            MountStep2();
        }
    }

    private void MountStep2()
    {
        var catalog = App.Services.GetRequiredService<IModelCatalog>();
        var installed = App.Services.GetRequiredService<IInstalledModelStore>();
        var downloader = App.Services.GetRequiredService<IModelDownloadService>();

        var managerVm = new ModelManagerDialogViewModel(catalog, installed, ModelManagerDialogMode.OptIn, downloader);
        managerVm.RequestClose = selected =>
        {
            if (selected is not null && DataContext is ChatOptInDialogViewModel outer)
            {
                outer.CompleteFromStep2(selected.Id);
            }
            else if (selected is null && DataContext is ChatOptInDialogViewModel outer2)
            {
                outer2.CancelFromStep2.Execute(null);
            }
        };

        _managerDialog = new ModelManagerDialog(managerVm);
        var host = this.FindControl<ContentControl>("PART_Step2Host");
        if (host is not null) host.Content = _managerDialog;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 29.2: Integration test for the full flow**

Create `tests/AuraCore.Tests.UI.Avalonia/Views/ChatOptInFlowIntegrationTests.cs`:

```csharp
using AuraCore.UI.Avalonia;
using AuraCore.UI.Avalonia.Services.AI;
using AuraCore.UI.Avalonia.ViewModels;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

public class ChatOptInFlowIntegrationTests
{
    [Fact]
    public void Step1Continue_AdvancesToStep2()
    {
        var settings = new AppSettings { ChatOptInAcknowledged = false };
        var vm = new ChatOptInDialogViewModel(settings);

        vm.ContinueFromStep1.Execute(null);

        Assert.Equal(2, vm.CurrentStep);
        Assert.True(settings.ChatOptInAcknowledged);
    }

    [Fact]
    public void Step2Completion_EnablesChatAndSetsModel()
    {
        var settings = new AppSettings { ChatOptInAcknowledged = true };
        var vm = new ChatOptInDialogViewModel(settings);

        vm.CompleteFromStep2("phi3-mini-q4km");

        Assert.True(settings.ChatEnabled);
        Assert.Equal("phi3-mini-q4km", settings.ActiveChatModelId);
    }

    [Fact]
    public void Step2Cancel_KeepsAcknowledgedButChatOff()
    {
        var settings = new AppSettings { ChatOptInAcknowledged = true };
        var vm = new ChatOptInDialogViewModel(settings);

        vm.CancelFromStep2.Execute(null);

        Assert.True(settings.ChatOptInAcknowledged);
        Assert.False(settings.ChatEnabled);
        Assert.Null(settings.ActiveChatModelId);
    }
}
```

- [ ] **Step 29.3: Run tests**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~ChatOptInFlowIntegration" --verbosity minimal
```
Expected: `Passed: 3, Failed: 0`.

- [ ] **Step 29.4: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Dialogs/ChatOptInDialog.axaml.cs tests/AuraCore.Tests.UI.Avalonia/Views/ChatOptInFlowIntegrationTests.cs
git commit -m "feat(ui): wire Step 2 ModelManagerDialog inside ChatOptInDialog (2-step opt-in flow)"
```

---

## Task 30: Wire chat toggle → ChatOptInDialog flow

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/ViewModels/AIFeaturesViewModel.cs` OR `src/UI/AuraCore.UI.Avalonia/Views/Pages/AI/ChatSection.axaml.cs`

When user toggles ChatCard ON, intercept: if `ActiveChatModelId is null` OR `ChatOptInAcknowledged is false`, open ChatOptInDialog instead of naively flipping the setting.

- [ ] **Step 30.1: Intercept ChatCard toggle in AIFeaturesViewModel**

Open `AIFeaturesViewModel.cs`. Find the `WireToggleHandlers` method's Chat branch. Replace with:

```csharp
ChatCard.PropertyChanged += async (_, e) =>
{
    if (e.PropertyName != nameof(AIFeatureCardVM.IsEnabled)) return;

    if (ChatCard.IsEnabled)
    {
        // User is trying to enable chat — check if flow is needed
        if (_settings.ActiveChatModelId is null || !_settings.ChatOptInAcknowledged)
        {
            // Open the opt-in dialog. If user completes, ChatEnabled gets set True internally.
            // If user cancels, revert the toggle.
            ChatCard.IsEnabled = false; // revert until opt-in completes
            var opened = await OpenChatOptInDialogAsync();
            if (opened)
            {
                // ChatOptInDialog set ChatEnabled=true via its internal CompleteFromStep2
                ChatCard.IsEnabled = true;
            }
            return;
        }
    }

    _settings.ChatEnabled = ChatCard.IsEnabled;
    _settings.Save();
    _ambient.Refresh();
};
```

Add the helper:

```csharp
/// <summary>
/// Wired by the View's code-behind (has access to dialog owner window + DI).
/// Returns true if opt-in completed (ChatEnabled is now true).
/// </summary>
public Func<Task<bool>>? ChatOptInOpener { get; set; }

private async Task<bool> OpenChatOptInDialogAsync()
{
    if (ChatOptInOpener is null) return false;
    return await ChatOptInOpener();
}
```

- [ ] **Step 30.2: Wire ChatOptInOpener in AIFeaturesView code-behind**

Edit `src/UI/AuraCore.UI.Avalonia/Views/Pages/AIFeaturesView.axaml.cs`. In the `OnLoaded` handler (after `vm.SectionViewFactory = ...`), add:

```csharp
vm.ChatOptInOpener = async () =>
{
    var ownerWindow = global::Avalonia.VisualTree.VisualExtensions.FindAncestorOfType<Window>(this);
    if (ownerWindow is null) return false;

    var optInVm = new ChatOptInDialogViewModel(App.Services.GetRequiredService<AppSettings>());
    var dialog = new Views.Dialogs.ChatOptInDialog(optInVm);
    await dialog.ShowDialog<bool>(ownerWindow);

    // On completion, ChatOptInDialogViewModel.CompleteFromStep2 has already set ChatEnabled
    return App.Services.GetRequiredService<AppSettings>().ChatEnabled;
};
```

- [ ] **Step 30.3: Build + smoke test**

```bash
dotnet build src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj --configuration Debug --verbosity minimal
dotnet run --project src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj --configuration Debug
```

Manual flow:
1. Navigate to AI Features → Chat card.
2. Toggle ON.
3. ChatOptInDialog Step 1 appears.
4. Click "I understand, continue".
5. Step 2 appears with model list (all 8 models, Phi-3 Mini Q4KM recommended).
6. Click a model — Download button enables.
7. Click Download — progress bar shows (will actually download from R2, or cancel via X).
8. Completion: dialog closes, Chat card shows ON, ChatSection in detail pane shows "model active".

If network flow fails: check Cloudflare (Task 1 infrastructure should be ready).

- [ ] **Step 30.4: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/ViewModels/AIFeaturesViewModel.cs src/UI/AuraCore.UI.Avalonia/Views/Pages/AIFeaturesView.axaml.cs
git commit -m "feat(ui): wire chat toggle to open ChatOptInDialog on first enable"
```

---

## Task 31: Chat header model chip + dropdown switcher

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/AI/ChatSection.axaml`
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/AI/ChatSection.axaml.cs`

Spec §4.6 ChatSection. The `ModelChip` placeholder added in Task 23.b gets its dropdown behavior: list installed models (one active) + "Download more..." opens ModelManagerDialog in Manage mode.

- [ ] **Step 31.1: Replace ModelChip button with a dropdown-capable control**

Open `ChatSection.axaml`. Find the `<Button x:Name="ModelChip" ... />` (added in Task 23.b). Replace with:

```xml
<SplitButton x:Name="ModelChip"
             Grid.Column="1"
             Classes="model-chip"
             FontSize="10"
             Content="⚙ No model selected ▾"
             Padding="10,4"
             Margin="0,0,8,0">
  <SplitButton.Flyout>
    <MenuFlyout x:Name="ModelMenuFlyout">
      <!-- Items populated at runtime -->
    </MenuFlyout>
  </SplitButton.Flyout>
</SplitButton>
```

- [ ] **Step 31.2: Populate the flyout from code-behind**

Edit `ChatSection.axaml.cs`. In the `OnLoaded` handler (preserve all existing AI engine wiring), add:

```csharp
private void BuildModelMenu()
{
    var flyout = this.FindControl<MenuFlyout>("ModelMenuFlyout");
    if (flyout is null) return;

    flyout.Items.Clear();

    var catalog = App.Services.GetRequiredService<AuraCore.UI.Avalonia.Services.AI.IModelCatalog>();
    var installed = App.Services.GetRequiredService<AuraCore.UI.Avalonia.Services.AI.IInstalledModelStore>();
    var settings = App.Services.GetRequiredService<AuraCore.UI.Avalonia.AppSettings>();

    var installedModels = installed.Enumerate().ToList();
    foreach (var im in installedModels)
    {
        var descriptor = catalog.FindById(im.ModelId);
        if (descriptor is null) continue;

        var isActive = settings.ActiveChatModelId == descriptor.Id;
        var item = new MenuItem
        {
            Header = (isActive ? "● " : "  ") + descriptor.DisplayName + $"  ({im.SizeBytes / (1024d * 1024 * 1024):F1} GB)",
            Tag = descriptor.Id,
        };
        item.Click += (_, _) => OnSwitchModel(descriptor.Id);
        flyout.Items.Add(item);
    }

    flyout.Items.Add(new Separator());

    var downloadMore = new MenuItem { Header = "⬇ Download more models..." };
    downloadMore.Click += (_, _) => OnOpenModelManager();
    flyout.Items.Add(downloadMore);

    // Update chip content to reflect current model
    var chip = this.FindControl<SplitButton>("ModelChip");
    if (chip is not null)
    {
        var active = installedModels.FirstOrDefault(im => im.ModelId == settings.ActiveChatModelId);
        var activeDescriptor = active is not null ? catalog.FindById(active.ModelId) : null;
        chip.Content = activeDescriptor is not null
            ? $"⚙ {activeDescriptor.DisplayName} ▾"
            : "⚙ No model selected ▾";
    }
}

private void OnSwitchModel(string modelId)
{
    var settings = App.Services.GetRequiredService<AuraCore.UI.Avalonia.AppSettings>();
    settings.ActiveChatModelId = modelId;
    settings.Save();
    BuildModelMenu();
    // Trigger model reload in LLM engine
    ReloadLLM();
}

private async void OnOpenModelManager()
{
    var ownerWindow = global::Avalonia.VisualTree.VisualExtensions.FindAncestorOfType<Window>(this);
    if (ownerWindow is null) return;

    var catalog = App.Services.GetRequiredService<AuraCore.UI.Avalonia.Services.AI.IModelCatalog>();
    var installed = App.Services.GetRequiredService<AuraCore.UI.Avalonia.Services.AI.IInstalledModelStore>();
    var downloader = App.Services.GetRequiredService<AuraCore.UI.Avalonia.Services.AI.IModelDownloadService>();

    var vm = new AuraCore.UI.Avalonia.ViewModels.ModelManagerDialogViewModel(
        catalog, installed,
        AuraCore.UI.Avalonia.ViewModels.ModelManagerDialogMode.Manage,
        downloader);

    var managerDialog = new Window
    {
        Title = "Manage AI Models",
        Width = 580, Height = 640,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Content = new AuraCore.UI.Avalonia.Views.Dialogs.ModelManagerDialog(vm),
    };

    vm.RequestClose = _ => managerDialog.Close();
    await managerDialog.ShowDialog(ownerWindow);

    // Refresh menu after dialog closes (new model may have been downloaded)
    BuildModelMenu();
}

private void ReloadLLM()
{
    // Delegates to existing IAuraCoreLLM reload logic — preserved from old AIChatView code-behind.
    // If the original AIChatView had a _llm.ReloadAsync() or similar, call it here.
    try { _llm?.GetType().GetMethod("ReloadAsync")?.Invoke(_llm, null); } catch { }
}
```

Call `BuildModelMenu()` at end of the existing `OnLoaded` handler.

Add `using AuraCore.UI.Avalonia.Services.AI;` and `using AuraCore.UI.Avalonia.ViewModels;` at top of file if needed.

- [ ] **Step 31.3: Build + manual smoke**

```bash
dotnet build src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj --configuration Debug --verbosity minimal
```

Manual:
1. Launch app → AI Features → Chat (after opt-in from Task 30).
2. Model chip shows active model name.
3. Click dropdown — list shows installed models with active one marked `●`.
4. Click "Download more..." — ModelManagerDialog opens in Manage mode.
5. Download a 2nd model.
6. After dialog closes, dropdown shows both models.
7. Click the non-active model in dropdown — switches (chip updates).

- [ ] **Step 31.4: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Pages/AI/ChatSection.axaml src/UI/AuraCore.UI.Avalonia/Views/Pages/AI/ChatSection.axaml.cs
git commit -m "feat(ui): add model chip dropdown to ChatSection (switch installed + open Manage dialog)"
```

---

## Task 32: DashboardViewModel subscribe to CortexAmbientService + Cortex Insights ripple

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/ViewModels/DashboardViewModel.cs`
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/DashboardView.axaml[.cs]`

Spec §5.3. DashboardViewModel observes CortexAmbientService; exposes properties controlling Cortex Insights card visibility, header chip state, Smart Optimize CTA state. DashboardView binds to those properties.

- [ ] **Step 32.1: Write failing tests**

Create `tests/AuraCore.Tests.UI.Avalonia/ViewModels/DashboardViewModelRippleTests.cs`:

```csharp
using AuraCore.UI.Avalonia;
using AuraCore.UI.Avalonia.Services.AI;
using AuraCore.UI.Avalonia.ViewModels;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.ViewModels;

public class DashboardViewModelRippleTests
{
    private (AppSettings, CortexAmbientService, DashboardViewModel) Setup(
        bool insights = true, bool recs = true, bool schedule = false, bool chat = false)
    {
        var settings = new AppSettings
        {
            InsightsEnabled = insights,
            RecommendationsEnabled = recs,
            ScheduleEnabled = schedule,
            ChatEnabled = chat,
        };
        var ambient = new CortexAmbientService(settings);
        var vm = new DashboardViewModel(ambient); // ensure ctor accepts ambient; add overload if needed
        return (settings, ambient, vm);
    }

    [Fact]
    public void InsightsOn_ShowInsightsCard_True()
    {
        var (_, _, vm) = Setup(insights: true);
        Assert.True(vm.ShowCortexInsightsCard);
    }

    [Fact]
    public void InsightsOff_ShowInsightsCard_False()
    {
        var (settings, ambient, vm) = Setup(insights: false);
        Assert.False(vm.ShowCortexInsightsCard);
    }

    [Fact]
    public void ToggleInsights_FiresPropertyChanged()
    {
        var (settings, ambient, vm) = Setup(insights: false);
        var fired = new List<string>();
        ((System.ComponentModel.INotifyPropertyChanged)vm).PropertyChanged += (_, e) => fired.Add(e.PropertyName ?? "");

        settings.InsightsEnabled = true;
        ambient.Refresh();

        Assert.Contains(nameof(DashboardViewModel.ShowCortexInsightsCard), fired);
    }

    [Fact]
    public void CortexChipState_AnyOn_ReturnsOn()
    {
        var (_, _, vm) = Setup(insights: true);
        Assert.Equal("ON", vm.CortexChipState);
    }

    [Fact]
    public void CortexChipState_AllOff_ReturnsOff()
    {
        var (_, _, vm) = Setup(insights: false, recs: false, schedule: false, chat: false);
        Assert.Equal("OFF", vm.CortexChipState);
    }

    [Fact]
    public void RecommendationsOff_SmartOptimizeEnabled_False()
    {
        var (_, _, vm) = Setup(recs: false);
        Assert.False(vm.SmartOptimizeEnabled);
    }

    [Fact]
    public void RecommendationsOn_SmartOptimizeEnabled_True()
    {
        var (_, _, vm) = Setup(recs: true);
        Assert.True(vm.SmartOptimizeEnabled);
    }
}
```

- [ ] **Step 32.2: Update DashboardViewModel**

Edit `src/UI/AuraCore.UI.Avalonia/ViewModels/DashboardViewModel.cs`. Add these properties (Phase 3 additions — keep all Phase 2 properties):

```csharp
// Phase 3: ripple properties driven by CortexAmbientService

private readonly ICortexAmbientService? _ambient;

// Update ctor (overload or primary):
public DashboardViewModel(ICortexAmbientService? ambient = null)
{
    _ambient = ambient;
    if (_ambient is not null)
    {
        _ambient.PropertyChanged += (_, _) => OnAmbientChanged();
    }
}

public bool ShowCortexInsightsCard =>
    _ambient?.AnyFeatureEnabled == true /* && InsightsEnabled specifically */;

public bool ShowCortexSubtitle => _ambient?.AnyFeatureEnabled == true;

public string CortexChipState => _ambient?.AnyFeatureEnabled == true ? "ON" : "OFF";

public bool SmartOptimizeEnabled => GetSettings()?.RecommendationsEnabled == true;

private void OnAmbientChanged()
{
    OnPropertyChanged(nameof(ShowCortexInsightsCard));
    OnPropertyChanged(nameof(ShowCortexSubtitle));
    OnPropertyChanged(nameof(CortexChipState));
    OnPropertyChanged(nameof(SmartOptimizeEnabled));
}

private AppSettings? GetSettings()
{
    try { return global::AuraCore.UI.Avalonia.App.Services.GetService<AppSettings>(); }
    catch { return null; }
}
```

Refinement: `ShowCortexInsightsCard` should specifically check `Settings.InsightsEnabled`, not just `AnyFeatureEnabled`. Update:

```csharp
public bool ShowCortexInsightsCard => GetSettings()?.InsightsEnabled == true;
public bool ShowCortexSubtitle => GetSettings()?.InsightsEnabled == true;
```

- [ ] **Step 32.3: Bind DashboardView.axaml to new properties**

In `DashboardView.axaml`, find the Cortex Insights card element and its subtitle. Wrap with `IsVisible` bindings:

```xml
<!-- Cortex Insights card wrapper -->
<Border IsVisible="{Binding ShowCortexInsightsCard}">
  <!-- existing insights card content -->
</Border>

<!-- Replacement placeholder when paused -->
<Border IsVisible="{Binding !ShowCortexInsightsCard}"
        Padding="12"
        Background="{DynamicResource BgCard}"
        CornerRadius="8">
  <TextBlock Text="AI Insights paused — Enable in AI Features"
             FontSize="11"
             Foreground="{DynamicResource TextMuted}" />
</Border>
```

For the Cortex chip in the header:

```xml
<controls:StatusChip Text="{Binding CortexChipState, StringFormat='Cortex AI · {0}'}"
                     Accent="{Binding CortexChipState, Converter={StaticResource OnOffAccentConverter}}" />
```

(Add a simple converter that returns "Teal" when "ON", "Gray" when "OFF".)

For the Smart Optimize CTA:

```xml
<Button Command="{Binding SmartOptimizeCommand}"
        IsEnabled="{Binding SmartOptimizeEnabled}"
        ToolTip.Tip="{Binding SmartOptimizeEnabled, Converter={StaticResource SmartOptimizeTooltipConverter}}"
        Content="Smart Optimize Now" />
```

- [ ] **Step 32.4: Run tests**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~DashboardViewModelRippleTests" --verbosity minimal
```
Expected: `Passed: 7, Failed: 0`.

- [ ] **Step 32.5: Manual smoke**

Launch → Dashboard renders with Cortex Insights card. Navigate to AI Features → toggle Insights OFF. Return to Dashboard → Cortex Insights card replaced by "AI Insights paused" placeholder. Toggle Recommendations OFF → Smart Optimize CTA greys out.

- [ ] **Step 32.6: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/ViewModels/DashboardViewModel.cs src/UI/AuraCore.UI.Avalonia/Views/Pages/DashboardView.axaml tests/AuraCore.Tests.UI.Avalonia/ViewModels/DashboardViewModelRippleTests.cs
git commit -m "feat(dashboard): ripple from CortexAmbientService (InsightsCard visibility, chip state, Smart Optimize CTA)"
```

---

## Task 33: StatusBar ripple

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/MainWindow.axaml` (or wherever status bar lives)
- Modify: `src/UI/AuraCore.UI.Avalonia/ViewModels/SidebarViewModel.cs` or new `StatusBarViewModel.cs`

Spec §5.3. Status bar shows "Active · Learning day N" / "Paused" / "Ready" depending on CORTEX state.

- [ ] **Step 33.1: Locate current status bar element**

Run:
```bash
grep -n "Cortex\|status-bar\|StatusBar" src/UI/AuraCore.UI.Avalonia/Views/MainWindow.axaml | head -10
```

Note the element containing the CORTEX message.

- [ ] **Step 33.2: Bind status bar text to ambient service**

Option A: simplest — bind directly to `App.Services.GetRequiredService<ICortexAmbientService>().AggregatedStatusText` via a mediator property in MainWindow's code-behind or a dedicated StatusBarViewModel.

In MainWindow.axaml.cs, expose:

```csharp
public string CortexStatusText => App.Services.GetService<ICortexAmbientService>()?.AggregatedStatusText ?? "";

// In ctor, subscribe:
if (App.Services.GetService<ICortexAmbientService>() is { } ambient)
{
    ambient.PropertyChanged += (_, _) =>
        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // Fire notify to rebind status bar
            DataContextChanged?.Invoke(this, EventArgs.Empty);
        });
}
```

In MainWindow.axaml status bar text:

```xml
<TextBlock Text="{Binding CortexStatusText, RelativeSource={RelativeSource AncestorType=Window}, StringFormat='✦ Cortex · {0}'}" />
```

> If MainWindow already uses a ViewModel, add `CortexStatusText` there instead. Adjust binding accordingly.

- [ ] **Step 33.3: Integration test**

Extend `tests/AuraCore.Tests.UI.Avalonia/Views/MainWindowTests.cs` with:

```csharp
[AvaloniaFact]
public void StatusBar_ReflectsAmbientState_AnyEnabled()
{
    var settings = new AppSettings { InsightsEnabled = true };
    var ambient = new CortexAmbientService(settings);
    // Assert ambient.AggregatedStatusText contains "Active"
    Assert.Contains("Active", ambient.AggregatedStatusText, StringComparison.OrdinalIgnoreCase);
}

[AvaloniaFact]
public void StatusBar_ReflectsAmbientState_AllDisabled()
{
    var settings = new AppSettings();
    var ambient = new CortexAmbientService(settings);
    Assert.Contains("Ready", ambient.AggregatedStatusText, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 33.4: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/MainWindow.axaml src/UI/AuraCore.UI.Avalonia/Views/MainWindow.axaml.cs tests/AuraCore.Tests.UI.Avalonia/Views/MainWindowTests.cs
git commit -m "feat(status-bar): bind CORTEX message to CortexAmbientService"
```

---

## Task 34: SidebarViewModel — wire IsLocked + add missing modules

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/ViewModels/SidebarViewModel.cs`
- Create: `tests/AuraCore.Tests.UI.Avalonia/ViewModels/SidebarViewModelTierLockTests.cs`

Spec §8.2 + §8.3. Add `ITierService`-driven `IsLocked`; add 10 missing modules with platform filtering.

- [ ] **Step 34.1: Inject ITierService into SidebarViewModel**

Edit `SidebarViewModel.cs`. Ensure the ctor accepts `ITierService tierService` and `UserTier currentTier` (the current user's tier — obtain from existing UserSession or hardcode `UserTier.Free` for now if session lookup isn't available):

```csharp
private readonly ITierService _tierService;
private readonly UserTier _currentTier;

public SidebarViewModel(ITierService? tierService = null, UserTier currentTier = UserTier.Free)
{
    _tierService = tierService ?? new TierService();
    _currentTier = currentTier;
}
```

- [ ] **Step 34.2: Wire IsLocked in BuildCategories**

In the module construction loop, set `IsLocked`:

```csharp
foreach (var module in category.Modules)
{
    var navItem = new SidebarNavItemViewModel // or whatever type wraps a module
    {
        Key = module.Key,
        LabelKey = module.LabelKey,
        Icon = module.Icon,
        IsLocked = _tierService.IsModuleLocked(module.Key, _currentTier),
    };
    // ...
}
```

If the sidebar items are rendered via a `SidebarNavItem` control (from Task 17), bind its `IsLocked` DP to this property.

- [ ] **Step 34.3: Add 10 missing modules**

Find `BuildCategories()` and add entries per spec §8.3:

```csharp
// Apps & Tools category — add system-health
appsCategory.Modules.Add(new SidebarModule
{
    Key = "system-health",
    LabelKey = "nav.module.system-health",
    Icon = "IconHeartPulse", // or similar — fall back to existing icon
    View = nameof(global::AuraCore.UI.Avalonia.Views.Pages.SystemHealthView), // verify actual class name
});

// Advanced category — add admin-panel
advancedCategory.Modules.Add(new SidebarModule
{
    Key = "admin-panel",
    LabelKey = "nav.module.admin-panel",
    Icon = "IconShield",
    View = nameof(global::AuraCore.UI.Avalonia.Views.Pages.AdminPanelView),
});

// Linux-only modules (inside existing OS check)
if (OperatingSystem.IsLinux())
{
    optimizeCategory.Modules.Add(new SidebarModule { Key = "systemd-manager", LabelKey = "nav.module.systemd-manager", View = nameof(global::AuraCore.UI.Avalonia.Views.Pages.SystemdManagerView) });
    cleanCategory.Modules.Add(new SidebarModule { Key = "package-cleaner", LabelKey = "nav.module.package-cleaner", View = nameof(global::AuraCore.UI.Avalonia.Views.Pages.PackageCleanerView) });
    optimizeCategory.Modules.Add(new SidebarModule { Key = "swap-optimizer", LabelKey = "nav.module.swap-optimizer", View = nameof(global::AuraCore.UI.Avalonia.Views.Pages.SwapOptimizerView) });
    advancedCategory.Modules.Add(new SidebarModule { Key = "cron-manager", LabelKey = "nav.module.cron-manager", View = nameof(global::AuraCore.UI.Avalonia.Views.Pages.CronManagerView) });
}

// macOS-only modules
if (OperatingSystem.IsMacOS())
{
    appsCategory.Modules.Add(new SidebarModule { Key = "defaults-optimizer", LabelKey = "nav.module.defaults-optimizer", View = nameof(global::AuraCore.UI.Avalonia.Views.Pages.DefaultsOptimizerView) });
    advancedCategory.Modules.Add(new SidebarModule { Key = "launchagent-manager", LabelKey = "nav.module.launchagent-manager", View = nameof(global::AuraCore.UI.Avalonia.Views.Pages.LaunchAgentManagerView) });
    appsCategory.Modules.Add(new SidebarModule { Key = "brew-manager", LabelKey = "nav.module.brew-manager", View = nameof(global::AuraCore.UI.Avalonia.Views.Pages.BrewManagerView) });
    securityCategory.Modules.Add(new SidebarModule { Key = "timemachine-manager", LabelKey = "nav.module.timemachine-manager", View = nameof(global::AuraCore.UI.Avalonia.Views.Pages.TimeMachineManagerView) });
}
```

> If the actual View class names differ from the guesses above, use `grep -rn "class XxxView : UserControl"` in `src/UI/AuraCore.UI.Avalonia/Views/Pages/` to find them and substitute. Some of these views may not exist yet — in that case, remove the module from the sidebar entry and file a bug (but do not block Phase 3 on them).

- [ ] **Step 34.4: Register SidebarViewModel in DI with ITierService**

In `App.axaml.cs` Phase 3 block (if SidebarViewModel isn't already registered):

```csharp
sc.AddSingleton<global::AuraCore.UI.Avalonia.ViewModels.SidebarViewModel>(sp =>
    new global::AuraCore.UI.Avalonia.ViewModels.SidebarViewModel(
        sp.GetRequiredService<ITierService>(),
        currentTier: ResolveCurrentTier()));

// Helper: obtains the current user's tier. Phase 3 returns Free by default.
// Phase 5 (Settings/Onboarding cohesion) wires this to UserSession.Tier.
static UserTier ResolveCurrentTier()
{
    // If Phase 2 introduced a user session object accessible statically, read it here.
    // Otherwise, Free is the safe default — locked modules still honor tier checks.
    return UserTier.Free;
}
```

- [ ] **Step 34.5: Write failing tests**

Create `tests/AuraCore.Tests.UI.Avalonia/ViewModels/SidebarViewModelTierLockTests.cs`:

```csharp
using AuraCore.UI.Avalonia.Services.AI;
using AuraCore.UI.Avalonia.ViewModels;
using System.Linq;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.ViewModels;

public class SidebarViewModelTierLockTests
{
    [Fact]
    public void FreeTier_AdminPanelItem_IsLocked()
    {
        var vm = new SidebarViewModel(new TierService(), UserTier.Free);
        vm.BuildCategories();

        var allItems = vm.Categories.SelectMany(c => c.Modules).ToList();
        var adminPanel = allItems.FirstOrDefault(m => m.Key == "admin-panel");
        Assert.NotNull(adminPanel);
        Assert.True(adminPanel!.IsLocked);
    }

    [Fact]
    public void AdminTier_AdminPanelItem_Unlocked()
    {
        var vm = new SidebarViewModel(new TierService(), UserTier.Admin);
        vm.BuildCategories();

        var adminPanel = vm.Categories.SelectMany(c => c.Modules).First(m => m.Key == "admin-panel");
        Assert.False(adminPanel.IsLocked);
    }

    [Fact]
    public void SystemHealth_ExistsInAppsTools()
    {
        var vm = new SidebarViewModel(new TierService(), UserTier.Free);
        vm.BuildCategories();

        var systemHealth = vm.Categories
            .SelectMany(c => c.Modules)
            .FirstOrDefault(m => m.Key == "system-health");
        Assert.NotNull(systemHealth);
    }
}
```

- [ ] **Step 34.6: Run tests**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~SidebarViewModelTierLockTests" --verbosity minimal
```
Expected: `Passed: 3, Failed: 0`.

- [ ] **Step 34.7: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/ViewModels/SidebarViewModel.cs src/UI/AuraCore.UI.Avalonia/App.axaml.cs tests/AuraCore.Tests.UI.Avalonia/ViewModels/SidebarViewModelTierLockTests.cs
git commit -m "feat(sidebar): wire IsLocked via ITierService + add 10 missing modules with platform filtering"
```

---

## Task 35: TierUpgradePlaceholderDialog

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Dialogs/TierUpgradePlaceholderDialog.axaml`
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Dialogs/TierUpgradePlaceholderDialog.axaml.cs`
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/MainWindow.axaml.cs` — hook click of locked sidebar item

Spec §8.2 `ShowTierUpgradeDialog`. Simple placeholder modal shown when user clicks a locked item.

- [ ] **Step 35.1: Create TierUpgradePlaceholderDialog.axaml**

Create `src/UI/AuraCore.UI.Avalonia/Views/Dialogs/TierUpgradePlaceholderDialog.axaml`:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="AuraCore.UI.Avalonia.Views.Dialogs.TierUpgradePlaceholderDialog"
        Width="420" Height="220"
        CanResize="False"
        WindowStartupLocation="CenterOwner"
        Title="Feature locked"
        SystemDecorations="BorderOnly">

  <Border Background="{DynamicResource BgSurface}"
          BorderBrush="{DynamicResource BorderEmphasis}"
          BorderThickness="1" CornerRadius="12">
    <Grid RowDefinitions="Auto,*,Auto" Margin="24">
      <StackPanel Grid.Row="0" Spacing="4">
        <PathIcon Data="{DynamicResource IconLock}" Width="18" Height="18"
                  Foreground="{DynamicResource AccentAmber}" HorizontalAlignment="Left" />
        <TextBlock Text="Feature locked" FontSize="16" FontWeight="SemiBold"
                   Foreground="{DynamicResource TextPrimary}" />
      </StackPanel>
      <TextBlock Grid.Row="1" Margin="0,12,0,0"
                 x:Name="BodyText"
                 Text=""
                 FontSize="12" Foreground="{DynamicResource TextSecondary}"
                 TextWrapping="Wrap" />
      <Button Grid.Row="2" Content="Close"
              Classes="primary"
              Click="OnCloseClick"
              HorizontalAlignment="Right" Padding="16,8" />
    </Grid>
  </Border>
</Window>
```

- [ ] **Step 35.2: Create code-behind**

Create `src/UI/AuraCore.UI.Avalonia/Views/Dialogs/TierUpgradePlaceholderDialog.axaml.cs`:

```csharp
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Markup.Xaml;
using AuraCore.UI.Avalonia.Services.AI;

namespace AuraCore.UI.Avalonia.Views.Dialogs;

public partial class TierUpgradePlaceholderDialog : Window
{
    public TierUpgradePlaceholderDialog()
    {
        InitializeComponent();
    }

    public TierUpgradePlaceholderDialog(string moduleKey, UserTier requiredTier) : this()
    {
        var body = this.FindControl<TextBlock>("BodyText");
        if (body is not null)
        {
            body.Text = $"This feature requires {requiredTier} tier. Contact admin to upgrade.";
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 35.3: Hook locked-click in MainWindow**

Edit `src/UI/AuraCore.UI.Avalonia/Views/MainWindow.axaml.cs`. Find the sidebar nav click handler. Before dispatching navigation, check if locked:

```csharp
private async void OnSidebarItemClick(SidebarModule module)
{
    var tierService = App.Services.GetRequiredService<ITierService>();
    // Phase 3: Free default. Phase 5 (Settings/Onboarding) wires to UserSession.Tier.
    var currentTier = UserTier.Free;

    if (tierService.IsModuleLocked(module.Key, currentTier))
    {
        var required = tierService.GetRequiredTier(module.Key);
        var dialog = new TierUpgradePlaceholderDialog(module.Key, required);
        await dialog.ShowDialog(this);
        return;
    }

    NavigateToModule(module.Key);
}
```

- [ ] **Step 35.4: Register in DI + commit**

Register dialog:

```csharp
sc.AddTransient<global::AuraCore.UI.Avalonia.Views.Dialogs.TierUpgradePlaceholderDialog>();
```

Commit:

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Dialogs/TierUpgradePlaceholderDialog.axaml src/UI/AuraCore.UI.Avalonia/Views/Dialogs/TierUpgradePlaceholderDialog.axaml.cs src/UI/AuraCore.UI.Avalonia/Views/MainWindow.axaml.cs src/UI/AuraCore.UI.Avalonia/App.axaml.cs
git commit -m "feat(ui): add TierUpgradePlaceholderDialog + locked-item click handler in MainWindow"
```

---

## Task 36: Add all Phase 3 localization keys (EN + TR)

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/LocalizationService.cs`

Spec §9. Add all keys for both locales. This is mechanical but must be complete — UI shows localized text.

- [ ] **Step 36.1: Locate the English + Turkish dictionaries**

Run:
```bash
grep -n "_english\|_turkish\|Turkish\|English" src/UI/AuraCore.UI.Avalonia/LocalizationService.cs | head -20
```

Note the line numbers where each dictionary begins.

- [ ] **Step 36.2: Add all EN keys**

In the English dictionary, after the existing Phase 2 entries, add:

```csharp
// ── Phase 3: AI Features ──
["nav.aiFeatures.title"] = "AI Features",
["nav.aiFeatures.badge"] = "CORTEX",

["aiFeatures.hero.kicker"] = "CORTEX",
["aiFeatures.hero.title"] = "AI Features",
["aiFeatures.hero.tagline"] = "Intelligent monitoring, predictions, and automation",
["aiFeatures.hero.status.active"] = "Active · Learning day {0}",
["aiFeatures.hero.status.paused"] = "Paused",
["aiFeatures.hero.status.ready"] = "Ready to start",
["aiFeatures.overview.navItem"] = "Overview",
["aiFeatures.card.insights.title"] = "Cortex Insights",
["aiFeatures.card.insights.previewSingular"] = "{0} active · {1} warning",
["aiFeatures.card.insights.previewPlural"] = "{0} active · {1} warnings",
["aiFeatures.card.recommendations.title"] = "Recommendations",
["aiFeatures.card.recommendations.preview"] = "{0} pending · {1} applied this week",
["aiFeatures.card.schedule.title"] = "Smart Schedule",
["aiFeatures.card.schedule.preview"] = "Next: {0} at {1}",
["aiFeatures.card.chat.title"] = "Chat",
["aiFeatures.card.chat.previewEnabled"] = "{0} · {1} messages",
["aiFeatures.card.chat.previewDisabled"] = "Not enabled",
["aiFeatures.card.chat.experimentalBadge"] = "EXPERIMENTAL",
["aiFeatures.card.paused.preview"] = "Paused",
["aiFeatures.section.paused.message"] = "This feature is paused",
["aiFeatures.section.paused.enableButton"] = "Enable",
["aiFeatures.chat.warningBanner"] = "⚠ Experimental — CORTEX Chat may produce inaccurate outputs. Verify before applying any suggestion.",

["chatOptIn.step1.title"] = "CORTEX Chat — Experimental",
["chatOptIn.step1.body"] = "CORTEX Chat uses a local AI model that may produce inaccurate or misleading outputs. Always verify any suggestions before applying them to your system.\n\nThis feature is under active development.",
["chatOptIn.step1.continueButton"] = "I understand, continue",
["chatOptIn.step1.cancelButton"] = "Cancel",
["chatOptIn.step2.title"] = "Choose your AI model",
["chatOptIn.step2.subtitle"] = "Please select a model to download. Models are pulled from AuraCore cloud. You can switch or download more later from the Chat header.",
["chatOptIn.stepIndicator"] = "Step {0} of {1}",

["modelManager.title.optIn"] = "Choose your AI model",
["modelManager.title.manage"] = "Manage AI Models",
["modelManager.tier.lite"] = "Lite",
["modelManager.tier.standard"] = "Standard",
["modelManager.tier.advanced"] = "Advanced",
["modelManager.tier.heavy"] = "Heavy",
["modelManager.tier.ramRequirement"] = "needs {0} GB+ RAM",
["modelManager.column.model"] = "Model",
["modelManager.column.size"] = "Size",
["modelManager.column.ram"] = "RAM",
["modelManager.column.speed"] = "Speed",
["modelManager.speed.fast"] = "FAST",
["modelManager.speed.medium"] = "MEDIUM",
["modelManager.speed.slow"] = "SLOW",
["modelManager.recommended"] = "RECOMMENDED",
["modelManager.installedBadge"] = "Installed",
["modelManager.downloadButton"] = "Download & use",
["modelManager.downloadManageButton"] = "Download",
["modelManager.cancelButton"] = "Cancel",
["modelManager.stats"] = "Disk free: {0} · Your RAM: {1}",

["modelManager.model.tinyllama.description"] = "Fastest · basic quality · good for quick questions",
["modelManager.model.phi3-mini-q4km.description"] = "Best balance · fine-tuned for PC optimization",
["modelManager.model.phi2.description"] = "Microsoft Phi-2 · stronger reasoning",
["modelManager.model.phi3-mini.description"] = "Full-precision Phi-3 mini · better fidelity",
["modelManager.model.mistral-7b.description"] = "High quality · multilingual",
["modelManager.model.llama31-8b.description"] = "Meta · high quality · latest",
["modelManager.model.phi3-medium.description"] = "Best reasoning in 14B class · workstation hardware",
["modelManager.model.qwen25-32b.description"] = "Highest quality · may not run on 32 GB systems",

["modelDownload.progress"] = "Downloading {0} · {1}% · {2} MB/s · ETA {3}",
["modelDownload.error.network"] = "Couldn't reach models.auracore.pro. Check your connection and try again.",
["modelDownload.error.timeout"] = "Download took too long. Try a smaller model or check your connection.",
["modelDownload.error.diskFull"] = "Not enough disk space. You need {0} GB free.",
["modelDownload.error.blocked"] = "Download blocked by server. Please contact support.",
["modelDownload.retryButton"] = "Retry",

["tier.lockedTooltip"] = "Upgrade to {0} to unlock",
["tier.upgrade.dialog.title"] = "Feature locked",
["tier.upgrade.dialog.body"] = "This feature requires {0} tier. Contact admin to upgrade.",

["nav.module.system-health"] = "System Health",
["nav.module.admin-panel"] = "Admin Panel",
["nav.module.systemd-manager"] = "Systemd Manager",
["nav.module.package-cleaner"] = "Package Cleaner",
["nav.module.swap-optimizer"] = "Swap Optimizer",
["nav.module.cron-manager"] = "Cron Manager",
["nav.module.defaults-optimizer"] = "Defaults Optimizer",
["nav.module.launchagent-manager"] = "Launch Agent Manager",
["nav.module.brew-manager"] = "Brew Manager",
["nav.module.timemachine-manager"] = "Time Machine Manager",
```

- [ ] **Step 36.3: Add all TR keys**

In the Turkish dictionary, add matching entries:

```csharp
// ── Phase 3: AI Features ──
["nav.aiFeatures.title"] = "Yapay Zekâ",
["nav.aiFeatures.badge"] = "CORTEX",

["aiFeatures.hero.kicker"] = "CORTEX",
["aiFeatures.hero.title"] = "Yapay Zekâ",
["aiFeatures.hero.tagline"] = "Akıllı izleme, tahminler ve otomasyon",
["aiFeatures.hero.status.active"] = "Aktif · Öğrenme günü {0}",
["aiFeatures.hero.status.paused"] = "Duraklatıldı",
["aiFeatures.hero.status.ready"] = "Başlamaya hazır",
["aiFeatures.overview.navItem"] = "Genel Bakış",
["aiFeatures.card.insights.title"] = "Cortex İçgörüler",
["aiFeatures.card.insights.previewSingular"] = "{0} aktif · {1} uyarı",
["aiFeatures.card.insights.previewPlural"] = "{0} aktif · {1} uyarı",
["aiFeatures.card.recommendations.title"] = "Öneriler",
["aiFeatures.card.recommendations.preview"] = "{0} bekliyor · bu hafta {1} uygulandı",
["aiFeatures.card.schedule.title"] = "Akıllı Zamanlama",
["aiFeatures.card.schedule.preview"] = "Sonraki: {0} · {1}",
["aiFeatures.card.chat.title"] = "Sohbet",
["aiFeatures.card.chat.previewEnabled"] = "{0} · {1} mesaj",
["aiFeatures.card.chat.previewDisabled"] = "Etkin değil",
["aiFeatures.card.chat.experimentalBadge"] = "DENEYSEL",
["aiFeatures.card.paused.preview"] = "Duraklatıldı",
["aiFeatures.section.paused.message"] = "Bu özellik duraklatıldı",
["aiFeatures.section.paused.enableButton"] = "Etkinleştir",
["aiFeatures.chat.warningBanner"] = "⚠ Deneysel — CORTEX Sohbet hatalı yanıtlar üretebilir. Herhangi bir öneri uygulamadan önce doğrulayın.",

["chatOptIn.step1.title"] = "CORTEX Sohbet — Deneysel",
["chatOptIn.step1.body"] = "CORTEX Sohbet yerel bir AI modeli kullanır ve hatalı veya yanıltıcı çıktılar üretebilir. Sisteminize uygulamadan önce tüm önerileri doğrulayın.\n\nBu özellik aktif geliştirme aşamasındadır.",
["chatOptIn.step1.continueButton"] = "Anladım, devam et",
["chatOptIn.step1.cancelButton"] = "İptal",
["chatOptIn.step2.title"] = "AI modelini seç",
["chatOptIn.step2.subtitle"] = "İndirmek için bir model seçin. Modeller AuraCore bulutundan çekilir. Daha sonra Sohbet başlığından model değiştirebilir veya yeni modeller indirebilirsiniz.",
["chatOptIn.stepIndicator"] = "Adım {0}/{1}",

["modelManager.title.optIn"] = "AI modelini seç",
["modelManager.title.manage"] = "AI Modellerini Yönet",
["modelManager.tier.lite"] = "Hafif",
["modelManager.tier.standard"] = "Standart",
["modelManager.tier.advanced"] = "Gelişmiş",
["modelManager.tier.heavy"] = "Ağır",
["modelManager.tier.ramRequirement"] = "en az {0} GB RAM gerekir",
["modelManager.column.model"] = "Model",
["modelManager.column.size"] = "Boyut",
["modelManager.column.ram"] = "RAM",
["modelManager.column.speed"] = "Hız",
["modelManager.speed.fast"] = "HIZLI",
["modelManager.speed.medium"] = "ORTA",
["modelManager.speed.slow"] = "YAVAŞ",
["modelManager.recommended"] = "ÖNERİLEN",
["modelManager.installedBadge"] = "Yüklü",
["modelManager.downloadButton"] = "İndir ve kullan",
["modelManager.downloadManageButton"] = "İndir",
["modelManager.cancelButton"] = "İptal",
["modelManager.stats"] = "Boş disk: {0} · RAM: {1}",

["modelManager.model.tinyllama.description"] = "En hızlısı · temel kalite · hızlı sorular için",
["modelManager.model.phi3-mini-q4km.description"] = "En dengelisi · PC optimizasyonu için özelleştirildi",
["modelManager.model.phi2.description"] = "Microsoft Phi-2 · daha güçlü akıl yürütme",
["modelManager.model.phi3-mini.description"] = "Tam hassasiyet Phi-3 mini · daha yüksek kalite",
["modelManager.model.mistral-7b.description"] = "Yüksek kalite · çok dilli",
["modelManager.model.llama31-8b.description"] = "Meta · yüksek kalite · en güncel",
["modelManager.model.phi3-medium.description"] = "14B sınıfında en güçlü · iş istasyonu donanımı gerektirir",
["modelManager.model.qwen25-32b.description"] = "En yüksek kalite · 32 GB sistemlerde çalışmayabilir",

["modelDownload.progress"] = "İndiriliyor {0} · %{1} · {2} MB/sn · KS {3}",
["modelDownload.error.network"] = "models.auracore.pro adresine ulaşılamadı. Bağlantınızı kontrol edip tekrar deneyin.",
["modelDownload.error.timeout"] = "İndirme çok uzun sürdü. Daha küçük bir model deneyin veya bağlantınızı kontrol edin.",
["modelDownload.error.diskFull"] = "Yeterli disk alanı yok. {0} GB boş alan gerekiyor.",
["modelDownload.error.blocked"] = "İndirme sunucu tarafından engellendi. Lütfen destek ile iletişime geçin.",
["modelDownload.retryButton"] = "Yeniden dene",

["tier.lockedTooltip"] = "Kilidi açmak için {0} sürümüne yükseltin",
["tier.upgrade.dialog.title"] = "Özellik kilitli",
["tier.upgrade.dialog.body"] = "Bu özellik {0} sürümünü gerektirir. Yükseltme için yönetici ile iletişime geçin.",

["nav.module.system-health"] = "Sistem Sağlığı",
["nav.module.admin-panel"] = "Yönetim Paneli",
["nav.module.systemd-manager"] = "Systemd Yöneticisi",
["nav.module.package-cleaner"] = "Paket Temizleyici",
["nav.module.swap-optimizer"] = "Swap Optimizasyonu",
["nav.module.cron-manager"] = "Cron Yöneticisi",
["nav.module.defaults-optimizer"] = "Defaults Optimizasyonu",
["nav.module.launchagent-manager"] = "Launch Agent Yöneticisi",
["nav.module.brew-manager"] = "Brew Yöneticisi",
["nav.module.timemachine-manager"] = "Time Machine Yöneticisi",
```

- [ ] **Step 36.4: Verification test — every EN key has a TR counterpart**

Create `tests/AuraCore.Tests.UI.Avalonia/LocalizationPhase3Tests.cs`:

```csharp
using AuraCore.UI.Avalonia;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia;

public class LocalizationPhase3Tests
{
    private static readonly string[] Phase3Keys = new[]
    {
        "nav.aiFeatures.title", "aiFeatures.hero.title", "aiFeatures.hero.status.active",
        "aiFeatures.hero.status.paused", "aiFeatures.hero.status.ready",
        "aiFeatures.card.chat.experimentalBadge", "aiFeatures.chat.warningBanner",
        "chatOptIn.step1.title", "chatOptIn.step1.continueButton", "chatOptIn.step2.title",
        "modelManager.title.optIn", "modelManager.title.manage",
        "modelManager.tier.lite", "modelManager.tier.standard", "modelManager.tier.advanced", "modelManager.tier.heavy",
        "modelManager.speed.fast", "modelManager.speed.medium", "modelManager.speed.slow",
        "modelManager.recommended", "modelManager.downloadButton",
        "modelManager.model.tinyllama.description",
        "modelManager.model.phi3-mini-q4km.description",
        "modelDownload.error.network", "modelDownload.error.blocked",
        "tier.lockedTooltip", "tier.upgrade.dialog.title",
        "nav.module.system-health", "nav.module.admin-panel",
    };

    [Theory]
    [MemberData(nameof(AllKeys))]
    public void EachKey_ResolvesInEnglish(string key)
    {
        LocalizationService.CurrentLanguage = "en";
        var value = LocalizationService._(key);
        Assert.NotEqual(key, value); // should not echo the key back
        Assert.False(string.IsNullOrWhiteSpace(value));
    }

    [Theory]
    [MemberData(nameof(AllKeys))]
    public void EachKey_ResolvesInTurkish(string key)
    {
        LocalizationService.CurrentLanguage = "tr";
        var value = LocalizationService._(key);
        Assert.NotEqual(key, value);
        Assert.False(string.IsNullOrWhiteSpace(value));
    }

    public static IEnumerable<object[]> AllKeys =>
        Phase3Keys.Select(k => new object[] { k });
}
```

- [ ] **Step 36.5: Run tests**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~LocalizationPhase3Tests" --verbosity minimal
```
Expected: all keys resolve in both locales. If any fail: add missing entries.

- [ ] **Step 36.6: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/LocalizationService.cs tests/AuraCore.Tests.UI.Avalonia/LocalizationPhase3Tests.cs
git commit -m "feat(i18n): add Phase 3 localization keys (EN + TR) for AI Features, dialogs, tiers"
```

---

## Task 37: Full test suite run + fix regressions

**Files:** None created. Verification only.

Spec §14. Target: **~343 tests passing** (283 Phase 2 baseline + ~60 new).

- [ ] **Step 37.1: Run full solution test suite**

Run:
```bash
dotnet test AuraCorePro.sln --configuration Debug --verbosity minimal 2>&1 | tee /tmp/phase3-tests.log | tail -20
```

Expected: `Passed: ~343, Failed: 0`.

- [ ] **Step 37.2: Investigate any failures**

If `Failed > 0`, inspect the log:

```bash
grep -E "Failed|FAIL" /tmp/phase3-tests.log | head -30
```

Common causes:
- A test referring to deleted classes (`AIModelSettings`, `AIInsightsView`, etc.) — update or delete the test.
- A test expecting an old XAML element name — update to new name (e.g., `ScheduleSection` instead of `SchedulerView`).
- A localization test finding an unmapped key — add missing EN/TR entry.

Fix each failure, re-run. Do not proceed to next task until 0 failures.

- [ ] **Step 37.3: Verify test count**

Run:
```bash
dotnet test AuraCorePro.sln --configuration Debug --verbosity normal 2>&1 | grep -E "Passed:|Failed:" | tail -10
```

Confirm ~343 total. Acceptable range: 330-360 (minor variance from local overrides).

- [ ] **Step 37.4: Commit any test fixes**

```bash
git add -A
git commit -m "test: fix Phase 3 regressions found during full suite run"
```

(Skip this step if no fixes were needed.)

---

## Task 38: Manual visual checks

**Files:** None. Manual QA per spec §11.5.

- [ ] **Step 38.1: Build Release and run**

```bash
dotnet build AuraCorePro.sln --configuration Release --verbosity minimal
dotnet run --project src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj --configuration Release
```

- [ ] **Step 38.2: Walk through §11.5 checklist**

1. Login screen → no crash.
2. Dashboard renders, Cortex Insights card visible.
3. Click sidebar "✦ AI Features [CORTEX]" (single link, no accordion).
4. AIFeaturesView: hero + 2×2 grid.
5. Click Insights card → detail mode (sidebar nav + Insights content).
6. Click "Overview" → returns to 2×2 grid.
7. Toggle Insights OFF → dashboard Cortex Insights card swaps to "paused" placeholder.
8. Toggle Chat ON → ChatOptInDialog Step 1.
9. Click "I understand, continue" → Step 2 ModelManagerDialog.
10. Select Phi-3 Mini Q4KM → Download enabled → click → progress bar → completion.
11. ChatSection active, model chip shows Phi-3 Mini Q4KM.
12. Click model chip dropdown → "Download more..." → Manage dialog opens.
13. Resize window < 1000 px → AIFeatures narrow mode (1-column overview).
14. Click a locked module (if any) → TierUpgradePlaceholderDialog.
15. Linux VM (if available): `systemd-manager` visible in Optimize. macOS VM: `brew-manager` in Apps & Tools.

- [ ] **Step 38.3: Document any visual issues**

If anything renders wrong:
- Take a screenshot.
- Note the step number.
- File an issue (in-repo or inline fix).
- Fix before milestone commit.

---

## Task 39: Milestone commit

- [ ] **Step 39.1: Ensure clean working tree**

```bash
git status
```

Expected: all intended changes committed. If build artifacts are dirty, ignore them (project pattern per Phase 2 carryover).

- [ ] **Step 39.2: Create milestone commit**

```bash
git commit --allow-empty -m "$(cat <<'EOF'
milestone: Phase 3 AI Features Consolidation complete

Delivers per docs/superpowers/specs/2026-04-15-phase3-ai-features-consolidation-design.md:

Architecture:
- Unified AIFeaturesView (hero + 2x2 overview + hybrid drill-in)
- 4 sections moved to Views/Pages/AI/ + refreshed with Phase 1 primitives
- ViewModels: AIFeaturesViewModel + AIFeatureCardVM + ChatOptInDialogViewModel + ModelManagerDialogViewModel

Services/AI:
- IModelCatalog (8 models), IModelDownloadService (R2 + size verify),
  IInstalledModelStore, ICortexAmbientService, ITierService

Dialogs:
- ChatOptInDialog (2-step: experimental ack + model select)
- ModelManagerDialog (OptIn + Manage modes, RAM-aware)
- TierUpgradePlaceholderDialog (locked-click placeholder)

Sidebar (Phase 2.5 carry-overs):
- Single AI Features link (accordion removed)
- IsLocked DP on SidebarNavItem + tier locking
- 10 missing modules added (system-health, admin-panel, Linux x4, macOS x4)

Ripple:
- CortexAmbientService aggregates toggle state
- Dashboard Cortex Insights card, Smart Optimize CTA, header chip
- Status bar CORTEX message

Infrastructure (set up 2026-04-15):
- R2 custom domain models.auracore.pro
- Cache Rule 1y TTL + HSTS + TLS 1.2 + Bot Fight Mode

Tests: ~343 passing (283 baseline + ~60 new). Zero regressions.
Localization: EN + TR complete.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 39.3: Push branch (optional)**

```bash
git push -u origin phase-3-ai-features
```

Only if user wants branch on remote. Do not merge to main without explicit review.

---

## Implementation Complete

At this point, Phase 3 is shipped:

- Branch `phase-3-ai-features` contains all changes as atomic commits.
- Milestone commit marks the end.
- Spec §14 success criteria all satisfied.
- Follow-up work (Phase 4+): Settings > Models page, model delete/pause/resume, V1 theme bridge removal.

Next: run retrospective against vision doc / spec before starting Phase 4.
