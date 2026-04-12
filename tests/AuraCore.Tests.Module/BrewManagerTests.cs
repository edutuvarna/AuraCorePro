using Xunit;
using AuraCore.Application;
using AuraCore.Module.BrewManager;
using AuraCore.Domain.Enums;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.Tests.Module;

public class BrewManagerTests
{
    [Fact]
    public void BrewManager_Metadata_IsValid()
    {
        var m = new BrewManagerModule();
        Assert.Equal("brew-manager", m.Id);
        Assert.False(string.IsNullOrWhiteSpace(m.DisplayName));
        Assert.Equal(OptimizationCategory.ApplicationManagement, m.Category);
        Assert.Equal(RiskLevel.Low, m.Risk);
        Assert.Equal(SupportedPlatform.MacOS, m.Platform);
        IOptimizationModule iface = m;
        Assert.False(iface.IsAdvanced);
    }

    [Fact]
    public async Task BrewManager_Scan_OnNonMacOS_ReturnsBlocked()
    {
        if (OperatingSystem.IsMacOS()) return;
        var m = new BrewManagerModule();
        var r = await m.ScanAsync(new ScanOptions());
        Assert.False(r.Success);
        Assert.Equal("brew-manager", r.ModuleId);
    }

    [Fact]
    public async Task BrewManager_Optimize_WithEmptyPlan_ReturnsSuccess()
    {
        var m = new BrewManagerModule();
        var plan = new OptimizationPlan(m.Id, new List<string>());
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("brew-manager", result.ModuleId);
        Assert.NotEmpty(result.OperationId);
    }
}
