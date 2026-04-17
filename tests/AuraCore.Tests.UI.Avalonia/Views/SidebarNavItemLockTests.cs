using AuraCore.UI.Avalonia.Views.Controls;
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

public class SidebarNavItemLockTests
{
    [AvaloniaFact]
    public void IsLocked_DefaultsToFalse()
    {
        var item = new SidebarNavItem();
        Assert.False(item.IsLocked);
    }

    [AvaloniaFact]
    public void IsLocked_Set_ReflectsInProperty()
    {
        var item = new SidebarNavItem { IsLocked = true };
        Assert.True(item.IsLocked);
    }

    [AvaloniaFact]
    public void IsLocked_SetTrue_AddsLockedPseudoClass()
    {
        var item = new SidebarNavItem();
        using var handle = AvaloniaTestBase.RenderInWindow(item, 200, 40);

        item.IsLocked = true;

        Assert.Contains(":locked", item.Classes);
    }

    [AvaloniaFact]
    public void IsLocked_SetFalse_NoLockedPseudoClass()
    {
        var item = new SidebarNavItem();
        using var handle = AvaloniaTestBase.RenderInWindow(item, 200, 40);

        Assert.DoesNotContain(":locked", item.Classes);
    }
}
