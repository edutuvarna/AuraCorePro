using Xunit;
using AuraCore.Application;
using AuraCore.Module.XcodeCleaner;
using AuraCore.Domain.Enums;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.Tests.Module;

public class XcodeCleanerTests
{
    [Fact]
    public void XcodeCleaner_Metadata_IsValid()
    {
        var m = new XcodeCleanerModule();
        Assert.Equal("xcode-cleaner", m.Id);
        Assert.False(string.IsNullOrWhiteSpace(m.DisplayName));
        Assert.Equal(OptimizationCategory.DiskCleanup, m.Category);
        Assert.Equal(RiskLevel.Low, m.Risk);
        Assert.Equal(SupportedPlatform.MacOS, m.Platform);
        IOptimizationModule iface = m;
        Assert.False(iface.IsAdvanced);
    }

    [Fact]
    public async Task XcodeCleaner_Scan_OnNonMacOS_ReturnsBlocked()
    {
        if (OperatingSystem.IsMacOS()) return;
        var m = new XcodeCleanerModule();
        var r = await m.ScanAsync(new ScanOptions());
        Assert.False(r.Success);
        Assert.Equal("xcode-cleaner", r.ModuleId);
    }

    [Fact]
    public async Task XcodeCleaner_Optimize_WithEmptyPlan_ReturnsSuccess()
    {
        var m = new XcodeCleanerModule();
        var plan = new OptimizationPlan(m.Id, new List<string>());
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("xcode-cleaner", result.ModuleId);
        Assert.NotEmpty(result.OperationId);
    }
}
