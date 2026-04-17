using System.ComponentModel;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Markup.Xaml;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.UI.Avalonia;
using AuraCore.UI.Avalonia.ViewModels;
using AuraCore.UI.Avalonia.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class AIFeaturesView : UserControl
{
    // Phase 5.3 Task 10 narrow-mode constants
    private const double SidebarWide       = 120;  // original width
    private const double SidebarNarrow     =  80;  // compressed at <1000 px
    private const double SidebarVeryNarrow =   0;  // hidden at <900 px

    private readonly INarrowModeService? _narrowMode;

    public AIFeaturesView()
    {
        InitializeComponent();

        // Resolve NarrowModeService from App singleton (null in test harness / design-time).
        _narrowMode = App.NarrowMode;
        if (_narrowMode is not null)
        {
            _narrowMode.PropertyChanged += OnNarrowModeChanged;
            // Apply initial state before the first layout pass so there is no flicker.
            ApplyNarrowMode();
        }

        Loaded += OnLoaded;
    }

    protected override void OnDetachedFromVisualTree(global::Avalonia.VisualTreeAttachmentEventArgs e)
    {
        if (_narrowMode is not null)
            _narrowMode.PropertyChanged -= OnNarrowModeChanged;

        base.OnDetachedFromVisualTree(e);
    }

    private void OnNarrowModeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(INarrowModeService.IsNarrow)
                           or nameof(INarrowModeService.IsVeryNarrow))
        {
            ApplyNarrowMode();
        }
    }

    /// <summary>
    /// Applies the three-state narrow layout:
    /// - Wide   (>= 1000 px): sidebar 120 DIP, full text, overview grid 2-col
    /// - Narrow  (< 1000 px): sidebar  80 DIP, compact style, overview grid 2-col
    /// - V-Narrow(< 900  px): sidebar   0 DIP (hidden),      overview grid 1-col
    /// </summary>
    private void ApplyNarrowMode()
    {
        bool isVeryNarrow = _narrowMode?.IsVeryNarrow ?? false;
        bool isNarrow     = _narrowMode?.IsNarrow     ?? false;

        // --- Detail mode: sidebar column width ---
        double sidebarWidth = isVeryNarrow ? SidebarVeryNarrow
                            : isNarrow     ? SidebarNarrow
                                           : SidebarWide;

        if (PART_DetailRoot?.ColumnDefinitions?.Count >= 1)
            PART_DetailRoot.ColumnDefinitions[0].Width = new GridLength(sidebarWidth);

        // Sidebar panel visibility (collapsed at very-narrow so it takes no space)
        if (PART_SectionNav is not null)
            PART_SectionNav.IsVisible = !isVeryNarrow;

        // Compact style class on this UserControl drives narrower button padding/font
        if (isNarrow && !isVeryNarrow)
            Classes.Add("narrow-nav");
        else
            Classes.Remove("narrow-nav");

        // --- Overview mode: card grid columns ---
        // Very-narrow -> 1-col stack; otherwise keep 2-col (Rows=2, Columns=2)
        if (PART_OverviewGrid is not null)
        {
            PART_OverviewGrid.Columns = isVeryNarrow ? 1 : 2;
            // At very-narrow, 1-col means all 4 cards stack vertically;
            // set Rows to 0 (auto) so UniformGrid calculates from child count.
            PART_OverviewGrid.Rows = isVeryNarrow ? 0 : 2;
        }
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

        // Wire chat toggle -> opt-in dialog when first enabling chat.
        vm.ChatOptInOpener = async () =>
        {
            var ownerWindow = global::Avalonia.VisualTree.VisualExtensions.FindAncestorOfType<Window>(this);
            if (ownerWindow is null) return false;

            var optInVm = new ChatOptInDialogViewModel(App.Services.GetRequiredService<AppSettings>());
            var dialog = new ChatOptInDialog(optInVm);
            await dialog.ShowDialog(ownerWindow);

            // On completion, ChatOptInDialogViewModel.CompleteFromStep2 has already set ChatEnabled
            return App.Services.GetRequiredService<AppSettings>().ChatEnabled;
        };

        // Wire card click -> navigate
        foreach (var card in new[] { vm.InsightsCard, vm.RecommendationsCard, vm.ScheduleCard, vm.ChatCard })
        {
            card.NavigateToDetail = vm.NavigateToSection;
        }

        // Apply localization to back button
        ApplyLocalization();
    }

    /// <summary>
    /// Applies localized strings to UI elements.
    /// </summary>
    private void ApplyLocalization()
    {
        if (PART_BackToOverviewBtn is not null)
            PART_BackToOverviewBtn.Content = LocalizationService.Get("ai.backToOverview");
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
            "chat" => App.Services.GetRequiredService<global::AuraCore.UI.Avalonia.Views.Pages.AI.ChatSection>(),
            _ => new UserControl { Content = new TextBlock { Text = $"[{section}] placeholder — wired in Task 20+" } },
        };
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
