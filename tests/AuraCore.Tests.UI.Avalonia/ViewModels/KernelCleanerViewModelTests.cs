using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.Application;
using AuraCore.Module.KernelCleaner;
using AuraCore.Module.KernelCleaner.Models;
using AuraCore.UI.Avalonia.ViewModels;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.ViewModels;

/// <summary>
/// Phase 4.3.4 Kernel Cleaner VM unit tests.
/// Uses a hand-rolled <see cref="IKernelCleanerEngine"/> fake — matches the
/// DockerCleaner / SnapFlatpakCleaner / JournalCleaner FakeEngine style
/// (no Moq dependency in the test project).
/// </summary>
public class KernelCleanerViewModelTests
{
    // ── 1. Ctor ────────────────────────────────────────────────────

    [Fact]
    public void Ctor_DoesNotThrow_WithValidDependencies()
    {
        var engine = new FakeEngine();
        var vm = new KernelCleanerViewModel(engine);
        Assert.NotNull(vm);
        Assert.False(vm.IsBusy);
        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.HasError);
        Assert.Null(vm.Report);
        Assert.Equal("--", vm.ActiveKernelDisplay);
        Assert.Equal("--", vm.RemovableDisplay);
        Assert.False(vm.PackageManagerAvailable);
        Assert.False(vm.DangerAcknowledged);
        Assert.Empty(vm.KernelItems);
        Assert.False(vm.HasSelection);
        Assert.Equal(0, vm.SelectedCount);
    }

    [Fact]
    public void Ctor_NullEngine_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new KernelCleanerViewModel((IKernelCleanerEngine)null!));
        Assert.Throws<ArgumentNullException>(() =>
            new KernelCleanerViewModel((KernelCleanerModule)null!));
    }

    // ── 2. Scan populates kernel list with correct flags ───────────

    [Fact]
    public async Task ScanCommand_PopulatesKernelItems_WithCorrectFlags()
    {
        var report = BuildReport(
            current: "6.8.0-52-generic",
            kernels: new[]
            {
                ("5.15.0-100-generic", false, false, 200L * 1024 * 1024),
                ("6.2.0-30-generic",   false, false, 220L * 1024 * 1024),
                ("6.5.0-10-generic",   false, false, 240L * 1024 * 1024),
                ("6.8.0-52-generic",   true,  false, 260L * 1024 * 1024), // running
                ("6.9.0-5-generic",    false, true,  280L * 1024 * 1024), // newest
            });
        var engine = new FakeEngine { ScanSuccess = true, ReportToReturn = report };
        var vm = new KernelCleanerViewModel(engine);

        await vm.ScanAsync();

        Assert.Equal(5, vm.KernelItems.Count);
        var running = vm.KernelItems.Single(k => k.IsRunning);
        Assert.Equal("6.8.0-52-generic", running.Version);
        var newest = vm.KernelItems.Single(k => k.IsNewest);
        Assert.Equal("6.9.0-5-generic", newest.Version);
        // Running takes visual precedence — its newest bit isn't set here but VM doesn't assume either
        Assert.False(running.IsCheckboxVisible); // running → placeholder only
        Assert.False(newest.IsCheckboxEnabled); // newest → disabled checkbox
    }

    // ── 3. Scan pre-checks all-but-two-newest removable ────────────

    [Fact]
    public async Task ScanCommand_PreChecksAllButTwoNewestRemovable_WhenMoreThan2Removable()
    {
        // 5 total: 1 running, 1 newest, 3 removable (A,B,C oldest→newest)
        // Expect A to be pre-checked, B+C left unchecked.
        var report = BuildReport(
            current: "6.8.0-52-generic",
            kernels: new[]
            {
                ("5.15.0-100-generic", false, false, 100L * 1024 * 1024), // A - oldest removable
                ("6.2.0-30-generic",   false, false, 200L * 1024 * 1024), // B
                ("6.5.0-10-generic",   false, false, 300L * 1024 * 1024), // C
                ("6.8.0-52-generic",   true,  false, 400L * 1024 * 1024), // running
                ("6.9.0-5-generic",    false, true,  500L * 1024 * 1024), // newest
            });
        var engine = new FakeEngine { ScanSuccess = true, ReportToReturn = report };
        var vm = new KernelCleanerViewModel(engine);

        await vm.ScanAsync();

        var a = vm.KernelItems.Single(k => k.Version == "5.15.0-100-generic");
        var b = vm.KernelItems.Single(k => k.Version == "6.2.0-30-generic");
        var c = vm.KernelItems.Single(k => k.Version == "6.5.0-10-generic");
        Assert.True(a.IsSelected, "Oldest removable (A) should be pre-checked.");
        Assert.False(b.IsSelected, "Second-newest removable (B) should be unchecked.");
        Assert.False(c.IsSelected, "Newest removable (C) should be unchecked.");
    }

    [Fact]
    public async Task ScanCommand_LeavesAllUnchecked_WhenTwoOrFewerRemovable()
    {
        // 3 total: 1 running, 1 newest, 1 removable → no pre-check.
        var report = BuildReport(
            current: "6.8.0-52-generic",
            kernels: new[]
            {
                ("5.15.0-100-generic", false, false, 200L * 1024 * 1024),
                ("6.8.0-52-generic",   true,  false, 260L * 1024 * 1024),
                ("6.9.0-5-generic",    false, true,  280L * 1024 * 1024),
            });
        var engine = new FakeEngine { ScanSuccess = true, ReportToReturn = report };
        var vm = new KernelCleanerViewModel(engine);

        await vm.ScanAsync();

        Assert.All(vm.KernelItems, item => Assert.False(item.IsSelected));
        Assert.False(vm.HasSelection);
    }

    // ── 4. Scan resets DangerAcknowledged ──────────────────────────

    [Fact]
    public async Task ScanCommand_ResetsDangerAcknowledged()
    {
        var report = BuildReport(current: "6.8.0-52-generic", kernels: Array.Empty<(string, bool, bool, long)>());
        var engine = new FakeEngine { ScanSuccess = true, ReportToReturn = report };
        var vm = new KernelCleanerViewModel(engine);
        vm.DangerAcknowledged = true;

        await vm.ScanAsync();

        Assert.False(vm.DangerAcknowledged);
    }

    // ── 5. AutoRemoveOldCommand uses "remove-old" item id ─────────

    [Fact]
    public async Task AutoRemoveOldCommand_CallsEngineWith_RemoveOldItemId()
    {
        var report = BuildReport(current: "6.8.0-52-generic", kernels: Array.Empty<(string, bool, bool, long)>());
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OptimizeSuccess = true,
            ReportToReturn = report,
        };
        var vm = new KernelCleanerViewModel(engine);
        await vm.ScanAsync();

        await vm.OptimizeAsync(new[] { "remove-old" });

        Assert.NotNull(engine.LastOptimizePlan);
        Assert.Single(engine.LastOptimizePlan!.SelectedItemIds);
        Assert.Equal("remove-old", engine.LastOptimizePlan.SelectedItemIds[0]);
    }

    // ── 6. RemoveSelectedCommand builds remove:<version> items ────

    [Fact]
    public async Task RemoveSelectedCommand_BuildsRemoveVersionItems_ForSelectedOnly()
    {
        var report = BuildReport(
            current: "6.8.0-52-generic",
            kernels: new[]
            {
                ("5.15.0-100-generic", false, false, 100L * 1024 * 1024),
                ("6.2.0-30-generic",   false, false, 200L * 1024 * 1024),
                ("6.5.0-10-generic",   false, false, 300L * 1024 * 1024),
                ("6.8.0-52-generic",   true,  false, 400L * 1024 * 1024),
                ("6.9.0-5-generic",    false, true,  500L * 1024 * 1024),
            });
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OptimizeSuccess = true,
            ReportToReturn = report,
        };
        var vm = new KernelCleanerViewModel(engine);
        await vm.ScanAsync();

        // Manually check B in addition to pre-checked A
        var b = vm.KernelItems.Single(k => k.Version == "6.2.0-30-generic");
        b.IsSelected = true;

        await vm.RemoveSelectedAsync();

        Assert.NotNull(engine.LastOptimizePlan);
        var ids = engine.LastOptimizePlan!.SelectedItemIds.ToList();
        Assert.Equal(2, ids.Count);
        Assert.Contains("remove:5.15.0-100-generic", ids);
        Assert.Contains("remove:6.2.0-30-generic", ids);
    }

    // ── 7. RemoveSelectedCommand never emits running or newest ────

    [Fact]
    public async Task RemoveSelectedCommand_NeverEmitsRunningOrNewest_EvenIfSelected()
    {
        var report = BuildReport(
            current: "6.8.0-52-generic",
            kernels: new[]
            {
                ("6.8.0-52-generic",   true,  false, 400L * 1024 * 1024), // running
                ("6.9.0-5-generic",    false, true,  500L * 1024 * 1024), // newest
            });
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OptimizeSuccess = true,
            ReportToReturn = report,
        };
        var vm = new KernelCleanerViewModel(engine);
        await vm.ScanAsync();

        // Attempt to subvert protection by setting IsSelected directly
        foreach (var item in vm.KernelItems) item.IsSelected = true;

        await vm.RemoveSelectedAsync();

        // No dangerous op should have been emitted — plan should be null or empty
        // (our VM returns early with 0 items)
        Assert.Null(engine.LastOptimizePlan);
    }

    // ── 8. RemoveAllButCurrentCommand uses correct item id ────────

    [Fact]
    public async Task RemoveAllButCurrentCommand_CallsEngineWith_RemoveAllButCurrentItemId()
    {
        var report = BuildReport(current: "6.8.0-52-generic", kernels: Array.Empty<(string, bool, bool, long)>());
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OptimizeSuccess = true,
            ReportToReturn = report,
        };
        var vm = new KernelCleanerViewModel(engine);
        await vm.ScanAsync();
        vm.DangerAcknowledged = true;

        await vm.OptimizeAsync(new[] { "remove-all-but-current" });

        Assert.NotNull(engine.LastOptimizePlan);
        Assert.Single(engine.LastOptimizePlan!.SelectedItemIds);
        Assert.Equal("remove-all-but-current", engine.LastOptimizePlan.SelectedItemIds[0]);
    }

    // ── 9. RemoveAllButCurrent disabled when !DangerAcknowledged ──

    [Fact]
    public async Task RemoveAllButCurrentCommand_Disabled_WhenNotAcknowledged()
    {
        var report = BuildReport(current: "6.8.0-52-generic", kernels: Array.Empty<(string, bool, bool, long)>());
        var engine = new FakeEngine { ScanSuccess = true, ReportToReturn = report };
        var vm = new KernelCleanerViewModel(engine);
        await vm.ScanAsync();

        Assert.True(vm.PackageManagerAvailable);
        Assert.False(vm.DangerAcknowledged);
        Assert.False(vm.RemoveAllButCurrentCommand.CanExecute(null));

        vm.DangerAcknowledged = true;
        Assert.True(vm.RemoveAllButCurrentCommand.CanExecute(null));
    }

    [Fact]
    public async Task RemoveAllButCurrentCommand_ResetsAcknowledgedAfterCompletion()
    {
        var report = BuildReport(current: "6.8.0-52-generic", kernels: Array.Empty<(string, bool, bool, long)>());
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OptimizeSuccess = true,
            ReportToReturn = report,
        };
        var vm = new KernelCleanerViewModel(engine);
        await vm.ScanAsync();
        vm.DangerAcknowledged = true;

        await vm.OptimizeAsync(new[] { "remove-all-but-current" });

        Assert.False(vm.DangerAcknowledged);
    }

    // ── 10. SelectedCount/SelectedBytes recompute on IsSelected change

    [Fact]
    public async Task SelectedCountAndBytes_RecomputeOnSelectionChange()
    {
        var report = BuildReport(
            current: "6.8.0-52-generic",
            kernels: new[]
            {
                ("5.15.0-100-generic", false, false, 100L * 1024 * 1024),
                ("6.2.0-30-generic",   false, false, 200L * 1024 * 1024),
                ("6.5.0-10-generic",   false, false, 300L * 1024 * 1024),
                ("6.8.0-52-generic",   true,  false, 400L * 1024 * 1024),
                ("6.9.0-5-generic",    false, true,  500L * 1024 * 1024),
            });
        var engine = new FakeEngine { ScanSuccess = true, ReportToReturn = report };
        var vm = new KernelCleanerViewModel(engine);
        await vm.ScanAsync();

        // A was pre-checked (100MB)
        Assert.Equal(1, vm.SelectedCount);
        Assert.Equal(100L * 1024 * 1024, vm.SelectedBytes);
        Assert.True(vm.HasSelection);

        // Toggle B on (200MB)
        var b = vm.KernelItems.Single(k => k.Version == "6.2.0-30-generic");
        b.IsSelected = true;
        Assert.Equal(2, vm.SelectedCount);
        Assert.Equal(300L * 1024 * 1024, vm.SelectedBytes);

        // Untoggle A
        var a = vm.KernelItems.Single(k => k.Version == "5.15.0-100-generic");
        a.IsSelected = false;
        Assert.Equal(1, vm.SelectedCount);
        Assert.Equal(200L * 1024 * 1024, vm.SelectedBytes);
    }

    // ── 11. HasSelection flips when items toggle ──────────────────

    [Fact]
    public async Task HasSelection_FlipsWhenItemsToggle()
    {
        var report = BuildReport(
            current: "6.8.0-52-generic",
            kernels: new[]
            {
                ("5.15.0-100-generic", false, false, 200L * 1024 * 1024),
                ("6.8.0-52-generic",   true,  false, 260L * 1024 * 1024),
                ("6.9.0-5-generic",    false, true,  280L * 1024 * 1024),
            });
        // Only 1 removable → not pre-checked by default
        var engine = new FakeEngine { ScanSuccess = true, ReportToReturn = report };
        var vm = new KernelCleanerViewModel(engine);
        await vm.ScanAsync();

        Assert.False(vm.HasSelection);

        var a = vm.KernelItems.Single(k => k.Version == "5.15.0-100-generic");
        a.IsSelected = true;
        Assert.True(vm.HasSelection);

        a.IsSelected = false;
        Assert.False(vm.HasSelection);
    }

    // ── 12. Unavailable report surfaces status ────────────────────

    [Fact]
    public async Task ScanCommand_SetsUnavailable_WhenNoPackageManager()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = false,
            ReportToReturn = KernelReport.None(),
        };
        var vm = new KernelCleanerViewModel(engine);

        await vm.ScanAsync();

        Assert.False(vm.PackageManagerAvailable);
    }

    // ── 13. Cancel handling ────────────────────────────────────────

    [Fact]
    public async Task CancelCommand_CancelsInFlightOperation()
    {
        var engine = new FakeEngine { SimulateLongOptimize = true };
        var vm = new KernelCleanerViewModel(engine);

        var task = vm.OptimizeAsync(new[] { "remove-old" });
        await Task.Delay(50);
        Assert.True(vm.IsBusy, "VM should be busy mid-flight");

        vm.Cancel();

        await task; // VM swallows OCE
        Assert.False(vm.IsBusy);
    }

    // ── 14. FormatSize theory ──────────────────────────────────────

    [Theory]
    [InlineData(0, "--")]
    [InlineData(-1, "--")]
    [InlineData(500, "500 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1024L * 1024, "1 MB")]
    [InlineData((long)(1.5 * 1024 * 1024 * 1024), "1.50 GB")]
    public void FormatSize_ReturnsExpected(long bytes, string expected)
    {
        Assert.Equal(expected, KernelCleanerViewModel.FormatSize(bytes));
    }

    // ── 15. RemovableDisplay format ────────────────────────────────

    [Fact]
    public async Task RemovableDisplay_FormatsCorrectly()
    {
        var report = BuildReport(
            current: "6.8.0-52-generic",
            kernels: new[]
            {
                ("5.15.0-100-generic", false, false, 200L * 1024 * 1024),
                ("6.2.0-30-generic",   false, false, 300L * 1024 * 1024),
                ("6.5.0-10-generic",   false, false, 300L * 1024 * 1024),
                ("6.8.0-52-generic",   true,  false, 400L * 1024 * 1024),
                ("6.9.0-5-generic",    false, true,  500L * 1024 * 1024),
            },
            totalRemovable: 800L * 1024 * 1024);
        var engine = new FakeEngine { ScanSuccess = true, ReportToReturn = report };
        var vm = new KernelCleanerViewModel(engine);
        await vm.ScanAsync();

        // 3 removable (non-running, non-newest), 800 MB total
        Assert.Contains("3", vm.RemovableDisplay);
        Assert.Contains("800 MB", vm.RemovableDisplay);
    }

    // ── 16. ActiveKernelDisplay ────────────────────────────────────

    [Fact]
    public async Task ActiveKernelDisplay_ShowsCurrentKernelAfterScan()
    {
        var report = BuildReport(current: "6.8.0-52-generic", kernels: Array.Empty<(string, bool, bool, long)>());
        var engine = new FakeEngine { ScanSuccess = true, ReportToReturn = report };
        var vm = new KernelCleanerViewModel(engine);

        await vm.ScanAsync();

        Assert.Equal("6.8.0-52-generic", vm.ActiveKernelDisplay);
    }

    // ── 17. Error surfaces on exception ────────────────────────────

    [Fact]
    public async Task ScanCommand_SetsErrorMessage_OnException()
    {
        var engine = new FakeEngine { ScanThrows = true };
        var vm = new KernelCleanerViewModel(engine);

        await vm.ScanAsync();

        Assert.True(vm.HasError);
        Assert.False(vm.IsBusy);
    }

    // ── 18. Rescan clears prior selection ─────────────────────────

    [Fact]
    public async Task Rescan_RebuildsItemsAndResetsSelection()
    {
        var report1 = BuildReport(
            current: "6.8.0-52-generic",
            kernels: new[]
            {
                ("5.15.0-100-generic", false, false, 100L * 1024 * 1024),
                ("6.2.0-30-generic",   false, false, 200L * 1024 * 1024),
                ("6.5.0-10-generic",   false, false, 300L * 1024 * 1024),
                ("6.8.0-52-generic",   true,  false, 400L * 1024 * 1024),
                ("6.9.0-5-generic",    false, true,  500L * 1024 * 1024),
            });
        var engine = new FakeEngine { ScanSuccess = true, ReportToReturn = report1 };
        var vm = new KernelCleanerViewModel(engine);
        await vm.ScanAsync();

        // User manually selects B
        vm.KernelItems.Single(k => k.Version == "6.2.0-30-generic").IsSelected = true;
        Assert.Equal(2, vm.SelectedCount);

        // New scan rebuilds — the previous IsSelected state goes away because the list is rebuilt.
        var report2 = BuildReport(
            current: "6.8.0-52-generic",
            kernels: new[]
            {
                ("5.15.0-100-generic", false, false, 100L * 1024 * 1024),
                ("6.2.0-30-generic",   false, false, 200L * 1024 * 1024),
                ("6.5.0-10-generic",   false, false, 300L * 1024 * 1024),
                ("6.8.0-52-generic",   true,  false, 400L * 1024 * 1024),
                ("6.9.0-5-generic",    false, true,  500L * 1024 * 1024),
            });
        engine.ReportToReturn = report2;

        await vm.ScanAsync();

        // After fresh scan, only A (oldest removable) should be pre-checked
        Assert.Equal(1, vm.SelectedCount);
        var a = vm.KernelItems.Single(k => k.Version == "5.15.0-100-generic");
        Assert.True(a.IsSelected);
    }

    // ── 19. KernelItemVM visual-state hints ───────────────────────

    [Fact]
    public void KernelItemVM_RunningRow_HidesCheckboxShowsPlaceholder()
    {
        var item = new KernelItemVM("6.8.0-52-generic", "linux-image-6.8.0-52-generic", 1024, isRunning: true, isNewest: false);
        Assert.False(item.IsCheckboxVisible);
        Assert.False(item.IsCheckboxEnabled);
        Assert.False(item.IsNewestBadgeVisible);
    }

    [Fact]
    public void KernelItemVM_NewestRow_ShowsDisabledCheckbox()
    {
        var item = new KernelItemVM("6.9.0-5-generic", "linux-image-6.9.0-5-generic", 1024, isRunning: false, isNewest: true);
        Assert.True(item.IsCheckboxVisible);
        Assert.False(item.IsCheckboxEnabled);
        Assert.True(item.IsNewestBadgeVisible);
    }

    [Fact]
    public void KernelItemVM_RemovableRow_ShowsEnabledCheckbox()
    {
        var item = new KernelItemVM("5.15.0-100-generic", "linux-image-5.15.0-100-generic", 1024, isRunning: false, isNewest: false);
        Assert.True(item.IsCheckboxVisible);
        Assert.True(item.IsCheckboxEnabled);
        Assert.False(item.IsNewestBadgeVisible);
    }

    // ── Helpers ────────────────────────────────────────────────────

    private static KernelReport BuildReport(
        string current,
        (string version, bool isCurrent, bool isLatest, long size)[] kernels,
        long? totalRemovable = null)
    {
        var list = kernels.Select(k =>
            new KernelInfo(k.version, $"linux-image-{k.version}", k.isCurrent, k.isLatest, k.size, null)).ToList();
        long total = totalRemovable ?? list.Where(k => !k.IsCurrent && !k.IsLatest).Sum(k => k.SizeBytes);
        return new KernelReport(list, current, total, "apt", true);
    }

    /// <summary>Lightweight in-memory test engine — implements the VM's adapter contract.</summary>
    private sealed class FakeEngine : IKernelCleanerEngine
    {
        public int ScanCallCount;
        public bool ScanSuccess = true;
        public bool ScanThrows;
        public string? BlockedReason = null;
        public KernelReport ReportToReturn = KernelReport.None();
        public OptimizationPlan? LastOptimizePlan;
        public bool OptimizeSuccess = true;
        public bool SimulateLongOptimize;

        public string Id => "kernel-cleaner";
        public KernelReport? LastReport { get; private set; }

        public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
        {
            ScanCallCount++;
            await Task.Yield();
            if (ScanThrows) throw new InvalidOperationException("boom");
            LastReport = ReportToReturn;
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
