using Xunit;
using AuraCore.Application;
using AuraCore.Module.TimeMachineManager;
using AuraCore.Domain.Enums;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.Tests.Module;

public class TimeMachineManagerTests
{
    [Fact]
    public void TimeMachineManager_Metadata_IsValid()
    {
        var m = new TimeMachineManagerModule();
        Assert.Equal("time-machine-manager", m.Id);
        Assert.False(string.IsNullOrWhiteSpace(m.DisplayName));
        Assert.Equal(OptimizationCategory.SystemHealth, m.Category);
        Assert.Equal(RiskLevel.Low, m.Risk);
        Assert.Equal(SupportedPlatform.MacOS, m.Platform);
        IOptimizationModule iface = m;
        Assert.False(iface.IsAdvanced);
    }

    [Fact]
    public async Task TimeMachineManager_Scan_OnNonMacOS_ReturnsBlocked()
    {
        if (OperatingSystem.IsMacOS()) return;
        var m = new TimeMachineManagerModule();
        var r = await m.ScanAsync(new ScanOptions());
        Assert.False(r.Success);
        Assert.Equal("time-machine-manager", r.ModuleId);
    }

    [Fact]
    public async Task TimeMachineManager_Optimize_InjectionAttempt_Rejected()
    {
        var m = new TimeMachineManagerModule();
        var plan = new OptimizationPlan(m.Id, new List<string>
        {
            "delete-backup:/Volumes/Backup/foo;rm -rf /",
            "delete-old-backups:not-a-number",
            "delete-old-backups:-5",
            "unknown-item-id"
        });
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("time-machine-manager", result.ModuleId);
        Assert.Equal(0, result.ItemsProcessed);
    }

    [Fact]
    public async Task TimeMachineManager_Optimize_WithEmptyPlan_ReturnsSuccess()
    {
        var m = new TimeMachineManagerModule();
        var plan = new OptimizationPlan(m.Id, new List<string>());
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("time-machine-manager", result.ModuleId);
        Assert.NotEmpty(result.OperationId);
    }
}
