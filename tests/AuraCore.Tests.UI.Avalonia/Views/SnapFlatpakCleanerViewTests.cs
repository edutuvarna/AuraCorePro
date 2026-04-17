using System.Linq;
using AuraCore.UI.Avalonia.Views.Controls;
using AuraCore.UI.Avalonia.Views.Pages;
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using global::Avalonia.VisualTree;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

/// <summary>
/// Phase 4.3.2: SnapFlatpakCleanerView uses the Phase 4.0 shell
/// (ModuleHeader + StatRow + GlassCard). Mirrors JournalCleanerViewTests structure.
/// </summary>
public class SnapFlatpakCleanerViewTests
{
    [AvaloniaFact]
    public void Ctor_DoesNotThrow()
    {
        var v = new SnapFlatpakCleanerView();
        Assert.NotNull(v);
    }

    [AvaloniaFact]
    public void RenderInWindow_DoesNotCrash_OnLoaded()
    {
        var v = new SnapFlatpakCleanerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(v.IsMeasureValid);
    }

    [AvaloniaFact]
    public void Layout_UsesModuleHeader()
    {
        var v = new SnapFlatpakCleanerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var header = v.GetVisualDescendants().OfType<ModuleHeader>().FirstOrDefault();
        Assert.NotNull(header);
    }

    [AvaloniaFact]
    public void ModuleHeader_HasExpectedTitle()
    {
        var v = new SnapFlatpakCleanerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var header = v.GetVisualDescendants().OfType<ModuleHeader>().FirstOrDefault();
        Assert.NotNull(header);
        // Title set in ApplyLocalizedTexts via LocalizationService._("nav.snapFlatpakCleaner")
        Assert.Equal("Snap / Flatpak Cleaner", header!.Title);
    }

    [AvaloniaFact]
    public void Layout_HasThreeCleanupButtons()
    {
        var v = new SnapFlatpakCleanerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.NotNull(v.FindControl<global::Avalonia.Controls.Button>("CleanSnapBtn"));
        Assert.NotNull(v.FindControl<global::Avalonia.Controls.Button>("CleanFlatpakBtn"));
        Assert.NotNull(v.FindControl<global::Avalonia.Controls.Button>("CleanBothBtn"));
    }

    [AvaloniaFact]
    public void Layout_UsesGlassCard()
    {
        var v = new SnapFlatpakCleanerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var cards = v.GetVisualDescendants().OfType<GlassCard>().ToList();
        Assert.NotEmpty(cards);
    }
}
