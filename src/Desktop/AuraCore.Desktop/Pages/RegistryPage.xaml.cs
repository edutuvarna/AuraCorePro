using AuraCore.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.RegistryOptimizer;
using AuraCore.Module.RegistryOptimizer.Models;

namespace AuraCore.Desktop.Pages;

public sealed partial class RegistryPage : Page
{
    private RegistryOptimizerModule? _module;
    private readonly Dictionary<string, bool> _selections = new();

    public RegistryPage()
    {
        InitializeComponent();
        ApplyLocalization();
        Services.S.LanguageChanged += () => DispatcherQueue?.TryEnqueue(ApplyLocalization);
        var modules = App.Current.Services.GetServices<IOptimizationModule>();
        _module = modules.FirstOrDefault(m => m.Id == "registry-optimizer") as RegistryOptimizerModule;
    }

    private async void ScanBtn_Click(object sender, RoutedEventArgs e)
    {
        ScanBtn.IsEnabled = false; FixBtn.IsEnabled = false;
        Progress.IsActive = true; Progress.Visibility = Visibility.Visible;
        StatusText.Text = "Scanning registry...";
        IssueList.Children.Clear(); ResultCard.Visibility = Visibility.Collapsed;
        _selections.Clear();

        try
        {
            if (_module is null) { StatusText.Text = "Module not available."; return; }

            await _module.ScanAsync(new ScanOptions());
            var report = _module.LastReport;
            if (report is null) { StatusText.Text = "No data."; return; }

            if (report.TotalIssues == 0)
            {
                StatusText.Text = "No registry issues found — your registry is clean!";
                SummaryCard.Visibility = Visibility.Collapsed;
                return;
            }

            // Summary
            TotalText.Text = report.TotalIssues.ToString();
            SafeText.Text = report.SafeIssues.ToString();
            CautionText.Text = report.CautionIssues.ToString();
            SummaryCard.Visibility = Visibility.Visible;

            // Group by category
            var grouped = report.Issues.GroupBy(i => i.Category).OrderByDescending(g => g.Count());

            foreach (var group in grouped)
            {
                var section = new StackPanel { Spacing = 4 };

                // Category header with count
                var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };
                headerRow.Children.Add(new TextBlock
                {
                    Text = $"{group.Key} ({group.Count()})",
                    Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["BodyStrongTextBlockStyle"]
                });

                // Select all for this category
                var selectAll = new CheckBox
                {
                    Content = "Select all",
                    FontSize = 12,
                    MinWidth = 0,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var groupIds = group.Where(i => i.Risk == "Safe").Select(i => i.Id).ToList();
                selectAll.Checked += (s, ev) => { foreach (var id in groupIds) _selections[id] = true; UpdateFixBtn(); };
                selectAll.Unchecked += (s, ev) => { foreach (var id in groupIds) _selections[id] = false; UpdateFixBtn(); };
                headerRow.Children.Add(selectAll);

                section.Children.Add(headerRow);

                foreach (var issue in group)
                {
                    var isCautionLocked = issue.Risk == "Caution";
                    _selections[issue.Id] = false;

                    var card = new Border
                    {
                        Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(14, 8, 14, 8),
                        BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],
                        BorderThickness = new Thickness(0.5)
                    };

                    var grid = new Grid { ColumnSpacing = 10 };
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var check = new CheckBox
                    {
                        MinWidth = 0,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    var capturedId = issue.Id;
                    check.Checked += (s, ev) => { _selections[capturedId] = true; UpdateFixBtn(); };
                    check.Unchecked += (s, ev) => { _selections[capturedId] = false; UpdateFixBtn(); };
                    Grid.SetColumn(check, 0);
                    grid.Children.Add(check);

                    var info = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
                    info.Children.Add(new TextBlock
                    {
                        Text = issue.Description,
                        FontSize = 13,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                    });
                    info.Children.Add(new TextBlock
                    {
                        Text = issue.Detail,
                        FontSize = 11, Opacity = 0.5, TextWrapping = TextWrapping.Wrap
                    });
                    info.Children.Add(new TextBlock
                    {
                        Text = issue.KeyPath,
                        FontSize = 10, Opacity = 0.3, FontFamily = new FontFamily("Consolas")
                    });
                    Grid.SetColumn(info, 1);
                    grid.Children.Add(info);

                    // Risk badge
                    var riskColor = issue.Risk switch
                    {
                        "Safe" => Windows.UI.Color.FromArgb(255, 46, 125, 50),
                        "Caution" => Windows.UI.Color.FromArgb(255, 230, 81, 0),
                        _ => Windows.UI.Color.FromArgb(255, 158, 158, 158)
                    };
                    var badge = new Border
                    {
                        Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, riskColor.R, riskColor.G, riskColor.B)),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(6, 3, 6, 3),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    badge.Child = new TextBlock
                    {
                        Text = issue.Risk.ToUpper(),
                        FontSize = 9,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(riskColor)
                    };
                    Grid.SetColumn(badge, 2);
                    grid.Children.Add(badge);

                    card.Child = grid;
                    section.Children.Add(card);
                }

                IssueList.Children.Add(section);
            }

            StatusText.Text = $"Found {report.TotalIssues} issues — {report.SafeIssues} safe to fix";
            FixAllSafeBtn.IsEnabled = report.SafeIssues > 0;
            LoadBackupHistory();
        }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
        finally
        {
            ScanBtn.IsEnabled = true;
            Progress.IsActive = false; Progress.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateFixBtn()
    {
        var count = _selections.Count(kv => kv.Value);
        FixBtn.Content = $"Fix Selected ({count})";
        FixBtn.IsEnabled = count > 0;
    }

    private async void FixBtn_Click(object sender, RoutedEventArgs e)
    {
        var selected = _selections.Where(kv => kv.Value).Select(kv => kv.Key).ToList();
        if (selected.Count == 0) return;

        var dialog = new ContentDialog
        {
            Title = $"Fix {selected.Count} registry issue(s)?",
            Content = "A full backup of your user registry will be created before any changes.\nThis backup can be restored at any time.\n\nNote: HKLM entries (Caution items) require running as Administrator.",
            PrimaryButtonText = "Create Backup & Fix",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot,
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        ScanBtn.IsEnabled = false; FixBtn.IsEnabled = false;
        Progress.IsActive = true; Progress.Visibility = Visibility.Visible;
        StatusText.Text = "Creating backup...";

        try
        {
            if (_module is null) return;

            var plan = new OptimizationPlan("registry-optimizer", selected);
            var progress = new Progress<TaskProgress>(p =>
            {
                DispatcherQueue.TryEnqueue(() => StatusText.Text = p.StatusText);
            });

            var result = await _module.OptimizeAsync(plan, progress);

            var backupPath = _module.LastReport?.BackupPath ?? "unknown";
            ResultTitle.Text = $"Fixed {result.ItemsProcessed} of {selected.Count} issue(s)";
            ResultDetail.Text = $"Backup saved to: {backupPath}\nCompleted in {result.Duration.TotalSeconds:F1}s. Use 'Restore Backup' if anything seems wrong.";
            ResultCard.Visibility = Visibility.Visible;
            RestoreBtn.Visibility = Visibility.Visible;
            LoadBackupHistory();
            StatusText.Text = "Registry optimization complete!";
        }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
        finally
        {
            ScanBtn.IsEnabled = true;
            Progress.IsActive = false; Progress.Visibility = Visibility.Collapsed;
        }
    }

    private async void RestoreBtn_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Restore registry backup?",
            Content = "This will import the backup created before the last optimization.\nYour registry will be restored to its previous state.",
            PrimaryButtonText = "Restore",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot,
            DefaultButton = ContentDialogButton.Close
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        StatusText.Text = "Restoring backup...";
        Progress.IsActive = true; Progress.Visibility = Visibility.Visible;

        try
        {
            if (_module is null) return;
            await _module.RollbackAsync("", default);
            ResultTitle.Text = "Backup restored successfully";
            ResultDetail.Text = "Your registry has been restored to its previous state.";
            StatusText.Text = "Restore complete!";
        }
        catch (Exception ex) { StatusText.Text = $"Restore error: {ex.Message}"; }
        finally { Progress.IsActive = false; Progress.Visibility = Visibility.Collapsed; }
    }

    private void FixAllSafe_Click(object sender, RoutedEventArgs e)
    {
        if (_module?.LastReport is null) return;

        // Select all Safe issues, deselect Caution
        foreach (var issue in _module.LastReport.Issues)
        {
            _selections[issue.Id] = issue.Risk == "Safe";
        }

        // Rebuild UI to reflect selection changes
        IssueList.Children.Clear();
        var grouped = _module.LastReport.Issues.GroupBy(i => i.Category).OrderByDescending(g => g.Count());
        foreach (var group in grouped)
        {
            var section = new StackPanel { Spacing = 4 };
            var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };
            headerRow.Children.Add(new TextBlock
            {
                Text = $"{group.Key} ({group.Count()})",
                Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["BodyStrongTextBlockStyle"]
            });
            section.Children.Add(headerRow);

            foreach (var issue in group)
            {
                var isSelected = _selections.ContainsKey(issue.Id) && _selections[issue.Id];

                var card = new Border
                {
                    Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                    CornerRadius = new CornerRadius(6), Padding = new Thickness(14, 8, 14, 8),
                    BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],
                    BorderThickness = new Thickness(0.5)
                };
                var grid = new Grid { ColumnSpacing = 10 };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var check = new CheckBox { IsChecked = isSelected, MinWidth = 0, VerticalAlignment = VerticalAlignment.Center };
                var capturedId = issue.Id;
                check.Checked += (s, ev) => { _selections[capturedId] = true; UpdateFixBtn(); };
                check.Unchecked += (s, ev) => { _selections[capturedId] = false; UpdateFixBtn(); };
                Grid.SetColumn(check, 0);
                grid.Children.Add(check);

                var info = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
                info.Children.Add(new TextBlock { Text = issue.Description, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                info.Children.Add(new TextBlock { Text = issue.Detail, FontSize = 11, Opacity = 0.5, TextWrapping = TextWrapping.Wrap });
                info.Children.Add(new TextBlock { Text = issue.KeyPath, FontSize = 10, Opacity = 0.3, FontFamily = new FontFamily("Consolas") });
                Grid.SetColumn(info, 1);
                grid.Children.Add(info);

                var riskColor = issue.Risk == "Safe"
                    ? Windows.UI.Color.FromArgb(255, 46, 125, 50)
                    : Windows.UI.Color.FromArgb(255, 230, 81, 0);
                var badge = new Border
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, riskColor.R, riskColor.G, riskColor.B)),
                    CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 3, 6, 3), VerticalAlignment = VerticalAlignment.Center
                };
                badge.Child = new TextBlock
                {
                    Text = issue.Risk.ToUpper(), FontSize = 9, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(riskColor)
                };
                Grid.SetColumn(badge, 2);
                grid.Children.Add(badge);

