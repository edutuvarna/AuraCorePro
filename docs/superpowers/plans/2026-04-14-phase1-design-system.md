# Phase 1: Design System Foundation — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the complete foundation for the AuraCorePro UI rebuild — all design tokens (colors, typography, spacing, radius, glow) in a new theme file, a Lucide-style icon system, and 12 reusable primitive components that all later phases will compose.

**Architecture:** A new `AuraCoreThemeV2.axaml` ResourceDictionary holds every token from the Vision Document §5. An `Icons.axaml` ResourceDictionary holds ~30 Lucide-style icon `StreamGeometry` resources (no external icon package — keeps bundle lean). 12 reusable controls live under `Views/Controls/` as Avalonia `UserControl` with `StyledProperty<T>` DPs for binding. A new xUnit test project (`AuraCore.Tests.UI.Avalonia`) uses `Avalonia.Headless.XUnit` to verify each control instantiates, exposes its public API, and reacts to property changes. A visual `ComponentGalleryWindow` lets humans eyeball every control in one place during development.

**Tech Stack:** Avalonia 11.2.7, xUnit 2.9.2, Avalonia.Headless.XUnit 11.2.7, Inter font (already bundled). No new runtime packages beyond `Avalonia.Headless.XUnit` for tests.

---

## Context & References

- **Vision Document:** `docs/superpowers/specs/2026-04-14-ui-rebuild-vision-document.md` — authoritative source for all token values and component specs. Read this first.

> **Codebase note 1 (discovered during Task 2):** The codebase has an `AuraCore.Application` namespace (from `src/Core/AuraCore.Application/`). From inside `namespace AuraCore.Tests.UI.Avalonia`, the unqualified identifier `Application` resolves to that namespace, NOT to `Avalonia.Application`. Existing production code (`src/UI/AuraCore.UI.Avalonia/App.axaml.cs`, `Program.cs`) handles this with `global::Avalonia.Application` or `using global::Avalonia;`. Apply the same pattern whenever new test code references `Avalonia.Application` unqualified.

> **Codebase note 2 (discovered during Task 3):** The UI project's csproj has `<AssemblyName>AuraCore.Pro</AssemblyName>`, so the assembly name differs from the project name. All `avares://` URIs must use `AuraCore.Pro` as the host, NOT `AuraCore.UI.Avalonia`. Example: `avares://AuraCore.Pro/Themes/AuraCoreThemeV2.axaml`. Existing `App.axaml` uses this form. Apply to all subsequent tasks that load XAML via `avares://` (Tasks 3, 4, 5, 19).
- **Reference Dashboard mockup:** `.superpowers/brainstorm/1483-1776150339/content/full-dashboard-mockup-v4.html` — visual source of truth.
- **Old theme:** `src/UI/AuraCore.UI.Avalonia/Themes/AuraCoreTheme.axaml` — keep unchanged this phase. V2 will live alongside. Phase 2 will switch `App.axaml` to V2.
- **Existing control pattern:** `src/UI/AuraCore.UI.Avalonia/Views/Controls/OrbitalLogo.axaml/.cs` — follow this pattern.
- **Project root:** `C:/Users/Admin/Desktop/AuraCorePro/AuraCorePro/`. All paths below are relative to this.

## File Structure

### Created

```
src/UI/AuraCore.UI.Avalonia/
├── Themes/
│   ├── AuraCoreThemeV2.axaml            NEW — full token set (colors, typography, spacing, radius, glow)
│   └── Icons.axaml                       NEW — ~30 Lucide StreamGeometry resources
└── Views/Controls/
    ├── Gauge.axaml + .cs                 NEW — circular conic-gradient gauge
    ├── GlassCard.axaml + .cs             NEW — backdrop-translucent card
    ├── HeroCTA.axaml + .cs               NEW — hero call-to-action
    ├── InsightCard.axaml + .cs           NEW — vertical list of insight rows
    ├── QuickActionTile.axaml + .cs       NEW — small colored action tile
    ├── SidebarNavItem.axaml + .cs        NEW — nav item (icon + label + chip)
    ├── SidebarSectionDivider.axaml + .cs NEW — "── LABEL ──" divider
    ├── StatusChip.axaml + .cs            NEW — compact dot+label chip
    ├── AuraToggle.axaml + .cs            NEW — styled on/off toggle
    ├── AccentBadge.axaml + .cs           NEW — small uppercase badge
    ├── UserChip.axaml + .cs              NEW — avatar + email + role
    └── AppLogoBadge.axaml + .cs          NEW — logo + name + tagline

src/UI/AuraCore.UI.Avalonia/Views/Dev/
└── ComponentGalleryWindow.axaml + .cs    NEW — visual review window (dev-only, not shipped)

tests/AuraCore.Tests.UI.Avalonia/
├── AuraCore.Tests.UI.Avalonia.csproj     NEW — xUnit + Avalonia.Headless.XUnit
├── AvaloniaTestBase.cs                    NEW — shared test fixture
├── ThemeTokenTests.cs                     NEW — resolve every token key
├── IconsTests.cs                          NEW — resolve every icon key
└── Controls/
    ├── GaugeTests.cs
    ├── GlassCardTests.cs
    ├── HeroCTATests.cs
    ├── InsightCardTests.cs
    ├── QuickActionTileTests.cs
    ├── SidebarNavItemTests.cs
    ├── SidebarSectionDividerTests.cs
    ├── StatusChipTests.cs
    ├── AuraToggleTests.cs
    ├── AccentBadgeTests.cs
    ├── UserChipTests.cs
    └── AppLogoBadgeTests.cs
```

### Modified

- `AuraCorePro.sln` — add new test project
- `src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj` — no changes (we use built-in Avalonia primitives only)

### Untouched

- `src/UI/AuraCore.UI.Avalonia/Themes/AuraCoreTheme.axaml` — old theme stays. Phase 2 handles switchover.
- `App.axaml` — no wiring of V2 yet. Phase 2 handles.
- Any `Views/Pages/*.axaml` — no changes.

---

## Task 1: Create the test project

**Files:**
- Create: `tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj`
- Modify: `AuraCorePro.sln`

- [ ] **Step 1.1: Create the .csproj file**

Create `tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj` with this exact content:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="coverlet.collector" Version="6.0.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Avalonia" Version="11.2.7" />
    <PackageReference Include="Avalonia.Headless.XUnit" Version="11.2.7" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="11.2.7" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\UI\AuraCore.UI.Avalonia\AuraCore.UI.Avalonia.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 1.2: Add the project to the solution**

Run:
```bash
cd C:/Users/Admin/Desktop/AuraCorePro/AuraCorePro
dotnet sln AuraCorePro.sln add tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj
```

Expected output:
```
Project `tests\AuraCore.Tests.UI.Avalonia\AuraCore.Tests.UI.Avalonia.csproj` added to the solution.
```

- [ ] **Step 1.3: Verify the project builds**

Run:
```bash
dotnet build tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj
```

Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 1.4: Commit**

```bash
git add tests/AuraCore.Tests.UI.Avalonia/ AuraCorePro.sln
git commit -m "test(ui): add Avalonia headless test project for design system"
```

---

## Task 2: Create the Avalonia test application harness

Avalonia.Headless.XUnit needs an `AvaloniaTestApplication` entry point. This sets up the test app that can load XAML and instantiate controls.

**Files:**
- Create: `tests/AuraCore.Tests.UI.Avalonia/AvaloniaTestApplication.cs`
- Create: `tests/AuraCore.Tests.UI.Avalonia/AvaloniaTestAssembly.cs`

- [ ] **Step 2.1: Write the test app**

Create `tests/AuraCore.Tests.UI.Avalonia/AvaloniaTestApplication.cs`:

```csharp
using Avalonia;
using Avalonia.Themes.Fluent;

namespace AuraCore.Tests.UI.Avalonia;

public class AvaloniaTestApplication : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }
}
```

- [ ] **Step 2.2: Register the headless app configuration**

Create `tests/AuraCore.Tests.UI.Avalonia/AvaloniaTestAssembly.cs`:

```csharp
using Avalonia;
using Avalonia.Headless;
using AuraCore.Tests.UI.Avalonia;

[assembly: AvaloniaTestApplication(typeof(AvaloniaTestAppBuilder))]

namespace AuraCore.Tests.UI.Avalonia;

public static class AvaloniaTestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<AvaloniaTestApplication>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
```

- [ ] **Step 2.3: Write a smoke test**

Create `tests/AuraCore.Tests.UI.Avalonia/SmokeTests.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia;

public class SmokeTests
{
    [AvaloniaFact]
    public void HeadlessPlatform_CanCreateControl()
    {
        var border = new Border();
        Assert.NotNull(border);
    }
}
```

- [ ] **Step 2.4: Run the smoke test**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~SmokeTests"
```

Expected: `Passed!  - 1/1` (one test passed).

- [ ] **Step 2.5: Commit**

```bash
git add tests/AuraCore.Tests.UI.Avalonia/AvaloniaTestApplication.cs tests/AuraCore.Tests.UI.Avalonia/AvaloniaTestAssembly.cs tests/AuraCore.Tests.UI.Avalonia/SmokeTests.cs
git commit -m "test(ui): wire up Avalonia headless test harness"
```

---

## Task 3: Build AuraCoreThemeV2 — color tokens

The Vision Document §5 specifies every color token. Create the new theme file with color resources only; typography/spacing/radius come in the next tasks.

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Themes/AuraCoreThemeV2.axaml`
- Create: `tests/AuraCore.Tests.UI.Avalonia/ThemeTokenTests.cs`

- [ ] **Step 3.1: Write the failing test**

Create `tests/AuraCore.Tests.UI.Avalonia/ThemeTokenTests.cs`:

```csharp
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia;

public class ThemeTokenTests
{
    private static Styles LoadThemeV2()
    {
        var uri = new System.Uri("avares://AuraCore.UI.Avalonia/Themes/AuraCoreThemeV2.axaml");
        return (Styles)AvaloniaXamlLoader.Load(uri);
    }

    [AvaloniaTheory]
    [InlineData("BgDeepBrush", "#0A0A10")]
    [InlineData("BgSurfaceBrush", "#0E0E14")]
    [InlineData("AccentTealBrush", "#00D4AA")]
    [InlineData("AccentPurpleBrush", "#B088FF")]
    [InlineData("AccentAmberBrush", "#F59E0B")]
    [InlineData("AccentPinkBrush", "#EC4899")]
    [InlineData("TextPrimaryBrush", "#F0F0F5")]
    [InlineData("TextSecondaryBrush", "#E8E8F0")]
    [InlineData("TextMutedBrush", "#888899")]
    public void ColorToken_Resolves_WithExpectedValue(string key, string expectedHex)
    {
        var styles = LoadThemeV2();
        var resources = styles.Resources;
        Assert.True(
            resources.TryGetResource(key, ThemeVariant.Dark, out var value),
            $"Token '{key}' not found in theme.");
        var brush = Assert.IsAssignableFrom<ISolidColorBrush>(value);
        Assert.Equal(Color.Parse(expectedHex), brush.Color);
    }
}
```

- [ ] **Step 3.2: Run the test, verify it fails**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~ThemeTokenTests"
```

Expected: all 9 theories FAIL with "Could not resolve Uri" (the file doesn't exist yet).

- [ ] **Step 3.3: Create the theme file with color tokens**

Create `src/UI/AuraCore.UI.Avalonia/Themes/AuraCoreThemeV2.axaml`:

```xml
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

  <!-- ══════════════════════════════════════════════════════
       AuraCore Pro V2 Design System
       Source: docs/superpowers/specs/2026-04-14-ui-rebuild-vision-document.md
       Dark-only in v1. Modern glassmorphism, cross-platform.
       ══════════════════════════════════════════════════════ -->

  <Styles.Resources>
    <ResourceDictionary>
      <ResourceDictionary.ThemeDictionaries>
        <ResourceDictionary x:Key="Dark">

          <!-- ── Backgrounds ──────────────────────────── -->
          <SolidColorBrush x:Key="BgDeepBrush" Color="#0A0A10"/>
          <SolidColorBrush x:Key="BgSurfaceBrush" Color="#0E0E14"/>
          <SolidColorBrush x:Key="BgSidebarBrush" Color="#0B0B11"/>
          <SolidColorBrush x:Key="BgCardBrush" Color="#06FFFFFF"/><!-- ~2.5% alpha white -->
          <SolidColorBrush x:Key="BgCardElevatedBrush" Color="#0AFFFFFF"/><!-- ~4% -->

          <!-- ── Borders ─────────────────────────────── -->
          <SolidColorBrush x:Key="BorderSubtleBrush" Color="#0FFFFFFF"/><!-- ~6% -->
          <SolidColorBrush x:Key="BorderEmphasisBrush" Color="#14FFFFFF"/><!-- ~8% -->

          <!-- ── Text ─────────────────────────────────── -->
          <SolidColorBrush x:Key="TextPrimaryBrush" Color="#F0F0F5"/>
          <SolidColorBrush x:Key="TextSecondaryBrush" Color="#E8E8F0"/>
          <SolidColorBrush x:Key="TextMutedBrush" Color="#888899"/>
          <SolidColorBrush x:Key="TextDisabledBrush" Color="#555570"/>

          <!-- ── Accent: Teal ─────────────────────────── -->
          <SolidColorBrush x:Key="AccentTealBrush" Color="#00D4AA"/>
          <SolidColorBrush x:Key="AccentTealLightBrush" Color="#6CE0C0"/>
          <SolidColorBrush x:Key="AccentTealDimBrush" Color="#1400D4AA"/><!-- ~8% -->

          <!-- ── Accent: Purple ───────────────────────── -->
          <SolidColorBrush x:Key="AccentPurpleBrush" Color="#B088FF"/>
          <SolidColorBrush x:Key="AccentPurpleDeepBrush" Color="#8B5CF6"/>
          <SolidColorBrush x:Key="AccentPurpleDimBrush" Color="#0F8B5CF6"/><!-- ~6% -->

          <!-- ── Accent: Amber ────────────────────────── -->
          <SolidColorBrush x:Key="AccentAmberBrush" Color="#F59E0B"/>
          <SolidColorBrush x:Key="AccentAmberDimBrush" Color="#14F59E0B"/><!-- ~8% -->

          <!-- ── Accent: Pink ─────────────────────────── -->
          <SolidColorBrush x:Key="AccentPinkBrush" Color="#EC4899"/>
          <SolidColorBrush x:Key="AccentPinkDimBrush" Color="#14EC4899"/><!-- ~8% -->

          <!-- ── Semantic ─────────────────────────────── -->
          <SolidColorBrush x:Key="StatusSuccessBrush" Color="#00D4AA"/>
          <SolidColorBrush x:Key="StatusWarningBrush" Color="#F59E0B"/>
          <SolidColorBrush x:Key="StatusErrorBrush" Color="#EF4444"/>
          <SolidColorBrush x:Key="StatusInfoBrush" Color="#B088FF"/>

        </ResourceDictionary>
      </ResourceDictionary.ThemeDictionaries>
    </ResourceDictionary>
  </Styles.Resources>
