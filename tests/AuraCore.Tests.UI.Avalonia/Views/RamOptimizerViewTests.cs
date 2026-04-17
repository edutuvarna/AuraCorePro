using System.Linq;
using System.Reflection;
using AuraCore.UI.Avalonia.Views.Controls;
using AuraCore.UI.Avalonia.Views.Pages;
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using global::Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

/// <summary>
/// Task 8 (Phase 4.0): RamOptimizerView migrated to ModuleHeader + GlassCard shell.
/// Stress-test pilot — 3 header actions (Scan/Optimize/Boost) + auto-optimize toggle
/// + gradient progress bar + history graph.
/// RamOptimizerView.ctor calls App.Services.GetServices, so we seed a minimal
/// ServiceProvider via reflection before constructing the view.
/// </summary>
public class RamOptimizerViewTests
{
    /// <summary>
    /// Seed App._services with an empty container so GetServices returns an
    /// empty enumerable rather than throwing ArgumentNullException on a null provider.
    /// Uses reflection because App._services is private static.
    /// </summary>
    private static void EnsureAppServicesInitialized()
    {
        var field = typeof(AuraCore.UI.Avalonia.App)
            .GetField("_services", BindingFlags.NonPublic | BindingFlags.Static);
        if (field?.GetValue(null) is null)
        {
            var sc = new ServiceCollection();
            field!.SetValue(null, sc.BuildServiceProvider());
        }
    }

    [AvaloniaFact]
    public void Ctor_DoesNotThrow()
    {
        EnsureAppServicesInitialized();
        var v = new RamOptimizerView();
        Assert.NotNull(v);
    }

    [AvaloniaFact]
    public void RenderInWindow_DoesNotCrash()
    {
        EnsureAppServicesInitialized();
        var v = new RamOptimizerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(v.IsMeasureValid);
    }

    [AvaloniaFact]
    public void Layout_UsesModuleHeader_WithMultipleActions()
    {
        EnsureAppServicesInitialized();
        var v = new RamOptimizerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var header = v.GetVisualDescendants().OfType<ModuleHeader>().FirstOrDefault();
        Assert.NotNull(header);
        var buttons = header!.GetVisualDescendants().OfType<Button>().Count();
        Assert.True(buttons >= 3, $"Expected >= 3 buttons in header Actions, found {buttons}");
    }

    [AvaloniaFact]
    public void CodeBehind_CriticalNames_StillResolve()
    {
        EnsureAppServicesInitialized();
        var v = new RamOptimizerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        // These are the most critical x:Names the code-behind depends on
        Assert.NotNull(v.FindControl<Button>("OptBtn"));
        Assert.NotNull(v.FindControl<Button>("BoostBtn"));
        Assert.NotNull(v.FindControl<TextBlock>("UsedRam"));
        Assert.NotNull(v.FindControl<TextBlock>("RamPct"));
        Assert.NotNull(v.FindControl<Border>("RamBar"));
    }
}
