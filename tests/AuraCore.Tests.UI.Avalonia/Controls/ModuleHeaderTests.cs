using AuraCore.UI.Avalonia.Views.Controls;
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Controls;

public class ModuleHeaderTests
{
    [AvaloniaFact]
    public void Ctor_DoesNotThrow()
    {
        var h = new ModuleHeader();
        Assert.NotNull(h);
        Assert.Equal(string.Empty, h.Title);
        Assert.Null(h.Subtitle);
        Assert.Null(h.Actions);
    }

    [AvaloniaFact]
    public void Title_Property_RoundTrips()
    {
        var h = new ModuleHeader { Title = "Firewall Rules" };
        Assert.Equal("Firewall Rules", h.Title);
    }

    [AvaloniaFact]
    public void Subtitle_Property_RoundTrips()
    {
        var h = new ModuleHeader { Subtitle = "Manage inbound/outbound" };
        Assert.Equal("Manage inbound/outbound", h.Subtitle);
    }

    [AvaloniaFact]
    public void Actions_AcceptsArbitraryContent()
    {
        var btn = new Button { Content = "Scan" };
        var h = new ModuleHeader { Actions = btn };
        Assert.Same(btn, h.Actions);
    }

    [AvaloniaFact]
    public void RendersInWindow_WithTitleOnly()
    {
        var h = new ModuleHeader { Title = "DNS Benchmark" };
        using var handle = AvaloniaTestBase.RenderInWindow(h, 800, 80);
        Assert.True(h.IsMeasureValid);
    }

    [AvaloniaFact]
    public void RendersInWindow_WithTitleSubtitleAndActions()
    {
        var h = new ModuleHeader
        {
            Title = "Firewall Rules",
            Subtitle = "Manage Windows Firewall inbound/outbound",
            Actions = new Button { Content = "Scan Rules" },
        };
        using var handle = AvaloniaTestBase.RenderInWindow(h, 800, 80);
        Assert.True(h.IsMeasureValid);
    }
}
