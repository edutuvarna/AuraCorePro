using Avalonia.Headless.XUnit;
using AuraCore.Tests.UI.Avalonia.Helpers;
using AuraCore.UI.Avalonia.Views.Pages;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Responsive;

/// <summary>
/// Phase 5.3 Task 10 render smoke tests — AIFeaturesView renders at
/// wide / narrow / very-narrow without throwing or producing invalid bounds.
/// </summary>
public class AIFeaturesViewResponsiveTests
{
    [AvaloniaFact]
    public void AIFeaturesView_renders_wide_without_throwing()
    {
        var ex = Record.Exception(() =>
            ResponsiveRenderAsserts.AssertRendersAtSize<AIFeaturesView>(1200, 800));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void AIFeaturesView_renders_narrow_without_throwing()
    {
        var ex = Record.Exception(() =>
            ResponsiveRenderAsserts.AssertRendersAtSize<AIFeaturesView>(950, 600));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void AIFeaturesView_renders_very_narrow_without_throwing()
    {
        var ex = Record.Exception(() =>
            ResponsiveRenderAsserts.AssertRendersAtSize<AIFeaturesView>(850, 600));
        Assert.Null(ex);
    }
}
