using System.Linq;
using AuraCore.UI.Avalonia.Views.Controls;
using AuraCore.UI.Avalonia.Views.Pages;
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using global::Avalonia.VisualTree;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

/// <summary>
/// Phase 4.0: SystemdManagerView migrated to ModuleHeader + StatRow + GlassCard shell.
/// </summary>
public class SystemdManagerViewTests
{
    [AvaloniaFact]
    public void Ctor_DoesNotThrow()
    {
        var v = new SystemdManagerView();
        Assert.NotNull(v);
    }

    [AvaloniaFact]
    public void RenderInWindow_DoesNotCrash()
    {
        var v = new SystemdManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(v.IsMeasureValid);
    }

    [AvaloniaFact]
    public void Layout_UsesModuleHeader()
    {
        var v = new SystemdManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var header = v.GetVisualDescendants().OfType<ModuleHeader>().FirstOrDefault();
        Assert.NotNull(header);
    }

    [AvaloniaFact]
    public void Layout_UsesGlassCard()
    {
        var v = new SystemdManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var card = v.GetVisualDescendants().OfType<GlassCard>().FirstOrDefault();
        Assert.NotNull(card);
    }
}
