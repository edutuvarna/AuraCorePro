using AuraCore.UI.Avalonia;
using AuraCore.UI.Avalonia.Services.AI;
using AuraCore.UI.Avalonia.ViewModels;
using AuraCore.UI.Avalonia.Views.Controls;
using AuraCore.UI.Avalonia.Views.Pages;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Headless.XUnit;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

public class AIFeaturesViewTests
{
    private AIFeaturesViewModel BuildVM()
    {
        var settings = new AppSettings { InsightsEnabled = true };
        var ambient = new CortexAmbientService(settings);
        return new AIFeaturesViewModel(settings, ambient);
    }

    [AvaloniaFact]
    public void Hero_Renders_WithTitleAndStatusChip()
    {
        var vm = BuildVM();
        var view = new AIFeaturesView { DataContext = vm };
        using var handle = AvaloniaTestBase.RenderInWindow(view, 1100, 700);

        view.Measure(new global::Avalonia.Size(1100, 700));
        view.Arrange(new global::Avalonia.Rect(0, 0, 1100, 700));

        var title = view.FindControl<TextBlock>("PART_HeroTitle");
        Assert.NotNull(title);
        Assert.Equal("AI Features", title!.Text);

        var chip = view.FindControl<StatusChip>("PART_HeroStatusChip");
        Assert.NotNull(chip);
    }

    [AvaloniaFact]
    public void OverviewGrid_Renders_FourCards()
    {
        var vm = BuildVM();
        var view = new AIFeaturesView { DataContext = vm };
        using var handle = AvaloniaTestBase.RenderInWindow(view, 1100, 700);

        view.Measure(new global::Avalonia.Size(1100, 700));
        view.Arrange(new global::Avalonia.Rect(0, 0, 1100, 700));

        var grid = view.FindControl<UniformGrid>("PART_OverviewGrid");
        Assert.NotNull(grid);
        Assert.True(grid!.IsVisible);
        Assert.Equal(4, grid.Children.Count);
        Assert.All(grid.Children, c => Assert.IsType<AIFeatureCard>(c));
    }

    [AvaloniaFact]
    public void DetailRoot_Hidden_WhenInOverviewMode()
    {
        var vm = BuildVM();
        var view = new AIFeaturesView { DataContext = vm };
        using var handle = AvaloniaTestBase.RenderInWindow(view, 1100, 700);

        view.Measure(new global::Avalonia.Size(1100, 700));
        view.Arrange(new global::Avalonia.Rect(0, 0, 1100, 700));

        var detailRoot = view.FindControl<Grid>("PART_DetailRoot");
        Assert.NotNull(detailRoot);
        Assert.False(detailRoot!.IsVisible);
    }

    [AvaloniaFact]
    public void DetailRoot_Visible_AfterNavigateToSection()
    {
        var vm = BuildVM();
        var view = new AIFeaturesView { DataContext = vm };
        using var handle = AvaloniaTestBase.RenderInWindow(view, 1100, 700);

        view.Measure(new global::Avalonia.Size(1100, 700));
        view.Arrange(new global::Avalonia.Rect(0, 0, 1100, 700));

        vm.NavigateToSection.Execute("insights");

        // Force a second layout pass after mode change
        view.InvalidateMeasure();
        view.Measure(new global::Avalonia.Size(1100, 700));
        view.Arrange(new global::Avalonia.Rect(0, 0, 1100, 700));

        var detailRoot = view.FindControl<Grid>("PART_DetailRoot");
        var overviewGrid = view.FindControl<UniformGrid>("PART_OverviewGrid");
        Assert.True(detailRoot!.IsVisible);
        Assert.False(overviewGrid!.IsVisible);
    }

    [AvaloniaFact]
    public void BackToOverview_ReshowsGrid()
    {
        var vm = BuildVM();
        var view = new AIFeaturesView { DataContext = vm };
        using var handle = AvaloniaTestBase.RenderInWindow(view, 1100, 700);

        view.Measure(new global::Avalonia.Size(1100, 700));
        view.Arrange(new global::Avalonia.Rect(0, 0, 1100, 700));

        vm.NavigateToSection.Execute("insights");
        vm.NavigateToOverview.Execute(null);

        view.InvalidateMeasure();
        view.Measure(new global::Avalonia.Size(1100, 700));
        view.Arrange(new global::Avalonia.Rect(0, 0, 1100, 700));

        var overviewGrid = view.FindControl<UniformGrid>("PART_OverviewGrid");
        Assert.True(overviewGrid!.IsVisible);
    }
}
