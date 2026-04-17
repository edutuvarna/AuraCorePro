using Avalonia.Headless.XUnit;
using AuraCore.Tests.UI.Avalonia.Helpers;
using AuraCore.UI.Avalonia.Views.Pages;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Responsive;

public class PilotModulesResponsiveTests
{
    [AvaloniaFact]
    public void DashboardView_renders_at_wide()
    {
        var ex = Record.Exception(() => ResponsiveRenderAsserts.AssertRendersAtSize<DashboardView>(1200, 800));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void DashboardView_renders_at_narrow()
    {
        var ex = Record.Exception(() => ResponsiveRenderAsserts.AssertRendersAtSize<DashboardView>(800, 600));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void SystemHealthView_renders_at_wide()
    {
        var ex = Record.Exception(() => ResponsiveRenderAsserts.AssertRendersAtSize<SystemHealthView>(1200, 800));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void SystemHealthView_renders_at_narrow()
    {
        var ex = Record.Exception(() => ResponsiveRenderAsserts.AssertRendersAtSize<SystemHealthView>(800, 600));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void BloatwareRemovalView_renders_at_wide()
    {
        var ex = Record.Exception(() => ResponsiveRenderAsserts.AssertRendersAtSize<BloatwareRemovalView>(1200, 800));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void BloatwareRemovalView_renders_at_narrow()
    {
        var ex = Record.Exception(() => ResponsiveRenderAsserts.AssertRendersAtSize<BloatwareRemovalView>(800, 600));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void RamOptimizerView_renders_at_wide()
    {
        var ex = Record.Exception(() => ResponsiveRenderAsserts.AssertRendersAtSize<RamOptimizerView>(1200, 800));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void RamOptimizerView_renders_at_narrow()
    {
        var ex = Record.Exception(() => ResponsiveRenderAsserts.AssertRendersAtSize<RamOptimizerView>(800, 600));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void AIFeaturesView_renders_at_wide_under_audit_suite()
    {
        // Re-validation of 5.3.2 Task 10 layout under the audit suite
        var ex = Record.Exception(() => ResponsiveRenderAsserts.AssertRendersAtSize<AIFeaturesView>(1200, 800));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void AIFeaturesView_renders_at_narrow_under_audit_suite()
    {
        var ex = Record.Exception(() => ResponsiveRenderAsserts.AssertRendersAtSize<AIFeaturesView>(800, 600));
        Assert.Null(ex);
    }
}
