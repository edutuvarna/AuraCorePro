using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.Application;
using AuraCore.Module.RegistryOptimizer;
using Xunit;

namespace AuraCore.Tests.Module;

public class RegistryOptimizerModuleTests
{
    [Fact]
    public async Task ScanAsync_OnNonWindows_ReturnsEmptyResult_NoThrow()
    {
        if (OperatingSystem.IsWindows()) return;
        var module = new RegistryOptimizerModule();
        var result = await module.ScanAsync(new ScanOptions(), CancellationToken.None);
        Assert.True(result.Success);
        Assert.Equal(0, result.ItemsFound);
    }

    [Fact]
    public async Task OptimizeAsync_OnNonWindows_ReturnsNoopResult_NoThrow()
    {
        if (OperatingSystem.IsWindows()) return;
        var module = new RegistryOptimizerModule();
        var result = await module.OptimizeAsync(
            new OptimizationPlan(module.Id, new List<string> { "any-issue-id" }),
            null,
            CancellationToken.None);
        Assert.True(result.Success);
        Assert.Equal(0, result.ItemsProcessed);
    }
}
