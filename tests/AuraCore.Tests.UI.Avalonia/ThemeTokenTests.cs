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
}
