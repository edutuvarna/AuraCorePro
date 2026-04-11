using Xunit;
using AuraCore.Application;
using AuraCore.Module.SystemHealth;
using AuraCore.Domain.Enums;

namespace AuraCore.Tests.Module;

public class SystemHealthOptimizeTests
{
    [Fact]
    public void SystemHealth_Risk_IsLow()
    {
        var m = new SystemHealthModule();
        Assert.Equal(RiskLevel.Low, m.Risk);
    }

    [Fact]
    public async Task SystemHealth_Optimize_WithNoItems_ReturnsEmptySuccess()
    {
        var m = new SystemHealthModule();
        await m.ScanAsync(new ScanOptions());
        var plan = new OptimizationPlan(m.Id, new List<string>());
        var result = await m.OptimizeAsync(plan);

        Assert.True(result.Success);
        Assert.Equal("system-health", result.ModuleId);
        Assert.NotEmpty(result.OperationId);
        Assert.Equal(0, result.ItemsProcessed);
        Assert.Equal(0, result.BytesFreed);
    }

    [Fact]
    public async Task SystemHealth_Optimize_CleanTempItem_ProducesResult()
    {
        var m = new SystemHealthModule();
        await m.ScanAsync(new ScanOptions());
        var plan = new OptimizationPlan(m.Id, new List<string> { "clean-temp" });
        var result = await m.OptimizeAsync(plan);

        // Should succeed even if nothing to clean (no exception, valid result)
        Assert.True(result.Success);
        Assert.Equal("system-health", result.ModuleId);
        Assert.NotEmpty(result.OperationId);
        Assert.True(result.Duration >= TimeSpan.Zero);
    }
}
