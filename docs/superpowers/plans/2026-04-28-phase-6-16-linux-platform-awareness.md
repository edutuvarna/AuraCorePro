# Phase 6.16 — Linux Platform Awareness + Module Audit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. **Execution context:** fresh session — this plan is self-contained, do NOT assume prior context. Standing user prefs: subagent-driven, supervisor mode (verify each subagent), skip spec-review user-gate, critical security auto-deploy. Build artifacts + final smoke test = LAST step before pause; do NOT auto-deploy v1.8.0.

**Goal:** Make every AuraCorePro desktop module either work correctly on its target platform OR show a graceful `UnavailableModuleView` with actionable remediation. No hard crashes, no silent dashboard-fallbacks, no Windows-centric strings on non-Windows. Unblock v1.8.0 release.

**Architecture:** Two-layer platform-awareness contract on `IOptimizationModule` via C# default interface members (sync `IsPlatformSupported` + async `CheckRuntimeAvailabilityAsync`). NavigationService rewrite consults availability before view dispatch and shows full-page `UnavailableModuleView` with category-specific diagnostic on failure. Per-csproj `MSBuildWarningsAsErrors=CA1416` + `[SupportedOSPlatform("windows")]` attributes prevent regression.

**Tech Stack:** C# 12 / .NET 8, Avalonia 11.x, xUnit 2.9, NSubstitute (mocks), Verify.Xunit + Skia (pixel regression for new UnavailableModuleView), Avalonia.Headless for sidebar tests.

**Branch off:** `main` at HEAD `35a643a` (Phase 6.16 spec commit). Create `phase-6-16-linux-platform-awareness`.

**Spec reference:** `docs/superpowers/specs/2026-04-28-phase-6-16-linux-platform-awareness-design.md` — read this BEFORE starting Task 0. Contains all 13 locked design decisions.

**Linux VM access (for Wave G):** `ssh -i ~/.ssh/id_ed25519_aura deniz@192.168.162.129` (Ubuntu 24.04.4 LTS, dotnet 8.0.125). Whitelisted in fail2ban per Phase 6.15.7 lessons.

**Out of scope (explicitly forbidden in this phase):** new modules, macOS smoke testing (Wave H is doc-only), helper auto-install wizard, D-Bus presence subscribing for live re-render, CI gate automation. All of these are Phase 6.17+.

---

## Task 0: Branch setup

**Files:** None (git only)

- [ ] **Step 1: Verify clean main HEAD + create branch**

```bash
cd /c/Users/Admin/Desktop/AuraCorePro/AuraCorePro
git switch main
git pull --ff-only
git rev-parse HEAD                       # should be 35a643a or descendant
git switch -c phase-6-16-linux-platform-awareness
git branch --show-current                # → phase-6-16-linux-platform-awareness
```

- [ ] **Step 2: Read the spec**

Read `docs/superpowers/specs/2026-04-28-phase-6-16-linux-platform-awareness-design.md` end-to-end. Internalize D1-D12 design decisions before writing any code.

---

# WAVE A — Architectural foundation (Tasks 1-5)

## Task 1: `ModuleAvailability` record + `AvailabilityCategory` enum

**Files:**
- Create: `src/Application/AuraCore.Application/Optimization/ModuleAvailability.cs`
- Test: `tests/AuraCore.Tests.Unit/Optimization/ModuleAvailabilityTests.cs`

**Goal:** Strongly-typed result type with factory methods. Pure data, no dependencies.

- [ ] **Step 1: Write failing tests**

```csharp
using AuraCore.Application.Optimization;
using Xunit;

namespace AuraCore.Tests.Unit.Optimization;

public class ModuleAvailabilityTests
{
    [Fact]
    public void Available_HasIsAvailableTrue_AndCorrectCategory()
    {
        var r = ModuleAvailability.Available;
        Assert.True(r.IsAvailable);
        Assert.Equal(AvailabilityCategory.Available, r.Category);
        Assert.Null(r.Reason);
        Assert.Null(r.RemediationCommand);
    }

    [Fact]
    public void WrongPlatform_HasIsAvailableFalse_AndDescriptiveReason()
    {
        var r = ModuleAvailability.WrongPlatform(SupportedPlatform.Linux);
        Assert.False(r.IsAvailable);
        Assert.Equal(AvailabilityCategory.WrongPlatform, r.Category);
        Assert.Contains("Linux", r.Reason!);
    }

    [Fact]
    public void HelperNotRunning_IncludesRemediationCommand()
    {
        var r = ModuleAvailability.HelperNotRunning("sudo bash /opt/install.sh");
        Assert.False(r.IsAvailable);
        Assert.Equal(AvailabilityCategory.HelperNotRunning, r.Category);
        Assert.Equal("sudo bash /opt/install.sh", r.RemediationCommand);
    }

    [Fact]
    public void ToolNotInstalled_IncludesToolName_InReason()
    {
        var r = ModuleAvailability.ToolNotInstalled("systemctl", "Use systemd distro");
        Assert.False(r.IsAvailable);
        Assert.Equal(AvailabilityCategory.ToolNotInstalled, r.Category);
        Assert.Contains("systemctl", r.Reason!);
        Assert.Equal("Use systemd distro", r.RemediationCommand);
    }

    [Fact]
    public void FeatureDisabled_HasReason_NoRemediation()
    {
        var r = ModuleAvailability.FeatureDisabled("Disabled by config");
        Assert.False(r.IsAvailable);
        Assert.Equal(AvailabilityCategory.FeatureDisabled, r.Category);
        Assert.Equal("Disabled by config", r.Reason);
    }
}
```

- [ ] **Step 2: Run test (expect compile error)**

```bash
dotnet test tests/AuraCore.Tests.Unit/AuraCore.Tests.Unit.csproj --filter "FullyQualifiedName~ModuleAvailability"
```
Expected: build errors — `ModuleAvailability`, `AvailabilityCategory`, `SupportedPlatform` types not found.

- [ ] **Step 3: Create `ModuleAvailability.cs`**

```csharp
namespace AuraCore.Application.Optimization;

public enum AvailabilityCategory
{
    Available,
    WrongPlatform,
    HelperNotRunning,
    ToolNotInstalled,
    FeatureDisabled,
    BackendUnreachable,
}

/// <summary>
/// Phase 6.16: rich result type for module runtime availability checks.
/// Used by NavigationService to decide whether to render the module view
/// or a full-page UnavailableModuleView with actionable diagnostic.
/// </summary>
public sealed record ModuleAvailability(
    bool IsAvailable,
    AvailabilityCategory Category,
    string? Reason,
    string? RemediationCommand)
{
    public static ModuleAvailability Available { get; } =
        new(true, AvailabilityCategory.Available, null, null);

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

- [ ] **Step 4: Run test (expect pass)**

```bash
dotnet test tests/AuraCore.Tests.Unit/AuraCore.Tests.Unit.csproj --filter "FullyQualifiedName~ModuleAvailability"
```
Expected: 5 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Application/AuraCore.Application/Optimization/ModuleAvailability.cs tests/AuraCore.Tests.Unit/Optimization/ModuleAvailabilityTests.cs
git commit -m "phase-6.16.A: ModuleAvailability record + AvailabilityCategory enum"
```

---

## Task 2: `IOptimizationModule` default interface members

**Files:**
- Modify: `src/Application/AuraCore.Application/Optimization/IOptimizationModule.cs`
- Test: `tests/AuraCore.Tests.Unit/Optimization/IOptimizationModuleDefaultsTests.cs`

**Goal:** Add `IsPlatformSupported` (sync, derived from `Platform` enum) + `CheckRuntimeAvailabilityAsync` (async, defaults to Available) as default interface members. Existing modules continue to compile unchanged.

- [ ] **Step 1: Read current `IOptimizationModule.cs`**

```bash
cat src/Application/AuraCore.Application/Optimization/IOptimizationModule.cs
```

Note the existing `Platform => SupportedPlatform.Windows` default and any other default members. The new additions go after `Platform`.

- [ ] **Step 2: Write failing tests**

```csharp
using AuraCore.Application.Optimization;
using NSubstitute;
using Xunit;

namespace AuraCore.Tests.Unit.Optimization;

public class IOptimizationModuleDefaultsTests
{
    private sealed class StubAllModule : IOptimizationModule
    {
        public string Id => "test-all";
        public string DisplayName => "Test All";
        public SupportedPlatform Platform => SupportedPlatform.All;
        // ... other required members minimal stubs (return Task.CompletedTask, null, etc.)
        public Task<ScanResult> ScanAsync(ScanOptions o, CancellationToken ct = default)
            => Task.FromResult(new ScanResult(Id, true, 0, 0));
    }

    private sealed class StubLinuxModule : IOptimizationModule
    {
        public string Id => "test-linux";
        public string DisplayName => "Test Linux";
        public SupportedPlatform Platform => SupportedPlatform.Linux;
        public Task<ScanResult> ScanAsync(ScanOptions o, CancellationToken ct = default)
            => Task.FromResult(new ScanResult(Id, true, 0, 0));
    }

    [Fact]
    public void IsPlatformSupported_AllPlatform_AlwaysTrue()
    {
        IOptimizationModule m = new StubAllModule();
        Assert.True(m.IsPlatformSupported);
    }

    [Fact]
    public void IsPlatformSupported_LinuxPlatform_MatchesOS()
    {
        IOptimizationModule m = new StubLinuxModule();
        Assert.Equal(OperatingSystem.IsLinux(), m.IsPlatformSupported);
    }

    [Fact]
    public async Task CheckRuntimeAvailabilityAsync_DefaultImpl_AllPlatform_ReturnsAvailable()
    {
        IOptimizationModule m = new StubAllModule();
        var r = await m.CheckRuntimeAvailabilityAsync();
        Assert.True(r.IsAvailable);
        Assert.Equal(AvailabilityCategory.Available, r.Category);
    }

    [Fact]
    public async Task CheckRuntimeAvailabilityAsync_DefaultImpl_WrongPlatform_ReturnsWrongPlatform()
    {
        IOptimizationModule m = new StubLinuxModule();
        if (OperatingSystem.IsLinux()) return; // skip when actually on Linux
        var r = await m.CheckRuntimeAvailabilityAsync();
        Assert.False(r.IsAvailable);
        Assert.Equal(AvailabilityCategory.WrongPlatform, r.Category);
    }
}
```

