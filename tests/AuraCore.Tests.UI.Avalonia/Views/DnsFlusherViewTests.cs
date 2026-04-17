using System.Linq;
using AuraCore.UI.Avalonia.Views.Controls;
using AuraCore.UI.Avalonia.Views.Pages;
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using global::Avalonia.VisualTree;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

/// <summary>
/// Phase 4.4.1: DnsFlusherView — hero button layout (no Scan button in header,
/// auto-scan on Loaded). Mirrors GrubManager / JournalCleaner view test structure.
/// </summary>
public class DnsFlusherViewTests
{
    [AvaloniaFact]
    public void Ctor_DoesNotThrow()
    {
        var v = new DnsFlusherView();
        Assert.NotNull(v);
    }

    [AvaloniaFact]
    public void RenderInWindow_DoesNotCrash_OnLoaded()
    {
        var v = new DnsFlusherView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(v.IsMeasureValid);
    }

    [AvaloniaFact]
    public void ModuleHeader_HasExpectedTitle()
    {
        var v = new DnsFlusherView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var header = v.GetVisualDescendants().OfType<ModuleHeader>().FirstOrDefault();
        Assert.NotNull(header);
        Assert.Equal("DNS Flusher", header!.Title);
    }

    [AvaloniaFact]
    public void Header_ContainsNoScanButton()
    {
        // Phase 4.4.1 user-directed hero UX: NO manual Scan button.
        // Auto-scan is triggered via the View's Loaded event — same pattern as 4.3.6 GRUB Manager.
        var v = new DnsFlusherView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var buttons = v.GetVisualDescendants().OfType<Button>().ToList();
        Assert.DoesNotContain(buttons, b => b.Content is string s && s == "Scan");
        Assert.Null(v.FindControl<Button>("ScanBtn"));
    }

    [AvaloniaFact]
    public void FlushHeroButton_IsPresent()
    {
        var v = new DnsFlusherView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var flushBtn = v.FindControl<Button>("FlushBtn");
        Assert.NotNull(flushBtn);
        // Hero button should show the localized Flush DNS Cache label
        Assert.Equal("Flush DNS Cache", flushBtn!.Content as string);
    }
}
