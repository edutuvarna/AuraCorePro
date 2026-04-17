using Avalonia.Headless.XUnit;
using AuraCore.UI.Avalonia.Views.Controls;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Controls;

public class AuraToggleTests
{
    [AvaloniaFact]
    public void Toggle_Defaults()
    {
        var t = new AuraToggle();
        Assert.False(t.IsOn);
    }

    [AvaloniaFact]
    public void Toggle_IsOnToggles()
    {
        var t = new AuraToggle();
        t.IsOn = true;
        Assert.True(t.IsOn);
    }

    [AvaloniaFact]
    public void Toggle_RendersInWindow()
    {
        var t = new AuraToggle();
        using var window = AvaloniaTestBase.RenderInWindow(t, 60, 30);
        Assert.True(t.IsMeasureValid);
    }
}
