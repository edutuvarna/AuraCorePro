using System.Linq;
using AuraCore.UI.Avalonia.Views.Controls;
using AuraCore.UI.Avalonia.Views.Pages;
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using global::Avalonia.VisualTree;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

/// <summary>
/// Phase 4.4.4: XcodeCleanerView — Layout A safety-tiered with 3 risk
/// buckets (Safe hero, Granular grid, Danger ack-gated). Auto-scan on
/// Loaded (no Scan button). Mirrors Docker 4.3.3 / Spotlight 4.4.3 view
/// test structure.
/// </summary>
public class XcodeCleanerViewTests
{
    [AvaloniaFact]
    public void Ctor_DoesNotThrow()
    {
        var v = new XcodeCleanerView();
        Assert.NotNull(v);
    }

    [AvaloniaFact]
    public void RenderInWindow_DoesNotCrash_OnLoaded()
    {
        var v = new XcodeCleanerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(v.IsMeasureValid);
    }

    [AvaloniaFact]
    public void ModuleHeader_HasExpectedTitle()
    {
        var v = new XcodeCleanerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var header = v.GetVisualDescendants().OfType<ModuleHeader>().FirstOrDefault();
        Assert.NotNull(header);
        Assert.Equal("Xcode Cleaner", header!.Title);
    }

    [AvaloniaFact]
    public void Header_ContainsNoScanButton()
    {
        // Phase 4.4.4 hero UX: NO manual Scan button. Auto-scan via Loaded event.
        var v = new XcodeCleanerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var buttons = v.GetVisualDescendants().OfType<Button>().ToList();
        Assert.DoesNotContain(buttons, b => b.Content is string s && s == "Scan");
        Assert.Null(v.FindControl<Button>("ScanBtn"));
    }

    [AvaloniaFact]
    public void Layout_HasThreeRiskBuckets()
    {
        // Layout A safety-tiered: Safe hero + Granular + Danger cards all present.
        var v = new XcodeCleanerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // Safe hero card
        Assert.NotNull(v.FindControl<GlassCard>("SafeCard"));
        // Granular grid card
        Assert.NotNull(v.FindControl<GlassCard>("GranularCard"));
        // Danger zone card (Border, not GlassCard)
        Assert.NotNull(v.FindControl<Border>("DangerCard"));
    }

    [AvaloniaFact]
    public void DangerZone_HasAcknowledgeCheckBox()
    {
        var v = new XcodeCleanerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var ack = v.FindControl<CheckBox>("DangerAckCheckBox");
        Assert.NotNull(ack);
    }
}
