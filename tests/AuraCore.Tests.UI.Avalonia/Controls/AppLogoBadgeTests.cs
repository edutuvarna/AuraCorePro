using Avalonia.Headless.XUnit;
using AuraCore.UI.Avalonia.Views.Controls;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Controls;

public class AppLogoBadgeTests
{
    [AvaloniaFact]
    public void Logo_Defaults()
    {
        var l = new AppLogoBadge();
        Assert.Equal("AuraCore", l.ProductName);
        Assert.Equal("PRO • CORTEX", l.Tagline);
    }

    [AvaloniaFact]
    public void Logo_AcceptsValues()
    {
        var l = new AppLogoBadge { ProductName = "Other", Tagline = "V2" };
        Assert.Equal("Other", l.ProductName);
        Assert.Equal("V2", l.Tagline);
    }

    [AvaloniaFact]
    public void Logo_RendersInWindow()
    {
        var l = new AppLogoBadge();
        using var window = AvaloniaTestBase.RenderInWindow(l, 220, 50);
        Assert.True(l.IsMeasureValid);
    }
}
