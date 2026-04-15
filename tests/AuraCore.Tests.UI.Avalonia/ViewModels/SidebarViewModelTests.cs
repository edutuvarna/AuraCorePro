using System.Linq;
using AuraCore.UI.Avalonia.ViewModels;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.ViewModels;

public class SidebarViewModelTests
{
    [Fact]
    public void Defaults_NoCategoryExpanded_DashboardActive()
    {
        var vm = new SidebarViewModel();
        Assert.Null(vm.ExpandedCategoryId);
        Assert.Equal("dashboard", vm.ActiveModuleId);
    }

    [Fact]
    public void ToggleCategory_ExpandsOnFirstCall()
    {
        var vm = new SidebarViewModel();
        vm.ToggleCategory("optimize");
        Assert.Equal("optimize", vm.ExpandedCategoryId);
    }

    [Fact]
    public void ToggleCategory_CollapsesOnSecondCall()
    {
        var vm = new SidebarViewModel();
        vm.ToggleCategory("optimize");
        vm.ToggleCategory("optimize");
        Assert.Null(vm.ExpandedCategoryId);
    }

    [Fact]
    public void ToggleCategory_CollapsesPreviousOnOtherCategory()
    {
        var vm = new SidebarViewModel();
        vm.ToggleCategory("optimize");
        vm.ToggleCategory("gaming");
        Assert.Equal("gaming", vm.ExpandedCategoryId);
    }

    [Fact]
    public void NavigateTo_UpdatesActiveModule()
    {
        var vm = new SidebarViewModel();
        vm.NavigateTo("ram-optimizer");
        Assert.Equal("ram-optimizer", vm.ActiveModuleId);
    }

    [Fact]
    public void NavigateTo_AutoExpandsOwnerCategory()
    {
        var vm = new SidebarViewModel();
        vm.NavigateTo("ram-optimizer"); // ram-optimizer is in the Optimize category
        Assert.Equal("optimize", vm.ExpandedCategoryId);
    }

    [Fact]
    public void Categories_ContainsSixMainCategories()
    {
        var vm = new SidebarViewModel();
        var expected = new[] { "optimize", "clean-debloat", "gaming", "security", "apps-tools", "ai-features" };
        foreach (var id in expected)
            Assert.Contains(vm.Categories, c => c.Id == id);
    }

    [Fact]
    public void Categories_OptimizeContains5Modules()
    {
        var vm = new SidebarViewModel();
        var optimize = vm.Categories.First(c => c.Id == "optimize");
        Assert.Equal(5, optimize.Modules.Count);
    }
}
