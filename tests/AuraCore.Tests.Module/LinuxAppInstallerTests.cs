using Xunit;
using AuraCore.Application;
using AuraCore.Module.LinuxAppInstaller;
using AuraCore.Domain.Enums;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.Tests.Module;

public class LinuxAppInstallerTests
{
    [Fact]
    public void LinuxAppInstaller_Metadata_IsValid()
    {
        var m = new LinuxAppInstallerModule();
        Assert.Equal("linux-app-installer", m.Id);
        Assert.False(string.IsNullOrWhiteSpace(m.DisplayName));
        Assert.Equal(OptimizationCategory.ApplicationManagement, m.Category);
        Assert.Equal(RiskLevel.Low, m.Risk);
        Assert.Equal(SupportedPlatform.Linux, m.Platform);
        IOptimizationModule iface = m;
        Assert.False(iface.IsAdvanced);
    }

    [Fact]
    public void LinuxAppInstaller_Bundles_Has10Categories()
    {
        Assert.Equal(10, LinuxAppBundles.AllBundles.Count);
    }

    [Fact]
    public void LinuxAppInstaller_Bundles_HasAtLeast130Apps()
    {
        var total = LinuxAppBundles.AllBundles.Sum(b => b.Apps.Count);
        Assert.True(total >= 130, $"Expected >= 130 apps, got {total}");
    }

    [Fact]
    public void LinuxAppInstaller_AllAppIds_AreUnique()
    {
        var ids = LinuxAppBundles.AllBundles.SelectMany(b => b.Apps).Select(a => a.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public async Task LinuxAppInstaller_Scan_OnNonLinux_ReturnsBlocked()
    {
        if (OperatingSystem.IsLinux()) return;
        var m = new LinuxAppInstallerModule();
        var r = await m.ScanAsync(new ScanOptions());
        Assert.False(r.Success);
        Assert.Equal("linux-app-installer", r.ModuleId);
    }

    [Fact]
    public async Task LinuxAppInstaller_Optimize_InvalidItemId_NoCrash()
    {
        var m = new LinuxAppInstallerModule();
        var plan = new OptimizationPlan(m.Id, new List<string>
        {
            "install:nonexistent-app",
            "unknown-action:firefox",
            "install:;rm -rf /",
            "no-colon"
        });
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("linux-app-installer", result.ModuleId);
        Assert.Equal(0, result.ItemsProcessed);
    }
}
