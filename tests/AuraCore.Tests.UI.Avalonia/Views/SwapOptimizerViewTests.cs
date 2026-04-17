using System.Linq;
using AuraCore.UI.Avalonia.Views.Controls;
using AuraCore.UI.Avalonia.Views.Pages;
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using global::Avalonia.VisualTree;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

/// <summary>
/// Phase 4.0: SwapOptimizerView migrated to ModuleHeader + StatRow + GlassCard shell.
/// Linux-only module. Largest in the final batch (96 lines original).
/// </summary>
public class SwapOptimizerViewTests
{
    [AvaloniaFact]
    public void Ctor_DoesNotThrow()
    {
        var v = new SwapOptimizerView();
        Assert.NotNull(v);
    }

    [AvaloniaFact]
    public void RenderInWindow_DoesNotCrash()
    {
        var v = new SwapOptimizerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(v.IsMeasureValid);
    }

    [AvaloniaFact]
    public void Layout_UsesModuleHeader()
    {
        var v = new SwapOptimizerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var header = v.GetVisualDescendants().OfType<ModuleHeader>().FirstOrDefault();
        Assert.NotNull(header);
    }

    [AvaloniaFact]
    public void Layout_UsesGlassCard()
    {
        var v = new SwapOptimizerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var cards = v.GetVisualDescendants().OfType<GlassCard>().ToList();
        Assert.NotEmpty(cards);
    }

    [AvaloniaFact]
    public void CodeBehind_NamedElements_StillResolve()
    {
        var v = new SwapOptimizerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // Localization target (hidden bridge)
        Assert.NotNull(v.FindControl<TextBlock>("PageTitle"));

        // Stat counter bridges — code-behind writes values directly
        Assert.NotNull(v.FindControl<TextBlock>("SwapTotal"));
        Assert.NotNull(v.FindControl<TextBlock>("SwapUsed"));
        Assert.NotNull(v.FindControl<TextBlock>("Swappiness"));

        // Named scan button — code-behind toggles IsEnabled
        Assert.NotNull(v.FindControl<Button>("ScanBtn"));

        // Status / subtitle and list
        Assert.NotNull(v.FindControl<TextBlock>("SubText"));
        Assert.NotNull(v.FindControl<ItemsControl>("SwapList"));

        // Recommendations TextBlock — code-behind writes recommendation string
        Assert.NotNull(v.FindControl<TextBlock>("RecommendationText"));
    }
}
