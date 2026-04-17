using System.Linq;
using AuraCore.UI.Avalonia.Views.Controls;
using AuraCore.UI.Avalonia.Views.Pages;
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using global::Avalonia.VisualTree;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

/// <summary>
/// Task 7 (Phase 4.0): FirewallRulesView migrated to ModuleHeader + StatRow + StatCard + GlassCard shell.
/// FirewallRulesView.ctor does not call App.Services, so no ServiceProvider seeding is needed.
/// </summary>
public class FirewallRulesViewTests
{
    [AvaloniaFact]
    public void Ctor_DoesNotThrow()
    {
        var v = new FirewallRulesView();
        Assert.NotNull(v);
    }

    [AvaloniaFact]
    public void RenderInWindow_DoesNotCrash()
    {
        var v = new FirewallRulesView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(v.IsMeasureValid);
    }

    [AvaloniaFact]
    public void Layout_UsesModuleHeader_AndStatRow_WithThreeCards()
    {
        var v = new FirewallRulesView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // ModuleHeader must be present
        var header = v.GetVisualDescendants().OfType<ModuleHeader>().FirstOrDefault();
        Assert.NotNull(header);

        // StatRow must be present
        var statRow = v.GetVisualDescendants().OfType<StatRow>().FirstOrDefault();
        Assert.NotNull(statRow);

        // Exactly 3 StatCards (Total, Enabled, Blocked)
        var statCards = v.GetVisualDescendants().OfType<StatCard>().ToList();
        Assert.Equal(3, statCards.Count);
    }

    [AvaloniaFact]
    public void Layout_UsesGlassCard()
    {
        var v = new FirewallRulesView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var card = v.GetVisualDescendants().OfType<GlassCard>().FirstOrDefault();
        Assert.NotNull(card);
    }

    [AvaloniaFact]
    public void CodeBehind_NamedElements_StillResolve()
    {
        var v = new FirewallRulesView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // Localization target
        Assert.NotNull(v.FindControl<TextBlock>("PageTitle"));

        // Stat counter TextBlocks — code-behind writes counts via .Text
        Assert.NotNull(v.FindControl<TextBlock>("TotalRules"));
        Assert.NotNull(v.FindControl<TextBlock>("EnabledRules"));
        Assert.NotNull(v.FindControl<TextBlock>("BlockedRules"));

        // Rules panel controls
        Assert.NotNull(v.FindControl<TextBox>("SearchBox"));
        Assert.NotNull(v.FindControl<Button>("ScanBtn"));
        Assert.NotNull(v.FindControl<TextBlock>("SubText"));
        Assert.NotNull(v.FindControl<ItemsControl>("RulesList"));
    }
}
