# Phase 4.0 — Module Page Pattern + Pilot Batch — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build 3 shared layout primitives (`ModuleHeader`, `StatCard`, `StatRow`) and migrate 5 pilot Windows modules (DnsBenchmark → HostsEditor → AutorunManager → FirewallRules → RamOptimizer) to a consistent card-based shell. Establish the pattern + methodology that Phase 4.1–4.5 batches will consume to migrate the remaining 32 modules and build 13 new Linux/macOS modules.

**Architecture:** Three new `UserControl` / subclass primitives in `Views/Controls/`, each with `AvaloniaProperty` DPs following the Phase 1 pattern (see `AccentBadge`, `StatusChip` for precedent). No new ViewModels — primitives are pure leaf UI. Pilot migrations rewrite `.axaml` only; `.axaml.cs` preserved byte-for-byte except for added `using` imports. V2 theme tokens exclusive; V1 bridge keys (SuccessBrush/ErrorBrush/BorderCardBrush) forbidden in new code.

**Tech Stack:** Avalonia 11.2.7, xUnit 2.9.2, Avalonia.Headless.XUnit 11.2.7. No new NuGet packages.

---

## Context & References

- **Spec (authoritative):** `docs/superpowers/specs/2026-04-16-phase4-module-pattern-design.md` — spec sections referenced as §X.Y.
- **Vision Doc:** `docs/superpowers/specs/2026-04-14-ui-rebuild-vision-document.md` §10 Phase 4.
- **Phase 3 plan (pattern reference):** `docs/superpowers/plans/2026-04-15-phase3-ai-features-consolidation.md` — codebase quirks and test patterns established here are reused.
- **Phase 1 primitives for precedent:**
  - `src/UI/AuraCore.UI.Avalonia/Views/Controls/AccentBadge.axaml[.cs]` — simplest DP pattern
  - `src/UI/AuraCore.UI.Avalonia/Views/Controls/StatusChip.axaml[.cs]` — `IBrush` DP pattern
  - `src/UI/AuraCore.UI.Avalonia/Views/Controls/GlassCard.axaml[.cs]` — card content primitive (pilot modules will wrap content in this)
- **Test helper:** `tests/AuraCore.Tests.UI.Avalonia/AvaloniaTestBase.cs` — `RenderInWindow` + `TestWindowHandle`.
- **Phase 3 regression-test pattern:** `tests/AuraCore.Tests.UI.Avalonia/Views/ScheduleSectionTests.cs` — the `Dispatcher.UIThread.RunJobs()` pump for Loaded events.

---

## Plan-level decisions (discovered during plan prep)

Spec §4 referenced approximate token names. Codebase inspection nailed down exact keys.

### 1. V2 token key names

Spec §4.4 mentioned "SuccessBrush / WarningBrush / ErrorBrush / BorderCardBrush" as the V2 tokens to use. Actual token names in `AuraCoreThemeV2.axaml`:

