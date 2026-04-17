using System.Linq;
using AuraCore.UI.Avalonia.Views.Controls;
using AuraCore.UI.Avalonia.Views.Pages;
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using global::Avalonia.VisualTree;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

/// <summary>
/// Phase 4.0: LaunchAgentManagerView migrated to ModuleHeader + StatRow + GlassCard shell.
/// </summary>
public class LaunchAgentManagerViewTests
{
    [AvaloniaFact]
    public void Ctor_DoesNotThrow()
    {
        var v = new LaunchAgentManagerView();
        Assert.NotNull(v);
    }

    [AvaloniaFact]
    public void RenderInWindow_DoesNotCrash()
    {
        var v = new LaunchAgentManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(v.IsMeasureValid);
    }

    [AvaloniaFact]
    public void Layout_UsesModuleHeader()
    {
        var v = new LaunchAgentManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var header = v.GetVisualDescendants().OfType<ModuleHeader>().FirstOrDefault();
        Assert.NotNull(header);
    }

    [AvaloniaFact]
    public void Layout_UsesGlassCard()
    {
        var v = new LaunchAgentManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var card = v.GetVisualDescendants().OfType<GlassCard>().FirstOrDefault();
        Assert.NotNull(card);
    }

    [AvaloniaFact]
    public void Layout_UsesStatRow_WithThreeCards()
    {
        var v = new LaunchAgentManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var statRow = v.GetVisualDescendants().OfType<StatRow>().FirstOrDefault();
        Assert.NotNull(statRow);

        var statCards = v.GetVisualDescendants().OfType<StatCard>().ToList();
        Assert.Equal(3, statCards.Count);
    }

    [AvaloniaFact]
    public void CodeBehind_NamedElements_StillResolve()
    {
        var v = new LaunchAgentManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // Localization target (hidden)
        Assert.NotNull(v.FindControl<TextBlock>("PageTitle"));

        // Stat counter TextBlocks — code-behind writes counts via .Text
        Assert.NotNull(v.FindControl<TextBlock>("UserAgents"));
        Assert.NotNull(v.FindControl<TextBlock>("SystemAgents"));
        Assert.NotNull(v.FindControl<TextBlock>("DaemonCount"));

        // Agent list panel
        Assert.NotNull(v.FindControl<TextBlock>("SubText"));
        Assert.NotNull(v.FindControl<ItemsControl>("AgentList"));
    }
}
