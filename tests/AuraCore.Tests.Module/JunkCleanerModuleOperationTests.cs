using System;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.Module.JunkCleaner;
using Xunit;

namespace AuraCore.Tests.Module;

public class JunkCleanerModuleOperationTests
{
    private sealed class StubGuard : IPrivilegedActionGuard
    {
        private readonly bool _grant;
        public StubGuard(bool grant) { _grant = grant; }
        public Task<bool> TryGuardAsync(string actionDescription, string? remediationCommandOverride = null, CancellationToken ct = default)
            => Task.FromResult(_grant);
    }

    [Fact]
    public async Task RunOperationAsync_HelperMissing_ReturnsSkipped_OnLinux()
    {
        IOperationModule module = new JunkCleanerModule();
        var result = await module.RunOperationAsync(
            new OptimizationPlan(module.Id, Array.Empty<string>()),
            new StubGuard(grant: false));

        if (OperatingSystem.IsLinux())
        {
            Assert.Equal(OperationStatus.Skipped, result.Status);
            Assert.Contains("helper", result.Reason!, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            // Windows / macOS bypass the guard branch — should not be Skipped from guard rejection.
            Assert.NotEqual(OperationStatus.Skipped, result.Status);
        }
    }

    [Fact]
    public async Task RunOperationAsync_GuardGranted_NotSkipped()
    {
        IOperationModule module = new JunkCleanerModule();
        var result = await module.RunOperationAsync(
            new OptimizationPlan(module.Id, Array.Empty<string>()),
            new StubGuard(grant: true));

        Assert.NotEqual(OperationStatus.Skipped, result.Status);
    }
}
