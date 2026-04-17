using System.Linq;
using AuraCore.UI.Avalonia.Views.Controls;
using AuraCore.UI.Avalonia.Views.Pages;
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using global::Avalonia.VisualTree;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

/// <summary>
/// Phase 4.0: SymlinkManagerView migrated to ModuleHeader + GlassCard shell.
/// Win+Linux shared module — same migration pattern applies.
/// </summary>
public class SymlinkManagerViewTests
{
    [AvaloniaFact]
    public void Ctor_DoesNotThrow()
    {
        var v = new SymlinkManagerView();
        Assert.NotNull(v);
    }

    [AvaloniaFact]
    public void RenderInWindow_DoesNotCrash()
    {
        var v = new SymlinkManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(v.IsMeasureValid);
    }

    [AvaloniaFact]
    public void Layout_UsesModuleHeader()
    {
        var v = new SymlinkManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var header = v.GetVisualDescendants().OfType<ModuleHeader>().FirstOrDefault();
        Assert.NotNull(header);
    }

    [AvaloniaFact]
    public void Layout_UsesGlassCard()
    {
        var v = new SymlinkManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var cards = v.GetVisualDescendants().OfType<GlassCard>().ToList();
        Assert.NotEmpty(cards);
    }

    [AvaloniaFact]
    public void CodeBehind_NamedElements_StillResolve()
    {
        var v = new SymlinkManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // Localization target (hidden bridge)
        Assert.NotNull(v.FindControl<TextBlock>("PageTitle"));

        // Create link form fields
        Assert.NotNull(v.FindControl<ComboBox>("LinkTypeCombo"));
        Assert.NotNull(v.FindControl<TextBox>("LinkPathBox"));
        Assert.NotNull(v.FindControl<TextBox>("TargetPathBox"));

        // Status feedback TextBlocks
        Assert.NotNull(v.FindControl<TextBlock>("StatusText"));

        // Scan section
        Assert.NotNull(v.FindControl<TextBox>("ScanPathBox"));
        Assert.NotNull(v.FindControl<ItemsControl>("LinkList"));
        Assert.NotNull(v.FindControl<TextBlock>("ScanStatus"));
    }
}