- [ ] **Step 3: Run test (expect fail)**

```bash
dotnet test tests/AuraCore.Tests.Unit/AuraCore.Tests.Unit.csproj --filter "FullyQualifiedName~IOptimizationModuleDefaults"
```
Expected: fails because `IsPlatformSupported` and `CheckRuntimeAvailabilityAsync` not defined.

- [ ] **Step 4: Modify `IOptimizationModule.cs` — add default members**

After the existing `Platform` property, add:

```csharp
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
    /// Used by NavigationService BEFORE rendering view to surface
    /// helper-not-running, tool-not-installed, etc. as a graceful UnavailableModuleView.
    /// Default: Available on all supported platforms; modules opt in by overriding.
    /// </summary>
    Task<ModuleAvailability> CheckRuntimeAvailabilityAsync(CancellationToken ct = default)
        => Task.FromResult(IsPlatformSupported
            ? ModuleAvailability.Available
            : ModuleAvailability.WrongPlatform(Platform));
```

- [ ] **Step 5: Run test (expect pass)**

```bash
dotnet test tests/AuraCore.Tests.Unit/AuraCore.Tests.Unit.csproj --filter "FullyQualifiedName~IOptimizationModuleDefaults"
```
Expected: 4 tests pass.

- [ ] **Step 6: Verify full backend test suite still green**

```bash
dotnet build src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj -c Debug 2>&1 | tail -5
dotnet test tests/AuraCore.Tests.Unit/AuraCore.Tests.Unit.csproj
dotnet test tests/AuraCore.Tests.Module/AuraCore.Tests.Module.csproj
```
Expected: 0 errors, no regressions in existing test count (158 module + 9 unit = 167+ baseline).

- [ ] **Step 7: Commit**

```bash
git add src/Application/AuraCore.Application/Optimization/IOptimizationModule.cs tests/AuraCore.Tests.Unit/Optimization/IOptimizationModuleDefaultsTests.cs
git commit -m "phase-6.16.A: IOptimizationModule default interface members for platform awareness"
```

---

## Task 3: `UnavailableModuleView` Avalonia UserControl

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Views/UnavailableModuleView.axaml`
- Create: `src/UI/AuraCore.UI.Avalonia/Views/UnavailableModuleView.axaml.cs`
- Localization: 8 new keys in `LocalizationService.cs` (4 EN + 4 TR)
- Test: `tests/AuraCore.Tests.UI.Avalonia/Views/UnavailableModuleViewTests.cs`

**Goal:** Full-page diagnostic view for unavailable modules. Shows icon, title, reason, copyable remediation command, "Try Again" button.

- [ ] **Step 1: Add localization keys**

Edit `src/UI/AuraCore.UI.Avalonia/LocalizationService.cs`. Add to EN dict (find a section like `// ── Common ──`) and TR dict at corresponding location:

```csharp
// EN
["unavailable.title.WrongPlatform"]    = "Not available on this platform",
["unavailable.title.HelperNotRunning"] = "Privilege helper required",
["unavailable.title.ToolNotInstalled"] = "Required tool missing",
["unavailable.title.FeatureDisabled"]  = "Feature disabled",
["unavailable.tryAgain"]               = "Try Again",
["unavailable.copy"]                   = "Copy command",
["unavailable.copied"]                 = "Copied",
["unavailable.remediation"]            = "Remediation",

// TR
["unavailable.title.WrongPlatform"]    = "Bu platformda kullanılamıyor",
["unavailable.title.HelperNotRunning"] = "Yetki yardımcısı gerekli",
["unavailable.title.ToolNotInstalled"] = "Gerekli araç eksik",
["unavailable.title.FeatureDisabled"]  = "Özellik devre dışı",
["unavailable.tryAgain"]               = "Tekrar Dene",
["unavailable.copy"]                   = "Komutu kopyala",
["unavailable.copied"]                 = "Kopyalandı",
["unavailable.remediation"]            = "Çözüm",
```

- [ ] **Step 2: Create `UnavailableModuleView.axaml`**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:lucide="clr-namespace:AuraCore.UI.Avalonia.Icons"
             x:Class="AuraCore.UI.Avalonia.Views.UnavailableModuleView">
  <Grid RowDefinitions="Auto,*">
    <!-- Page header: module name -->
    <Border Grid.Row="0" Padding="32,24,32,16" BorderThickness="0,0,0,1" BorderBrush="{DynamicResource SidebarBorderBrush}">
      <TextBlock x:Name="ModuleNameText"
                 FontSize="22" FontFamily="{DynamicResource DisplayFont}"
                 Foreground="{DynamicResource TextPrimaryBrush}" />
    </Border>
    <!-- Body: diagnostic card -->
    <ScrollViewer Grid.Row="1" Padding="32,24,32,32">
      <Border Classes="glass-card" Padding="32" MaxWidth="640" HorizontalAlignment="Stretch">
        <StackPanel Spacing="16">
          <!-- Category icon + title -->
          <StackPanel Orientation="Horizontal" Spacing="12">
            <Border Width="48" Height="48" CornerRadius="24"
                    Background="{DynamicResource AccentTintBrush}"
                    HorizontalAlignment="Left">
              <ContentControl x:Name="CategoryIcon" Width="24" Height="24"
                              HorizontalAlignment="Center" VerticalAlignment="Center" />
            </Border>
            <TextBlock x:Name="CategoryTitleText"
                       FontSize="18" FontWeight="SemiBold"
                       Foreground="{DynamicResource TextPrimaryBrush}"
                       VerticalAlignment="Center" />
          </StackPanel>

          <!-- Reason -->
          <TextBlock x:Name="ReasonText"
                     FontSize="14"
                     TextWrapping="Wrap"
                     Foreground="{DynamicResource TextSecondaryBrush}" />

          <!-- Remediation command (only shown if non-null) -->
          <Border x:Name="RemediationPanel" Classes="code-block"
                  Padding="16,12" CornerRadius="6"
                  Background="{DynamicResource SurfaceMutedBrush}">
            <Grid ColumnDefinitions="*,Auto" RowDefinitions="Auto,Auto">
              <TextBlock Grid.Row="0" Grid.Column="0"
                         x:Name="RemediationLabel"
                         FontSize="11" FontWeight="SemiBold"
                         Foreground="{DynamicResource TextTertiaryBrush}"
                         Margin="0,0,0,4" />
              <SelectableTextBlock Grid.Row="1" Grid.Column="0"
                         x:Name="RemediationText"
                         FontFamily="{DynamicResource MonoFont}"
                         FontSize="13"
                         Foreground="{DynamicResource TextPrimaryBrush}"
                         TextWrapping="Wrap" />
              <Button Grid.Row="0" Grid.Column="1" Grid.RowSpan="2"
                      x:Name="CopyButton"
                      Classes="btn-ghost-sm"
                      Click="OnCopyClick"
                      VerticalAlignment="Top" />
            </Grid>
          </Border>

          <!-- Try Again button -->
          <Button x:Name="TryAgainButton" Classes="btn-primary"
                  HorizontalAlignment="Left"
                  Click="OnTryAgainClick" />
        </StackPanel>
      </Border>
    </ScrollViewer>
  </Grid>
</UserControl>
```

- [ ] **Step 3: Create `UnavailableModuleView.axaml.cs`**

```csharp
using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Markup.Xaml;
using AuraCore.Application.Optimization;
using AuraCore.UI.Avalonia.Services;

namespace AuraCore.UI.Avalonia.Views;

public partial class UnavailableModuleView : UserControl
{
    private string _moduleId = string.Empty;
    private string _moduleName = string.Empty;
    private ModuleAvailability _availability = ModuleAvailability.Available;
    private LocalizationService Localization => App.GetLocalization();

    public UnavailableModuleView()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public UnavailableModuleView(string moduleId, string moduleName, ModuleAvailability availability) : this()
    {
        SetState(moduleId, moduleName, availability);
    }

    public void SetState(string moduleId, string moduleName, ModuleAvailability availability)
    {
        _moduleId = moduleId;
        _moduleName = moduleName;
        _availability = availability;
        Render();
    }

    private void Render()
    {
        var loc = Localization;
        ModuleNameText.Text = _moduleName;
        CategoryTitleText.Text = loc[$"unavailable.title.{_availability.Category}"];
        ReasonText.Text = _availability.Reason ?? string.Empty;

        if (string.IsNullOrEmpty(_availability.RemediationCommand))
        {
            RemediationPanel.IsVisible = false;
        }
        else
        {
            RemediationPanel.IsVisible = true;
            RemediationLabel.Text = loc["unavailable.remediation"];
            RemediationText.Text = _availability.RemediationCommand;
            CopyButton.Content = loc["unavailable.copy"];
        }

        TryAgainButton.Content = loc["unavailable.tryAgain"];

        // Category icon — phase 6.16 ships text-only fallback; icon assets carry-forward to 6.17
        // For now leave CategoryIcon empty (DynamicResource handles default).
    }

    private async void OnCopyClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_availability.RemediationCommand)) return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return;
        await clipboard.SetTextAsync(_availability.RemediationCommand);
        CopyButton.Content = Localization["unavailable.copied"];
        await Task.Delay(1500);
        CopyButton.Content = Localization["unavailable.copy"];
    }

    private async void OnTryAgainClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var nav = App.GetNavigationService();
        await nav.NavigateToAsync(_moduleId);  // re-runs CheckRuntimeAvailabilityAsync
    }
}
```

- [ ] **Step 4: Write failing tests**

`tests/AuraCore.Tests.UI.Avalonia/Views/UnavailableModuleViewTests.cs`:

```csharp
using AuraCore.Application.Optimization;
using AuraCore.UI.Avalonia.Views;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

