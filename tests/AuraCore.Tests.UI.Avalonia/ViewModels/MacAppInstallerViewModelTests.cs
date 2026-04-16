using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.Application;
using AuraCore.Module.MacAppInstaller;
using AuraCore.Module.MacAppInstaller.Models;
using AuraCore.UI.Avalonia.ViewModels;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.ViewModels;

/// <summary>
/// Phase 4.4.5 Mac App Installer VM unit tests.
/// Uses a hand-rolled <see cref="IMacAppInstallerEngine"/> fake — matches the
/// LinuxAppInstaller FakeEngine style (no Moq dependency in the test project).
///
/// IMPORTANT: <see cref="MacAppBundles.AllBundles"/> is a shared static catalog
/// and <see cref="MacBundleApp.IsInstalled"/> is mutable. Each test that
/// exercises installed state resets the catalog via <see cref="ResetCatalog"/>
/// in a using-finally so mutation doesn't leak between tests.
/// </summary>
public class MacAppInstallerViewModelTests : IDisposable
{
    public MacAppInstallerViewModelTests() => ResetCatalog();
    public void Dispose() => ResetCatalog();

    private static void ResetCatalog()
    {
        foreach (var b in MacAppBundles.AllBundles)
            foreach (var a in b.Apps)
                a.IsInstalled = false;
    }

    // ── 1. Ctor ────────────────────────────────────────────────────

