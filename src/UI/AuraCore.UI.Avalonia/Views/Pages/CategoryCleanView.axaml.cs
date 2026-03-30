using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.JunkCleaner;
using AuraCore.Module.DiskCleanup;
using AuraCore.Module.PrivacyCleaner;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class CategoryCleanView : UserControl
{
    private readonly IOptimizationModule _module;
    private readonly List<CatInfo> _cats = new();
    private readonly List<CheckBox> _catCheckBoxes = new();

    private record CatInfo(string Name, string Desc, string Risk, int Files, long Bytes, string SizeText, bool NeedAdmin);

    public CategoryCleanView() : this(null!) { }

    public CategoryCleanView(IOptimizationModule module)
    {
        InitializeComponent();
        _module = module;
        if (module is null) return;
        PageTitle.Text = module.DisplayName;
        PageSubtitle.Text = module.Id switch
        {
            "junk-cleaner"    => "Find and remove temporary files, caches, and system junk",
            "disk-cleanup"    => "Deep clean Windows system files and caches",
            "privacy-cleaner" => "Remove browser history, cookies, and telemetry traces",
            _ => "Scan and clean"
        };
        Loaded += async (s, e) => await RunScan();
    }

    private async Task RunScan()
    {
        ScanLabel.Text = "Scanning...";
        _cats.Clear();
        try
        {
            await _module.ScanAsync(new ScanOptions(DeepScan: true));

            if (_module is JunkCleanerModule jc && jc.LastReport is not null)
                _cats.AddRange(jc.LastReport.Categories.Select(c =>
                    new CatInfo(c.Name, c.Description, "Safe", c.FileCount, c.TotalBytes, c.TotalSizeDisplay, false)));
            else if (_module is DiskCleanupModule dc && dc.LastReport is not null)
                _cats.AddRange(dc.LastReport.Categories.Select(c =>
                    new CatInfo(c.Name, c.Description, c.RiskLevel, c.FileCount, c.TotalBytes, c.TotalSizeDisplay, c.RequiresAdmin)));
            else if (_module is PrivacyCleanerModule pc && pc.LastReport is not null)
                _cats.AddRange(pc.LastReport.Categories.Select(c =>
                    new CatInfo(c.Name, c.Description, c.RiskLevel, c.ItemCount, c.TotalBytes, c.TotalSizeDisplay, false)));

            CatCount.Text = _cats.Count.ToString();
            FileCount.Text = _cats.Sum(c => c.Files).ToString();
            TotalSize.Text = FormatBytes(_cats.Sum(c => c.Bytes));
            RenderCategories();
        }
        catch { StatusText.Text = "Scan failed"; }
        finally { ScanLabel.Text = "Scan"; }
    }

    private void RenderCategories()
    {
        CategoryPanel.Children.Clear();
        _catCheckBoxes.Clear();

        foreach (var cat in _cats)
        {
            var card = new Border
            {
                CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 12),
                Background = global::Avalonia.Application.Current!.FindResource("BgElevated") is Color c
                    ? new SolidColorBrush(c) : new SolidColorBrush(Color.Parse("#252538")),
                BorderBrush = new SolidColorBrush(Color.Parse("#33334A")), BorderThickness = new Thickness(1)
            };

            var grid = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("40,*,100,80") };

            var cb = new CheckBox
            {
                IsChecked = cat.Risk != "High" && !cat.NeedAdmin,
                IsEnabled = !cat.NeedAdmin,
                Tag = cat.Name, VerticalAlignment = VerticalAlignment.Center
            };
            cb.Click += (s, e) => UpdateCleanButton();
            _catCheckBoxes.Add(cb);
            Grid.SetColumn(cb, 0);
            grid.Children.Add(cb);

            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
            info.Children.Add(new TextBlock
            {
                Text = cat.Name, FontSize = 13, FontWeight = FontWeight.SemiBold,
                Foreground = global::Avalonia.Application.Current!.FindResource("TextPrimaryBrush") as ISolidColorBrush
            });
            info.Children.Add(new TextBlock
            {
                Text = cat.Desc, FontSize = 10, TextWrapping = TextWrapping.Wrap,
                Foreground = global::Avalonia.Application.Current!.FindResource("TextMutedBrush") as ISolidColorBrush
            });
            var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            meta.Children.Add(new TextBlock
            {
                Text = $"{cat.Files} files", FontSize = 10,
                Foreground = global::Avalonia.Application.Current!.FindResource("TextSecondaryBrush") as ISolidColorBrush
            });
            if (cat.NeedAdmin)
                meta.Children.Add(new TextBlock
                {
                    Text = "Admin required", FontSize = 10,
                    Foreground = new SolidColorBrush(Color.Parse("#F59E0B"))
                });
            info.Children.Add(meta);
            Grid.SetColumn(info, 1);
            grid.Children.Add(info);

            // Size
            var sizeText = new TextBlock
            {
                Text = cat.SizeText, FontSize = 14, FontWeight = FontWeight.Bold,
                Foreground = cat.Bytes > 100_000_000 ? new SolidColorBrush(Color.Parse("#EF4444"))
                           : cat.Bytes > 10_000_000 ? new SolidColorBrush(Color.Parse("#F59E0B"))
                           : global::Avalonia.Application.Current!.FindResource("AccentPrimaryBrush") as ISolidColorBrush,
                VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right
            };
            Grid.SetColumn(sizeText, 2);
            grid.Children.Add(sizeText);

            // Risk badge
            var (riskFg, riskBg) = cat.Risk switch
            {
                "High"   => ("#EF4444", "#20EF4444"),
                "Medium" => ("#F59E0B", "#20F59E0B"),
                "Low"    => ("#3B82F6", "#203B82F6"),
                _        => ("#22C55E", "#2022C55E")
            };
            var badge = new Border
            {
                CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 2),
                Background = new SolidColorBrush(Color.Parse(riskBg)),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock
            {
                Text = cat.Risk, FontSize = 10, FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse(riskFg))
            };
            Grid.SetColumn(badge, 3);
            grid.Children.Add(badge);

            card.Child = grid;
            CategoryPanel.Children.Add(card);
        }
        UpdateCleanButton();
    }

    private void UpdateCleanButton()
    {
        var selected = _catCheckBoxes.Count(cb => cb.IsChecked == true);
        CleanBtn.IsEnabled = selected > 0;
        CleanLabel.Text = selected > 0 ? $"Clean {selected} Category(s)" : "Clean Selected";
    }

    private void SelectAll_Click(object? sender, RoutedEventArgs e)
    {
        bool allChecked = _catCheckBoxes.All(cb => cb.IsChecked == true || !cb.IsEnabled);
        foreach (var cb in _catCheckBoxes)
            if (cb.IsEnabled) cb.IsChecked = !allChecked;
        SelectAllLabel.Text = allChecked ? "Select All" : "Deselect All";
        UpdateCleanButton();
    }

    private async void Scan_Click(object? sender, RoutedEventArgs e) => await RunScan();

    private async void Clean_Click(object? sender, RoutedEventArgs e)
    {
        CleanBtn.IsEnabled = false;
        CleanLabel.Text = "Cleaning...";
        try
        {
            var plan = new OptimizationPlan(_module.Id, new List<string> { "all" });
            var progress = new Progress<TaskProgress>(p =>
            {
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    StatusText.Text = $"{p.Percentage:F0}% - {p.StatusText}");
            });

            var result = await _module.OptimizeAsync(plan, progress);

            StatusText.Text = result.Success
                ? $"Cleaned {result.ItemsProcessed} items. Freed {FormatBytes(result.BytesFreed)} in {result.Duration.TotalSeconds:F1}s"
                : "Clean failed. Try running as administrator.";

            await RunScan(); // refresh
        }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
        finally { CleanLabel.Text = "Clean Selected"; }
    }

    private static string FormatBytes(long b) => b switch
    {
        >= 1073741824 => $"{b / 1073741824.0:F1} GB",
        >= 1048576    => $"{b / 1048576.0:F1} MB",
        >= 1024       => $"{b / 1024.0:F1} KB",
        > 0           => $"{b} B",
        _             => "0 B"
    };
}
