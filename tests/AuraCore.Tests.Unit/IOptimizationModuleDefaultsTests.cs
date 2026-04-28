using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using Xunit;

namespace AuraCore.Tests.Unit;

public class IOptimizationModuleDefaultsTests
{
    private sealed class StubAllModule : IOptimizationModule
    {
        public string Id => "test-all";
        public string DisplayName => "Test All";
        public OptimizationCategory Category => OptimizationCategory.SystemHealth;
        public RiskLevel Risk => RiskLevel.None;
        public SupportedPlatform Platform => SupportedPlatform.All;
        public Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
            => Task.FromResult(new ScanResult(Id, true, 0, 0));
        public Task<OptimizationResult> OptimizeAsync(OptimizationPlan plan, IProgress<TaskProgress>? progress = null, CancellationToken ct = default)
            => Task.FromResult(new OptimizationResult(Id, "op", true, 0, 0, TimeSpan.Zero));
        public Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default)
            => Task.FromResult(false);
        public Task RollbackAsync(string operationId, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class StubLinuxModule : IOptimizationModule
    {
        public string Id => "test-linux";
        public string DisplayName => "Test Linux";
        public OptimizationCategory Category => OptimizationCategory.SystemHealth;
        public RiskLevel Risk => RiskLevel.None;
        public SupportedPlatform Platform => SupportedPlatform.Linux;
        public Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
            => Task.FromResult(new ScanResult(Id, true, 0, 0));
        public Task<OptimizationResult> OptimizeAsync(OptimizationPlan plan, IProgress<TaskProgress>? progress = null, CancellationToken ct = default)
            => Task.FromResult(new OptimizationResult(Id, "op", true, 0, 0, TimeSpan.Zero));
        public Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default)
            => Task.FromResult(false);
        public Task RollbackAsync(string operationId, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    [Fact]
    public void IsPlatformSupported_AllPlatform_AlwaysTrue()
    {
        IOptimizationModule m = new StubAllModule();
        Assert.True(m.IsPlatformSupported);
    }

    [Fact]
    public void IsPlatformSupported_LinuxPlatform_MatchesOS()
    {
        IOptimizationModule m = new StubLinuxModule();
        Assert.Equal(OperatingSystem.IsLinux(), m.IsPlatformSupported);
    }

    [Fact]
    public async Task CheckRuntimeAvailabilityAsync_DefaultImpl_AllPlatform_ReturnsAvailable()
    {
        IOptimizationModule m = new StubAllModule();
        var r = await m.CheckRuntimeAvailabilityAsync();
        Assert.True(r.IsAvailable);
        Assert.Equal(AvailabilityCategory.Available, r.Category);
    }

    [Fact]
    public async Task CheckRuntimeAvailabilityAsync_DefaultImpl_WrongPlatform_ReturnsWrongPlatform()
    {
        IOptimizationModule m = new StubLinuxModule();
        if (OperatingSystem.IsLinux()) return; // skip when actually on Linux
        var r = await m.CheckRuntimeAvailabilityAsync();
        Assert.False(r.IsAvailable);
        Assert.Equal(AvailabilityCategory.WrongPlatform, r.Category);
    }
}
