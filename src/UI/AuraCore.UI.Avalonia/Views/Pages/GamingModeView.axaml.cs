using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.GamingMode;
using AuraCore.Module.GamingMode.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

public record BgProcessItem(string Name, string MemText, string Category, bool Suggest, int Pid);

public partial class GamingModeView : UserControl
{
    private readonly GamingModeModule? _module;
    private readonly List<CheckBox> _toggleCbs = new();

    public GamingModeView()
    {
        InitializeComponent();
        Loaded += (s, e) => ApplyLocalization();
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
        _module = App.Services.GetServices<IOptimizationModule>()
            .OfType<GamingModeModule>().FirstOrDefault();
        Loaded += async (s, e) => await RunScan();
}

    private async Task RunScan()
    {
        if (_module is null) return;
        await _module.ScanAsync(new ScanOptions());
        var state = _module.LastState;
        if (state is null) return;

        UpdateStatus(state.IsActive);
        PowerPlanText.Text = $"Power Plan: {state.CurrentPowerPlan}";

        // Toggles
        TogglePanel.Children.Clear();
        _toggleCbs.Clear();
        foreach (var t in state.Toggles)
        {
            var card = new Border
            {
                CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 12),
                Background = new SolidColorBrush(Color.Parse("#1A1A28")),
                BorderBrush = new SolidColorBrush(Color.Parse("#33334A")), BorderThickness = new Thickness(1)
            };
            var grid = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("Auto,*,60") };
            var cb = new CheckBox { IsChecked = true, Tag = t.Id, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            _toggleCbs.Add(cb);
            Grid.SetColumn(cb, 0);
            grid.Children.Add(cb);
            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
            info.Children.Add(new TextBlock { Text = t.Name, FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = new SolidColorBrush(Color.Parse("#E8E8F0")) });
            info.Children.Add(new TextBlock { Text = t.Description, FontSize = 10, Foreground = new SolidColorBrush(Color.Parse("#555570")), TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn(info, 1);
            grid.Children.Add(info);
            var risk = new TextBlock { Text = t.Risk, FontSize = 10, FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse(t.Risk == "Caution" ? "#F59E0B" : "#22C55E")),
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(risk, 2);
            grid.Children.Add(risk);
            card.Child = grid;
            TogglePanel.Children.Add(card);
        }

        // Processes
        var items = state.BackgroundProcesses.Select(p => new BgProcessItem(
            p.Name, $"{p.MemoryMb} MB", p.Category, p.SuggestSuspend, p.Pid
        )).ToList();
        ProcessList.ItemsSource = items;
    }

    private void UpdateStatus(bool active)
    {
        if (active)
        {
            StatusLabel.Text = "ACTIVE - Gaming Mode On";
            StatusLabel.Foreground = new SolidColorBrush(Color.Parse("#22C55E"));
            StatusDot.Background = new SolidColorBrush(Color.Parse("#22C55E"));
            StatusBorder.Background = new SolidColorBrush(Color.Parse("#0D22C55E"));
            ActivateLabel.Text = "Deactivate";
            ActivateBtn.Background = new SolidColorBrush(Color.Parse("#EF4444"));
        }
        else
        {
            StatusLabel.Text = "Inactive";
            StatusLabel.Foreground = new SolidColorBrush(Color.Parse("#8888A0"));
            StatusDot.Background = new SolidColorBrush(Color.Parse("#555570"));
            StatusBorder.Background = new SolidColorBrush(Color.Parse("#1A1A28"));
            ActivateLabel.Text = "Activate";
            ActivateBtn.Background = new SolidColorBrush(Color.Parse("#00D4AA"));
        }
    }

    private async void Activate_Click(object? sender, RoutedEventArgs e)
    {
        if (_module is null) return;
        ActivateBtn.IsEnabled = false;
        ActivateLabel.Text = "Working...";

        var isActive = _module.IsActive;
        var ids = new List<string> { isActive ? "deactivate" : "activate" };
        if (!isActive)
            ids.AddRange(_toggleCbs.Where(cb => cb.IsChecked == true).Select(cb => cb.Tag!.ToString()!));

        var plan = new OptimizationPlan(_module.Id, ids);
        var progress = new Progress<TaskProgress>(p =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => SubText.Text = p.StatusText));

        await _module.OptimizeAsync(plan, progress);
        await RunScan();
        ActivateBtn.IsEnabled = true;
}

    private void ApplyLocalization()
    {
        PageTitle.Text = LocalizationService._("nav.gamingMode");
    }
}