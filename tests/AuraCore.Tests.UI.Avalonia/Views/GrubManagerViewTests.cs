using System.Linq;
using AuraCore.UI.Avalonia.Views.Controls;
using AuraCore.UI.Avalonia.Views.Pages;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Headless.XUnit;
using global::Avalonia.VisualTree;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

/// <summary>
/// Phase 4.3.6: GrubManagerView — form-based layout (no Scan button, auto-scan on Loaded).
/// Mirrors KernelCleaner / DockerCleaner / LinuxAppInstaller view test structure.
/// </summary>
public class GrubManagerViewTests
{
    [AvaloniaFact]
    public void Ctor_DoesNotThrow()
    {
        var v = new GrubManagerView();
        Assert.NotNull(v);
    }

    [AvaloniaFact]
    public void RenderInWindow_DoesNotCrash_OnLoaded()
    {
        var v = new GrubManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(v.IsMeasureValid);
    }

    [AvaloniaFact]
    public void ModuleHeader_HasExpectedTitle()
    {
        var v = new GrubManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var header = v.GetVisualDescendants().OfType<ModuleHeader>().FirstOrDefault();
        Assert.NotNull(header);
        Assert.Equal("GRUB Manager", header!.Title);
    }

    [AvaloniaFact]
    public void Header_ContainsNoScanButton()
    {
        // Phase 4.3.6 user-requested tweak: NO manual Scan button in header.
        // Auto-scan is triggered via the View's Loaded event.
        var v = new GrubManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var buttons = v.GetVisualDescendants().OfType<Button>().ToList();
        Assert.DoesNotContain(buttons, b => b.Content is string s && s == "Scan");
        // Also assert no button named "ScanBtn"
        Assert.Null(v.FindControl<Button>("ScanBtn"));
    }

    [AvaloniaFact]
    public void EditControls_ArePresent()
    {
        // Slider + ComboBox + ToggleSwitch represent the three editable settings
        var v = new GrubManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.NotNull(v.FindControl<Slider>("TimeoutSlider"));
        Assert.NotNull(v.FindControl<ComboBox>("DefaultComboBox"));
        Assert.NotNull(v.FindControl<ToggleSwitch>("OsProberToggle"));
    }

    [AvaloniaFact]
    public void ApplyResetRollback_Buttons_ArePresent()
    {
        var v = new GrubManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.NotNull(v.FindControl<Button>("ApplyChangesBtn"));
        Assert.NotNull(v.FindControl<Button>("ResetBtn"));
        Assert.NotNull(v.FindControl<Button>("RollbackBtn"));
    }
}