</Styles>
```

- [ ] **Step 3.4: Run the tests, verify they pass**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~ThemeTokenTests"
```

Expected: `Passed!  - 9/9`.

- [ ] **Step 3.5: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Themes/AuraCoreThemeV2.axaml tests/AuraCore.Tests.UI.Avalonia/ThemeTokenTests.cs
git commit -m "feat(ui): add AuraCoreThemeV2 color tokens"
```

---

## Task 4: Add typography, spacing, radius, and glow tokens to Theme V2

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Themes/AuraCoreThemeV2.axaml`
- Modify: `tests/AuraCore.Tests.UI.Avalonia/ThemeTokenTests.cs`

- [ ] **Step 4.1: Add failing tests for non-color tokens**

Open `tests/AuraCore.Tests.UI.Avalonia/ThemeTokenTests.cs` and add this method at the end of the class:

```csharp
    [AvaloniaTheory]
    [InlineData("RadiusXs", 4.0)]
    [InlineData("RadiusSm", 6.0)]
    [InlineData("RadiusMd", 8.0)]
    [InlineData("RadiusLg", 12.0)]
    [InlineData("RadiusXl", 14.0)]
    public void RadiusToken_Resolves_WithExpectedValue(string key, double expected)
    {
        var styles = LoadThemeV2();
        Assert.True(
            styles.Resources.TryGetResource(key, ThemeVariant.Dark, out var value),
            $"Radius token '{key}' not found.");
        var radius = Assert.IsType<CornerRadius>(value);
        Assert.Equal(expected, radius.TopLeft);
    }

    [AvaloniaTheory]
    [InlineData("FontSizeDisplay", 24.0)]
    [InlineData("FontSizeHeading", 18.0)]
    [InlineData("FontSizeSubheading", 14.0)]
    [InlineData("FontSizeBody", 12.0)]
    [InlineData("FontSizeBodySmall", 11.0)]
    [InlineData("FontSizeLabel", 10.0)]
    [InlineData("FontSizeCaption", 9.0)]
    public void FontSizeToken_Resolves_WithExpectedValue(string key, double expected)
    {
        var styles = LoadThemeV2();
        Assert.True(
            styles.Resources.TryGetResource(key, ThemeVariant.Dark, out var value),
            $"Font size token '{key}' not found.");
        Assert.Equal(expected, Assert.IsType<double>(value));
    }
```

- [ ] **Step 4.2: Run the tests, verify they fail**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~ThemeTokenTests"
```

Expected: 12 new theories FAIL with "not found" messages.

- [ ] **Step 4.3: Add tokens to the theme**

Open `src/UI/AuraCore.UI.Avalonia/Themes/AuraCoreThemeV2.axaml`. After the closing `</ResourceDictionary.ThemeDictionaries>` tag but before the closing `</ResourceDictionary>` tag that wraps everything, add this block:

```xml
      <!-- ── SHARED (theme-independent) TOKENS ────── -->

      <!-- Typography: font family stack -->
      <FontFamily x:Key="PrimaryFontFamily">Inter, Segoe UI Variable, Segoe UI, sans-serif</FontFamily>

      <!-- Typography: sizes (double) -->
      <x:Double x:Key="FontSizeDisplay">24</x:Double>
      <x:Double x:Key="FontSizeHeading">18</x:Double>
      <x:Double x:Key="FontSizeSubheading">14</x:Double>
      <x:Double x:Key="FontSizeBody">12</x:Double>
      <x:Double x:Key="FontSizeBodySmall">11</x:Double>
      <x:Double x:Key="FontSizeLabel">10</x:Double>
      <x:Double x:Key="FontSizeCaption">9</x:Double>

      <!-- Radius scale -->
      <CornerRadius x:Key="RadiusXs">4</CornerRadius>
      <CornerRadius x:Key="RadiusSm">6</CornerRadius>
      <CornerRadius x:Key="RadiusMd">8</CornerRadius>
      <CornerRadius x:Key="RadiusLg">12</CornerRadius>
      <CornerRadius x:Key="RadiusXl">14</CornerRadius>

      <!-- Spacing (Thickness) -->
      <Thickness x:Key="SpacingCardPadding">14</Thickness>
      <Thickness x:Key="SpacingPagePadding">22,18</Thickness>
      <Thickness x:Key="SpacingGapSm">0,6</Thickness>
      <Thickness x:Key="SpacingGapMd">0,10</Thickness>
      <Thickness x:Key="SpacingGapLg">0,14</Thickness>

      <!-- Glow shadows (BoxShadow "offsetX offsetY blur spread color") -->
      <BoxShadows x:Key="GlowTeal">0 0 20 0 #3300D4AA</BoxShadows>
      <BoxShadows x:Key="GlowPurple">0 0 20 0 #33B088FF</BoxShadows>
      <BoxShadows x:Key="GlowAmber">0 0 20 0 #2EF59E0B</BoxShadows>
      <BoxShadows x:Key="GlowPink">0 0 20 0 #33EC4899</BoxShadows>
      <BoxShadows x:Key="GlowHealth">0 0 28 0 #6600D4AA</BoxShadows>
      <BoxShadows x:Key="GlowHero">0 0 32 0 #1A00D4AA</BoxShadows>
```

- [ ] **Step 4.4: Run tests, verify they pass**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~ThemeTokenTests"
```

Expected: `Passed!  - 21/21` (9 color + 5 radius + 7 font size).

- [ ] **Step 4.5: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Themes/AuraCoreThemeV2.axaml tests/AuraCore.Tests.UI.Avalonia/ThemeTokenTests.cs
git commit -m "feat(ui): add typography, spacing, radius, glow tokens to theme v2"
```

---

## Task 5: Build the Lucide icon resource dictionary

~30 Lucide-style icons as `StreamGeometry` resources. No external package — keeps bundle lean and lets us match Lucide exactly. Pulled from lucide.dev/icons (MIT license, attribution in header comment).

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Themes/Icons.axaml`
- Create: `tests/AuraCore.Tests.UI.Avalonia/IconsTests.cs`

- [ ] **Step 5.1: Write the failing test**

Create `tests/AuraCore.Tests.UI.Avalonia/IconsTests.cs`:

```csharp
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia;

public class IconsTests
{
    private static Styles LoadIcons()
    {
        var uri = new System.Uri("avares://AuraCore.UI.Avalonia/Themes/Icons.axaml");
        return (Styles)AvaloniaXamlLoader.Load(uri);
    }

    [AvaloniaTheory]
    [InlineData("IconDashboard")]
    [InlineData("IconCpu")]
    [InlineData("IconRam")]
    [InlineData("IconGpu")]
    [InlineData("IconHardDrive")]
    [InlineData("IconHeart")]
    [InlineData("IconZap")]
    [InlineData("IconSparkles")]
    [InlineData("IconRotateCcw")]
    [InlineData("IconGamepad")]
    [InlineData("IconShield")]
    [InlineData("IconShieldCheck")]
    [InlineData("IconPackage")]
    [InlineData("IconStar")]
    [InlineData("IconSettings")]
    [InlineData("IconTarget")]
    [InlineData("IconTrendingUp")]
    [InlineData("IconAlertTriangle")]
    [InlineData("IconArrowRight")]
    [InlineData("IconTrash")]
    [InlineData("IconUser")]
    [InlineData("IconCheck")]
    [InlineData("IconChevronDown")]
    [InlineData("IconX")]
    [InlineData("IconEye")]
    [InlineData("IconActivity")]
    [InlineData("IconDroplet")]
    [InlineData("IconClock")]
    [InlineData("IconDatabase")]
    [InlineData("IconWifi")]
    public void Icon_Resolves_AsGeometry(string key)
    {
        var styles = LoadIcons();
        Assert.True(
            styles.Resources.TryGetResource(key, ThemeVariant.Default, out var value),
            $"Icon '{key}' not found.");
        Assert.IsAssignableFrom<Geometry>(value);
    }
}
```

