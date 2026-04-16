using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.Application;
using AuraCore.Module.DnsFlusher;
using AuraCore.Module.DnsFlusher.Models;
using AuraCore.UI.Avalonia;
using AuraCore.UI.Avalonia.ViewModels;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.ViewModels;

/// <summary>
/// Phase 4.4.1 DNS Flusher VM unit tests. Uses a hand-rolled
/// <see cref="IDnsFlusherEngine"/> fake — matches GrubManager / KernelCleaner /
/// DockerCleaner / SnapFlatpak / Journal FakeEngine style (no Moq in the test project).
/// </summary>
[Collection("Localization")]
public class DnsFlusherViewModelTests
{
    // ── 1. Ctor ────────────────────────────────────────────────────

    [Fact]
    public void Ctor_DoesNotThrow_WithValidDependencies()
    {
        var engine = new FakeEngine();
        var vm = new DnsFlusherViewModel(engine);
        Assert.NotNull(vm);
        Assert.False(vm.IsBusy);
        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.HasError);
        Assert.False(vm.DscacheutilAvailable);
        Assert.Null(vm.LastFlush);
    }

    [Fact]
    public void Ctor_NullEngine_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DnsFlusherViewModel((IDnsFlusherEngine)null!));
        Assert.Throws<ArgumentNullException>(() =>
            new DnsFlusherViewModel((DnsFlusherModule)null!));
    }

    // ── 2. TriggerInitialScan calls engine.ScanAsync ──────────────

    [Fact]
    public async Task TriggerInitialScan_InvokesScanAsync()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            ReportToReturn = new DnsFlusherReport(DscacheutilAvailable: true, LastFlush: null),
        };
        var vm = new DnsFlusherViewModel(engine);

        vm.TriggerInitialScan();
        for (int i = 0; i < 20 && engine.ScanCallCount == 0; i++)
            await Task.Yield();
        await WaitUntilNotBusy(vm);

        Assert.True(engine.ScanCallCount >= 1);
    }

    // ── 3. Scan populates Report + derived properties ─────────────

    [Fact]
    public async Task ScanAsync_PopulatesReport_AndDerivedProperties()
    {
        var flushTime = DateTime.UtcNow.AddMinutes(-5);
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            ReportToReturn = new DnsFlusherReport(DscacheutilAvailable: true, LastFlush: flushTime),
        };
        var vm = new DnsFlusherViewModel(engine);

        await vm.ScanAsync();

        Assert.NotNull(vm.Report);
        Assert.True(vm.DscacheutilAvailable);
        Assert.Equal(flushTime, vm.LastFlush);
    }

    // ── 4. Scan sets DscacheutilAvailable correctly ────────────────

    [Fact]
    public async Task ScanAsync_SetsDscacheutilAvailable_FromReport()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = false,
            ReportToReturn = new DnsFlusherReport(DscacheutilAvailable: false, LastFlush: null),
            BlockedReason = "dscacheutil not available",
        };
        var vm = new DnsFlusherViewModel(engine);

        await vm.ScanAsync();

        Assert.False(vm.DscacheutilAvailable);
    }

    // ── 5. Scan handles LastFlush=null (never-flushed case) ────────

    [Fact]
    public async Task ScanAsync_HandlesNeverFlushed()
    {
        AuraCore.UI.Avalonia.LocalizationService.SetLanguage("en");
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            ReportToReturn = new DnsFlusherReport(DscacheutilAvailable: true, LastFlush: null),
        };
        var vm = new DnsFlusherViewModel(engine);

        await vm.ScanAsync();

        Assert.Null(vm.LastFlush);
        Assert.Equal("Never flushed in this session", vm.LastFlushDisplay);
    }

    // ── 6. FlushCommand builds ["flush"] SelectedItemIds ─────────

    [Fact]
    public async Task FlushCommand_BuildsFlushItemId()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OptimizeSuccess = true,
            ReportToReturn = new DnsFlusherReport(DscacheutilAvailable: true, LastFlush: null),
        };
        var vm = new DnsFlusherViewModel(engine);
        await vm.ScanAsync();

        await vm.FlushAsync();

        Assert.NotNull(engine.LastOptimizePlan);
        Assert.Single(engine.LastOptimizePlan!.SelectedItemIds);
        Assert.Equal("flush", engine.LastOptimizePlan.SelectedItemIds[0]);
    }

    // ── 7. FlushCommand triggers post-flush rescan on success ─────

    [Fact]
    public async Task FlushCommand_TriggersPostFlushRescan_OnSuccess()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OptimizeSuccess = true,
            ReportToReturn = new DnsFlusherReport(DscacheutilAvailable: true, LastFlush: null),
        };
        var vm = new DnsFlusherViewModel(engine);
        await vm.ScanAsync();
        int scanBefore = engine.ScanCallCount;

        await vm.FlushAsync();

        Assert.True(engine.ScanCallCount > scanBefore,
            "Expected re-scan after successful flush to refresh LastFlush display");
    }

    // ── 8. FlushCommand transitions Flushing → Success → Idle ────

    [Fact]
    public async Task FlushCommand_TransitionsToSuccessState_OnSuccess()
    {
        AuraCore.UI.Avalonia.LocalizationService.SetLanguage("en");
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OptimizeSuccess = true,
            ReportToReturn = new DnsFlusherReport(DscacheutilAvailable: true, LastFlush: DateTime.UtcNow),
        };
        var vm = new DnsFlusherViewModel(engine);
        await vm.ScanAsync();

        await vm.FlushAsync();

        // Immediately after FlushAsync completes, VM should be in Success state
        // (the 3-second transient revert runs in a fire-and-forget Task.Run).
        Assert.Equal(DnsFlusherFeedbackState.Success, vm.FeedbackState);
        Assert.True(vm.IsSuccess);
        Assert.Contains("flushed just now", vm.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    // ── 9. FlushCommand disabled when !DscacheutilAvailable ──────

    [Fact]
    public async Task FlushCommand_IsDisabled_WhenDscacheutilUnavailable()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = false,
            ReportToReturn = new DnsFlusherReport(DscacheutilAvailable: false, LastFlush: null),
            BlockedReason = "dscacheutil not available",
        };
        var vm = new DnsFlusherViewModel(engine);
        await vm.ScanAsync();

        Assert.False(vm.FlushCommand.CanExecute(null));
    }

    // ── 10. FlushCommand sets ErrorMessage on engine exception ────

    [Fact]
    public async Task FlushCommand_SetsErrorMessage_OnEngineException()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            ReportToReturn = new DnsFlusherReport(DscacheutilAvailable: true, LastFlush: null),
            ThrowOnOptimize = true,
        };
        var vm = new DnsFlusherViewModel(engine);
        await vm.ScanAsync();

        await vm.FlushAsync();

        Assert.True(vm.HasError);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Equal(DnsFlusherFeedbackState.Error, vm.FeedbackState);
    }

    // ── 11. LastFlushDisplay returns localized "never" when null ──

    [Fact]
    public void LastFlushDisplay_ReturnsLocalizedNever_WhenLastFlushIsNull()
    {
        AuraCore.UI.Avalonia.LocalizationService.SetLanguage("en");
        var engine = new FakeEngine();
        var vm = new DnsFlusherViewModel(engine);

        Assert.Equal("Never flushed in this session", vm.LastFlushDisplay);
    }

    // ── 12. LastFlushDisplay returns "Just now"-style prefix ──────

    [Fact]
    public async Task LastFlushDisplay_ReturnsJustNowPrefix_WhenFlushVeryRecent()
    {
        AuraCore.UI.Avalonia.LocalizationService.SetLanguage("en");
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            ReportToReturn = new DnsFlusherReport(DscacheutilAvailable: true, LastFlush: DateTime.UtcNow),
        };
        var vm = new DnsFlusherViewModel(engine);

        await vm.ScanAsync();

        Assert.Contains("Last flushed:", vm.LastFlushDisplay);
        Assert.Contains("Just now", vm.LastFlushDisplay);
    }

    // ── 13. LastFlushDisplay returns relative "minutes ago" ───────

    [Fact]
    public async Task LastFlushDisplay_ReturnsMinutesAgo_ForRecentFlush()
    {
        AuraCore.UI.Avalonia.LocalizationService.SetLanguage("en");
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            ReportToReturn = new DnsFlusherReport(
                DscacheutilAvailable: true,
                LastFlush: DateTime.UtcNow.AddMinutes(-5)),
        };
        var vm = new DnsFlusherViewModel(engine);

        await vm.ScanAsync();

        Assert.Contains("Last flushed:", vm.LastFlushDisplay);
        Assert.Contains("minutes ago", vm.LastFlushDisplay);
    }

    // ── 14. RelativeTimeFormatter boundaries ─────────────────────

    [Fact]
    public void RelativeTimeFormatter_BoundariesAreCorrect()
    {
        AuraCore.UI.Avalonia.LocalizationService.SetLanguage("en");
        var now = new DateTime(2026, 4, 16, 12, 0, 0, DateTimeKind.Utc);

        // <10s → justNow
        Assert.Equal("Just now", RelativeTimeFormatter.Format(now.AddSeconds(-5), now));
        Assert.Equal("Just now", RelativeTimeFormatter.Format(now.AddSeconds(-9), now));

        // [10s, 60s) → secondsAgo
        Assert.Contains("seconds ago", RelativeTimeFormatter.Format(now.AddSeconds(-15), now));
        Assert.Contains("seconds ago", RelativeTimeFormatter.Format(now.AddSeconds(-59), now));

        // [60s, 1h) → minutesAgo
        Assert.Contains("minutes ago", RelativeTimeFormatter.Format(now.AddMinutes(-1), now));
        Assert.Contains("minutes ago", RelativeTimeFormatter.Format(now.AddMinutes(-59), now));

        // [1h, 24h) → hoursAgo
        Assert.Contains("hours ago", RelativeTimeFormatter.Format(now.AddHours(-1), now));
        Assert.Contains("hours ago", RelativeTimeFormatter.Format(now.AddHours(-23), now));

        // ≥24h → absolute date — just verify it returns a date-like string
        var absolute = RelativeTimeFormatter.Format(now.AddDays(-2), now);
        Assert.DoesNotContain("ago", absolute);
        Assert.DoesNotContain("Just now", absolute);
        Assert.Matches(@"\d{4}-\d{2}-\d{2}", absolute);
    }

    // ── 15. FlushCommand disabled when IsBusy ────────────────────

    [Fact]
    public async Task FlushCommand_IsDisabled_WhenIsBusy()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OptimizeSuccess = true,
            ReportToReturn = new DnsFlusherReport(DscacheutilAvailable: true, LastFlush: null),
            SimulateLongOptimize = true,
        };
        var vm = new DnsFlusherViewModel(engine);
        await vm.ScanAsync();

        Assert.True(vm.FlushCommand.CanExecute(null), "Precondition: command executable pre-flush");

        var flushTask = vm.FlushAsync();
        for (int i = 0; i < 20 && !vm.IsBusy; i++)
            await Task.Yield();

        Assert.True(vm.IsBusy, "Precondition: VM entered busy state");
        Assert.False(vm.FlushCommand.CanExecute(null), "Expected command disabled while IsBusy");

        // Let the long flush finish so the test exits cleanly
        try { await flushTask; } catch { }
    }

    // ── Helpers ────────────────────────────────────────────────────

    private static async Task WaitUntilNotBusy(DnsFlusherViewModel vm, int maxIter = 200)
    {
        for (int i = 0; i < maxIter && vm.IsBusy; i++)
            await Task.Yield();
    }

    private sealed class FakeEngine : IDnsFlusherEngine
    {
        public int ScanCallCount;
        public bool ScanSuccess = true;
        public bool OptimizeSuccess = true;
        public bool ThrowOnOptimize;
        public bool SimulateLongOptimize;
        public string? BlockedReason;
        public DnsFlusherReport? ReportToReturn;
        public OptimizationPlan? LastOptimizePlan;

        public string Id => "dns-flusher";
        public DnsFlusherReport? LastReport { get; private set; }

        public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
        {
            ScanCallCount++;
            await Task.Yield();
            LastReport = ReportToReturn;
            return ScanSuccess
                ? new ScanResult(Id, true, 1, 0, null)
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
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
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
