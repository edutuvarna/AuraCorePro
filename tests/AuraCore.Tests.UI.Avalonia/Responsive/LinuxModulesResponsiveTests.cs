using Avalonia.Headless.XUnit;
using AuraCore.Tests.UI.Avalonia.Helpers;
using AuraCore.UI.Avalonia.Views.Pages;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Responsive;

/// <summary>
/// Phase 5.3 Task 13 render smoke tests — 6 Linux modules render at
/// wide (1200×800) and narrow (800×600) without throwing or producing invalid bounds.
/// </summary>
public class LinuxModulesResponsiveTests
{
    [AvaloniaFact]
    public void JournalCleanerView_renders_at_wide()
    {
        var ex = Record.Exception(() => ResponsiveRenderAsserts.AssertRendersAtSize<JournalCleanerView>(1200, 800));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void JournalCleanerView_renders_at_narrow()
    {
        var ex = Record.Exception(() => ResponsiveRenderAsserts.AssertRendersAtSize<JournalCleanerView>(800, 600));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void SnapFlatpakCleanerView_renders_at_wide()
    {
        var ex = Record.Exception(() => ResponsiveRenderAsserts.AssertRendersAtSize<SnapFlatpakCleanerView>(1200, 800));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void SnapFlatpakCleanerView_renders_at_narrow()
    {
        var ex = Record.Exception(() => ResponsiveRenderAsserts.AssertRendersAtSize<SnapFlatpakCleanerView>(800, 600));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void DockerCleanerView_renders_at_wide()
    {
        var ex = Record.Exception(() => ResponsiveRenderAsserts.AssertRendersAtSize<DockerCleanerView>(1200, 800));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void DockerCleanerView_renders_at_narrow()
    {
        var ex = Record.Exception(() => ResponsiveRenderAsserts.AssertRendersAtSize<DockerCleanerView>(800, 600));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void KernelCleanerView_renders_at_wide()
    {
        var ex = Record.Exception(() => ResponsiveRenderAsserts.AssertRendersAtSize<KernelCleanerView>(1200, 800));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void KernelCleanerView_renders_at_narrow()
    {
        var ex = Record.Exception(() => ResponsiveRenderAsserts.AssertRendersAtSize<KernelCleanerView>(800, 600));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void LinuxAppInstallerView_renders_at_wide()
    {
        var ex = Record.Exception(() => ResponsiveRenderAsserts.AssertRendersAtSize<LinuxAppInstallerView>(1200, 800));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void LinuxAppInstallerView_renders_at_narrow()
    {
        var ex = Record.Exception(() => ResponsiveRenderAsserts.AssertRendersAtSize<LinuxAppInstallerView>(800, 600));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void GrubManagerView_renders_at_wide()
    {
        var ex = Record.Exception(() => ResponsiveRenderAsserts.AssertRendersAtSize<GrubManagerView>(1200, 800));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void GrubManagerView_renders_at_narrow()
    {
        var ex = Record.Exception(() => ResponsiveRenderAsserts.AssertRendersAtSize<GrubManagerView>(800, 600));
        Assert.Null(ex);
    }
}
