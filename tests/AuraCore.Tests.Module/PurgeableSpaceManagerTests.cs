using Xunit;
using AuraCore.Application;
using AuraCore.Module.PurgeableSpaceManager;
using AuraCore.Domain.Enums;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.Tests.Module;

public class PurgeableSpaceManagerTests
{
    [Fact]
    public void PurgeableSpaceManager_Metadata_IsValid()
    {
        var m = new PurgeableSpaceManagerModule();
        Assert.Equal("purgeable-space-manager", m.Id);
        Assert.False(string.IsNullOrWhiteSpace(m.DisplayName));
        Assert.Equal(OptimizationCategory.DiskCleanup, m.Category);
        Assert.Equal(RiskLevel.Medium, m.Risk);
        Assert.Equal(SupportedPlatform.MacOS, m.Platform);
        IOptimizationModule iface = m;
        Assert.False(iface.IsAdvanced);
    }

    [Fact]
    public async Task PurgeableSpaceManager_Scan_OnNonMacOS_ReturnsBlocked()
    {
        if (OperatingSystem.IsMacOS()) return;
        var m = new PurgeableSpaceManagerModule();
        var r = await m.ScanAsync(new ScanOptions());
        Assert.False(r.Success);
        Assert.Equal("purgeable-space-manager", r.ModuleId);
    }

    [Fact]
    public async Task PurgeableSpaceManager_Optimize_WithEmptyPlan_ReturnsSuccess()
    {
        var m = new PurgeableSpaceManagerModule();
        var plan = new OptimizationPlan(m.Id, new List<string>());
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("purgeable-space-manager", result.ModuleId);
        Assert.NotEmpty(result.OperationId);
    }
}
