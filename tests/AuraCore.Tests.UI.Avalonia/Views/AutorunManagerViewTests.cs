using System.Linq;
using System.Reflection;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.UI.Avalonia.Views.Controls;
using AuraCore.UI.Avalonia.Views.Pages;
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using global::Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

/// <summary>
/// Task 6 (Phase 4.0): AutorunManagerView migrated to ModuleHeader + StatRow + GlassCard shell.
/// AutorunManagerView.ctor calls App.Services.GetServices, so we seed a minimal
/// ServiceProvider via reflection before constructing the view.
/// </summary>
public class AutorunManagerViewTests
{
    /// <summary>
    /// Seed App._services with an empty container so GetServices returns an
    /// empty enumerable rather than throwing ArgumentNullException on a null provider.
    /// </summary>
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
        var v = new AutorunManagerView();
        Assert.NotNull(v);
    }

    [AvaloniaFact]
    public void RenderInWindow_DoesNotCrash()
    {
        EnsureAppServicesInitialized();
        var v = new AutorunManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(v.IsMeasureValid);
    }

    [AvaloniaFact]
    public void Layout_UsesModuleHeader()
    {
        EnsureAppServicesInitialized();
        var v = new AutorunManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var header = v.GetVisualDescendants().OfType<ModuleHeader>().FirstOrDefault();
        Assert.NotNull(header);
    }

    [AvaloniaFact]
    public void Layout_UsesGlassCard()
    {
        EnsureAppServicesInitialized();
        var v = new AutorunManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var cards = v.GetVisualDescendants().OfType<GlassCard>().ToList();
        Assert.NotEmpty(cards);
    }

    [AvaloniaFact]
    public void CodeBehind_NamedElements_StillResolve()
    {
        EnsureAppServicesInitialized();
        var v = new AutorunManagerView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // Localization target
        Assert.NotNull(v.FindControl<TextBlock>("PageTitle"));

        // Scan button label — code-behind writes "Scanning..." / "Scan"
        Assert.NotNull(v.FindControl<TextBlock>("ScanLabel"));

        // Subtitle — code-behind writes "Scan failed" on error
        Assert.NotNull(v.FindControl<TextBlock>("SubtitleText"));

        // Stat counter TextBlocks — code-behind writes counts directly
        Assert.NotNull(v.FindControl<TextBlock>("TotalCount"));
        Assert.NotNull(v.FindControl<TextBlock>("EnabledCount"));
        Assert.NotNull(v.FindControl<TextBlock>("DisabledCount"));
        Assert.NotNull(v.FindControl<TextBlock>("HighRiskCount"));

        // Autorun list — code-behind sets ItemsSource
        Assert.NotNull(v.FindControl<ItemsControl>("AutorunList"));
    }
}