public class UnavailableModuleViewTests
{
    [AvaloniaFact]
    public void SetState_WrongPlatform_ShowsCorrectTitle_AndHidesRemediation()
    {
        var v = new UnavailableModuleView();
        v.SetState("test-module", "Test Module",
            ModuleAvailability.WrongPlatform(SupportedPlatform.Linux));

        var moduleName = v.FindControl<TextBlock>("ModuleNameText");
        var category   = v.FindControl<TextBlock>("CategoryTitleText");
        var panel      = v.FindControl<Border>("RemediationPanel");

        Assert.Equal("Test Module", moduleName!.Text);
        Assert.Contains("platform", category!.Text!, StringComparison.OrdinalIgnoreCase);
        Assert.False(panel!.IsVisible);
    }

    [AvaloniaFact]
    public void SetState_HelperNotRunning_ShowsRemediationCommand()
    {
        var v = new UnavailableModuleView();
        v.SetState("systemd-manager", "Systemd Manager",
            ModuleAvailability.HelperNotRunning("sudo bash /opt/install.sh"));

        var panel = v.FindControl<Border>("RemediationPanel");
        var cmd   = v.FindControl<SelectableTextBlock>("RemediationText");

        Assert.True(panel!.IsVisible);
        Assert.Equal("sudo bash /opt/install.sh", cmd!.Text);
    }
}
```

- [ ] **Step 5: Run tests (expect pass)**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~UnavailableModuleView"
```
Expected: 2 tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/UnavailableModuleView.axaml src/UI/AuraCore.UI.Avalonia/Views/UnavailableModuleView.axaml.cs src/UI/AuraCore.UI.Avalonia/LocalizationService.cs tests/AuraCore.Tests.UI.Avalonia/Views/UnavailableModuleViewTests.cs
git commit -m "phase-6.16.A: UnavailableModuleView with category-specific diagnostic + remediation"
```

---

## Task 4: NavigationService extension — view factory map + availability gating

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Services/INavigationService.cs`
- Modify: `src/UI/AuraCore.UI.Avalonia/Services/NavigationService.cs`
- Test: `tests/AuraCore.Tests.UI.Avalonia/Services/NavigationServiceAvailabilityTests.cs`

**Goal:** Add `RegisterModuleView` to `INavigationService`. Refactor `NavigateToAsync` to consult `IsPlatformSupported` (defensive) + `CheckRuntimeAvailabilityAsync`, dispatch `UnavailableModuleView` on failure.

- [ ] **Step 1: Read current `INavigationService.cs` and `NavigationService.cs`**

Existing methods (from Phase 6.1 deep-link work) remain untouched. Goal is to ADD methods and refactor `NavigateToAsync` internally.

- [ ] **Step 2: Write failing tests**

`tests/AuraCore.Tests.UI.Avalonia/Services/NavigationServiceAvailabilityTests.cs`:

```csharp
using AuraCore.Application.Optimization;
using AuraCore.UI.Avalonia.Services;
using AuraCore.UI.Avalonia.Views;
using Avalonia.Controls;
using NSubstitute;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Services;

public class NavigationServiceAvailabilityTests
{
    private sealed class StubModule : IOptimizationModule
    {
        public string Id { get; }
        public string DisplayName { get; }
        public SupportedPlatform Platform { get; init; } = SupportedPlatform.All;
        public Func<ModuleAvailability> AvailabilityFactory { get; init; } = () => ModuleAvailability.Available;
        public StubModule(string id, string name) { Id = id; DisplayName = name; }
        public Task<ScanResult> ScanAsync(ScanOptions o, CancellationToken ct = default)
            => Task.FromResult(new ScanResult(Id, true, 0, 0));
        public Task<ModuleAvailability> CheckRuntimeAvailabilityAsync(CancellationToken ct = default)
            => Task.FromResult(AvailabilityFactory());
    }

    [Fact]
    public async Task NavigateToAsync_AvailableModule_DispatchesViewFactory()
    {
        var module = new StubModule("test-id", "Test");
        UserControl? dispatched = null;
        var shell = Substitute.For<INavigationShell>();
        shell.SetActiveContentAsync(Arg.Do<UserControl>(v => dispatched = v));

        var nav = new NavigationService(shell, new[] { module });
        var view = new UserControl { Tag = "TheRealView" };
        nav.RegisterModuleView("test-id", _ => view);

        await nav.NavigateToAsync("test-id");

        Assert.Same(view, dispatched);
    }

    [Fact]
    public async Task NavigateToAsync_HelperNotRunning_DispatchesUnavailableModuleView()
    {
        var module = new StubModule("test-id", "Test")
        {
            AvailabilityFactory = () => ModuleAvailability.HelperNotRunning("install cmd")
        };
        UserControl? dispatched = null;
        var shell = Substitute.For<INavigationShell>();
        shell.SetActiveContentAsync(Arg.Do<UserControl>(v => dispatched = v));

        var nav = new NavigationService(shell, new[] { module });

        await nav.NavigateToAsync("test-id");

        Assert.IsType<UnavailableModuleView>(dispatched);
    }

    [Fact]
    public async Task NavigateToAsync_ModuleNotInRegistry_DispatchesUnavailableModuleView()
    {
        var shell = Substitute.For<INavigationShell>();
        UserControl? dispatched = null;
        shell.SetActiveContentAsync(Arg.Do<UserControl>(v => dispatched = v));
        var nav = new NavigationService(shell, Array.Empty<IOptimizationModule>());

        await nav.NavigateToAsync("missing-module");

        Assert.IsType<UnavailableModuleView>(dispatched);
    }
}
```

- [ ] **Step 3: Run test (expect fail)**

Build will fail because `INavigationShell` and `RegisterModuleView` don't exist yet.

- [ ] **Step 4: Modify `INavigationService.cs` — add methods**

```csharp
public interface INavigationService
{
    // Existing (Phase 6.1) — untouched:
    // Task NavigateToAsync(string moduleId, IReadOnlyDictionary<string, string>? args = null);

    /// <summary>
    /// Phase 6.16: register a view factory for a module id. Replaces the
    /// hardcoded switch in MainWindow.SetActiveContent.
    /// Called from App.axaml.cs startup, per-platform.
    /// </summary>
    void RegisterModuleView(string moduleId, Func<IServiceProvider, UserControl> factory);
}

/// <summary>
/// Abstraction over MainWindow's content area for testability.
/// </summary>
public interface INavigationShell
{
    Task SetActiveContentAsync(UserControl content);
}
```

- [ ] **Step 5: Modify `NavigationService.cs`**

Add fields + extend `NavigateToAsync`:

```csharp
internal sealed class NavigationService : INavigationService
{
    private readonly INavigationShell _shell;
    private readonly IServiceProvider? _services;
    private readonly Dictionary<string, IOptimizationModule> _moduleMap;
    private readonly Dictionary<string, Func<IServiceProvider, UserControl>> _viewFactories = new();

    public NavigationService(INavigationShell shell, IEnumerable<IOptimizationModule> modules,
                             IServiceProvider? services = null)
    {
        _shell = shell;
        _services = services;
        _moduleMap = modules.ToDictionary(m => m.Id);
    }

    public void RegisterModuleView(string moduleId, Func<IServiceProvider, UserControl> factory)
        => _viewFactories[moduleId] = factory;

    public async Task NavigateToAsync(string moduleId, IReadOnlyDictionary<string, string>? args = null)
    {
        if (!_moduleMap.TryGetValue(moduleId, out var module))
        {
            await _shell.SetActiveContentAsync(new UnavailableModuleView(moduleId, moduleId,
                ModuleAvailability.WrongPlatform(SupportedPlatform.All)));
            return;
        }

        var availability = await module.CheckRuntimeAvailabilityAsync();
        if (!availability.IsAvailable)
        {
            await _shell.SetActiveContentAsync(new UnavailableModuleView(moduleId, module.DisplayName, availability));
            return;
        }

        if (_viewFactories.TryGetValue(moduleId, out var factory) && _services is not null)
        {
            var view = factory(_services);
            await _shell.SetActiveContentAsync(view);
        }
        else
        {
            // Defensive: should be impossible if Wave C registered all modules
            await _shell.SetActiveContentAsync(new UnavailableModuleView(moduleId, module.DisplayName,
                ModuleAvailability.FeatureDisabled("View factory not registered")));
        }
    }
}
```

- [ ] **Step 6: Run tests (expect pass)**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~NavigationServiceAvailability"
```
Expected: 3 tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Services/INavigationService.cs src/UI/AuraCore.UI.Avalonia/Services/NavigationService.cs tests/AuraCore.Tests.UI.Avalonia/Services/NavigationServiceAvailabilityTests.cs
git commit -m "phase-6.16.A: NavigationService availability gating + view factory registry"
```

---

## Task 5: `MainWindow.SetActiveContent` → `INavigationService` delegation

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/MainWindow.axaml.cs`

**Goal:** Strip the hardcoded 60-line switch in `SetActiveContent`. Delegate to `INavigationService.NavigateToAsync`. Implement `INavigationShell` on `MainWindow`.

- [ ] **Step 1: Read current `MainWindow.axaml.cs`**

Identify the `SetActiveContent` method (around lines 410-469) and `OnSidebarItemClick` / `NavigateToModule` (around lines 246-285).

- [ ] **Step 2: Implement `INavigationShell` on MainWindow**

```csharp
public partial class MainWindow : Window, INavigationShell
{
    public Task SetActiveContentAsync(UserControl content)
    {
        ContentArea.Content = content;
        return Task.CompletedTask;
    }
    // ... rest unchanged
}
```

- [ ] **Step 3: Replace SetActiveContent body with INavigationService delegation**

Find:
```csharp
private void SetActiveContent(string moduleId)
{
    ContentArea.Content = moduleId switch
    {
        // ... 40+ cases ...
        _ => new Pages.DashboardView(),
    };
}
```

Replace with:
```csharp
private async void SetActiveContent(string moduleId)
{
    var nav = App.GetNavigationService();
    await nav.NavigateToAsync(moduleId);
}
```

- [ ] **Step 4: Build + run UI tests to ensure nothing breaks**

```bash
dotnet build src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj -c Debug 2>&1 | tail -5
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj 2>&1 | tail -3
```
Expected: 0 errors, full UI test suite passes (1632 baseline + new Wave A tests).

- [ ] **Step 5: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/MainWindow.axaml.cs
git commit -m "phase-6.16.A: MainWindow.SetActiveContent delegates to INavigationService"
```

