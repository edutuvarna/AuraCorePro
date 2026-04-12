using Xunit;
using AuraCore.Application;
using AuraCore.Module.LaunchAgentManager;
using AuraCore.Domain.Enums;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.Tests.Module;

public class LaunchAgentManagerTests
{
    [Fact]
    public void LaunchAgentManager_Metadata_IsValid()
    {
        var m = new LaunchAgentManagerModule();
        Assert.Equal("launch-agent-manager", m.Id);
        Assert.False(string.IsNullOrWhiteSpace(m.DisplayName));
        Assert.Equal(OptimizationCategory.SystemHealth, m.Category);
        Assert.Equal(RiskLevel.Medium, m.Risk);
        Assert.Equal(SupportedPlatform.MacOS, m.Platform);
        IOptimizationModule iface = m;
        Assert.False(iface.IsAdvanced);
    }

    [Fact]
    public async Task LaunchAgentManager_Scan_OnNonMacOS_ReturnsBlocked()
    {
        if (OperatingSystem.IsMacOS()) return;
        var m = new LaunchAgentManagerModule();
        var r = await m.ScanAsync(new ScanOptions());
        Assert.False(r.Success);
        Assert.Equal("launch-agent-manager", r.ModuleId);
    }

    [Fact]
    public async Task LaunchAgentManager_Optimize_InjectionAttempt_DoesNotCrash()
    {
        var m = new LaunchAgentManagerModule();
        var plan = new OptimizationPlan(m.Id, new List<string>
        {
            "unload:foo;rm -rf /",
            "no-colon-item",
            "unknown-action:com.example.agent"
        });
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("launch-agent-manager", result.ModuleId);
        Assert.Equal(0, result.ItemsProcessed);
    }

    [Fact]
    public async Task LaunchAgentManager_Optimize_WithEmptyPlan_ReturnsSuccess()
    {
        var m = new LaunchAgentManagerModule();
        var plan = new OptimizationPlan(m.Id, new List<string>());
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("launch-agent-manager", result.ModuleId);
        Assert.NotEmpty(result.OperationId);
    }
}
