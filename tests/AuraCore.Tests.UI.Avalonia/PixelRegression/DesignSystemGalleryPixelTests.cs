using System.Threading.Tasks;
using AuraCore.Tests.UI.Avalonia.TestViews;
using Avalonia.Headless.XUnit;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.PixelRegression;

/// <summary>
/// Snapshots the design-system primitive grid. Regressions in tokens
/// (colors, radii, typography, spacing) or shared controls (StatusChip,
/// AccentBadge) will show up as pixel diffs here.
///
/// DesignSystemGallery is a test-only view hosting purely static content —
/// no DI, no OS calls, no timers. Deterministic by construction.
/// </summary>
[Trait("Category", "PixelRegression")]
public class DesignSystemGalleryPixelTests
{
    [AvaloniaFact]
    public async Task Gallery_wide()
    {
        var png = await PixelRegressionHarness.RenderViewAsync<DesignSystemGallery>(1200, 900);
        await PixelVerify.Verify(png);
    }

    [AvaloniaFact]
    public async Task Gallery_narrow()
    {
        var png = await PixelRegressionHarness.RenderViewAsync<DesignSystemGallery>(800, 1100);
        await PixelVerify.Verify(png);
    }
}