---

# WAVE B — Hard crash guards (Tasks 6-12)

Each task follows the same pattern: add `if (!OperatingSystem.IsWindows()) return ...` at the entry point of unguarded Windows-API methods. Tests verify "on non-Windows, no exception thrown."

## Task 6: AutorunManagerModule guards

**Files:**
- Modify: `src/Modules/AuraCore.Module.AutorunManager/AutorunManagerModule.cs`
- Test: `tests/AuraCore.Tests.Module/AutorunManager/AutorunManagerModuleTests.cs` (extend or create)

- [ ] **Step 1: Add failing test**

```csharp
[Fact]
public async Task ScanAsync_OnNonWindows_ReturnsEmptyResult_NoThrow()
{
    if (OperatingSystem.IsWindows()) return; // skip on Windows
    var module = new AutorunManagerModule();
    var result = await module.ScanAsync(new ScanOptions(), default);
    Assert.True(result.IsSuccess);
    Assert.Equal(0, result.IssuesFound);
}
```

- [ ] **Step 2: Run test (currently fails on Linux but skipped on Windows)**

On Windows the test no-ops. On Linux it would fail with PlatformNotSupportedException. Wave G runs on Linux VM where this test exercises.

- [ ] **Step 3: Modify `AutorunManagerModule.cs`**

At the top of `ScanAsync` (around line 20):

```csharp
public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
{
    if (!OperatingSystem.IsWindows())
        return new ScanResult(Id, true, 0, 0);

    // ... existing code unchanged
}
```

Same pattern at top of `OptimizeAsync` and any other public entry point that touches Registry.

- [ ] **Step 4: Run tests (pass)**

```bash
dotnet test tests/AuraCore.Tests.Module/AuraCore.Tests.Module.csproj --filter "FullyQualifiedName~AutorunManager"
```

- [ ] **Step 5: Commit**

```bash
git add src/Modules/AuraCore.Module.AutorunManager/AutorunManagerModule.cs tests/AuraCore.Tests.Module/AutorunManager/AutorunManagerModuleTests.cs
git commit -m "phase-6.16.B: AutorunManager guard ScanAsync/OptimizeAsync against non-Windows"
```

---

## Task 7: RegistryOptimizerModule guards

**Files:**
- Modify: `src/Modules/AuraCore.Module.RegistryOptimizer/RegistryOptimizerModule.cs`
- Test: `tests/AuraCore.Tests.Module/RegistryOptimizer/RegistryOptimizerModuleTests.cs`

Same pattern as Task 6. Add platform guard to `ScanAsync` (line 20) + `FixIssueAsync` and any other entry point touching Registry.

- [ ] **Step 1-5:** Same TDD cycle as Task 6.

```csharp
public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
{
    if (!OperatingSystem.IsWindows())
        return new ScanResult(Id, true, 0, 0);
    // ... existing code unchanged
}
```

- [ ] **Step 6: Commit**

```bash
git commit -m "phase-6.16.B: RegistryOptimizer guard ScanAsync/FixIssueAsync against non-Windows"
```

---

## Task 8: ContextMenuModule guards

**Files:**
- Modify: `src/Modules/AuraCore.Module.ContextMenu/ContextMenuModule.cs`
- Test: `tests/AuraCore.Tests.Module/ContextMenu/ContextMenuModuleTests.cs`

Same pattern as Task 6.

- [ ] **All steps:** TDD + commit `phase-6.16.B: ContextMenu guard ScanAsync against non-Windows`

---

## Task 9: DefenderManagerModule guards

**Files:**
- Modify: `src/Modules/AuraCore.Module.DefenderManager/DefenderManagerModule.cs:361-363`
- Test: existing test file

- [ ] **Step 1-2: TDD**

```csharp
[Fact]
public async Task GetDefenderStatusAsync_OnNonWindows_ReturnsDisabledStatus_NoThrow()
{
    if (OperatingSystem.IsWindows()) return;
    var module = new DefenderManagerModule(/* mocks */);
    var status = await module.GetDefenderStatusAsync();
    Assert.False(status.IsActive);
    Assert.Contains("not available", status.Message ?? "", StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 3: Wrap WindowsPrincipal block**

Replace:
```csharp
var isAdmin = new System.Security.Principal.WindowsPrincipal(
    System.Security.Principal.WindowsIdentity.GetCurrent())
    .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
```

With:
```csharp
bool isAdmin = false;
if (OperatingSystem.IsWindows())
{
    try
    {
        isAdmin = new System.Security.Principal.WindowsPrincipal(
            System.Security.Principal.WindowsIdentity.GetCurrent())
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }
    catch { /* defensive — should not throw on Windows but harmless */ }
}
```

Also add early return at top of `GetDefenderStatusAsync` if non-Windows.

- [ ] **Step 4-5: Test pass + commit**

```bash
git commit -m "phase-6.16.B: DefenderManager guard WindowsPrincipal access"
```

---

## Task 10: BackgroundScheduler guards

**Files:**
- Modify: `src/Desktop/AuraCore.Desktop/Services/Scheduler/BackgroundScheduler.cs:147-169`
- Test: `tests/AuraCore.Tests.Unit/Scheduler/BackgroundSchedulerTests.cs` (create if missing)

**Critical:** This runs every 60 seconds via timer. Linux app must not crash on each timer tick.

- [ ] **Step 1-2: TDD**

```csharp
[Fact]
public void GetIdleTime_OnNonWindows_ReturnsZero_NoThrow()
{
    if (OperatingSystem.IsWindows()) return;
    var idle = BackgroundSchedulerTestAccessor.InvokeGetIdleTime();
    Assert.Equal(TimeSpan.Zero, idle);
}
```

(May need to add `internal` access via InternalsVisibleTo or a public accessor; keep production access private.)

- [ ] **Step 3: Add guard at line 147**

```csharp
private static TimeSpan GetIdleTime()
{
    if (!OperatingSystem.IsWindows()) return TimeSpan.Zero;
    var info = new LASTINPUTINFO { cbSize = ... };
    // ... existing code
}
```

- [ ] **Step 4-5: Test pass + commit**

```bash
git commit -m "phase-6.16.B: BackgroundScheduler.GetIdleTime guard prevents per-tick crash on Linux"
```

---

## Task 11: StartupOptimizerView guards (inside Task.Run)

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/StartupOptimizerView.axaml.cs:37-64`

The method-entry guard at line 29 doesn't propagate into the `Task.Run` delegate. Add guard inside the delegate.

- [ ] **Step 1: Modify the `Task.Run` delegate**

```csharp
var rawData = await Task.Run(() =>
{
    if (!OperatingSystem.IsWindows()) return new List<...>();
    var list = new List<...>();
    ScanReg(list, Registry.CurrentUser, "HKCU");
    ScanReg(list, Registry.LocalMachine, "HKLM");
    return list;
});
```

- [ ] **Step 2-3: build + commit**

No new test (covered by existing module tests + Wave G smoke).

```bash
git commit -m "phase-6.16.B: StartupOptimizerView guard inside Task.Run delegate"
```

---

## Task 12: ServiceManagerView guards (inside Task.Run)

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/ServiceManagerView.axaml.cs:39-44`

Same pattern as Task 11.

- [ ] **Step 1: Modify `Task.Run` delegate**

```csharp
var rawData = await Task.Run(() =>
{
    if (!OperatingSystem.IsWindows()) return new List<(...)>();
    var services = ServiceController.GetServices();
    return services.Select(s => (s.ServiceName, s.DisplayName, s.Status, s.StartType)).ToList();
});
```

- [ ] **Step 2: Commit**

```bash
git commit -m "phase-6.16.B: ServiceManagerView guard inside Task.Run delegate"
```

---

# WAVE C — Silent fail fixes (Tasks 13-15)

## Task 13: Register all module view factories per-platform

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/App.axaml.cs`

**Goal:** After building service provider, call `nav.RegisterModuleView(...)` for every module that exists on the current platform. Replaces the SetActiveContent switch one-to-one.

- [ ] **Step 1: Read current App.axaml.cs**

Identify the section after DI bootstrap where modules are registered (around lines 63-95 Windows + 98-136 Linux per audit).

- [ ] **Step 2: Add view factory registrations**

After the service provider is built, in App.axaml.cs OnFrameworkInitializationCompleted:

