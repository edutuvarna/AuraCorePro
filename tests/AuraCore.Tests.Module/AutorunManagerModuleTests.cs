using System;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.Application;
using AuraCore.Module.AutorunManager;
using Xunit;

namespace AuraCore.Tests.Module;

public class AutorunManagerModuleTests
{
    [Fact]
    public async Task ScanAsync_OnNonWindows_ReturnsEmptyResult_NoThrow()
    {
        if (OperatingSystem.IsWindows()) return; // skip on Windows — guarded path is non-Windows-specific
        var module = new AutorunManagerModule();
        var result = await module.ScanAsync(new ScanOptions(), CancellationToken.None);
        Assert.True(result.Success);
        Assert.Equal(0, result.ItemsFound);
    }

    [Fact]
    public async Task OptimizeAsync_OnNonWindows_ReturnsNoopResult_NoThrow()
    {
        if (OperatingSystem.IsWindows()) return;
        var module = new AutorunManagerModule();
        var result = await module.OptimizeAsync(
            new OptimizationPlan(module.Id, new System.Collections.Generic.List<string> { "disable:foo" }),
            null,
            CancellationToken.None);
        Assert.True(result.Success);
        Assert.Equal(0, result.ItemsProcessed);
    }
}
