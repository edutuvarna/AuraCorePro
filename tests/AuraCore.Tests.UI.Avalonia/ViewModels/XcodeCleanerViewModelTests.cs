using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.Application;
using AuraCore.Module.XcodeCleaner;
using AuraCore.Module.XcodeCleaner.Models;
using AuraCore.UI.Avalonia;
using AuraCore.UI.Avalonia.ViewModels;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.ViewModels;

/// <summary>
/// Phase 4.4.4 Xcode Cleaner VM unit tests. Uses a hand-rolled
/// <see cref="IXcodeCleanerEngine"/> fake — matches DnsFlusher /
/// PurgeableSpaceManager / SpotlightManager FakeEngine style.
/// </summary>
[Collection("Localization")]
public class XcodeCleanerViewModelTests
{
    // ── 1. Ctor ───────────────────────────────────────────────────

    [Fact]
    public void Ctor_DoesNotThrow_WithValidEngine()
    {
        var engine = new FakeEngine();
        var vm = new XcodeCleanerViewModel(engine);
        Assert.NotNull(vm);
        Assert.False(vm.IsBusy);
        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.HasError);
        Assert.Null(vm.Report);
        Assert.False(vm.DangerAcknowledged);
        Assert.False(vm.XcodeInstalled);
        Assert.Empty(vm.SafeCategoriesItems);
        Assert.Empty(vm.GranularCategoriesItems);
        Assert.Empty(vm.DangerCategoriesItems);
    }

    [Fact]
    public void Ctor_NullEngine_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new XcodeCleanerViewModel((IXcodeCleanerEngine)null!));
        Assert.Throws<ArgumentNullException>(() =>
            new XcodeCleanerViewModel((XcodeCleanerModule)null!));
    }

    // ── 2. TriggerInitialScan invokes engine ──────────────────────

    [Fact]
    public async Task TriggerInitialScan_InvokesScanAsync()
    {
        var engine = new FakeEngine { ScanSuccess = true, ReportToReturn = StandardReport() };
        var vm = new XcodeCleanerViewModel(engine);

        vm.TriggerInitialScan();
        for (int i = 0; i < 50 && engine.ScanCallCount == 0; i++)
            await Task.Yield();
        await WaitUntilNotBusy(vm);

        Assert.True(engine.ScanCallCount >= 1);
    }

    // ── 3. Scan populates Safe bucket ─────────────────────────────

    [Fact]
    public async Task ScanAsync_PopulatesSafeBucket_WithThreeAlwaysSafeCategories()
    {
        var engine = new FakeEngine { ScanSuccess = true, ReportToReturn = StandardReport() };
        var vm = new XcodeCleanerViewModel(engine);

        await vm.ScanAsync();

        Assert.Equal(3, vm.SafeCategoriesItems.Count);
        Assert.Equal("derived-data", vm.SafeCategoriesItems[0].Id);
        Assert.Equal("simulator-caches", vm.SafeCategoriesItems[1].Id);
        Assert.Equal("xcode-cache", vm.SafeCategoriesItems[2].Id);
    }

    // ── 4. Scan populates Granular bucket ─────────────────────────

    [Fact]
    public async Task ScanAsync_PopulatesGranularBucket_WithSimulatorDevicesAndPseudoUnavailable()
    {
        var engine = new FakeEngine { ScanSuccess = true, ReportToReturn = StandardReport() };
        var vm = new XcodeCleanerViewModel(engine);

        await vm.ScanAsync();

        Assert.Equal(2, vm.GranularCategoriesItems.Count);
        Assert.Equal("simulator-devices", vm.GranularCategoriesItems[0].Id);
        Assert.False(vm.GranularCategoriesItems[0].IsPseudo);
        Assert.Equal("unavailable-simulators", vm.GranularCategoriesItems[1].Id);
        Assert.True(vm.GranularCategoriesItems[1].IsPseudo);
        // Pseudo size is "--" (engine can't measure pre-exec)
        Assert.Equal("--", vm.GranularCategoriesItems[1].SizeDisplay);
    }

    // ── 5. Scan populates Danger bucket ───────────────────────────

    [Fact]
    public async Task ScanAsync_PopulatesDangerBucket_WithArchivesAnd3DeviceSupport()
    {
        var engine = new FakeEngine { ScanSuccess = true, ReportToReturn = StandardReport() };
        var vm = new XcodeCleanerViewModel(engine);

        await vm.ScanAsync();

        Assert.Equal(4, vm.DangerCategoriesItems.Count);
        Assert.Equal("archives", vm.DangerCategoriesItems[0].Id);
        Assert.Equal("ios-device-support", vm.DangerCategoriesItems[1].Id);
        Assert.Equal("watchos-device-support", vm.DangerCategoriesItems[2].Id);
        Assert.Equal("tvos-device-support", vm.DangerCategoriesItems[3].Id);
    }

    // ── 6. Missing category marked Exists=false ───────────────────

    [Fact]
    public async Task ScanAsync_MissingCategory_MarksExistsFalseAndIsEnabledFalse()
    {
        // Report with only derived-data present, others missing from report.
        var report = new XcodeCleanerReport(
            XcodeInstalled: true,
            HomeDir: "/Users/test",
            Categories: new List<XcodeCacheCategory>
            {
                new("derived-data", "Derived Data", "/dd", 100 * 1024 * 1024, 5, DateTime.UtcNow.AddMonths(-3), Exists: true),
            },
            TotalBytes: 100 * 1024 * 1024);
        var engine = new FakeEngine { ScanSuccess = true, ReportToReturn = report };
        var vm = new XcodeCleanerViewModel(engine);

        await vm.ScanAsync();

        // Safe bucket: derived-data exists, simulator-caches + xcode-cache do not.
        Assert.True(vm.SafeCategoriesItems[0].Exists);
        Assert.False(vm.SafeCategoriesItems[1].Exists);
        Assert.False(vm.SafeCategoriesItems[1].IsEnabled);
        Assert.False(vm.SafeCategoriesItems[2].Exists);
        Assert.False(vm.SafeCategoriesItems[2].IsEnabled);
        // Danger bucket: all missing → all disabled.
        Assert.All(vm.DangerCategoriesItems, d => Assert.False(d.Exists));
    }

    // ── 7. Scan resets DangerAcknowledged ─────────────────────────

    [Fact]
    public async Task ScanAsync_ResetsDangerAcknowledged()
    {
        var engine = new FakeEngine { ScanSuccess = true, ReportToReturn = StandardReport() };
        var vm = new XcodeCleanerViewModel(engine);
        vm.DangerAcknowledged = true;
        Assert.True(vm.DangerAcknowledged);

        await vm.ScanAsync();

        Assert.False(vm.DangerAcknowledged);
    }

    // ── 8. SafeReclaimableBytes excludes risky categories ─────────

    [Fact]
    public async Task SafeReclaimableDisplay_ExcludesRiskyCategories()
    {
        // Set up a report where:
        // - derived-data = 1 GB
        // - simulator-caches = 500 MB
        // - xcode-cache = 100 MB
        // - archives = 2 GB (should be excluded)
        // - simulator-devices = 3 GB (should be excluded)
        // - ios-device-support = 500 MB (should be excluded)
        long gb = 1024L * 1024 * 1024;
        long mb = 1024L * 1024;
        var categories = new List<XcodeCacheCategory>
        {
            new("derived-data", "Derived Data", "/", gb, 10, DateTime.UtcNow, true),
            new("simulator-caches", "Simulator Caches", "/", 500 * mb, 10, DateTime.UtcNow, true),
            new("xcode-cache", "Xcode Cache", "/", 100 * mb, 10, DateTime.UtcNow, true),
            new("archives", "Archives", "/", 2 * gb, 10, DateTime.UtcNow, true),
            new("simulator-devices", "Simulator Devices", "/", 3 * gb, 10, DateTime.UtcNow, true),
            new("ios-device-support", "iOS Device Support", "/", 500 * mb, 10, DateTime.UtcNow, true),
            new("watchos-device-support", "watchOS Device Support", "/", 0, 0, null, false),
            new("tvos-device-support", "tvOS Device Support", "/", 0, 0, null, false),
        };
        var report = new XcodeCleanerReport(true, "/", categories, gb + 500 * mb + 100 * mb + 2 * gb + 3 * gb + 500 * mb);
        var engine = new FakeEngine { ScanSuccess = true, ReportToReturn = report };
        var vm = new XcodeCleanerViewModel(engine);

        await vm.ScanAsync();

        // Safe total = derived-data + simulator-caches + xcode-cache
        //            = 1 GB + 500 MB + 100 MB = 1.59 GB approx
        long expectedSafe = gb + 500 * mb + 100 * mb;
        Assert.Equal(expectedSafe, vm.SafeReclaimableBytes);
        // Not the full total (which would be much larger)
        Assert.NotEqual(report.TotalBytes, vm.SafeReclaimableBytes);
    }

    // ── 9. PruneSafeCommand dispatches engine "all" ───────────────

    [Fact]
    public async Task PruneSafeCommand_DispatchesEngineWithAllItemId()
    {
        var engine = new FakeEngine { ScanSuccess = true, OptimizeSuccess = true, ReportToReturn = StandardReport() };
        var vm = new XcodeCleanerViewModel(engine);
        await vm.ScanAsync();

        await vm.PruneItemAsync("all");

        Assert.NotNull(engine.LastOptimizePlan);
        Assert.Single(engine.LastOptimizePlan!.SelectedItemIds);
        Assert.Equal("all", engine.LastOptimizePlan!.SelectedItemIds[0]);
    }

    // ── 10. PruneCategoryCommand dispatches engine <category-id> ──

    [Fact]
    public async Task PruneCategoryCommand_DispatchesEngineWithCategoryId()
    {
        var engine = new FakeEngine { ScanSuccess = true, OptimizeSuccess = true, ReportToReturn = StandardReport() };
        var vm = new XcodeCleanerViewModel(engine);
        await vm.ScanAsync();

        var archives = vm.DangerCategoriesItems[0];
        Assert.Equal("archives", archives.Id);
        await vm.PruneCategoryAsync(archives);

        Assert.NotNull(engine.LastOptimizePlan);
        Assert.Single(engine.LastOptimizePlan!.SelectedItemIds);
        Assert.Equal("archives", engine.LastOptimizePlan!.SelectedItemIds[0]);
    }

    // ── 11. PruneCategoryCommand dispatches "unavailable-simulators" for pseudo ─

    [Fact]
    public async Task PruneCategoryCommand_DispatchesUnavailableSimulators_ForPseudo()
    {
        var engine = new FakeEngine { ScanSuccess = true, OptimizeSuccess = true, ReportToReturn = StandardReport() };
        var vm = new XcodeCleanerViewModel(engine);
        await vm.ScanAsync();

        var pseudo = vm.GranularCategoriesItems[1];
        Assert.True(pseudo.IsPseudo);
        Assert.Equal("unavailable-simulators", pseudo.Id);

        await vm.PruneCategoryAsync(pseudo);

        Assert.NotNull(engine.LastOptimizePlan);
        Assert.Equal("unavailable-simulators", engine.LastOptimizePlan!.SelectedItemIds[0]);
    }

    // ── 12. PruneDangerAllCommand dispatches archives + device-support sequence ─

    [Fact]
    public async Task PruneDangerAllCommand_DispatchesArchivesAnd3DeviceSupport_Sequence()
    {
        var engine = new FakeEngine { ScanSuccess = true, OptimizeSuccess = true, ReportToReturn = StandardReport() };
        var vm = new XcodeCleanerViewModel(engine);
        await vm.ScanAsync();
        vm.DangerAcknowledged = true;

        await vm.PruneDangerAllAsync();

        Assert.NotNull(engine.LastOptimizePlan);
        var items = engine.LastOptimizePlan!.SelectedItemIds;
        Assert.Equal(4, items.Count);
        Assert.Contains("archives", items);
        Assert.Contains("ios-device-support", items);
        Assert.Contains("watchos-device-support", items);
        Assert.Contains("tvos-device-support", items);
    }

    // ── 13. PruneDangerAllCommand disabled when !DangerAcknowledged ─

    [Fact]
    public async Task PruneDangerAllCommand_Disabled_WhenNotAcknowledged()
    {
        var engine = new FakeEngine { ScanSuccess = true, OptimizeSuccess = true, ReportToReturn = StandardReport() };
        var vm = new XcodeCleanerViewModel(engine);
        await vm.ScanAsync();

        Assert.False(vm.DangerAcknowledged);
        Assert.False(vm.PruneDangerAllCommand.CanExecute(null));

        vm.DangerAcknowledged = true;
        Assert.True(vm.PruneDangerAllCommand.CanExecute(null));
    }

    // ── 14. PruneSafeCommand triggers rescan on success ───────────

    [Fact]
    public async Task PruneSafeCommand_TriggersRescanOnSuccess()
    {
        var engine = new FakeEngine { ScanSuccess = true, OptimizeSuccess = true, ReportToReturn = StandardReport() };
        var vm = new XcodeCleanerViewModel(engine);
        await vm.ScanAsync();
        var scansBefore = engine.ScanCallCount;

        await vm.PruneItemAsync("all");

        // One optimize + one rescan
        Assert.True(engine.ScanCallCount >= scansBefore + 1);
    }

    // ── 15. All prune commands reset DangerAcknowledged ───────────

    [Fact]
    public async Task AllPruneCommands_ResetDangerAcknowledgedOnCompletion()
    {
        var engine = new FakeEngine { ScanSuccess = true, OptimizeSuccess = true, ReportToReturn = StandardReport() };
        var vm = new XcodeCleanerViewModel(engine);
        await vm.ScanAsync();

        vm.DangerAcknowledged = true;
        await vm.PruneItemAsync("all");
        Assert.False(vm.DangerAcknowledged);

        vm.DangerAcknowledged = true;
        await vm.PruneCategoryAsync(vm.DangerCategoriesItems[0]);
        Assert.False(vm.DangerAcknowledged);

        vm.DangerAcknowledged = true;
        await vm.PruneDangerAllAsync();
        Assert.False(vm.DangerAcknowledged);
    }

    // ── 16. FormatSize sanity ─────────────────────────────────────

    [Fact]
    public void FormatSize_HandlesBoundaries()
    {
        Assert.Equal("--", XcodeCleanerViewModel.FormatSize(0));
        Assert.Equal("--", XcodeCleanerViewModel.FormatSize(-1));
        Assert.Equal("500 B", XcodeCleanerViewModel.FormatSize(500));
        Assert.Equal("1 KB", XcodeCleanerViewModel.FormatSize(1024));
        Assert.Equal("1 MB", XcodeCleanerViewModel.FormatSize(1024 * 1024));
        Assert.Equal("1.00 GB", XcodeCleanerViewModel.FormatSize(1024L * 1024 * 1024));
    }

    // ── 17. HumanizeAge sanity ────────────────────────────────────

    [Fact]
    public void HumanizeAge_HandlesRanges()
    {
        var now = DateTime.UtcNow;
        Assert.Equal("<1 day", XcodeCleanerViewModel.HumanizeAge(now.AddHours(-1)));
        Assert.Equal("5 days", XcodeCleanerViewModel.HumanizeAge(now.AddDays(-5)));
        Assert.Equal("8 months", XcodeCleanerViewModel.HumanizeAge(now.AddDays(-240)));
        Assert.Equal("2 years", XcodeCleanerViewModel.HumanizeAge(now.AddDays(-800)));
    }

    // ── 18. OldestItemDisplay picks minimum across existing categories ─

    [Fact]
    public async Task OldestItemDisplay_ReturnsOldestFromExistingCategories()
    {
        var now = DateTime.UtcNow;
        var categories = new List<XcodeCacheCategory>
        {
            new("derived-data", "Derived Data", "/", 1024, 1, now.AddDays(-30), true),
            new("archives", "Archives", "/", 1024, 1, now.AddDays(-800), true), // oldest: ~2 years
            new("simulator-caches", "Simulator Caches", "/", 0, 0, null, false), // absent, should be ignored
            new("simulator-devices", "Simulator Devices", "/", 1024, 1, now.AddDays(-60), true),
            new("xcode-cache", "Xcode Cache", "/", 0, 0, null, false),
            new("ios-device-support", "iOS Device Support", "/", 0, 0, null, false),
            new("watchos-device-support", "watchOS Device Support", "/", 0, 0, null, false),
            new("tvos-device-support", "tvOS Device Support", "/", 0, 0, null, false),
        };
        var report = new XcodeCleanerReport(true, "/", categories, 3072);
        var engine = new FakeEngine { ScanSuccess = true, ReportToReturn = report };
        var vm = new XcodeCleanerViewModel(engine);

        await vm.ScanAsync();

        Assert.Equal("2 years", vm.OldestItemDisplay);
    }

    // ── 19. ErrorMessage populated on engine exception ────────────

    [Fact]
    public async Task PruneSafe_PopulatesErrorMessage_OnEngineException()
    {
        var engine = new FakeEngine { ScanSuccess = true, ThrowOnOptimize = true, ReportToReturn = StandardReport() };
        var vm = new XcodeCleanerViewModel(engine);
        await vm.ScanAsync();

        await vm.PruneItemAsync("all");

        Assert.NotNull(vm.ErrorMessage);
        Assert.True(vm.HasError);
    }

    // ── 20. Cancel handling ───────────────────────────────────────

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
        var vm = new XcodeCleanerViewModel(engine);
        await vm.ScanAsync();

        var task = vm.PruneItemAsync("all");
        for (int i = 0; i < 50 && !vm.IsBusy; i++)
            await Task.Yield();

        Assert.True(vm.IsBusy);
        Assert.True(vm.CancelCommand.CanExecute(null));

        try { vm.CancelCommand.Execute(null); } catch { }
        try { await task; } catch { }
    }

    // ── 21. CategoriesPresentDisplay shows "n of 8" ───────────────

    [Fact]
    public async Task CategoriesPresentDisplay_ShowsExistingOfTotal()
    {
        var engine = new FakeEngine { ScanSuccess = true, ReportToReturn = StandardReport() };
        var vm = new XcodeCleanerViewModel(engine);

        Assert.Equal("--", vm.CategoriesPresentDisplay);

        await vm.ScanAsync();

        // StandardReport has derived-data + archives + simulator-caches + xcode-cache (4 existing)
        // Should format against "8" total.
        Assert.Contains("4", vm.CategoriesPresentDisplay);
        Assert.Contains("8", vm.CategoriesPresentDisplay);
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static XcodeCleanerReport StandardReport()
    {
        long mb = 1024L * 1024;
        var now = DateTime.UtcNow;
        // 4 categories present, 4 missing — exercises the IsEnabled gating.
        var categories = new List<XcodeCacheCategory>
        {
            new("derived-data", "Derived Data", "/h/dd", 500 * mb, 10, now.AddMonths(-3), true),
            new("archives", "Archives", "/h/ar", 200 * mb, 2, now.AddMonths(-6), true),
            new("simulator-caches", "Simulator Caches", "/h/sc", 100 * mb, 5, now.AddDays(-30), true),
            new("simulator-devices", "Simulator Devices", "/h/sd", 0, 0, null, false),
            new("xcode-cache", "Xcode Cache", "/h/xc", 50 * mb, 3, now.AddDays(-60), true),
            new("ios-device-support", "iOS Device Support", "/h/ios", 0, 0, null, false),
            new("watchos-device-support", "watchOS Device Support", "/h/w", 0, 0, null, false),
            new("tvos-device-support", "tvOS Device Support", "/h/tv", 0, 0, null, false),
        };
        return new XcodeCleanerReport(
            XcodeInstalled: true,
            HomeDir: "/home",
            Categories: categories,
            TotalBytes: 850 * mb);
    }

    private static async Task WaitUntilNotBusy(XcodeCleanerViewModel vm, int maxIter = 200)
    {
        for (int i = 0; i < maxIter && vm.IsBusy; i++)
            await Task.Yield();
    }

    private sealed class FakeEngine : IXcodeCleanerEngine
    {
        public int ScanCallCount;
        public bool ScanSuccess = true;
        public bool OptimizeSuccess = true;
        public bool ThrowOnOptimize;
        public bool SimulateLongOptimize;
        public string? BlockedReason;
        public XcodeCleanerReport? ReportToReturn;
        public OptimizationPlan? LastOptimizePlan;

        public string Id => "xcode-cleaner";
        public XcodeCleanerReport? LastReport { get; private set; }

        public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
        {
            ScanCallCount++;
            await Task.Yield();
            LastReport = ReportToReturn;
            return ScanSuccess
                ? new ScanResult(Id, true, LastReport?.Categories?.Count(c => c.Exists) ?? 0, LastReport?.TotalBytes ?? 0, null)
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
