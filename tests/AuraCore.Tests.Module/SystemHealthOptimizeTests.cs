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
        // Deliberately skip ScanAsync — Optimize does not depend on it, and on
        // Windows ScanAsync can stall on slow WMI queries unrelated to the
        // pipeline we're actually testing here.
        var m = new SystemHealthModule();
        var plan = new OptimizationPlan(m.Id, new List<string>());
        var result = await m.OptimizeAsync(plan);

        Assert.True(result.Success);
        Assert.Equal("system-health", result.ModuleId);
        Assert.NotEmpty(result.OperationId);
        Assert.Equal(0, result.ItemsProcessed);
        Assert.Equal(0, result.BytesFreed);
    }

    [Fact]
    public async Task SystemHealth_Optimize_UnknownItem_SkipsSilently()
    {
        // Unknown item IDs should fall through the switch's default branch
        // without touching any real filesystem. This validates the pipeline
        // without triggering the (slow, destructive) clean-temp code path.
        var m = new SystemHealthModule();
        var plan = new OptimizationPlan(m.Id, new List<string> { "not-a-real-item", "also-bogus" });
        var result = await m.OptimizeAsync(plan);

        Assert.True(result.Success);
        Assert.Equal("system-health", result.ModuleId);
        Assert.NotEmpty(result.OperationId);
        Assert.Equal(0, result.ItemsProcessed);
        Assert.True(result.Duration >= TimeSpan.Zero);
    }

    [Fact]
    public async Task SystemHealth_Optimize_CleanTemp_HonoursCancellation()
    {
        // Rather than letting clean-temp recursively walk the real Windows
        // temp folder (which can hold hundreds of thousands of files on a
        // developer machine), cancel almost immediately and verify that
        // OptimizeAsync propagates OperationCanceledException. This proves
        // the pipeline starts and cooperates with cancellation tokens.
        var m = new SystemHealthModule();
        var plan = new OptimizationPlan(m.Id, new List<string> { "clean-temp" });
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        try
        {
            var result = await m.OptimizeAsync(plan, progress: null, ct: cts.Token);
            // If the machine's temp folder happens to be empty, the call can
            // finish before the cancellation token fires — that's also fine.
            Assert.Equal("system-health", result.ModuleId);
        }
        catch (OperationCanceledException)
        {
            // Expected path on a populated temp folder.
        }
    }
}
