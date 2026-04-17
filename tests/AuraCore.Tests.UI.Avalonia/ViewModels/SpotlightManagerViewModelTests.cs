using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.Application;
using AuraCore.Module.SpotlightManager;
using AuraCore.Module.SpotlightManager.Models;
using AuraCore.UI.Avalonia;
using AuraCore.UI.Avalonia.ViewModels;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.ViewModels;

/// <summary>
/// Phase 4.4.3 Spotlight Manager VM unit tests. Uses a hand-rolled
/// <see cref="ISpotlightEngine"/> fake — matches DnsFlusher /
/// PurgeableSpaceManager / KernelCleaner FakeEngine style.
/// </summary>
[Collection("Localization")]
public class SpotlightManagerViewModelTests
{
    // ── 1. Ctor ────────────────────────────────────────────────────

    [Fact]
    public void Ctor_DoesNotThrow_WithValidEngine()
    {
        var engine = new FakeEngine();
        var vm = new SpotlightManagerViewModel(engine);
        Assert.NotNull(vm);
        Assert.False(vm.IsBusy);
        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.HasError);
        Assert.Null(vm.Report);
        Assert.Null(vm.PendingRebuildVolume);
        Assert.Empty(vm.VolumeItems);
    }

    [Fact]
    public void Ctor_NullEngine_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SpotlightManagerViewModel((ISpotlightEngine)null!));
        Assert.Throws<ArgumentNullException>(() =>
            new SpotlightManagerViewModel((SpotlightManagerModule)null!));
    }

    // ── 2. TriggerInitialScan invokes engine ──────────────────────

    [Fact]
    public async Task TriggerInitialScan_InvokesScanAsync()
    {
        var engine = new FakeEngine { ScanSuccess = true, ReportToReturn = StandardReport() };
        var vm = new SpotlightManagerViewModel(engine);

        vm.TriggerInitialScan();
        for (int i = 0; i < 50 && engine.ScanCallCount == 0; i++)
            await Task.Yield();
        await WaitUntilNotBusy(vm);

        Assert.True(engine.ScanCallCount >= 1);
    }

    // ── 3. Scan populates VolumeItems ─────────────────────────────

    [Fact]
    public async Task ScanAsync_PopulatesVolumeItemsFromReport()
    {
        var engine = new FakeEngine { ScanSuccess = true, ReportToReturn = StandardReport() };
        var vm = new SpotlightManagerViewModel(engine);

        await vm.ScanAsync();

        Assert.Equal(3, vm.VolumeItems.Count);
        Assert.Equal("/Volumes/Macintosh HD", vm.VolumeItems[0].MountPoint);
        Assert.Equal("/Volumes/External", vm.VolumeItems[1].MountPoint);
        Assert.Equal("/Volumes/Backup", vm.VolumeItems[2].MountPoint);
    }

    // ── 4. Scan sets IsIndexed + ActualIndexingEnabled ────────────

    [Fact]
    public async Task ScanAsync_SetsIsIndexedAndActualEnabled_Correctly()
    {
        var engine = new FakeEngine { ScanSuccess = true, ReportToReturn = StandardReport() };
        var vm = new SpotlightManagerViewModel(engine);

        await vm.ScanAsync();

        // Macintosh HD enabled, External disabled, Backup enabled
        Assert.True(vm.VolumeItems[0].IsIndexed);
        Assert.True(vm.VolumeItems[0].ActualIndexingEnabled);
        Assert.False(vm.VolumeItems[1].IsIndexed);
        Assert.False(vm.VolumeItems[1].ActualIndexingEnabled);
        Assert.True(vm.VolumeItems[2].IsIndexed);
        Assert.True(vm.VolumeItems[2].ActualIndexingEnabled);
    }

    // ── 5. Display strings for counts ─────────────────────────────

    [Fact]
    public async Task ScanAsync_PopulatesCountDisplays()
    {
        var engine = new FakeEngine { ScanSuccess = true, ReportToReturn = StandardReport() };
        var vm = new SpotlightManagerViewModel(engine);

        Assert.Equal("--", vm.TotalVolumesDisplay);
        Assert.Equal("--", vm.EnabledCountDisplay);
        Assert.Equal("--", vm.DisabledCountDisplay);

        await vm.ScanAsync();

        Assert.Equal("3", vm.TotalVolumesDisplay);
        Assert.Equal("2", vm.EnabledCountDisplay);
        Assert.Equal("1", vm.DisabledCountDisplay);
    }

    // ── 6. Toggle fires engine with correct itemId ────────────────

    [Fact]
    public async Task ToggleIndexing_FiresEngine_WithCorrectEnableItemId()
    {
        var engine = new FakeEngine { ScanSuccess = true, OptimizeSuccess = true, ReportToReturn = StandardReport() };
        var vm = new SpotlightManagerViewModel(engine);
        await vm.ScanAsync();

        // External is currently disabled. Flipping to true should dispatch enable:<mount>.
        var external = vm.VolumeItems[1];
        await vm.DispatchToggleAsync(external, true);

        Assert.NotNull(engine.LastOptimizePlan);
        var items = engine.LastOptimizePlan!.SelectedItemIds;
        Assert.Single(items);
        Assert.Equal("enable:/Volumes/External", items[0]);
    }

    [Fact]
    public async Task ToggleIndexing_FiresEngine_WithCorrectDisableItemId()
    {
        var engine = new FakeEngine { ScanSuccess = true, OptimizeSuccess = true, ReportToReturn = StandardReport() };
        var vm = new SpotlightManagerViewModel(engine);
        await vm.ScanAsync();

        var mac = vm.VolumeItems[0]; // enabled
        await vm.DispatchToggleAsync(mac, false);

        Assert.NotNull(engine.LastOptimizePlan);
        Assert.Equal("disable:/Volumes/Macintosh HD", engine.LastOptimizePlan!.SelectedItemIds[0]);
    }

    // ── 7. Toggle reverts on engine failure ───────────────────────

    [Fact]
    public async Task ToggleIndexing_Reverts_WhenEngineFails()
    {
        var engine = new FakeEngine { ScanSuccess = true, OptimizeSuccess = false, ReportToReturn = StandardReport() };
        var vm = new SpotlightManagerViewModel(engine);
        await vm.ScanAsync();

        var external = vm.VolumeItems[1]; // false/false to start
        await vm.DispatchToggleAsync(external, true);

        // Engine reported failure → IsIndexed should snap back to ActualIndexingEnabled (false)
        Assert.False(external.ActualIndexingEnabled);
        Assert.False(external.IsIndexed);
        Assert.NotNull(vm.ErrorMessage);
    }

    // ── 8. Revert via ApplyFromEngine does NOT re-fire Requested ──

    [Fact]
    public void ApplyFromEngine_DoesNotRaiseRequested_OnRevert()
    {
        var item = new VolumeItemVM("/Volumes/X", indexingEnabled: false);
        int requestedCount = 0;
        item.Requested += (_, _) => requestedCount++;

        // Simulate user flip to true (one invocation expected).
        item.IsIndexed = true;
        Assert.Equal(1, requestedCount);

        // Simulate engine revert back to false. This MUST NOT re-fire Requested
        // (would cause infinite dispatch loop).
        item.ApplyFromEngine(false);
        Assert.Equal(1, requestedCount);
        Assert.False(item.IsIndexed);
        Assert.False(item.ActualIndexingEnabled);
    }

    // ── 9. BeginRebuildCommand sets PendingRebuildVolume ──────────

    [Fact]
    public async Task BeginRebuildCommand_SetsPendingRebuildVolume()
    {
        var engine = new FakeEngine { ScanSuccess = true, OptimizeSuccess = true, ReportToReturn = StandardReport() };
        var vm = new SpotlightManagerViewModel(engine);
        await vm.ScanAsync();

        var mac = vm.VolumeItems[0];
        vm.BeginRebuildCommand.Execute(mac);

        Assert.Same(mac, vm.PendingRebuildVolume);
        Assert.True(vm.HasPendingRebuild);
    }

    // ── 10. CancelRebuildCommand clears PendingRebuildVolume ──────

    [Fact]
    public async Task CancelRebuildCommand_ClearsPendingRebuildVolume()
    {
        var engine = new FakeEngine { ScanSuccess = true, ReportToReturn = StandardReport() };
        var vm = new SpotlightManagerViewModel(engine);
        await vm.ScanAsync();

        vm.BeginRebuildCommand.Execute(vm.VolumeItems[0]);
        Assert.NotNull(vm.PendingRebuildVolume);

        vm.CancelRebuildCommand.Execute(null);

        Assert.Null(vm.PendingRebuildVolume);
        Assert.False(vm.HasPendingRebuild);
    }

    // ── 11. ConfirmRebuildCommand dispatches rebuild itemId ───────

    [Fact]
    public async Task ConfirmRebuild_DispatchesRebuildItemId()
    {
        var engine = new FakeEngine { ScanSuccess = true, OptimizeSuccess = true, ReportToReturn = StandardReport() };
        var vm = new SpotlightManagerViewModel(engine);
        await vm.ScanAsync();

        var mac = vm.VolumeItems[0];
        await vm.ConfirmRebuildAsync(mac);

        Assert.NotNull(engine.LastOptimizePlan);
        Assert.Single(engine.LastOptimizePlan!.SelectedItemIds);
        Assert.Equal("rebuild:/Volumes/Macintosh HD", engine.LastOptimizePlan!.SelectedItemIds[0]);
    }

    // ── 12. ConfirmRebuild clears PendingRebuildVolume on success ──

    [Fact]
    public async Task ConfirmRebuild_ClearsPending_OnSuccess()
    {
        var engine = new FakeEngine { ScanSuccess = true, OptimizeSuccess = true, ReportToReturn = StandardReport() };
        var vm = new SpotlightManagerViewModel(engine);
        await vm.ScanAsync();

        vm.BeginRebuildCommand.Execute(vm.VolumeItems[0]);
        Assert.NotNull(vm.PendingRebuildVolume);

        await vm.ConfirmRebuildAsync(vm.VolumeItems[0]);

        Assert.Null(vm.PendingRebuildVolume);
    }

    // ── 13. ConfirmRebuild clears PendingRebuildVolume on failure ──

    [Fact]
    public async Task ConfirmRebuild_ClearsPending_OnFailure()
    {
        var engine = new FakeEngine { ScanSuccess = true, OptimizeSuccess = false, ReportToReturn = StandardReport() };
        var vm = new SpotlightManagerViewModel(engine);
        await vm.ScanAsync();

        vm.BeginRebuildCommand.Execute(vm.VolumeItems[0]);
        Assert.NotNull(vm.PendingRebuildVolume);

        await vm.ConfirmRebuildAsync(vm.VolumeItems[0]);

        // Still cleared even on failure — no leaked confirmation state
        Assert.Null(vm.PendingRebuildVolume);
        Assert.NotNull(vm.ErrorMessage);
    }

    // ── 14. DispatchToggle sanity: skip unknown volume ────────────

    [Fact]
    public async Task DispatchToggle_SkipsVolume_WhenNotInReport()
    {
        var engine = new FakeEngine { ScanSuccess = true, OptimizeSuccess = true, ReportToReturn = StandardReport() };
        var vm = new SpotlightManagerViewModel(engine);
        await vm.ScanAsync();

        // Manually construct an off-report VolumeItemVM — VM must ignore.
        var rogue = new VolumeItemVM("/Volumes/NotInReport", indexingEnabled: false);
        await vm.DispatchToggleAsync(rogue, true);

        Assert.Null(engine.LastOptimizePlan);
    }

    // ── 15. Toggle updates StatusText ─────────────────────────────

    [Fact]
    public async Task ToggleIndexing_UpdatesStatusTextOnCompletion()
    {
        var engine = new FakeEngine { ScanSuccess = true, OptimizeSuccess = true, ReportToReturn = StandardReport() };
        var vm = new SpotlightManagerViewModel(engine);
        await vm.ScanAsync();

        await vm.DispatchToggleAsync(vm.VolumeItems[1], true);

        // Final state is "Done."
        Assert.Equal(LocalizationService._("spotlight.status.done"), vm.StatusText);
    }

    // ── 16. ErrorMessage populated on engine exception ────────────

    [Fact]
    public async Task ToggleIndexing_PopulatesErrorMessage_OnEngineException()
    {
        var engine = new FakeEngine { ScanSuccess = true, ThrowOnOptimize = true, ReportToReturn = StandardReport() };
        var vm = new SpotlightManagerViewModel(engine);
        await vm.ScanAsync();

        var external = vm.VolumeItems[1];
        await vm.DispatchToggleAsync(external, true);

        Assert.NotNull(vm.ErrorMessage);
        Assert.True(vm.HasError);
        // Toggle should have reverted too
        Assert.False(external.IsIndexed);
    }

    // ── 17. Cancel clears PendingRebuildVolume when idle ──────────

    [Fact]
    public async Task CancelCommand_CanExecute_WhenBusy()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OptimizeSuccess = true,
            SimulateLongOptimize = true,
            ReportToReturn = StandardReport(),
        };
        var vm = new SpotlightManagerViewModel(engine);
        await vm.ScanAsync();

        var task = vm.DispatchToggleAsync(vm.VolumeItems[1], true);
        for (int i = 0; i < 50 && !vm.IsBusy; i++)
            await Task.Yield();

        Assert.True(vm.IsBusy);
        Assert.True(vm.CancelCommand.CanExecute(null));

        try { vm.CancelCommand.Execute(null); } catch { }
        try { await task; } catch { }
    }

    // ── 18. Derived HasPendingRebuild + HasError props ────────────

    [Fact]
    public async Task HasPendingRebuild_ReflectsPendingState()
    {
        var engine = new FakeEngine { ScanSuccess = true, ReportToReturn = StandardReport() };
        var vm = new SpotlightManagerViewModel(engine);
        await vm.ScanAsync();

        Assert.False(vm.HasPendingRebuild);

        vm.BeginRebuildCommand.Execute(vm.VolumeItems[0]);
        Assert.True(vm.HasPendingRebuild);

        vm.CancelRebuildCommand.Execute(null);
        Assert.False(vm.HasPendingRebuild);
    }

    // ── Helpers ────────────────────────────────────────────────────

    private static SpotlightReport StandardReport()
    {
        var volumes = new List<SpotlightVolumeInfo>
        {
            new("/Volumes/Macintosh HD", IndexingEnabled: true,  IndexSizeBytes: 0),
            new("/Volumes/External",     IndexingEnabled: false, IndexSizeBytes: 0),
            new("/Volumes/Backup",       IndexingEnabled: true,  IndexSizeBytes: 0),
        };
        return new SpotlightReport(
            Volumes: volumes,
            TotalVolumes: 3,
            EnabledCount: 2,
            DisabledCount: 1,
            IsAvailable: true);
    }

    private static async Task WaitUntilNotBusy(SpotlightManagerViewModel vm, int maxIter = 200)
    {
        for (int i = 0; i < maxIter && vm.IsBusy; i++)
            await Task.Yield();
    }

    private sealed class FakeEngine : ISpotlightEngine
    {
        public int ScanCallCount;
        public bool ScanSuccess = true;
        public bool OptimizeSuccess = true;
        public bool ThrowOnOptimize;
        public bool SimulateLongOptimize;
        public string? BlockedReason;
        public SpotlightReport? ReportToReturn;
        public OptimizationPlan? LastOptimizePlan;

        public string Id => "spotlight-manager";
        public SpotlightReport? LastReport { get; private set; }

        public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
        {
            ScanCallCount++;
            await Task.Yield();
            LastReport = ReportToReturn;
            return ScanSuccess
                ? new ScanResult(Id, true, LastReport?.TotalVolumes ?? 0, 0, null)
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
            var processed = OptimizeSuccess ? (plan.SelectedItemIds?.Count ?? 0) : 0;
            return new OptimizationResult(Id, operationId, OptimizeSuccess,
                processed, 0, TimeSpan.Zero);
        }
    }
}
