using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.Application;
using AuraCore.Module.PurgeableSpaceManager;
using AuraCore.Module.PurgeableSpaceManager.Models;
using AuraCore.UI.Avalonia;
using AuraCore.UI.Avalonia.ViewModels;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.ViewModels;

/// <summary>
/// Phase 4.4.2 Purgeable Space Manager VM unit tests. Uses a hand-rolled
/// <see cref="IPurgeableSpaceManagerEngine"/> fake — matches GrubManager /
/// KernelCleaner / DockerCleaner / DnsFlusher FakeEngine style.
/// </summary>
[Collection("Localization")]
public class PurgeableSpaceManagerViewModelTests
{
    // ── 1. Ctor ────────────────────────────────────────────────────

    [Fact]
    public void Ctor_DoesNotThrow_WithValidEngine()
    {
        var engine = new FakeEngine();
        var vm = new PurgeableSpaceManagerViewModel(engine);
        Assert.NotNull(vm);
        Assert.False(vm.IsBusy);
        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.HasError);
        Assert.Null(vm.Report);
    }

    [Fact]
    public void Ctor_NullEngine_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PurgeableSpaceManagerViewModel((IPurgeableSpaceManagerEngine)null!));
        Assert.Throws<ArgumentNullException>(() =>
            new PurgeableSpaceManagerViewModel((PurgeableSpaceManagerModule)null!));
    }

    // ── 2. TriggerInitialScan invokes the engine ──────────────────

    [Fact]
    public async Task TriggerInitialScan_InvokesScanAsync()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            ReportToReturn = BuildReport(totalCapacity: 500_000_000_000, volumeFree: 200_000_000_000,
                containerFree: 150_000_000_000, purgeable: 50_000_000_000, snapshotCount: 2),
        };
        var vm = new PurgeableSpaceManagerViewModel(engine);

        vm.TriggerInitialScan();
        for (int i = 0; i < 50 && engine.ScanCallCount == 0; i++)
            await Task.Yield();
        await WaitUntilNotBusy(vm);

        Assert.True(engine.ScanCallCount >= 1);
    }

    // ── 3. Scan populates byte displays and percents ──────────────

    [Fact]
    public async Task ScanAsync_PopulatesBytesAndPercents()
    {
        // 500GB total, 200GB volume-free, 150GB container-free, 50GB purgeable
        // Used = 500GB - 200GB = 300GB
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            ReportToReturn = BuildReport(
                totalCapacity: 500_000_000_000,
                volumeFree: 200_000_000_000,
                containerFree: 150_000_000_000,
                purgeable: 50_000_000_000,
                snapshotCount: 2),
        };
        var vm = new PurgeableSpaceManagerViewModel(engine);

        await vm.ScanAsync();

        Assert.NotNull(vm.Report);
        Assert.Equal(500_000_000_000, vm.TotalCapacityBytes);
        Assert.Equal(300_000_000_000, vm.UsedBytes);
        Assert.Equal(50_000_000_000, vm.PurgeableBytes);
        Assert.Equal(150_000_000_000, vm.FreeBytes);

        // Percents: 60 / 10 / 30
        Assert.Equal(60.0, vm.UsedPercent, precision: 2);
        Assert.Equal(10.0, vm.PurgeablePercent, precision: 2);
        Assert.Equal(30.0, vm.FreePercent, precision: 2);

        Assert.NotEqual("--", vm.UsedDisplay);
        Assert.NotEqual("--", vm.PurgeableDisplay);
        Assert.NotEqual("--", vm.FreeDisplay);
        Assert.Equal("60%", vm.UsedPercentDisplay);
        Assert.Equal("10%", vm.PurgeablePercentDisplay);
        Assert.Equal("30%", vm.FreePercentDisplay);
        Assert.Equal("2", vm.SnapshotCountDisplay);
    }

    // ── 4. Scan handles TotalCapacity=0 (engine gave nothing) ────

    [Fact]
    public async Task ScanAsync_HandlesZeroTotalCapacity()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            ReportToReturn = new PurgeableReport(
                VolumeFreeBytes: 0, ContainerFreeBytes: 0, PurgeableBytes: 0,
                LocalSnapshotCount: 0, LocalSnapshots: Array.Empty<string>(),
                IsAvailable: true, TotalCapacityBytes: 0),
        };
        var vm = new PurgeableSpaceManagerViewModel(engine);

        await vm.ScanAsync();

        Assert.Equal(0, vm.TotalCapacityBytes);
        Assert.Equal(0, vm.UsedBytes);
        Assert.Equal(0.0, vm.UsedPercent);
        Assert.Equal(0.0, vm.PurgeablePercent);
        Assert.Equal(0.0, vm.FreePercent);
        Assert.Equal("1*,0*,0*", vm.ColumnDefinitions);
    }

    // ── 5. ColumnDefinitions renders proportion string ────────────

    [Fact]
    public async Task ScanAsync_PopulatesColumnDefinitions_ForTypicalProportions()
    {
        // Arrange for 62/20/18 rough split (integer-rounding tolerance)
        // Total = 1000, Used = 620 (via VolumeFree = 380), Purgeable = 200, Free = 180
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            ReportToReturn = BuildReport(
                totalCapacity: 1000,
                volumeFree: 380,
                containerFree: 180,
                purgeable: 200,
                snapshotCount: 0),
        };
        var vm = new PurgeableSpaceManagerViewModel(engine);

        await vm.ScanAsync();

        Assert.Equal("62*,20*,18*", vm.ColumnDefinitions);
    }

    // ── 6. CleanUserCaches command uses "clean-user-caches" itemId

    [Fact]
    public async Task CleanUserCachesCommand_UsesCleanUserCachesItemId()
    {
        var engine = StandardEngine();
        var vm = new PurgeableSpaceManagerViewModel(engine);
        await vm.ScanAsync();

        await vm.RunActionAsync("clean-user-caches");

        Assert.NotNull(engine.LastOptimizePlan);
        Assert.Single(engine.LastOptimizePlan!.SelectedItemIds);
        Assert.Equal("clean-user-caches", engine.LastOptimizePlan.SelectedItemIds[0]);
    }

    // ── 7. RunPeriodic uses "run-periodic" itemId ─────────────────

    [Fact]
    public async Task RunPeriodicCommand_UsesRunPeriodicItemId()
    {
        var engine = StandardEngine();
        var vm = new PurgeableSpaceManagerViewModel(engine);
        await vm.ScanAsync();

        await vm.RunActionAsync("run-periodic");

        Assert.NotNull(engine.LastOptimizePlan);
        Assert.Single(engine.LastOptimizePlan!.SelectedItemIds);
        Assert.Equal("run-periodic", engine.LastOptimizePlan.SelectedItemIds[0]);
    }

    // ── 8. ThinSnapshots uses "thin-snapshots" itemId ─────────────

    [Fact]
    public async Task ThinSnapshotsCommand_UsesThinSnapshotsItemId()
    {
        var engine = StandardEngine();
        var vm = new PurgeableSpaceManagerViewModel(engine);
        await vm.ScanAsync();

        await vm.RunActionAsync("thin-snapshots");

        Assert.NotNull(engine.LastOptimizePlan);
        Assert.Single(engine.LastOptimizePlan!.SelectedItemIds);
        Assert.Equal("thin-snapshots", engine.LastOptimizePlan.SelectedItemIds[0]);
    }

    // ── 9. Post-action re-scan ────────────────────────────────────

    [Fact]
    public async Task Action_TriggersPostActionRescan_OnSuccess()
    {
        var engine = StandardEngine();
        var vm = new PurgeableSpaceManagerViewModel(engine);
        await vm.ScanAsync();
        var scanBefore = engine.ScanCallCount;

        await vm.RunActionAsync("clean-user-caches");

        Assert.True(engine.ScanCallCount > scanBefore,
            "Expected re-scan after successful action to refresh stacked bar + stats");
    }

    // ── 10. Error handling on engine exception ────────────────────

    [Fact]
    public async Task Action_SetsErrorMessage_OnEngineException()
    {
        var engine = StandardEngine();
        engine.ThrowOnOptimize = true;
        var vm = new PurgeableSpaceManagerViewModel(engine);
        await vm.ScanAsync();

        await vm.RunActionAsync("clean-user-caches");

        Assert.True(vm.HasError);
        Assert.NotNull(vm.ErrorMessage);
    }

    // ── 11. Percents sum to ~100 with typical proportions ─────────

    [Fact]
    public async Task ScanAsync_PercentsSumToApproximately100()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            ReportToReturn = BuildReport(
                totalCapacity: 999,
                volumeFree: 400,
                containerFree: 250,
                purgeable: 150,
                snapshotCount: 0),
        };
        var vm = new PurgeableSpaceManagerViewModel(engine);

        await vm.ScanAsync();

        // Sum of the three raw percents should be close to 100 — used + free
        // already partition the total by construction (used = capacity -
        // volumeFree, free = containerFree, purgeable = the gap). Small drift
        // from integer-boundaries is acceptable.
        var sum = vm.UsedPercent + vm.PurgeablePercent + vm.FreePercent;
        Assert.InRange(sum, 90.0, 110.0);
    }

    // ── 12. Purgeable=0 case (clean system, no purgeable bytes) ──

    [Fact]
    public async Task ScanAsync_HandlesPurgeableZero()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            ReportToReturn = BuildReport(
                totalCapacity: 500,
                volumeFree: 200,
                containerFree: 200,
                purgeable: 0,
                snapshotCount: 0),
        };
        var vm = new PurgeableSpaceManagerViewModel(engine);

        await vm.ScanAsync();

        Assert.Equal(0, vm.PurgeableBytes);
        Assert.Equal(0.0, vm.PurgeablePercent);
        Assert.Equal("--", vm.PurgeableDisplay);
        // Bar shape: 60*,0*,40*
        Assert.Equal("60*,0*,40*", vm.ColumnDefinitions);
    }

    // ── 13. Cancel command enables when IsBusy ────────────────────

    [Fact]
    public async Task CancelCommand_IsEnabledOnlyWhileBusy()
    {
        var engine = StandardEngine();
        engine.SimulateLongOptimize = true;
        var vm = new PurgeableSpaceManagerViewModel(engine);
        await vm.ScanAsync();

        Assert.False(vm.CancelCommand.CanExecute(null));

        var task = vm.RunActionAsync("clean-user-caches");
        for (int i = 0; i < 50 && !vm.IsBusy; i++)
            await Task.Yield();

        Assert.True(vm.IsBusy);
        Assert.True(vm.CancelCommand.CanExecute(null));

        vm.CancelCommand.Execute(null);
        try { await task; } catch { }
    }

    // ── 14. SnapshotCountDisplay "--" before scan, integer after

    [Fact]
    public async Task SnapshotCountDisplay_TransitionsFromDashToCount()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            ReportToReturn = BuildReport(totalCapacity: 1000, volumeFree: 200,
                containerFree: 200, purgeable: 0, snapshotCount: 5),
        };
        var vm = new PurgeableSpaceManagerViewModel(engine);
        Assert.Equal("--", vm.SnapshotCountDisplay);

        await vm.ScanAsync();

        Assert.Equal(5, vm.SnapshotCount);
        Assert.Equal("5", vm.SnapshotCountDisplay);
    }

    // ── 15. FormatBytes scales units correctly ────────────────────

    [Fact]
    public void FormatBytes_ScalesUnits()
    {
        Assert.Equal("--", PurgeableSpaceManagerViewModel.FormatBytes(0));
        Assert.Equal("500 B", PurgeableSpaceManagerViewModel.FormatBytes(500));
        Assert.Equal("10 KB", PurgeableSpaceManagerViewModel.FormatBytes(10 * 1024));
        Assert.Equal("5.0 MB", PurgeableSpaceManagerViewModel.FormatBytes(5L * 1024 * 1024));
        Assert.Equal("12.3 GB",
            PurgeableSpaceManagerViewModel.FormatBytes((long)(12.3 * 1024 * 1024 * 1024)));
    }

    // ── 16. Unavailable scan surfaces unavailable status ─────────

    [Fact]
    public async Task ScanAsync_UnavailableReport_SetsUnavailableStatus()
    {
        LocalizationService.SetLanguage("en");
        var engine = new FakeEngine
        {
            ScanSuccess = false,
            BlockedReason = "diskutil info / failed",
            ReportToReturn = PurgeableReport.None(),
        };
        var vm = new PurgeableSpaceManagerViewModel(engine);

        await vm.ScanAsync();

        Assert.False(vm.Report!.IsAvailable);
        Assert.Contains("diskutil", vm.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    // ── 17. Commands disabled while IsBusy ────────────────────────

    [Fact]
    public async Task Commands_AreDisabled_WhileIsBusy()
    {
        var engine = StandardEngine();
        engine.SimulateLongOptimize = true;
        var vm = new PurgeableSpaceManagerViewModel(engine);
        await vm.ScanAsync();

        Assert.True(vm.CleanUserCachesCommand.CanExecute(null));
        Assert.True(vm.RunPeriodicCommand.CanExecute(null));
        Assert.True(vm.ThinSnapshotsCommand.CanExecute(null));

        var task = vm.RunActionAsync("clean-user-caches");
        for (int i = 0; i < 50 && !vm.IsBusy; i++)
            await Task.Yield();

        Assert.True(vm.IsBusy);
        Assert.False(vm.CleanUserCachesCommand.CanExecute(null));
        Assert.False(vm.RunPeriodicCommand.CanExecute(null));
        Assert.False(vm.ThinSnapshotsCommand.CanExecute(null));

        try { await task; } catch { }
    }

    // ── 18. Integer rounding handles large capacities ─────────────

    [Fact]
    public async Task ScanAsync_RealisticTerabyteDrive_ProducesSensibleProportions()
    {
        // 1 TB drive, 400 GB used, 50 GB purgeable, 574 GB free (roughly)
        const long oneGB = 1024L * 1024 * 1024;
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            ReportToReturn = BuildReport(
                totalCapacity: 1024 * oneGB, // 1 TB
                volumeFree: 624 * oneGB,     // Used = 1024-624 = 400GB
                containerFree: 574 * oneGB,
                purgeable: 50 * oneGB,
                snapshotCount: 1),
        };
        var vm = new PurgeableSpaceManagerViewModel(engine);

        await vm.ScanAsync();

        Assert.InRange(vm.UsedPercent, 39.0, 40.5);
        Assert.InRange(vm.PurgeablePercent, 4.0, 5.5);
        Assert.InRange(vm.FreePercent, 55.0, 57.0);
        Assert.Matches(@"^\d+\*,\d+\*,\d+\*$", vm.ColumnDefinitions);
    }

    // ── Helpers ────────────────────────────────────────────────────

    private static PurgeableReport BuildReport(long totalCapacity, long volumeFree,
        long containerFree, long purgeable, int snapshotCount)
    {
        var snapshots = Enumerable.Range(0, snapshotCount)
            .Select(i => $"com.apple.TimeMachine.2026-04-16-{i:000000}.local")
            .ToList();
        return new PurgeableReport(
            VolumeFreeBytes: volumeFree,
            ContainerFreeBytes: containerFree,
            PurgeableBytes: purgeable,
            LocalSnapshotCount: snapshotCount,
            LocalSnapshots: snapshots,
            IsAvailable: true,
            TotalCapacityBytes: totalCapacity);
    }

    private static FakeEngine StandardEngine() => new()
    {
        ScanSuccess = true,
        OptimizeSuccess = true,
        ReportToReturn = BuildReport(totalCapacity: 500_000_000_000,
            volumeFree: 200_000_000_000, containerFree: 150_000_000_000,
            purgeable: 50_000_000_000, snapshotCount: 2),
    };

    private static async Task WaitUntilNotBusy(PurgeableSpaceManagerViewModel vm, int maxIter = 200)
    {
        for (int i = 0; i < maxIter && vm.IsBusy; i++)
            await Task.Yield();
    }

    private sealed class FakeEngine : IPurgeableSpaceManagerEngine
    {
        public int ScanCallCount;
        public bool ScanSuccess = true;
        public bool OptimizeSuccess = true;
        public bool ThrowOnOptimize;
        public bool SimulateLongOptimize;
        public string? BlockedReason;
        public PurgeableReport? ReportToReturn;
        public OptimizationPlan? LastOptimizePlan;

        public string Id => "purgeable-space-manager";
        public PurgeableReport? LastReport { get; private set; }

        public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
        {
            ScanCallCount++;
            await Task.Yield();
            LastReport = ReportToReturn;
            return ScanSuccess
                ? new ScanResult(Id, true, LastReport?.LocalSnapshotCount ?? 0,
                    LastReport?.PurgeableBytes ?? 0, null)
                : new ScanResult(Id, false, 0, 0, BlockedReason);
        }

        public async Task<OptimizationResult> OptimizeAsync(
            OptimizationPlan plan,
            IProgress<TaskProgress>? progress,
            CancellationToken ct)
        {
            LastOptimizePlan = plan;
            if (ThrowOnOptimize)
                throw new InvalidOperationException("simulated engine failure");

            progress?.Report(new TaskProgress(Id, 0, "starting"));
            if (SimulateLongOptimize)
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            else
                await Task.Yield();
            progress?.Report(new TaskProgress(Id, 100, "complete"));
            var operationId = Guid.NewGuid().ToString("N")[..8];
            return new OptimizationResult(Id, operationId, OptimizeSuccess,
                plan.SelectedItemIds?.Count ?? 0, 0, TimeSpan.Zero);
        }
    }
}
