using System.Threading.Tasks;
using AuraCore.Tests.UI.Avalonia.TestViews;
using AuraCore.UI.Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.PixelRegression;

/// <summary>
/// Light-variant snapshots mirroring Phase 6.2's DesignSystemGalleryPixelTests.
/// Applies AppTheme.Light at test start and restores the previous theme at
/// teardown so Dark-oriented tests aren't polluted.
/// </summary>
[Trait("Category", "PixelRegression")]
public class DesignSystemGalleryLightPixelTests
{
    [AvaloniaFact]
    public async Task Gallery_wide_light()
    {
        var prev = ThemeService.CurrentTheme;
        ThemeService.SetTheme(ThemeService.AppTheme.Light);
        Dispatcher.UIThread.RunJobs();
        try
        {
            var png = await PixelRegressionHarness.RenderViewAsync<DesignSystemGallery>(1200, 900);
            await PixelVerify.Verify(png);
        }
        finally
        {
            ThemeService.SetTheme(prev);
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public async Task Gallery_narrow_light()
    {
        var prev = ThemeService.CurrentTheme;
        ThemeService.SetTheme(ThemeService.AppTheme.Light);
        Dispatcher.UIThread.RunJobs();
        try
        {
            var png = await PixelRegressionHarness.RenderViewAsync<DesignSystemGallery>(800, 1100);
            await PixelVerify.Verify(png);
        }
        finally
        {
            ThemeService.SetTheme(prev);
            Dispatcher.UIThread.RunJobs();
        }
    }
}
