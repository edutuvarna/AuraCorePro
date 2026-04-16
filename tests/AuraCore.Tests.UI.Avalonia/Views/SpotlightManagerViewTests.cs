using System.Linq;
using AuraCore.UI.Avalonia.Views.Controls;
using AuraCore.UI.Avalonia.Views.Pages;
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using global::Avalonia.VisualTree;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

/// <summary>
/// Phase 4.4.3: SpotlightManagerView — per-volume ToggleSwitch list with
/// inline Rebuild confirmation. Auto-scan on Loaded (no Scan button).
/// Mirrors DnsFlusher / PurgeableSpaceManager view test structure.
/// </summary>
public class SpotlightManagerViewTests
{
    [AvaloniaFact]
    public void Ctor_DoesNotThrow()
    {
        var v = new SpotlightManagerView();
        Assert.NotNull(v);
    }

    [AvaloniaFact]
    public void RenderInWindow_DoesNotCrash_OnLoaded()
    {
        var v = new SpotlightManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(v.IsMeasureValid);
    }

    [AvaloniaFact]
    public void ModuleHeader_HasExpectedTitle()
    {
        var v = new SpotlightManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var header = v.GetVisualDescendants().OfType<ModuleHeader>().FirstOrDefault();
        Assert.NotNull(header);
        Assert.Equal("Spotlight Manager", header!.Title);
    }

    [AvaloniaFact]
    public void Header_ContainsNoScanButton()
    {
        // Phase 4.4.3 hero UX: NO manual Scan button. Auto-scan via Loaded event.
        var v = new SpotlightManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var buttons = v.GetVisualDescendants().OfType<Button>().ToList();
        Assert.DoesNotContain(buttons, b => b.Content is string s && s == "Scan");
        Assert.Null(v.FindControl<Button>("ScanBtn"));
    }

    [AvaloniaFact]
    public void StatRow_HasThreeStatCards()
    {
        var v = new SpotlightManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.NotNull(v.FindControl<StatCard>("VolumesStatCard"));
        Assert.NotNull(v.FindControl<StatCard>("IndexedStatCard"));
        Assert.NotNull(v.FindControl<StatCard>("DisabledStatCard"));
    }

    [AvaloniaFact]
    public void VolumeItemsControl_IsPresent()
    {
        var v = new SpotlightManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var list = v.FindControl<ItemsControl>("VolumeItemsList");
        Assert.NotNull(list);
    }
}