- [ ] **Step 5.2: Run test, verify fail**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~IconsTests"
```

Expected: 30 theories FAIL with "Could not resolve Uri" (file missing).

- [ ] **Step 5.3: Create Icons.axaml**

Create `src/UI/AuraCore.UI.Avalonia/Themes/Icons.axaml`. Each icon is a 24×24 `StreamGeometry` converted from the Lucide SVG path:

```xml
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

  <!-- ══════════════════════════════════════════════════════
       Lucide Icons (MIT License) — https://lucide.dev
       Each icon is a 24×24 StreamGeometry, stroke="currentColor"
       stroke-width="2" by default when rendered via Path control.
       ══════════════════════════════════════════════════════ -->

  <Styles.Resources>
    <ResourceDictionary>

      <!-- layout-dashboard -->
      <StreamGeometry x:Key="IconDashboard">M3 3H10V12H3zM14 3H21V8H14zM14 12H21V21H14zM3 16H10V21H3z</StreamGeometry>

      <!-- cpu -->
      <StreamGeometry x:Key="IconCpu">M4 4H20A2 2 0 0 1 22 6V18A2 2 0 0 1 20 20H4A2 2 0 0 1 2 18V6A2 2 0 0 1 4 4zM9 9H15V15H9zM9 2V4M15 2V4M9 20V22M15 20V22M2 9H4M2 15H4M20 9H22M20 15H22</StreamGeometry>

      <!-- memory-stick -->
      <StreamGeometry x:Key="IconRam">M2 8H22V16H2zM4 8V6M8 8V6M12 8V6M16 8V6M20 8V6M6 16V18M10 16V18M14 16V18M18 16V18</StreamGeometry>

      <!-- circuit-board / gpu -->
      <StreamGeometry x:Key="IconGpu">M2 7H22V19H2zM8 13A2 2 0 1 1 8 13.001M16 13A1 1 0 1 1 16 13.001M2 11H6M18 11H22</StreamGeometry>

      <!-- hard-drive -->
      <StreamGeometry x:Key="IconHardDrive">M22 12H2M5.45 5.11L2 12V18A2 2 0 0 0 4 20H20A2 2 0 0 0 22 18V12L18.55 5.11A2 2 0 0 0 16.76 4H7.24A2 2 0 0 0 5.45 5.11zM6 16L6.01 16M10 16L10.01 16</StreamGeometry>

      <!-- heart (filled variant used via Path Fill property) -->
      <StreamGeometry x:Key="IconHeart">M20.84 4.61A5.5 5.5 0 0 0 13.06 4.61L12 5.67L10.94 4.61A5.5 5.5 0 0 0 3.16 12.39L4.22 13.45L12 21.23L19.78 13.45L20.84 12.39A5.5 5.5 0 0 0 20.84 4.61z</StreamGeometry>

      <!-- zap -->
      <StreamGeometry x:Key="IconZap">M13 2L3 14H12L11 22L21 10H12L13 2z</StreamGeometry>

      <!-- sparkles -->
      <StreamGeometry x:Key="IconSparkles">M9.937 15.5A2 2 0 0 0 8.5 14.063L2.365 12.481A0.5 0.5 0 0 1 2.365 11.519L8.5 9.936A2 2 0 0 0 9.937 8.5L11.519 2.365A0.5 0.5 0 0 1 12.482 2.365L14.063 8.5A2 2 0 0 0 15.5 9.937L21.636 11.518A0.5 0.5 0 0 1 21.636 12.482L15.5 14.063A2 2 0 0 0 14.063 15.5L12.482 21.636A0.5 0.5 0 0 1 11.519 21.636zM20 3V7M22 5H18M4 17V19M5 18H3</StreamGeometry>

      <!-- rotate-ccw -->
      <StreamGeometry x:Key="IconRotateCcw">M3 12A9 9 0 1 0 12 3A9.75 9.75 0 0 0 5.26 5.74L3 8M3 3V8H8</StreamGeometry>

      <!-- gamepad-2 -->
      <StreamGeometry x:Key="IconGamepad">M2 6H22A2 2 0 0 1 22 18H2A2 2 0 0 1 2 6zM6 12H10M8 10V14M15 10A1 1 0 1 1 15 10.001M18 13A1 1 0 1 1 18 13.001</StreamGeometry>

      <!-- shield -->
      <StreamGeometry x:Key="IconShield">M12 22S20 18 20 12V5L12 2L4 5V12C4 18 12 22 12 22z</StreamGeometry>

      <!-- shield-check -->
      <StreamGeometry x:Key="IconShieldCheck">M12 22S20 18 20 12V5L12 2L4 5V12C4 18 12 22 12 22zM9 12L11 14L15 10</StreamGeometry>

      <!-- package -->
      <StreamGeometry x:Key="IconPackage">M21 16V8A2 2 0 0 0 20 6.27L13 2.27A2 2 0 0 0 11 2.27L4 6.27A2 2 0 0 0 3 8V16A2 2 0 0 0 4 17.73L11 21.73A2 2 0 0 0 13 21.73L20 17.73A2 2 0 0 0 21 16zM3.27 6.96L12 12.01L20.73 6.96M12 22.08V12</StreamGeometry>

      <!-- star (filled, used for CORTEX sparkle) -->
      <StreamGeometry x:Key="IconStar">M12 2L14.4 9.4H22L16 14L18.3 21.4L12 17L5.7 21.4L8 14L2 9.4H9.6L12 2z</StreamGeometry>

      <!-- settings -->
      <StreamGeometry x:Key="IconSettings">M12 15A3 3 0 1 0 12 9A3 3 0 0 0 12 15zM19.4 15A1.65 1.65 0 0 0 19.73 16.82L19.79 16.88A2 2 0 0 1 17 19.71A2 2 0 0 1 16.93 19.71L16.87 19.65A1.65 1.65 0 0 0 15.05 19.32A1.65 1.65 0 0 0 14 20.83V21A2 2 0 0 1 10 21V20.91A1.65 1.65 0 0 0 9 19.4A1.65 1.65 0 0 0 7.18 19.73L7.12 19.79A2 2 0 0 1 4.29 17L4.35 16.94A1.65 1.65 0 0 0 4.68 15.12A1.65 1.65 0 0 0 3.17 14H3A2 2 0 0 1 3 10H3.09A1.65 1.65 0 0 0 4.6 9A1.65 1.65 0 0 0 4.27 7.18L4.21 7.12A2 2 0 0 1 7 4.29L7.06 4.35A1.65 1.65 0 0 0 8.88 4.68A1.65 1.65 0 0 0 10 3.17V3A2 2 0 0 1 14 3V3.09A1.65 1.65 0 0 0 15 4.6A1.65 1.65 0 0 0 16.82 4.27L16.88 4.21A2 2 0 0 1 19.71 7L19.65 7.06A1.65 1.65 0 0 0 19.32 8.88V9A1.65 1.65 0 0 0 20.83 10H21A2 2 0 0 1 21 14H20.91A1.65 1.65 0 0 0 19.4 15z</StreamGeometry>

      <!-- target (prediction) -->
      <StreamGeometry x:Key="IconTarget">M12 22A10 10 0 1 1 12 2A10 10 0 1 1 12 22zM12 18A6 6 0 1 1 12 6A6 6 0 1 1 12 18zM12 14A2 2 0 1 1 12 10A2 2 0 1 1 12 14z</StreamGeometry>

      <!-- trending-up (pattern learned) -->
      <StreamGeometry x:Key="IconTrendingUp">M3 3V21H21M7 14L10 9L14 12L19 5</StreamGeometry>

      <!-- alert-triangle -->
      <StreamGeometry x:Key="IconAlertTriangle">M10.29 3.86L1.82 18A2 2 0 0 0 3.53 21H20.47A2 2 0 0 0 22.18 18L13.71 3.86A2 2 0 0 0 10.29 3.86zM12 9V13M12 17L12.01 17</StreamGeometry>

      <!-- arrow-right -->
      <StreamGeometry x:Key="IconArrowRight">M5 12H19M12 5L19 12L12 19</StreamGeometry>

      <!-- trash-2 -->
      <StreamGeometry x:Key="IconTrash">M3 6H21M19 6V20A2 2 0 0 1 17 22H7A2 2 0 0 1 5 20V6M8 6V4A2 2 0 0 1 10 2H14A2 2 0 0 1 16 4V6M10 11V17M14 11V17</StreamGeometry>

      <!-- user -->
      <StreamGeometry x:Key="IconUser">M20 21V19A4 4 0 0 0 16 15H8A4 4 0 0 0 4 19V21M12 11A4 4 0 1 1 12 3A4 4 0 0 1 12 11z</StreamGeometry>

      <!-- check -->
      <StreamGeometry x:Key="IconCheck">M20 6L9 17L4 12</StreamGeometry>

      <!-- chevron-down -->
      <StreamGeometry x:Key="IconChevronDown">M6 9L12 15L18 9</StreamGeometry>

      <!-- x -->
      <StreamGeometry x:Key="IconX">M18 6L6 18M6 6L18 18</StreamGeometry>

      <!-- eye -->
      <StreamGeometry x:Key="IconEye">M1 12S5 4 12 4S23 12 23 12S19 20 12 20S1 12 1 12zM12 15A3 3 0 1 1 12 9A3 3 0 1 1 12 15z</StreamGeometry>

      <!-- activity -->
      <StreamGeometry x:Key="IconActivity">M22 12H18L15 21L9 3L6 12H2</StreamGeometry>

      <!-- droplet -->
      <StreamGeometry x:Key="IconDroplet">M12 2.69L17.66 8.35A8 8 0 1 1 6.34 8.35z</StreamGeometry>

      <!-- clock -->
      <StreamGeometry x:Key="IconClock">M12 22A10 10 0 1 1 12 2A10 10 0 1 1 12 22zM12 6V12L16 14</StreamGeometry>

      <!-- database -->
      <StreamGeometry x:Key="IconDatabase">M12 8A9 3 0 1 0 12 2A9 3 0 0 0 12 8zM3 5V19A9 3 0 0 0 21 19V5M3 12A9 3 0 0 0 21 12</StreamGeometry>

      <!-- wifi -->
      <StreamGeometry x:Key="IconWifi">M5 12.55A11 11 0 0 1 19 12.55M1.42 9A16 16 0 0 1 22.58 9M8.53 16.11A6 6 0 0 1 15.47 16.11M12 20L12.01 20</StreamGeometry>

    </ResourceDictionary>
  </Styles.Resources>
</Styles>
```

- [ ] **Step 5.4: Run tests, verify pass**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~IconsTests"
```

Expected: `Passed!  - 30/30`.

- [ ] **Step 5.5: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Themes/Icons.axaml tests/AuraCore.Tests.UI.Avalonia/IconsTests.cs
git commit -m "feat(ui): add Lucide icon resource dictionary (30 icons)"
```

---

## Task 6: AvaloniaTestBase — shared test infrastructure

Several component tests will need to instantiate a UserControl, attach it to a Window, and inspect its visual tree. Consolidate that setup.

**Files:**
- Create: `tests/AuraCore.Tests.UI.Avalonia/AvaloniaTestBase.cs`

- [ ] **Step 6.1: Create the base helper**

Create `tests/AuraCore.Tests.UI.Avalonia/AvaloniaTestBase.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Headless;

namespace AuraCore.Tests.UI.Avalonia;

public static class AvaloniaTestBase
{
    /// <summary>
    /// Attach <paramref name="control"/> to a headless Window, measure + arrange,
    /// then return the window so tests can inspect the visual tree.
    /// </summary>
    public static Window RenderInWindow(Control control, double width = 400, double height = 200)
    {
        var window = new Window
        {
            Width = width,
            Height = height,
            Content = control
        };
        window.Show();
        HeadlessWindowExtensions.GetLastRenderedFrame(window); // force render
        return window;
    }
}
```

- [ ] **Step 6.2: Build the test project to verify**

Run:
```bash
dotnet build tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj
```

Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 6.3: Commit**

```bash
git add tests/AuraCore.Tests.UI.Avalonia/AvaloniaTestBase.cs
git commit -m "test(ui): add AvaloniaTestBase helper"
```

---

## Task 7: Gauge component

Circular conic-gradient progress ring with center value + optional AI insight footer. Vision Doc §7 (Gauges section) is the spec.

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Controls/Gauge.axaml`
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Controls/Gauge.axaml.cs`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Controls/GaugeTests.cs`

- [ ] **Step 7.1: Write the failing test**

Create `tests/AuraCore.Tests.UI.Avalonia/Controls/GaugeTests.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using AuraCore.UI.Avalonia.Views.Controls;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Controls;

public class GaugeTests
{
    [AvaloniaFact]
    public void Gauge_InstantiatesWithDefaults()
    {
        var gauge = new Gauge();
        Assert.Equal(0.0, gauge.Value);
        Assert.Equal("CPU", gauge.Label);
        Assert.NotNull(gauge.RingBrush);
    }

    [AvaloniaFact]
    public void Gauge_ValueProperty_IsStyledAndBindable()
    {
        var gauge = new Gauge { Value = 42.5 };
        Assert.Equal(42.5, gauge.Value);
    }

    [AvaloniaFact]
    public void Gauge_LabelProperty_IsStyledAndBindable()
    {
        var gauge = new Gauge { Label = "RAM" };
        Assert.Equal("RAM", gauge.Label);
    }

    [AvaloniaFact]
    public void Gauge_RendersInWindow()
    {
        var gauge = new Gauge { Value = 50, Label = "GPU" };
        using var window = AvaloniaTestBase.RenderInWindow(gauge);
        Assert.True(gauge.IsMeasureValid);
    }
}
```

- [ ] **Step 7.2: Run the test, verify fail**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~GaugeTests"
```

Expected: compile error — `Gauge` type not found.

- [ ] **Step 7.3: Create the Gauge control (code-behind)**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/Gauge.axaml.cs`:

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Controls;

public partial class Gauge : UserControl
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<Gauge, double>(nameof(Value), 0.0);

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<Gauge, string>(nameof(Label), "CPU");

    public static readonly StyledProperty<string> SubLabelProperty =
        AvaloniaProperty.Register<Gauge, string>(nameof(SubLabel), string.Empty);

    public static readonly StyledProperty<string> InsightProperty =
        AvaloniaProperty.Register<Gauge, string>(nameof(Insight), string.Empty);

    public static readonly StyledProperty<IBrush> RingBrushProperty =
        AvaloniaProperty.Register<Gauge, IBrush>(nameof(RingBrush), Brushes.Teal);

    public static readonly StyledProperty<IBrush> InsightBrushProperty =
        AvaloniaProperty.Register<Gauge, IBrush>(nameof(InsightBrush), Brushes.Gray);

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<Gauge, Geometry?>(nameof(Icon));

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string SubLabel { get => GetValue(SubLabelProperty); set => SetValue(SubLabelProperty, value); }
    public string Insight { get => GetValue(InsightProperty); set => SetValue(InsightProperty, value); }
    public IBrush RingBrush { get => GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }
    public IBrush InsightBrush { get => GetValue(InsightBrushProperty); set => SetValue(InsightBrushProperty, value); }
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public Gauge()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 7.4: Create the Gauge XAML**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/Gauge.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="using:AuraCore.UI.Avalonia.Views.Controls"
             x:Class="AuraCore.UI.Avalonia.Views.Controls.Gauge"
             x:Name="Root">
  <Border CornerRadius="{DynamicResource RadiusLg}"
          BorderBrush="{DynamicResource BorderSubtleBrush}"
          BorderThickness="1"
          Background="{DynamicResource BgCardBrush}"
          Padding="12">
    <Grid RowDefinitions="Auto,*,Auto" RowSpacing="8">

      <!-- Header: label + icon -->
      <Grid Grid.Row="0" ColumnDefinitions="*,Auto">
        <TextBlock Text="{Binding #Root.Label}"
                   Foreground="{DynamicResource TextMutedBrush}"
                   FontSize="{DynamicResource FontSizeCaption}"
                   FontWeight="SemiBold"
                   LetterSpacing="1"/>
        <Path Grid.Column="1"
              Data="{Binding #Root.Icon}"
              Stroke="{Binding #Root.RingBrush}"
              StrokeThickness="2"
              Width="14" Height="14"/>
      </Grid>

      <!-- Center: circular ring with value -->
      <Grid Grid.Row="1" HorizontalAlignment="Center">
        <!-- Outer ring (full circle, subtle) -->
        <Ellipse Width="56" Height="56"
                 Stroke="{DynamicResource BorderSubtleBrush}"
                 StrokeThickness="6"/>
        <!-- Progress arc drawn via StrokeDashArray trick -->
        <Ellipse Width="56" Height="56"
                 Stroke="{Binding #Root.RingBrush}"
                 StrokeThickness="6"
                 StrokeDashArray="{Binding #Root.Value, Converter={x:Static local:GaugeDashConverter.Instance}}"/>
        <!-- Center value -->
        <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center">
          <TextBlock Text="{Binding #Root.Value, StringFormat={}{0:0}}"
                     Foreground="{DynamicResource TextPrimaryBrush}"
                     FontSize="14" FontWeight="Bold"
                     HorizontalAlignment="Center"/>
          <TextBlock Text="{Binding #Root.SubLabel}"
                     Foreground="{DynamicResource TextMutedBrush}"
                     FontSize="7"
                     HorizontalAlignment="Center"
                     IsVisible="{Binding #Root.SubLabel, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"/>
        </StackPanel>
      </Grid>

      <!-- Footer: AI insight -->
      <TextBlock Grid.Row="2"
                 Text="{Binding #Root.Insight}"
                 Foreground="{Binding #Root.InsightBrush}"
                 FontSize="9"
                 HorizontalAlignment="Center"
                 IsVisible="{Binding #Root.Insight, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"/>
    </Grid>
  </Border>
