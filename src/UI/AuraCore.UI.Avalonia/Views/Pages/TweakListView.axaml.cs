using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.ContextMenu;
using AuraCore.Module.TaskbarTweaks;
using AuraCore.Module.ExplorerTweaks;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class TweakListView : UserControl
{
    private readonly IOptimizationModule _module;
    private readonly List<TweakItem> _tweaks = new();
    private readonly List<CheckBox> _checkBoxes = new();

    private record TweakItem(string Id, string Name, string Desc, string Category, string Risk, bool IsApplied);

    public TweakListView() : this(null!) { }

    public TweakListView(IOptimizationModule module)
    {
        InitializeComponent();
        _module = module;
        if (module is null) return;
        PageTitle.Text = module.DisplayName;
        PageSubtitle.Text = module.Id switch
        {
            "context-menu" => "Add or remove items from your right-click menu",
            "taskbar-tweaks" => "Customize Windows taskbar behavior and appearance",
            "explorer-tweaks" => "Tweak File Explorer settings and behavior",
            _ => "Toggle system tweaks"
        };
        Loaded += async (s, e) => await RunScan();
    }

    private async Task RunScan()
    {
        ScanLabel.Text = "Scanning...";
        try
        {
            await _module.ScanAsync(new ScanOptions());
            _tweaks.Clear();

            // Extract tweaks from module-specific report
            if (_module is ContextMenuModule cm && cm.LastReport is not null)
                _tweaks.AddRange(cm.LastReport.Tweaks.Select(t =>
                    new TweakItem(t.Id, t.Name, t.Description, t.Category, t.Risk, t.IsApplied)));
            else if (_module is TaskbarTweaksModule tb && tb.LastReport is not null)
                _tweaks.AddRange(tb.LastReport.Tweaks.Select(t =>
                    new TweakItem(t.Id, t.Name, t.Description, t.Category, t.Risk, t.IsApplied)));
            else if (_module is ExplorerTweaksModule ex && ex.LastReport is not null)
                _tweaks.AddRange(ex.LastReport.Tweaks.Select(t =>
                    new TweakItem(t.Id, t.Name, t.Description, t.Category, t.Risk, t.IsApplied)));

            TotalCount.Text = _tweaks.Count.ToString();
            AppliedCount.Text = _tweaks.Count(t => t.IsApplied).ToString();
            AvailableCount.Text = _tweaks.Count(t => !t.IsApplied).ToString();

            RenderTweaks();
        }
        catch { StatusText.Text = "Scan failed"; }
        finally { ScanLabel.Text = "Scan"; }
    }

    private void RenderTweaks()
    {
        TweakPanel.Children.Clear();
        _checkBoxes.Clear();

        string? lastCat = null;
        foreach (var t in _tweaks)
        {
            // Category header
            if (t.Category != lastCat)
            {
                lastCat = t.Category;
                TweakPanel.Children.Add(new TextBlock
                {
                    Text = t.Category.ToUpperInvariant(),
                    FontSize = 10, FontWeight = FontWeight.SemiBold,
                    Foreground = new SolidColorBrush(Color.Parse("#555570")),
                    Margin = new Thickness(12, lastCat == _tweaks[0].Category ? 8 : 16, 0, 6)
                });
            }

            // Tweak row
            var row = new Border
            {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 10),
                Margin = new Thickness(0, 1),
                Background = Brushes.Transparent
            };

            var grid = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("40,*,80,80") };

            var cb = new CheckBox
            {
                IsChecked = t.IsApplied,
                Tag = t.Id,
                VerticalAlignment = VerticalAlignment.Center
            };
            cb.Click += (s, e) => UpdateApplyButton();
            _checkBoxes.Add(cb);
            Grid.SetColumn(cb, 0);
            grid.Children.Add(cb);

            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(new TextBlock
            {
                Text = t.Name, FontSize = 12, FontWeight = FontWeight.SemiBold,
                Foreground = global::Avalonia.Application.Current!.FindResource("TextPrimaryBrush") as ISolidColorBrush
            });
            info.Children.Add(new TextBlock
            {
                Text = t.Desc, FontSize = 10,
                Foreground = global::Avalonia.Application.Current!.FindResource("TextMutedBrush") as ISolidColorBrush,
                TextWrapping = TextWrapping.Wrap
            });
            Grid.SetColumn(info, 1);
            grid.Children.Add(info);

            // Risk badge
            var (riskFg, riskBg) = t.Risk switch
            {
                "Caution" => ("#F59E0B", "#20F59E0B"),
                "Safe" => ("#22C55E", "#2022C55E"),
                _ => ("#8888A0", "#208888A0")
            };
            var riskBadge = new Border
            {
                CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 2),
                Background = new SolidColorBrush(Color.Parse(riskBg)),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
            riskBadge.Child = new TextBlock
            {
                Text = t.Risk, FontSize = 10, FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse(riskFg))
            };
            Grid.SetColumn(riskBadge, 2);
            grid.Children.Add(riskBadge);

            // Status
            var status = new TextBlock
            {
                Text = t.IsApplied ? "Active" : "Off", FontSize = 10, FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse(t.IsApplied ? "#22C55E" : "#8888A0")),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(status, 3);
            grid.Children.Add(status);

            row.Child = grid;
            TweakPanel.Children.Add(row);
        }

        UpdateApplyButton();
    }

    private void UpdateApplyButton()
    {
        // Count tweaks whose checkbox state differs from their original state
        int changes = 0;
        for (int i = 0; i < _checkBoxes.Count && i < _tweaks.Count; i++)
            if (_checkBoxes[i].IsChecked != _tweaks[i].IsApplied) changes++;
        ApplyBtn.IsEnabled = changes > 0;
        ApplyLabel.Text = changes > 0 ? $"Apply {changes} Change(s)" : "Apply Selected";
    }

    private async void Scan_Click(object? sender, RoutedEventArgs e) => await RunScan();

    private async void Apply_Click(object? sender, RoutedEventArgs e)
    {
        ApplyBtn.IsEnabled = false;
        ApplyLabel.Text = "Applying...";
        StatusText.Text = "";

        try
        {
            var ids = new List<string>();
            for (int i = 0; i < _checkBoxes.Count && i < _tweaks.Count; i++)
                if (_checkBoxes[i].IsChecked != _tweaks[i].IsApplied)
                    ids.Add(_tweaks[i].Id);

            if (ids.Count == 0) return;

            var plan = new OptimizationPlan(_module.Id, ids);
            var result = await _module.OptimizeAsync(plan);

            StatusText.Text = result.Success
                ? $"Applied {result.ItemsProcessed} tweak(s) in {result.Duration.TotalSeconds:F1}s. Explorer may restart."
                : "Some tweaks failed. Try running as administrator.";

            await RunScan(); // refresh state
        }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
        finally { ApplyLabel.Text = "Apply Selected"; }
    }
}
