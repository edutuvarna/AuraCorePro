using System.Linq;
using AuraCore.UI.Avalonia.ViewModels;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Sidebar;

/// <summary>
/// Phase 5.1.10 FontManager soft-hide + 5.1.11 app-installer category fix.
/// Verifies FontManager is absent from the sidebar (view/module/tests/i18n
/// preserved — only exposure removed) and the Windows app-installer lives
/// under Apps &amp; Tools, not Clean &amp; Debloat (matches Linux App Installer
/// placement from Phase 4.3.5).
/// </summary>
public class SidebarCleanupTests
{
    [Fact]
    public void Sidebar_DoesNotInclude_FontManager()
    {
        var vm = new SidebarViewModel();

        var allItems = vm.Categories
            .SelectMany(c => c.Modules)
            .Concat(vm.AdvancedItems);

        Assert.DoesNotContain(allItems, item => item.Id == "font-manager");
    }

    [Fact]
    public void Sidebar_WindowsAppInstaller_IsUnder_AppsAndTools()
    {
        var vm = new SidebarViewModel();

        var appsTools = vm.Categories.FirstOrDefault(c => c.Id == "apps-tools");
        Assert.NotNull(appsTools);

        var cleanDebloat = vm.Categories.FirstOrDefault(c => c.Id == "clean-debloat");
        Assert.NotNull(cleanDebloat);

        Assert.Contains(appsTools!.Modules, m => m.Id == "app-installer");
        Assert.DoesNotContain(cleanDebloat!.Modules, m => m.Id == "app-installer");
    }
}
