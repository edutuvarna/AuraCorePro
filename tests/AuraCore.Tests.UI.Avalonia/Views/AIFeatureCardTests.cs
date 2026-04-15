using AuraCore.UI.Avalonia.ViewModels;
using AuraCore.UI.Avalonia.Views.Controls;
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

public class AIFeatureCardTests
{
    [AvaloniaFact]
    public void AIFeatureCard_BindsToViewModel_RendersTitle()
    {
        var vm = new AIFeatureCardVM("insights", "Cortex Insights", "AccentPurple", "IconSparklesFilled", false)
        {
            IsEnabled = true,
            PreviewSummary = "3 active · 1 warning",
        };

        var card = new AIFeatureCard { DataContext = vm };
        using var window = AvaloniaTestBase.RenderInWindow(card, 400, 300);

        card.Measure(new global::Avalonia.Size(400, 300));
        card.Arrange(new global::Avalonia.Rect(0, 0, 400, 300));

        var title = card.FindControl<TextBlock>("PART_Title");
        Assert.NotNull(title);
        Assert.Equal("Cortex Insights", title!.Text);
    }

    [AvaloniaFact]
    public void AIFeatureCard_ExperimentalCard_ShowsBadge()
    {
        var vm = new AIFeatureCardVM("chat", "Chat", "AccentPink", "IconMessageSquare", isChatExperimental: true);
        var card = new AIFeatureCard { DataContext = vm };
        using var window = AvaloniaTestBase.RenderInWindow(card, 400, 300);

        card.Measure(new global::Avalonia.Size(400, 300));
        card.Arrange(new global::Avalonia.Rect(0, 0, 400, 300));

        var badge = card.FindControl<Control>("PART_ExperimentalBadge");
        Assert.NotNull(badge);
        Assert.True(badge!.IsVisible);
    }

    [AvaloniaFact]
    public void AIFeatureCard_NonExperimental_BadgeHidden()
    {
        var vm = new AIFeatureCardVM("insights", "T", "AccentPurple", "IconSparklesFilled", isChatExperimental: false);
        var card = new AIFeatureCard { DataContext = vm };
        using var window = AvaloniaTestBase.RenderInWindow(card, 400, 300);

        card.Measure(new global::Avalonia.Size(400, 300));
        card.Arrange(new global::Avalonia.Rect(0, 0, 400, 300));

        var badge = card.FindControl<Control>("PART_ExperimentalBadge");
        Assert.False(badge?.IsVisible ?? true);
    }
}
