using AuraCore.UI.Avalonia.Views.Controls;
using global::Avalonia.Media;
using global::Avalonia.Headless.XUnit;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Controls;

public class StatCardTests
{
    [AvaloniaFact]
    public void Ctor_DoesNotThrow_AndSetsDefaults()
    {
        var c = new StatCard();
        Assert.NotNull(c);
        Assert.Equal(string.Empty, c.Label);
        Assert.Equal("--", c.Value);
        Assert.Same(Brushes.White, c.ValueBrush);
    }

    [AvaloniaFact]
    public void Label_Property_RoundTrips()
    {
        var c = new StatCard { Label = "TOTAL" };
        Assert.Equal("TOTAL", c.Label);
    }

    [AvaloniaFact]
    public void Value_Property_RoundTrips()
    {
        var c = new StatCard { Value = "247" };
        Assert.Equal("247", c.Value);
    }

    [AvaloniaFact]
    public void Value_EmptyString_IsAllowed()
    {
        var c = new StatCard { Value = "" };
        Assert.Equal("", c.Value);
    }

    [AvaloniaFact]
    public void ValueBrush_AcceptsSuccessBrush()
    {
        var green = new SolidColorBrush(Colors.Green);
        var c = new StatCard { ValueBrush = green };
        Assert.Same(green, c.ValueBrush);
    }

    [AvaloniaFact]
    public void ValueBrush_AcceptsErrorBrush()
    {
        var red = new SolidColorBrush(Colors.Red);
        var c = new StatCard { ValueBrush = red };
        Assert.Same(red, c.ValueBrush);
    }

    [AvaloniaFact]
    public void RendersInWindow_WithDefaults()
    {
        var c = new StatCard();
        using var handle = AvaloniaTestBase.RenderInWindow(c, 160, 80);
        Assert.True(c.IsMeasureValid);
    }

    [AvaloniaFact]
    public void RendersInWindow_WithFullData()
    {
        var c = new StatCard
        {
            Label = "ENABLED",
            Value = "198",
            ValueBrush = new SolidColorBrush(Colors.LimeGreen),
        };
        using var handle = AvaloniaTestBase.RenderInWindow(c, 160, 80);
        Assert.True(c.IsMeasureValid);
    }
}