    [Fact]
    public void Ctor_DoesNotThrow_WithValidInterface()
    {
        var engine = new FakeEngine();
        var vm = new MacAppInstallerViewModel(engine);
        Assert.NotNull(vm);
        Assert.False(vm.IsBusy);
        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.HasError);
        Assert.False(vm.HasSelection);
        Assert.Equal(0, vm.SelectedCount);
    }

    [Fact]
    public void Ctor_NullEngine_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new MacAppInstallerViewModel((IMacAppInstallerEngine)null!));
        Assert.Throws<ArgumentNullException>(() =>
            new MacAppInstallerViewModel((MacAppInstallerModule)null!));
    }

    [Fact]
    public void Ctor_AcceptsConcreteModule_ViaAdapter()
    {
        var module = new MacAppInstallerModule();
        var vm = new MacAppInstallerViewModel(module);
        Assert.NotNull(vm);
    }

    // ── 2. AllBundles populated from static catalog ───────────────

    [Fact]
    public void Ctor_PopulatesAllBundles_FromStaticCatalog()
    {
        var engine = new FakeEngine();
        var vm = new MacAppInstallerViewModel(engine);

        Assert.Equal(MacAppBundles.AllBundles.Count, vm.AllBundles.Count);
        Assert.True(vm.TotalAppsCount > 0, "Total apps count should be derived from the catalog");
        Assert.Equal(MacAppBundles.AllBundles.Sum(b => b.Apps.Count), vm.TotalAppsCount);
    }

    // ── 3. Initial expansion: first bundle expanded, others collapsed ─

    [Fact]
    public void Ctor_SetsFirstBundleExpanded_AndRestCollapsed()
    {
        var engine = new FakeEngine();
        var vm = new MacAppInstallerViewModel(engine);

        Assert.True(vm.AllBundles[0].IsExpanded);
        for (int i = 1; i < vm.AllBundles.Count; i++)
            Assert.False(vm.AllBundles[i].IsExpanded);
    }

    // ── 4. SearchText empty → VisibleBundles == AllBundles ────────

    [Fact]
    public void SearchTextEmpty_VisibleBundlesMatchesAllBundles()
    {
        var engine = new FakeEngine();
        var vm = new MacAppInstallerViewModel(engine);

        Assert.Equal("", vm.SearchText);
        var visible = vm.VisibleBundles.ToList();
        Assert.Equal(vm.AllBundles.Count, visible.Count);
    }

    // ── 5. Non-empty search filters correctly + auto-expands matching ─

    [Fact]
    public void SearchText_NonEmpty_FiltersByNameOrDescription()
    {
        var engine = new FakeEngine();
        var vm = new MacAppInstallerViewModel(engine);

        vm.SearchText = "firefox";

        var visible = vm.VisibleBundles.ToList();
        Assert.NotEmpty(visible);
        foreach (var b in visible)
        {
            Assert.True(b.HasVisibleApps);
            Assert.True(b.IsExpanded, "Non-empty search auto-expands matching bundles");
            Assert.Contains(b.VisibleApps, a =>
                a.Name.Contains("firefox", StringComparison.OrdinalIgnoreCase)
                || a.Description.Contains("firefox", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void SearchText_Nonexistent_VisibleBundlesEmpty()
    {
        var engine = new FakeEngine();
        var vm = new MacAppInstallerViewModel(engine);

        vm.SearchText = "zzzxyxxxnoooopesuchthingexists";

        Assert.Empty(vm.VisibleBundles);
    }

    // ── 6. Search cleared → restore initial state ─────────────────

    [Fact]
    public void SearchText_ClearedFromNonEmpty_RestoresInitialExpansionState()
    {
        var engine = new FakeEngine();
        var vm = new MacAppInstallerViewModel(engine);

        // Expand everything via search
        vm.SearchText = "a";
        // Then clear — should restore (first expanded, rest collapsed).
        vm.SearchText = "";

        Assert.True(vm.AllBundles[0].IsExpanded);
        for (int i = 1; i < vm.AllBundles.Count; i++)
            Assert.False(vm.AllBundles[i].IsExpanded);
    }

    // ── 7. ScanCommand calls engine.ScanAsync ─────────────────────

    [Fact]
    public async Task ScanCommand_CallsEngineScan()
    {
        var engine = new FakeEngine { ScanSuccess = true };
        var vm = new MacAppInstallerViewModel(engine);

        await vm.ScanAsync();

        Assert.Equal(1, engine.ScanCallCount);
        Assert.False(vm.IsBusy);
    }

    // ── 8. Scan mirrors IsInstalled from shared catalog ───────────

    [Fact]
    public async Task ScanCommand_MirrorsIsInstalledFromCatalog_IntoAppVMs()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OnScan = () =>
            {
                var firstApp = MacAppBundles.AllBundles[0].Apps[0];
                firstApp.IsInstalled = true;
            },
        };
        var vm = new MacAppInstallerViewModel(engine);

        await vm.ScanAsync();

        var firstAppVM = vm.AllBundles[0].Apps[0];
        Assert.True(firstAppVM.IsInstalled);
    }

    // ── 9. Scan recomputes stat counts ────────────────────────────

    [Fact]
    public async Task ScanCommand_RecomputesInstalledAndAvailable()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OnScan = () =>
            {
                var toMark = MacAppBundles.AllBundles.SelectMany(b => b.Apps).Take(3).ToList();
                foreach (var a in toMark) a.IsInstalled = true;
            },
        };
        var vm = new MacAppInstallerViewModel(engine);
        var totalBefore = vm.TotalAppsCount;

        await vm.ScanAsync();

        Assert.Equal(3, vm.InstalledCount);
        Assert.Equal(totalBefore - 3, vm.AvailableCount);
        Assert.Equal("3", vm.InstalledDisplay);
        Assert.Equal((totalBefore - 3).ToString(), vm.AvailableDisplay);
    }

    // ── 10. InstallSelected builds install:<id> plan ──────────────

    [Fact]
    public async Task InstallSelected_BuildsCorrectItemIds_ForSelectedApps()
    {
        var engine = new FakeEngine { ScanSuccess = true, OptimizeSuccess = true };
        var vm = new MacAppInstallerViewModel(engine);

        // Select the first two not-installed apps.
        var app1 = vm.AllBundles[0].Apps[0];
        var app2 = vm.AllBundles[0].Apps[1];
        app1.IsSelected = true;
        app2.IsSelected = true;

        await vm.InstallSelectedAsync();

        Assert.NotNull(engine.LastOptimizePlan);
        var ids = engine.LastOptimizePlan!.SelectedItemIds.ToList();
        Assert.Contains($"install:{app1.Id}", ids);
        Assert.Contains($"install:{app2.Id}", ids);
    }

    // ── 11. InstallSelected ignores already-installed apps ────────

    [Fact]
    public async Task InstallSelected_IgnoresAlreadyInstalledApps()
    {
        var engine = new FakeEngine { ScanSuccess = true, OptimizeSuccess = true };
        var vm = new MacAppInstallerViewModel(engine);
        var app = vm.AllBundles[0].Apps[0];
        // Set IsInstalled true AFTER Select → setter clears selection.
        app.IsSelected = true;
        app.IsInstalled = true;
        Assert.False(app.IsSelected);

        await vm.InstallSelectedAsync();

        // Either plan not sent OR plan has no install:<this app> item.
        if (engine.LastOptimizePlan is not null)
            Assert.DoesNotContain($"install:{app.Id}", engine.LastOptimizePlan.SelectedItemIds);
    }

    // ── 12. InstallSelected auto-rescans after success ────────────

    [Fact]
    public async Task InstallSelected_TriggersAutoRescan_OnSuccess()
    {
        var engine = new FakeEngine { ScanSuccess = true, OptimizeSuccess = true };
        var vm = new MacAppInstallerViewModel(engine);
        var app = vm.AllBundles[0].Apps[0];
        app.IsSelected = true;

        await vm.InstallSelectedAsync();

        // One for the install-triggered rescan — scan wasn't invoked before.
        Assert.Equal(1, engine.ScanCallCount);
    }

    // ── 13. InstallSelected clears IsSelected after completion ────

    [Fact]
    public async Task InstallSelected_ClearsIsSelectedOnAllApps_AfterCompletion()
    {
        var engine = new FakeEngine { ScanSuccess = true, OptimizeSuccess = true };
        var vm = new MacAppInstallerViewModel(engine);
        var app1 = vm.AllBundles[0].Apps[0];
        var app2 = vm.AllBundles[0].Apps[1];
        app1.IsSelected = true;
        app2.IsSelected = true;
        Assert.Equal(2, vm.SelectedCount);

        await vm.InstallSelectedAsync();

        Assert.False(app1.IsSelected);
        Assert.False(app2.IsSelected);
        Assert.Equal(0, vm.SelectedCount);
        Assert.False(vm.HasSelection);
    }

    // ── 14. SelectedCount recomputes on toggle ────────────────────

    [Fact]
    public void SelectedCount_RecomputesOnAppVMToggle()
    {
        var engine = new FakeEngine();
        var vm = new MacAppInstallerViewModel(engine);

        Assert.Equal(0, vm.SelectedCount);

        var a = vm.AllBundles[0].Apps[0];
        var b = vm.AllBundles[0].Apps[1];
        a.IsSelected = true;
        Assert.Equal(1, vm.SelectedCount);
        b.IsSelected = true;
        Assert.Equal(2, vm.SelectedCount);
        a.IsSelected = false;
        Assert.Equal(1, vm.SelectedCount);
    }

    // ── 15. SelectedSummaryDisplay breaks down by source ──────────

    [Fact]
    public void SelectedSummaryDisplay_IncludesPerSourceBreakdown()
    {
        var engine = new FakeEngine();
        var vm = new MacAppInstallerViewModel(engine);

        // Select one formula + one cask from the catalog.
        var allApps = vm.AllBundles.SelectMany(b => b.Apps).ToList();
        var formula = allApps.First(a => a.PackageSource == MacPackageSource.BrewFormula);
        var cask = allApps.First(a => a.PackageSource == MacPackageSource.BrewCask);
        formula.IsSelected = true;
        cask.IsSelected = true;

        var disp = vm.SelectedSummaryDisplay;
        Assert.Contains("2", disp);
        Assert.Contains("formula: 1", disp);
        Assert.Contains("cask: 1", disp);
    }

    // ── 16. BundleVM.InstalledCount refreshes when app flips ─────

    [Fact]
    public void BundleVM_InstalledCountRecomputes_WhenAppInstalledChanges()
    {
        var engine = new FakeEngine();
        var vm = new MacAppInstallerViewModel(engine);
        var bundle = vm.AllBundles[0];
        var initial = bundle.InstalledCount;

        bundle.Apps[0].IsInstalled = true;

        Assert.Equal(initial + 1, bundle.InstalledCount);
        Assert.Equal(bundle.TotalCount - (initial + 1), bundle.AvailableCount);
    }

    // ── 17. Error handling: engine throws on scan ─────────────────

    [Fact]
    public async Task ScanCommand_SetsErrorMessage_OnException()
    {
        var engine = new FakeEngine { ScanThrows = true };
        var vm = new MacAppInstallerViewModel(engine);

        await vm.ScanAsync();

        Assert.True(vm.HasError);
        Assert.False(vm.IsBusy);
    }

    // ── 18. Cancel during install ─────────────────────────────────

    [Fact]
    public async Task CancelCommand_CancelsInFlightInstall()
    {
        var engine = new FakeEngine { SimulateLongOptimize = true, OptimizeSuccess = true };
        var vm = new MacAppInstallerViewModel(engine);
        vm.AllBundles[0].Apps[0].IsSelected = true;

        var task = vm.InstallSelectedAsync();
        await Task.Delay(50);
        Assert.True(vm.IsBusy, "VM should be busy mid-flight");

        vm.Cancel();

        await task;
        Assert.False(vm.IsBusy);
    }

    // ── 19. AppVM: installed app cannot be selected ───────────────

    [Fact]
    public void AppVM_InstalledApp_CannotBeSelected()
    {
        var engine = new FakeEngine();
        var vm = new MacAppInstallerViewModel(engine);
        var app = vm.AllBundles[0].Apps[0];
        app.IsInstalled = true;

        app.IsSelected = true;

        Assert.False(app.IsSelected); // setter clears when installed
    }

    // ── 20. Search state-machine transitions ──────────────────────

    [Fact]
    public void SearchText_StateMachine_InitialNonEmptyCleared()
    {
        var engine = new FakeEngine();
        var vm = new MacAppInstallerViewModel(engine);

        // Manually collapse the default-expanded first bundle to confirm
        // restore behavior is driven by the state machine, not a no-op.
        vm.AllBundles[0].IsExpanded = false;
        vm.AllBundles[2].IsExpanded = true;

        vm.SearchText = "a";
        var anyExpandedDuring = vm.AllBundles.Any(b => b.IsExpanded);
        Assert.True(anyExpandedDuring);

        vm.SearchText = "";
        Assert.True(vm.AllBundles[0].IsExpanded);
        for (int i = 1; i < vm.AllBundles.Count; i++)
            Assert.False(vm.AllBundles[i].IsExpanded);
    }

    // ── 21. Source pill visibility — formula vs cask ──────────────

    [Fact]
    public void AppVM_SourcePillVisibility_MatchesPackageSource()
    {
        var engine = new FakeEngine();
        var vm = new MacAppInstallerViewModel(engine);

        var formulaApp = vm.AllBundles.SelectMany(b => b.Apps)
            .First(a => a.PackageSource == MacPackageSource.BrewFormula);
        var caskApp = vm.AllBundles.SelectMany(b => b.Apps)
            .First(a => a.PackageSource == MacPackageSource.BrewCask);

        Assert.True(formulaApp.IsFormulaPillVisible);
        Assert.False(formulaApp.IsCaskPillVisible);

        Assert.False(caskApp.IsFormulaPillVisible);
        Assert.True(caskApp.IsCaskPillVisible);

        // Once installed, neither pill shows (green INSTALLED pill takes over).
        formulaApp.IsInstalled = true;
        Assert.False(formulaApp.IsFormulaPillVisible);
        Assert.False(formulaApp.IsCaskPillVisible);
    }

    // ── Helpers ────────────────────────────────────────────────────

    /// <summary>Lightweight in-memory test engine — implements the VM's adapter contract.</summary>
    private sealed class FakeEngine : IMacAppInstallerEngine
    {
        public int ScanCallCount;
        public bool ScanSuccess = true;
        public bool ScanThrows;
        public string? BlockedReason = null;
        public Action? OnScan;
        public OptimizationPlan? LastOptimizePlan;
        public bool OptimizeSuccess = true;
        public bool SimulateLongOptimize;

        public string Id => "mac-app-installer";

        public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
        {
            ScanCallCount++;
            await Task.Yield();
            if (ScanThrows) throw new InvalidOperationException("boom");
            OnScan?.Invoke();
            return ScanSuccess
                ? new ScanResult(Id, true, 0, 0, null)
                : new ScanResult(Id, false, 0, 0, BlockedReason);
        }

        public async Task<OptimizationResult> OptimizeAsync(
            OptimizationPlan plan,
            IProgress<TaskProgress>? progress,
            CancellationToken ct)
        {
            LastOptimizePlan = plan;
            progress?.Report(new TaskProgress(Id, 0, "starting"));
            if (SimulateLongOptimize)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
            }
            else
            {
                await Task.Yield();
            }
            progress?.Report(new TaskProgress(Id, 100, "complete"));
            var operationId = Guid.NewGuid().ToString("N")[..8];
            return new OptimizationResult(Id, operationId, OptimizeSuccess, plan.SelectedItemIds?.Count ?? 0, 0, TimeSpan.Zero);
        }
    }
}