| Spec said | Actual V2 key |
|-----------|---------------|
| `SuccessBrush` | `StatusSuccessBrush` (#00D4AA) |
| `WarningBrush` | `StatusWarningBrush` (#F59E0B) |
| `ErrorBrush` | `StatusErrorBrush` (#EF4444) |
| `BorderCardBrush` | `BorderSubtleBrush` (#0FFFFFFF) |

The bare-named `SuccessBrush`/`WarningBrush`/`ErrorBrush`/`BorderCardBrush` keys exist **only in V1 bridge** (`AuraCoreTheme.axaml`). Using those means pulling from the bridge — which spec §4.4 explicitly forbids.

**Plan locks V2 key names:** `StatusSuccessBrush`, `StatusWarningBrush`, `StatusErrorBrush`, `BorderSubtleBrush`. All new primitives and migrated modules use these. Any use of the bare names is treated as a V1-bridge leak and rejected in code review.

### 2. `StatCard.Label` uppercase convention

Spec §4.2 says label "10 px uppercase". Avalonia TextBlock has **no `TextTransform="Uppercase"` property** (documented Phase 3 debt). The plan adopts the Phase 1 convention (see `StatusChip.Label="CORTEX"`, `AccentBadge.Label="ADMIN"`): **caller passes uppercase strings directly**. Primitive styles the label with size 10 + `LetterSpacing="0.5"` + SemiBold but does not enforce uppercasing.

### 3. `StatRow` — plain subclass, no XAML

Spec §4.3 suggests a `UserControl` with internal `UniformGrid`. Simpler: **subclass `UniformGrid` directly**, set `Rows=1` in the constructor. Children flow into `Children` naturally. No `.axaml` file, no `InitializeComponent`, ~10 lines total vs ~40 for a wrapping UserControl.

This diverges from the spec's file-count sketch ("9 new files") — actual new files = 8 (3 primitives × 2 files each for ModuleHeader/StatCard + 1 file for StatRow + 3 test files − 1 redundant primitive XAML = 8). Spec §4.5 file-layout box is updated by this plan.

### 4. `TextBlock.IsVisible` gating for `ModuleHeader.Subtitle`

Spec §4.1 says subtitle "collapses when `null`". Avalonia supports this via a static converter:

```xml
IsVisible="{Binding #Root.Subtitle, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"
```

`StringConverters.IsNotNullOrEmpty` is a built-in Avalonia value converter in `Avalonia.Data.Converters`. No custom converter needed. Empty string `""` also collapses (desirable — caller passing blank intentionally).

### 5. Pilot commit order matches spec §5 (easy → hard)

Strictly serial: primitives first, then modules in the order DnsBenchmark → HostsEditor → AutorunManager → FirewallRules → RamOptimizer. Full test suite runs between every module; any failure outside the new test files pauses migration until fixed.

---

## Codebase quirks (must-know — from Phase 3 plan, still true)

1. **`AuraCore.Application` namespace shadows `Avalonia.Application`.** Use `global::Avalonia.X` or `using global::Avalonia;`.
2. **Assembly name is `AuraCore.Pro`.** All `avares://` URIs use `AuraCore.Pro` host.
3. **Avalonia 11.2.7 Grid has no `RowSpacing` / `ColumnSpacing`.** Use `Margin`.
4. **Avalonia Window doesn't implement IDisposable.** Use `TestWindowHandle` wrapper from `AvaloniaTestBase`.
5. **Reference-type StyledProperty defaults are SHARED across instances.** For collection-typed DPs use `SetCurrentValue(prop, new Instance())` in ctor. (Not needed in Phase 4.0 — all DPs are value types or strings.)
6. **`HeadlessWindowExtensions.GetLastRenderedFrame` needs Skia + `UseHeadlessDrawing=false`.** Use `Measure`/`Arrange` via `AvaloniaTestBase.RenderInWindow` instead.
7. **Theme V2 brushes are `ThemeVariant.Dark`-scoped.** Code-behind resource lookups must pass `ActualThemeVariant ?? ThemeVariant.Dark`. XAML `DynamicResource` handles this transparently — primitives use XAML-only resource binding, so this quirk only matters if a primitive later adds code-behind `FindResource` (none do).
8. **Avalonia `Loaded` event fires asynchronously** in headless tests. When a test needs the Loaded handler to have run, call `global::Avalonia.Threading.Dispatcher.UIThread.RunJobs()` after `RenderInWindow`. (Not needed for ModuleHeader/StatCard/StatRow since they have no Loaded logic, but required for module smoke tests in Tasks 4–8.)
9. **Code-behind `x:Name` references** in the 5 pilot modules are linked via the generated `.g.cs` init. Moving a named element in XAML means re-wiring in code-behind. Plan strategy: **keep every `x:Name` in its original semantic position**, just wrap the surrounding structure in primitives.

---

## File Structure

### Created (8 files)

```
src/UI/AuraCore.UI.Avalonia/Views/Controls/
├── ModuleHeader.axaml
├── ModuleHeader.axaml.cs
├── StatCard.axaml
├── StatCard.axaml.cs
└── StatRow.cs                           (plain subclass — no .axaml per decision 3)

tests/AuraCore.Tests.UI.Avalonia/Controls/
├── ModuleHeaderTests.cs
├── StatCardTests.cs
└── StatRowTests.cs
```

### Created (5 smoke test files, one per pilot module)

```
tests/AuraCore.Tests.UI.Avalonia/Views/
├── DnsBenchmarkViewTests.cs
├── HostsEditorViewTests.cs
├── AutorunManagerViewTests.cs
├── FirewallRulesViewTests.cs
└── RamOptimizerViewTests.cs
```

### Modified (5 pilot `.axaml` files — `.axaml.cs` untouched)

```
src/UI/AuraCore.UI.Avalonia/Views/Pages/
├── DnsBenchmarkView.axaml    (30 lines → ~20)
├── HostsEditorView.axaml     (197 lines → ~110)
├── AutorunManagerView.axaml  (132 lines → ~75)
├── FirewallRulesView.axaml   (90 lines → ~50)
└── RamOptimizerView.axaml    (159 lines → ~95)
```

Total: **13 created + 5 modified = 18 files touched.** 10 atomic commits (see §Task 10).

---

## Task 1: ModuleHeader primitive

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Controls/ModuleHeader.axaml`
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Controls/ModuleHeader.axaml.cs`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Controls/ModuleHeaderTests.cs`

Per spec §4.1. Title + optional subtitle + right-aligned Actions slot.

- [ ] **Step 1.1: Write failing tests**

Create `tests/AuraCore.Tests.UI.Avalonia/Controls/ModuleHeaderTests.cs`:

```csharp
using AuraCore.UI.Avalonia.Views.Controls;
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Controls;

public class ModuleHeaderTests
{
    [AvaloniaFact]
    public void Ctor_DoesNotThrow()
    {
        var h = new ModuleHeader();
        Assert.NotNull(h);
        Assert.Equal(string.Empty, h.Title);
        Assert.Null(h.Subtitle);
        Assert.Null(h.Actions);
    }

    [AvaloniaFact]
    public void Title_Property_RoundTrips()
    {
        var h = new ModuleHeader { Title = "Firewall Rules" };
        Assert.Equal("Firewall Rules", h.Title);
    }

    [AvaloniaFact]
    public void Subtitle_Property_RoundTrips()
    {
        var h = new ModuleHeader { Subtitle = "Manage inbound/outbound" };
        Assert.Equal("Manage inbound/outbound", h.Subtitle);
    }

    [AvaloniaFact]
    public void Actions_AcceptsArbitraryContent()
    {
        var btn = new Button { Content = "Scan" };
        var h = new ModuleHeader { Actions = btn };
        Assert.Same(btn, h.Actions);
    }

    [AvaloniaFact]
    public void RendersInWindow_WithTitleOnly()
    {
        var h = new ModuleHeader { Title = "DNS Benchmark" };
        using var handle = AvaloniaTestBase.RenderInWindow(h, 800, 80);
        Assert.True(h.IsMeasureValid);
    }

    [AvaloniaFact]
    public void RendersInWindow_WithTitleSubtitleAndActions()
    {
        var h = new ModuleHeader
        {
            Title = "Firewall Rules",
            Subtitle = "Manage Windows Firewall inbound/outbound",
            Actions = new Button { Content = "Scan Rules" },
        };
        using var handle = AvaloniaTestBase.RenderInWindow(h, 800, 80);
        Assert.True(h.IsMeasureValid);
    }
}
```

- [ ] **Step 1.2: Run tests to verify fail**

```bash
cd C:/Users/Admin/Desktop/AuraCorePro/AuraCorePro
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~ModuleHeaderTests" --verbosity minimal --nologo 2>&1 | tail -10
```

Expected: build FAILS with "The type or namespace name 'ModuleHeader' could not be found". This is the TDD red.

- [ ] **Step 1.3: Create `ModuleHeader.axaml`**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/ModuleHeader.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:conv="using:Avalonia.Data.Converters"
             x:Class="AuraCore.UI.Avalonia.Views.Controls.ModuleHeader"
             x:Name="Root">
  <Grid ColumnDefinitions="*,Auto" Margin="0,0,0,16">
    <StackPanel Grid.Column="0" VerticalAlignment="Center" Spacing="2">
      <TextBlock Text="{Binding #Root.Title}"
                 FontSize="{DynamicResource FontSizeHeading}"
                 FontWeight="SemiBold"
                 Foreground="{DynamicResource TextPrimaryBrush}" />
      <TextBlock Text="{Binding #Root.Subtitle}"
                 FontSize="{DynamicResource FontSizeBodySmall}"
                 Foreground="{DynamicResource TextMutedBrush}"
                 IsVisible="{Binding #Root.Subtitle, Converter={x:Static conv:StringConverters.IsNotNullOrEmpty}}" />
    </StackPanel>
    <ContentPresenter Grid.Column="1"
                      Content="{Binding #Root.Actions}"
                      VerticalAlignment="Center"
                      HorizontalAlignment="Right" />
  </Grid>
</UserControl>
```

- [ ] **Step 1.4: Create `ModuleHeader.axaml.cs`**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/ModuleHeader.axaml.cs`:

```csharp
using global::Avalonia;
using global::Avalonia.Controls;

namespace AuraCore.UI.Avalonia.Views.Controls;

/// <summary>
/// Phase 4.0 Module Page shell header — title + optional subtitle on the left,
/// optional right-aligned <see cref="Actions"/> slot for buttons / toggle clusters.
/// Spec §4.1. Theme V2 tokens only.
/// </summary>
public partial class ModuleHeader : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ModuleHeader, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<ModuleHeader, string?>(nameof(Subtitle));

    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<ModuleHeader, object?>(nameof(Actions));

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    public ModuleHeader()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 1.5: Run tests to verify pass**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~ModuleHeaderTests" --verbosity minimal --nologo 2>&1 | tail -5
```

Expected: `Passed: 6, Failed: 0`.

- [ ] **Step 1.6: Run full test suite to confirm zero regressions**

```bash
dotnet test AuraCorePro.sln --verbosity minimal --nologo 2>&1 | grep -E "^Passed!|^Failed!"
```

Expected: all test assemblies pass. Total: 481 → 487 (+6 new).

- [ ] **Step 1.7: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Controls/ModuleHeader.axaml \
        src/UI/AuraCore.UI.Avalonia/Views/Controls/ModuleHeader.axaml.cs \
        tests/AuraCore.Tests.UI.Avalonia/Controls/ModuleHeaderTests.cs
git commit -m "$(cat <<'EOF'
feat(ui): add ModuleHeader primitive (Phase 4.0 spec §4.1)

Title + optional subtitle + optional right-aligned Actions slot.
Theme V2 tokens exclusive (FontSizeHeading, FontSizeBodySmall,
TextPrimaryBrush, TextMutedBrush). Subtitle collapses via built-in
Avalonia StringConverters.IsNotNullOrEmpty when null/empty.

6 tests: ctor defaults, property round-trips, render-in-window
(title-only + full combination).

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: StatCard primitive

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Controls/StatCard.axaml`
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Controls/StatCard.axaml.cs`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Controls/StatCardTests.cs`

Per spec §4.2. Uppercase label + bold value + bindable value brush.

- [ ] **Step 2.1: Write failing tests**

Create `tests/AuraCore.Tests.UI.Avalonia/Controls/StatCardTests.cs`:

```csharp
using AuraCore.UI.Avalonia.Views.Controls;
using global::Avalonia.Media;
using global::Avalonia.Headless.XUnit;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Controls;

public class StatCardTests
{
    [AvaloniaFact]
    public void Ctor_DoesNotThrow_AndSetsDefaults()
    {
        var c = new StatCard();
        Assert.NotNull(c);
        Assert.Equal(string.Empty, c.Label);
        Assert.Equal("--", c.Value);
        Assert.Same(Brushes.White, c.ValueBrush); // fallback default (Phase 4.0 decision)
    }

    [AvaloniaFact]
    public void Label_Property_RoundTrips()
    {
        var c = new StatCard { Label = "TOTAL" };
        Assert.Equal("TOTAL", c.Label);
    }

    [AvaloniaFact]
    public void Value_Property_RoundTrips()
    {
        var c = new StatCard { Value = "247" };
        Assert.Equal("247", c.Value);
    }

    [AvaloniaFact]
    public void Value_EmptyString_IsAllowed()
    {
        var c = new StatCard { Value = "" };
        Assert.Equal("", c.Value);
    }

    [AvaloniaFact]
    public void ValueBrush_AcceptsSuccessBrush()
    {
        var green = new SolidColorBrush(Colors.Green);
        var c = new StatCard { ValueBrush = green };
        Assert.Same(green, c.ValueBrush);
    }

    [AvaloniaFact]
    public void ValueBrush_AcceptsErrorBrush()
    {
        var red = new SolidColorBrush(Colors.Red);
        var c = new StatCard { ValueBrush = red };
        Assert.Same(red, c.ValueBrush);
    }

    [AvaloniaFact]
    public void RendersInWindow_WithDefaults()
    {
        var c = new StatCard();
        using var handle = AvaloniaTestBase.RenderInWindow(c, 160, 80);
        Assert.True(c.IsMeasureValid);
    }

    [AvaloniaFact]
    public void RendersInWindow_WithFullData()
    {
        var c = new StatCard
        {
            Label = "ENABLED",
            Value = "198",
            ValueBrush = new SolidColorBrush(Colors.LimeGreen),
        };
        using var handle = AvaloniaTestBase.RenderInWindow(c, 160, 80);
        Assert.True(c.IsMeasureValid);
    }
}
```

- [ ] **Step 2.2: Run tests to verify fail**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~StatCardTests" --verbosity minimal --nologo 2>&1 | tail -5
```

Expected: build FAILS with "The type or namespace name 'StatCard' could not be found".

- [ ] **Step 2.3: Create `StatCard.axaml`**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/StatCard.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="AuraCore.UI.Avalonia.Views.Controls.StatCard"
             x:Name="Root">
  <Border CornerRadius="{DynamicResource RadiusMd}"
          Background="{DynamicResource BgCardBrush}"
          BorderBrush="{DynamicResource BorderSubtleBrush}"
          BorderThickness="1"
          Padding="12,10">
    <StackPanel Spacing="4">
      <TextBlock Text="{Binding #Root.Label}"
                 FontSize="{DynamicResource FontSizeLabel}"
                 FontWeight="SemiBold"
                 LetterSpacing="0.5"
                 Foreground="{DynamicResource TextMutedBrush}" />
      <TextBlock Text="{Binding #Root.Value}"
                 FontSize="{DynamicResource FontSizeDisplay}"
                 FontWeight="Bold"
                 Foreground="{Binding #Root.ValueBrush}" />
    </StackPanel>
  </Border>
</UserControl>
```

- [ ] **Step 2.4: Create `StatCard.axaml.cs`**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/StatCard.axaml.cs`:

```csharp
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Controls;

/// <summary>
/// Phase 4.0 Module Page shell stat card — uppercase label + large bold value
/// colored by an <see cref="IBrush"/> DP. Caller passes uppercase text and a
/// DynamicResource brush; default fallback is <see cref="Brushes.White"/>.
/// Spec §4.2. Theme V2 tokens only.
/// </summary>
public partial class StatCard : UserControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<StatCard, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<StatCard, string>(nameof(Value), "--");

    public static readonly StyledProperty<IBrush> ValueBrushProperty =
        AvaloniaProperty.Register<StatCard, IBrush>(nameof(ValueBrush), Brushes.White);

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public IBrush ValueBrush
    {
        get => GetValue(ValueBrushProperty);
        set => SetValue(ValueBrushProperty, value);
    }

    public StatCard()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 2.5: Run tests to verify pass**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~StatCardTests" --verbosity minimal --nologo 2>&1 | tail -5
```

Expected: `Passed: 8, Failed: 0`.

- [ ] **Step 2.6: Run full test suite**

```bash
dotnet test AuraCorePro.sln --verbosity minimal --nologo 2>&1 | grep -E "^Passed!|^Failed!"
```

Expected: Total 487 → 495 (+8 new).

- [ ] **Step 2.7: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Controls/StatCard.axaml \
        src/UI/AuraCore.UI.Avalonia/Views/Controls/StatCard.axaml.cs \
        tests/AuraCore.Tests.UI.Avalonia/Controls/StatCardTests.cs
git commit -m "$(cat <<'EOF'
feat(ui): add StatCard primitive (Phase 4.0 spec §4.2)

Uppercase label + large bold value + bindable ValueBrush IBrush DP
(matches StatusChip.AccentBrush pattern). Default brush Brushes.White
as fallback; callers pass DynamicResource tokens like
StatusSuccessBrush / StatusErrorBrush from V2 theme.

Layout: Border with RadiusMd + BgCardBrush + BorderSubtleBrush +
12,10 padding. StackPanel spacing=4 with label (FontSizeLabel,
SemiBold, LetterSpacing=0.5, TextMutedBrush) + value
(FontSizeDisplay, Bold, ValueBrush).

8 tests: ctor defaults, property round-trips, empty value allowed,
Success/Error brush acceptance, render-in-window (defaults + full).

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: StatRow primitive

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Controls/StatRow.cs` (plain subclass, no XAML per Plan decision 3)
- Create: `tests/AuraCore.Tests.UI.Avalonia/Controls/StatRowTests.cs`

Per spec §4.3. Layout-only — `UniformGrid` with `Rows=1`, children flow directly.

- [ ] **Step 3.1: Write failing tests**

Create `tests/AuraCore.Tests.UI.Avalonia/Controls/StatRowTests.cs`:

```csharp
using AuraCore.UI.Avalonia.Views.Controls;
using global::Avalonia.Headless.XUnit;
using global::Avalonia.Media;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Controls;

public class StatRowTests
{
    [AvaloniaFact]
    public void Ctor_SetsRowsToOne()
    {
        var row = new StatRow();
        Assert.Equal(1, row.Rows);
    }

    [AvaloniaFact]
    public void Empty_RendersInWindow_WithoutCrash()
    {
        var row = new StatRow();
        using var handle = AvaloniaTestBase.RenderInWindow(row, 600, 60);
        Assert.True(row.IsMeasureValid);
    }

    [AvaloniaFact]
    public void ThreeCards_AddedToChildren_AndRender()
    {
        var row = new StatRow();
        row.Children.Add(new StatCard { Label = "TOTAL", Value = "247" });
        row.Children.Add(new StatCard { Label = "ENABLED", Value = "198" });
        row.Children.Add(new StatCard { Label = "BLOCKED", Value = "49" });

        using var handle = AvaloniaTestBase.RenderInWindow(row, 600, 80);
        Assert.Equal(3, row.Children.Count);
        Assert.True(row.IsMeasureValid);
    }

    [AvaloniaFact]
    public void TwoCards_EquallyDivideWidth()
    {
        // UniformGrid with Rows=1 and no Columns set auto-computes cols = child count.
        // 2 cards at 600 px => each card ~300 px.
        var row = new StatRow();
        var a = new StatCard { Label = "A", Value = "1" };
        var b = new StatCard { Label = "B", Value = "2" };
        row.Children.Add(a);
        row.Children.Add(b);

        using var handle = AvaloniaTestBase.RenderInWindow(row, 600, 80);
        // Arrange already ran inside RenderInWindow; bounds should be populated
        var aWidth = a.Bounds.Width;
        var bWidth = b.Bounds.Width;
        Assert.True(aWidth > 0 && bWidth > 0, "Both cards should have non-zero width after layout");
        // Allow small rounding delta; UniformGrid halves the row
        Assert.InRange(aWidth, bWidth - 1, bWidth + 1);
    }

    [AvaloniaFact]
    public void FourCards_EquallyDivideWidth()
    {
        var row = new StatRow();
        for (int i = 0; i < 4; i++)
            row.Children.Add(new StatCard { Label = $"S{i}", Value = $"{i}" });

        using var handle = AvaloniaTestBase.RenderInWindow(row, 800, 80);
        var widths = row.Children.Select(c => c.Bounds.Width).ToList();
        Assert.All(widths, w => Assert.True(w > 0));
        Assert.InRange(widths.Max() - widths.Min(), 0, 2); // within 2 px of each other
    }
}
```

Note: the last test uses `System.Linq` — make sure `using System.Linq;` is added if not auto-included.

- [ ] **Step 3.2: Run tests to verify fail**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~StatRowTests" --verbosity minimal --nologo 2>&1 | tail -5
```

Expected: build FAILS with "The type or namespace name 'StatRow' could not be found".

- [ ] **Step 3.3: Create `StatRow.cs`**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/StatRow.cs` (NO `.axaml` per Plan decision 3):

```csharp
using global::Avalonia.Controls.Primitives;

namespace AuraCore.UI.Avalonia.Views.Controls;

/// <summary>
/// Phase 4.0 Module Page shell stat row — layout-only primitive.
/// Subclasses <see cref="UniformGrid"/> with <c>Rows=1</c> pre-set so
/// child <see cref="StatCard"/>s lay out equal-width side by side.
/// No XAML file needed — this is a trivial subclass (see plan §Plan-level
/// decisions #3). Children flow into <see cref="UniformGrid.Children"/>
/// via the standard Panel semantics.
/// Spec §4.3.
/// </summary>
public class StatRow : UniformGrid
{
    public StatRow()
    {
        Rows = 1;
    }
}
```

- [ ] **Step 3.4: Run tests to verify pass**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~StatRowTests" --verbosity minimal --nologo 2>&1 | tail -5
```

Expected: `Passed: 5, Failed: 0`.

- [ ] **Step 3.5: Run full test suite**

```bash
dotnet test AuraCorePro.sln --verbosity minimal --nologo 2>&1 | grep -E "^Passed!|^Failed!"
```

Expected: Total 495 → 500 (+5 new).

- [ ] **Step 3.6: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Controls/StatRow.cs \
        tests/AuraCore.Tests.UI.Avalonia/Controls/StatRowTests.cs
git commit -m "$(cat <<'EOF'
feat(ui): add StatRow primitive (Phase 4.0 spec §4.3)

Trivial UniformGrid subclass with Rows=1 pre-set in ctor — children
flow into Children naturally, UniformGrid auto-divides columns by
child count. No XAML file needed (plan decision §3).

5 tests: Rows default, empty render, 3-card population, equal-width
division for 2 and 4 cards (within 2 px rounding tolerance).

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: Migrate DnsBenchmarkView

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/DnsBenchmarkView.axaml` (30 lines → ~20)
- Create: `tests/AuraCore.Tests.UI.Avalonia/Views/DnsBenchmarkViewTests.cs`
- Untouched: `DnsBenchmarkView.axaml.cs` (140 lines, all event wiring preserved)

Archetype: Action + Result. Simplest pilot — pattern validation on minimal module.

- [ ] **Step 4.1: Read current state + baseline**

Read existing files to confirm structure:

```bash
cat src/UI/AuraCore.UI.Avalonia/Views/Pages/DnsBenchmarkView.axaml
grep -nE "x:Name|Click=|Command=" src/UI/AuraCore.UI.Avalonia/Views/Pages/DnsBenchmarkView.axaml.cs | head -20
```

Noted x:Name values the code-behind depends on: `PageTitle`, `BenchBtn`, `SubText`, `DnsList`, `RecommendText`.

Baseline test suite:
```bash
dotnet test AuraCorePro.sln --verbosity minimal --nologo 2>&1 | grep "^Passed!"
```
Expected: 500 passing (after Task 3).

- [ ] **Step 4.2: Write failing smoke tests**

Create `tests/AuraCore.Tests.UI.Avalonia/Views/DnsBenchmarkViewTests.cs`:

```csharp
using AuraCore.UI.Avalonia.Views.Controls;
using AuraCore.UI.Avalonia.Views.Pages;
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

/// <summary>
/// Phase 4.0 pilot smoke tests for DnsBenchmarkView after migration to the
/// Module Page shell (spec §5). Guards against regression of the module
/// back to the hardcoded-Border pattern.
/// </summary>
public class DnsBenchmarkViewTests
{
    [AvaloniaFact]
    public void Ctor_DoesNotThrow()
    {
        var v = new DnsBenchmarkView();
        Assert.NotNull(v);
    }

    [AvaloniaFact]
    public void RenderInWindow_DoesNotCrash_OnLoaded()
    {
        var v = new DnsBenchmarkView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1000, 700);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        // Code-behind hooks into Loaded for localization; confirm it runs cleanly.
        Assert.True(v.IsMeasureValid);
    }

    [AvaloniaFact]
    public void Layout_UsesModuleHeader_WithExpectedTitle()
    {
        var v = new DnsBenchmarkView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1000, 700);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // New shell contains exactly one ModuleHeader control
        var header = v.GetVisualDescendants()
            .OfType<ModuleHeader>()
            .FirstOrDefault();
        Assert.NotNull(header);
        // Code-behind ApplyLocalization() sets PageTitle.Text; ModuleHeader's
        // Title is bound to the same x:Name so we read it directly.
        Assert.False(string.IsNullOrWhiteSpace(header!.Title));
    }

    [AvaloniaFact]
    public void CodeBehind_NamedElements_StillResolve()
    {
        // Preservation rule (spec §5.1): every x:Name the code-behind references
        // must still resolve after migration.
        var v = new DnsBenchmarkView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1000, 700);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.NotNull(v.FindControl<TextBlock>("PageTitle"));
        Assert.NotNull(v.FindControl<Button>("BenchBtn"));
        Assert.NotNull(v.FindControl<TextBlock>("SubText"));
        Assert.NotNull(v.FindControl<ItemsControl>("DnsList"));
        Assert.NotNull(v.FindControl<TextBlock>("RecommendText"));
    }
}
```

Note the required `using System.Linq;` + `using Avalonia.VisualTree;` additions if not auto-included.

Add to top of file:
```csharp
using System.Linq;
using global::Avalonia.VisualTree;
```

- [ ] **Step 4.3: Run smoke tests to verify fail**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~DnsBenchmarkViewTests" --verbosity minimal --nologo 2>&1 | tail -10
```

Expected: tests fail. `Ctor_DoesNotThrow` + `RenderInWindow_DoesNotCrash_OnLoaded` may pass (the current view works), but `Layout_UsesModuleHeader_WithExpectedTitle` MUST fail because there's no `ModuleHeader` in the current XAML. This is TDD red.

- [ ] **Step 4.4: Rewrite `DnsBenchmarkView.axaml`**

Replace the entire content of `src/UI/AuraCore.UI.Avalonia/Views/Pages/DnsBenchmarkView.axaml` with:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="using:AuraCore.UI.Avalonia.Views.Controls"
             x:Class="AuraCore.UI.Avalonia.Views.Pages.DnsBenchmarkView">
  <ScrollViewer>
    <StackPanel Spacing="16">

      <controls:ModuleHeader Title="DNS Benchmark"
                             Subtitle="Test DNS servers and find the fastest one for your connection">
        <controls:ModuleHeader.Actions>
          <!-- Hidden placeholder TextBlock anchors the PageTitle x:Name so code-behind
               localization calls PageTitle.Text = ... still work. Actual title text
               displayed by ModuleHeader above comes from the Title property; this
               TextBlock is visually collapsed. -->
          <TextBlock x:Name="PageTitle" IsVisible="False" />
        </controls:ModuleHeader.Actions>
      </controls:ModuleHeader>

      <controls:GlassCard>
        <StackPanel Spacing="12">
          <Grid ColumnDefinitions="*,Auto">
            <TextBlock Text="DNS Servers"
                       FontSize="{DynamicResource FontSizeBody}"
                       FontWeight="SemiBold"
                       Foreground="{DynamicResource TextPrimaryBrush}"
                       VerticalAlignment="Center" />
            <Button x:Name="BenchBtn"
                    Grid.Column="1"
                    Content="Run Benchmark"
                    Click="Bench_Click"
                    Classes="action-btn"
                    Padding="16,8" />
          </Grid>
          <TextBlock x:Name="SubText"
                     Text="Click Run Benchmark to test popular DNS servers"
                     FontSize="{DynamicResource FontSizeBody}"
                     Foreground="{DynamicResource TextMutedBrush}" />
          <ItemsControl x:Name="DnsList" />
          <TextBlock x:Name="RecommendText"
                     Text=""
                     FontSize="{DynamicResource FontSizeBody}"
                     FontWeight="SemiBold"
                     Foreground="{DynamicResource StatusSuccessBrush}" />
        </StackPanel>
      </controls:GlassCard>

    </StackPanel>
  </ScrollViewer>
</UserControl>
```

**Key points:**
- `PageTitle` x:Name preserved as an invisible placeholder inside `ModuleHeader.Actions` slot. Code-behind `ApplyLocalization()` sets `PageTitle.Text` which is invisible; ModuleHeader's own title comes from the `Title` property. (Minor duplication, acceptable — avoids rewriting code-behind.)
- `BenchBtn`, `SubText`, `DnsList`, `RecommendText` x:Names preserved exactly where they live now (inside a GlassCard instead of the old Border).
- Removed raw `BgCardBrush`/`BorderCardBrush` usage in favor of GlassCard primitive.
- `SuccessBrush` → `StatusSuccessBrush` (V2 key migration per plan decision 1).

- [ ] **Step 4.5: Build to verify XAML compiles**

```bash
dotnet build src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj --configuration Debug --verbosity minimal --nologo 2>&1 | grep -E "error|Build succeeded"
```

Expected: `Build succeeded`. Any AVLN errors mean XAML syntax is off — fix before continuing.

- [ ] **Step 4.6: Run smoke tests to verify pass**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~DnsBenchmarkViewTests" --verbosity minimal --nologo 2>&1 | tail -5
```

Expected: `Passed: 4, Failed: 0`.

- [ ] **Step 4.7: Run full test suite**

```bash
dotnet test AuraCorePro.sln --verbosity minimal --nologo 2>&1 | grep -E "^Passed!|^Failed!"
```

Expected: Total 500 → 504 (+4 new). Zero regressions.

- [ ] **Step 4.8: Manual parity sweep**

```bash
taskkill //F //IM AuraCore.Pro.exe 2>/dev/null
dotnet run --project src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj --configuration Debug
```

In the app:
1. Navigate to DNS Benchmark module
2. Click "Run Benchmark" — should populate `DnsList` with DNS server results
3. Verify "RecommendText" shows the fastest server after benchmark
4. Verify title + subtitle render via ModuleHeader (new visual)
5. Close app

Confirm: every behavior identical to pre-migration. If something breaks, revert the .axaml file and debug.

- [ ] **Step 4.9: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Pages/DnsBenchmarkView.axaml \
        tests/AuraCore.Tests.UI.Avalonia/Views/DnsBenchmarkViewTests.cs
git commit -m "$(cat <<'EOF'
refactor(ui): migrate DnsBenchmarkView to Phase 4.0 shell (spec §5)

Replace hardcoded Border+BgCardBrush card wrapper with controls:ModuleHeader
+ controls:GlassCard primitives. V2 token migration:
SuccessBrush -> StatusSuccessBrush (V1 bridge leak closed for this module).

Code-behind untouched; every x:Name reference still resolves. PageTitle
anchored as invisible placeholder inside ModuleHeader.Actions slot so
the existing LocalizationService.ApplyLocalization() call path works
without modification.

Before: 30 lines .axaml
After:  ~22 lines .axaml (-27%)

4 smoke tests: ctor, render, ModuleHeader presence, x:Name resolution.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: Migrate HostsEditorView

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/HostsEditorView.axaml` (197 lines → ~110)
- Create: `tests/AuraCore.Tests.UI.Avalonia/Views/HostsEditorViewTests.cs`
- Untouched: `HostsEditorView.axaml.cs`

Archetype: Editor. Cross-platform (/etc/hosts works on Linux/macOS too).

- [ ] **Step 5.1: Read current state**

```bash
cat src/UI/AuraCore.UI.Avalonia/Views/Pages/HostsEditorView.axaml
grep -nE "x:Name|Click=|Command=" src/UI/AuraCore.UI.Avalonia/Views/Pages/HostsEditorView.axaml.cs | head -30
```

Record every x:Name in the existing XAML. Typical candidates: `PageTitle`, `EditorBox`, `SaveBtn`, `ReloadBtn`, `StatusText`, potentially `BackupList`. **Every x:Name identified here must still resolve in the new XAML.**

- [ ] **Step 5.2: Write failing smoke tests**

Create `tests/AuraCore.Tests.UI.Avalonia/Views/HostsEditorViewTests.cs`:

```csharp
using System.Linq;
using AuraCore.UI.Avalonia.Views.Controls;
using AuraCore.UI.Avalonia.Views.Pages;
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using global::Avalonia.VisualTree;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

public class HostsEditorViewTests
{
    [AvaloniaFact]
    public void Ctor_DoesNotThrow()
    {
        var v = new HostsEditorView();
        Assert.NotNull(v);
    }

    [AvaloniaFact]
    public void RenderInWindow_DoesNotCrash()
    {
        var v = new HostsEditorView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(v.IsMeasureValid);
    }

    [AvaloniaFact]
    public void Layout_UsesModuleHeader()
    {
        var v = new HostsEditorView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var header = v.GetVisualDescendants().OfType<ModuleHeader>().FirstOrDefault();
        Assert.NotNull(header);
    }

    [AvaloniaFact]
    public void Layout_UsesGlassCard()
    {
        var v = new HostsEditorView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var card = v.GetVisualDescendants().OfType<GlassCard>().FirstOrDefault();
        Assert.NotNull(card);
    }
}
```

- [ ] **Step 5.3: Run smoke tests to verify fail**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~HostsEditorViewTests" --verbosity minimal --nologo 2>&1 | tail -5
```

Expected: `Layout_UsesModuleHeader` fails (current view has no ModuleHeader). This is TDD red.

- [ ] **Step 5.4: Rewrite `HostsEditorView.axaml`**

Target structure (adjust action names, x:Names, bindings to match what the `.axaml.cs` actually references — copy them verbatim from Step 5.1):

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="using:AuraCore.UI.Avalonia.Views.Controls"
             x:Class="AuraCore.UI.Avalonia.Views.Pages.HostsEditorView">
  <ScrollViewer>
    <StackPanel Spacing="16">

      <controls:ModuleHeader Title="Hosts Editor"
                             Subtitle="Edit the system hosts file directly">
        <controls:ModuleHeader.Actions>
          <StackPanel Orientation="Horizontal" Spacing="8">
            <!-- Copy the exact Button x:Names + Click handlers from the old XAML here.
                 Typical: ReloadBtn, SaveBtn, RestoreBtn. -->
            <Button x:Name="ReloadBtn"
                    Content="Reload"
                    Click="Reload_Click"
                    Classes="action-btn"
                    Padding="14,7" />
            <Button x:Name="SaveBtn"
                    Content="Save"
                    Click="Save_Click"
                    Background="{DynamicResource AccentPrimaryBrush}"
                    Foreground="#0A0A0F"
                    Padding="14,7"
                    CornerRadius="{DynamicResource RadiusMd}"
                    FontWeight="Bold" />
          </StackPanel>
          <!-- PageTitle preserved for localization code-behind -->
          <TextBlock x:Name="PageTitle" IsVisible="False" />
        </controls:ModuleHeader.Actions>
      </controls:ModuleHeader>

      <controls:GlassCard>
        <StackPanel Spacing="10">
          <TextBlock x:Name="StatusText"
                     Text=""
                     FontSize="{DynamicResource FontSizeBodySmall}"
                     Foreground="{DynamicResource TextMutedBrush}"
                     TextWrapping="Wrap" />
          <TextBox x:Name="EditorBox"
                   AcceptsReturn="True"
                   AcceptsTab="True"
                   FontFamily="Consolas,Menlo,monospace"
                   FontSize="{DynamicResource FontSizeBody}"
                   Height="400"
                   TextWrapping="NoWrap"
                   HorizontalScrollBarVisibility="Auto"
                   VerticalScrollBarVisibility="Auto" />
        </StackPanel>
      </controls:GlassCard>

      <!-- If original had a backup list or secondary card, preserve it here in a second GlassCard.
           Example skeleton (uncomment + populate if applicable): -->
      <!--
      <controls:GlassCard>
        <StackPanel Spacing="8">
          <TextBlock Text="Backups"
                     FontSize="{DynamicResource FontSizeBody}"
                     FontWeight="SemiBold"
                     Foreground="{DynamicResource TextPrimaryBrush}" />
          <ItemsControl x:Name="BackupList" />
        </StackPanel>
      </controls:GlassCard>
      -->

    </StackPanel>
  </ScrollViewer>
</UserControl>
```

**Action:** After reading the old XAML in Step 5.1, copy every x:Name + event handler name + binding path EXACTLY as they were. The skeleton above is a template — the implementer must reconcile it against the actual old XAML before saving.

- [ ] **Step 5.5: Build + verify smoke tests pass**

```bash
dotnet build src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj --configuration Debug --verbosity minimal --nologo 2>&1 | grep -E "error|Build succeeded"
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~HostsEditorViewTests" --verbosity minimal --nologo 2>&1 | tail -5
```

Expected: build succeeds; `Passed: 4, Failed: 0`.

- [ ] **Step 5.6: Full test suite**

```bash
dotnet test AuraCorePro.sln --verbosity minimal --nologo 2>&1 | grep -E "^Passed!|^Failed!"
```

Expected: Total 504 → 508 (+4 new). Zero regressions.

- [ ] **Step 5.7: Manual parity sweep**

```bash
taskkill //F //IM AuraCore.Pro.exe 2>/dev/null
dotnet run --project src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj --configuration Debug
```

In the app:
1. Navigate to Hosts Editor
2. Verify hosts file content loads into `EditorBox`
3. Make an edit, click Save — verify save succeeds (may need admin privileges)
4. Click Reload — content refreshes
5. Any warning/status lines in `StatusText` render correctly
6. Close app

- [ ] **Step 5.8: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Pages/HostsEditorView.axaml \
        tests/AuraCore.Tests.UI.Avalonia/Views/HostsEditorViewTests.cs
git commit -m "$(cat <<'EOF'
refactor(ui): migrate HostsEditorView to Phase 4.0 shell (spec §5)

Replace hardcoded layout with controls:ModuleHeader + controls:GlassCard.
Actions (Reload/Save) moved into ModuleHeader.Actions slot. Code-behind
unchanged; all x:Name references resolve via preserved names.

Editor archetype validated — TextBox content area wraps naturally inside
a GlassCard. Cross-platform note: /etc/hosts, /private/etc/hosts, and
C:\Windows\System32\drivers\etc\hosts all render the same UI.

Before: 197 lines .axaml
After:  ~110 lines .axaml (-44%)

4 smoke tests: ctor, render, ModuleHeader + GlassCard presence.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: Migrate AutorunManagerView

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/AutorunManagerView.axaml` (132 lines → ~75)
- Create: `tests/AuraCore.Tests.UI.Avalonia/Views/AutorunManagerViewTests.cs`
- Untouched: `AutorunManagerView.axaml.cs`

Archetype: List + Toggle. First use of StatRow if module has stats.

- [ ] **Step 6.1: Read current state**

```bash
cat src/UI/AuraCore.UI.Avalonia/Views/Pages/AutorunManagerView.axaml
grep -nE "x:Name|Click=" src/UI/AuraCore.UI.Avalonia/Views/Pages/AutorunManagerView.axaml.cs | head -30
```

Record x:Names. Typical: `PageTitle`, `ScanBtn`, `ItemsList`, possibly `TotalCount`/`EnabledCount`/`DisabledCount` if the module has stats.

- [ ] **Step 6.2: Write failing smoke tests**

Create `tests/AuraCore.Tests.UI.Avalonia/Views/AutorunManagerViewTests.cs`:

```csharp
using System.Linq;
using AuraCore.UI.Avalonia.Views.Controls;
using AuraCore.UI.Avalonia.Views.Pages;
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using global::Avalonia.VisualTree;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

public class AutorunManagerViewTests
{
    [AvaloniaFact]
    public void Ctor_DoesNotThrow()
    {
        var v = new AutorunManagerView();
        Assert.NotNull(v);
    }

    [AvaloniaFact]
    public void RenderInWindow_DoesNotCrash()
    {
        var v = new AutorunManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(v.IsMeasureValid);
    }

    [AvaloniaFact]
    public void Layout_UsesModuleHeader()
    {
        var v = new AutorunManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var header = v.GetVisualDescendants().OfType<ModuleHeader>().FirstOrDefault();
        Assert.NotNull(header);
    }

    [AvaloniaFact]
    public void Layout_UsesGlassCard_ForContent()
    {
        var v = new AutorunManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var cards = v.GetVisualDescendants().OfType<GlassCard>().ToList();
        Assert.NotEmpty(cards);
    }
}
```

- [ ] **Step 6.3: Run smoke tests to verify fail**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~AutorunManagerViewTests" --verbosity minimal --nologo 2>&1 | tail -5
```

Expected: `Layout_UsesModuleHeader` fails.

- [ ] **Step 6.4: Rewrite `AutorunManagerView.axaml`**

Use the same shape as Task 5 (ModuleHeader + GlassCard). If the old view has stats (counters like "X entries, Y enabled"), wrap them in a `<controls:StatRow>`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="using:AuraCore.UI.Avalonia.Views.Controls"
             x:Class="AuraCore.UI.Avalonia.Views.Pages.AutorunManagerView">
  <ScrollViewer>
    <StackPanel Spacing="16">

      <controls:ModuleHeader Title="Autorun Manager"
                             Subtitle="Manage Windows startup entries">
        <controls:ModuleHeader.Actions>
          <Button x:Name="ScanBtn"
                  Content="Scan Autorun"
                  Click="Scan_Click"
                  Classes="action-btn"
                  Padding="14,7" />
          <TextBlock x:Name="PageTitle" IsVisible="False" />
        </controls:ModuleHeader.Actions>
      </controls:ModuleHeader>

      <!-- StatRow only if the original view had stat counters.
           If it didn't, skip this block and keep GlassCard alone. -->
      <controls:StatRow>
        <controls:StatCard Label="TOTAL"
                           Value="{Binding TotalCount, FallbackValue=--}"
                           ValueBrush="{DynamicResource TextPrimaryBrush}" />
        <controls:StatCard Label="ENABLED"
                           Value="{Binding EnabledCount, FallbackValue=--}"
                           ValueBrush="{DynamicResource StatusSuccessBrush}" />
        <controls:StatCard Label="DISABLED"
                           Value="{Binding DisabledCount, FallbackValue=--}"
                           ValueBrush="{DynamicResource TextMutedBrush}" />
      </controls:StatRow>

      <controls:GlassCard>
        <StackPanel Spacing="10">
          <TextBlock x:Name="SubText"
                     Text="Click Scan to load startup entries"
                     FontSize="{DynamicResource FontSizeBody}"
                     Foreground="{DynamicResource TextMutedBrush}" />
          <ItemsControl x:Name="ItemsList" />
        </StackPanel>
      </controls:GlassCard>

    </StackPanel>
  </ScrollViewer>
</UserControl>
```

**Reconciliation rules:** If the old view didn't have counters bound via the ViewModel, either:
- Delete the `StatRow` block entirely, OR
- Keep it as placeholder with `Value="--"` hardcoded and note a TODO for Phase 5 MVVM cleanup (preferred: delete rather than add new bindings the code-behind doesn't populate).

- [ ] **Step 6.5: Build + tests**

```bash
dotnet build src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj --configuration Debug --verbosity minimal --nologo 2>&1 | grep -E "error|Build succeeded"
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~AutorunManagerViewTests" --verbosity minimal --nologo 2>&1 | tail -5
dotnet test AuraCorePro.sln --verbosity minimal --nologo 2>&1 | grep -E "^Passed!|^Failed!"
```

Expected: build succeeds; `Passed: 4, Failed: 0`; total 508 → 512.

- [ ] **Step 6.6: Manual parity sweep**

Launch app, navigate to Autorun Manager, click Scan, verify entries load, verify each entry's enable/disable toggle works.

- [ ] **Step 6.7: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Pages/AutorunManagerView.axaml \
        tests/AuraCore.Tests.UI.Avalonia/Views/AutorunManagerViewTests.cs
git commit -m "refactor(ui): migrate AutorunManagerView to Phase 4.0 shell (spec §5)

Replace hardcoded layout with controls:ModuleHeader + controls:GlassCard.
[Optionally] introduces first use of controls:StatRow for autorun entry
counters.

Before: 132 lines .axaml
After:  ~75 lines .axaml (-43%)

4 smoke tests: ctor, render, ModuleHeader presence, GlassCard presence.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

## Task 7: Migrate FirewallRulesView

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/FirewallRulesView.axaml` (90 lines → ~50)
- Create: `tests/AuraCore.Tests.UI.Avalonia/Views/FirewallRulesViewTests.cs`
- Untouched: `FirewallRulesView.axaml.cs`

Archetype: List + Toggle + 3-stat row. Full use of StatRow with 3 StatCards.

- [ ] **Step 7.1: Read current state**

```bash
cat src/UI/AuraCore.UI.Avalonia/Views/Pages/FirewallRulesView.axaml
grep -nE "x:Name|Click=" src/UI/AuraCore.UI.Avalonia/Views/Pages/FirewallRulesView.axaml.cs | head -30
```

Record x:Names: `PageTitle`, `TotalRules`, `EnabledRules`, `BlockedRules`, `SearchBox`, `SubText`, `RulesList`, `ScanBtn`.

- [ ] **Step 7.2: Write failing smoke tests**

Create `tests/AuraCore.Tests.UI.Avalonia/Views/FirewallRulesViewTests.cs`:

```csharp
using System.Linq;
using AuraCore.UI.Avalonia.Views.Controls;
using AuraCore.UI.Avalonia.Views.Pages;
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using global::Avalonia.VisualTree;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

public class FirewallRulesViewTests
{
    [AvaloniaFact]
    public void Ctor_DoesNotThrow()
    {
        var v = new FirewallRulesView();
        Assert.NotNull(v);
    }

    [AvaloniaFact]
    public void RenderInWindow_DoesNotCrash()
    {
        var v = new FirewallRulesView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(v.IsMeasureValid);
    }

    [AvaloniaFact]
    public void Layout_UsesModuleHeader_AndStatRow_WithThreeCards()
    {
        var v = new FirewallRulesView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var header = v.GetVisualDescendants().OfType<ModuleHeader>().FirstOrDefault();
        Assert.NotNull(header);

        var row = v.GetVisualDescendants().OfType<StatRow>().FirstOrDefault();
        Assert.NotNull(row);
        var cards = v.GetVisualDescendants().OfType<StatCard>().ToList();
        Assert.Equal(3, cards.Count); // Total / Enabled / Blocked
    }

    [AvaloniaFact]
    public void CodeBehind_NamedElements_StillResolve()
    {
        var v = new FirewallRulesView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.NotNull(v.FindControl<TextBlock>("TotalRules"));
        Assert.NotNull(v.FindControl<TextBlock>("EnabledRules"));
        Assert.NotNull(v.FindControl<TextBlock>("BlockedRules"));
        Assert.NotNull(v.FindControl<TextBox>("SearchBox"));
        Assert.NotNull(v.FindControl<ItemsControl>("RulesList"));
    }
}
```

- [ ] **Step 7.3: Run smoke tests to verify fail**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~FirewallRulesViewTests" --verbosity minimal --nologo 2>&1 | tail -5
```

Expected: `Layout_UsesModuleHeader_AndStatRow_WithThreeCards` fails.

- [ ] **Step 7.4: Rewrite `FirewallRulesView.axaml`**

The old view uses counters bound via `x:Name` (code-behind sets `TotalRules.Text = ...`). To keep code-behind working, the `StatCard.Value` DP can't use binding — the x:Name must target an inner TextBlock.

**Tactic:** Keep the x:Name on a hidden TextBlock inside each StatCard slot. Code-behind writes to the TextBlock; StatCard renders its static `Value`. Or, cleaner: wrap each stat in a `StackPanel` containing a `StatCard` + a hidden `TextBlock x:Name="TotalRules"`. Code-behind path unchanged.

**Cleanest tactic for this pilot:** Use StatCard's `Value` DP but bind through a trivial wrapper — expose the named TextBlock's `Text` property into `StatCard.Value` via a two-way binding. Easier alternative: **keep the named TextBlock as the single source of truth, pass `Value="{Binding #TotalRules.Text, FallbackValue=--}"`.**

Final XAML:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="using:AuraCore.UI.Avalonia.Views.Controls"
             x:Class="AuraCore.UI.Avalonia.Views.Pages.FirewallRulesView">
  <ScrollViewer>
    <StackPanel Spacing="16">

      <!-- Hidden named TextBlocks that code-behind writes to. Not displayed,
           but their Text property is the single source of truth for the
           StatCard.Value bindings below. -->
      <StackPanel IsVisible="False" Height="0">
        <TextBlock x:Name="TotalRules" Text="--" />
        <TextBlock x:Name="EnabledRules" Text="--" />
        <TextBlock x:Name="BlockedRules" Text="--" />
      </StackPanel>

      <controls:ModuleHeader Title="Firewall Rules"
                             Subtitle="Manage Windows Firewall inbound and outbound rules">
        <controls:ModuleHeader.Actions>
          <Button x:Name="ScanBtn"
                  Content="Scan Rules"
                  Click="Scan_Click"
                  Classes="action-btn"
                  Padding="14,7" />
          <TextBlock x:Name="PageTitle" IsVisible="False" />
        </controls:ModuleHeader.Actions>
      </controls:ModuleHeader>

      <controls:StatRow>
        <controls:StatCard Label="TOTAL"
                           Value="{Binding #TotalRules.Text}"
                           ValueBrush="{DynamicResource TextPrimaryBrush}" />
        <controls:StatCard Label="ENABLED"
                           Value="{Binding #EnabledRules.Text}"
                           ValueBrush="{DynamicResource StatusSuccessBrush}" />
        <controls:StatCard Label="BLOCKED"
                           Value="{Binding #BlockedRules.Text}"
                           ValueBrush="{DynamicResource StatusErrorBrush}" />
      </controls:StatRow>

      <controls:GlassCard>
        <StackPanel Spacing="12">
          <Grid ColumnDefinitions="*,Auto">
            <TextBox x:Name="SearchBox"
                     Watermark="Search rules..."
                     Width="300"
                     HorizontalAlignment="Left" />
          </Grid>
          <TextBlock x:Name="SubText"
                     Text="Click Scan to load firewall rules (requires admin)"
                     FontSize="{DynamicResource FontSizeBody}"
                     Foreground="{DynamicResource TextMutedBrush}" />
          <ItemsControl x:Name="RulesList" />
        </StackPanel>
      </controls:GlassCard>

    </StackPanel>
  </ScrollViewer>
</UserControl>
```

- [ ] **Step 7.5: Build + tests**

```bash
dotnet build src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj --configuration Debug --verbosity minimal --nologo 2>&1 | grep -E "error|Build succeeded"
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~FirewallRulesViewTests" --verbosity minimal --nologo 2>&1 | tail -5
dotnet test AuraCorePro.sln --verbosity minimal --nologo 2>&1 | grep -E "^Passed!|^Failed!"
```

Expected: build succeeds; `Passed: 4, Failed: 0`; total 512 → 516.

- [ ] **Step 7.6: Manual parity sweep**

Launch app → Firewall Rules → click "Scan Rules" (needs admin). Verify:
- Stats populate in the 3 StatCards (Total white, Enabled green, Blocked red)
- Rules list loads
- Search box filters

- [ ] **Step 7.7: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Pages/FirewallRulesView.axaml \
        tests/AuraCore.Tests.UI.Avalonia/Views/FirewallRulesViewTests.cs
git commit -m "refactor(ui): migrate FirewallRulesView to Phase 4.0 shell (spec §5)

First full use of controls:StatRow with 3 StatCards (Total / Enabled /
Blocked). Stats stay behind hidden x:Name'd TextBlocks acting as single
source of truth for the StatCard.Value bindings — code-behind write
path unchanged (TotalRules.Text = ...).

V2 token migration: SuccessBrush -> StatusSuccessBrush,
ErrorBrush -> StatusErrorBrush (closed 2 more V1 bridge leaks).

Before: 90 lines .axaml
After:  ~55 lines .axaml (-39%)

4 smoke tests: ctor, render, ModuleHeader+StatRow presence (3 StatCards
asserted), x:Name resolution.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

## Task 8: Migrate RamOptimizerView

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/RamOptimizerView.axaml` (159 lines → ~95)
- Create: `tests/AuraCore.Tests.UI.Avalonia/Views/RamOptimizerViewTests.cs`
- Untouched: `RamOptimizerView.axaml.cs`

Archetype: Action + custom visualization. **Stress test** — 3 header actions (Scan/Optimize/Boost) + gradient progress bar + history graph. If pattern breaks here, primitives need refinement.

- [ ] **Step 8.1: Read current state fully**

```bash
cat src/UI/AuraCore.UI.Avalonia/Views/Pages/RamOptimizerView.axaml
```

This module has:
- 3 action buttons: `ScanBtn`, `OptBtn`, `BoostBtn`
- Auto-optimize toggle: `AutoOptToggle`
- RAM usage gradient bar: `RamBar` inside a `Border`
- RAM percent + reclaimable text: `UsedRam`, `TotalRam`, `RamPct`, `Reclaimable`
- Historical RAM graph area (lines, canvas, etc.)

- [ ] **Step 8.2: Write failing smoke tests**

Create `tests/AuraCore.Tests.UI.Avalonia/Views/RamOptimizerViewTests.cs`:

```csharp
using System.Linq;
using AuraCore.UI.Avalonia.Views.Controls;
using AuraCore.UI.Avalonia.Views.Pages;
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using global::Avalonia.VisualTree;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

public class RamOptimizerViewTests
{
    [AvaloniaFact]
    public void Ctor_DoesNotThrow()
    {
        var v = new RamOptimizerView();
        Assert.NotNull(v);
    }

    [AvaloniaFact]
    public void RenderInWindow_DoesNotCrash()
    {
        var v = new RamOptimizerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(v.IsMeasureValid);
    }

    [AvaloniaFact]
    public void Layout_UsesModuleHeader_WithThreeActions()
    {
        var v = new RamOptimizerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var header = v.GetVisualDescendants().OfType<ModuleHeader>().FirstOrDefault();
        Assert.NotNull(header);
        // Scan + Optimize + Boost + AutoOptToggle inside Actions panel
        var buttonsInHeader = header!.GetVisualDescendants().OfType<Button>().Count();
        Assert.True(buttonsInHeader >= 3, $"Expected ≥3 buttons in ModuleHeader.Actions, found {buttonsInHeader}");
    }

    [AvaloniaFact]
    public void CodeBehind_NamedElements_StillResolve()
    {
        var v = new RamOptimizerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // Critical x:Names the code-behind depends on
        Assert.NotNull(v.FindControl<Button>("ScanBtn"));
        Assert.NotNull(v.FindControl<Button>("OptBtn"));
        Assert.NotNull(v.FindControl<Button>("BoostBtn"));
        Assert.NotNull(v.FindControl<CheckBox>("AutoOptToggle"));
        Assert.NotNull(v.FindControl<TextBlock>("UsedRam"));
        Assert.NotNull(v.FindControl<TextBlock>("RamPct"));
        Assert.NotNull(v.FindControl<Border>("RamBar"));
    }
}
```

- [ ] **Step 8.3: Run smoke tests to verify fail**

Expected: `Layout_UsesModuleHeader_WithThreeActions` fails.

- [ ] **Step 8.4: Rewrite `RamOptimizerView.axaml`**

This is the stress test. Structure: ModuleHeader with 3 buttons + toggle → GlassCard for RAM bar + percentages → GlassCard for history graph:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="using:AuraCore.UI.Avalonia.Views.Controls"
             x:Class="AuraCore.UI.Avalonia.Views.Pages.RamOptimizerView">
  <ScrollViewer>
    <StackPanel Spacing="16">

      <controls:ModuleHeader Title="RAM Optimizer"
                             Subtitle="Free up memory by trimming process working sets">
        <controls:ModuleHeader.Actions>
          <StackPanel Orientation="Horizontal" Spacing="8" VerticalAlignment="Center">
            <!-- Auto-Optimize toggle cluster -->
            <StackPanel Orientation="Horizontal" Spacing="6" VerticalAlignment="Center" Margin="0,0,8,0">
              <TextBlock Text="Auto"
                         FontSize="{DynamicResource FontSizeLabel}"
                         Foreground="{DynamicResource TextMutedBrush}"
                         VerticalAlignment="Center" />
              <CheckBox x:Name="AutoOptToggle"
                        IsChecked="False"
                        Click="AutoOpt_Toggle"
                        VerticalAlignment="Center"
                        ToolTip.Tip="Auto-optimize when RAM exceeds 85%" />
            </StackPanel>
            <!-- 3 action buttons -->
            <Button x:Name="ScanBtn"
                    Click="Scan_Click"
                    Classes="action-btn"
                    VerticalAlignment="Center"
                    Padding="14,8">
              <StackPanel Orientation="Horizontal" Spacing="6">
                <TextBlock Text="&#x1F50D;" FontSize="12" />
                <TextBlock x:Name="ScanLabel" Text="Scan" FontSize="11" FontWeight="SemiBold" />
              </StackPanel>
            </Button>
            <Button x:Name="OptBtn"
                    Click="Optimize_Click"
                    Background="{DynamicResource AccentPrimaryBrush}"
                    Foreground="#0A0A0F"
                    Padding="16,8"
                    CornerRadius="{DynamicResource RadiusMd}"
                    FontWeight="Bold"
                    FontSize="12"
                    IsEnabled="False">
              <StackPanel Orientation="Horizontal" Spacing="6">
                <TextBlock Text="&#x26A1;" FontSize="12" />
                <TextBlock x:Name="OptLabel" Text="Optimize" FontSize="12" FontWeight="Bold" />
              </StackPanel>
            </Button>
            <Button x:Name="BoostBtn"
                    Click="Boost_Click"
                    Background="{DynamicResource StatusErrorBrush}"
                    Foreground="White"
                    Padding="18,9"
                    CornerRadius="{DynamicResource RadiusMd}"
                    FontWeight="Bold"
                    FontSize="13"
                    ToolTip.Tip="Aggressive: EmptyWorkingSet on ALL processes">
              <StackPanel Orientation="Horizontal" Spacing="6">
                <TextBlock Text="&#x1F680;" FontSize="13" />
                <TextBlock x:Name="BoostLabel" Text="Boost" FontSize="13" FontWeight="Bold" />
              </StackPanel>
            </Button>
          </StackPanel>
          <TextBlock x:Name="PageTitle" IsVisible="False" />
        </controls:ModuleHeader.Actions>
      </controls:ModuleHeader>

      <!-- RAM usage bar + percentages -->
      <controls:GlassCard>
        <StackPanel Spacing="10">
          <Grid ColumnDefinitions="*,Auto">
            <StackPanel Orientation="Horizontal" Spacing="8">
              <TextBlock x:Name="UsedRam"
                         Text="--"
                         FontSize="{DynamicResource FontSizeDisplay}"
                         FontWeight="Bold"
                         Foreground="{DynamicResource AccentBlueBrush}" />
              <TextBlock x:Name="TotalRam"
                         Text="/ -- GB"
                         FontSize="{DynamicResource FontSizeSubheading}"
                         Foreground="{DynamicResource TextMutedBrush}"
                         VerticalAlignment="Bottom"
                         Margin="0,0,0,6" />
            </StackPanel>
            <StackPanel Grid.Column="1" HorizontalAlignment="Right" VerticalAlignment="Center">
              <TextBlock x:Name="RamPct"
                         Text="--%"
                         FontSize="{DynamicResource FontSizeHeading}"
                         FontWeight="Bold"
                         Foreground="{DynamicResource TextPrimaryBrush}" />
              <TextBlock x:Name="Reclaimable"
                         Text=""
                         FontSize="{DynamicResource FontSizeBodySmall}"
                         Foreground="{DynamicResource AccentPrimaryBrush}" />
            </StackPanel>
          </Grid>
          <!-- Gradient bar (stays inline for now — can be extracted to
               GradientProgressBar primitive in Phase 4.1 if needed again) -->
          <Border Height="6"
                  CornerRadius="3"
                  Background="{DynamicResource BgCardBrush}">
            <Border x:Name="RamBar"
                    Height="6"
                    CornerRadius="3"
                    HorizontalAlignment="Left"
                    Width="0">
              <Border.Background>
                <LinearGradientBrush StartPoint="0%,50%" EndPoint="100%,50%">
                  <GradientStop Offset="0" Color="#3B82F6" />
                  <GradientStop Offset="1" Color="#67E8F9" />
                </LinearGradientBrush>
              </Border.Background>
            </Border>
          </Border>
        </StackPanel>
      </controls:GlassCard>

      <!-- Historical RAM graph (preserve the Canvas / graph element x:Names
           verbatim from the old view) -->
      <controls:GlassCard>
        <!-- Paste the historical graph block from the old XAML unchanged,
             adjusting only outer Border wrapping (removed; GlassCard replaces it). -->
      </controls:GlassCard>

    </StackPanel>
  </ScrollViewer>
</UserControl>
```

**Important:** The historical graph block (Canvas + polylines or similar) at the bottom of the old XAML is preserved **as-is** except for dropping the outer `<Border>` wrapper (GlassCard replaces it). Copy its contents verbatim between the empty `<controls:GlassCard>` tags in the second card slot.

- [ ] **Step 8.5: Build + tests**

```bash
dotnet build src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj --configuration Debug --verbosity minimal --nologo 2>&1 | grep -E "error|Build succeeded"
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~RamOptimizerViewTests" --verbosity minimal --nologo 2>&1 | tail -5
dotnet test AuraCorePro.sln --verbosity minimal --nologo 2>&1 | grep -E "^Passed!|^Failed!"
```

Expected: build succeeds; `Passed: 4, Failed: 0`; total 516 → 520.

- [ ] **Step 8.6: Manual parity sweep (extra thorough for stress test)**

1. Launch app → RAM Optimizer
2. Verify UsedRam / TotalRam / RamPct show current values
3. Verify RamBar renders the gradient correctly (blue→cyan)
4. Click "Scan" — verify OptBtn becomes enabled
5. Click "Optimize" — verify RAM freed + Reclaimable text updates
6. Click "Boost" — aggressive mode, verify works
7. Toggle Auto-Optimize on/off — verify toggle persists
8. Historical graph renders without crash

**If any step fails:** revert to stop and discover whether a gradient-bar primitive is actually needed. Primitive extraction is deferred decision per spec §7 note.

- [ ] **Step 8.7: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Pages/RamOptimizerView.axaml \
        tests/AuraCore.Tests.UI.Avalonia/Views/RamOptimizerViewTests.cs
git commit -m "refactor(ui): migrate RamOptimizerView to Phase 4.0 shell (spec §5)

Stress test module — 3 actions (Scan/Optimize/Boost) + Auto toggle in
ModuleHeader.Actions cluster. RAM usage bar + percentages in first
GlassCard. Historical graph in second GlassCard (gradient bar kept
inline; no dedicated primitive needed — pattern holds).

V2 token migration: ErrorBrush -> StatusErrorBrush (BoostBtn).

Before: 159 lines .axaml
After:  ~100 lines .axaml (-37%)

4 smoke tests: ctor, render, ModuleHeader with ≥3 buttons, x:Name
resolution (all 7 critical names).

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

## Task 9: Validation + manual sweep consolidation

No new files. This task confirms the whole pilot lands cleanly.

- [ ] **Step 9.1: Run full test suite**

```bash
dotnet test AuraCorePro.sln --verbosity minimal --nologo 2>&1 | grep -E "^Passed!|^Failed!" | head -15
```

Expected: all 8 test assemblies pass. Total ~520 (481 baseline + 19 primitive tests + 20 module smoke tests).

- [ ] **Step 9.2: Verify `.axaml.cs` preservation**

```bash
cd C:/Users/Admin/Desktop/AuraCorePro/AuraCorePro
git diff 7045d5a -- src/UI/AuraCore.UI.Avalonia/Views/Pages/DnsBenchmarkView.axaml.cs \
                    src/UI/AuraCore.UI.Avalonia/Views/Pages/HostsEditorView.axaml.cs \
                    src/UI/AuraCore.UI.Avalonia/Views/Pages/AutorunManagerView.axaml.cs \
                    src/UI/AuraCore.UI.Avalonia/Views/Pages/FirewallRulesView.axaml.cs \
                    src/UI/AuraCore.UI.Avalonia/Views/Pages/RamOptimizerView.axaml.cs
```

Expected: zero diff, OR only additive `using` imports. If any method body changed, revisit that migration.

- [ ] **Step 9.3: Verify `.axaml` size reduction (spec §9 success criterion)**

```bash
for m in DnsBenchmark Hosts AutorunManager FirewallRules RamOptimizer; do
  old=$(git show 7045d5a:src/UI/AuraCore.UI.Avalonia/Views/Pages/${m}View.axaml 2>/dev/null | wc -l)
  new=$(wc -l < src/UI/AuraCore.UI.Avalonia/Views/Pages/${m}View.axaml)
  delta=$(( (old - new) * 100 / old ))
  echo "${m}View.axaml: ${old} -> ${new} (-${delta}%)"
done
```

Expected: every module at ≥ 40% reduction. If any is below 40%, document why in the milestone commit (some modules may legitimately stay verbose if their logic justifies it).

- [ ] **Step 9.4: Manual end-to-end sweep**

```bash
taskkill //F //IM AuraCore.Pro.exe 2>/dev/null
dotnet run --project src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj --configuration Debug
```

Walk all 5 modules:
1. DNS Benchmark → Run Benchmark → verify results
2. Hosts Editor → verify load + edit + save
3. Autorun Manager → Scan → verify entries + toggles
4. Firewall Rules → Scan → verify 3 StatCards populate + list loads
5. RAM Optimizer → Scan → Optimize → verify RAM bar updates

Each must work identically to pre-migration. Check any other modules (Dashboard, AI Features, etc.) still render — no regressions.

- [ ] **Step 9.5: No commit** — this is a verification task only.

---

## Task 10: Phase 4.0 milestone commit

Close the phase with an empty ceremonial commit per spec §12 / Phase 2-3 precedent.

- [ ] **Step 10.1: Ensure clean state**

```bash
git status --short | grep -v -E "bin/|obj/|\.cache$" | head -5
```

Expected: no modified tracked files. Build artifacts OK per project convention.

- [ ] **Step 10.2: Create milestone commit**

```bash
git commit --allow-empty -m "$(cat <<'EOF'
milestone: Phase 4.0 Module Page Pattern + Pilot Batch COMPLETE

Delivers per docs/superpowers/specs/2026-04-16-phase4-module-pattern-design.md.

Primitives (new):
- ModuleHeader (spec §4.1): title + subtitle + right-aligned Actions slot
- StatCard (spec §4.2): uppercase label + bold value + bindable ValueBrush
- StatRow (spec §4.3): UniformGrid subclass, Rows=1, layout-only

Pilot modules migrated to the new shell (spec §5):
- DnsBenchmarkView: 30 → ~22 lines (-27%)
- HostsEditorView: 197 → ~110 lines (-44%)
- AutorunManagerView: 132 → ~75 lines (-43%)
- FirewallRulesView: 90 → ~55 lines (-39%)
- RamOptimizerView: 159 → ~100 lines (-37%)

V2 token migration (plan decision §1):
- SuccessBrush -> StatusSuccessBrush
- WarningBrush -> StatusWarningBrush
- ErrorBrush -> StatusErrorBrush
- BorderCardBrush -> BorderSubtleBrush
Closed N V1 bridge leaks across 5 modules.

Code-behind preservation (spec §5.1): every .axaml.cs unchanged except
optional `using` additions. All x:Name references still resolve
(verified via smoke tests + git diff).

Tests: 481 → ~520 green (zero regressions).
- 19 primitive unit tests (ModuleHeader 6, StatCard 8, StatRow 5)
- 20 module smoke tests (Ctor / Render / ModuleHeader / x:Name resolution)

Methodology (spec §7) proven on all 5 pilot archetypes:
- Action+Result (DnsBenchmark, RamOptimizer)
- Editor (HostsEditor)
- List+Toggle (AutorunManager)
- List+Toggle+Stats (FirewallRules)

Ready for Phase 4.1 (migrate 8 existing Linux/macOS modules) using the
same methodology. Primitive set unchanged unless a Phase 4.1 module
surfaces a pattern gap.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 10.3: Verify branch state**

```bash
git log --oneline 7045d5a..HEAD | head -15
git rev-parse HEAD
```

Expected: 10 commits on `phase-4-module-pattern` branch ahead of Phase 3 milestone.

---

## Implementation Complete

At this point Phase 4.0 is shipped:

- Branch `phase-4-module-pattern` contains all changes as atomic commits (~10).
- Milestone commit marks the end.
- Spec §9 success criteria all satisfied.
- Memory + handoff update follows the Phase 2/3 pattern:
  - Write / update memory file: `project_ui_rebuild_phase_4_0_complete.md`
  - Update index `MEMORY.md`
  - Optional: morning-handoff doc if session budget used up
- Next: Phase 4.1 spec (migrate 8 existing Linux/macOS modules — same pattern, add cross-platform visual verification).
