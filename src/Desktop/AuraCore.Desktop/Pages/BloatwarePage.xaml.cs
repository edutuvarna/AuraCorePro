using AuraCore.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.BloatwareRemoval;
using AuraCore.Module.BloatwareRemoval.Models;

namespace AuraCore.Desktop.Pages;

public sealed partial class BloatwarePage : Page
{
    private BloatwareRemovalModule? _module;
    private BloatwareScanReport? _report;
    private readonly Dictionary<string, bool> _selectedApps = new();
    private string _currentFilter = "all";

    public BloatwarePage()
    {
        InitializeComponent();
        ApplyLocalization();
        Services.S.LanguageChanged += () => DispatcherQueue?.TryEnqueue(ApplyLocalization);
        var modules = App.Current.Services.GetServices<IOptimizationModule>();
        _module = modules.FirstOrDefault(m => m.Id == "bloatware-removal") as BloatwareRemovalModule;
    }

    private async void ScanBtn_Click(object sender, RoutedEventArgs e)
    {
        ScanBtn.IsEnabled = false;
        RemoveBtn.IsEnabled = false;
        Progress.IsActive = true;
        Progress.Visibility = Visibility.Visible;
        StatusText.Text = "Scanning installed apps... (this may take a moment)";
        AppList.Children.Clear();
        ResultCard.Visibility = Visibility.Collapsed;
        _selectedApps.Clear();

        try
        {
            if (_module is null) { StatusText.Text = "Module not available."; return; }

            await _module.ScanAsync(new ScanOptions());
            _report = _module.LastReport;
            if (_report is null || _report.TotalApps == 0)
            {
                StatusText.Text = "No apps found.";
                return;
            }

            TotalAppsText.Text = _report.TotalApps.ToString();
            RemovableText.Text = _report.RemovableApps.ToString();
            SafeCountText.Text = _report.Apps.Count(a => a.Risk == BloatRisk.Safe).ToString();
            SystemCountText.Text = _report.Apps.Count(a => a.Risk == BloatRisk.System || a.IsFramework).ToString();
            SummaryCard.Visibility = Visibility.Visible;
            FilterBar.Visibility = Visibility.Visible;
            SearchBox.Visibility = Visibility.Visible;

            RenderAppList("all");
            StatusText.Text = $"Found {_report.TotalApps} apps — {_report.RemovableApps} removable";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            ScanBtn.IsEnabled = true;
            Progress.IsActive = false;
            Progress.Visibility = Visibility.Collapsed;
        }
    }

