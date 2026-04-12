using Xunit;
using AuraCore.Application;
using AuraCore.Module.CronManager;
using AuraCore.Domain.Enums;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.Tests.Module;

public class CronManagerTests
{
    [Fact]
    public void CronManager_Metadata_IsValid()
    {
        var m = new CronManagerModule();
        Assert.Equal("cron-manager", m.Id);
        Assert.False(string.IsNullOrWhiteSpace(m.DisplayName));
        Assert.Equal(OptimizationCategory.ShellCustomization, m.Category);
        Assert.Equal(RiskLevel.Low, m.Risk);
        Assert.Equal(SupportedPlatform.Linux, m.Platform);
        IOptimizationModule iface = m;
        Assert.False(iface.IsAdvanced);
    }

    [Fact]
    public async Task CronManager_Scan_OnNonLinux_ReturnsBlocked()
    {
        if (OperatingSystem.IsLinux()) return;
        var m = new CronManagerModule();
        var r = await m.ScanAsync(new ScanOptions());
        Assert.False(r.Success);
        Assert.Equal("cron-manager", r.ModuleId);
    }

    [Fact]
    public async Task CronManager_Optimize_WithEmptyPlan_ReturnsSuccess()
    {
        var m = new CronManagerModule();
        var plan = new OptimizationPlan(m.Id, new List<string>());
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("cron-manager", result.ModuleId);
        Assert.NotEmpty(result.OperationId);
    }
}
