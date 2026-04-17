using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using AuraCore.Tests.UI.Avalonia.Helpers;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Helpers;

public class ResponsiveRenderAssertsTests
{
    private sealed class TrivialControl : UserControl
    {
        public TrivialControl()
        {
            Content = new TextBlock { Text = "hi", Name = "HelloText" };
        }
    }

    [AvaloniaFact]
    public void AssertRendersAtSize_succeeds_for_trivial_control_at_wide()
    {
        var ex = Record.Exception(() =>
            ResponsiveRenderAsserts.AssertRendersAtSize<TrivialControl>(1200, 800));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void AssertRendersAtSize_succeeds_for_trivial_control_at_narrow()
    {
        var ex = Record.Exception(() =>
            ResponsiveRenderAsserts.AssertRendersAtSize<TrivialControl>(800, 600));
        Assert.Null(ex);
    }
}
