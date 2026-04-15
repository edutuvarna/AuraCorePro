using AuraCore.UI.Avalonia.Services.AI;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Services.AI;

public class TierServiceTests
{
    [Fact]
    public void IsModuleLocked_AdminTier_AllUnlocked()
    {
        var svc = new TierService();
        Assert.False(svc.IsModuleLocked("admin-panel", UserTier.Admin));
        Assert.False(svc.IsModuleLocked("junk-cleaner", UserTier.Admin));
    }

    [Fact]
    public void IsModuleLocked_FreeTier_AdminPanelIsLocked()
    {
        var svc = new TierService();
        Assert.True(svc.IsModuleLocked("admin-panel", UserTier.Free));
    }

    [Fact]
    public void IsModuleLocked_ProTier_AdminPanelIsLocked()
    {
        var svc = new TierService();
        Assert.True(svc.IsModuleLocked("admin-panel", UserTier.Pro));
    }

    [Fact]
    public void IsModuleLocked_EnterpriseTier_AdminPanelIsLocked()
    {
        var svc = new TierService();
        Assert.True(svc.IsModuleLocked("admin-panel", UserTier.Enterprise));
    }

    [Fact]
    public void IsModuleLocked_FreeTier_BasicModulesUnlocked()
    {
        var svc = new TierService();
        Assert.False(svc.IsModuleLocked("junk-cleaner", UserTier.Free));
        Assert.False(svc.IsModuleLocked("ram-optimizer", UserTier.Free));
        Assert.False(svc.IsModuleLocked("dashboard", UserTier.Free));
    }

    [Fact]
    public void IsModuleLocked_UnknownModule_DefaultsToUnlocked()
    {
        var svc = new TierService();
        Assert.False(svc.IsModuleLocked("brand-new-module", UserTier.Free));
    }

    [Fact]
    public void GetRequiredTier_AdminPanel_ReturnsAdmin()
    {
        var svc = new TierService();
        Assert.Equal(UserTier.Admin, svc.GetRequiredTier("admin-panel"));
    }

    [Fact]
    public void GetRequiredTier_UnmappedModule_ReturnsFree()
    {
        var svc = new TierService();
        Assert.Equal(UserTier.Free, svc.GetRequiredTier("random-module"));
    }
}
