using System.Linq;
using AuraCore.UI.Avalonia.Views.Controls;
using AuraCore.UI.Avalonia.Views.Pages;
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using global::Avalonia.VisualTree;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

/// <summary>
/// Phase 4.3.1: JournalCleanerView uses the Phase 4.0 shell
/// (ModuleHeader + StatRow + GlassCard). Follows the ScheduleSection test pattern
/// for theme-variant brush regression guarding (see hotfix 442518f).
/// </summary>
public class JournalCleanerViewTests
{
    [AvaloniaFact]
    public void Ctor_DoesNotThrow()
    {
        var v = new JournalCleanerView();
        Assert.NotNull(v);
    }

    [AvaloniaFact]
    public void RenderInWindow_DoesNotCrash_OnLoaded()
    {
        var v = new JournalCleanerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(v.IsMeasureValid);
    }

    [AvaloniaFact]
    public void Layout_UsesModuleHeader()
    {
        var v = new JournalCleanerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var header = v.GetVisualDescendants().OfType<ModuleHeader>().FirstOrDefault();
        Assert.NotNull(header);
    }

    [AvaloniaFact]
    public void ModuleHeader_HasExpectedTitle()
    {
        var v = new JournalCleanerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var header = v.GetVisualDescendants().OfType<ModuleHeader>().FirstOrDefault();
        Assert.NotNull(header);
        // Title is set in ApplyLocalizedTexts via LocalizationService._("nav.journalCleaner")
        Assert.Equal("Journal Cleaner", header!.Title);
    }

    [AvaloniaFact]
    public void Layout_HasFourVacuumButtons()
    {
        var v = new JournalCleanerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.NotNull(v.FindControl<global::Avalonia.Controls.Button>("Vacuum500Btn"));
        Assert.NotNull(v.FindControl<global::Avalonia.Controls.Button>("Vacuum1GBtn"));
        Assert.NotNull(v.FindControl<global::Avalonia.Controls.Button>("Vacuum7DaysBtn"));
        Assert.NotNull(v.FindControl<global::Avalonia.Controls.Button>("Vacuum30DaysBtn"));
    }

    [AvaloniaFact]
    public void Layout_UsesGlassCard()
    {
        var v = new JournalCleanerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var cards = v.GetVisualDescendants().OfType<GlassCard>().ToList();
        Assert.NotEmpty(cards);
    }
}
