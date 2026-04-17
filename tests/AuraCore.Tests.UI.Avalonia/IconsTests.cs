using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia;

public class IconsTests
{
    private static Styles LoadIcons()
    {
        var uri = new System.Uri("avares://AuraCore.Pro/Themes/Icons.axaml");
        return (Styles)AvaloniaXamlLoader.Load(uri);
    }

    [AvaloniaTheory]
    [InlineData("IconDashboard")]
    [InlineData("IconCpu")]
    [InlineData("IconRam")]
    [InlineData("IconGpu")]
    [InlineData("IconHardDrive")]
    [InlineData("IconHeart")]
    [InlineData("IconZap")]
    [InlineData("IconSparkles")]
    [InlineData("IconRotateCcw")]
    [InlineData("IconGamepad")]
    [InlineData("IconShield")]
    [InlineData("IconShieldCheck")]
    [InlineData("IconPackage")]
    [InlineData("IconStar")]
    [InlineData("IconSettings")]
    [InlineData("IconTarget")]
    [InlineData("IconTrendingUp")]
    [InlineData("IconAlertTriangle")]
    [InlineData("IconArrowRight")]
    [InlineData("IconTrash")]
    [InlineData("IconUser")]
    [InlineData("IconCheck")]
    [InlineData("IconChevronDown")]
    [InlineData("IconX")]
    [InlineData("IconEye")]
    [InlineData("IconActivity")]
    [InlineData("IconDroplet")]
    [InlineData("IconClock")]
    [InlineData("IconDatabase")]
    [InlineData("IconWifi")]
    [InlineData("IconSparklesFilled")]
    [InlineData("IconLightbulb")]
    [InlineData("IconCalendarClock")]
    [InlineData("IconMessageSquare")]
    [InlineData("IconDownload")]
    [InlineData("IconWarningTriangleFilled")]
    [InlineData("IconLock")]
    public void Icon_Resolves_AsGeometry(string key)
    {
        var styles = LoadIcons();
        Assert.True(
            styles.Resources.TryGetResource(key, ThemeVariant.Default, out var value),
            $"Icon '{key}' not found.");
        Assert.IsAssignableFrom<Geometry>(value);
    }
}
