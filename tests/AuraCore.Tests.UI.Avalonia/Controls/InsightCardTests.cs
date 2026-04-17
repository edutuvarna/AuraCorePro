using System.Collections.ObjectModel;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using AuraCore.UI.Avalonia.Views.Controls;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Controls;

public class InsightCardTests
{
    [AvaloniaFact]
    public void InsightCard_Defaults()
    {
        var card = new InsightCard();
        Assert.Equal("Insights", card.Title);
        Assert.NotNull(card.Rows);
        Assert.Empty(card.Rows);
    }

    [AvaloniaFact]
    public void InsightCard_AcceptsRows()
    {
        var card = new InsightCard
        {
            Rows = new ObservableCollection<InsightRow>
            {
                new() { Title = "Spike", Description = "Brave 42% idle", IconBrush = Brushes.Orange },
                new() { Title = "Pattern", Description = "Gaming at 21:00", IconBrush = Brushes.Teal }
            }
        };
        Assert.Equal(2, card.Rows.Count);
    }

    [AvaloniaFact]
    public void InsightCard_RendersInWindow()
    {
        var card = new InsightCard { Title = "Cortex Insights" };
        using var window = AvaloniaTestBase.RenderInWindow(card, 300, 200);
        Assert.True(card.IsMeasureValid);
    }
}
