using Xunit;
using AuraCore.Application;
using AuraCore.Module.SpotlightManager;
using AuraCore.Domain.Enums;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.Tests.Module;

public class SpotlightManagerTests
{
    [Fact]
    public void SpotlightManager_Metadata_IsValid()
    {
        var m = new SpotlightManagerModule();
        Assert.Equal("spotlight-manager", m.Id);
        Assert.False(string.IsNullOrWhiteSpace(m.DisplayName));
        Assert.Equal(OptimizationCategory.SystemHealth, m.Category);
        Assert.Equal(RiskLevel.Low, m.Risk);
        Assert.Equal(SupportedPlatform.MacOS, m.Platform);
        IOptimizationModule iface = m;
        Assert.False(iface.IsAdvanced);
    }

    [Fact]
    public async Task SpotlightManager_Scan_OnNonMacOS_ReturnsBlocked()
    {
        if (OperatingSystem.IsMacOS()) return;
        var m = new SpotlightManagerModule();
        var r = await m.ScanAsync(new ScanOptions());
        Assert.False(r.Success);
        Assert.Equal("spotlight-manager", r.ModuleId);
    }

    [Fact]
    public async Task SpotlightManager_Optimize_InjectionAttempt_Rejected()
    {
        var m = new SpotlightManagerModule();
        var plan = new OptimizationPlan(m.Id, new List<string>
        {
            "disable:/Volumes/foo;rm -rf /",
            "unknown-action:/Volumes/X",
            "disable:",
            "no-colon"
        });
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("spotlight-manager", result.ModuleId);
        Assert.Equal(0, result.ItemsProcessed);
    }

    [Fact]
    public async Task SpotlightManager_Optimize_WithEmptyPlan_ReturnsSuccess()
    {
        var m = new SpotlightManagerModule();
        var plan = new OptimizationPlan(m.Id, new List<string>());
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("spotlight-manager", result.ModuleId);
        Assert.NotEmpty(result.OperationId);
    }
}
