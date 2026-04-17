using Avalonia.Headless.XUnit;
using AuraCore.Tests.UI.Avalonia.Helpers;
using AuraCore.UI.Avalonia.Views.Controls;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Responsive;

public class StatRowResponsiveTests
{
    [AvaloniaFact]
    public void StatRow_renders_at_wide_size_without_throwing()
    {
        var ex = Record.Exception(() =>
            ResponsiveRenderAsserts.AssertRendersAtSize<StatRow>(1200, 400));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void StatRow_renders_at_narrow_size_without_throwing()
    {
        var ex = Record.Exception(() =>
            ResponsiveRenderAsserts.AssertRendersAtSize<StatRow>(800, 400));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void StatRow_renders_at_very_narrow_size_without_throwing()
    {
        var ex = Record.Exception(() =>
            ResponsiveRenderAsserts.AssertRendersAtSize<StatRow>(700, 400));
        Assert.Null(ex);
    }
}
