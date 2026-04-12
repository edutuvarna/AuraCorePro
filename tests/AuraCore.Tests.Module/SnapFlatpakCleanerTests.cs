using Xunit;
using AuraCore.Application;
using AuraCore.Module.SnapFlatpakCleaner;
using AuraCore.Domain.Enums;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.Tests.Module;

public class SnapFlatpakCleanerTests
{
    [Fact]
    public void SnapFlatpakCleaner_Metadata_IsValid()
    {
        var m = new SnapFlatpakCleanerModule();
        Assert.Equal("snap-flatpak-cleaner", m.Id);
        Assert.Equal("Snap & Flatpak Cleaner", m.DisplayName);
        Assert.Equal(OptimizationCategory.SystemCleaning, m.Category);
        Assert.Equal(RiskLevel.Low, m.Risk);
        Assert.Equal(SupportedPlatform.Linux, m.Platform);
        IOptimizationModule iface = m;
        Assert.False(iface.IsAdvanced);
    }

    [Fact]
    public async Task SnapFlatpakCleaner_Scan_OnNonLinux_ReturnsBlockedOrFailure()
    {
        if (OperatingSystem.IsLinux()) return;
        var m = new SnapFlatpakCleanerModule();
        var r = await m.ScanAsync(new ScanOptions());
        Assert.False(r.Success);
        Assert.Equal("snap-flatpak-cleaner", r.ModuleId);
    }

    [Fact]
    public async Task SnapFlatpakCleaner_Optimize_WithEmptyPlan_ReturnsSuccess()
    {
        var m = new SnapFlatpakCleanerModule();
        var plan = new OptimizationPlan(m.Id, new List<string>());
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("snap-flatpak-cleaner", result.ModuleId);
        Assert.NotEmpty(result.OperationId);
    }

    [Fact]
    public async Task SnapFlatpakCleaner_Optimize_WithInvalidItems_DoesNotCrash()
    {
        var m = new SnapFlatpakCleanerModule();
        var plan = new OptimizationPlan(m.Id, new List<string>
        {
            "snap-remove:../../../../etc/passwd:1",
            "flatpak-remove:org.evil;rm -rf /",
            "unknown-action:foo",
            "snap-remove:missingrevision",
            "flatpak-remove:"
        });
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("snap-flatpak-cleaner", result.ModuleId);
        Assert.NotEmpty(result.OperationId);
        // None of the invalid items should be processed
        Assert.Equal(0, result.ItemsProcessed);
    }

    [Fact]
    public async Task SnapFlatpakCleaner_Rollback_IsNotSupported()
    {
        var m = new SnapFlatpakCleanerModule();
        var canRollback = await m.CanRollbackAsync("test-op-id");
        Assert.False(canRollback);
        // Should not throw
        await m.RollbackAsync("test-op-id");
    }
}
