using Avalonia.Headless.XUnit;
using AuraCore.Tests.UI.Avalonia.Helpers;
using AuraCore.UI.Avalonia.Views.Pages;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Responsive;

/// <summary>
/// Phase 5.3 Task 14 render smoke tests — 5 macOS modules render at
/// wide (1200×800) and narrow (800×600) without throwing or producing invalid bounds.
/// </summary>
public class MacOSModulesResponsiveTests
{
    [AvaloniaFact]
    public void DnsFlusherView_renders_at_wide()
    {
        var ex = Record.Exception(() => ResponsiveRenderAsserts.AssertRendersAtSize<DnsFlusherView>(1200, 800));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void DnsFlusherView_renders_at_narrow()
    {
        var ex = Record.Exception(() => ResponsiveRenderAsserts.AssertRendersAtSize<DnsFlusherView>(800, 600));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void PurgeableSpaceManagerView_renders_at_wide()
    {
        var ex = Record.Exception(() => ResponsiveRenderAsserts.AssertRendersAtSize<PurgeableSpaceManagerView>(1200, 800));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void PurgeableSpaceManagerView_renders_at_narrow()
    {
        var ex = Record.Exception(() => ResponsiveRenderAsserts.AssertRendersAtSize<PurgeableSpaceManagerView>(800, 600));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void SpotlightManagerView_renders_at_wide()
    {
        var ex = Record.Exception(() => ResponsiveRenderAsserts.AssertRendersAtSize<SpotlightManagerView>(1200, 800));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void SpotlightManagerView_renders_at_narrow()
    {
        var ex = Record.Exception(() => ResponsiveRenderAsserts.AssertRendersAtSize<SpotlightManagerView>(800, 600));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void XcodeCleanerView_renders_at_wide()
    {
        var ex = Record.Exception(() => ResponsiveRenderAsserts.AssertRendersAtSize<XcodeCleanerView>(1200, 800));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void XcodeCleanerView_renders_at_narrow()
    {
        var ex = Record.Exception(() => ResponsiveRenderAsserts.AssertRendersAtSize<XcodeCleanerView>(800, 600));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void MacAppInstallerView_renders_at_wide()
    {
        var ex = Record.Exception(() => ResponsiveRenderAsserts.AssertRendersAtSize<MacAppInstallerView>(1200, 800));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void MacAppInstallerView_renders_at_narrow()
    {
        var ex = Record.Exception(() => ResponsiveRenderAsserts.AssertRendersAtSize<MacAppInstallerView>(800, 600));
        Assert.Null(ex);
    }
}
