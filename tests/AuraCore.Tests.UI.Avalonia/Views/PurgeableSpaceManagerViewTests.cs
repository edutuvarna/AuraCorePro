using System.Linq;
using AuraCore.UI.Avalonia.Views.Controls;
using AuraCore.UI.Avalonia.Views.Pages;
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using global::Avalonia.VisualTree;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

/// <summary>
/// Phase 4.4.2: PurgeableSpaceManagerView — stacked-bar hero layout with
/// education + cleanup-actions + privilege cards. Auto-scan on Loaded (no
/// manual Scan button). Mirrors DnsFlusher / GrubManager view test structure.
/// </summary>
public class PurgeableSpaceManagerViewTests
{
    [AvaloniaFact]
    public void Ctor_DoesNotThrow()
    {
        var v = new PurgeableSpaceManagerView();
        Assert.NotNull(v);
    }

    [AvaloniaFact]
    public void RenderInWindow_DoesNotCrash_OnLoaded()
    {
        var v = new PurgeableSpaceManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(v.IsMeasureValid);
    }

    [AvaloniaFact]
    public void ModuleHeader_HasExpectedTitle()
    {
        var v = new PurgeableSpaceManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var header = v.GetVisualDescendants().OfType<ModuleHeader>().FirstOrDefault();
        Assert.NotNull(header);
        Assert.Equal("Purgeable Space", header!.Title);
    }

    [AvaloniaFact]
    public void Header_ContainsNoScanButton()
    {
        // Phase 4.4.2 hero UX: NO manual Scan button. Auto-scan via Loaded event.
        var v = new PurgeableSpaceManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var buttons = v.GetVisualDescendants().OfType<Button>().ToList();
        Assert.DoesNotContain(buttons, b => b.Content is string s && s == "Scan");
        Assert.Null(v.FindControl<Button>("ScanBtn"));
    }

    [AvaloniaFact]
    public void BreakdownBarGrid_IsPresent()
    {
        // The stacked-bar visualization: a named Grid whose ColumnDefinitions
        // get re-parsed from the VM string via code-behind.
        var v = new PurgeableSpaceManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var grid = v.FindControl<Grid>("BreakdownBar");
        Assert.NotNull(grid);
        // Default seeded shape has three columns even before a VM attaches.
        Assert.Equal(3, grid!.ColumnDefinitions.Count);
    }

    [AvaloniaFact]
    public void CleanupActionButtons_ArePresent()
    {
        var v = new PurgeableSpaceManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.NotNull(v.FindControl<Button>("CleanCachesBtn"));
        Assert.NotNull(v.FindControl<Button>("RunPeriodicBtn"));
        Assert.NotNull(v.FindControl<Button>("ThinSnapshotsBtn"));
    }
}
