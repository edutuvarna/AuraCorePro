using AuraCore.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.DiskCleanup;
using AuraCore.Module.DiskCleanup.Models;
using System.Security.Principal;

namespace AuraCore.Desktop.Pages;

public sealed partial class DiskCleanupPage : Page
{
    private DiskCleanupModule? _module;
    private CleanupScanReport? _lastReport;
    private readonly Dictionary<string, bool> _categorySelections = new();
    private static bool IsAdmin => new WindowsPrincipal(WindowsIdentity.GetCurrent())
        .IsInRole(WindowsBuiltInRole.Administrator);

    public DiskCleanupPage()
    {
        InitializeComponent();
        var modules = App.Current.Services.GetServices<IOptimizationModule>();
        _module = modules.FirstOrDefault(m => m.Id == "disk-cleanup") as DiskCleanupModule;

        if (!IsAdmin)
            AdminWarning.IsOpen = true;

        ApplyDcLocalization();
        Services.S.LanguageChanged += () => DispatcherQueue?.TryEnqueue(ApplyDcLocalization);
    }

    private async void ScanBtn_Click(object sender, RoutedEventArgs e)
    {
        ScanBtn.IsEnabled = false; CleanBtn.IsEnabled = false;
        Progress.IsActive = true; Progress.Visibility = Visibility.Visible;
        StatusText.Text = "Deep scanning system...";
        CategoryList.Children.Clear(); ResultCard.Visibility = Visibility.Collapsed;
        _categorySelections.Clear();

        try
        {
            if (_module is null) { StatusText.Text = "Module not available."; return; }
            await _module.ScanAsync(new ScanOptions());
            _lastReport = _module.LastReport;

            if (_lastReport is null || _lastReport.TotalFiles == 0)
            {
                StatusText.Text = "No reclaimable space found - your system is already clean!";
                SummaryCard.Visibility = Visibility.Collapsed;
                return;
            }

            TotalSizeText.Text = _lastReport.TotalSizeDisplay;
            TotalFilesText.Text = _lastReport.TotalFiles.ToString("N0");
            TotalCategoriesText.Text = _lastReport.Categories.Count.ToString();
            AdminCountText.Text = _lastReport.Categories.Count(c => c.RequiresAdmin).ToString();
            SummaryCard.Visibility = Visibility.Visible;

            // Select All / Deselect All
            var selectAllRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 4, 0, 4) };
            var selectAllBtn = new Button { Content = "Select All", Padding = new Thickness(12, 4, 12, 4), FontSize = 12 };
            selectAllBtn.Click += (s, ev) => { foreach (var k in _categorySelections.Keys.ToList()) _categorySelections[k] = true; RebuildUI(); };
            var deselectAllBtn = new Button { Content = "Deselect All", Padding = new Thickness(12, 4, 12, 4), FontSize = 12 };
            deselectAllBtn.Click += (s, ev) => { foreach (var k in _categorySelections.Keys.ToList()) _categorySelections[k] = false; RebuildUI(); };
            var safeOnlyBtn = new Button { Content = "Safe Only", Padding = new Thickness(12, 4, 12, 4), FontSize = 12 };
            safeOnlyBtn.Click += (s, ev) =>
            {
                foreach (var cat in _lastReport.Categories)
                    _categorySelections[cat.Name] = cat.RiskLevel is "Safe" or "Low";
                RebuildUI();
            };
            selectAllRow.Children.Add(selectAllBtn);
            selectAllRow.Children.Add(deselectAllBtn);
            selectAllRow.Children.Add(safeOnlyBtn);
            CategoryList.Children.Add(selectAllRow);

            foreach (var cat in _lastReport.Categories.OrderByDescending(c => c.TotalBytes))
                _categorySelections[cat.Name] = cat.RiskLevel is "Safe" or "Low";

            RebuildUI();
            UpdateCleanBtn();
            StatusText.Text = $"Found {_lastReport.TotalSizeDisplay} reclaimable in {_lastReport.Categories.Count} categories";
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

            // Dim card if admin required but not running as admin
            if (cat.RequiresAdmin && !IsAdmin)
                card.Opacity = 0.5;

