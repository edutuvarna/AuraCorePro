using AuraCore.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.JunkCleaner;
using AuraCore.Module.JunkCleaner.Models;

namespace AuraCore.Desktop.Pages;

public sealed partial class JunkCleanerPage : Page
{
    private JunkCleanerModule? _module;
    private JunkScanReport? _lastReport;
    private readonly Dictionary<string, bool> _categorySelections = new();

    public JunkCleanerPage()
    {
        InitializeComponent();
        ApplyLocalization();
        Services.S.LanguageChanged += () => DispatcherQueue?.TryEnqueue(ApplyLocalization);
        var modules = App.Current.Services.GetServices<IOptimizationModule>();
        _module = modules.FirstOrDefault(m => m.Id == "junk-cleaner") as JunkCleanerModule;
    }

    private async void ScanBtn_Click(object sender, RoutedEventArgs e)
    {
        ScanBtn.IsEnabled = false; CleanBtn.IsEnabled = false;
        Progress.IsActive = true; Progress.Visibility = Visibility.Visible;
        StatusText.Text = "Scanning for junk files...";
        CategoryList.Children.Clear(); ResultCard.Visibility = Visibility.Collapsed;
        _categorySelections.Clear();

        try
        {
            if (_module is null) { StatusText.Text = "Module not available."; return; }
            var result = await _module.ScanAsync(new ScanOptions());
            _lastReport = _module.LastReport;

            if (_lastReport is null || _lastReport.TotalFiles == 0)
            {
                StatusText.Text = "No junk files found — your system is clean!";
                SummaryCard.Visibility = Visibility.Collapsed;
                return;
            }

            TotalSizeText.Text = _lastReport.TotalSizeDisplay;
            TotalFilesText.Text = _lastReport.TotalFiles.ToString("N0");
            TotalCategoriesText.Text = _lastReport.Categories.Count.ToString();
            SummaryCard.Visibility = Visibility.Visible;

            // Select All / Deselect All
            var selectAllRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 4, 0, 4) };
            var selectAllBtn = new Button { Content = "Select All", Padding = new Thickness(12, 4, 12, 4), FontSize = 12 };
            selectAllBtn.Click += (s, ev) => { foreach (var k in _categorySelections.Keys.ToList()) _categorySelections[k] = true; RebuildCategoryUI(); };
            var deselectAllBtn = new Button { Content = "Deselect All", Padding = new Thickness(12, 4, 12, 4), FontSize = 12 };
            deselectAllBtn.Click += (s, ev) => { foreach (var k in _categorySelections.Keys.ToList()) _categorySelections[k] = false; RebuildCategoryUI(); };
            selectAllRow.Children.Add(selectAllBtn);
            selectAllRow.Children.Add(deselectAllBtn);
            CategoryList.Children.Add(selectAllRow);

            // Category cards
            foreach (var cat in _lastReport.Categories.OrderByDescending(c => c.TotalBytes))
            {
                _categorySelections[cat.Name] = true; // default selected
            }
            RebuildCategoryUI();

