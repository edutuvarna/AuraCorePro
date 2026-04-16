using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.Application;
using AuraCore.Module.LinuxAppInstaller;
using AuraCore.Module.LinuxAppInstaller.Models;
using AuraCore.UI.Avalonia.ViewModels;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.ViewModels;

/// <summary>
/// Phase 4.3.5 Linux App Installer VM unit tests.
/// Uses a hand-rolled <see cref="ILinuxAppInstallerEngine"/> fake — matches the
/// DockerCleaner / SnapFlatpakCleaner / KernelCleaner / JournalCleaner FakeEngine
/// style (no Moq dependency in the test project).
///
/// IMPORTANT: <see cref="LinuxAppBundles.AllBundles"/> is a shared static catalog
/// and <see cref="LinuxBundleApp.IsInstalled"/> is mutable. Each test that
/// exercises installed state resets the catalog via <see cref="ResetCatalog"/>
/// in a using-finally so mutation doesn't leak between tests.
/// </summary>
public class LinuxAppInstallerViewModelTests : IDisposable
{
    public LinuxAppInstallerViewModelTests() => ResetCatalog();
    public void Dispose() => ResetCatalog();

    private static void ResetCatalog()
    {
        foreach (var b in LinuxAppBundles.AllBundles)
            foreach (var a in b.Apps)
                a.IsInstalled = false;
    }

    // ── 1. Ctor ────────────────────────────────────────────────────