            var grid = new Grid { ColumnSpacing = 12 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var isDisabled = cat.RequiresAdmin && !IsAdmin;

            var check = new CheckBox
            {
                IsChecked = isSelected && !isDisabled,
                IsEnabled = !isDisabled,
                MinWidth = 0,
                VerticalAlignment = VerticalAlignment.Center
            };
            var capturedName = cat.Name;
            check.Checked += (s, ev) => { _categorySelections[capturedName] = true; UpdateCleanBtn(); };
            check.Unchecked += (s, ev) => { _categorySelections[capturedName] = false; UpdateCleanBtn(); };
            Grid.SetColumn(check, 0);
            grid.Children.Add(check);

            var info = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            titleRow.Children.Add(new TextBlock { Text = cat.Name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 14 });
            if (cat.RequiresAdmin)
            {
                var adminBadge = new Border
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, 198, 40, 40)),
                    CornerRadius = new CornerRadius(3), Padding = new Thickness(6, 1, 6, 1),
                    VerticalAlignment = VerticalAlignment.Center
                };
                adminBadge.Child = new TextBlock
                {
                    Text = "ADMIN", FontSize = 9, FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 198, 40, 40))
                };
                titleRow.Children.Add(adminBadge);
            }
            info.Children.Add(titleRow);
            info.Children.Add(new TextBlock { Text = cat.Description, FontSize = 11, Opacity = 0.6 });

            var sampleFiles = string.Join(", ", cat.Files.Take(3).Select(f => System.IO.Path.GetFileName(f.FullPath)));
            if (cat.FileCount > 3) sampleFiles += $" +{cat.FileCount - 3} more";
            info.Children.Add(new TextBlock { Text = sampleFiles, FontSize = 10, Opacity = 0.35, FontFamily = new FontFamily("Consolas") });
            Grid.SetColumn(info, 1);
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
            Grid.SetColumn(badge, 2);
            grid.Children.Add(badge);

            var countText = new TextBlock
            {
                Text = $"{cat.FileCount:N0} files", FontSize = 12, Opacity = 0.5,
                VerticalAlignment = VerticalAlignment.Center, MinWidth = 60
            };
            Grid.SetColumn(countText, 3);
            grid.Children.Add(countText);

            var sizeText = new TextBlock
            {
                Text = cat.TotalSizeDisplay, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center, MinWidth = 70, TextAlignment = TextAlignment.Right
            };
            Grid.SetColumn(sizeText, 4);
            grid.Children.Add(sizeText);

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
        CleanBtn.Content = $"Clean Selected ({selectedCount}, {sizeDisplay})";
        CleanBtn.IsEnabled = selectedCount > 0;
    }

    private async void CleanBtn_Click(object sender, RoutedEventArgs e)
    {
        var selectedCategories = _categorySelections.Where(kv => kv.Value).Select(kv => kv.Key).ToList();
        if (selectedCategories.Count == 0) return;

        var highRisk = selectedCategories.Any(c =>
            _lastReport?.Categories.FirstOrDefault(cat => cat.Name == c)?.RiskLevel is "High" or "Medium");

        var warningText = highRisk
            ? "WARNING: You selected medium/high-risk categories. Some of these files may affect system rollback capabilities."
            : "Selected files will be permanently deleted.";

        var totalFiles = _lastReport?.Categories
            .Where(c => selectedCategories.Contains(c.Name)).Sum(c => c.FileCount) ?? 0;

        var dialog = new ContentDialog
        {
            Title = $"Deep Clean {selectedCategories.Count} categories?",
            Content = $"This will delete {totalFiles:N0} files.\n\n{warningText}",
            PrimaryButtonText = "Clean",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot,
            DefaultButton = ContentDialogButton.Close
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        ScanBtn.IsEnabled = false; CleanBtn.IsEnabled = false;
        Progress.IsActive = true; Progress.Visibility = Visibility.Visible;
        StatusText.Text = "Deep cleaning...";

        try
        {
            if (_module is null) return;
            var plan = new OptimizationPlan("disk-cleanup", selectedCategories);
            var progress = new Progress<TaskProgress>(p =>
                DispatcherQueue.TryEnqueue(() => StatusText.Text = p.StatusText));
            var result = await _module.OptimizeAsync(plan, progress);

            var freedDisplay = result.BytesFreed switch
            {
                < 1024 * 1024 => $"{result.BytesFreed / 1024.0:F1} KB",
                < 1024L * 1024 * 1024 => $"{result.BytesFreed / (1024.0 * 1024):F1} MB",
                _ => $"{result.BytesFreed / (1024.0 * 1024 * 1024):F2} GB"
            };

            ResultTitle.Text = $"Reclaimed {freedDisplay}";
            ResultDetail.Text = $"Deleted {result.ItemsProcessed:N0} files in {result.Duration.TotalSeconds:F1}s";
            ResultCard.Visibility = Visibility.Visible;
            StatusText.Text = "Deep clean complete!";
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

    private void ApplyDcLocalization()
    {
        try
        {
            PageTitle.Text = S._("dc.title");
            PageSubtitle.Text = S._("dc.subtitle");
            AdminWarning.Title = S._("dc.adminWarningTitle");
            AdminWarning.Message = S._("dc.adminWarningMsg");
            ScanBtn.Content = S._("dc.scanBtn");
            StatusText.Text = S._("dc.scanStatus");
        }
        catch { }
    }
}
