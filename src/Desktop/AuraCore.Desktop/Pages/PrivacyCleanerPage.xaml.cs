using AuraCore.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.PrivacyCleaner;
using AuraCore.Module.PrivacyCleaner.Models;

namespace AuraCore.Desktop.Pages;

public sealed partial class PrivacyCleanerPage : Page
{
    private PrivacyCleanerModule? _module;
    private PrivacyScanReport? _lastReport;
    private readonly Dictionary<string, bool> _categorySelections = new();

    private static readonly Windows.UI.Color Purple = Windows.UI.Color.FromArgb(255, 124, 31, 162);
    private static readonly Windows.UI.Color Green = Windows.UI.Color.FromArgb(255, 46, 125, 50);
    private static readonly Windows.UI.Color Blue = Windows.UI.Color.FromArgb(255, 33, 150, 243);
    private static readonly Windows.UI.Color Amber = Windows.UI.Color.FromArgb(255, 230, 81, 0);
    private static readonly Windows.UI.Color Red = Windows.UI.Color.FromArgb(255, 198, 40, 40);

    public PrivacyCleanerPage()
    {
        InitializeComponent();
        var modules = App.Current.Services.GetServices<IOptimizationModule>();
        _module = modules.FirstOrDefault(m => m.Id == "privacy-cleaner") as PrivacyCleanerModule;

        ApplyLocalization();
        Services.S.LanguageChanged += () => DispatcherQueue?.TryEnqueue(ApplyLocalization);
    }

    private async void ScanBtn_Click(object sender, RoutedEventArgs e)
    {
        ScanBtn.IsEnabled = false; CleanBtn.IsEnabled = false;
        Progress.IsActive = true; Progress.Visibility = Visibility.Visible;
        StatusText.Text = S._("priv.scanning");
        CategoryList.Children.Clear(); ResultCard.Visibility = Visibility.Collapsed;
        _categorySelections.Clear();

        try
        {
            if (_module is null) { StatusText.Text = "Module not available."; return; }
            await _module.ScanAsync(new ScanOptions());
            _lastReport = _module.LastReport;

            if (_lastReport is null || _lastReport.TotalItems == 0)
            {
                StatusText.Text = S._("priv.clean");
                SummaryCard.Visibility = Visibility.Collapsed;
                return;
            }

            TotalSizeText.Text = _lastReport.TotalSizeDisplay;
            TotalItemsText.Text = _lastReport.TotalItems.ToString("N0");
            TotalCategoriesText.Text = _lastReport.Categories.Count.ToString();
            SummaryCard.Visibility = Visibility.Visible;

            // Select All / Deselect All / Safe Only
            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 4, 0, 4) };
            var selectAllBtn = new Button { Content = S._("priv.selectAll"), Padding = new Thickness(12, 4, 12, 4), FontSize = 12 };
            selectAllBtn.Click += (s, ev) => { foreach (var k in _categorySelections.Keys.ToList()) _categorySelections[k] = true; RebuildUI(); };
            var deselectBtn = new Button { Content = S._("priv.deselectAll"), Padding = new Thickness(12, 4, 12, 4), FontSize = 12 };
            deselectBtn.Click += (s, ev) => { foreach (var k in _categorySelections.Keys.ToList()) _categorySelections[k] = false; RebuildUI(); };
            var safeBtn = new Button { Content = S._("priv.safeOnly"), Padding = new Thickness(12, 4, 12, 4), FontSize = 12 };
            safeBtn.Click += (s, ev) =>
            {
                foreach (var cat in _lastReport.Categories)
                    _categorySelections[cat.Name] = cat.RiskLevel is "Safe" or "Low";
                RebuildUI();
            };
            btnRow.Children.Add(selectAllBtn);
            btnRow.Children.Add(deselectBtn);
            btnRow.Children.Add(safeBtn);
            CategoryList.Children.Add(btnRow);

