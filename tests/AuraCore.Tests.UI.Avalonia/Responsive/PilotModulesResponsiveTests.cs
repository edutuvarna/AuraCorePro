using Avalonia.Headless.XUnit;
using AuraCore.Tests.UI.Avalonia.Helpers;
using AuraCore.UI.Avalonia.Views.Pages;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Responsive;

/// <summary>
/// Phase 5.3.3 pilot-module narrow-mode render smoke tests.
///
/// 3 of the 5 pilot views (SystemHealthView, BloatwareRemovalView,
/// RamOptimizerView) call <c>App.Services.GetServices&lt;IOptimizationModule&gt;()</c>
/// in their constructors. As of Phase debt-B2, <c>AvaloniaTestApplication.Initialize()</c>
/// bootstraps a minimal DI container with those 3 modules registered, so all
/// pilot-view render tests now run with real view construction under the
/// headless Avalonia harness.
///
/// DashboardView and AIFeaturesView have constructors that tolerate
/// null-DI or don't require services — their wide + narrow renders are
/// the live proofs that Phase 5.3.1 infrastructure + 5.3.2 narrow
/// layout both integrate correctly with real view trees.
/// </summary>
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
