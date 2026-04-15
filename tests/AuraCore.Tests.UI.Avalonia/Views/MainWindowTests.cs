using global::Avalonia.Headless.XUnit;
using AuraCore.UI.Avalonia.ViewModels;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

public class MainWindowTests
{
    [AvaloniaFact]
    public void SidebarViewModel_BuildsCategoriesForCurrentPlatform()
    {
        var vm = new SidebarViewModel();
        var visible = vm.VisibleCategories();
        Assert.NotEmpty(visible);
    }

    [AvaloniaFact]
    public void NavigateTo_SetsActiveModule_AndExpandsCategory()
    {
        var vm = new SidebarViewModel();
        vm.NavigateTo("ai-insights");
        Assert.Equal("ai-insights", vm.ActiveModuleId);
        Assert.Equal("ai-features", vm.ExpandedCategoryId);
    }
}