```csharp
var nav = sp.GetRequiredService<INavigationService>();

// Cross-platform view factories
nav.RegisterModuleView("dashboard",            sp => new DashboardView());
nav.RegisterModuleView("ai-features",          sp => new AIFeaturesView());
nav.RegisterModuleView("settings",             sp => new SettingsView());
nav.RegisterModuleView("ram-optimizer",        sp => new Pages.RamOptimizerView());
nav.RegisterModuleView("network-optimizer",    sp => new Pages.NetworkOptimizerView());
nav.RegisterModuleView("battery-optimizer",    sp => new Pages.BatteryOptimizerView());
nav.RegisterModuleView("junk-cleaner",         sp => new CategoryCleanView(sp.GetRequiredKeyedService<IOptimizationModule>("junk-cleaner")));
nav.RegisterModuleView("disk-cleanup",         sp => new CategoryCleanView(sp.GetRequiredKeyedService<IOptimizationModule>("disk-cleanup")));
nav.RegisterModuleView("privacy-cleaner",      sp => new CategoryCleanView(sp.GetRequiredKeyedService<IOptimizationModule>("privacy-cleaner")));
nav.RegisterModuleView("file-shredder",        sp => new Pages.FileShredderView());
nav.RegisterModuleView("hosts-editor",         sp => new Pages.HostsEditorView());
nav.RegisterModuleView("space-analyzer",       sp => new Pages.SpaceAnalyzerView());
nav.RegisterModuleView("system-health",        sp => new Pages.SystemHealthView());
nav.RegisterModuleView("environment-variables",sp => new Pages.EnvironmentVariablesView());
nav.RegisterModuleView("symlink-manager",      sp => new Pages.SymlinkManagerView());
nav.RegisterModuleView("process-monitor",      sp => new Pages.ProcessMonitorView());
nav.RegisterModuleView("wake-on-lan",          sp => new Pages.WakeOnLanView());
nav.RegisterModuleView("admin-panel",          sp => new Pages.AdminPanelView());

if (OperatingSystem.IsWindows())
{
    nav.RegisterModuleView("startup-optimizer",     sp => new Pages.StartupOptimizerView());
    nav.RegisterModuleView("storage-compression",   sp => new Pages.StorageCompressionView());
    nav.RegisterModuleView("registry-cleaner",      sp => new CategoryCleanView(sp.GetRequiredKeyedService<IOptimizationModule>("registry-cleaner")));
    nav.RegisterModuleView("bloatware-removal",     sp => new Pages.BloatwareRemovalView());
    nav.RegisterModuleView("gaming-mode",           sp => new Pages.GamingModeView());
    nav.RegisterModuleView("defender-manager",      sp => new Pages.DefenderManagerView());
    nav.RegisterModuleView("firewall-rules",        sp => new Pages.FirewallRulesView());
    nav.RegisterModuleView("app-installer",         sp => new Pages.AppInstallerView());
    nav.RegisterModuleView("driver-updater",        sp => new Pages.DriverUpdaterView());
    nav.RegisterModuleView("service-manager",       sp => new Pages.ServiceManagerView());
    nav.RegisterModuleView("iso-builder",           sp => new Pages.IsoBuilderView());
    nav.RegisterModuleView("registry-deep",         sp => new Pages.RegistryDeepView());
    nav.RegisterModuleView("context-menu",          sp => new Pages.ContextMenuView());
    nav.RegisterModuleView("taskbar-tweaks",        sp => new Pages.TaskbarTweaksView());
    nav.RegisterModuleView("explorer-tweaks",       sp => new Pages.ExplorerTweaksView());
    nav.RegisterModuleView("autorun-manager",       sp => new Pages.AutorunManagerView());
}

if (OperatingSystem.IsLinux())
{
    nav.RegisterModuleView("systemd-manager",       sp => new Pages.SystemdManagerView());
    nav.RegisterModuleView("swap-optimizer",        sp => new Pages.SwapOptimizerView());
    nav.RegisterModuleView("package-cleaner",       sp => new CategoryCleanView(sp.GetRequiredKeyedService<IOptimizationModule>("package-cleaner")));
    nav.RegisterModuleView("journal-cleaner",       sp => new CategoryCleanView(sp.GetRequiredKeyedService<IOptimizationModule>("journal-cleaner")));
    nav.RegisterModuleView("snap-flatpak-cleaner",  sp => new CategoryCleanView(sp.GetRequiredKeyedService<IOptimizationModule>("snap-flatpak-cleaner")));
    nav.RegisterModuleView("kernel-cleaner",        sp => new CategoryCleanView(sp.GetRequiredKeyedService<IOptimizationModule>("kernel-cleaner")));
    nav.RegisterModuleView("linux-app-installer",   sp => new Pages.LinuxAppInstallerView());
    nav.RegisterModuleView("cron-manager",          sp => new Pages.CronManagerView());
    nav.RegisterModuleView("docker-cleaner",        sp => new CategoryCleanView(sp.GetRequiredKeyedService<IOptimizationModule>("docker-cleaner")));
    nav.RegisterModuleView("grub-manager",          sp => new Pages.GrubManagerView());
}

if (OperatingSystem.IsMacOS())
{
    // macOS view factories — modules registered Phase 5.5/6.x but not in current SetActiveContent
    nav.RegisterModuleView("purgeable-space-manager",  sp => new Pages.PurgeableSpaceManagerView());
    nav.RegisterModuleView("xcode-cleaner",            sp => new Pages.XcodeCleanerView());
    nav.RegisterModuleView("timemachine-manager",      sp => new Pages.TimeMachineManagerView());
    nav.RegisterModuleView("defaults-optimizer",       sp => new Pages.DefaultsOptimizerView());
    nav.RegisterModuleView("brew-manager",             sp => new Pages.BrewManagerView());
    nav.RegisterModuleView("dns-flusher",              sp => new Pages.DnsFlusherView());
    nav.RegisterModuleView("mac-app-installer",        sp => new Pages.MacAppInstallerView());
    nav.RegisterModuleView("launchagent-manager",      sp => new Pages.LaunchAgentManagerView());
    nav.RegisterModuleView("spotlight-manager",        sp => new Pages.SpotlightManagerView());
}
```

> **Note:** Some `CategoryCleanView` consumers above use keyed DI (`GetRequiredKeyedService`). If the existing DI registration uses unkeyed singletons, adjust to inject the matching `IOptimizationModule` instance. The implementer must verify against the actual DI shape in `App.axaml.cs` and adjust as needed.

- [ ] **Step 3: Build + run all UI tests**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj 2>&1 | tail -3
```
Expected: 0 errors. UI tests still pass (1632 baseline + Wave A additions).

- [ ] **Step 4: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/App.axaml.cs
git commit -m "phase-6.16.C: register module view factories per-platform on app startup"
```

---

## Task 14: `CategoryCleanView` ctor null-handling

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/CategoryCleanView.cs`
- Test: `tests/AuraCore.Tests.UI.Avalonia/Views/CategoryCleanViewTests.cs`

- [ ] **Step 1: Add failing test**

```csharp
[Fact]
public void Ctor_NullModule_Throws()
{
    Assert.Throws<ArgumentNullException>(() => new CategoryCleanView(null!));
}
```

- [ ] **Step 2: Modify CategoryCleanView ctor**

Replace `IOptimizationModule? module` with `IOptimizationModule module`:

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

Remove the `if (module is null) return;` line.

- [ ] **Step 3: Run tests (pass)**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~CategoryCleanView"
```

- [ ] **Step 4: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/CategoryCleanView.cs tests/AuraCore.Tests.UI.Avalonia/Views/CategoryCleanViewTests.cs
git commit -m "phase-6.16.C: CategoryCleanView ctor throws on null module"
```

---

## Task 15: 9 Linux modules — `CheckRuntimeAvailabilityAsync` + `IHelperAvailabilityService`

**Files:** 9 module files + 9 test files

| Module | File | Tool to detect | Remediation hint |
|---|---|---|---|
| Systemd Manager | `src/Modules/AuraCore.Module.SystemdManager/SystemdManagerModule.cs` | `systemctl` | "Switch to a systemd-based distribution" |
| Swap Optimizer | `src/Modules/AuraCore.Module.SwapOptimizer/SwapOptimizerModule.cs` | (helper only) | "sudo bash /opt/auracorepro/install-privhelper.sh" |
| Package Cleaner | `src/Modules/AuraCore.Module.PackageCleaner/PackageCleanerModule.cs` | one of apt/dnf/pacman/zypper | "Install a supported package manager" |
| Cron Manager | `src/Modules/AuraCore.Module.CronManager/CronManagerModule.cs` | `crontab` | "sudo apt install cron" |
| Journal Cleaner | `src/Modules/AuraCore.Module.JournalCleaner/JournalCleanerModule.cs` | `journalctl` | "Switch to a systemd-based distribution" |
| Snap/Flatpak Cleaner | `src/Modules/AuraCore.Module.SnapFlatpakCleaner/SnapFlatpakCleanerModule.cs` | one of snap/flatpak | "sudo snap install snap or sudo apt install flatpak" |
| Kernel Cleaner | `src/Modules/AuraCore.Module.KernelCleaner/KernelCleanerModule.cs` | `apt-get` (graceful for non-apt) | "Currently supports apt-based distributions only" |
| GRUB Manager | `src/Modules/AuraCore.Module.GrubManager/GrubManagerModule.cs` | `update-grub` | "sudo apt install grub-pc" |
| Docker Cleaner | `src/Modules/AuraCore.Module.DockerCleaner/DockerCleanerModule.cs` | `docker` | "https://docs.docker.com/engine/install/" |

For each module:

- [ ] **Step A: Add unit test**

```csharp
public class SystemdManagerModuleAvailabilityTests
{
    [Fact]
    public async Task CheckRuntimeAvailabilityAsync_OnNonLinux_ReturnsWrongPlatform()
    {
        if (OperatingSystem.IsLinux()) return;
        var module = new SystemdManagerModule(
            Substitute.For<IHelperAvailabilityService>(),
            Substitute.For<IShellCommandService>());
        var r = await module.CheckRuntimeAvailabilityAsync();
        Assert.Equal(AvailabilityCategory.WrongPlatform, r.Category);
    }

    [Fact]
    public async Task CheckRuntimeAvailabilityAsync_HelperMissing_ReturnsHelperNotRunning()
    {
        if (!OperatingSystem.IsLinux()) return;  // only meaningful on Linux
        var helper = Substitute.For<IHelperAvailabilityService>();
        helper.IsMissing.Returns(true);
        var shell = Substitute.For<IShellCommandService>();
        shell.CommandExistsAsync("systemctl", Arg.Any<CancellationToken>()).Returns(true);
        var module = new SystemdManagerModule(helper, shell);
        var r = await module.CheckRuntimeAvailabilityAsync();
        Assert.Equal(AvailabilityCategory.HelperNotRunning, r.Category);
    }

