using System.Linq;
using AuraCore.UI.Avalonia.Views.Controls;
using AuraCore.UI.Avalonia.Views.Pages;
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using global::Avalonia.VisualTree;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

/// <summary>
/// Phase 4.3.4: KernelCleanerView uses Layout B "safety-tiered, kernel-list variant"
/// (ModuleHeader + 2-stat StatRow + 4 GlassCards + Danger Zone border + ItemsControl).
/// Mirrors DockerCleanerViewTests / SnapFlatpakCleanerViewTests structure.
/// </summary>
public class KernelCleanerViewTests
{
    [AvaloniaFact]
    public void Ctor_DoesNotThrow()
    {
        var v = new KernelCleanerView();
        Assert.NotNull(v);
    }

    [AvaloniaFact]
    public void RenderInWindow_DoesNotCrash_OnLoaded()
    {
        var v = new KernelCleanerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(v.IsMeasureValid);
    }

    [AvaloniaFact]
    public void ModuleHeader_HasExpectedTitle()
    {
        var v = new KernelCleanerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var header = v.GetVisualDescendants().OfType<ModuleHeader>().FirstOrDefault();
        Assert.NotNull(header);
        Assert.Equal("Kernel Cleaner", header!.Title);
    }

    [AvaloniaFact]
    public void StatRow_HasTwoCards()
    {
        var v = new KernelCleanerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var cards = v.GetVisualDescendants().OfType<StatCard>().ToList();
        Assert.Equal(2, cards.Count);
    }

    [AvaloniaFact]
    public void AllGlassCards_AndDangerBorder_Present()
    {
        // Expected GlassCards: Safe Cleanup, Manual Selection, Status/Progress, Privilege warning (4 total)
        // Plus a Border (not GlassCard) for Danger Zone — it has a red border color.
        var v = new KernelCleanerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var cards = v.GetVisualDescendants().OfType<GlassCard>().ToList();
        Assert.True(cards.Count >= 4, $"Expected >=4 GlassCards, got {cards.Count}");

        // Verify key named buttons from Safe Cleanup + Manual Selection + Danger Zone
        Assert.NotNull(v.FindControl<Button>("AutoRemoveOldBtn"));
        Assert.NotNull(v.FindControl<Button>("RemoveSelectedBtn"));
        Assert.NotNull(v.FindControl<Button>("RemoveAllButCurrentBtn"));
        Assert.NotNull(v.FindControl<CheckBox>("DangerAckCheckBox"));
    }

    [AvaloniaFact]
    public void KernelItemsList_IsPresent()
    {
        var v = new KernelCleanerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var list = v.FindControl<ItemsControl>("KernelItemsList");
        Assert.NotNull(list);
    }
}
