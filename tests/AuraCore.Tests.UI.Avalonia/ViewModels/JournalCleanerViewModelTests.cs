using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.Application;
using AuraCore.Module.JournalCleaner;
using AuraCore.Module.JournalCleaner.Models;
using AuraCore.UI.Avalonia.ViewModels;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.ViewModels;

/// <summary>
/// Phase 4.3.1 Journal Cleaner VM unit tests.
/// Uses a hand-rolled <see cref="IJournalCleanerEngine"/> fake — the test project
/// doesn't reference Moq (see .csproj), which matches the rest of the VM test style
/// (see AIFeaturesViewModelTests for precedent).
/// </summary>
public class JournalCleanerViewModelTests
{
    // ── 1. Ctor ────────────────────────────────────────────────────

    [Fact]
    public void Ctor_DoesNotThrow_WithValidDependencies()
    {
        var engine = new FakeEngine();
        var vm = new JournalCleanerViewModel(engine);
        Assert.NotNull(vm);
        Assert.False(vm.IsBusy);
        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.HasError);
        Assert.Null(vm.Report);
        Assert.Equal("--", vm.CurrentUsageDisplay);
        Assert.Equal("--", vm.FileCountDisplay);
        Assert.Equal("--", vm.OldestEntryDisplay);
    }

    [Fact]
    public void Ctor_NullEngine_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new JournalCleanerViewModel((IJournalCleanerEngine)null!));
        Assert.Throws<ArgumentNullException>(() =>
            new JournalCleanerViewModel((JournalCleanerModule)null!));
    }

    // ── 2. ScanCommand invokes engine scan ─────────────────────────

    [Fact]
    public async Task ScanCommand_InvokesEngineScan()
    {
        var engine = new FakeEngine();
        var vm = new JournalCleanerViewModel(engine);

        await vm.ScanAsync();

        Assert.Equal(1, engine.ScanCallCount);
    }

    // ── 3. ScanCommand populates display strings on success ────────

    [Fact]
    public async Task ScanCommand_PopulatesDisplayStrings_OnSuccess()
    {
        // 1.5 GiB journal, 42 files, oldest 2025-11-03
        var bytes = (long)(1.5 * 1024 * 1024 * 1024);
        var engine = new FakeEngine
        {
            ReportToReturn = new JournalReport(
                CurrentBytes: bytes,
                OldestEntry: new DateTime(2025, 11, 3),
                JournalFileCount: 42,
                RecommendedLimit: 500L * 1024 * 1024,
                IsAvailable: true),
            ScanSuccess = true,
        };
        var vm = new JournalCleanerViewModel(engine);

        await vm.ScanAsync();

        Assert.NotNull(vm.Report);
        Assert.Equal("1.50 GB", vm.CurrentUsageDisplay);
        Assert.Equal("42 files", vm.FileCountDisplay);
        Assert.Equal("2025-11-03", vm.OldestEntryDisplay);
    }

    // ── 4. ScanCommand surfaces engine failure ─────────────────────

    [Fact]
    public async Task ScanCommand_SetsErrorMessage_OnFailedScanResult()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = false,
            BlockedReason = "systemd-journald (journalctl) not available",
            ReportToReturn = JournalReport.None(),
        };
        var vm = new JournalCleanerViewModel(engine);

        await vm.ScanAsync();

        Assert.True(vm.HasError);
        Assert.Equal("systemd-journald (journalctl) not available", vm.ErrorMessage);
    }

    // ── 5. VacuumCommand calls Optimize with correct item id ───────

    [Fact]
    public async Task VacuumCommand_CallsEngineOptimize_WithCorrectItemId()
    {
        var engine = new FakeEngine { ScanSuccess = true, OptimizeSuccess = true };
        var vm = new JournalCleanerViewModel(engine);

        await vm.VacuumAsync("vacuum-500m");

        Assert.NotNull(engine.LastOptimizePlan);
        Assert.Single(engine.LastOptimizePlan!.SelectedItemIds);
        Assert.Equal("vacuum-500m", engine.LastOptimizePlan.SelectedItemIds[0]);
    }

    // ── 6. VacuumCommand respects cancellation ─────────────────────

    [Fact]
    public async Task VacuumCommand_RespectsCancellation()
    {
        var engine = new FakeEngine { SimulateLongOptimize = true };
        var vm = new JournalCleanerViewModel(engine);

        var vacuumTask = vm.VacuumAsync("vacuum-1g");
        // Give VacuumAsync time to enter its Optimize call
        await Task.Delay(50);
        Assert.True(vm.IsBusy, "VM should be busy mid-flight");

        vm.Cancel();

        await vacuumTask; // VM swallows OCE internally
        Assert.False(vm.IsBusy, "VM should finish after cancellation");
    }

    // ── 7. VacuumCommand clears error on success ───────────────────

    [Fact]
    public async Task VacuumCommand_ClearsErrorOnSuccess()
    {
        var engine = new FakeEngine { ScanSuccess = false, BlockedReason = "pre-existing error" };
        var vm = new JournalCleanerViewModel(engine);

        // Populate an error from a bad scan first
        await vm.ScanAsync();
        Assert.True(vm.HasError);

        // Now flip engine to success + do a successful vacuum — error should clear
        engine.ScanSuccess = true;
        engine.OptimizeSuccess = true;
        engine.BytesFreedOnOptimize = 300L * 1024 * 1024;

        await vm.VacuumAsync("vacuum-500m");

        Assert.False(vm.HasError);
        Assert.Null(vm.ErrorMessage);
    }

    // ── 8. INPC fires on Report change (safety net for binding) ────

    [Fact]
    public async Task ScanCommand_RaisesPropertyChanged_ForDisplayStrings()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            ReportToReturn = new JournalReport(
                100 * 1024 * 1024, new DateTime(2026, 1, 1), 5, 50 * 1024 * 1024, true),
        };
        var vm = new JournalCleanerViewModel(engine);
        var fired = new List<string>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => fired.Add(e.PropertyName ?? "");

        await vm.ScanAsync();

        Assert.Contains(nameof(JournalCleanerViewModel.Report), fired);
        Assert.Contains(nameof(JournalCleanerViewModel.CurrentUsageDisplay), fired);
        Assert.Contains(nameof(JournalCleanerViewModel.FileCountDisplay), fired);
    }

    // ── 9. Format helper sanity ────────────────────────────────────

    [Theory]
    [InlineData(0, "--")]
    [InlineData(-1, "--")]
    [InlineData(500, "500 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1024L * 1024, "1 MB")]
    [InlineData((long)(1.5 * 1024 * 1024 * 1024), "1.50 GB")]
    public void FormatSize_ReturnsExpected(long bytes, string expected)
    {
        Assert.Equal(expected, JournalCleanerViewModel.FormatSize(bytes));
    }

    // ── Helpers ────────────────────────────────────────────────────

    /// <summary>Lightweight in-memory test engine — implements the VM's adapter contract.</summary>
    private sealed class FakeEngine : IJournalCleanerEngine
    {
        public int ScanCallCount;
        public bool ScanSuccess = true;
        public string? BlockedReason;
        public JournalReport ReportToReturn = JournalReport.None();
        public OptimizationPlan? LastOptimizePlan;
        public bool OptimizeSuccess = true;
        public long BytesFreedOnOptimize = 0;
        public bool SimulateLongOptimize;

        public string Id => "journal-cleaner";
        public JournalReport? LastReport { get; private set; }

        public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
        {
            ScanCallCount++;
            await Task.Yield();
            LastReport = ReportToReturn;
            return ScanSuccess
                ? new ScanResult(Id, true, ReportToReturn.JournalFileCount, 0, null)
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
                // Will throw OCE if cancelled; VM swallows it.
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
            }
            else
            {
                await Task.Yield();
            }
            progress?.Report(new TaskProgress(Id, 100, "complete"));
            var operationId = Guid.NewGuid().ToString("N")[..8];
            return new OptimizationResult(Id, operationId, OptimizeSuccess, 1, BytesFreedOnOptimize, TimeSpan.Zero);
        }
    }
}