</UserControl>
```

- [ ] **Step 7.5: Create the StrokeDashArray converter**

The arc progress is simulated via StrokeDashArray on a full Ellipse. Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/GaugeDashConverter.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Collections;
using Avalonia.Data.Converters;

namespace AuraCore.UI.Avalonia.Views.Controls;

/// <summary>Converts a 0-100 value into a StrokeDashArray that shows that fraction of a 56px-diameter circle's circumference.</summary>
public sealed class GaugeDashConverter : IValueConverter
{
    public static readonly GaugeDashConverter Instance = new();

    // Circumference of a 56-diameter ring = π·d ÷ strokeThickness ratio
    // Ellipse uses stroke width in its own units; at thickness=6, dash-on = (circumference/6) * (value/100)
    private const double Circumference = Math.PI * 56.0 / 6.0;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var pct = value is double d ? d : 0;
        pct = Math.Clamp(pct, 0, 100);
        var on = Circumference * (pct / 100.0);
        var off = Circumference - on;
        return new AvaloniaList<double> { on, off };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

- [ ] **Step 7.6: Run tests, verify pass**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~GaugeTests"
```

Expected: `Passed!  - 4/4`.

- [ ] **Step 7.7: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Controls/Gauge.axaml src/UI/AuraCore.UI.Avalonia/Views/Controls/Gauge.axaml.cs src/UI/AuraCore.UI.Avalonia/Views/Controls/GaugeDashConverter.cs tests/AuraCore.Tests.UI.Avalonia/Controls/GaugeTests.cs
git commit -m "feat(ui): add Gauge control with conic-style progress"
```

---

## Task 8: GlassCard component

A reusable translucent card with optional glow. Accepts any content. Vision Doc §8.

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Controls/GlassCard.axaml`
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Controls/GlassCard.axaml.cs`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Controls/GlassCardTests.cs`

- [ ] **Step 8.1: Write the failing test**

Create `tests/AuraCore.Tests.UI.Avalonia/Controls/GlassCardTests.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using AuraCore.UI.Avalonia.Views.Controls;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Controls;

public class GlassCardTests
{
    [AvaloniaFact]
    public void GlassCard_InstantiatesWithDefaults()
    {
        var card = new GlassCard();
        Assert.Null(card.CardContent);
        Assert.Equal(12.0, card.CardCornerRadius.TopLeft);
    }

    [AvaloniaFact]
    public void GlassCard_AcceptsContent()
    {
        var tb = new TextBlock { Text = "hello" };
        var card = new GlassCard { CardContent = tb };
        Assert.Same(tb, card.CardContent);
    }

    [AvaloniaFact]
    public void GlassCard_RendersInWindow()
    {
        var card = new GlassCard { CardContent = new TextBlock { Text = "hello" } };
        using var window = AvaloniaTestBase.RenderInWindow(card, 300, 200);
        Assert.True(card.IsMeasureValid);
    }
}
```

- [ ] **Step 8.2: Run test, verify fail**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~GlassCardTests"
```

Expected: compile error — `GlassCard` not found.

- [ ] **Step 8.3: Create GlassCard code-behind**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/GlassCard.axaml.cs`:

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Controls;

public partial class GlassCard : UserControl
{
    public static readonly StyledProperty<object?> CardContentProperty =
        AvaloniaProperty.Register<GlassCard, object?>(nameof(CardContent));

    public static readonly StyledProperty<CornerRadius> CardCornerRadiusProperty =
        AvaloniaProperty.Register<GlassCard, CornerRadius>(nameof(CardCornerRadius), new CornerRadius(12));

    public static readonly StyledProperty<BoxShadows> CardGlowProperty =
        AvaloniaProperty.Register<GlassCard, BoxShadows>(nameof(CardGlow), default);

    public object? CardContent { get => GetValue(CardContentProperty); set => SetValue(CardContentProperty, value); }
    public CornerRadius CardCornerRadius { get => GetValue(CardCornerRadiusProperty); set => SetValue(CardCornerRadiusProperty, value); }
    public BoxShadows CardGlow { get => GetValue(CardGlowProperty); set => SetValue(CardGlowProperty, value); }

    public GlassCard()
    {
        InitializeComponent();
    }
}
```

NOTE: GlassCard has ONE default look (BgCardBrush + BorderSubtleBrush). Consumers wanting tinted variants (e.g. amber hero card) use the colored cards (HeroCTA, InsightCard) that are built separately — they already have custom backgrounds baked in.

- [ ] **Step 8.4: Create GlassCard XAML**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/GlassCard.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="AuraCore.UI.Avalonia.Views.Controls.GlassCard"
             x:Name="Root">
  <Border CornerRadius="{Binding #Root.CardCornerRadius}"
          BorderThickness="1"
          Padding="{DynamicResource SpacingCardPadding}"
          BoxShadow="{Binding #Root.CardGlow}"
          Background="{DynamicResource BgCardBrush}"
          BorderBrush="{DynamicResource BorderSubtleBrush}">
    <ContentPresenter Content="{Binding #Root.CardContent}"/>
  </Border>
</UserControl>
```

- [ ] **Step 8.5: Run tests, verify pass**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~GlassCardTests"
```

Expected: `Passed!  - 3/3`.

- [ ] **Step 8.6: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Controls/GlassCard.axaml src/UI/AuraCore.UI.Avalonia/Views/Controls/GlassCard.axaml.cs tests/AuraCore.Tests.UI.Avalonia/Controls/GlassCardTests.cs
git commit -m "feat(ui): add GlassCard translucent card primitive"
```

---

## Task 9: HeroCTA component

The hero call-to-action card. Has kicker, title, body, primary button, optional secondary button. Vision Doc §7 and mockup v4.

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Controls/HeroCTA.axaml`
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Controls/HeroCTA.axaml.cs`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Controls/HeroCTATests.cs`

- [ ] **Step 9.1: Write the failing test**

Create `tests/AuraCore.Tests.UI.Avalonia/Controls/HeroCTATests.cs`:

```csharp
using System.Windows.Input;
using Avalonia.Headless.XUnit;
using AuraCore.UI.Avalonia.Views.Controls;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Controls;

public class HeroCTATests
{
    [AvaloniaFact]
    public void HeroCTA_Defaults()
    {
        var hero = new HeroCTA();
        Assert.Equal(string.Empty, hero.Kicker);
        Assert.Equal(string.Empty, hero.Title);
        Assert.Equal("Go", hero.PrimaryButtonText);
    }

    [AvaloniaFact]
    public void HeroCTA_PrimaryCommand_Invokes()
    {
        var invoked = false;
        var hero = new HeroCTA
        {
            PrimaryCommand = new TestCommand(() => invoked = true)
        };
        hero.PrimaryCommand.Execute(null);
        Assert.True(invoked);
    }

    [AvaloniaFact]
    public void HeroCTA_RendersInWindow()
    {
        var hero = new HeroCTA
        {
            Kicker = "CORTEX RECOMMENDS",
            Title = "Smart Optimize Now",
            Body = "RAM cleanup + 3 bloatware apps"
        };
        using var window = AvaloniaTestBase.RenderInWindow(hero, 400, 200);
        Assert.True(hero.IsMeasureValid);
    }

    private sealed class TestCommand : ICommand
    {
        private readonly System.Action _action;
        public TestCommand(System.Action action) => _action = action;
        public event System.EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _action();
    }
}
```

- [ ] **Step 9.2: Run test, verify fail**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~HeroCTATests"
```

Expected: compile error — `HeroCTA` not found.

- [ ] **Step 9.3: Create HeroCTA code-behind**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/HeroCTA.axaml.cs`:

```csharp
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace AuraCore.UI.Avalonia.Views.Controls;

public partial class HeroCTA : UserControl
{
    public static readonly StyledProperty<string> KickerProperty =
        AvaloniaProperty.Register<HeroCTA, string>(nameof(Kicker), string.Empty);

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<HeroCTA, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string> BodyProperty =
        AvaloniaProperty.Register<HeroCTA, string>(nameof(Body), string.Empty);

    public static readonly StyledProperty<string> PrimaryButtonTextProperty =
        AvaloniaProperty.Register<HeroCTA, string>(nameof(PrimaryButtonText), "Go");

    public static readonly StyledProperty<string> SecondaryButtonTextProperty =
        AvaloniaProperty.Register<HeroCTA, string>(nameof(SecondaryButtonText), string.Empty);

    public static readonly StyledProperty<ICommand?> PrimaryCommandProperty =
        AvaloniaProperty.Register<HeroCTA, ICommand?>(nameof(PrimaryCommand));

    public static readonly StyledProperty<ICommand?> SecondaryCommandProperty =
        AvaloniaProperty.Register<HeroCTA, ICommand?>(nameof(SecondaryCommand));

    public string Kicker { get => GetValue(KickerProperty); set => SetValue(KickerProperty, value); }
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Body { get => GetValue(BodyProperty); set => SetValue(BodyProperty, value); }
    public string PrimaryButtonText { get => GetValue(PrimaryButtonTextProperty); set => SetValue(PrimaryButtonTextProperty, value); }
    public string SecondaryButtonText { get => GetValue(SecondaryButtonTextProperty); set => SetValue(SecondaryButtonTextProperty, value); }
    public ICommand? PrimaryCommand { get => GetValue(PrimaryCommandProperty); set => SetValue(PrimaryCommandProperty, value); }
    public ICommand? SecondaryCommand { get => GetValue(SecondaryCommandProperty); set => SetValue(SecondaryCommandProperty, value); }

    public HeroCTA()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 9.4: Create HeroCTA XAML**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/HeroCTA.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="AuraCore.UI.Avalonia.Views.Controls.HeroCTA"
             x:Name="Root">
  <Border CornerRadius="{DynamicResource RadiusXl}"
          BorderBrush="{DynamicResource AccentTealDimBrush}"
          BorderThickness="1"
          Padding="18"
          BoxShadow="{DynamicResource GlowHero}">
    <Border.Background>
      <LinearGradientBrush StartPoint="0%,0%" EndPoint="100%,100%">
        <GradientStop Offset="0" Color="#2600D4AA"/>
        <GradientStop Offset="1" Color="#198B5CF6"/>
      </LinearGradientBrush>
    </Border.Background>
    <StackPanel Spacing="4">
      <!-- Kicker -->
      <TextBlock Text="{Binding #Root.Kicker}"
                 Foreground="{DynamicResource AccentPurpleBrush}"
                 FontSize="{DynamicResource FontSizeLabel}"
                 FontWeight="Bold"
                 LetterSpacing="1"
                 IsVisible="{Binding #Root.Kicker, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"/>
      <!-- Title -->
      <TextBlock Text="{Binding #Root.Title}"
                 Foreground="{DynamicResource TextPrimaryBrush}"
                 FontSize="17"
                 FontWeight="SemiBold"/>
      <!-- Body -->
      <TextBlock Text="{Binding #Root.Body}"
                 Foreground="{DynamicResource TextSecondaryBrush}"
                 FontSize="{DynamicResource FontSizeBodySmall}"
                 TextWrapping="Wrap"
                 Margin="0,0,0,14"/>
      <!-- Buttons -->
      <StackPanel Orientation="Horizontal" Spacing="8">
        <Button Content="{Binding #Root.PrimaryButtonText}"
                Command="{Binding #Root.PrimaryCommand}"
                Background="{DynamicResource AccentTealBrush}"
                Foreground="{DynamicResource BgDeepBrush}"
                Padding="18,8"
                CornerRadius="{DynamicResource RadiusMd}"
                FontWeight="Bold"
                FontSize="{DynamicResource FontSizeBody}"/>
        <Button Content="{Binding #Root.SecondaryButtonText}"
                Command="{Binding #Root.SecondaryCommand}"
                Background="{DynamicResource BgCardElevatedBrush}"
                Foreground="{DynamicResource TextPrimaryBrush}"
                BorderBrush="{DynamicResource BorderSubtleBrush}"
                BorderThickness="1"
                Padding="14,8"
                CornerRadius="{DynamicResource RadiusMd}"
                FontSize="{DynamicResource FontSizeBody}"
                IsVisible="{Binding #Root.SecondaryButtonText, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"/>
      </StackPanel>
    </StackPanel>
  </Border>
</UserControl>
```

- [ ] **Step 9.5: Run tests, verify pass**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~HeroCTATests"
```

Expected: `Passed!  - 3/3`.

- [ ] **Step 9.6: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Controls/HeroCTA.axaml src/UI/AuraCore.UI.Avalonia/Views/Controls/HeroCTA.axaml.cs tests/AuraCore.Tests.UI.Avalonia/Controls/HeroCTATests.cs
git commit -m "feat(ui): add HeroCTA component"
```

---

## Task 10: InsightCard component

A vertical list of insight rows. Each row has colored icon + title + description. Vision Doc §7 (Cortex Insights).

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Controls/InsightCard.axaml`
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Controls/InsightCard.axaml.cs`
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Controls/InsightRow.cs`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Controls/InsightCardTests.cs`

- [ ] **Step 10.1: Write the failing test**

Create `tests/AuraCore.Tests.UI.Avalonia/Controls/InsightCardTests.cs`:

```csharp
using System.Collections.ObjectModel;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using AuraCore.UI.Avalonia.Views.Controls;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Controls;

public class InsightCardTests
{
    [AvaloniaFact]
    public void InsightCard_Defaults()
    {
        var card = new InsightCard();
        Assert.Equal("Insights", card.Title);
        Assert.NotNull(card.Rows);
        Assert.Empty(card.Rows);
    }