    private void Filter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string filter)
        {
            _currentFilter = filter;
            RenderAppList(filter);
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RenderAppList(_currentFilter);
    }

    private void RenderAppList(string filter)
    {
        if (_report is null) return;
        AppList.Children.Clear();

        var filtered = filter switch
        {
            "safe" => _report.Apps.Where(a => a.Risk == BloatRisk.Safe),
            "caution" => _report.Apps.Where(a => a.Risk == BloatRisk.Caution),
            "oem" => _report.Apps.Where(a => a.Category == BloatCategory.OemBloat),
            _ => _report.Apps.AsEnumerable()
        };

        // Skip frameworks
        filtered = filtered.Where(a => !a.IsFramework);

        // Apply search filter
        var search = SearchBox.Text.Trim();
        if (!string.IsNullOrEmpty(search))
            filtered = filtered.Where(a =>
                a.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                a.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                a.Publisher.Contains(search, StringComparison.OrdinalIgnoreCase));

        foreach (var app in filtered)
        {
            var isSystemLocked = app.Risk == BloatRisk.System;

            var card = new Border
            {
                Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(16, 12, 16, 12),
                BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(0.5),
                Opacity = isSystemLocked ? 0.5 : 1.0
            };

            var grid = new Grid { ColumnSpacing = 12 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });         // Checkbox
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Name+info
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });         // Risk badge
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });         // Community score
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });         // Size

            // Checkbox
            var check = new CheckBox
            {
                IsEnabled = !isSystemLocked,
                Tag = app.PackageFullName,
                IsChecked = _selectedApps.ContainsKey(app.PackageFullName) && _selectedApps[app.PackageFullName],
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 0
            };
            check.Checked += (s, e) => { _selectedApps[app.PackageFullName] = true; UpdateRemoveButton(); };
            check.Unchecked += (s, e) => { _selectedApps[app.PackageFullName] = false; UpdateRemoveButton(); };
            Grid.SetColumn(check, 0);
            grid.Children.Add(check);

            // Name + publisher + reason
            var infoStack = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            infoStack.Children.Add(new TextBlock
            {
                Text = app.DisplayName,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 13
            });
            infoStack.Children.Add(new TextBlock
            {
                Text = app.RiskReason,
                FontSize = 11,
                Opacity = 0.6
            });
            Grid.SetColumn(infoStack, 1);
            grid.Children.Add(infoStack);

            // Risk badge
            var (badgeText, badgeColor) = app.Risk switch
            {
                BloatRisk.Safe => ("SAFE", Windows.UI.Color.FromArgb(255, 46, 125, 50)),
                BloatRisk.Caution => ("CAUTION", Windows.UI.Color.FromArgb(255, 230, 81, 0)),
                BloatRisk.Warning => ("WARNING", Windows.UI.Color.FromArgb(255, 198, 40, 40)),
                BloatRisk.System => ("SYSTEM", Windows.UI.Color.FromArgb(255, 96, 125, 139)),
                _ => ("UNKNOWN", Windows.UI.Color.FromArgb(255, 128, 128, 128))
            };

            var badge = new Border
            {
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, badgeColor.R, badgeColor.G, badgeColor.B)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 3, 8, 3),
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock
            {
                Text = badgeText,
                FontSize = 10,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(badgeColor)
            };
            Grid.SetColumn(badge, 2);
            grid.Children.Add(badge);

            // Community score
            if (app.CommunityScore > 0)
            {
                var scoreColor = app.CommunityScore switch
                {
                    >= 85 => Windows.UI.Color.FromArgb(255, 198, 40, 40),
                    >= 65 => Windows.UI.Color.FromArgb(255, 230, 81, 0),
                    >= 40 => Windows.UI.Color.FromArgb(255, 33, 150, 243),
                    _ => Windows.UI.Color.FromArgb(255, 46, 125, 50)
                };
                var scorePanel = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center, MinWidth = 70 };
                var scoreRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
                scoreRow.Children.Add(new TextBlock { Text = $"{app.CommunityScore}%", FontSize = 11,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(scoreColor) });
                scoreRow.Children.Add(new FontIcon { Glyph = "\uE734", FontSize = 10,
                    Foreground = new SolidColorBrush(scoreColor), VerticalAlignment = VerticalAlignment.Center }); // thumbs up
                scorePanel.Children.Add(scoreRow);
                scorePanel.Children.Add(new TextBlock { Text = $"{app.CommunityVotes:N0} votes", FontSize = 9, Opacity = 0.4 });
                Grid.SetColumn(scorePanel, 3);
                grid.Children.Add(scorePanel);
            }

            // Size
            var sizeText = new TextBlock
            {
                Text = app.SizeDisplay,
                FontSize = 12,
                Opacity = 0.6,
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 60,
                TextAlignment = TextAlignment.Right
            };
            Grid.SetColumn(sizeText, 4);
            grid.Children.Add(sizeText);

            card.Child = grid;
            AppList.Children.Add(card);
        }
    }

    private void UpdateRemoveButton()
    {
        var count = _selectedApps.Count(kv => kv.Value);
        RemoveBtn.Content = $"Remove Selected ({count})";
        RemoveBtn.IsEnabled = count > 0;
    }

    private void PresetFresh_Click(object sender, RoutedEventArgs e)
    {
        if (_report is null) return;

        // Pre-select known bloatware that 90%+ of users don't need
        var freshCleanupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Microsoft.549981C3F5F10",           // Cortana
            "Microsoft.BingFinance", "Microsoft.BingNews", "Microsoft.BingSports",
            "Microsoft.BingWeather", "Microsoft.BingTranslator", "Microsoft.BingTravel",
            "Microsoft.BingFoodAndDrink", "Microsoft.BingHealthAndFitness",
            "Microsoft.3DBuilder", "Microsoft.Microsoft3DViewer", "Microsoft.Print3D",
            "Microsoft.MixedReality.Portal",
            "Microsoft.GetHelp", "Microsoft.Getstarted",
            "Microsoft.People", "Microsoft.Messaging",
            "Microsoft.MicrosoftSolitaireCollection",
            "Microsoft.ZuneMusic", "Microsoft.ZuneVideo",
            "Microsoft.WindowsFeedbackHub", "Microsoft.WindowsMaps",
            "Microsoft.YourPhone", "Microsoft.OneConnect",
            "Microsoft.SkypeApp", "Microsoft.Office.OneNote",
            "Clipchamp.Clipchamp", "Microsoft.PowerAutomateDesktop",
            "MicrosoftCorporationII.QuickAssist",
            "Microsoft.MicrosoftOfficeHub",
            "Microsoft.Todos", "Microsoft.Wallet",
            "Microsoft.OutlookForWindows",
            "Microsoft.Windows.DevHome",
            "Microsoft.Teams.Free", "MSTeams",
        };

        foreach (var app in _report.Apps)
        {
            if (app.IsFramework || app.Risk == BloatRisk.System) continue;

            var baseName = app.Name.Split('_')[0];
            var isPresetTarget = freshCleanupIds.Contains(baseName)
                || app.Category == BloatCategory.OemBloat;

            _selectedApps[app.PackageFullName] = isPresetTarget;
        }

        RenderAppList(_currentFilter);
        UpdateRemoveButton();
        StatusText.Text = $"Fresh Windows Cleanup preset applied — {_selectedApps.Count(kv => kv.Value)} apps selected";
    }

    private void SelectAllSafe_Click(object sender, RoutedEventArgs e)
    {
        if (_report is null) return;
        foreach (var app in _report.Apps)
        {
            if (app.IsFramework || app.Risk == BloatRisk.System) continue;
            _selectedApps[app.PackageFullName] = app.Risk == BloatRisk.Safe;
        }
        RenderAppList(_currentFilter);
        UpdateRemoveButton();
        StatusText.Text = $"Selected all Safe apps — {_selectedApps.Count(kv => kv.Value)} selected";
    }

    private void DeselectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var key in _selectedApps.Keys.ToList())
            _selectedApps[key] = false;
        RenderAppList(_currentFilter);
        UpdateRemoveButton();
    }

    private async void RemoveBtn_Click(object sender, RoutedEventArgs e)
    {
        var selected = _selectedApps.Where(kv => kv.Value).Select(kv => kv.Key).ToList();
        if (selected.Count == 0) return;

        // Get app names for the dialog
        var appNames = _report?.Apps
            .Where(a => selected.Contains(a.PackageFullName))
            .Select(a => a.DisplayName)
            .Take(5)
            .ToList() ?? new();

        var listText = string.Join("\n", appNames);
        if (selected.Count > 5) listText += $"\n...and {selected.Count - 5} more";

        var dialog = new ContentDialog
        {
            Title = $"Remove {selected.Count} app(s)?",
            Content = $"The following apps will be uninstalled:\n\n{listText}\n\nThis cannot be easily undone.",
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot,
            DefaultButton = ContentDialogButton.Close
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        ScanBtn.IsEnabled = false;
        RemoveBtn.IsEnabled = false;
        Progress.IsActive = true;
        Progress.Visibility = Visibility.Visible;

        try
        {
            if (_module is null) return;

            var plan = new OptimizationPlan("bloatware-removal", selected);
            var progress = new Progress<TaskProgress>(p =>
            {
                DispatcherQueue.TryEnqueue(() => StatusText.Text = p.StatusText);
            });

            var result = await _module.OptimizeAsync(plan, progress);

            ResultTitle.Text = $"Removed {result.ItemsProcessed} app(s)";
            ResultDetail.Text = $"Completed in {result.Duration.TotalSeconds:F1} seconds";
            ResultCard.Visibility = Visibility.Visible;
            StatusText.Text = "Removal complete!";

            // Clear list
            AppList.Children.Clear();
            _selectedApps.Clear();
            SummaryCard.Visibility = Visibility.Collapsed;
            FilterBar.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            ScanBtn.IsEnabled = true;
            RemoveBtn.IsEnabled = false;
            Progress.IsActive = false;
            Progress.Visibility = Visibility.Collapsed;
        }
    }

    private void ApplyLocalization()
    {
        if (FindName("PageTitle") is Microsoft.UI.Xaml.Controls.TextBlock title)
            title.Text = S._("bloat.title");
        if (FindName("PageSubtitle") is Microsoft.UI.Xaml.Controls.TextBlock subtitle)
            subtitle.Text = S._("bloat.subtitle");
    }
}