            UpdateCleanBtn();
            StatusText.Text = $"Found {_lastReport.TotalSizeDisplay} of junk in {_lastReport.Categories.Count} categories";
        }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
        finally
        {
            ScanBtn.IsEnabled = true;
            Progress.IsActive = false; Progress.Visibility = Visibility.Collapsed;
        }
    }

    private void RebuildCategoryUI()
    {
        // Remove all category cards but keep the Select All row
        while (CategoryList.Children.Count > 1)
            CategoryList.Children.RemoveAt(1);

        if (_lastReport is null) return;

        foreach (var cat in _lastReport.Categories.OrderByDescending(c => c.TotalBytes))
        {
            var isSelected = _categorySelections.ContainsKey(cat.Name) && _categorySelections[cat.Name];

            var risk = GetCategoryRisk(cat.Name);
            var riskColor = risk switch
            {
                "Safe" => Windows.UI.Color.FromArgb(255, 46, 125, 50),
                "Low" => Windows.UI.Color.FromArgb(255, 33, 150, 243),
                "Medium" => Windows.UI.Color.FromArgb(255, 230, 81, 0),
                "High" => Windows.UI.Color.FromArgb(255, 198, 40, 40),
                _ => Windows.UI.Color.FromArgb(255, 158, 158, 158)
            };

            var card = new Border
            {
                Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 12, 16, 12),
                BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(0.5)
            };

            var grid = new Grid { ColumnSpacing = 12 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Checkbox
            var check = new CheckBox { IsChecked = isSelected, MinWidth = 0, VerticalAlignment = VerticalAlignment.Center };
            var capturedName = cat.Name;
            check.Checked += (s, ev) => { _categorySelections[capturedName] = true; UpdateCleanBtn(); };
            check.Unchecked += (s, ev) => { _categorySelections[capturedName] = false; UpdateCleanBtn(); };
            Grid.SetColumn(check, 0);
            grid.Children.Add(check);

            // Info
            var info = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(new TextBlock { Text = cat.Name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 14 });
            info.Children.Add(new TextBlock { Text = cat.Description, FontSize = 11, Opacity = 0.6 });
            // Sample files preview
            var sampleFiles = string.Join(", ", cat.Files.Take(3).Select(f => Path.GetFileName(f.FullPath)));
            if (cat.FileCount > 3) sampleFiles += $" +{cat.FileCount - 3} more";
            info.Children.Add(new TextBlock { Text = sampleFiles, FontSize = 10, Opacity = 0.35, FontFamily = new FontFamily("Consolas") });
            Grid.SetColumn(info, 1);
            grid.Children.Add(info);

            // Risk badge
            var badge = new Border
            {
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, riskColor.R, riskColor.G, riskColor.B)),
                CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 3, 8, 3), VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock
            {
                Text = risk.ToUpper(), FontSize = 10, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(riskColor)
            };
            Grid.SetColumn(badge, 2);
            grid.Children.Add(badge);

            // File count
            var countText = new TextBlock
            {
                Text = $"{cat.FileCount:N0} files", FontSize = 12, Opacity = 0.5,
                VerticalAlignment = VerticalAlignment.Center, MinWidth = 60
            };
            Grid.SetColumn(countText, 3);
            grid.Children.Add(countText);

            // Size
            var sizeText = new TextBlock
            {
                Text = cat.TotalSizeDisplay, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center, MinWidth = 70, TextAlignment = TextAlignment.Right
            };
            Grid.SetColumn(sizeText, 4);
            grid.Children.Add(sizeText);

            // Size bar
            var maxBytes = _lastReport.Categories.Max(c => c.TotalBytes);
            var pct = maxBytes > 0 ? (double)cat.TotalBytes / maxBytes * 100 : 0;
            var barRow = new StackPanel { Margin = new Thickness(44, 2, 0, 0) };
            barRow.Children.Add(new ProgressBar { Minimum = 0, Maximum = 100, Value = pct, Height = 3, CornerRadius = new CornerRadius(1.5) });

            var cardStack = new StackPanel { Spacing = 4 };
            cardStack.Children.Add(grid);
            cardStack.Children.Add(barRow);
            card.Child = cardStack;

            CategoryList.Children.Add(card);
        }
    }

    private void UpdateCleanBtn()
    {
        var selectedCount = _categorySelections.Count(kv => kv.Value);
        var selectedBytes = _lastReport?.Categories.Where(c => _categorySelections.ContainsKey(c.Name) && _categorySelections[c.Name]).Sum(c => c.TotalBytes) ?? 0;
        var sizeDisplay = selectedBytes switch
        {
            < 1024 * 1024 => $"{selectedBytes / 1024.0:F0} KB",
            < 1024L * 1024 * 1024 => $"{selectedBytes / (1024.0 * 1024):F1} MB",
            _ => $"{selectedBytes / (1024.0 * 1024 * 1024):F2} GB"
        };
        CleanBtn.Content = $"Clean Selected ({selectedCount} categories, {sizeDisplay})";
        CleanBtn.IsEnabled = selectedCount > 0;
    }

    private async void CleanBtn_Click(object sender, RoutedEventArgs e)
    {
        var selectedCategories = _categorySelections.Where(kv => kv.Value).Select(kv => kv.Key).ToList();
        if (selectedCategories.Count == 0) return;

        var highRiskSelected = selectedCategories.Any(c => GetCategoryRisk(c) == "High");
        var warningText = highRiskSelected
            ? "WARNING: You selected high-risk categories (e.g., Recycle Bin). These files cannot be recovered after deletion."
            : "Selected files will be permanently deleted.";

        var totalFiles = _lastReport?.Categories.Where(c => selectedCategories.Contains(c.Name)).Sum(c => c.FileCount) ?? 0;

        var dialog = new ContentDialog
        {
            Title = $"Clean {selectedCategories.Count} categories?",
            Content = $"This will delete {totalFiles:N0} files.\n\n{warningText}",
            PrimaryButtonText = "Clean",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot,
            DefaultButton = ContentDialogButton.Close
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        ScanBtn.IsEnabled = false; CleanBtn.IsEnabled = false;
        Progress.IsActive = true; Progress.Visibility = Visibility.Visible;
        StatusText.Text = "Cleaning...";

        try
        {
            if (_module is null) return;
            var plan = new OptimizationPlan("junk-cleaner", new List<string>());
            var progress = new Progress<TaskProgress>(p => DispatcherQueue.TryEnqueue(() => StatusText.Text = p.StatusText));
            var result = await _module.OptimizeAsync(plan, progress);

            var freedDisplay = result.BytesFreed switch
            {
                < 1024 * 1024 => $"{result.BytesFreed / 1024.0:F1} KB",
                < 1024L * 1024 * 1024 => $"{result.BytesFreed / (1024.0 * 1024):F1} MB",
                _ => $"{result.BytesFreed / (1024.0 * 1024 * 1024):F2} GB"
            };

            ResultTitle.Text = $"Cleaned {freedDisplay}";
            ResultDetail.Text = $"Deleted {result.ItemsProcessed:N0} files in {result.Duration.TotalSeconds:F1}s";
            ResultCard.Visibility = Visibility.Visible;
            StatusText.Text = "Done!";
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

    private static string GetCategoryRisk(string name) => name switch
    {
        "Windows Temp" or "Chrome Cache" or "Edge Cache" or "Recent Shortcuts" => "Safe",
        "Prefetch Cache" or "Thumbnail Cache" => "Low",
        "Windows Logs" => "Medium",
        "Recycle Bin" => "High",
        _ => "Safe"
    };

    private void ApplyLocalization()
    {
        if (FindName("PageTitle") is Microsoft.UI.Xaml.Controls.TextBlock title)
            title.Text = S._("junk.title");
        if (FindName("PageSubtitle") is Microsoft.UI.Xaml.Controls.TextBlock subtitle)
            subtitle.Text = S._("junk.subtitle");
    }
}
