using System.Linq;
using System.Reflection;
using AuraCore.UI.Avalonia.Views.Controls;
using AuraCore.UI.Avalonia.Views.Pages;
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using global::Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

/// <summary>
/// Phase 4.0: SystemHealthView migrated to ModuleHeader + GlassCard shell.
/// </summary>
public class SystemHealthViewTests
{
    private static void EnsureAppServicesInitialized()
    {
        var field = typeof(AuraCore.UI.Avalonia.App)
            .GetField("_services", BindingFlags.NonPublic | BindingFlags.Static);
        if (field?.GetValue(null) is null)
        {
            var sc = new ServiceCollection();
            field!.SetValue(null, sc.BuildServiceProvider());
        }
    }

    [AvaloniaFact]
    public void Ctor_DoesNotThrow()
    {
        EnsureAppServicesInitialized();
        var v = new SystemHealthView();
        Assert.NotNull(v);
    }

    [AvaloniaFact]
    public void RenderInWindow_RunsJobs_DoesNotCrash()
    {
        EnsureAppServicesInitialized();
        var v = new SystemHealthView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(v.IsMeasureValid);
    }

    [AvaloniaFact]
    public void Layout_UsesModuleHeader()
    {
        EnsureAppServicesInitialized();
        var v = new SystemHealthView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var header = v.GetVisualDescendants().OfType<ModuleHeader>().FirstOrDefault();
        Assert.NotNull(header);
    }

    [AvaloniaFact]
    public void Layout_UsesGlassCard()
    {
        EnsureAppServicesInitialized();
        var v = new SystemHealthView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var cards = v.GetVisualDescendants().OfType<GlassCard>().ToList();
        Assert.NotEmpty(cards);
    }
}
