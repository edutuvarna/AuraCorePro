using Xunit;
using AuraCore.Application;
using AuraCore.Module.SymlinkManager;
using AuraCore.Domain.Enums;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.Tests.Module;

public class SymlinkManagerTests
{
    [Fact]
    public void SymlinkManager_Metadata_IsValid()
    {
        var m = new SymlinkManagerModule();
        Assert.Equal("symlink-manager", m.Id);
        Assert.False(string.IsNullOrWhiteSpace(m.DisplayName));
        Assert.Equal(OptimizationCategory.ShellCustomization, m.Category);
        Assert.Equal(RiskLevel.Medium, m.Risk);
        Assert.Equal(SupportedPlatform.Linux, m.Platform);
        IOptimizationModule iface = m;
        Assert.False(iface.IsAdvanced);
    }

    [Fact]
    public async Task SymlinkManager_Scan_OnNonLinux_ReturnsBlocked()
    {
        if (OperatingSystem.IsLinux()) return;
        var m = new SymlinkManagerModule();
        var r = await m.ScanAsync(new ScanOptions());
        Assert.False(r.Success);
        Assert.Equal("symlink-manager", r.ModuleId);
    }

    [Fact]
    public async Task SymlinkManager_Optimize_WithEmptyPlan_ReturnsSuccess()
    {
        var m = new SymlinkManagerModule();
        var plan = new OptimizationPlan(m.Id, new List<string>());
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("symlink-manager", result.ModuleId);
        Assert.NotEmpty(result.OperationId);
    }
}