            // Default: select Safe and Low
            foreach (var cat in _lastReport.Categories.OrderByDescending(c => c.TotalBytes))
                _categorySelections[cat.Name] = cat.RiskLevel is "Safe" or "Low";

            // Show warning if browser data found
            var hasBrowser = _lastReport.Categories.Any(c =>
                c.Name.Contains("Chrome") || c.Name.Contains("Edge") || c.Name.Contains("Firefox"));
            PrivacyWarning.IsOpen = hasBrowser;

            RebuildUI();
            UpdateCleanBtn();
            StatusText.Text = string.Format(S._("priv.found"), _lastReport.TotalSizeDisplay, _lastReport.Categories.Count);
        }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
        finally
        {
            ScanBtn.IsEnabled = true;
            Progress.IsActive = false; Progress.Visibility = Visibility.Collapsed;
        }
    }

    private void RebuildUI()
    {
        while (CategoryList.Children.Count > 1)
            CategoryList.Children.RemoveAt(1);

        if (_lastReport is null) return;

        foreach (var cat in _lastReport.Categories.OrderByDescending(c => c.TotalBytes))
        {
            var isSelected = _categorySelections.ContainsKey(cat.Name) && _categorySelections[cat.Name];

            var riskColor = cat.RiskLevel switch
            {
                "Safe" => Green,
                "Low" => Blue,
                "Medium" => Amber,
                "High" => Red,
                _ => Blue
            };

            // Icon for category type
            var iconGlyph = cat.Name switch
            {
                var n when n.Contains("Chrome") => "\uE774",
                var n when n.Contains("Edge") => "\uE774",
                var n when n.Contains("Firefox") => "\uE774",
                "Recent Documents" => "\uE8A5",
                "Jump Lists" => "\uE71D",
                "Thumbnail Cache" => "\uEB9F",
                "Prefetch Files" => "\uE768",
                "Activity Timeline" => "\uE916",
                "User Temp Files" => "\uE74D",
                "Clipboard History" => "\uE8C8",
                "DNS Cache" => "\uE968",
                _ => "\uE72E"
            };

            var card = new Border
            {
                Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 12, 16, 12),
                BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(0.5)
            };

            var grid = new Grid { ColumnSpacing = 12 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // checkbox
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // icon
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // info
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // risk badge
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // count
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // size

            var check = new CheckBox { IsChecked = isSelected, MinWidth = 0, VerticalAlignment = VerticalAlignment.Center };
            var capturedName = cat.Name;
            check.Checked += (s, ev) => { _categorySelections[capturedName] = true; UpdateCleanBtn(); };
            check.Unchecked += (s, ev) => { _categorySelections[capturedName] = false; UpdateCleanBtn(); };
            Grid.SetColumn(check, 0);
            grid.Children.Add(check);

            var icon = new FontIcon
            {
                Glyph = iconGlyph, FontSize = 18,
                Foreground = new SolidColorBrush(Purple),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(icon, 1);
            grid.Children.Add(icon);

            var info = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(new TextBlock { Text = cat.Name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 14 });
            info.Children.Add(new TextBlock { Text = cat.Description, FontSize = 11, Opacity = 0.55, TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn(info, 2);
            grid.Children.Add(info);

            var badge = new Border
            {
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, riskColor.R, riskColor.G, riskColor.B)),
                CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 3, 8, 3), VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock
            {
                Text = cat.RiskLevel.ToUpper(), FontSize = 10, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(riskColor)
            };
            Grid.SetColumn(badge, 3);
            grid.Children.Add(badge);

            var countText = new TextBlock
            {
                Text = $"{cat.ItemCount:N0} items", FontSize = 12, Opacity = 0.5,
                VerticalAlignment = VerticalAlignment.Center, MinWidth = 65
            };
            Grid.SetColumn(countText, 4);
            grid.Children.Add(countText);

            var sizeText = new TextBlock
            {
                Text = cat.TotalSizeDisplay, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center, MinWidth = 70, TextAlignment = TextAlignment.Right
            };
            Grid.SetColumn(sizeText, 5);
            grid.Children.Add(sizeText);

            card.Child = grid;
            CategoryList.Children.Add(card);
        }

        UpdateCleanBtn();
    }

    private void UpdateCleanBtn()
    {
        var selectedCount = _categorySelections.Count(kv => kv.Value);
        var selectedBytes = _lastReport?.Categories
            .Where(c => _categorySelections.ContainsKey(c.Name) && _categorySelections[c.Name])
            .Sum(c => c.TotalBytes) ?? 0;
        var sizeDisplay = selectedBytes switch
        {
            < 1024 * 1024 => $"{selectedBytes / 1024.0:F0} KB",
            < 1024L * 1024 * 1024 => $"{selectedBytes / (1024.0 * 1024):F1} MB",
            _ => $"{selectedBytes / (1024.0 * 1024 * 1024):F2} GB"
        };
        CleanBtn.Content = $"{S._("priv.cleanBtn")} ({selectedCount}, {sizeDisplay})";
        CleanBtn.IsEnabled = selectedCount > 0;
    }

    private async void CleanBtn_Click(object sender, RoutedEventArgs e)
    {
        var selectedCategories = _categorySelections.Where(kv => kv.Value).Select(kv => kv.Key).ToList();
        if (selectedCategories.Count == 0) return;

        var hasMedium = selectedCategories.Any(c =>
            _lastReport?.Categories.FirstOrDefault(cat => cat.Name == c)?.RiskLevel is "Medium");

        var warningText = hasMedium
            ? S._("priv.warnMedium")
            : S._("priv.warnNormal");

        var totalItems = _lastReport?.Categories
            .Where(c => selectedCategories.Contains(c.Name)).Sum(c => c.ItemCount) ?? 0;

        var dialog = new ContentDialog
        {
            Title = string.Format(S._("priv.confirmTitle"), selectedCategories.Count),
            Content = $"{totalItems:N0} items.\n\n{warningText}",
            PrimaryButtonText = S._("priv.cleanAction"),
            CloseButtonText = S._("priv.cancel"),
            XamlRoot = this.XamlRoot,
            DefaultButton = ContentDialogButton.Close
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        ScanBtn.IsEnabled = false; CleanBtn.IsEnabled = false;
        Progress.IsActive = true; Progress.Visibility = Visibility.Visible;
        StatusText.Text = S._("priv.cleaning");

        try
        {
            if (_module is null) return;
            var plan = new OptimizationPlan("privacy-cleaner", selectedCategories);
            var progress = new Progress<TaskProgress>(p =>
                DispatcherQueue.TryEnqueue(() => StatusText.Text = p.StatusText));
            var result = await _module.OptimizeAsync(plan, progress);

            var freedDisplay = result.BytesFreed switch
            {
                < 1024 * 1024 => $"{result.BytesFreed / 1024.0:F1} KB",
                < 1024L * 1024 * 1024 => $"{result.BytesFreed / (1024.0 * 1024):F1} MB",
                _ => $"{result.BytesFreed / (1024.0 * 1024 * 1024):F2} GB"
            };

            ResultTitle.Text = string.Format(S._("priv.resultTitle"), freedDisplay);
            ResultDetail.Text = string.Format(S._("priv.resultDetail"), result.ItemsProcessed, result.Duration.TotalSeconds);
            ResultCard.Visibility = Visibility.Visible;
            StatusText.Text = S._("priv.done");
            CategoryList.Children.Clear();
            SummaryCard.Visibility = Visibility.Collapsed;
            _lastReport = null;
        }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
        finally
        {
            ScanBtn.IsEnabled = true;
            Progress.IsActive = false; Progress.Visibility = Visibility.Collapsed;
        }
    }

    private void ApplyLocalization()
    {
        try
        {
            PageTitle.Text = S._("priv.title");
            PageSubtitle.Text = S._("priv.subtitle");
            ScanBtn.Content = S._("priv.scanBtn");
            StatusText.Text = S._("priv.scanStatus");
        }
        catch { }
    }
}
