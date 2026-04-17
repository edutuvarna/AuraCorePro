using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.Application;
using AuraCore.Module.SnapFlatpakCleaner;
using AuraCore.UI.Avalonia.ViewModels;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.ViewModels;

/// <summary>
/// Phase 4.3.2 Snap/Flatpak Cleaner VM unit tests.
/// Uses a hand-rolled <see cref="ISnapFlatpakCleanerEngine"/> fake — the test
/// project does not reference Moq (see .csproj). Mirrors JournalCleaner's
/// FakeEngine pattern.
/// </summary>
public class SnapFlatpakCleanerViewModelTests
{
    // ── 1. Ctor ────────────────────────────────────────────────────

    [Fact]
    public void Ctor_DoesNotThrow_WithValidDependencies()
    {
        var engine = new FakeEngine();
        var vm = new SnapFlatpakCleanerViewModel(engine);
        Assert.NotNull(vm);
        Assert.False(vm.IsBusy);
        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.HasError);
        Assert.Equal(0, vm.SnapDisabledCount);
        Assert.Equal(0, vm.FlatpakUnusedCount);
        Assert.False(vm.SnapAvailable);
        Assert.False(vm.FlatpakAvailable);
        Assert.Equal("--", vm.SnapDisplay);
        Assert.Equal("--", vm.FlatpakDisplay);
    }

    [Fact]
    public void Ctor_NullEngine_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SnapFlatpakCleanerViewModel((ISnapFlatpakCleanerEngine)null!));
        Assert.Throws<ArgumentNullException>(() =>
            new SnapFlatpakCleanerViewModel((SnapFlatpakCleanerModule)null!));
    }

    // ── 2. ScanCommand invokes engine scan ─────────────────────────

    [Fact]
    public async Task ScanCommand_InvokesEngineScan()
    {
        var engine = new FakeEngine();
        var vm = new SnapFlatpakCleanerViewModel(engine);

        await vm.ScanAsync();

        Assert.Equal(1, engine.ScanCallCount);
    }

    // ── 3. ScanCommand populates display strings on success ────────

    [Fact]
    public async Task ScanCommand_PopulatesCounts_OnSuccess()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            DisabledSnapCount = 3,
            UnusedFlatpakCount = 7,
            SnapAvailable = true,
            FlatpakAvailable = true,
        };
        var vm = new SnapFlatpakCleanerViewModel(engine);

        await vm.ScanAsync();

        Assert.Equal(3, vm.SnapDisabledCount);
        Assert.Equal(7, vm.FlatpakUnusedCount);
        Assert.True(vm.SnapAvailable);
        Assert.True(vm.FlatpakAvailable);
        Assert.Equal("3", vm.SnapDisplay);
        Assert.Equal("7", vm.FlatpakDisplay);
    }

    // ── 4. ScanCommand surfaces unavailable tooling with "--" ──────

    [Fact]
    public async Task ScanCommand_SnapMissing_RendersDashForSnap()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            SnapAvailable = false,
            FlatpakAvailable = true,
            DisabledSnapCount = 0,
            UnusedFlatpakCount = 2,
        };
        var vm = new SnapFlatpakCleanerViewModel(engine);

        await vm.ScanAsync();

        Assert.False(vm.SnapAvailable);
        Assert.True(vm.FlatpakAvailable);
        Assert.Equal("--", vm.SnapDisplay);
        Assert.Equal("2", vm.FlatpakDisplay);
    }

    [Fact]
    public async Task ScanCommand_BothMissing_RendersDashForBoth()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = false,
            SnapAvailable = false,
            FlatpakAvailable = false,
            BlockedReason = "Neither snap nor flatpak is installed.",
        };
        var vm = new SnapFlatpakCleanerViewModel(engine);

        await vm.ScanAsync();

        Assert.Equal("--", vm.SnapDisplay);
        Assert.Equal("--", vm.FlatpakDisplay);
        Assert.True(vm.HasError);
    }

    // ── 5. ScanCommand surfaces engine failure ─────────────────────

    [Fact]
    public async Task ScanCommand_SetsErrorMessage_OnFailedScanResult()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = false,
            BlockedReason = "snap unavailable",
            SnapAvailable = false,
            FlatpakAvailable = true,
        };
        var vm = new SnapFlatpakCleanerViewModel(engine);

        await vm.ScanAsync();

        Assert.True(vm.HasError);
        Assert.Equal("snap unavailable", vm.ErrorMessage);
    }

    [Fact]
    public async Task ScanCommand_SetsErrorMessage_OnEngineException()
    {
        var engine = new FakeEngine { ScanThrows = true };
        var vm = new SnapFlatpakCleanerViewModel(engine);

        await vm.ScanAsync();

        Assert.True(vm.HasError);
        Assert.False(vm.IsBusy);
    }

    // ── 6. CleanSnapCommand uses correct item id ───────────────────

    [Fact]
    public async Task CleanSnapCommand_CallsEngineOptimize_WithSnapCleanAll()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OptimizeSuccess = true,
            SnapAvailable = true,
            FlatpakAvailable = true,
        };
        var vm = new SnapFlatpakCleanerViewModel(engine);

        await vm.CleanAsync("snap-clean-all");

        Assert.NotNull(engine.LastOptimizePlan);
        Assert.Single(engine.LastOptimizePlan!.SelectedItemIds);
        Assert.Equal("snap-clean-all", engine.LastOptimizePlan.SelectedItemIds[0]);
    }

    [Fact]
    public async Task CleanFlatpakCommand_CallsEngineOptimize_WithFlatpakCleanAll()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OptimizeSuccess = true,
            SnapAvailable = true,
            FlatpakAvailable = true,
        };
        var vm = new SnapFlatpakCleanerViewModel(engine);

        await vm.CleanAsync("flatpak-clean-all");

        Assert.NotNull(engine.LastOptimizePlan);
        Assert.Equal("flatpak-clean-all", engine.LastOptimizePlan!.SelectedItemIds[0]);
    }

    // ── 7. CleanBoth calls both clean-all IDs sequentially ─────────

    [Fact]
    public async Task CleanBothCommand_CallsBothCleanAllItems()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OptimizeSuccess = true,
            SnapAvailable = true,
            FlatpakAvailable = true,
        };
        var vm = new SnapFlatpakCleanerViewModel(engine);
        // Populate availability by scanning first
        await vm.ScanAsync();

        await vm.CleanBothAsync();

        // The FakeEngine records every optimize call — should have 2 cleanups (snap, flatpak)
        Assert.Contains("snap-clean-all", engine.OptimizeItemIds);
        Assert.Contains("flatpak-clean-all", engine.OptimizeItemIds);
    }

    [Fact]
    public async Task CleanBothCommand_StopsOnSnapFailure()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OptimizeSuccess = false,
            SnapAvailable = true,
            FlatpakAvailable = true,
        };
        var vm = new SnapFlatpakCleanerViewModel(engine);
        await vm.ScanAsync();

        await vm.CleanBothAsync();

        Assert.True(vm.HasError);
        // Only snap-clean-all should have been attempted — abort before flatpak
        Assert.Contains("snap-clean-all", engine.OptimizeItemIds);
        Assert.DoesNotContain("flatpak-clean-all", engine.OptimizeItemIds);
    }

    [Fact]
    public async Task CleanBothCommand_SkipsMissingTooling()
    {
        // snap available, flatpak NOT — CleanBoth should only call snap
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OptimizeSuccess = true,
            SnapAvailable = true,
            FlatpakAvailable = false,
        };
        var vm = new SnapFlatpakCleanerViewModel(engine);
        await vm.ScanAsync();

        await vm.CleanBothAsync();

        Assert.Contains("snap-clean-all", engine.OptimizeItemIds);
        Assert.DoesNotContain("flatpak-clean-all", engine.OptimizeItemIds);
        Assert.False(vm.HasError);
    }

    // ── 8. Clean respects cancellation ─────────────────────────────

    [Fact]
    public async Task CleanCommand_RespectsCancellation()
    {
        var engine = new FakeEngine { SimulateLongOptimize = true };
        var vm = new SnapFlatpakCleanerViewModel(engine);

        var cleanTask = vm.CleanAsync("snap-clean-all");
        await Task.Delay(50);
        Assert.True(vm.IsBusy, "VM should be busy mid-flight");

        vm.Cancel();

        await cleanTask; // VM swallows OCE internally
        Assert.False(vm.IsBusy, "VM should finish after cancellation");
    }

    // ── 9. Error clears on successful clean ────────────────────────

    [Fact]
    public async Task CleanCommand_ClearsErrorOnSuccess()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = false,
            BlockedReason = "pre-existing error",
            SnapAvailable = true,
            FlatpakAvailable = true,
        };
        var vm = new SnapFlatpakCleanerViewModel(engine);
        await vm.ScanAsync();
        Assert.True(vm.HasError);

        engine.ScanSuccess = true;
        engine.OptimizeSuccess = true;
        engine.BlockedReason = null;

        await vm.CleanAsync("snap-clean-all");

        Assert.False(vm.HasError);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task CleanCommand_ErrorOnFailedResult()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OptimizeSuccess = false,
            SnapAvailable = true,
            FlatpakAvailable = true,
        };
        var vm = new SnapFlatpakCleanerViewModel(engine);

        await vm.CleanAsync("snap-clean-all");

        Assert.True(vm.HasError);
    }

    // ── 10. INPC fires on scan changes (safety net for binding) ────

    [Fact]
    public async Task ScanCommand_RaisesPropertyChanged_ForDisplayStrings()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            SnapAvailable = true,
            FlatpakAvailable = true,
            DisabledSnapCount = 5,
            UnusedFlatpakCount = 10,
        };
        var vm = new SnapFlatpakCleanerViewModel(engine);
        var fired = new List<string>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => fired.Add(e.PropertyName ?? "");

        await vm.ScanAsync();

        Assert.Contains(nameof(SnapFlatpakCleanerViewModel.SnapDisabledCount), fired);
        Assert.Contains(nameof(SnapFlatpakCleanerViewModel.FlatpakUnusedCount), fired);
        Assert.Contains(nameof(SnapFlatpakCleanerViewModel.SnapDisplay), fired);
        Assert.Contains(nameof(SnapFlatpakCleanerViewModel.FlatpakDisplay), fired);
    }

    // ── 11. CleanAsync with empty/null itemId is no-op ─────────────

    [Fact]
    public async Task CleanAsync_EmptyItemId_NoOp()
    {
        var engine = new FakeEngine { OptimizeSuccess = true };
        var vm = new SnapFlatpakCleanerViewModel(engine);

        await vm.CleanAsync(null);
        await vm.CleanAsync(string.Empty);

        Assert.Null(engine.LastOptimizePlan);
        Assert.False(vm.HasError);
    }

    // ── 12. IsBusy flips around commands ───────────────────────────

    [Fact]
    public async Task ScanCommand_IsBusyFalse_AfterCompletion()
    {
        var engine = new FakeEngine { ScanSuccess = true };
        var vm = new SnapFlatpakCleanerViewModel(engine);

        await vm.ScanAsync();

        Assert.False(vm.IsBusy);
    }

    // ── Helpers ────────────────────────────────────────────────────

    /// <summary>Lightweight in-memory test engine — implements the VM's adapter contract.</summary>
    private sealed class FakeEngine : ISnapFlatpakCleanerEngine
    {
        public int ScanCallCount;
        public bool ScanSuccess = true;
        public bool ScanThrows;
        public string? BlockedReason;
        public int DisabledSnapCount;
        public int UnusedFlatpakCount;
        public bool SnapAvailable;
        public bool FlatpakAvailable;
        public OptimizationPlan? LastOptimizePlan;
        public List<string> OptimizeItemIds { get; } = new();
        public bool OptimizeSuccess = true;
        public bool SimulateLongOptimize;

        public string Id => "snap-flatpak-cleaner";
        public int LastDisabledSnapCount => DisabledSnapCount;
        public int LastUnusedFlatpakCount => UnusedFlatpakCount;
        public bool LastSnapAvailable => SnapAvailable;
        public bool LastFlatpakAvailable => FlatpakAvailable;

        public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
        {
            ScanCallCount++;
            await Task.Yield();
            if (ScanThrows) throw new InvalidOperationException("boom");
            var total = DisabledSnapCount + UnusedFlatpakCount;
            return ScanSuccess
                ? new ScanResult(Id, true, total, 0, null)
                : new ScanResult(Id, false, 0, 0, BlockedReason);
        }

        public async Task<OptimizationResult> OptimizeAsync(
            OptimizationPlan plan,
            IProgress<TaskProgress>? progress,
            CancellationToken ct)
        {
            LastOptimizePlan = plan;
            if (plan.SelectedItemIds is { } ids)
                OptimizeItemIds.AddRange(ids);

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