                card.Child = grid;
                section.Children.Add(card);
            }
            IssueList.Children.Add(section);
        }

        UpdateFixBtn();
        var safeCount = _selections.Count(kv => kv.Value);
        StatusText.Text = $"Selected {safeCount} safe issue(s) — click Fix Selected to clean";
    }

    private void LoadBackupHistory()
    {
        BackupList.Children.Clear();
        try
        {
            var backupDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AuraCorePro", "RegistryBackups");

            if (!Directory.Exists(backupDir))
            {
                BackupHistoryCard.Visibility = Visibility.Collapsed;
                return;
            }

            var files = Directory.GetFiles(backupDir, "*.reg")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .Take(10)
                .ToList();

            if (files.Count == 0)
            {
                BackupHistoryCard.Visibility = Visibility.Collapsed;
                return;
            }

            BackupHistoryCard.Visibility = Visibility.Visible;

            foreach (var file in files)
            {
                var row = new Grid { ColumnSpacing = 12, Padding = new Thickness(8, 4, 8, 4) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var nameText = new TextBlock
                {
                    Text = file.Name, FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
                    FontFamily = new FontFamily("Consolas"), Opacity = 0.8
                };
                Grid.SetColumn(nameText, 0);
                row.Children.Add(nameText);

                var dateText = new TextBlock
                {
                    Text = file.CreationTime.ToString("yyyy-MM-dd HH:mm"),
                    FontSize = 11, Opacity = 0.5, VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(dateText, 1);
                row.Children.Add(dateText);

                var sizeText = new TextBlock
                {
                    Text = file.Length switch
                    {
                        < 1024 * 1024 => $"{file.Length / 1024.0:F0} KB",
                        _ => $"{file.Length / (1024.0 * 1024):F1} MB"
                    },
                    FontSize = 11, Opacity = 0.5, VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(sizeText, 2);
                row.Children.Add(sizeText);

                var restoreBtn = new Button
                {
                    Content = "Restore", Padding = new Thickness(10, 3, 10, 3),
                    FontSize = 11, VerticalAlignment = VerticalAlignment.Center
                };
                var capturedPath = file.FullName;
                restoreBtn.Click += async (s, ev) =>
                {
                    var dlg = new ContentDialog
                    {
                        Title = "Restore this backup?",
                        Content = $"This will import {file.Name} into your registry.\nCreated: {file.CreationTime:g}",
                        PrimaryButtonText = "Restore", CloseButtonText = "Cancel",
                        XamlRoot = this.XamlRoot, DefaultButton = ContentDialogButton.Close
                    };
                    if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

                    StatusText.Text = "Restoring...";
                    try
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "reg.exe", Arguments = $"import \"{capturedPath}\"",
                            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true
                        };
                        using var proc = System.Diagnostics.Process.Start(psi);
                        if (proc is not null) await proc.WaitForExitAsync();
                        StatusText.Text = $"Restored: {file.Name}";
                    }
                    catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
                };
                Grid.SetColumn(restoreBtn, 3);
                row.Children.Add(restoreBtn);

                BackupList.Children.Add(row);
            }
        }
        catch { BackupHistoryCard.Visibility = Visibility.Collapsed; }
    }

    private void ApplyLocalization()
    {
        if (FindName("PageTitle") is Microsoft.UI.Xaml.Controls.TextBlock title)
            title.Text = S._("reg.title");
        if (FindName("PageSubtitle") is Microsoft.UI.Xaml.Controls.TextBlock subtitle)
            subtitle.Text = S._("reg.subtitle");
    }
}