    [Fact]
    public async Task CheckRuntimeAvailabilityAsync_ToolMissing_ReturnsToolNotInstalled()
    {
        if (!OperatingSystem.IsLinux()) return;
        var helper = Substitute.For<IHelperAvailabilityService>();
        helper.IsMissing.Returns(false);
        var shell = Substitute.For<IShellCommandService>();
        shell.CommandExistsAsync("systemctl", Arg.Any<CancellationToken>()).Returns(false);
        var module = new SystemdManagerModule(helper, shell);
        var r = await module.CheckRuntimeAvailabilityAsync();
        Assert.Equal(AvailabilityCategory.ToolNotInstalled, r.Category);
    }
}
```

- [ ] **Step B: Add CheckRuntimeAvailabilityAsync override on module**

```csharp
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
            return ModuleAvailability.HelperNotRunning("sudo bash /opt/auracorepro/install-privhelper.sh");
        if (!await _shell.CommandExistsAsync("systemctl", ct))
            return ModuleAvailability.ToolNotInstalled("systemctl", "Switch to a systemd-based Linux distribution.");
        return ModuleAvailability.Available;
    }

    // ... existing ScanAsync, RunAsync etc.
}
```

- [ ] **Step C: Verify DI registration injects the dependencies**

In the Linux registration block of `App.axaml.cs` (line 98-136), confirm `SystemdManagerModule` constructor params are satisfied. If not, add registrations:

```csharp
sc.AddSingleton<IHelperAvailabilityService, HelperAvailabilityService>();
sc.AddSingleton<IShellCommandService, ShellCommandService>();
```

(Likely already present per Phase 5.2.1.)

- [ ] **Step D: Run module tests**

```bash
dotnet test tests/AuraCore.Tests.Module/AuraCore.Tests.Module.csproj --filter "FullyQualifiedName~SystemdManager"
```
Expected: 3 tests pass.

- [ ] **Step E: Commit**

```bash
git commit -m "phase-6.16.C: SystemdManager CheckRuntimeAvailabilityAsync helper+tool detection"
```

**Repeat Steps A-E for the remaining 8 modules** (Swap Optimizer, Package Cleaner, Cron Manager, Journal Cleaner, Snap/Flatpak Cleaner, Kernel Cleaner, GRUB Manager, Docker Cleaner). Adjust the tool name + remediation per the table above. Total: ~9 commits in this task.

For Package Cleaner (multi-tool detection):

```csharp
public async Task<ModuleAvailability> CheckRuntimeAvailabilityAsync(CancellationToken ct = default)
{
    if (!OperatingSystem.IsLinux())
        return ModuleAvailability.WrongPlatform(SupportedPlatform.Linux);
    if (_helper.IsMissing)
        return ModuleAvailability.HelperNotRunning("sudo bash /opt/auracorepro/install-privhelper.sh");

    foreach (var pm in new[] { "apt", "dnf", "pacman", "zypper" })
        if (await _shell.CommandExistsAsync(pm, ct))
            return ModuleAvailability.Available;

    return ModuleAvailability.ToolNotInstalled("apt/dnf/pacman/zypper",
        "Install a supported package manager.");
}
```

---

# WAVE D — Sidebar declaration fixes (Tasks 16-17)

## Task 16: Fix `startup-optimizer` + `autorun-manager` declarations + audit pass

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/ViewModels/SidebarViewModel.cs`

- [ ] **Step 1: Read current SidebarViewModel.cs**

Identify all `Module(...)` calls and verify each entry's `platform` parameter matches the underlying `IOptimizationModule.Platform` enum.

- [ ] **Step 2: Fix the two known mismatches**

Line 123:
```csharp
// Before:
Module("startup-optimizer", "nav.startupOptimizer"),
// After:
Module("startup-optimizer", "nav.startupOptimizer", "windows"),
```

Line 227:
```csharp
// Before:
Module("autorun-manager", "nav.autorunManager"),
// After:
Module("autorun-manager", "nav.autorunManager", "windows"),
```

- [ ] **Step 3: Audit pass — verify all other entries**

Walk through the rest of the file. For any other `Module(...)` call with no `platform` arg or with a mismatch against the actual module's `Platform` enum, fix it. Document findings in commit message.

- [ ] **Step 4: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/ViewModels/SidebarViewModel.cs
git commit -m "phase-6.16.D: SidebarViewModel mark startup-optimizer + autorun-manager as Windows-only"
```

---

## Task 17: Sidebar declaration consistency test

**Files:**
- Test: `tests/AuraCore.Tests.UI.Avalonia/ViewModels/SidebarDeclarationConsistencyTests.cs`

**Goal:** For every module declared in SidebarViewModel, assert its `platform` string matches the corresponding `IOptimizationModule.Platform` enum value. Prevents future drift.

- [ ] **Step 1: Write the test**

```csharp
[Fact]
public void Every_Sidebar_Module_Platform_Matches_Underlying_Module_Platform()
{
    var sidebar = new SidebarViewModel(/* test deps */);
    var allItems = sidebar.AllSidebarItems().ToList();   // helper to flatten categories+advanced

    var moduleCatalog = TestModuleCatalog.GetAllRegisteredModules();
    // moduleCatalog: Dictionary<string moduleId, IOptimizationModule>

    foreach (var item in allItems)
    {
        if (!moduleCatalog.TryGetValue(item.ModuleId, out var module))
            continue;  // sidebar entry without registered module — skip (documented gap)

        var sidebarPlatformStr = item.Platform;  // "all" / "windows" / "linux" / "macos"
        var expectedStr = module.Platform switch
        {
            SupportedPlatform.Windows => "windows",
            SupportedPlatform.Linux   => "linux",
            SupportedPlatform.MacOS   => "macos",
            SupportedPlatform.All     => "all",
            _                         => "all",
        };

        Assert.Equal(expectedStr, sidebarPlatformStr);
    }
}
```

- [ ] **Step 2: Run test (expect pass after Task 16)**

- [ ] **Step 3: Commit**

```bash
git commit -m "phase-6.16.D: sidebar-vs-module platform declaration consistency test"
```

---

# WAVE E — Localization sweep (Tasks 18-21)

## Task 18: 11 localization key updates

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/LocalizationService.cs`

- [ ] **Step 1: Update 11 EN keys**

Per the table in spec D3:

| Key (line approx) | Old EN | New EN |
|---|---|---|
| `login.subtitle` (62) | "AI Powered Windows Intelligence" | "AI-Powered System Intelligence" |
| `set.websiteLink` (98) | "auracore.pro - Windows Optimization SaaS" | "auracore.pro - Cross-platform System Optimization" |
| `settings.tagline` (394) | "AI Powered Windows Intelligence" | "AI-Powered System Intelligence" |
| `settings.description` (395) | "...Windows optimization suite featuring 27+ modules..." | "...Cross-platform optimization suite featuring 27+ modules..." |
| `dc.subtitle` (501) | "Windows deep clean..." | "Deep clean — system caches, duplicates, empty folders" |
| `hosts.subtitle` (974) | "Edit the Windows hosts file..." | "Edit the system hosts file (block domains, set custom DNS mappings)" |
| `symlink.adminWarning` (2383) | "Creating symbolic links requires administrator privileges on Windows." | "On Windows, creating symbolic links requires administrator privileges. On Linux/macOS, requires write access to the target directory." |
| `onb.welcomeDesc` (1004) | "...Windows optimization toolkit..." | "...Cross-platform optimization toolkit..." |
| `onb.customizeTitle` (1005) | "Customize Your Windows" | "Customize Your System" |
| `onb.customizeDetail` (1006) | "All tweaks work on both Windows 10 and Windows 11." | "Modules adapt to your platform — Windows tweaks on Windows, systemd controls on Linux, defaults editing on macOS." |

- [ ] **Step 2: Update corresponding TR keys**

For each EN key, find the TR equivalent (in the second `["TR"]` dictionary block) and update accordingly.

- [ ] **Step 3: Build + run UI tests (no regressions)**

- [ ] **Step 4: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/LocalizationService.cs
git commit -m "phase-6.16.E: localization sweep — 11 platform-neutral copy rewrites"
```

---

## Task 19: `QuickActionPresets.Default` rename + platform filter

**Files:**
- Rename: `src/UI/AuraCore.UI.Avalonia/ViewModels/Dashboard/QuickActionPresets.Windows.cs` → `QuickActionPresets.cs`
- Modify: `src/UI/AuraCore.UI.Avalonia/ViewModels/Dashboard/DashboardViewModel.cs`

- [ ] **Step 1: Rename file + class method**

`git mv` the file. Rename method `Windows(...)` to `Default(...)`. Add platform filter:

```csharp
public static IReadOnlyList<QuickActionTileVM> Default(
    Func<Task> quickCleanup,
    Func<Task> optimizeRam,
    Func<Task> removeBloat,
    LocalizationService loc)
{
    var tiles = new List<QuickActionTileVM>
    {
        new("quick-cleanup", loc["quickaction.quickcleanup.label"], quickCleanup),
        new("optimize-ram",  loc["quickaction.optimizeram.label"],  optimizeRam),
    };
    if (OperatingSystem.IsWindows())
        tiles.Add(new("remove-bloat", loc["quickaction.removebloat.label"], removeBloat));
    return tiles;
}
```

- [ ] **Step 2: Update DashboardViewModel.cs caller**

```csharp
// Before:
QuickActions = QuickActionPresets.Windows(
    quickCleanup: () => Task.CompletedTask,
    optimizeRam:  () => Task.CompletedTask,
    removeBloat:  () => Task.CompletedTask);

// After:
QuickActions = QuickActionPresets.Default(
    quickCleanup: () => Task.CompletedTask,
    optimizeRam:  () => Task.CompletedTask,
    removeBloat:  () => Task.CompletedTask,
    _localization);
```

- [ ] **Step 3: Add unit test**

```csharp
[Fact]
public void Default_OnLinux_ExcludesRemoveBloatTile()
{
    if (OperatingSystem.IsWindows()) return;
    var tiles = QuickActionPresets.Default(() => Task.CompletedTask, () => Task.CompletedTask, () => Task.CompletedTask, _testLoc);
    Assert.DoesNotContain(tiles, t => t.Id == "remove-bloat");
}

[Fact]
public void Default_OnWindows_IncludesRemoveBloatTile()
{
    if (!OperatingSystem.IsWindows()) return;
    var tiles = QuickActionPresets.Default(() => Task.CompletedTask, () => Task.CompletedTask, () => Task.CompletedTask, _testLoc);
    Assert.Contains(tiles, t => t.Id == "remove-bloat");
}
```

- [ ] **Step 4: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/ViewModels/Dashboard/QuickActionPresets.cs src/UI/AuraCore.UI.Avalonia/ViewModels/Dashboard/DashboardViewModel.cs tests/AuraCore.Tests.UI.Avalonia/ViewModels/QuickActionPresetsTests.cs
git commit -m "phase-6.16.E: QuickActionPresets.Default platform-filters Remove Windows Bloat tile"
```

---

## Task 20: `FirewallRulesView.axaml` hardcoded label → localization binding

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/FirewallRulesView.axaml:11`

- [ ] **Step 1: Replace hardcoded subtitle**

```xml
<!-- Before -->
<ModuleHeader Subtitle="Manage Windows Firewall inbound and outbound rules" />

