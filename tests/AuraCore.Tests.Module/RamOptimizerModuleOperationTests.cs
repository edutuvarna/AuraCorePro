using System;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.Module.RamOptimizer;
using Xunit;

namespace AuraCore.Tests.Module;

public class RamOptimizerModuleOperationTests
{
    private sealed class StubGuard : IPrivilegedActionGuard
    {
        private readonly bool _grant;
        public StubGuard(bool grant) { _grant = grant; }
        public Task<bool> TryGuardAsync(string actionDescription, string? remediationCommandOverride = null, CancellationToken ct = default)
            => Task.FromResult(_grant);
    }

    [Fact]
    public async Task RunOperationAsync_HelperMissing_ReturnsSkipped_OnLinuxOrMacOS()
    {
        IOperationModule module = new RamOptimizerModule();
        var result = await module.RunOperationAsync(
            new OptimizationPlan(module.Id, Array.Empty<string>()),
            new StubGuard(grant: false));

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            Assert.Equal(OperationStatus.Skipped, result.Status);
            Assert.Contains("helper", result.Reason!, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            // Windows path skips guard branch entirely — should not be Skipped due to guard rejection.
            Assert.NotEqual(OperationStatus.Skipped, result.Status);
        }
    }

    [Fact]
    public async Task RunOperationAsync_GuardGranted_NotSkipped()
    {
        IOperationModule module = new RamOptimizerModule();
        var result = await module.RunOperationAsync(
            new OptimizationPlan(module.Id, Array.Empty<string>()),
            new StubGuard(grant: true));

        Assert.NotEqual(OperationStatus.Skipped, result.Status);
    }
}
