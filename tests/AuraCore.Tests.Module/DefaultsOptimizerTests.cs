using Xunit;
using AuraCore.Application;
using AuraCore.Module.DefaultsOptimizer;
using AuraCore.Domain.Enums;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.Tests.Module;

public class DefaultsOptimizerTests
{
    [Fact]
    public void DefaultsOptimizer_Metadata_IsValid()
    {
        var m = new DefaultsOptimizerModule();
        Assert.Equal("defaults-optimizer", m.Id);
        Assert.False(string.IsNullOrWhiteSpace(m.DisplayName));
        Assert.Equal(OptimizationCategory.ShellCustomization, m.Category);
        Assert.Equal(RiskLevel.Medium, m.Risk);
        Assert.Equal(SupportedPlatform.MacOS, m.Platform);
        IOptimizationModule iface = m;
        Assert.False(iface.IsAdvanced);
    }

    [Fact]
    public void DefaultsOptimizer_Catalog_Has15Tweaks()
    {
        Assert.Equal(15, DefaultsTweaksCatalog.All.Count);
    }

    [Fact]
    public void DefaultsOptimizer_Catalog_AllIdsUnique()
    {
        var ids = DefaultsTweaksCatalog.All.Select(t => t.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public async Task DefaultsOptimizer_Scan_OnNonMacOS_ReturnsBlocked()
    {
        if (OperatingSystem.IsMacOS()) return;
        var m = new DefaultsOptimizerModule();
        var r = await m.ScanAsync(new ScanOptions());
        Assert.False(r.Success);
        Assert.Equal("defaults-optimizer", r.ModuleId);
    }

    [Fact]
    public async Task DefaultsOptimizer_Optimize_WithEmptyPlan_ReturnsSuccess()
    {
        var m = new DefaultsOptimizerModule();
        var plan = new OptimizationPlan(m.Id, new List<string>());
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("defaults-optimizer", result.ModuleId);
        Assert.NotEmpty(result.OperationId);
    }
}
