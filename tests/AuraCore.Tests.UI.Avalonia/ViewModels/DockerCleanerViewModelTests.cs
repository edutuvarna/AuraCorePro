using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.Application;
using AuraCore.Module.DockerCleaner;
using AuraCore.Module.DockerCleaner.Models;
using AuraCore.UI.Avalonia.ViewModels;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.ViewModels;

/// <summary>
/// Phase 4.3.3 Docker Cleaner VM unit tests.
/// Uses a hand-rolled <see cref="IDockerCleanerEngine"/> fake — matches the JournalCleaner /
/// SnapFlatpakCleaner FakeEngine style (no Moq dependency in the test project).
/// </summary>
public class DockerCleanerViewModelTests
{
    // ── 1. Ctor ────────────────────────────────────────────────────

    [Fact]
    public void Ctor_DoesNotThrow_WithValidDependencies()
    {
        var engine = new FakeEngine();
        var vm = new DockerCleanerViewModel(engine);
        Assert.NotNull(vm);
        Assert.False(vm.IsBusy);
        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.HasError);
        Assert.Null(vm.Report);
        Assert.Equal("--", vm.ImagesDisplay);
        Assert.Equal("--", vm.ContainersDisplay);
        Assert.Equal("--", vm.VolumesDisplay);
        Assert.Equal("--", vm.BuildCacheDisplay);
        Assert.Equal("--", vm.ReclaimableSafeDisplay);
        Assert.False(vm.DockerAvailable);
        Assert.False(vm.VolumeRiskAcknowledged);
    }

    [Fact]
    public void Ctor_NullEngine_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DockerCleanerViewModel((IDockerCleanerEngine)null!));
        Assert.Throws<ArgumentNullException>(() =>
            new DockerCleanerViewModel((DockerCleanerModule)null!));
    }

    // ── 2. ScanCommand invokes engine scan ─────────────────────────

    [Fact]
    public async Task ScanCommand_InvokesEngineScan()
    {
        var engine = new FakeEngine();
        var vm = new DockerCleanerViewModel(engine);

        await vm.ScanAsync();

        Assert.Equal(1, engine.ScanCallCount);
    }

    // ── 3. ScanCommand populates display strings on success ────────

    [Fact]
    public async Task ScanCommand_PopulatesDisplayStrings_OnSuccess()
    {
        var report = new DockerReport(
            DockerAvailable: true,
            DockerVersion: "24.0.1",
            TotalContainers: 12,
            StoppedContainers: 5,
            DanglingImages: 3,
            UnusedVolumes: 2,
            ImagesTotalBytes: (long)(2.3 * 1024 * 1024 * 1024), // ~2.3 GB
            VolumesTotalBytes: 500L * 1024 * 1024,              // 500 MB
            BuildCacheBytes: 100L * 1024 * 1024,                // 100 MB
            TotalReclaimableBytes: 1L * 1024 * 1024 * 1024,
            ImagesReclaimableBytes: 800L * 1024 * 1024,
            ContainersReclaimableBytes: 50L * 1024 * 1024,
            VolumesReclaimableBytes: 100L * 1024 * 1024,
            BuildCacheReclaimableBytes: 50L * 1024 * 1024);
        var engine = new FakeEngine { ScanSuccess = true, ReportToReturn = report };
        var vm = new DockerCleanerViewModel(engine);

        await vm.ScanAsync();

        Assert.NotNull(vm.Report);
        Assert.Equal("2.30 GB", vm.ImagesDisplay);
        Assert.Contains("12", vm.ContainersDisplay);
        Assert.Contains("5", vm.ContainersDisplay);
        Assert.Equal("500 MB", vm.VolumesDisplay);
        Assert.Equal("100 MB", vm.BuildCacheDisplay);
    }

    [Fact]
    public async Task ScanCommand_SetsDockerAvailable_WhenReportDockerAvailableTrue()
    {
        var report = new DockerReport(true, "24.0.1", 0, 0, 0, 0, 0, 0, 0, 0);
        var engine = new FakeEngine { ScanSuccess = true, ReportToReturn = report };
        var vm = new DockerCleanerViewModel(engine);

        await vm.ScanAsync();

        Assert.True(vm.DockerAvailable);
    }

    // ── 4. VolumeRiskAcknowledged is reset on new scan ─────────────

    [Fact]
    public async Task ScanCommand_ResetsVolumeRiskAcknowledged()
    {
        var report = new DockerReport(true, "24.0.1", 0, 0, 0, 0, 0, 0, 0, 0);
        var engine = new FakeEngine { ScanSuccess = true, ReportToReturn = report };
        var vm = new DockerCleanerViewModel(engine);

        vm.VolumeRiskAcknowledged = true;
        Assert.True(vm.VolumeRiskAcknowledged);

        await vm.ScanAsync();

        Assert.False(vm.VolumeRiskAcknowledged);
    }

    // ── 5. ScanCommand surfaces engine error ───────────────────────

    [Fact]
    public async Task ScanCommand_SetsErrorMessage_OnException()
    {
        var engine = new FakeEngine { ScanThrows = true };
        var vm = new DockerCleanerViewModel(engine);

        await vm.ScanAsync();

        Assert.True(vm.HasError);
        Assert.False(vm.IsBusy);
    }

    // ── 6. Each prune command maps to the correct engine itemId ───

    [Fact]
    public async Task PruneSafeCommand_CallsEngineWith_PruneSystemItemId()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OptimizeSuccess = true,
            ReportToReturn = new DockerReport(true, "24.0.1", 0, 0, 0, 0, 0, 0, 0, 0),
        };
        var vm = new DockerCleanerViewModel(engine);
        await vm.ScanAsync();

        await vm.PruneAsync("prune-system");

        Assert.NotNull(engine.LastOptimizePlan);
        Assert.Single(engine.LastOptimizePlan!.SelectedItemIds);
        Assert.Equal("prune-system", engine.LastOptimizePlan.SelectedItemIds[0]);
    }

    [Fact]
    public async Task PruneImagesCommand_CallsEngineWith_PruneDanglingImagesItemId()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OptimizeSuccess = true,
            ReportToReturn = new DockerReport(true, "24.0.1", 0, 0, 0, 0, 0, 0, 0, 0),
        };
        var vm = new DockerCleanerViewModel(engine);
        await vm.ScanAsync();

        await vm.PruneAsync("prune-dangling-images");

        Assert.Equal("prune-dangling-images", engine.LastOptimizePlan!.SelectedItemIds[0]);
    }

    [Fact]
    public async Task PruneContainersCommand_CallsEngineWith_PruneContainersItemId()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OptimizeSuccess = true,
            ReportToReturn = new DockerReport(true, "24.0.1", 0, 0, 0, 0, 0, 0, 0, 0),
        };
        var vm = new DockerCleanerViewModel(engine);
        await vm.ScanAsync();

        await vm.PruneAsync("prune-containers");

        Assert.Equal("prune-containers", engine.LastOptimizePlan!.SelectedItemIds[0]);
    }

    [Fact]
    public async Task PruneBuildCacheCommand_CallsEngineWith_PruneBuildCacheItemId()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OptimizeSuccess = true,
            ReportToReturn = new DockerReport(true, "24.0.1", 0, 0, 0, 0, 0, 0, 0, 0),
        };
        var vm = new DockerCleanerViewModel(engine);
        await vm.ScanAsync();

        await vm.PruneAsync("prune-build-cache");

        Assert.Equal("prune-build-cache", engine.LastOptimizePlan!.SelectedItemIds[0]);
    }

    // ── 7. PruneVolumesCommand gated by VolumeRiskAcknowledged ────

    [Fact]
    public void PruneVolumesCommand_Disabled_WhenVolumeRiskNotAcknowledged()
    {
        var engine = new FakeEngine();
        var vm = new DockerCleanerViewModel(engine);
        // DockerAvailable=false by default → command is disabled for two reasons
        // Set DockerAvailable=true indirectly by scanning
        Assert.False(vm.PruneVolumesCommand.CanExecute(null));
    }

    [Fact]
    public async Task PruneVolumesCommand_Enabled_WhenAllConditionsMet()
    {
        var report = new DockerReport(true, "24.0.1", 0, 0, 0, 0, 0, 0, 0, 0);
        var engine = new FakeEngine { ScanSuccess = true, ReportToReturn = report };
        var vm = new DockerCleanerViewModel(engine);

        await vm.ScanAsync();
        Assert.True(vm.DockerAvailable);
        Assert.False(vm.PruneVolumesCommand.CanExecute(null)); // ack still false after fresh scan

        vm.VolumeRiskAcknowledged = true;
        Assert.True(vm.PruneVolumesCommand.CanExecute(null));
    }

    [Fact]
    public async Task PruneVolumesCommand_ResetsAcknowledgedAfterCompletion()
    {
        var report = new DockerReport(true, "24.0.1", 0, 0, 0, 0, 0, 0, 0, 0);
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OptimizeSuccess = true,
            ReportToReturn = report,
        };
        var vm = new DockerCleanerViewModel(engine);
        await vm.ScanAsync();
        vm.VolumeRiskAcknowledged = true;

        await vm.PruneAsync("prune-volumes");

        Assert.False(vm.VolumeRiskAcknowledged);
    }

    [Fact]
    public async Task PruneVolumesCommand_CallsEngineWith_PruneVolumesItemId()
    {
        var report = new DockerReport(true, "24.0.1", 0, 0, 0, 0, 0, 0, 0, 0);
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OptimizeSuccess = true,
            ReportToReturn = report,
        };
        var vm = new DockerCleanerViewModel(engine);
        await vm.ScanAsync();
        vm.VolumeRiskAcknowledged = true;

        await vm.PruneAsync("prune-volumes");

        Assert.Equal("prune-volumes", engine.LastOptimizePlan!.SelectedItemIds[0]);
    }

    // ── 8. CancelCommand ───────────────────────────────────────────

    [Fact]
    public async Task CancelCommand_CancelsInFlightOperation()
    {
        var engine = new FakeEngine { SimulateLongOptimize = true };
        var vm = new DockerCleanerViewModel(engine);

        var pruneTask = vm.PruneAsync("prune-containers");
        await Task.Delay(50);
        Assert.True(vm.IsBusy, "VM should be busy mid-flight");

        vm.Cancel();

        await pruneTask; // VM swallows OCE
        Assert.False(vm.IsBusy);
    }

    // ── 9. ReclaimableSafeDisplay excludes volumes reclaimable ────

    [Fact]
    public async Task ReclaimableSafeDisplay_ExcludesVolumesReclaimable()
    {
        // images=400MB + containers=100MB + buildCache=200MB + volumes=800MB reclaimable
        // Safe total = 700 MB (volumes excluded)
        var report = new DockerReport(
            DockerAvailable: true,
            DockerVersion: "24.0.1",
            TotalContainers: 0, StoppedContainers: 0, DanglingImages: 0, UnusedVolumes: 0,
            ImagesTotalBytes: 0, VolumesTotalBytes: 0, BuildCacheBytes: 0,
            TotalReclaimableBytes: 1500L * 1024 * 1024,
            ImagesReclaimableBytes: 400L * 1024 * 1024,
            ContainersReclaimableBytes: 100L * 1024 * 1024,
            VolumesReclaimableBytes: 800L * 1024 * 1024,
            BuildCacheReclaimableBytes: 200L * 1024 * 1024);
        var engine = new FakeEngine { ScanSuccess = true, ReportToReturn = report };
        var vm = new DockerCleanerViewModel(engine);

        await vm.ScanAsync();

        Assert.Equal("700 MB", vm.ReclaimableSafeDisplay);
    }

    // ── 10. FormatSize helper sanity ───────────────────────────────

    [Theory]
    [InlineData(0, "--")]
    [InlineData(-1, "--")]
    [InlineData(500, "500 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1024L * 1024, "1 MB")]
    [InlineData((long)(1.5 * 1024 * 1024 * 1024), "1.50 GB")]
    public void FormatSize_ReturnsExpected(long bytes, string expected)
    {
        Assert.Equal(expected, DockerCleanerViewModel.FormatSize(bytes));
    }

    // ── Helpers ────────────────────────────────────────────────────

    /// <summary>Lightweight in-memory test engine — implements the VM's adapter contract.</summary>
    private sealed class FakeEngine : IDockerCleanerEngine
    {
        public int ScanCallCount;
        public bool ScanSuccess = true;
        public bool ScanThrows;
        public string? BlockedReason = null;
        public DockerReport ReportToReturn = DockerReport.None();
        public OptimizationPlan? LastOptimizePlan;
        public bool OptimizeSuccess = true;
        public bool SimulateLongOptimize;

        public string Id => "docker-cleaner";
        public DockerReport? LastReport { get; private set; }

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
