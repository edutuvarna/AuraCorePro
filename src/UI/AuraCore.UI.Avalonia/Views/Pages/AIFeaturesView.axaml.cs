using global::Avalonia.Controls;
using global::Avalonia.Markup.Xaml;
using AuraCore.UI.Avalonia.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class AIFeaturesView : UserControl
{
    public AIFeaturesView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Resolve VM from DI if not already set externally
        if (DataContext is not AIFeaturesViewModel vm)
        {
            try
            {
                var settings = App.Services.GetRequiredService<AppSettings>();
                var ambient = App.Services.GetRequiredService<Services.AI.ICortexAmbientService>();
                vm = new AIFeaturesViewModel(settings, ambient);
                DataContext = vm;
            }
            catch
            {
                return; // design-time or DI not available
            }
        }

        // Wire the section-view factory. Later tasks (20-24) will replace placeholders with real sections.
        vm.SectionViewFactory = CreateSectionView;

        // Wire card click -> navigate
        foreach (var card in new[] { vm.InsightsCard, vm.RecommendationsCard, vm.ScheduleCard, vm.ChatCard })
        {
            card.NavigateToDetail = vm.NavigateToSection;
        }
    }

    /// <summary>
    /// Returns the UserControl for a given section. Tasks 20-24 replace the placeholder
    /// branches with real section controls via DI lookup.
    /// </summary>
    private UserControl CreateSectionView(string section)
    {
        // Phase 3 placeholder -- Tasks 20-24 wire real sections here via DI:
        //   "insights" => App.Services.GetRequiredService<Views.Pages.AI.InsightsSection>(),
        //   "recommendations" => App.Services.GetRequiredService<Views.Pages.AI.RecommendationsSection>(),
        //   "schedule" => App.Services.GetRequiredService<Views.Pages.AI.ScheduleSection>(),
        //   "chat" => App.Services.GetRequiredService<Views.Pages.AI.ChatSection>(),
        return section switch
        {
            "insights" => App.Services.GetRequiredService<global::AuraCore.UI.Avalonia.Views.Pages.AI.InsightsSection>(),
            "recommendations" => App.Services.GetRequiredService<global::AuraCore.UI.Avalonia.Views.Pages.AI.RecommendationsSection>(),
            "schedule" => App.Services.GetRequiredService<global::AuraCore.UI.Avalonia.Views.Pages.AI.ScheduleSection>(),
            _ => new UserControl { Content = new TextBlock { Text = $"[{section}] placeholder — wired in Task 20+" } },
        };
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