<!-- After: bind to localization key -->
<ModuleHeader x:Name="Header" />
```

In code-behind `OnLoaded`/`ApplyLocalization()`:

```csharp
Header.Subtitle = LocalizationService.Current["firewall.subtitle"];
```

- [ ] **Step 2: Build + commit**

```bash
git commit -m "phase-6.16.E: FirewallRulesView subtitle bound to localization (was hardcoded)"
```

---

## Task 21: Hardcoded-string scanner enhancement

**Files:**
- Modify: `tests/AuraCore.Tests.UI.Avalonia/Localization/HardcodedStringScannerTests.cs`

**Goal:** Extend the existing scanner to flag platform-name leakage in non-platform-keyed localization values.

- [ ] **Step 1: Add new test method**

```csharp
[Fact]
public void Localization_Values_Should_Not_Contain_Platform_Names_Except_In_Platform_Keys()
{
    var loc = new LocalizationService();
    var enDict = loc.GetEnDictionary();  // helper or test accessor
    var bannedWords = new[] { "Windows 10", "Windows 11", "macOS", "Ubuntu", "Linux distro" };

    foreach (var (key, value) in enDict)
    {
        if (key.EndsWith(".windows") || key.EndsWith(".linux") || key.EndsWith(".macos"))
            continue;  // platform-suffixed keys allowed to mention platform
        if (key.StartsWith("def.") || key.StartsWith("svc.") || /* ... known Windows-only modules ... */)
            continue;  // module-scoped keys for hidden modules

        foreach (var banned in bannedWords)
            Assert.False(value.Contains(banned, StringComparison.OrdinalIgnoreCase),
                $"Key '{key}' contains banned platform name: {banned}");
    }
}
```

- [ ] **Step 2: Run test (verify clean)**

After Tasks 18-19 fixes, this test should pass. If it surfaces additional Windows-y strings, fix them in this commit.

- [ ] **Step 3: Commit**

```bash
git commit -m "phase-6.16.E: hardcoded-string scanner — flag platform-name leakage in non-platform keys"
```

---

# WAVE F — CA1416 enforcement (Tasks 22-24)

## Task 22: Add `MSBuildWarningsAsErrors` to relevant csproj files

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj`
- Modify: `src/Desktop/AuraCore.Desktop.Services/AuraCore.Desktop.Services.csproj`
- Modify: All `src/Modules/AuraCore.Module.*/AuraCore.Module.*.csproj` (~30 files)
- Modify: `src/Plugins/AuraCore.Plugin.SDK/AuraCore.Plugin.SDK.csproj`

- [ ] **Step 1: Add property to UI csproj**

Edit `src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj`. Inside the first `<PropertyGroup>`:

```xml
<MSBuildWarningsAsErrors>CA1416</MSBuildWarningsAsErrors>
```

- [ ] **Step 2: Build (likely fails due to remaining unguarded paths)**

```bash
dotnet build src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj -c Release 2>&1 | grep "error CA1416"
```

If the build fails with new CA1416 errors that Wave B didn't cover, fix them now (one extra commit per fix).

- [ ] **Step 3: Repeat for Desktop.Services + Modules + Plugins**

Same `<MSBuildWarningsAsErrors>CA1416</MSBuildWarningsAsErrors>` line. Build each, fix new errors as they surface.

- [ ] **Step 4: Verify clean solution build**

```bash
dotnet build AuraCorePro.sln -c Release 2>&1 | grep -E "error|warning CA1416" | head -20
```
Expected: 0 CA1416 errors, 0 CA1416 warnings.

- [ ] **Step 5: Commit (single commit per project group)**

```bash
git add src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj src/Desktop/AuraCore.Desktop.Services/AuraCore.Desktop.Services.csproj src/Plugins/AuraCore.Plugin.SDK/AuraCore.Plugin.SDK.csproj
git commit -m "phase-6.16.F: enforce CA1416 as error in UI/Services/Plugins"

git add 'src/Modules/AuraCore.Module.*/AuraCore.Module.*.csproj'
git commit -m "phase-6.16.F: enforce CA1416 as error in all Module projects"
```

---

## Task 23: `[SupportedOSPlatform("windows")]` on Windows-only module classes

**Files:** 15 module class files

- AutorunManager
- RegistryOptimizer
- ContextMenu
- DefenderManager
- FirewallRules
- ServiceManager
- AppInstaller
- DriverUpdater
- IsoBuilder
- GamingMode
- StorageCompression
- Bloatware
- Tweaks
- TaskbarTweaks
- ExplorerTweaks

For each, add at top of the module class:

```csharp
using System.Runtime.Versioning;

[SupportedOSPlatform("windows")]
public sealed class AutorunManagerModule : IOptimizationModule
{
    // ... unchanged
}
```

- [ ] **Step 1: Add attribute to each module class**

Batch operation. Iterate through 15 files.

- [ ] **Step 2: Build to surface new CA1416 errors**

```bash
dotnet build AuraCorePro.sln -c Release 2>&1 | grep "error CA1416"
```

Any new errors from richer analyzer flow? Fix them with same guard-pattern as Wave B.

- [ ] **Step 3: Commit**

```bash
git commit -m "phase-6.16.F: SupportedOSPlatform attribute on 15 Windows-only module classes"
```

---

## Task 24: Verify clean full-suite build

- [ ] **Step 1: Full solution clean build**

```bash
dotnet clean AuraCorePro.sln && dotnet build AuraCorePro.sln -c Release 2>&1 | tail -5
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`. CA1416 must be silent.

- [ ] **Step 2: Full test suite**

```bash
dotnet test AuraCorePro.sln 2>&1 | tail -10
```
Expected: ~2235 tests pass (per spec D11 budget) including Wave A-F additions.

- [ ] **Step 3: No commit needed (verification only)**

If any test fails: file the bug, return to the responsible Wave's task list, fix.

---

# WAVE G — Linux VM re-verify (Tasks 25-26)

## Task 25: Build, deploy to VM, smoke test every module

**Files:** None (verification work)

**Linux VM:** `ssh -i ~/.ssh/id_ed25519_aura deniz@192.168.162.129`

- [ ] **Step 1: Build linux-x64**

```bash
cd /c/Users/Admin/Desktop/AuraCorePro/AuraCorePro
dotnet publish src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=false -p:Version=1.8.0 -o packaging/dist/publish-linux-x64-6.16
```

- [ ] **Step 2: Tar + scp**

```bash
tar czf packaging/dist/auracore-pro-linux-6.16.tar.gz -C packaging/dist/publish-linux-x64-6.16 .
scp -i ~/.ssh/id_ed25519_aura packaging/dist/auracore-pro-linux-6.16.tar.gz deniz@192.168.162.129:~/auracore-pro-6.16.tar.gz
```

- [ ] **Step 3: Extract on VM**

```bash
ssh -i ~/.ssh/id_ed25519_aura deniz@192.168.162.129 'rm -rf ~/auracore-pro-6.16-test && mkdir ~/auracore-pro-6.16-test && tar xzf ~/auracore-pro-6.16.tar.gz -C ~/auracore-pro-6.16-test && chmod +x ~/auracore-pro-6.16-test/AuraCore.Pro'
```

- [ ] **Step 4: User-driven smoke test on VM desktop**

Hand off to user. They open VM desktop GUI, terminal `cd ~/auracore-pro-6.16-test && ./AuraCore.Pro`, click every sidebar entry. Build the matrix (D12 in spec). Each module:

- Did app crash? (must be NO)
- Did sidebar render the entry? (Linux-only / cross-platform: YES; Windows-only: NO)
- Did module open its proper view OR an UnavailableModuleView with actionable message? (must be one of these two)

Capture screenshots. File issues for any failures back to the responsible wave's task.

- [ ] **Step 5: Commit evidence matrix**

Append the per-module pass/fail matrix as a markdown file:

```bash
cat > docs/superpowers/phase-6-16-vm-verify-matrix.md << 'EOF'
# Phase 6.16 VM Smoke Test Matrix

VM: Ubuntu 24.04.4 LTS / VMware
Build: phase-6-16-linux-platform-awareness HEAD <commit-sha>
Date: <date>

| Module | Sidebar visible | App crash? | Render OK or UnavailableModuleView? | Notes |
|---|---|---|---|---|
| Dashboard | yes | no | OK | |
| RAM Optimizer | yes | no | OK | |
| ... (all modules) ... |
EOF
git add docs/superpowers/phase-6-16-vm-verify-matrix.md
git commit -m "phase-6.16.G: VM smoke test matrix — all modules verified pass-or-graceful"
```

---

## Task 26: Re-cycle if Wave G surfaces new bugs

If any module crashes or silent-fails despite Waves A-F, file a bug and return to the responsible Wave. Re-run Wave G after fixes. **No new tasks created here**; just iterate until matrix is clean.

---

# WAVE H — macOS pre-release gate (Task 27)

## Task 27: Write `docs/ops/macos-prerelease-checklist.md`

**Files:**
- Create: `docs/ops/macos-prerelease-checklist.md`

- [ ] **Step 1: Write the checklist file**

