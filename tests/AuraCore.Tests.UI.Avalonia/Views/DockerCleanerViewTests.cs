using System.Linq;
using AuraCore.UI.Avalonia.Views.Controls;
using AuraCore.UI.Avalonia.Views.Pages;
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using global::Avalonia.VisualTree;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

/// <summary>
/// Phase 4.3.3: DockerCleanerView uses the Phase 4.0 shell
/// (ModuleHeader + StatRow + 3 GlassCards + Danger Zone border).
/// Mirrors SnapFlatpakCleanerViewTests / JournalCleanerViewTests structure.
/// </summary>
public class DockerCleanerViewTests
{
    [AvaloniaFact]
    public void Ctor_DoesNotThrow()
    {
        var v = new DockerCleanerView();
        Assert.NotNull(v);
    }

    [AvaloniaFact]
    public void RenderInWindow_DoesNotCrash_OnLoaded()
    {
        var v = new DockerCleanerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(v.IsMeasureValid);
    }

    [AvaloniaFact]
    public void ModuleHeader_HasExpectedTitle()
    {
        var v = new DockerCleanerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var header = v.GetVisualDescendants().OfType<ModuleHeader>().FirstOrDefault();
        Assert.NotNull(header);
        Assert.Equal("Docker Cleaner", header!.Title);
    }

    [AvaloniaFact]
    public void StatRow_HasFourCards()
    {
        var v = new DockerCleanerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var cards = v.GetVisualDescendants().OfType<StatCard>().ToList();
        Assert.Equal(4, cards.Count);
    }

    [AvaloniaFact]
    public void AllThreeGlassCards_Present()
    {
        // Expected GlassCards: Safe Cleanup, Granular Control, Status/Progress, Privilege warning
        // (plus a Border for Danger Zone — see separate test below).
        // The hero section of Layout A uses 3 "semantic" cards; we just assert GlassCards are present.
        var v = new DockerCleanerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var cards = v.GetVisualDescendants().OfType<GlassCard>().ToList();
        // Safe + Granular + Status + Privilege = at least 3 semantically, 4 total.
        Assert.True(cards.Count >= 3, $"Expected >=3 GlassCards, got {cards.Count}");
        // Ensure the named prune buttons are reachable (Safe + Granular + Volumes).
        Assert.NotNull(v.FindControl<Button>("PruneSafeBtn"));
        Assert.NotNull(v.FindControl<Button>("PruneImagesBtn"));
        Assert.NotNull(v.FindControl<Button>("PruneContainersBtn"));
        Assert.NotNull(v.FindControl<Button>("PruneBuildCacheBtn"));
        Assert.NotNull(v.FindControl<Button>("PruneVolumesBtn"));
    }

    [AvaloniaFact]
    public void DangerZoneCheckbox_Present()
    {
        var v = new DockerCleanerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var cb = v.FindControl<CheckBox>("VolumeAckCheckBox");
        Assert.NotNull(cb);
    }
}
