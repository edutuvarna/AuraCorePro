using Avalonia.Headless.XUnit;
using AuraCore.UI.Avalonia.Views.Controls;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Controls;

public class SidebarSectionDividerTests
{
    [AvaloniaFact]
    public void Divider_Defaults()
    {
        var divider = new SidebarSectionDivider();
        Assert.Equal(string.Empty, divider.Label);
    }

    [AvaloniaFact]
    public void Divider_AcceptsLabel()
    {
        var divider = new SidebarSectionDivider { Label = "ADVANCED" };
        Assert.Equal("ADVANCED", divider.Label);
    }

    [AvaloniaFact]
    public void Divider_RendersInWindow()
    {
        var divider = new SidebarSectionDivider { Label = "OVERVIEW" };
        using var window = AvaloniaTestBase.RenderInWindow(divider, 220, 20);
        Assert.True(divider.IsMeasureValid);
    }
}
