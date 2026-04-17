using System.Linq;
using AuraCore.UI.Avalonia.Views.Controls;
using AuraCore.UI.Avalonia.Views.Pages;
using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using global::Avalonia.VisualTree;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

public class DnsBenchmarkViewTests
{
    [AvaloniaFact]
    public void Ctor_DoesNotThrow()
    {
        var v = new DnsBenchmarkView();
        Assert.NotNull(v);
    }

    [AvaloniaFact]
    public void RenderInWindow_DoesNotCrash_OnLoaded()
    {
        var v = new DnsBenchmarkView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1000, 700);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(v.IsMeasureValid);
    }

    [AvaloniaFact]
    public void Layout_UsesModuleHeader_WithExpectedTitle()
    {
        var v = new DnsBenchmarkView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1000, 700);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var header = v.GetVisualDescendants()
            .OfType<ModuleHeader>()
            .FirstOrDefault();
        Assert.NotNull(header);
        Assert.False(string.IsNullOrWhiteSpace(header!.Title));
    }

    [AvaloniaFact]
    public void CodeBehind_NamedElements_StillResolve()
    {
        var v = new DnsBenchmarkView();
        using var handle = AvaloniaTestBase.RenderInWindow(v, 1000, 700);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.NotNull(v.FindControl<TextBlock>("PageTitle"));
        Assert.NotNull(v.FindControl<Button>("BenchBtn"));
        Assert.NotNull(v.FindControl<TextBlock>("SubText"));
        Assert.NotNull(v.FindControl<ItemsControl>("DnsList"));
        Assert.NotNull(v.FindControl<TextBlock>("RecommendText"));
    }
}
