using System.Linq;
using AuraCore.UI.Avalonia.ViewModels;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Sidebar;

/// <summary>
/// Phase 5.5.2.2: Disk Health moved to Dashboard widget — sidebar entry removed.
/// Verifies the module id "disk-health" is gone from Apps &amp; Tools and from
/// every other category (it should only be reachable via the Dashboard card).
/// </summary>
public class SidebarDiskHealthRemovalTests
{
    [Fact]
    public void DiskHealth_is_not_present_under_AppsAndTools_category()
    {
        var vm = new SidebarViewModel();
        var appsTools = vm.Categories.FirstOrDefault(c => c.Id == "apps-tools");

        Assert.NotNull(appsTools);
        Assert.DoesNotContain(appsTools!.Modules, m => m.Id == "disk-health");
    }

    [Fact]
    public void DiskHealth_is_not_present_in_any_sidebar_category()
    {
        var vm = new SidebarViewModel();

        var allCategoryModules = vm.Categories.SelectMany(c => c.Modules);
        Assert.DoesNotContain(allCategoryModules, m => m.Id == "disk-health");
    }

    [Fact]
    public void DiskHealth_is_not_present_in_advanced_items()
    {
        var vm = new SidebarViewModel();
        Assert.DoesNotContain(vm.AdvancedItems, m => m.Id == "disk-health");
    }
}
