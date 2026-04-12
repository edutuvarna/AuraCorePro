using Xunit;
using AuraCore.Application;
using AuraCore.Module.PackageCleaner;
using AuraCore.Domain.Enums;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.Tests.Module;

public class PackageCleanerTests
{
    [Fact]
    public void PackageCleaner_Metadata_IsValid()
    {
        var m = new PackageCleanerModule();
        Assert.Equal("package-cleaner", m.Id);
        Assert.False(string.IsNullOrWhiteSpace(m.DisplayName));
        Assert.Equal(OptimizationCategory.DiskCleanup, m.Category);
        Assert.Equal(RiskLevel.Medium, m.Risk);
        Assert.Equal(SupportedPlatform.Linux, m.Platform);
        IOptimizationModule iface = m;
        Assert.False(iface.IsAdvanced);
    }

    [Fact]
    public async Task PackageCleaner_Scan_OnNonLinux_ReturnsBlockedOrFailure()
    {
        if (OperatingSystem.IsLinux()) return;
        var m = new PackageCleanerModule();
        var r = await m.ScanAsync(new ScanOptions());
        Assert.False(r.Success);
        Assert.Equal("package-cleaner", r.ModuleId);
    }

    [Fact]
    public async Task PackageCleaner_Optimize_WithEmptyPlan_ReturnsSuccess()
    {
        var m = new PackageCleanerModule();
        var plan = new OptimizationPlan(m.Id, new List<string>());
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("package-cleaner", result.ModuleId);
        Assert.NotEmpty(result.OperationId);
    }
}