    [Fact]
    public void Ctor_DoesNotThrow_WithValidInterface()
    {
        var engine = new FakeEngine();
        var vm = new LinuxAppInstallerViewModel(engine);
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
            new LinuxAppInstallerViewModel((ILinuxAppInstallerEngine)null!));
        Assert.Throws<ArgumentNullException>(() =>
            new LinuxAppInstallerViewModel((LinuxAppInstallerModule)null!));
    }

    [Fact]
    public void Ctor_AcceptsConcreteModule_ViaAdapter()
    {
        var module = new LinuxAppInstallerModule();
        var vm = new LinuxAppInstallerViewModel(module);
        Assert.NotNull(vm);
    }

    // ── 2. AllBundles populated from static catalog ───────────────

    [Fact]
    public void Ctor_PopulatesAllBundles_FromStaticCatalog()
    {
        var engine = new FakeEngine();
        var vm = new LinuxAppInstallerViewModel(engine);

        Assert.Equal(LinuxAppBundles.AllBundles.Count, vm.AllBundles.Count);
        Assert.True(vm.TotalAppsCount > 0, "Total apps count should be derived from the catalog");
        Assert.Equal(LinuxAppBundles.AllBundles.Sum(b => b.Apps.Count), vm.TotalAppsCount);
    }

    // ── 3. Initial expansion: first bundle expanded, others collapsed ─

    [Fact]
    public void Ctor_SetsFirstBundleExpanded_AndRestCollapsed()
    {
        var engine = new FakeEngine();
        var vm = new LinuxAppInstallerViewModel(engine);

        Assert.True(vm.AllBundles[0].IsExpanded);
        for (int i = 1; i < vm.AllBundles.Count; i++)
            Assert.False(vm.AllBundles[i].IsExpanded);
    }

    // ── 4. SearchText empty → VisibleBundles == AllBundles ────────

    [Fact]
    public void SearchTextEmpty_VisibleBundlesMatchesAllBundles()
    {
        var engine = new FakeEngine();
        var vm = new LinuxAppInstallerViewModel(engine);

        Assert.Equal("", vm.SearchText);
        var visible = vm.VisibleBundles.ToList();
        Assert.Equal(vm.AllBundles.Count, visible.Count);
    }

    // ── 5. Non-empty search filters correctly + auto-expands matching ─

    [Fact]
    public void SearchText_NonEmpty_FiltersByNameOrDescription()
    {
        var engine = new FakeEngine();
        var vm = new LinuxAppInstallerViewModel(engine);

        vm.SearchText = "firefox";

        var visible = vm.VisibleBundles.ToList();
        Assert.NotEmpty(visible);
        // Every visible bundle has at least one firefox-matching app
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
        var vm = new LinuxAppInstallerViewModel(engine);

        vm.SearchText = "zzzxyxxxnoooopesuchthingexists";

        Assert.Empty(vm.VisibleBundles);
    }

    // ── 6. Search cleared → restore initial state ─────────────────

    [Fact]
    public void SearchText_ClearedFromNonEmpty_RestoresInitialExpansionState()
    {
        var engine = new FakeEngine();
        var vm = new LinuxAppInstallerViewModel(engine);

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
        var vm = new LinuxAppInstallerViewModel(engine);

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
                // Engine normally mutates the static catalog — simulate a couple flips.
                var firstApp = LinuxAppBundles.AllBundles[0].Apps[0];
                firstApp.IsInstalled = true;
            },
        };
        var vm = new LinuxAppInstallerViewModel(engine);

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
                // Mark exactly 3 apps as installed across the catalog.
                var toMark = LinuxAppBundles.AllBundles.SelectMany(b => b.Apps).Take(3).ToList();
                foreach (var a in toMark) a.IsInstalled = true;
            },
        };
        var vm = new LinuxAppInstallerViewModel(engine);
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
        var vm = new LinuxAppInstallerViewModel(engine);

        // Select the first two not-installed apps (everything is uninstalled at start).
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
        var vm = new LinuxAppInstallerViewModel(engine);
        var app = vm.AllBundles[0].Apps[0];
        // Set IsInstalled true AFTER Select attempt → setter clears selection.
        app.IsSelected = true;
        app.IsInstalled = true;
        // Ensure selection auto-cleared
        Assert.False(app.IsSelected);

        // Now try InstallSelected with the remaining selection (none).
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
        var vm = new LinuxAppInstallerViewModel(engine);
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
        var vm = new LinuxAppInstallerViewModel(engine);
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
        var vm = new LinuxAppInstallerViewModel(engine);

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
        var vm = new LinuxAppInstallerViewModel(engine);

        // Select one apt + one snap + one flatpak from the catalog.
        var allApps = vm.AllBundles.SelectMany(b => b.Apps).ToList();
        var apt = allApps.First(a => a.PackageSource == LinuxPackageSource.Apt);
        var snap = allApps.First(a => a.PackageSource == LinuxPackageSource.Snap);
        var flatpak = allApps.First(a => a.PackageSource == LinuxPackageSource.Flatpak);
        apt.IsSelected = true;
        snap.IsSelected = true;
        flatpak.IsSelected = true;

        var disp = vm.SelectedSummaryDisplay;
        Assert.Contains("3", disp);
        Assert.Contains("apt: 1", disp);
        Assert.Contains("snap: 1", disp);
        Assert.Contains("flatpak: 1", disp);
    }

    // ── 16. BundleVM.InstalledCount refreshes when app flips ─────

    [Fact]
    public void BundleVM_InstalledCountRecomputes_WhenAppInstalledChanges()
    {
        var engine = new FakeEngine();
        var vm = new LinuxAppInstallerViewModel(engine);
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
        var vm = new LinuxAppInstallerViewModel(engine);

        await vm.ScanAsync();

        Assert.True(vm.HasError);
        Assert.False(vm.IsBusy);
    }

    // ── 18. Cancel during install ─────────────────────────────────

    [Fact]
    public async Task CancelCommand_CancelsInFlightInstall()
    {
        var engine = new FakeEngine { SimulateLongOptimize = true, OptimizeSuccess = true };
        var vm = new LinuxAppInstallerViewModel(engine);
        vm.AllBundles[0].Apps[0].IsSelected = true;

        var task = vm.InstallSelectedAsync();
        await Task.Delay(50);
        Assert.True(vm.IsBusy, "VM should be busy mid-flight");

        vm.Cancel();

        await task; // VM swallows OCE
        Assert.False(vm.IsBusy);
    }

    // ── 19. AppVM: installed app cannot be selected ───────────────

    [Fact]
    public void AppVM_InstalledApp_CannotBeSelected()
    {
        var engine = new FakeEngine();
        var vm = new LinuxAppInstallerViewModel(engine);
        var app = vm.AllBundles[0].Apps[0];
        app.IsInstalled = true;

        app.IsSelected = true;

        Assert.False(app.IsSelected); // setter clears when installed
    }

    // ── 20. Search transitions (empty → non-empty → empty) ────────

    [Fact]
    public void SearchText_StateMachine_InitialNonEmptyCleared()
    {
        var engine = new FakeEngine();
        var vm = new LinuxAppInstallerViewModel(engine);

        // Manually collapse the default-expanded first bundle to confirm
        // restore behavior is driven by the state machine, not a no-op.
        vm.AllBundles[0].IsExpanded = false;
        vm.AllBundles[2].IsExpanded = true;

        vm.SearchText = "a";
        // Many bundles are likely expanded now (any that contain apps matching "a")
        var anyExpandedDuring = vm.AllBundles.Any(b => b.IsExpanded);
        Assert.True(anyExpandedDuring);

        vm.SearchText = "";
        // Back to initial state
        Assert.True(vm.AllBundles[0].IsExpanded);
        for (int i = 1; i < vm.AllBundles.Count; i++)
            Assert.False(vm.AllBundles[i].IsExpanded);
    }

    // ── Helpers ────────────────────────────────────────────────────

    /// <summary>Lightweight in-memory test engine — implements the VM's adapter contract.</summary>
    private sealed class FakeEngine : ILinuxAppInstallerEngine
    {
        public int ScanCallCount;
        public bool ScanSuccess = true;
        public bool ScanThrows;
        public string? BlockedReason = null;
        public Action? OnScan;
        public OptimizationPlan? LastOptimizePlan;
        public bool OptimizeSuccess = true;
        public bool SimulateLongOptimize;

        public string Id => "linux-app-installer";

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
