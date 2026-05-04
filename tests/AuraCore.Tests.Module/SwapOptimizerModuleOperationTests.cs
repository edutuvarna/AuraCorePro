using System;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.Module.SwapOptimizer;
using Xunit;

namespace AuraCore.Tests.Module;

public class SwapOptimizerModuleOperationTests
{
    private sealed class StubGuard : IPrivilegedActionGuard
    {
        private readonly bool _grant;
        public StubGuard(bool grant) { _grant = grant; }
        public Task<bool> TryGuardAsync(string actionDescription, string? remediationCommandOverride = null, CancellationToken ct = default)
            => Task.FromResult(_grant);
    }

    [Fact]
    public async Task RunOperationAsync_OnNonLinux_ReturnsFailed()
    {
        if (OperatingSystem.IsLinux()) return;
#pragma warning disable CA1416
        IOperationModule module = new SwapOptimizerModule();
        var result = await module.RunOperationAsync(
            new OptimizationPlan(module.Id, Array.Empty<string>()),
            new StubGuard(grant: true));
#pragma warning restore CA1416

        Assert.Equal(OperationStatus.Failed, result.Status);
        Assert.Contains("Linux", result.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunOperationAsync_HelperMissing_ReturnsSkipped_OnLinux()
    {
        if (!OperatingSystem.IsLinux()) return;
#pragma warning disable CA1416
        IOperationModule module = new SwapOptimizerModule();
        var result = await module.RunOperationAsync(
            new OptimizationPlan(module.Id, Array.Empty<string>()),
            new StubGuard(grant: false));
#pragma warning restore CA1416

        Assert.Equal(OperationStatus.Skipped, result.Status);
        Assert.Contains("helper", result.Reason!, StringComparison.OrdinalIgnoreCase);
    }
}