    [AvaloniaFact]
    public void InsightCard_AcceptsRows()
    {
        var card = new InsightCard
        {
            Rows = new ObservableCollection<InsightRow>
            {
                new() { Title = "Spike", Description = "Brave 42% idle", IconBrush = Brushes.Orange },
                new() { Title = "Pattern", Description = "Gaming at 21:00", IconBrush = Brushes.Teal }
            }
        };
        Assert.Equal(2, card.Rows.Count);
    }

    [AvaloniaFact]
    public void InsightCard_RendersInWindow()
    {
        var card = new InsightCard { Title = "Cortex Insights" };
        using var window = AvaloniaTestBase.RenderInWindow(card, 300, 200);
        Assert.True(card.IsMeasureValid);
    }
}
```

- [ ] **Step 10.2: Run test, verify fail**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~InsightCardTests"
```

Expected: compile error.

- [ ] **Step 10.3: Create the InsightRow data model**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/InsightRow.cs`:

```csharp
using Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Controls;

public sealed class InsightRow
{
    public Geometry? Icon { get; set; }
    public IBrush IconBrush { get; set; } = Brushes.Gray;
    public string Title { get; set; } = string.Empty;
    public IBrush TitleBrush { get; set; } = Brushes.White;
    public string Description { get; set; } = string.Empty;
}
```

- [ ] **Step 10.4: Create InsightCard code-behind**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/InsightCard.axaml.cs`:

```csharp
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;

namespace AuraCore.UI.Avalonia.Views.Controls;

public partial class InsightCard : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<InsightCard, string>(nameof(Title), "Insights");

    public static readonly StyledProperty<string> UpdatedAtProperty =
        AvaloniaProperty.Register<InsightCard, string>(nameof(UpdatedAt), string.Empty);

    public static readonly StyledProperty<ObservableCollection<InsightRow>> RowsProperty =
        AvaloniaProperty.Register<InsightCard, ObservableCollection<InsightRow>>(
            nameof(Rows), new ObservableCollection<InsightRow>());

    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string UpdatedAt { get => GetValue(UpdatedAtProperty); set => SetValue(UpdatedAtProperty, value); }
    public ObservableCollection<InsightRow> Rows { get => GetValue(RowsProperty); set => SetValue(RowsProperty, value); }

    public InsightCard()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 10.5: Create InsightCard XAML**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/InsightCard.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="AuraCore.UI.Avalonia.Views.Controls.InsightCard"
             x:Name="Root">
  <Border CornerRadius="{DynamicResource RadiusXl}"
          BorderBrush="{DynamicResource AccentPurpleDimBrush}"
          BorderThickness="1"
          Background="{DynamicResource AccentPurpleDimBrush}"
          Padding="14">
    <StackPanel Spacing="10">
      <!-- Header -->
      <Grid ColumnDefinitions="*,Auto">
        <TextBlock Text="{Binding #Root.Title}"
                   Foreground="{DynamicResource AccentPurpleBrush}"
                   FontSize="{DynamicResource FontSizeBodySmall}"
                   FontWeight="Bold"/>
        <TextBlock Grid.Column="1"
                   Text="{Binding #Root.UpdatedAt}"
                   Foreground="{DynamicResource TextMutedBrush}"
                   FontSize="{DynamicResource FontSizeCaption}"/>
      </Grid>
      <!-- Rows -->
      <ItemsControl ItemsSource="{Binding #Root.Rows}">
        <ItemsControl.ItemTemplate>
          <DataTemplate DataType="controls:InsightRow"
                        xmlns:controls="using:AuraCore.UI.Avalonia.Views.Controls">
            <Grid ColumnDefinitions="Auto,*" Margin="0,0,0,10">
              <Path Data="{Binding Icon}"
                    Stroke="{Binding IconBrush}"
                    StrokeThickness="2"
                    Width="13" Height="13"
                    VerticalAlignment="Top"
                    Margin="0,2,8,0"/>
              <StackPanel Grid.Column="1">
                <TextBlock Text="{Binding Title}"
                           Foreground="{Binding TitleBrush}"
                           FontSize="{DynamicResource FontSizeLabel}"
                           FontWeight="SemiBold"/>
                <TextBlock Text="{Binding Description}"
                           Foreground="{DynamicResource TextSecondaryBrush}"
                           FontSize="{DynamicResource FontSizeLabel}"
                           TextWrapping="Wrap"/>
              </StackPanel>
            </Grid>
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>
    </StackPanel>
  </Border>
</UserControl>
```

- [ ] **Step 10.6: Run tests, verify pass**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~InsightCardTests"
```

Expected: `Passed!  - 3/3`.

- [ ] **Step 10.7: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Controls/InsightCard.axaml src/UI/AuraCore.UI.Avalonia/Views/Controls/InsightCard.axaml.cs src/UI/AuraCore.UI.Avalonia/Views/Controls/InsightRow.cs tests/AuraCore.Tests.UI.Avalonia/Controls/InsightCardTests.cs
git commit -m "feat(ui): add InsightCard + InsightRow data model"
```

---

## Task 11: QuickActionTile component

Small colored action tile: icon + title + sub-label + command. Vision Doc §7.

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Controls/QuickActionTile.axaml`
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Controls/QuickActionTile.axaml.cs`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Controls/QuickActionTileTests.cs`

- [ ] **Step 11.1: Write the failing test**

Create `tests/AuraCore.Tests.UI.Avalonia/Controls/QuickActionTileTests.cs`:

```csharp
using System.Windows.Input;
using Avalonia.Headless.XUnit;
using AuraCore.UI.Avalonia.Views.Controls;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Controls;

public class QuickActionTileTests
{
    [AvaloniaFact]
    public void QuickActionTile_Defaults()
    {
        var tile = new QuickActionTile();
        Assert.Equal(string.Empty, tile.Title);
        Assert.Equal(string.Empty, tile.SubLabel);
    }

    [AvaloniaFact]
    public void QuickActionTile_CommandInvokes()
    {
        var fired = false;
        var tile = new QuickActionTile
        {
            Title = "Clean Junk",
            Command = new DelegateCmd(() => fired = true)
        };
        tile.Command!.Execute(null);
        Assert.True(fired);
    }

    [AvaloniaFact]
    public void QuickActionTile_RendersInWindow()
    {
        var tile = new QuickActionTile { Title = "Clean Junk", SubLabel = "Temp, cache, logs" };
        using var window = AvaloniaTestBase.RenderInWindow(tile, 200, 80);
        Assert.True(tile.IsMeasureValid);
    }

    private sealed class DelegateCmd : ICommand
    {
        private readonly System.Action _a;
        public DelegateCmd(System.Action a) => _a = a;
        public event System.EventHandler? CanExecuteChanged;
        public bool CanExecute(object? p) => true;
        public void Execute(object? p) => _a();
    }
}
```

- [ ] **Step 11.2: Run, verify fail**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~QuickActionTileTests"
```

Expected: compile error.

- [ ] **Step 11.3: Create the code-behind**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/QuickActionTile.axaml.cs`:

```csharp
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Controls;

public partial class QuickActionTile : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<QuickActionTile, string>(nameof(Title), string.Empty);
    public static readonly StyledProperty<string> SubLabelProperty =
        AvaloniaProperty.Register<QuickActionTile, string>(nameof(SubLabel), string.Empty);
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<QuickActionTile, Geometry?>(nameof(Icon));
    public static readonly StyledProperty<IBrush> TintBrushProperty =
        AvaloniaProperty.Register<QuickActionTile, IBrush>(nameof(TintBrush), Brushes.Teal);
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<QuickActionTile, ICommand?>(nameof(Command));

    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string SubLabel { get => GetValue(SubLabelProperty); set => SetValue(SubLabelProperty, value); }
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public IBrush TintBrush { get => GetValue(TintBrushProperty); set => SetValue(TintBrushProperty, value); }
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    public QuickActionTile() { InitializeComponent(); }
}
```

- [ ] **Step 11.4: Create the XAML**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/QuickActionTile.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="AuraCore.UI.Avalonia.Views.Controls.QuickActionTile"
             x:Name="Root">
  <Button Command="{Binding #Root.Command}"
          Padding="0"
          Background="Transparent"
          BorderThickness="0"
          Cursor="Hand"
          HorizontalContentAlignment="Stretch">
    <Border Padding="10"
            CornerRadius="{DynamicResource RadiusMd}"
            BorderThickness="1">
      <Border.Background>
        <SolidColorBrush Color="{Binding #Root.TintBrush.Color, FallbackValue=#00D4AA}" Opacity="0.08"/>
      </Border.Background>
      <Border.BorderBrush>
        <SolidColorBrush Color="{Binding #Root.TintBrush.Color, FallbackValue=#00D4AA}" Opacity="0.15"/>
      </Border.BorderBrush>
      <StackPanel>
        <Grid ColumnDefinitions="Auto,*" Margin="0,0,0,2">
          <Path Data="{Binding #Root.Icon}"
                Stroke="{Binding #Root.TintBrush}"
                StrokeThickness="2"
                Width="11" Height="11"
                Margin="0,0,6,0"/>
          <TextBlock Grid.Column="1"
                     Text="{Binding #Root.Title}"
                     Foreground="{Binding #Root.TintBrush}"
                     FontSize="{DynamicResource FontSizeBody}"/>
        </Grid>
        <TextBlock Text="{Binding #Root.SubLabel}"
                   Foreground="{DynamicResource TextMutedBrush}"
                   FontSize="{DynamicResource FontSizeCaption}"/>
      </StackPanel>
    </Border>
  </Button>
</UserControl>
```

- [ ] **Step 11.5: Run tests, verify pass**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~QuickActionTileTests"
```

Expected: `Passed!  - 3/3`.

- [ ] **Step 11.6: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Controls/QuickActionTile.axaml src/UI/AuraCore.UI.Avalonia/Views/Controls/QuickActionTile.axaml.cs tests/AuraCore.Tests.UI.Avalonia/Controls/QuickActionTileTests.cs
git commit -m "feat(ui): add QuickActionTile"
```

---

## Task 12: SidebarNavItem

Sidebar nav entry: icon + label + optional trailing chip. Has `IsActive` property for active state. Vision Doc §6.

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Controls/SidebarNavItem.axaml`
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Controls/SidebarNavItem.axaml.cs`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Controls/SidebarNavItemTests.cs`

- [ ] **Step 12.1: Write the failing test**

Create `tests/AuraCore.Tests.UI.Avalonia/Controls/SidebarNavItemTests.cs`:

```csharp
using Avalonia.Headless.XUnit;
using AuraCore.UI.Avalonia.Views.Controls;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Controls;

public class SidebarNavItemTests
{
    [AvaloniaFact]
    public void SidebarNavItem_Defaults()
    {
        var nav = new SidebarNavItem();
        Assert.Equal(string.Empty, nav.Label);
        Assert.False(nav.IsActive);
        Assert.Equal(string.Empty, nav.TrailingChipText);
    }

    [AvaloniaFact]
    public void SidebarNavItem_IsActiveToggles()
    {
        var nav = new SidebarNavItem { Label = "Dashboard" };
        nav.IsActive = true;
        Assert.True(nav.IsActive);
    }

    [AvaloniaFact]
    public void SidebarNavItem_RendersInWindow()
    {
        var nav = new SidebarNavItem { Label = "AI Features", TrailingChipText = "CORTEX" };
        using var window = AvaloniaTestBase.RenderInWindow(nav, 220, 32);
        Assert.True(nav.IsMeasureValid);
    }
}
```

- [ ] **Step 12.2: Run, verify fail**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~SidebarNavItemTests"
```

- [ ] **Step 12.3: Create code-behind**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/SidebarNavItem.axaml.cs`:

```csharp
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Controls;