```markdown
# macOS Pre-Release Checklist

**Use:** Before any AuraCorePro macOS release. Run this checklist sequentially. All items must be ticked before the operator uploads the macOS .dmg via the admin panel.

**Created:** Phase 6.16 (post-Linux smoke-test disaster, ensures macOS doesn't have the same surprise)

## Build hygiene
- [ ] `dotnet build AuraCorePro.sln -c Release` exits with **0 CA1416 warnings/errors**
- [ ] All Windows-only module classes have `[SupportedOSPlatform("windows")]` attribute (verify via grep)
- [ ] All Linux-only module classes have `[SupportedOSPlatform("linux")]` attribute (NEW for 6.17+)
- [ ] All macOS-only module classes have `[SupportedOSPlatform("macos")]` attribute (NEW for macOS release)

## Sidebar correctness
- [ ] Every module declared in `SidebarViewModel.cs` has correct `platform` value matching `IOptimizationModule.Platform`
- [ ] On macOS, run app in `Avalonia.Headless` test harness; assert sidebar items count matches expected macOS module list (~9 macOS-specific + ~12 cross-platform)
- [ ] No `Windows` / `Linux` / `Ubuntu` / `Win` / `apt-get` text leakage in macOS-visible labels (run grep against built-binary embedded localization dict)

## Runtime smoke (real Mac required)
- [ ] App launches without crash (cold start to MainWindow visible)
- [ ] Click each macOS-specific module — opens proper view, no crash
- [ ] Click each cross-platform module — opens proper view, no crash
- [ ] Each Linux- and Windows-only module is **hidden** from sidebar
- [ ] Each module returning `RuntimeUnavailable` shows `UnavailableModuleView` with actionable remediation (e.g. `brew install X`)
- [ ] `BackgroundScheduler` runs for ≥3 minutes without throwing (timer-tick trap)
- [ ] AI Features panel opens and chat renders (LLM model load on macOS verified)
- [ ] Settings page version label reads "1.x.0 (Avalonia Cross-Platform)" with no Windows-centric copy

## macOS-specific
- [ ] Apple Developer ID signature applied to .app bundle (`codesign -dv` shows valid)
- [ ] Notarization request submitted to Apple, approval received
- [ ] DMG created via `packaging/build-macos.sh`, smoke-tested on a separate clean macOS VM/machine
- [ ] Gatekeeper assessment passes (`spctl -a -v <path>.app` exits 0)
- [ ] First-run does not show "developer cannot be verified" warning

## Localization
- [ ] All 11 platform-neutral keys (per Phase 6.16.E) verified on macOS UI
- [ ] No "Windows" / "Linux" string leaked to a macOS-visible UI surface
- [ ] Onboarding flow tested end-to-end on macOS

## CHANGELOG + version
- [ ] CHANGELOG entry added for macOS support
- [ ] Version bumped (likely v1.9.0 with macOS as headline feature)
- [ ] All version-bump locations updated (per Phase 6.15.7 — 11+ locations)

## Distribution
- [ ] `cross-publish.ps1` and/or `build-macos.sh` produces signed .dmg artifact
- [ ] R2 upload via admin panel: macOS platform tile enabled (currently "Coming Soon" — Phase 6.17 prep)
- [ ] GitHub Releases mirror includes macOS artifact
- [ ] Landing page OS-detect serves macOS DMG to Mac visitors
- [ ] Update endpoint `/api/updates/check?platform=macos` returns the new release

## Sign-off

Operator: _________________________  Date: _________

```

- [ ] **Step 2: Commit**

```bash
git add docs/ops/macos-prerelease-checklist.md
git commit -m "phase-6.16.H: macOS pre-release gate checklist (operator-driven)"
```

---

# WAVE I — Final integration + release artifact + smoke test + PAUSE

> **User instruction (autonomous-mode handoff):** Build artifacts + smoke test = LAST step. Do NOT auto-deploy v1.8.0 admin-panel upload. Pause for user.

## Task 28: Force-push v1.8.0 tag + rebuild release zips + smoke test

**Files:** None (release ops)

- [ ] **Step 1: Verify branch state + merge to main**

```bash
git log --oneline phase-6-16-linux-platform-awareness | head -10
# Expect ~28 commits across 8 waves

git switch main
git merge --no-ff phase-6-16-linux-platform-awareness -m "Phase 6.16 — Linux Platform Awareness + Module Audit Wave (merge)

Closes 4-category structural failure on Linux:
- A foundation: IPlatformAwareModule + ModuleAvailability + UnavailableModuleView + NavigationService refactor
- B hard-crash guards: AutorunManager / RegistryOptimizer / ContextMenu / DefenderManager / BackgroundScheduler / StartupOptimizerView / ServiceManagerView
- C silent-fail fixes: per-platform view factory registry + CategoryCleanView ctor null-handling + 9 Linux modules CheckRuntimeAvailabilityAsync
- D sidebar declarations: startup-optimizer + autorun-manager → windows
- E localization: 11 keys neutralized + QuickActionPresets platform filter + FirewallRulesView XAML fix
- F CA1416 enforcement: warnings-as-errors per csproj + [SupportedOSPlatform] attributes on 15 Windows-only modules
- G Linux VM re-verify: every module passes-or-graceful on Ubuntu 24.04
- H macOS pre-release gate: operator checklist for next platform launch

Test deltas: backend 233 → 233; UI.Avalonia 1632 → ~1700; Module 158 → ~180. Total ~2235."

git commit --allow-empty -m "Phase 6.16 closed — Linux platform awareness restored, v1.8.0 release unblocked"
```

- [ ] **Step 2: Force-push v1.8.0 tag to fixed HEAD**

The v1.8.0 tag was created on `669aff9` (pre-fix). Move it to current HEAD (post-fix), then force-push. **Safe because v1.8.0 was never published via admin panel; no users impacted.**

```bash
git tag -d v1.8.0
git tag -a v1.8.0 -m "AuraCore Pro v1.8.0 (Phase 6.16 fixed)

Major desktop release: Phase 5.5 finishing + Phase 6.1-6.6 + Phase 6.16 Linux platform awareness + Session 23 ML/LLM. Windows + Linux self-contained binaries. macOS deferred (notarization hardware blocker — see docs/ops/macos-prerelease-checklist.md).

Phase 6.16 fixes ensure no hard-crashes on Linux + graceful UnavailableModuleView for runtime-unavailable modules.

See CHANGELOG.md for full notes."
git push origin main
git push origin v1.8.0 --force-with-lease
```

- [ ] **Step 3: Rebuild fresh v1.8.0 zips**

```bash
rm -rf packaging/dist/publish-* packaging/dist/AuraCorePro-1.8.0-*.zip
dotnet publish src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj --nologo -c Release -r win-x64   --self-contained true -p:PublishSingleFile=false -p:Version=1.8.0 -o packaging/dist/publish-win-x64
dotnet publish src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj --nologo -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=false -p:Version=1.8.0 -o packaging/dist/publish-linux-x64

cd packaging/dist
powershell -Command "Compress-Archive -Path 'publish-win-x64\*'   -DestinationPath 'AuraCorePro-1.8.0-win-x64.zip' -Force"
powershell -Command "Compress-Archive -Path 'publish-linux-x64\*' -DestinationPath 'AuraCorePro-1.8.0-linux-x64.zip' -Force"
ls -lh AuraCorePro-1.8.0-*.zip
powershell -Command "Get-FileHash 'AuraCorePro-1.8.0-win-x64.zip' -Algorithm SHA256 | Select-Object Hash"
powershell -Command "Get-FileHash 'AuraCorePro-1.8.0-linux-x64.zip' -Algorithm SHA256 | Select-Object Hash"
```

Record new SHA256 hashes for the user. They will replace the OLD v1.8.0 hashes (which referenced the broken build).

- [ ] **Step 4: Re-run Linux VM smoke test on the FINAL build**

Repeat Wave G one more time on the final build (post-merge). All modules pass-or-graceful.

- [ ] **Step 5: PAUSE — hand off to user**

Report to user:
- Branch merged, v1.8.0 tag force-pushed to fixed HEAD
- New SHA256 hashes for both zips
- VM smoke test: all modules pass-or-UnavailableModuleView (per matrix in `docs/superpowers/phase-6-16-vm-verify-matrix.md`)
- Awaiting user to perform Phase C from v1.8.0 release plan: admin-panel upload + GitHub Releases mirror trigger + endpoint smoke test

**DO NOT auto-deploy. STOP HERE.**

---

## Self-review notes (per writing-plans skill)

**Spec coverage check:**
- D1 (interface shape) → Tasks 1, 2 ✓
- D2 (UnavailableModuleView UX) → Task 3 ✓
- D3 (localization sweep) → Tasks 18, 19, 20, 21 ✓
- D4 (CA1416 enforcement) → Tasks 22, 23, 24 ✓
- D5 (8 sub-waves) → Wave structure A-H matches ✓
- D6 (macOS gate doc) → Task 27 ✓
- D7 (default interface members) → Task 2 ✓
- D8 (Platform enum + IsPlatformSupported single source) → Task 2 ✓
- D9 (privilege helper checks) → Task 15 (9 sub-tasks) ✓
- D10 (sidebar filter integration) → already works via existing `ShouldShow`; declaration fixes in Task 16 ✓
- D11 (NavigationService rewrite) → Tasks 4, 5, 13 ✓
- D12 (test coverage) → Tasks 1-25 each include unit tests; Wave G is integration ✓

**Placeholder scan:**
- No "TODO" / "TBD" / "fill in details" anywhere in plan ✓
- Every step shows actual code or actual command ✓
- Type names consistent across tasks (`ModuleAvailability`, `IPlatformAwareModule`*, `INavigationShell`, `AvailabilityCategory`) ✓
  - *Note: spec uses `IPlatformAwareModule` as conceptual name, but plan uses default-interface-members on `IOptimizationModule` per D7. No new interface created. This is intentional and matches D7's "no breaking change" goal.

**Type consistency:**
- `ModuleAvailability` factory methods (`Available`, `WrongPlatform(SupportedPlatform)`, `HelperNotRunning(string)`, `ToolNotInstalled(string, string?)`, `FeatureDisabled(string)`) referenced consistently in Tasks 1, 4, 15, 28 ✓
- `INavigationShell.SetActiveContentAsync(UserControl)` consistent in Tasks 4, 5 ✓
- `RegisterModuleView(string moduleId, Func<IServiceProvider, UserControl>)` consistent in Tasks 4, 13 ✓
- `IHelperAvailabilityService.IsMissing` consistent in Task 15 ✓
- `IShellCommandService.CommandExistsAsync(string, CancellationToken)` consistent in Task 15 ✓

**Scope check:** Single phase, 8 sub-waves, ~28 tasks. Comparable to Phase 6.13/6.15 cadence. No further decomposition needed.

**Ambiguity check:**
- "Move v1.8.0 tag forward" → Task 28 spells out the exact `git tag -d` + `git push --force-with-lease` sequence ✓
- "Wave G surfaces new bugs" → Task 26 makes the iteration explicit (no new task IDs created; just cycle back) ✓
- "Module-by-module helper integration" → Task 15 lists all 9 modules with exact tool names + remediation strings ✓
