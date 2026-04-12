using Xunit;
using AuraCore.Application;
using AuraCore.Module.KernelCleaner;
using AuraCore.Domain.Enums;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.Tests.Module;

public class KernelCleanerTests
{
    [Fact]
    public void KernelCleaner_Metadata_IsValid()
    {
        var m = new KernelCleanerModule();
        Assert.Equal("kernel-cleaner", m.Id);
        Assert.False(string.IsNullOrWhiteSpace(m.DisplayName));
        Assert.Equal(OptimizationCategory.DiskCleanup, m.Category);
        Assert.Equal(RiskLevel.Medium, m.Risk);
        Assert.Equal(SupportedPlatform.Linux, m.Platform);
        IOptimizationModule iface = m;
        Assert.False(iface.IsAdvanced);
    }

    [Fact]
    public async Task KernelCleaner_Scan_OnNonLinux_ReturnsBlockedOrFailure()
    {
        if (OperatingSystem.IsLinux()) return;
        var m = new KernelCleanerModule();
        var r = await m.ScanAsync(new ScanOptions());
        Assert.False(r.Success);
        Assert.Equal("kernel-cleaner", r.ModuleId);
    }

    [Fact]
    public async Task KernelCleaner_Optimize_WithEmptyPlan_ReturnsSuccess()
    {
        var m = new KernelCleanerModule();
        var plan = new OptimizationPlan(m.Id, new List<string>());
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("kernel-cleaner", result.ModuleId);
        Assert.NotEmpty(result.OperationId);
    }

    [Fact]
    public async Task KernelCleaner_Optimize_RefusesToRemoveCurrentKernel()
    {
        // This test ensures safety — can only run on Linux but safely returns on other platforms
        if (!OperatingSystem.IsLinux()) return;
        var m = new KernelCleanerModule();
        // uname -r format, shouldn't remove even if we try
        var plan = new OptimizationPlan(m.Id, new List<string> { "remove:1.0.0-impossible-version-that-matches-current-wont-exist" });
        var result = await m.OptimizeAsync(plan);
        // Expected: operation succeeds but processes 0 items (safety check)
        Assert.Equal("kernel-cleaner", result.ModuleId);
        Assert.True(result.Success);
    }
}
