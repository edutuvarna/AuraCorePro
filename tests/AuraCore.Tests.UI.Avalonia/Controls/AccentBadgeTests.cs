using Avalonia.Headless.XUnit;
using AuraCore.UI.Avalonia.Views.Controls;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Controls;

public class AccentBadgeTests
{
    [AvaloniaFact]
    public void Badge_Defaults()
    {
        var b = new AccentBadge();
        Assert.Equal(string.Empty, b.Label);
    }

    [AvaloniaFact]
    public void Badge_AcceptsLabel()
    {
        var b = new AccentBadge { Label = "CORTEX" };
        Assert.Equal("CORTEX", b.Label);
    }

    [AvaloniaFact]
    public void Badge_RendersInWindow()
    {
        var b = new AccentBadge { Label = "ADMIN" };
        using var window = AvaloniaTestBase.RenderInWindow(b, 60, 20);
        Assert.True(b.IsMeasureValid);
    }
}
