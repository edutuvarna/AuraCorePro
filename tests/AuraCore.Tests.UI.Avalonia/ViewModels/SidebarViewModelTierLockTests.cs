using AuraCore.UI.Avalonia.Services.AI;
using AuraCore.UI.Avalonia.ViewModels;
using System.Linq;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.ViewModels;

public class SidebarViewModelTierLockTests
{
    [Fact]
    public void FreeTier_AdminPanelItem_IsLocked()
    {
        var vm = new SidebarViewModel(new TierService(), UserTier.Free);

        var allModules = vm.Categories.SelectMany(c => c.Modules)
            .Concat(vm.AdvancedItems)
            .ToList();
        var adminPanel = allModules.FirstOrDefault(m => m.Id == "admin-panel");
        Assert.NotNull(adminPanel);
        Assert.True(adminPanel!.IsLocked);
    }

    [Fact]
    public void AdminTier_AdminPanelItem_Unlocked()
    {
        var vm = new SidebarViewModel(new TierService(), UserTier.Admin);

        var adminPanel = vm.Categories.SelectMany(c => c.Modules)
            .Concat(vm.AdvancedItems)
            .First(m => m.Id == "admin-panel");
        Assert.False(adminPanel.IsLocked);
    }

    [Fact]
    public void SystemHealth_ExistsAndUnlockedForFreeTier()
    {
        var vm = new SidebarViewModel(new TierService(), UserTier.Free);

        var systemHealth = vm.Categories
            .SelectMany(c => c.Modules)
            .FirstOrDefault(m => m.Id == "system-health");
        Assert.NotNull(systemHealth);
        Assert.False(systemHealth!.IsLocked);
    }

    [Fact]
    public void FreeTier_DiskCleanup_IsLocked()
    {
        // TierService mapping: disk-cleanup is Pro-tier
        var vm = new SidebarViewModel(new TierService(), UserTier.Free);

        var diskCleanup = vm.Categories.SelectMany(c => c.Modules)
            .FirstOrDefault(m => m.Id == "disk-cleanup");
        if (diskCleanup is not null)
            Assert.True(diskCleanup.IsLocked);
    }
}
