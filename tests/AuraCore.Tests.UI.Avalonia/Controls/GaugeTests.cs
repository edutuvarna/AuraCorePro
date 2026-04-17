using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using AuraCore.UI.Avalonia.Views.Controls;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Controls;

public class GaugeTests
{
    [AvaloniaFact]
    public void Gauge_InstantiatesWithDefaults()
    {
        var gauge = new Gauge();
        Assert.Equal(0.0, gauge.Value);
        Assert.Equal("CPU", gauge.Label);
        Assert.NotNull(gauge.RingBrush);
    }

    [AvaloniaFact]
    public void Gauge_ValueProperty_IsStyledAndBindable()
    {
        var gauge = new Gauge { Value = 42.5 };
        Assert.Equal(42.5, gauge.Value);
    }

    [AvaloniaFact]
    public void Gauge_LabelProperty_IsStyledAndBindable()
    {
        var gauge = new Gauge { Label = "RAM" };
        Assert.Equal("RAM", gauge.Label);
    }

    [AvaloniaFact]
    public void Gauge_RendersInWindow()
    {
        var gauge = new Gauge { Value = 50, Label = "GPU" };
        using var window = AvaloniaTestBase.RenderInWindow(gauge);
        Assert.True(gauge.IsMeasureValid);
    }
}
