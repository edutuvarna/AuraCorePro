using Avalonia.Headless.XUnit;
using AuraCore.UI.Avalonia.Views.Controls;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Controls;

public class SidebarNavItemTests
{
    [AvaloniaFact]
    public void SidebarNavItem_Defaults()
    {
        var nav = new SidebarNavItem();
        Assert.Equal(string.Empty, nav.Label);
        Assert.False(nav.IsActive);
        Assert.Equal(string.Empty, nav.TrailingChipText);
    }

    [AvaloniaFact]
    public void SidebarNavItem_IsActiveToggles()
    {
        var nav = new SidebarNavItem { Label = "Dashboard" };
        nav.IsActive = true;
        Assert.True(nav.IsActive);
    }

    [AvaloniaFact]
    public void SidebarNavItem_RendersInWindow()
    {
        var nav = new SidebarNavItem { Label = "AI Features", TrailingChipText = "CORTEX" };
        using var window = AvaloniaTestBase.RenderInWindow(nav, 220, 32);
        Assert.True(nav.IsMeasureValid);
    }
}
