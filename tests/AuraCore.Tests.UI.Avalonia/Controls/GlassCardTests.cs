using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using AuraCore.UI.Avalonia.Views.Controls;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Controls;

public class GlassCardTests
{
    [AvaloniaFact]
    public void GlassCard_InstantiatesWithDefaults()
    {
        var card = new GlassCard();
        Assert.Null(card.CardContent);
        Assert.Equal(12.0, card.CardCornerRadius.TopLeft);
    }

    [AvaloniaFact]
    public void GlassCard_AcceptsContent()
    {
        var tb = new TextBlock { Text = "hello" };
        var card = new GlassCard { CardContent = tb };
        Assert.Same(tb, card.CardContent);
    }

    [AvaloniaFact]
    public void GlassCard_RendersInWindow()
    {
        var card = new GlassCard { CardContent = new TextBlock { Text = "hello" } };
        using var window = AvaloniaTestBase.RenderInWindow(card, 300, 200);
        Assert.True(card.IsMeasureValid);
    }
}