public partial class SidebarNavItem : UserControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<SidebarNavItem, string>(nameof(Label), string.Empty);
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<SidebarNavItem, Geometry?>(nameof(Icon));
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<SidebarNavItem, bool>(nameof(IsActive), false);
    public static readonly StyledProperty<string> TrailingChipTextProperty =
        AvaloniaProperty.Register<SidebarNavItem, string>(nameof(TrailingChipText), string.Empty);
    public static readonly StyledProperty<IBrush> AccentBrushProperty =
        AvaloniaProperty.Register<SidebarNavItem, IBrush>(nameof(AccentBrush), Brushes.Teal);
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<SidebarNavItem, ICommand?>(nameof(Command));

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public bool IsActive { get => GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }
    public string TrailingChipText { get => GetValue(TrailingChipTextProperty); set => SetValue(TrailingChipTextProperty, value); }
    public IBrush AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    public SidebarNavItem() { InitializeComponent(); }
}
```

- [ ] **Step 12.4: Create XAML**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/SidebarNavItem.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="using:AuraCore.UI.Avalonia.Views.Controls"
             x:Class="AuraCore.UI.Avalonia.Views.Controls.SidebarNavItem"
             x:Name="Root">
  <Button Command="{Binding #Root.Command}"
          Background="Transparent"
          BorderThickness="0"
          Padding="0"
          Cursor="Hand"
          HorizontalAlignment="Stretch"
          HorizontalContentAlignment="Stretch">
    <Border Padding="10,7" CornerRadius="0,6,6,0" BorderThickness="2,0,0,0">
      <Border.Background>
        <MultiBinding Converter="{x:Static local:ActiveBackgroundConverter.Instance}">
          <Binding Path="IsActive" ElementName="Root"/>
          <Binding Path="AccentBrush" ElementName="Root"/>
        </MultiBinding>
      </Border.Background>
      <Border.BorderBrush>
        <MultiBinding Converter="{x:Static local:ActiveAccentConverter.Instance}">
          <Binding Path="IsActive" ElementName="Root"/>
          <Binding Path="AccentBrush" ElementName="Root"/>
        </MultiBinding>
      </Border.BorderBrush>
      <Grid ColumnDefinitions="Auto,*,Auto">
        <Path Data="{Binding #Root.Icon}"
              Width="14" Height="14"
              StrokeThickness="2"
              VerticalAlignment="Center">
          <Path.Stroke>
            <MultiBinding Converter="{x:Static local:ActiveForegroundConverter.Instance}">
              <Binding Path="IsActive" ElementName="Root"/>
              <Binding Path="AccentBrush" ElementName="Root"/>
            </MultiBinding>
          </Path.Stroke>
        </Path>
        <TextBlock Grid.Column="1"
                   Text="{Binding #Root.Label}"
                   FontSize="{DynamicResource FontSizeBody}"
                   VerticalAlignment="Center"
                   Margin="10,0,0,0">
          <TextBlock.Foreground>
            <MultiBinding Converter="{x:Static local:ActiveForegroundConverter.Instance}">
              <Binding Path="IsActive" ElementName="Root"/>
              <Binding Path="AccentBrush" ElementName="Root"/>
            </MultiBinding>
          </TextBlock.Foreground>
        </TextBlock>
        <Border Grid.Column="2"
                Padding="5,1"
                CornerRadius="3"
                VerticalAlignment="Center"
                IsVisible="{Binding #Root.TrailingChipText, Converter={x:Static StringConverters.IsNotNullOrEmpty}}">
          <Border.Background>
            <SolidColorBrush Color="{Binding #Root.AccentBrush.Color, FallbackValue=#00D4AA}" Opacity="0.2"/>
          </Border.Background>
          <TextBlock Text="{Binding #Root.TrailingChipText}"
                     FontSize="7"
                     FontWeight="Bold"
                     Foreground="{Binding #Root.AccentBrush}"/>
        </Border>
      </Grid>
    </Border>
  </Button>
</UserControl>
```

- [ ] **Step 12.5: Add the nav state converters**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/NavItemConverters.cs`:

```csharp
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Controls;

public sealed class ActiveBackgroundConverter : IMultiValueConverter
{
    public static readonly ActiveBackgroundConverter Instance = new();
    public object? Convert(IList<object?> values, System.Type targetType, object? parameter, CultureInfo culture)
    {
        var isActive = values.Count > 0 && values[0] is bool b && b;
        var accent = values.Count > 1 && values[1] is ISolidColorBrush s ? s.Color : Color.Parse("#00D4AA");
        if (!isActive) return Brushes.Transparent;
        return new LinearGradientBrush
        {
            StartPoint = new Avalonia.RelativePoint(0, 0, Avalonia.RelativeUnit.Relative),
            EndPoint = new Avalonia.RelativePoint(1, 0, Avalonia.RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0x1F, accent.R, accent.G, accent.B), 0),
                new GradientStop(Colors.Transparent, 1)
            }
        };
    }
}

public sealed class ActiveAccentConverter : IMultiValueConverter
{
    public static readonly ActiveAccentConverter Instance = new();
    public object? Convert(IList<object?> values, System.Type targetType, object? parameter, CultureInfo culture)
    {
        var isActive = values.Count > 0 && values[0] is bool b && b;
        var accent = values.Count > 1 && values[1] is IBrush br ? br : Brushes.Teal;
        return isActive ? accent : Brushes.Transparent;
    }
}

public sealed class ActiveForegroundConverter : IMultiValueConverter
{
    public static readonly ActiveForegroundConverter Instance = new();
    public object? Convert(IList<object?> values, System.Type targetType, object? parameter, CultureInfo culture)
    {
        var isActive = values.Count > 0 && values[0] is bool b && b;
        var accent = values.Count > 1 && values[1] is IBrush br ? br : Brushes.White;
        return isActive ? accent : new SolidColorBrush(Color.Parse("#D0D0DC"));
    }
}
```

- [ ] **Step 12.6: Run tests, verify pass**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~SidebarNavItemTests"
```

Expected: `Passed!  - 3/3`.

- [ ] **Step 12.7: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Controls/SidebarNavItem.axaml src/UI/AuraCore.UI.Avalonia/Views/Controls/SidebarNavItem.axaml.cs src/UI/AuraCore.UI.Avalonia/Views/Controls/NavItemConverters.cs tests/AuraCore.Tests.UI.Avalonia/Controls/SidebarNavItemTests.cs
git commit -m "feat(ui): add SidebarNavItem with active state + trailing chip"
```

---

## Task 13: SidebarSectionDivider

Horizontal lines with inline uppercase label: `── ADVANCED ──`. Vision Doc §6.

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Controls/SidebarSectionDivider.axaml`
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Controls/SidebarSectionDivider.axaml.cs`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Controls/SidebarSectionDividerTests.cs`

- [ ] **Step 13.1: Write failing test**

Create `tests/AuraCore.Tests.UI.Avalonia/Controls/SidebarSectionDividerTests.cs`:

```csharp
using Avalonia.Headless.XUnit;
using AuraCore.UI.Avalonia.Views.Controls;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Controls;

public class SidebarSectionDividerTests
{
    [AvaloniaFact]
    public void Divider_Defaults()
    {
        var divider = new SidebarSectionDivider();
        Assert.Equal(string.Empty, divider.Label);
    }

    [AvaloniaFact]
    public void Divider_AcceptsLabel()
    {
        var divider = new SidebarSectionDivider { Label = "ADVANCED" };
        Assert.Equal("ADVANCED", divider.Label);
    }

    [AvaloniaFact]
    public void Divider_RendersInWindow()
    {
        var divider = new SidebarSectionDivider { Label = "OVERVIEW" };
        using var window = AvaloniaTestBase.RenderInWindow(divider, 220, 20);
        Assert.True(divider.IsMeasureValid);
    }
}
```

- [ ] **Step 13.2: Run, verify fail**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~SidebarSectionDividerTests"
```

- [ ] **Step 13.3: Create code-behind**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/SidebarSectionDivider.axaml.cs`:

```csharp
using Avalonia;
using Avalonia.Controls;

namespace AuraCore.UI.Avalonia.Views.Controls;

public partial class SidebarSectionDivider : UserControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<SidebarSectionDivider, string>(nameof(Label), string.Empty);

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public SidebarSectionDivider() { InitializeComponent(); }
}
```

- [ ] **Step 13.4: Create XAML**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/SidebarSectionDivider.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="AuraCore.UI.Avalonia.Views.Controls.SidebarSectionDivider"
             x:Name="Root">
  <Grid ColumnDefinitions="*,Auto,*" Margin="8,14,8,6">
    <Rectangle Grid.Column="0" Height="1" Fill="{DynamicResource BorderSubtleBrush}"/>
    <TextBlock Grid.Column="1"
               Text="{Binding #Root.Label}"
               Foreground="{DynamicResource TextDisabledBrush}"
               FontSize="8"
               FontWeight="Bold"
               LetterSpacing="1.5"
               Margin="8,0"/>
    <Rectangle Grid.Column="2" Height="1" Fill="{DynamicResource BorderSubtleBrush}"/>
  </Grid>
</UserControl>
```

- [ ] **Step 13.5: Run, verify pass**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~SidebarSectionDividerTests"
```

Expected: `Passed!  - 3/3`.

- [ ] **Step 13.6: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Controls/SidebarSectionDivider.axaml src/UI/AuraCore.UI.Avalonia/Views/Controls/SidebarSectionDivider.axaml.cs tests/AuraCore.Tests.UI.Avalonia/Controls/SidebarSectionDividerTests.cs
git commit -m "feat(ui): add SidebarSectionDivider"
```

---

## Task 14: StatusChip

Small dot+label chip for status indicators (`● LIVE`, `✦ Cortex AI · ON`). 4 color variants. Vision Doc §8.

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Controls/StatusChip.axaml`
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Controls/StatusChip.axaml.cs`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Controls/StatusChipTests.cs`

- [ ] **Step 14.1: Failing test**

Create `tests/AuraCore.Tests.UI.Avalonia/Controls/StatusChipTests.cs`:

```csharp
using Avalonia.Headless.XUnit;
using AuraCore.UI.Avalonia.Views.Controls;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Controls;

public class StatusChipTests
{
    [AvaloniaFact]
    public void Chip_Defaults()
    {
        var chip = new StatusChip();
        Assert.Equal(string.Empty, chip.Label);
        Assert.True(chip.ShowDot);
    }

    [AvaloniaFact]
    public void Chip_AcceptsLabel()
    {
        var chip = new StatusChip { Label = "LIVE" };
        Assert.Equal("LIVE", chip.Label);
    }

    [AvaloniaFact]
    public void Chip_RendersInWindow()
    {
        var chip = new StatusChip { Label = "LIVE" };
        using var window = AvaloniaTestBase.RenderInWindow(chip, 80, 24);
        Assert.True(chip.IsMeasureValid);
    }
}
```

- [ ] **Step 14.2: Run, verify fail**

- [ ] **Step 14.3: Create code-behind**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/StatusChip.axaml.cs`:

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Controls;

public partial class StatusChip : UserControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<StatusChip, string>(nameof(Label), string.Empty);
    public static readonly StyledProperty<IBrush> AccentBrushProperty =
        AvaloniaProperty.Register<StatusChip, IBrush>(nameof(AccentBrush), Brushes.Teal);
    public static readonly StyledProperty<bool> ShowDotProperty =
        AvaloniaProperty.Register<StatusChip, bool>(nameof(ShowDot), true);
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<StatusChip, Geometry?>(nameof(Icon));

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public IBrush AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    public bool ShowDot { get => GetValue(ShowDotProperty); set => SetValue(ShowDotProperty, value); }
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public StatusChip() { InitializeComponent(); }
}
```

- [ ] **Step 14.4: Create XAML**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/StatusChip.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="AuraCore.UI.Avalonia.Views.Controls.StatusChip"
             x:Name="Root">
  <Border CornerRadius="12" Padding="10,4" BorderThickness="1">
    <Border.Background>
      <SolidColorBrush Color="{Binding #Root.AccentBrush.Color, FallbackValue=#00D4AA}" Opacity="0.15"/>
    </Border.Background>
    <Border.BorderBrush>
      <SolidColorBrush Color="{Binding #Root.AccentBrush.Color, FallbackValue=#00D4AA}" Opacity="0.2"/>
    </Border.BorderBrush>
    <StackPanel Orientation="Horizontal" Spacing="5" VerticalAlignment="Center">
      <Ellipse Width="6" Height="6"
               Fill="{Binding #Root.AccentBrush}"
               IsVisible="{Binding #Root.ShowDot}"/>
      <Path Data="{Binding #Root.Icon}"
            Fill="{Binding #Root.AccentBrush}"
            Width="11" Height="11"
            IsVisible="{Binding #Root.Icon, Converter={x:Static ObjectConverters.IsNotNull}}"/>
      <TextBlock Text="{Binding #Root.Label}"
                 Foreground="{Binding #Root.AccentBrush}"
                 FontSize="{DynamicResource FontSizeLabel}"
                 FontWeight="SemiBold"/>
    </StackPanel>
  </Border>
</UserControl>
```

- [ ] **Step 14.5: Run, verify pass**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~StatusChipTests"
```

Expected: `Passed!  - 3/3`.

- [ ] **Step 14.6: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Controls/StatusChip.axaml src/UI/AuraCore.UI.Avalonia/Views/Controls/StatusChip.axaml.cs tests/AuraCore.Tests.UI.Avalonia/Controls/StatusChipTests.cs
git commit -m "feat(ui): add StatusChip with dot/icon + label"
```

---

## Task 15: AuraToggle

Styled on/off toggle. Wraps Avalonia's `ToggleSwitch` with our theme. Vision Doc §8.

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Controls/AuraToggle.axaml`
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Controls/AuraToggle.axaml.cs`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Controls/AuraToggleTests.cs`

- [ ] **Step 15.1: Failing test**

Create `tests/AuraCore.Tests.UI.Avalonia/Controls/AuraToggleTests.cs`:

```csharp
using Avalonia.Headless.XUnit;
using AuraCore.UI.Avalonia.Views.Controls;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Controls;

public class AuraToggleTests
{
    [AvaloniaFact]
    public void Toggle_Defaults()
    {
        var t = new AuraToggle();
        Assert.False(t.IsOn);
    }

    [AvaloniaFact]
    public void Toggle_IsOnToggles()
    {
        var t = new AuraToggle();
        t.IsOn = true;
        Assert.True(t.IsOn);
    }

    [AvaloniaFact]
    public void Toggle_RendersInWindow()
    {
        var t = new AuraToggle();
        using var window = AvaloniaTestBase.RenderInWindow(t, 60, 30);
        Assert.True(t.IsMeasureValid);
    }
}
```

- [ ] **Step 15.2: Run, verify fail**

- [ ] **Step 15.3: Create code-behind**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/AuraToggle.axaml.cs`:

```csharp
using Avalonia;
using Avalonia.Controls;

namespace AuraCore.UI.Avalonia.Views.Controls;

public partial class AuraToggle : UserControl
{
    public static readonly StyledProperty<bool> IsOnProperty =
        AvaloniaProperty.Register<AuraToggle, bool>(nameof(IsOn), false, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public bool IsOn { get => GetValue(IsOnProperty); set => SetValue(IsOnProperty, value); }

    public AuraToggle() { InitializeComponent(); }
}
```

- [ ] **Step 15.4: Create XAML**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/AuraToggle.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="AuraCore.UI.Avalonia.Views.Controls.AuraToggle"
             x:Name="Root">
  <ToggleSwitch IsChecked="{Binding #Root.IsOn, Mode=TwoWay}"
                OnContent="" OffContent=""
                Padding="0" Margin="0"/>
</UserControl>
```

- [ ] **Step 15.5: Run, verify pass**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~AuraToggleTests"
```

Expected: `Passed!  - 3/3`.

- [ ] **Step 15.6: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Controls/AuraToggle.axaml src/UI/AuraCore.UI.Avalonia/Views/Controls/AuraToggle.axaml.cs tests/AuraCore.Tests.UI.Avalonia/Controls/AuraToggleTests.cs
git commit -m "feat(ui): add AuraToggle wrapper"
```

---

## Task 16: AccentBadge

Small uppercase chip: `[CORTEX]`, `[ADMIN]`, `[Experimental]`. Vision Doc §8.

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Controls/AccentBadge.axaml`
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Controls/AccentBadge.axaml.cs`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Controls/AccentBadgeTests.cs`

- [ ] **Step 16.1: Failing test**

Create `tests/AuraCore.Tests.UI.Avalonia/Controls/AccentBadgeTests.cs`:

```csharp
using Avalonia.Headless.XUnit;
using AuraCore.UI.Avalonia.Views.Controls;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Controls;

public class AccentBadgeTests
{
    [AvaloniaFact]
    public void Badge_Defaults()
    {
        var b = new AccentBadge();
        Assert.Equal(string.Empty, b.Label);
    }

    [AvaloniaFact]
    public void Badge_AcceptsLabel()
    {
        var b = new AccentBadge { Label = "CORTEX" };
        Assert.Equal("CORTEX", b.Label);
    }

    [AvaloniaFact]
    public void Badge_RendersInWindow()
    {
        var b = new AccentBadge { Label = "ADMIN" };
        using var window = AvaloniaTestBase.RenderInWindow(b, 60, 20);
        Assert.True(b.IsMeasureValid);
    }
}
```

- [ ] **Step 16.2: Run, verify fail**

- [ ] **Step 16.3: Create code-behind**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/AccentBadge.axaml.cs`:

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Controls;

public partial class AccentBadge : UserControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<AccentBadge, string>(nameof(Label), string.Empty);
    public static readonly StyledProperty<IBrush> AccentBrushProperty =
        AvaloniaProperty.Register<AccentBadge, IBrush>(nameof(AccentBrush), Brushes.Violet);

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public IBrush AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    public AccentBadge() { InitializeComponent(); }
}
```

- [ ] **Step 16.4: Create XAML**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/AccentBadge.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="AuraCore.UI.Avalonia.Views.Controls.AccentBadge"
             x:Name="Root">
  <Border CornerRadius="4" Padding="6,2">
    <Border.Background>
      <SolidColorBrush Color="{Binding #Root.AccentBrush.Color, FallbackValue=#8B5CF6}" Opacity="0.15"/>
    </Border.Background>
    <TextBlock Text="{Binding #Root.Label}"
               Foreground="{Binding #Root.AccentBrush}"
               FontSize="8"
               FontWeight="Bold"
               LetterSpacing="0.5"/>
  </Border>
</UserControl>
```

- [ ] **Step 16.5: Run, verify pass**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~AccentBadgeTests"
```

Expected: `Passed!  - 3/3`.

- [ ] **Step 16.6: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Controls/AccentBadge.axaml src/UI/AuraCore.UI.Avalonia/Views/Controls/AccentBadge.axaml.cs tests/AuraCore.Tests.UI.Avalonia/Controls/AccentBadgeTests.cs
git commit -m "feat(ui): add AccentBadge"
```

---

## Task 17: UserChip

Avatar (colored circle) + email + role badge. Sidebar top element. Vision Doc §8.

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Controls/UserChip.axaml`
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Controls/UserChip.axaml.cs`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Controls/UserChipTests.cs`

- [ ] **Step 17.1: Failing test**

Create `tests/AuraCore.Tests.UI.Avalonia/Controls/UserChipTests.cs`:

```csharp
using Avalonia.Headless.XUnit;
using AuraCore.UI.Avalonia.Views.Controls;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Controls;

public class UserChipTests
{
    [AvaloniaFact]
    public void Chip_Defaults()
    {
        var c = new UserChip();
        Assert.Equal(string.Empty, c.Email);
        Assert.Equal(string.Empty, c.Role);
    }

    [AvaloniaFact]
    public void Chip_Accepts_EmailAndRole()
    {
        var c = new UserChip { Email = "admin@aura.pro", Role = "ADMIN" };
        Assert.Equal("admin@aura.pro", c.Email);
        Assert.Equal("ADMIN", c.Role);
    }

    [AvaloniaFact]
    public void Chip_RendersInWindow()
    {
        var c = new UserChip { Email = "admin@aura.pro", Role = "ADMIN" };
        using var window = AvaloniaTestBase.RenderInWindow(c, 220, 40);
        Assert.True(c.IsMeasureValid);
    }
}
```

- [ ] **Step 17.2: Run, verify fail**

- [ ] **Step 17.3: Create code-behind**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/UserChip.axaml.cs`:

```csharp
using Avalonia;
using Avalonia.Controls;

namespace AuraCore.UI.Avalonia.Views.Controls;

public partial class UserChip : UserControl
{
    public static readonly StyledProperty<string> EmailProperty =
        AvaloniaProperty.Register<UserChip, string>(nameof(Email), string.Empty);
    public static readonly StyledProperty<string> RoleProperty =
        AvaloniaProperty.Register<UserChip, string>(nameof(Role), string.Empty);
    public static readonly StyledProperty<string> StatusTextProperty =
        AvaloniaProperty.Register<UserChip, string>(nameof(StatusText), "Signed in");
    public static readonly StyledProperty<string> AvatarInitialProperty =
        AvaloniaProperty.Register<UserChip, string>(nameof(AvatarInitial), "?");

    public string Email { get => GetValue(EmailProperty); set => SetValue(EmailProperty, value); }
    public string Role { get => GetValue(RoleProperty); set => SetValue(RoleProperty, value); }
    public string StatusText { get => GetValue(StatusTextProperty); set => SetValue(StatusTextProperty, value); }
    public string AvatarInitial { get => GetValue(AvatarInitialProperty); set => SetValue(AvatarInitialProperty, value); }

    public UserChip() { InitializeComponent(); }
}
```

- [ ] **Step 17.4: Create XAML**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/UserChip.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="using:AuraCore.UI.Avalonia.Views.Controls"
             x:Class="AuraCore.UI.Avalonia.Views.Controls.UserChip"
             x:Name="Root">
  <Grid ColumnDefinitions="Auto,*,Auto" Margin="8,6,8,6">
    <!-- Avatar -->
    <Border Width="26" Height="26" CornerRadius="13">
      <Border.Background>
        <LinearGradientBrush StartPoint="0%,0%" EndPoint="100%,100%">
          <GradientStop Offset="0" Color="#8B5CF6"/>
          <GradientStop Offset="1" Color="#EC4899"/>
        </LinearGradientBrush>
      </Border.Background>
      <TextBlock Text="{Binding #Root.AvatarInitial}"
                 HorizontalAlignment="Center"
                 VerticalAlignment="Center"
                 Foreground="White"
                 FontWeight="Bold"
                 FontSize="{DynamicResource FontSizeBodySmall}"/>
    </Border>
    <StackPanel Grid.Column="1" Margin="8,0,0,0" VerticalAlignment="Center">
      <TextBlock Text="{Binding #Root.Email}"
                 Foreground="{DynamicResource TextSecondaryBrush}"
                 FontSize="{DynamicResource FontSizeBodySmall}"
                 TextTrimming="CharacterEllipsis"/>
      <TextBlock Text="{Binding #Root.StatusText}"
                 Foreground="{DynamicResource AccentTealLightBrush}"
                 FontSize="8"/>
    </StackPanel>
    <local:AccentBadge Grid.Column="2" Label="{Binding #Root.Role}" VerticalAlignment="Center"
                       IsVisible="{Binding #Root.Role, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"/>
  </Grid>
</UserControl>
```

- [ ] **Step 17.5: Run, verify pass**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~UserChipTests"
```

Expected: `Passed!  - 3/3`.

- [ ] **Step 17.6: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Controls/UserChip.axaml src/UI/AuraCore.UI.Avalonia/Views/Controls/UserChip.axaml.cs tests/AuraCore.Tests.UI.Avalonia/Controls/UserChipTests.cs
git commit -m "feat(ui): add UserChip with gradient avatar + role badge"
```

---

## Task 18: AppLogoBadge

Logo square + product name + `PRO · CORTEX` tagline. Sidebar top branding. Vision Doc §8.

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Controls/AppLogoBadge.axaml`
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Controls/AppLogoBadge.axaml.cs`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Controls/AppLogoBadgeTests.cs`

- [ ] **Step 18.1: Failing test**

Create `tests/AuraCore.Tests.UI.Avalonia/Controls/AppLogoBadgeTests.cs`:

```csharp
using Avalonia.Headless.XUnit;
using AuraCore.UI.Avalonia.Views.Controls;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Controls;

public class AppLogoBadgeTests
{
    [AvaloniaFact]
    public void Logo_Defaults()
    {
        var l = new AppLogoBadge();
        Assert.Equal("AuraCore", l.ProductName);
        Assert.Equal("PRO • CORTEX", l.Tagline);
    }

    [AvaloniaFact]
    public void Logo_AcceptsValues()
    {
        var l = new AppLogoBadge { ProductName = "Other", Tagline = "V2" };
        Assert.Equal("Other", l.ProductName);
        Assert.Equal("V2", l.Tagline);
    }

    [AvaloniaFact]
    public void Logo_RendersInWindow()
    {
        var l = new AppLogoBadge();
        using var window = AvaloniaTestBase.RenderInWindow(l, 220, 50);
        Assert.True(l.IsMeasureValid);
    }
}
```

- [ ] **Step 18.2: Run, verify fail**

- [ ] **Step 18.3: Create code-behind**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/AppLogoBadge.axaml.cs`:

```csharp
using Avalonia;
using Avalonia.Controls;

namespace AuraCore.UI.Avalonia.Views.Controls;

public partial class AppLogoBadge : UserControl
{
    public static readonly StyledProperty<string> ProductNameProperty =
        AvaloniaProperty.Register<AppLogoBadge, string>(nameof(ProductName), "AuraCore");
    public static readonly StyledProperty<string> TaglineProperty =
        AvaloniaProperty.Register<AppLogoBadge, string>(nameof(Tagline), "PRO • CORTEX");

    public string ProductName { get => GetValue(ProductNameProperty); set => SetValue(ProductNameProperty, value); }
    public string Tagline { get => GetValue(TaglineProperty); set => SetValue(TaglineProperty, value); }

    public AppLogoBadge() { InitializeComponent(); }
}
```

- [ ] **Step 18.4: Create XAML**

Create `src/UI/AuraCore.UI.Avalonia/Views/Controls/AppLogoBadge.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="AuraCore.UI.Avalonia.Views.Controls.AppLogoBadge"
             x:Name="Root">
  <Grid ColumnDefinitions="Auto,*" Margin="8,6,8,16">
    <Border Width="28" Height="28" CornerRadius="7" BoxShadow="{DynamicResource GlowTeal}">
      <Border.Background>
        <LinearGradientBrush StartPoint="0%,0%" EndPoint="100%,100%">
          <GradientStop Offset="0" Color="#00D4AA"/>
          <GradientStop Offset="1" Color="#8B5CF6"/>
        </LinearGradientBrush>
      </Border.Background>
      <TextBlock Text="A"
                 HorizontalAlignment="Center"
                 VerticalAlignment="Center"
                 Foreground="White"
                 FontWeight="ExtraBold"
                 FontSize="13"/>
    </Border>
    <StackPanel Grid.Column="1" Margin="10,0,0,0" VerticalAlignment="Center">
      <TextBlock Text="{Binding #Root.ProductName}"
                 Foreground="{DynamicResource TextSecondaryBrush}"
                 FontSize="13" FontWeight="SemiBold"/>
      <TextBlock Text="{Binding #Root.Tagline}"
                 Foreground="{DynamicResource AccentTealBrush}"
                 FontSize="8" FontWeight="Bold"
                 LetterSpacing="1.5"/>
    </StackPanel>
  </Grid>
