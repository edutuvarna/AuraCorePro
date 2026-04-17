using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.Application;
using AuraCore.Module.GrubManager;
using AuraCore.UI.Avalonia.ViewModels;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.ViewModels;

/// <summary>
/// Phase 4.3.6 GRUB Manager VM unit tests.
/// Uses a hand-rolled <see cref="IGrubManagerEngine"/> fake — matches
/// KernelCleaner / DockerCleaner / SnapFlatpak / Journal FakeEngine style
/// (no Moq dependency in the test project).
/// </summary>
public class GrubManagerViewModelTests
{
    // ── 1. Ctor ────────────────────────────────────────────────────

    [Fact]
    public void Ctor_DoesNotThrow_WithValidDependencies()
    {
        var engine = new FakeEngine();
        var vm = new GrubManagerViewModel(engine);
        Assert.NotNull(vm);
        Assert.False(vm.IsBusy);
        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.HasError);
        Assert.False(vm.HasPendingChanges);
        Assert.False(vm.HasBackup);
        Assert.False(vm.BackupAcknowledged);
        Assert.Empty(vm.KernelList);
    }

    [Fact]
    public void Ctor_NullEngine_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GrubManagerViewModel((IGrubManagerEngine)null!));
        Assert.Throws<ArgumentNullException>(() =>
            new GrubManagerViewModel((GrubManagerModule)null!));
    }

    // ── 2. TriggerInitialScan calls engine.ScanAsync ──────────────

    [Fact]
    public async Task TriggerInitialScan_InvokesScanAsync()
    {
        var engine = new FakeEngine { ScanSuccess = true, SettingsToReturn = BuildSettings() };
        var vm = new GrubManagerViewModel(engine);

        vm.TriggerInitialScan();
        // TriggerInitialScan is fire-and-forget; give it a few turns to finish
        for (int i = 0; i < 10 && engine.ScanCallCount == 0; i++)
            await Task.Yield();
        await WaitUntilNotBusy(vm);

        Assert.True(engine.ScanCallCount >= 1);
    }

    // ── 3. Scan populates Current* + mirrors Pending* ─────────────

    [Fact]
    public async Task ScanAsync_PopulatesCurrentAndPendingValues()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            SettingsToReturn = BuildSettings(timeout: 7, grubDefault: "3", osProberDisabled: true,
                cmdline: "quiet splash",
                kernels: new List<string> { "vmlinuz-6.8.0-52-generic", "vmlinuz-5.15.0-100-generic" }),
        };
        var vm = new GrubManagerViewModel(engine);

        await vm.ScanAsync();

        Assert.Equal(7, vm.CurrentTimeout);
        Assert.Equal("3", vm.CurrentGrubDefault);
        Assert.False(vm.CurrentOsProberEnabled); // UI semantic: Disabled==true → UI enabled==false
        Assert.Equal("quiet splash", vm.CurrentCmdlineLinuxDefault);
        Assert.Equal(7, vm.PendingTimeout);
        Assert.Equal("3", vm.PendingGrubDefault);
        Assert.False(vm.PendingOsProberEnabled);
        Assert.False(vm.HasPendingChanges); // Pending == Current after fresh scan
    }

    // ── 4. Scan sets HasBackup based on engine.CanRollbackAsync ────

    [Fact]
    public async Task ScanAsync_SetsHasBackup_FromCanRollbackResult()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            SettingsToReturn = BuildSettings(),
            CanRollbackResult = true,
        };
        var vm = new GrubManagerViewModel(engine);

        await vm.ScanAsync();
        Assert.True(vm.HasBackup);

        engine.CanRollbackResult = false;
        await vm.ScanAsync();
        Assert.False(vm.HasBackup);
    }

    // ── 5. Scan sets KernelList ───────────────────────────────────

    [Fact]
    public async Task ScanAsync_SetsKernelList_FromSettings()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            SettingsToReturn = BuildSettings(kernels: new List<string>
            {
                "vmlinuz-6.8.0-52-generic",
                "vmlinuz-5.15.0-100-generic",
                "vmlinuz-6.2.0-30-generic",
            }),
        };
        var vm = new GrubManagerViewModel(engine);

        await vm.ScanAsync();

        Assert.Equal(3, vm.KernelList.Count);
        Assert.Contains("vmlinuz-6.8.0-52-generic", vm.KernelList);
    }

    // ── 6. HasPendingChanges after scan ─────────────────────────────

    [Fact]
    public async Task ScanAsync_HasPendingChangesIsFalse_AfterFreshScan()
    {
        var engine = new FakeEngine { ScanSuccess = true, SettingsToReturn = BuildSettings() };
        var vm = new GrubManagerViewModel(engine);

        await vm.ScanAsync();

        Assert.False(vm.HasPendingChanges);
    }

    // ── 7. Changing PendingTimeout sets HasPendingChanges=true ────

    [Fact]
    public async Task ChangingPendingTimeout_FlipsHasPendingChanges()
    {
        var engine = new FakeEngine { ScanSuccess = true, SettingsToReturn = BuildSettings(timeout: 5) };
        var vm = new GrubManagerViewModel(engine);
        await vm.ScanAsync();

        vm.PendingTimeout = 10;

        Assert.True(vm.HasPendingChanges);
    }

    // ── 8. Changing PendingGrubDefault sets HasPendingChanges=true ─

    [Fact]
    public async Task ChangingPendingGrubDefault_FlipsHasPendingChanges()
    {
        var engine = new FakeEngine { ScanSuccess = true, SettingsToReturn = BuildSettings(grubDefault: "0") };
        var vm = new GrubManagerViewModel(engine);
        await vm.ScanAsync();

        vm.PendingGrubDefault = "saved";

        Assert.True(vm.HasPendingChanges);
    }

    // ── 9. Changing PendingOsProberEnabled sets HasPendingChanges=true

    [Fact]
    public async Task ChangingPendingOsProberEnabled_FlipsHasPendingChanges()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            SettingsToReturn = BuildSettings(osProberDisabled: true),
        };
        var vm = new GrubManagerViewModel(engine);
        await vm.ScanAsync();
        Assert.False(vm.PendingOsProberEnabled);

        vm.PendingOsProberEnabled = true;

        Assert.True(vm.HasPendingChanges);
    }

    // ── 10. Apply builds set-timeout item id ──────────────────────

    [Fact]
    public async Task ApplyAsync_BuildsTimeoutItemId_WhenOnlyTimeoutChanged()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OptimizeSuccess = true,
            SettingsToReturn = BuildSettings(timeout: 5, grubDefault: "0", osProberDisabled: false),
        };
        var vm = new GrubManagerViewModel(engine);
        await vm.ScanAsync();
        vm.PendingTimeout = 10;
        vm.BackupAcknowledged = true;

        await vm.ApplyAsync();

        Assert.NotNull(engine.LastOptimizePlan);
        Assert.Single(engine.LastOptimizePlan!.SelectedItemIds);
        Assert.Equal("set-timeout:10", engine.LastOptimizePlan.SelectedItemIds[0]);
    }

    // ── 11. Apply builds set-default item id ──────────────────────

    [Fact]
    public async Task ApplyAsync_BuildsDefaultItemId_WhenOnlyDefaultChanged()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OptimizeSuccess = true,
            SettingsToReturn = BuildSettings(timeout: 5, grubDefault: "0", osProberDisabled: false),
        };
        var vm = new GrubManagerViewModel(engine);
        await vm.ScanAsync();
        vm.PendingGrubDefault = "saved";
        vm.BackupAcknowledged = true;

        await vm.ApplyAsync();

        Assert.NotNull(engine.LastOptimizePlan);
        Assert.Single(engine.LastOptimizePlan!.SelectedItemIds);
        Assert.Equal("set-default:saved", engine.LastOptimizePlan.SelectedItemIds[0]);
    }

    // ── 12. Apply builds enable-os-prober item id ────────────────

    [Fact]
    public async Task ApplyAsync_BuildsEnableOsProberItemId_WhenToggleFlippedOn()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OptimizeSuccess = true,
            SettingsToReturn = BuildSettings(osProberDisabled: true), // Current: disabled
        };
        var vm = new GrubManagerViewModel(engine);
        await vm.ScanAsync();
        vm.PendingOsProberEnabled = true; // enable → emits "enable-os-prober"
        vm.BackupAcknowledged = true;

        await vm.ApplyAsync();

        Assert.NotNull(engine.LastOptimizePlan);
        Assert.Single(engine.LastOptimizePlan!.SelectedItemIds);
        Assert.Equal("enable-os-prober", engine.LastOptimizePlan.SelectedItemIds[0]);
    }

    // ── 12b. Apply builds disable-os-prober item id ─────────────

    [Fact]
    public async Task ApplyAsync_BuildsDisableOsProberItemId_WhenToggleFlippedOff()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OptimizeSuccess = true,
            SettingsToReturn = BuildSettings(osProberDisabled: false), // Current: enabled
        };
        var vm = new GrubManagerViewModel(engine);
        await vm.ScanAsync();
        Assert.True(vm.PendingOsProberEnabled);
        vm.PendingOsProberEnabled = false;
        vm.BackupAcknowledged = true;

        await vm.ApplyAsync();

        Assert.NotNull(engine.LastOptimizePlan);
        Assert.Single(engine.LastOptimizePlan!.SelectedItemIds);
        Assert.Equal("disable-os-prober", engine.LastOptimizePlan.SelectedItemIds[0]);
    }

    // ── 13. Apply builds multi item list ──────────────────────────

    [Fact]
    public async Task ApplyAsync_BuildsAllThreeItemIds_WhenAllChanged()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OptimizeSuccess = true,
            SettingsToReturn = BuildSettings(timeout: 5, grubDefault: "0", osProberDisabled: true),
        };
        var vm = new GrubManagerViewModel(engine);
        await vm.ScanAsync();
        vm.PendingTimeout = 3;
        vm.PendingGrubDefault = "1";
        vm.PendingOsProberEnabled = true;
        vm.BackupAcknowledged = true;

        await vm.ApplyAsync();

        Assert.NotNull(engine.LastOptimizePlan);
        Assert.Equal(3, engine.LastOptimizePlan!.SelectedItemIds.Count);
        Assert.Contains("set-timeout:3", engine.LastOptimizePlan.SelectedItemIds);
        Assert.Contains("set-default:1", engine.LastOptimizePlan.SelectedItemIds);
        Assert.Contains("enable-os-prober", engine.LastOptimizePlan.SelectedItemIds);
    }

    // ── 14. Apply disabled when BackupAcknowledged is false ───────

    [Fact]
    public async Task ApplyAsync_DoesNothing_WhenBackupAckIsFalse()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OptimizeSuccess = true,
            SettingsToReturn = BuildSettings(timeout: 5),
        };
        var vm = new GrubManagerViewModel(engine);
        await vm.ScanAsync();
        vm.PendingTimeout = 10;
        Assert.False(vm.BackupAcknowledged);

        await vm.ApplyAsync();

        Assert.Null(engine.LastOptimizePlan);
    }

    // ── 15. Apply disabled when no pending changes ───────────────

    [Fact]
    public async Task ApplyAsync_DoesNothing_WhenNoPendingChanges()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OptimizeSuccess = true,
            SettingsToReturn = BuildSettings(timeout: 5),
        };
        var vm = new GrubManagerViewModel(engine);
        await vm.ScanAsync();
        vm.BackupAcknowledged = true;
        Assert.False(vm.HasPendingChanges);

        await vm.ApplyAsync();

        Assert.Null(engine.LastOptimizePlan);
    }

    // ── 16. Apply auto-rescans on success ─────────────────────────

    [Fact]
    public async Task ApplyAsync_TriggersRescan_OnSuccess()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OptimizeSuccess = true,
            SettingsToReturn = BuildSettings(timeout: 5),
        };
        var vm = new GrubManagerViewModel(engine);
        await vm.ScanAsync();
        int scanBefore = engine.ScanCallCount;
        vm.PendingTimeout = 10;
        vm.BackupAcknowledged = true;

        await vm.ApplyAsync();

        Assert.True(engine.ScanCallCount > scanBefore, "Expected a re-scan after apply success");
    }

    // ── 17. Apply resets BackupAcknowledged on completion ────────

    [Fact]
    public async Task ApplyAsync_ResetsBackupAcknowledged_OnCompletion()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OptimizeSuccess = true,
            SettingsToReturn = BuildSettings(timeout: 5),
        };
        var vm = new GrubManagerViewModel(engine);
        await vm.ScanAsync();
        vm.PendingTimeout = 10;
        vm.BackupAcknowledged = true;

        await vm.ApplyAsync();

        Assert.False(vm.BackupAcknowledged);
    }

    // ── 18. Reset reverts Pending* to Current* ───────────────────

    [Fact]
    public async Task Reset_RevertsPendingValues_ToCurrent()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            SettingsToReturn = BuildSettings(timeout: 5, grubDefault: "0", osProberDisabled: true),
        };
        var vm = new GrubManagerViewModel(engine);
        await vm.ScanAsync();
        vm.PendingTimeout = 25;
        vm.PendingGrubDefault = "saved";
        vm.PendingOsProberEnabled = true;
        Assert.True(vm.HasPendingChanges);

        vm.Reset();

        Assert.Equal(5, vm.PendingTimeout);
        Assert.Equal("0", vm.PendingGrubDefault);
        Assert.False(vm.PendingOsProberEnabled);
        Assert.False(vm.HasPendingChanges);
    }

    // ── 19. Reset clears BackupAcknowledged ─────────────────────

    [Fact]
    public async Task Reset_ClearsBackupAcknowledged()
    {
        var engine = new FakeEngine { ScanSuccess = true, SettingsToReturn = BuildSettings(timeout: 5) };
        var vm = new GrubManagerViewModel(engine);
        await vm.ScanAsync();
        vm.PendingTimeout = 10;
        vm.BackupAcknowledged = true;

        vm.Reset();

        Assert.False(vm.BackupAcknowledged);
    }

    // ── 20. Rollback calls engine.RollbackAsync and rescans ───────

    [Fact]
    public async Task RollbackAsync_CallsEngineRollback_AndRescans()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            CanRollbackResult = true,
            SettingsToReturn = BuildSettings(timeout: 5),
        };
        var vm = new GrubManagerViewModel(engine);
        await vm.ScanAsync();
        Assert.True(vm.HasBackup);
        int scanBefore = engine.ScanCallCount;

        await vm.RollbackAsync();

        Assert.True(engine.RollbackCallCount >= 1);
        Assert.True(engine.ScanCallCount > scanBefore, "Expected re-scan after rollback");
    }

    // ── 21. PendingChangeDescriptions ─────────────────────────────

    [Fact]
    public async Task PendingChangeDescriptions_ReturnsTimeoutChange_WhenTimeoutChanged()
    {
        AuraCore.UI.Avalonia.LocalizationService.SetLanguage("en");
        var engine = new FakeEngine { ScanSuccess = true, SettingsToReturn = BuildSettings(timeout: 5) };
        var vm = new GrubManagerViewModel(engine);
        await vm.ScanAsync();
        vm.PendingTimeout = 3;

        var descriptions = vm.PendingChangeDescriptions.ToList();

        Assert.Single(descriptions);
        Assert.Contains("GRUB_TIMEOUT", descriptions[0]);
        Assert.Contains("5", descriptions[0]);
        Assert.Contains("3", descriptions[0]);
    }

    [Fact]
    public async Task PendingChangeDescriptions_ReturnsDefaultChange_WhenDefaultChanged()
    {
        AuraCore.UI.Avalonia.LocalizationService.SetLanguage("en");
        var engine = new FakeEngine { ScanSuccess = true, SettingsToReturn = BuildSettings(grubDefault: "0") };
        var vm = new GrubManagerViewModel(engine);
        await vm.ScanAsync();
        vm.PendingGrubDefault = "saved";

        var descriptions = vm.PendingChangeDescriptions.ToList();

        Assert.Single(descriptions);
        Assert.Contains("GRUB_DEFAULT", descriptions[0]);
        Assert.Contains("saved", descriptions[0]);
    }

    [Fact]
    public async Task PendingChangeDescriptions_ReturnsOsProberChange_WhenToggleChanged()
    {
        AuraCore.UI.Avalonia.LocalizationService.SetLanguage("en");
        var engine = new FakeEngine { ScanSuccess = true, SettingsToReturn = BuildSettings(osProberDisabled: true) };
        var vm = new GrubManagerViewModel(engine);
        await vm.ScanAsync();
        vm.PendingOsProberEnabled = true;

        var descriptions = vm.PendingChangeDescriptions.ToList();

        Assert.Single(descriptions);
        Assert.Contains("GRUB_DISABLE_OS_PROBER", descriptions[0]);
    }

    // ── 22. Cancel during apply ────────────────────────────────────

    [Fact]
    public async Task Cancel_DoesNotThrow_WhenApplyInFlight()
    {
        var engine = new FakeEngine
        {
            ScanSuccess = true,
            OptimizeSuccess = true,
            SettingsToReturn = BuildSettings(timeout: 5),
            SimulateLongOptimize = true,
        };
        var vm = new GrubManagerViewModel(engine);
        await vm.ScanAsync();
        vm.PendingTimeout = 10;
        vm.BackupAcknowledged = true;

        var applyTask = vm.ApplyAsync();

        // Give the task a chance to start
        for (int i = 0; i < 10 && !vm.IsBusy; i++)
            await Task.Yield();
        vm.Cancel();

        // Await the task; it should complete (either cancelled or errored cleanly)
        try { await applyTask; } catch { }
        Assert.False(vm.IsBusy);
    }

    // ── Helpers ────────────────────────────────────────────────────

    private static GrubSettings BuildSettings(
        int timeout = 5,
        string grubDefault = "0",
        string cmdline = "quiet splash",
        bool osProberDisabled = false,
        List<string>? kernels = null) =>
        new(Timeout: timeout,
            GrubDefault: grubDefault,
            CmdlineLinuxDefault: cmdline,
            OsProberDisabled: osProberDisabled,
            InstalledKernels: kernels ?? new List<string>());

    private static async Task WaitUntilNotBusy(GrubManagerViewModel vm, int maxIter = 100)
    {
        for (int i = 0; i < maxIter && vm.IsBusy; i++)
            await Task.Yield();
    }

    private sealed class FakeEngine : IGrubManagerEngine
    {
        public int ScanCallCount;
        public int RollbackCallCount;
        public bool ScanSuccess = true;
        public string? BlockedReason = null;
        public GrubSettings? SettingsToReturn;
        public bool CanRollbackResult = false;
        public OptimizationPlan? LastOptimizePlan;
        public bool OptimizeSuccess = true;
        public bool SimulateLongOptimize;

        public string Id => "grub-manager";
        public GrubSettings? LastSettings { get; private set; }

        public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
        {
            ScanCallCount++;
            await Task.Yield();
            LastSettings = SettingsToReturn;
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

        public Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default) =>
            Task.FromResult(CanRollbackResult);

        public Task RollbackAsync(string operationId, CancellationToken ct = default)
        {
            RollbackCallCount++;
            return Task.CompletedTask;
        }
    }
}
