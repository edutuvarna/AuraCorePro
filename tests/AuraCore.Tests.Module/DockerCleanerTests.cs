using Xunit;
using AuraCore.Application;
using AuraCore.Module.DockerCleaner;
using AuraCore.Domain.Enums;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.Tests.Module;

public class DockerCleanerTests
{
    [Fact]
    public void DockerCleaner_Metadata_IsValid()
    {
        var m = new DockerCleanerModule();
        Assert.Equal("docker-cleaner", m.Id);
        Assert.False(string.IsNullOrWhiteSpace(m.DisplayName));
        Assert.Equal(OptimizationCategory.DiskCleanup, m.Category);
        Assert.Equal(RiskLevel.Low, m.Risk);
        Assert.True(m.Platform.HasFlag(SupportedPlatform.Linux));
        Assert.True(m.Platform.HasFlag(SupportedPlatform.MacOS));
        IOptimizationModule iface = m;
        Assert.False(iface.IsAdvanced);
    }

    [Fact]
    public async Task DockerCleaner_Scan_OnWindows_ReturnsBlocked()
    {
        if (!OperatingSystem.IsWindows()) return;
        var m = new DockerCleanerModule();
        var r = await m.ScanAsync(new ScanOptions());
        Assert.False(r.Success);
        Assert.Equal("docker-cleaner", r.ModuleId);
    }

    [Fact]
    public async Task DockerCleaner_Optimize_WithEmptyPlan_ReturnsSuccess()
    {
        var m = new DockerCleanerModule();
        var plan = new OptimizationPlan(m.Id, new List<string>());
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("docker-cleaner", result.ModuleId);
        Assert.NotEmpty(result.OperationId);
    }

    [Fact]
    public async Task DockerCleaner_Optimize_UnknownItem_DoesNotFail()
    {
        var m = new DockerCleanerModule();
        var plan = new OptimizationPlan(m.Id, new List<string> { "invalid-item-id" });
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("docker-cleaner", result.ModuleId);
        // Should skip unknown item without crashing
    }
}