</UserControl>
```

- [ ] **Step 18.5: Run, verify pass**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~AppLogoBadgeTests"
```

Expected: `Passed!  - 3/3`.

- [ ] **Step 18.6: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Controls/AppLogoBadge.axaml src/UI/AuraCore.UI.Avalonia/Views/Controls/AppLogoBadge.axaml.cs tests/AuraCore.Tests.UI.Avalonia/Controls/AppLogoBadgeTests.cs
git commit -m "feat(ui): add AppLogoBadge sidebar branding"
```

---

## Task 19: Component Gallery Window (visual review harness)

Developer-only window that displays all 12 components at once for human eyeballing. Not shipped, lives under `Views/Dev/`. Openable via a keyboard shortcut or menu command we wire up in Phase 2.

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Dev/ComponentGalleryWindow.axaml`
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Dev/ComponentGalleryWindow.axaml.cs`

- [ ] **Step 19.1: Create code-behind**

Create `src/UI/AuraCore.UI.Avalonia/Views/Dev/ComponentGalleryWindow.axaml.cs`:

```csharp
using Avalonia.Controls;

namespace AuraCore.UI.Avalonia.Views.Dev;

public partial class ComponentGalleryWindow : Window
{
    public ComponentGalleryWindow() { InitializeComponent(); }
}
```

- [ ] **Step 19.2: Create the gallery XAML**

Create `src/UI/AuraCore.UI.Avalonia/Views/Dev/ComponentGalleryWindow.axaml`:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:controls="using:AuraCore.UI.Avalonia.Views.Controls"
        x:Class="AuraCore.UI.Avalonia.Views.Dev.ComponentGalleryWindow"
        Title="AuraCore Design System — Gallery"
        Width="1100" Height="800"
        Background="{DynamicResource BgDeepBrush}">
  <ScrollViewer Padding="24">
    <StackPanel Spacing="20">

      <TextBlock Text="Design System Gallery" FontSize="22" FontWeight="Bold"
                 Foreground="{DynamicResource TextPrimaryBrush}"/>
      <TextBlock Text="All 12 primitives rendered with sample data." FontSize="12"
                 Foreground="{DynamicResource TextMutedBrush}"/>

      <!-- AppLogoBadge + UserChip -->
      <TextBlock Text="Sidebar header primitives" FontWeight="SemiBold"
                 Foreground="{DynamicResource TextPrimaryBrush}"/>
      <StackPanel Orientation="Horizontal" Spacing="20">
        <Border Width="240" Background="{DynamicResource BgSidebarBrush}">
          <StackPanel>
            <controls:AppLogoBadge/>
            <controls:UserChip Email="admin@aura.pro" Role="ADMIN" AvatarInitial="A"/>
          </StackPanel>
        </Border>
      </StackPanel>

      <!-- Sidebar items -->
      <TextBlock Text="Sidebar navigation" FontWeight="SemiBold" Foreground="{DynamicResource TextPrimaryBrush}"/>
      <Border Width="240" Background="{DynamicResource BgSidebarBrush}" Padding="10">
        <StackPanel Spacing="2">
          <controls:SidebarNavItem Label="Dashboard" IsActive="True" Icon="{StaticResource IconDashboard}"/>
          <controls:SidebarNavItem Label="Optimize" Icon="{StaticResource IconZap}"/>
          <controls:SidebarNavItem Label="AI Features" TrailingChipText="CORTEX" Icon="{StaticResource IconStar}"
                                   AccentBrush="{DynamicResource AccentPurpleBrush}"/>
          <controls:SidebarSectionDivider Label="ADVANCED"/>
          <controls:SidebarNavItem Label="Registry (deep)" Icon="{StaticResource IconSettings}"/>
        </StackPanel>
      </Border>

      <!-- Gauges -->
      <TextBlock Text="Gauges" FontWeight="SemiBold" Foreground="{DynamicResource TextPrimaryBrush}"/>
      <UniformGrid Columns="5" Rows="1" HorizontalAlignment="Left" Width="900">
        <controls:Gauge Label="CPU" Value="14" SubLabel="%" Insight="Spike detected"
                        RingBrush="{DynamicResource AccentTealBrush}"
                        InsightBrush="{DynamicResource AccentAmberBrush}"
                        Icon="{StaticResource IconCpu}"/>
        <controls:Gauge Label="RAM" Value="41" SubLabel="/ 31.3 GB" Insight="Healthy"
                        RingBrush="{DynamicResource AccentPurpleBrush}"
                        InsightBrush="{DynamicResource AccentTealBrush}"
                        Icon="{StaticResource IconRam}"/>
        <controls:Gauge Label="GPU" Value="28" SubLabel="68°C" Insight="Radeon 780M"
                        RingBrush="{DynamicResource AccentPinkBrush}"
                        InsightBrush="{DynamicResource TextSecondaryBrush}"
                        Icon="{StaticResource IconGpu}"/>
        <controls:Gauge Label="DISK C:" Value="88" SubLabel="105 GB free" Insight="127 days"
                        RingBrush="{DynamicResource AccentAmberBrush}"
                        InsightBrush="{DynamicResource AccentPurpleBrush}"
                        Icon="{StaticResource IconHardDrive}"/>
        <controls:Gauge Label="HEALTH" Value="90" SubLabel="" Insight="Excellent"
                        RingBrush="{DynamicResource AccentTealBrush}"
                        InsightBrush="{DynamicResource AccentTealBrush}"
                        Icon="{StaticResource IconHeart}"/>
      </UniformGrid>

      <!-- Hero -->
      <TextBlock Text="Hero CTA" FontWeight="SemiBold" Foreground="{DynamicResource TextPrimaryBrush}"/>
      <controls:HeroCTA Width="500"
                        Kicker="CORTEX RECOMMENDS"
                        Title="Smart Optimize Now"
                        Body="RAM cleanup + 3 bloatware apps + gaming mode profile."
                        PrimaryButtonText="Optimize"
                        SecondaryButtonText="Review"/>

      <!-- GlassCard + InsightCard -->
      <TextBlock Text="Cards" FontWeight="SemiBold" Foreground="{DynamicResource TextPrimaryBrush}"/>
      <StackPanel Orientation="Horizontal" Spacing="10">
        <controls:GlassCard Width="260">
          <controls:GlassCard.CardContent>
            <TextBlock Text="This is a GlassCard with any content inside."
                       Foreground="{DynamicResource TextSecondaryBrush}"
                       TextWrapping="Wrap"/>
          </controls:GlassCard.CardContent>
        </controls:GlassCard>
        <controls:InsightCard Title="Cortex Insights" UpdatedAt="2m ago" Width="280"/>
      </StackPanel>

      <!-- Quick Actions -->
      <TextBlock Text="Quick Actions" FontWeight="SemiBold" Foreground="{DynamicResource TextPrimaryBrush}"/>
      <UniformGrid Columns="4" Rows="1" Width="700">
        <controls:QuickActionTile Title="Clean Junk" SubLabel="Temp, cache, logs"
                                  Icon="{StaticResource IconSparkles}"
                                  TintBrush="{DynamicResource AccentTealBrush}"/>
        <controls:QuickActionTile Title="Optimize RAM" SubLabel="Free memory"
                                  Icon="{StaticResource IconRotateCcw}"
                                  TintBrush="{DynamicResource AccentPurpleBrush}"/>
        <controls:QuickActionTile Title="Gaming Mode" SubLabel="Ready to game"
                                  Icon="{StaticResource IconGamepad}"
                                  TintBrush="{DynamicResource AccentAmberBrush}"/>
        <controls:QuickActionTile Title="Security Scan" SubLabel="Defender + firewall"
                                  Icon="{StaticResource IconShieldCheck}"
                                  TintBrush="{DynamicResource AccentTealBrush}"/>
      </UniformGrid>

      <!-- Chips + Badges + Toggle -->
      <TextBlock Text="Chips, Badges, Toggle" FontWeight="SemiBold" Foreground="{DynamicResource TextPrimaryBrush}"/>
      <StackPanel Orientation="Horizontal" Spacing="10">
        <controls:StatusChip Label="LIVE" AccentBrush="{DynamicResource AccentTealBrush}"/>
        <controls:StatusChip Label="Cortex AI · ON" AccentBrush="{DynamicResource AccentPurpleBrush}"
                             ShowDot="False" Icon="{StaticResource IconStar}"/>
        <controls:AccentBadge Label="CORTEX" AccentBrush="{DynamicResource AccentPurpleBrush}"/>
        <controls:AccentBadge Label="ADMIN" AccentBrush="{DynamicResource AccentPurpleBrush}"/>
        <controls:AccentBadge Label="DENEYSEL" AccentBrush="{DynamicResource AccentAmberBrush}"/>
        <controls:AuraToggle IsOn="True"/>
      </StackPanel>

    </StackPanel>
  </ScrollViewer>
</Window>
```

- [ ] **Step 19.3: Wire gallery into App so it can be opened (dev flag)**

To keep the gallery invisible in production but openable in dev, wire it to a keyboard shortcut. Open `src/UI/AuraCore.UI.Avalonia/App.axaml.cs` and find `OnFrameworkInitializationCompleted`. Find the main window creation (search for `MainWindow`). After `desktop.MainWindow = ...`, add:

```csharp
#if DEBUG
desktop.MainWindow.KeyDown += (s, e) =>
{
    if (e.Key == Avalonia.Input.Key.F12 && e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control))
    {
        new Views.Dev.ComponentGalleryWindow().Show();
    }
};
#endif
```

Also load the theme V2 and icons in the gallery context. Open `App.axaml` and add (inside the `Application.Styles` element, in addition to whatever's there):

```xml
<StyleInclude Source="avares://AuraCore.UI.Avalonia/Themes/AuraCoreThemeV2.axaml"/>
<StyleInclude Source="avares://AuraCore.UI.Avalonia/Themes/Icons.axaml"/>
```

**Important:** Add these AFTER the existing theme `StyleInclude` so V2 resources override old keys where they overlap. Existing pages may reference old resource keys — V2 introduces new keys alongside, so there should be no conflict. Phase 2 will replace the old theme entirely.

- [ ] **Step 19.4: Build & run**

Run:
```bash
dotnet build src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj
```

Expected: `Build succeeded. 0 Error(s)`.

Manually launch the app and press `Ctrl+F12` to open the gallery. Verify all 12 primitives render without crashes. (This is a visual smoke check — no automated test.)

- [ ] **Step 19.5: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Dev/ComponentGalleryWindow.axaml src/UI/AuraCore.UI.Avalonia/Views/Dev/ComponentGalleryWindow.axaml.cs src/UI/AuraCore.UI.Avalonia/App.axaml src/UI/AuraCore.UI.Avalonia/App.axaml.cs
git commit -m "feat(ui): add ComponentGalleryWindow for visual review + load theme v2"
```

---

## Task 20: Full test suite run + documentation touch-up

Final sanity check. Ensure everything passes, plan is committed, and docs point at the new test project.

- [ ] **Step 20.1: Run the full UI test suite**

Run:
```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --verbosity minimal
```

Expected: all ~60 tests pass (ThemeTokens + Icons + 12 × 3 control tests + smoke). If any fail, fix before proceeding.

- [ ] **Step 20.2: Run the existing test suite to confirm no regression**

Run:
```bash
dotnet build AuraCorePro.sln
dotnet test AuraCorePro.sln --verbosity minimal
```

Expected: all existing tests still pass. 0 regressions.

- [ ] **Step 20.3: Verify the desktop app still builds and runs**

Run:
```bash
dotnet run --project src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj
```

Expected: app opens as before (Dashboard with old theme). Phase 1 is scaffolding only — visual behavior is unchanged until Phase 2 swaps the theme.

Press `Ctrl+F12` to open the gallery window. Eyeball each component. Close the app.

- [ ] **Step 20.4: Commit the plan itself**

```bash
git add docs/superpowers/plans/2026-04-14-phase1-design-system.md
git commit -m "docs: add phase 1 implementation plan"
```

- [ ] **Step 20.5: Final summary commit (empty, for the phase milestone)**

```bash
git commit --allow-empty -m "milestone: Phase 1 Design System foundation complete"
```

---

## Phase 1 Acceptance Criteria

Before declaring Phase 1 done:

- [ ] `AuraCoreThemeV2.axaml` contains every token from Vision Doc §5
- [ ] `Icons.axaml` has 30 Lucide icons, all resolvable
- [ ] All 12 primitives exist under `Views/Controls/` with XAML + code-behind
- [ ] Each primitive has 3 xUnit tests (defaults, property, renders)
- [ ] `ComponentGalleryWindow` displays every primitive with sample data
- [ ] `dotnet test` passes for the new UI test project (~60 tests)
- [ ] Existing tests still pass (0 regressions)
- [ ] Desktop app still launches and renders the current Dashboard unchanged
- [ ] `Ctrl+F12` in the running app opens the gallery
- [ ] 19 commits land on the feature branch (one per task + plan + milestone)

## Phase 2 Entry Conditions

Phase 2 (Sidebar + Dashboard) can begin once:
- All Phase 1 acceptance criteria above pass
- Gallery has been eyeballed by the user and any visual issues noted
- A retrospective against the Vision Document has confirmed no pivots needed
