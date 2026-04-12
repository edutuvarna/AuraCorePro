using Xunit;
using AuraCore.Application;
using AuraCore.Module.SwapOptimizer;
using AuraCore.Domain.Enums;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.Tests.Module;

public class SwapOptimizerTests
{
    [Fact]
    public void SwapOptimizer_Metadata_IsValid()
    {
        var m = new SwapOptimizerModule();
        Assert.Equal("swap-optimizer", m.Id);
        Assert.False(string.IsNullOrWhiteSpace(m.DisplayName));
        Assert.Equal(OptimizationCategory.MemoryOptimization, m.Category);
        Assert.Equal(RiskLevel.Medium, m.Risk);
        Assert.Equal(SupportedPlatform.Linux, m.Platform);
        IOptimizationModule iface = m;
        Assert.False(iface.IsAdvanced);
    }

    [Fact]
    public async Task SwapOptimizer_Scan_OnNonLinux_ReturnsBlocked()
    {
        if (OperatingSystem.IsLinux()) return;
        var m = new SwapOptimizerModule();
        var r = await m.ScanAsync(new ScanOptions());
        Assert.False(r.Success);
        Assert.Equal("swap-optimizer", r.ModuleId);
    }

    [Fact]
    public async Task SwapOptimizer_Optimize_InvalidSwappiness_Rejected()
    {
        var m = new SwapOptimizerModule();
        var plan = new OptimizationPlan(m.Id, new List<string>
        {
            "set-swappiness:-5",    // out of range
            "set-swappiness:200",   // out of range
            "set-swappiness:abc",   // not a number
            "set-swappiness:50;rm -rf /",  // injection attempt
        });
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("swap-optimizer", result.ModuleId);
        Assert.Equal(0, result.ItemsProcessed);
    }

    [Fact]
    public async Task SwapOptimizer_Optimize_WithEmptyPlan_ReturnsSuccess()
    {
        var m = new SwapOptimizerModule();
        var plan = new OptimizationPlan(m.Id, new List<string>());
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("swap-optimizer", result.ModuleId);
        Assert.NotEmpty(result.OperationId);
    }
}
