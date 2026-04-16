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
/// Task 5 (Phase 4.0): HostsEditorView migrated to ModuleHeader + GlassCard shell.
/// HostsEditorView.ctor calls App.Services.GetServices, so we seed a minimal
/// ServiceProvider via reflection before constructing the view.
/// </summary>
public class HostsEditorViewTests
{
    /// <summary>
    /// Seed App._services with an empty container so GetServices returns an
    /// empty enumerable rather than throwing ArgumentNullException on a null provider.
    /// Uses reflection because App._services is private static.
    /// </summary>
    private static void EnsureAppServicesInitialized()
    {
        var field = typeof(AuraCore.UI.Avalonia.App)
            .GetField("_services", BindingFlags.NonPublic | BindingFlags.Static);
        if (field?.GetValue(null) is null)
        {
            var sc = new ServiceCollection();
            // Register HostsEditorModule so the view can resolve it (gracefully returns null if absent)
            AuraCore.Module.HostsEditor.HostsEditorRegistration.AddHostsEditorModule(sc);
            field!.SetValue(null, sc.BuildServiceProvider());
        }
    }

    [AvaloniaFact]
    public void Ctor_DoesNotThrow()
    {
        EnsureAppServicesInitialized();
        var v = new HostsEditorView();
        Assert.NotNull(v);
    }

    [AvaloniaFact]
    public void RenderInWindow_DoesNotCrash()
    {
        EnsureAppServicesInitialized();
        var v = new HostsEditorView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(v.IsMeasureValid);
    }

    [AvaloniaFact]
    public void Layout_UsesModuleHeader()
    {
        EnsureAppServicesInitialized();
        var v = new HostsEditorView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var header = v.GetVisualDescendants().OfType<ModuleHeader>().FirstOrDefault();
        Assert.NotNull(header);
    }

    [AvaloniaFact]
    public void Layout_UsesGlassCard()
    {
        EnsureAppServicesInitialized();
        var v = new HostsEditorView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var card = v.GetVisualDescendants().OfType<GlassCard>().FirstOrDefault();
        Assert.NotNull(card);
    }

    [AvaloniaFact]
    public void CodeBehind_NamedElements_StillResolve()
    {
        EnsureAppServicesInitialized();
        var v = new HostsEditorView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1200, 800);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // Header / localization
        Assert.NotNull(v.FindControl<TextBlock>("PageTitle"));
        Assert.NotNull(v.FindControl<TextBlock>("ReloadLabel"));

        // Status bar
        Assert.NotNull(v.FindControl<TextBlock>("FilePath"));
        Assert.NotNull(v.FindControl<TextBlock>("EntryCount"));
        Assert.NotNull(v.FindControl<TextBlock>("AdminStatus"));
        Assert.NotNull(v.FindControl<Border>("UnsavedBadge"));

        // Subtitle text (code-behind writes to it)
        Assert.NotNull(v.FindControl<TextBlock>("SubtitleText"));

        // Add-entry row inputs
        Assert.NotNull(v.FindControl<TextBox>("NewIp"));
        Assert.NotNull(v.FindControl<TextBox>("NewHost"));
        Assert.NotNull(v.FindControl<TextBox>("NewComment"));

        // Hosts list
        Assert.NotNull(v.FindControl<ItemsControl>("HostsList"));

        // Bottom bar
        Assert.NotNull(v.FindControl<TextBox>("ImportUrl"));
        Assert.NotNull(v.FindControl<Button>("SaveBtn"));
    }
}
