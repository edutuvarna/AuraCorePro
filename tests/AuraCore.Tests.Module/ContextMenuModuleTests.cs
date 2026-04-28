using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.Application;
using AuraCore.Module.ContextMenu;
using Xunit;

namespace AuraCore.Tests.Module;

public class ContextMenuModuleTests
{
    [Fact]
    public async Task ScanAsync_OnNonWindows_ReturnsEmptyResult_NoThrow()
    {
        if (OperatingSystem.IsWindows()) return;
        var module = new ContextMenuModule();
        var result = await module.ScanAsync(new ScanOptions(), CancellationToken.None);
        Assert.True(result.Success);
        Assert.Equal(0, result.ItemsFound);
    }

    [Fact]
    public async Task OptimizeAsync_OnNonWindows_ReturnsNoopResult_NoThrow()
    {
        if (OperatingSystem.IsWindows()) return;
        var module = new ContextMenuModule();
        var result = await module.OptimizeAsync(
            new OptimizationPlan(module.Id, new List<string> { "remove-share" }),
            null,
            CancellationToken.None);
        Assert.True(result.Success);
        Assert.Equal(0, result.ItemsProcessed);
    }
}
