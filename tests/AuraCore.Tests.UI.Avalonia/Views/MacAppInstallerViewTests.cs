using System.Linq;
using AuraCore.UI.Avalonia.Views.Controls;
using AuraCore.UI.Avalonia.Views.Pages;
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using global::Avalonia.VisualTree;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

/// <summary>
/// Phase 4.4.5: MacAppInstallerView uses Layout X "accordion + live search"
/// (ModuleHeader with TextBox + Scan button, 3-stat StatRow, ItemsControl of
/// bundles, sticky action footer, status card, privilege warning).
/// Mirrors LinuxAppInstallerViewTests structure — macOS twin.
/// </summary>
public class MacAppInstallerViewTests
{
    [AvaloniaFact]
    public void Ctor_DoesNotThrow()
    {
        var v = new MacAppInstallerView();
        Assert.NotNull(v);
    }

    [AvaloniaFact]
    public void RenderInWindow_DoesNotCrash_OnLoaded()
    {
        var v = new MacAppInstallerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(v.IsMeasureValid);
    }

    [AvaloniaFact]
    public void ModuleHeader_HasExpectedTitle()
    {
        var v = new MacAppInstallerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var header = v.GetVisualDescendants().OfType<ModuleHeader>().FirstOrDefault();
        Assert.NotNull(header);
        Assert.Equal("Mac App Installer", header!.Title);
    }

    [AvaloniaFact]
    public void SearchBox_IsPresent_WithWatermark()
    {
        var v = new MacAppInstallerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var searchBox = v.FindControl<TextBox>("SearchBox");
        Assert.NotNull(searchBox);
        Assert.False(string.IsNullOrWhiteSpace(searchBox!.Watermark));
    }

    [AvaloniaFact]
    public void ScanButton_IsPresent()
    {
        var v = new MacAppInstallerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var scanBtn = v.FindControl<Button>("ScanBtn");
        Assert.NotNull(scanBtn);
    }

    [AvaloniaFact]
    public void BundlesList_StatRow_AndKeyControls_Present()
    {
        var v = new MacAppInstallerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var bundlesList = v.FindControl<ItemsControl>("BundlesList");
        Assert.NotNull(bundlesList);

        var cards = v.GetVisualDescendants().OfType<StatCard>().ToList();
        Assert.Equal(3, cards.Count);

        Assert.NotNull(v.FindControl<Button>("InstallSelectedBtn"));
        Assert.NotNull(v.FindControl<Button>("CancelBtn"));
        Assert.NotNull(v.FindControl<TextBlock>("PrivilegeWarning"));
    }
}
