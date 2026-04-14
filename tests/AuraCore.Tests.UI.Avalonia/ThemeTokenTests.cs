using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia;

public class ThemeTokenTests
{
    private static Styles LoadThemeV2()
    {
        var uri = new System.Uri("avares://AuraCore.Pro/Themes/AuraCoreThemeV2.axaml");
        return (Styles)AvaloniaXamlLoader.Load(uri);
    }

    [AvaloniaTheory]
    [InlineData("BgDeepBrush", "#0A0A10")]
    [InlineData("BgSurfaceBrush", "#0E0E14")]
    [InlineData("AccentTealBrush", "#00D4AA")]
    [InlineData("AccentPurpleBrush", "#B088FF")]
    [InlineData("AccentAmberBrush", "#F59E0B")]
    [InlineData("AccentPinkBrush", "#EC4899")]
    [InlineData("TextPrimaryBrush", "#F0F0F5")]
    [InlineData("TextSecondaryBrush", "#E8E8F0")]
    [InlineData("TextMutedBrush", "#888899")]
    [InlineData("BgCardBrush", "#06FFFFFF")]
    [InlineData("BorderSubtleBrush", "#0FFFFFFF")]
    [InlineData("AccentTealDimBrush", "#1400D4AA")]
    [InlineData("StatusErrorBrush", "#EF4444")]
    public void ColorToken_Resolves_WithExpectedValue(string key, string expectedHex)
    {
        var styles = LoadThemeV2();
        var resources = styles.Resources;
        Assert.True(
            resources.TryGetResource(key, ThemeVariant.Dark, out var value),
            $"Token '{key}' not found in theme.");
        var brush = Assert.IsAssignableFrom<ISolidColorBrush>(value);
        Assert.Equal(Color.Parse(expectedHex), brush.Color);
    }

    [AvaloniaTheory]
    [InlineData("RadiusXs", 4.0)]
    [InlineData("RadiusSm", 6.0)]
    [InlineData("RadiusMd", 8.0)]
    [InlineData("RadiusLg", 12.0)]
    [InlineData("RadiusXl", 14.0)]
    public void RadiusToken_Resolves_WithExpectedValue(string key, double expected)
    {
        var styles = LoadThemeV2();
        Assert.True(
            styles.Resources.TryGetResource(key, ThemeVariant.Dark, out var value),
            $"Radius token '{key}' not found.");
        var radius = Assert.IsType<CornerRadius>(value);
        Assert.Equal(expected, radius.TopLeft);
    }

    [AvaloniaTheory]
    [InlineData("FontSizeDisplay", 24.0)]
    [InlineData("FontSizeHeading", 18.0)]
    [InlineData("FontSizeSubheading", 14.0)]
    [InlineData("FontSizeBody", 12.0)]
    [InlineData("FontSizeBodySmall", 11.0)]
    [InlineData("FontSizeLabel", 10.0)]
    [InlineData("FontSizeCaption", 9.0)]
    [InlineData("FontSizeMicro", 8.0)]
    [InlineData("FontSizeTiny", 7.0)]
    [InlineData("FontSizeLarge", 14.0)]
    public void FontSizeToken_Resolves_WithExpectedValue(string key, double expected)
    {
        var styles = LoadThemeV2();
        Assert.True(
            styles.Resources.TryGetResource(key, ThemeVariant.Dark, out var value),
            $"Font size token '{key}' not found.");
        Assert.Equal(expected, Assert.IsType<double>(value));
    }
}
