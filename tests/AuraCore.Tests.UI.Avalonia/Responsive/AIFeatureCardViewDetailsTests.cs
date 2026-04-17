using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using AuraCore.UI.Avalonia.Views.Controls;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Responsive;

public class AIFeatureCardViewDetailsTests
{
    [AvaloniaFact]
    public void AIFeatureCard_exposes_ViewDetailsAffordance_named_TextBlock()
    {
        var card = new AIFeatureCard();
        var affordance = card.FindControl<TextBlock>("ViewDetailsAffordance");
        Assert.NotNull(affordance);
    }

    [AvaloniaFact]
    public void ViewDetailsAffordance_is_hit_test_invisible_so_card_body_remains_click_target()
    {
        var card = new AIFeatureCard();
        var affordance = card.FindControl<TextBlock>("ViewDetailsAffordance");
        Assert.NotNull(affordance);
        Assert.False(affordance.IsHitTestVisible,
            "The affordance must NOT eat clicks — the entire card body is the click target");
    }
}
