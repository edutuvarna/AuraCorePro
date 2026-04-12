using Xunit;
using AuraCore.Application;
using AuraCore.Module.DnsFlusher;
using AuraCore.Domain.Enums;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.Tests.Module;

public class DnsFlusherTests
{
    [Fact]
    public void DnsFlusher_Metadata_IsValid()
    {
        var m = new DnsFlusherModule();
        Assert.Equal("dns-flusher", m.Id);
        Assert.False(string.IsNullOrWhiteSpace(m.DisplayName));
        Assert.Equal(OptimizationCategory.NetworkOptimization, m.Category);
        Assert.Equal(RiskLevel.Low, m.Risk);
        Assert.Equal(SupportedPlatform.MacOS, m.Platform);
        IOptimizationModule iface = m;
        Assert.False(iface.IsAdvanced);
    }

    [Fact]
    public async Task DnsFlusher_Scan_OnNonMacOS_ReturnsBlocked()
    {
        if (OperatingSystem.IsMacOS()) return;
        var m = new DnsFlusherModule();
        var r = await m.ScanAsync(new ScanOptions());
        Assert.False(r.Success);
        Assert.Equal("dns-flusher", r.ModuleId);
    }

    [Fact]
    public async Task DnsFlusher_Optimize_WithEmptyPlan_ReturnsSuccess()
    {
        var m = new DnsFlusherModule();
        var plan = new OptimizationPlan(m.Id, new List<string>());
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("dns-flusher", result.ModuleId);
        Assert.NotEmpty(result.OperationId);
    }
}
