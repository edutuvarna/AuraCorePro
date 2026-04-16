using System.Linq;
using AuraCore.UI.Avalonia.Views.Controls;
using global::Avalonia.Headless.XUnit;
using global::Avalonia.Media;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Controls;

public class StatRowTests
{
    [AvaloniaFact]
    public void Ctor_SetsRowsToOne()
    {
        var row = new StatRow();
        Assert.Equal(1, row.Rows);
    }

    [AvaloniaFact]
    public void Empty_RendersInWindow_WithoutCrash()
    {
        var row = new StatRow();
        using var handle = AvaloniaTestBase.RenderInWindow(row, 600, 60);
        Assert.True(row.IsMeasureValid);
    }

    [AvaloniaFact]
    public void ThreeCards_AddedToChildren_AndRender()
    {
        var row = new StatRow();
        row.Children.Add(new StatCard { Label = "TOTAL", Value = "247" });
        row.Children.Add(new StatCard { Label = "ENABLED", Value = "198" });
        row.Children.Add(new StatCard { Label = "BLOCKED", Value = "49" });

        using var handle = AvaloniaTestBase.RenderInWindow(row, 600, 80);
        Assert.Equal(3, row.Children.Count);
        Assert.True(row.IsMeasureValid);
    }

    [AvaloniaFact]
    public void TwoCards_EquallyDivideWidth()
    {
        var row = new StatRow();
        var a = new StatCard { Label = "A", Value = "1" };
        var b = new StatCard { Label = "B", Value = "2" };
        row.Children.Add(a);
        row.Children.Add(b);

        using var handle = AvaloniaTestBase.RenderInWindow(row, 600, 80);
        var aWidth = a.Bounds.Width;
        var bWidth = b.Bounds.Width;
        Assert.True(aWidth > 0 && bWidth > 0, "Both cards should have non-zero width after layout");
        Assert.InRange(aWidth, bWidth - 1, bWidth + 1);
    }

    [AvaloniaFact]
    public void FourCards_EquallyDivideWidth()
    {
        var row = new StatRow();
        for (int i = 0; i < 4; i++)
            row.Children.Add(new StatCard { Label = $"S{i}", Value = $"{i}" });

        using var handle = AvaloniaTestBase.RenderInWindow(row, 800, 80);
        var widths = row.Children.Select(c => c.Bounds.Width).ToList();
        Assert.All(widths, w => Assert.True(w > 0));
        Assert.InRange(widths.Max() - widths.Min(), 0, 2);
    }
}
