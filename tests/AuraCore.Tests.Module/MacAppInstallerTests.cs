using Xunit;
using AuraCore.Application;
using AuraCore.Module.MacAppInstaller;
using AuraCore.Domain.Enums;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.Tests.Module;

public class MacAppInstallerTests
{
    [Fact]
    public void MacAppInstaller_Metadata_IsValid()
    {
        var m = new MacAppInstallerModule();
        Assert.Equal("mac-app-installer", m.Id);
        Assert.False(string.IsNullOrWhiteSpace(m.DisplayName));
        Assert.Equal(OptimizationCategory.ApplicationManagement, m.Category);
        Assert.Equal(RiskLevel.Low, m.Risk);
        Assert.Equal(SupportedPlatform.MacOS, m.Platform);
        IOptimizationModule iface = m;
        Assert.False(iface.IsAdvanced);
    }

    [Fact]
    public void MacAppInstaller_Bundles_Has10Categories()
    {
        Assert.Equal(10, MacAppBundles.AllBundles.Count);
    }

    [Fact]
    public void MacAppInstaller_Bundles_HasAtLeast130Apps()
    {
        var total = MacAppBundles.AllBundles.Sum(b => b.Apps.Count);
        Assert.True(total >= 130, $"Expected >= 130 apps, got {total}");
    }

    [Fact]
    public void MacAppInstaller_AllAppIds_AreUnique()
    {
        var ids = MacAppBundles.AllBundles.SelectMany(b => b.Apps).Select(a => a.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public async Task MacAppInstaller_Scan_OnNonMac_ReturnsBlocked()
    {
        if (OperatingSystem.IsMacOS()) return;
        var m = new MacAppInstallerModule();
        var r = await m.ScanAsync(new ScanOptions());
        Assert.False(r.Success);
        Assert.Equal("mac-app-installer", r.ModuleId);
    }

    [Fact]
    public async Task MacAppInstaller_Optimize_InvalidItemId_NoCrash()
    {
        var m = new MacAppInstallerModule();
        var plan = new OptimizationPlan(m.Id, new List<string>
        {
            "install:nonexistent-app",
            "unknown-action:firefox",
            "install:;rm -rf /",
            "no-colon"
        });
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("mac-app-installer", result.ModuleId);
        Assert.Equal(0, result.ItemsProcessed);
    }
}
