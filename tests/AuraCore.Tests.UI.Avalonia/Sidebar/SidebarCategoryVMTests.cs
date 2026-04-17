using AuraCore.UI.Avalonia.ViewModels;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Sidebar;

public class SidebarCategoryVMTests
{
    [Fact]
    public void Record_no_longer_has_IsAccent_property()
    {
        var prop = typeof(SidebarCategoryVM).GetProperty("IsAccent");
        Assert.Null(prop);
    }

    [Fact]
    public void HasBadge_true_when_Badge_is_non_empty_string()
    {
        var vm = new SidebarCategoryVM(
            Id: "x",
            LocalizationKey: "k",
            Icon: "i",
            Modules: new SidebarModuleVM[] { },
            Badge: "NEW");
        Assert.True(vm.HasBadge);
    }

    [Fact]
    public void HasBadge_false_when_Badge_is_null()
    {
        var vm = new SidebarCategoryVM(
            Id: "x",
            LocalizationKey: "k",
            Icon: "i",
            Modules: new SidebarModuleVM[] { });
        Assert.False(vm.HasBadge);
    }

    [Fact]
    public void HasBadge_false_when_Badge_is_empty_string()
    {
        var vm = new SidebarCategoryVM(
            Id: "x",
            LocalizationKey: "k",
            Icon: "i",
            Modules: new SidebarModuleVM[] { },
            Badge: "");
        Assert.False(vm.HasBadge);
    }
}
