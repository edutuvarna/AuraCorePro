using AuraCore.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.StorageCompression;
using AuraCore.Module.StorageCompression.Models;

namespace AuraCore.Desktop.Pages;

public sealed partial class StoragePage : Page
{
    private StorageCompressionModule? _module;
    private readonly Dictionary<string, bool> _selections = new();

    public StoragePage()
    {
        InitializeComponent();
        ApplyLocalization();
        Services.S.LanguageChanged += () => DispatcherQueue?.TryEnqueue(ApplyLocalization);
        var modules = App.Current.Services.GetServices<IOptimizationModule>();
        _module = modules.FirstOrDefault(m => m.Id == "storage-compression") as StorageCompressionModule;
    }

    private async void ScanBtn_Click(object sender, RoutedEventArgs e)
    {
        ScanBtn.IsEnabled = false;
        CompressBtn.IsEnabled = false;
        Progress.IsActive = true;
        Progress.Visibility = Visibility.Visible;
        StatusText.Text = S._("storage.analyzing");
        FolderList.Children.Clear();
        ResultCard.Visibility = Visibility.Collapsed;
        _selections.Clear();

        try
        {
            if (_module is null) { StatusText.Text = S._("common.moduleUnavailable"); return; }

            await _module.ScanAsync(new ScanOptions());
            var report = _module.LastReport;
            if (report is null) { StatusText.Text = S._("common.noData"); return; }

            // Summary
            FolderCountText.Text = report.Folders.Count.ToString();
            TotalSizeText.Text = report.CurrentSizeDisplay;
            SavingsText.Text = report.SavingsDisplay;
            CompactOsText.Text = report.CompactOsEnabled ? "Enabled" : "Disabled";
            DriveTypeText.Text = report.SystemDriveType switch
            {
                AuraCore.Module.StorageCompression.Models.StorageDriveType.SSD => "SSD",
                AuraCore.Module.StorageCompression.Models.StorageDriveType.HDD => "HDD",
                _ => "Unknown"
            };
            SummaryCard.Visibility = Visibility.Visible;

            // HDD warning
            if (report.SystemDriveType == AuraCore.Module.StorageCompression.Models.StorageDriveType.HDD)
            {
                HddWarningText.Text = report.DriveTypeWarning;
                HddWarning.Visibility = Visibility.Visible;
            }
            else
            {
                HddWarning.Visibility = Visibility.Collapsed;
            }

            // CompactOS card
            CompactOsBtn.Content = report.CompactOsEnabled ? "Disable CompactOS" : "Enable CompactOS";
            CompactOsBtn.Tag = report.CompactOsEnabled ? "compact-os-disable" : "compact-os-enable";
            CompactOsCard.Visibility = Visibility.Visible;

            // Folder list
            foreach (var folder in report.Folders)
            {
                var canCompress = !folder.IsAlreadyCompressed && folder.EstimatedSavings > 0;
                if (canCompress) _selections[folder.Path] = true;

                var card = new Border
                {
                    Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16, 12, 16, 12),
                    BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],
                    BorderThickness = new Thickness(0.5),
                    Opacity = folder.IsAlreadyCompressed ? 0.5 : 1.0
                };

                var grid = new Grid { ColumnSpacing = 12 };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Checkbox
                var check = new CheckBox
                {
                    IsChecked = canCompress,
                    IsEnabled = canCompress,
                    MinWidth = 0,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var capturedPath = folder.Path;
                check.Checked += (s, ev) => { _selections[capturedPath] = true; UpdateCompressBtn(); };
                check.Unchecked += (s, ev) => { _selections[capturedPath] = false; UpdateCompressBtn(); };
                Grid.SetColumn(check, 0);
                grid.Children.Add(check);

                // Info
                var info = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
                info.Children.Add(new TextBlock
                {
                    Text = folder.DisplayName,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    FontSize = 13
                });
                info.Children.Add(new TextBlock { Text = folder.Description, FontSize = 11, Opacity = 0.6 });
                info.Children.Add(new TextBlock
                {
                    Text = $"{folder.Path}  •  Algorithm: {folder.RecommendedAlgorithm}",
                    FontSize = 10, Opacity = 0.35
                });
                Grid.SetColumn(info, 1);
                grid.Children.Add(info);

                // Current size
                var sizeText = new TextBlock
                {
                    Text = folder.SizeDisplay,
                    FontSize = 12, Opacity = 0.6,
                    VerticalAlignment = VerticalAlignment.Center, MinWidth = 70, TextAlignment = TextAlignment.Right
                };
                Grid.SetColumn(sizeText, 2);
                grid.Children.Add(sizeText);

                // Savings or Already Compressed badge
                if (folder.IsAlreadyCompressed)
                {
                    var badge = new Border
                    {
                        Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, 33, 150, 243)),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(8, 3, 8, 3),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    badge.Child = new TextBlock
                    {
                        Text = "COMPRESSED",
                        FontSize = 10,
                        Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 33, 150, 243)),
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                    };
                    Grid.SetColumn(badge, 3);
                    grid.Children.Add(badge);

                    // Decompress button
                    var decompBtn = new Button
                    {
                        Content = "Decompress", Padding = new Thickness(10, 3, 10, 3),
                        FontSize = 11, VerticalAlignment = VerticalAlignment.Center
                    };
                    var decompPath = folder.Path;
                    decompBtn.Click += async (s, ev) =>
                    {
                        var dlg = new ContentDialog
                        {
                            Title = $"Decompress {folder.DisplayName}?",
                            Content = S._("storage.decompressConfirm"),
                            PrimaryButtonText = "Decompress", CloseButtonText = "Cancel",
                            XamlRoot = this.XamlRoot, DefaultButton = ContentDialogButton.Close
                        };
                        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
                        await RunCompression(new List<string> { $"decompress:{decompPath}" });
                    };
                    Grid.SetColumn(decompBtn, 5);
                    grid.Children.Add(decompBtn);
                }
                else
                {
                    var savText = new TextBlock
                    {
                        Text = $"~{folder.SavingsDisplay} savings",
                        FontSize = 12,
                        Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 46, 125, 50)),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(savText, 3);
                    grid.Children.Add(savText);
                }

                // Risk badge
                var riskColor = folder.Risk switch
                {
                    "Safe" => Windows.UI.Color.FromArgb(255, 46, 125, 50),
                    "Caution" => Windows.UI.Color.FromArgb(255, 230, 81, 0),
                    _ => Windows.UI.Color.FromArgb(255, 158, 158, 158)
                };
                var riskBadge = new Border
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, riskColor.R, riskColor.G, riskColor.B)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 3, 6, 3),
                    VerticalAlignment = VerticalAlignment.Center
                };
                riskBadge.Child = new TextBlock
                {
                    Text = folder.Risk.ToUpper(),
                    FontSize = 9,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(riskColor)
                };
                Grid.SetColumn(riskBadge, 4);
                grid.Children.Add(riskBadge);

                card.Child = grid;
                FolderList.Children.Add(card);
            }

            UpdateCompressBtn();
            StatusText.Text = string.Format(S._("storage.foundFolders"), report.Folders.Count, report.SavingsDisplay);
        }
        catch (Exception ex) { StatusText.Text = S._("common.errorPrefix") + ex.Message; }
        finally
        {
            ScanBtn.IsEnabled = true;
            Progress.IsActive = false;
            Progress.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateCompressBtn()
    {
        var count = _selections.Count(kv => kv.Value);
        CompressBtn.Content = $"Compress Selected ({count})";
        CompressBtn.IsEnabled = count > 0;
    }

    private async void CompressBtn_Click(object sender, RoutedEventArgs e)
    {
        var selected = _selections.Where(kv => kv.Value).Select(kv => kv.Key).ToList();
        if (selected.Count == 0) return;

        var dialog = new ContentDialog
        {
            Title = $"Compress {selected.Count} folder(s)?",
            Content = "Files will be transparently compressed using Windows NTFS compression.\nApps will work normally — they don't see the compression.\nThis is fully reversible.\n\nNote: Compression requires admin privileges for system folders.",
            PrimaryButtonText = "Compress",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot,
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        await RunCompression(selected);
    }

    private async void CompactOsBtn_Click(object sender, RoutedEventArgs e)
    {
        var action = CompactOsBtn.Tag?.ToString() ?? "";
        var enabling = action == "compact-os-enable";

        var dialog = new ContentDialog
        {
            Title = enabling ? "Enable CompactOS?" : "Disable CompactOS?",
            Content = enabling
                ? "This compresses the entire Windows installation.\nSaves 2-4 GB of disk space.\nNo performance impact on modern hardware (SSD required).\nThis may take several minutes."
                : "This will decompress Windows system files.\nYou'll lose the space savings but files will be slightly faster to read.",
            PrimaryButtonText = enabling ? "Enable" : "Disable",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot,
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        await RunCompression(new List<string> { action });
    }

    private async Task RunCompression(List<string> paths)
    {
        if (_module is null) return;

        ScanBtn.IsEnabled = false;
        CompressBtn.IsEnabled = false;
        Progress.IsActive = true;
        Progress.Visibility = Visibility.Visible;
        ResultCard.Visibility = Visibility.Collapsed;

        var plan = new OptimizationPlan("storage-compression", paths);
        var progress = new Progress<TaskProgress>(p =>
        {
            DispatcherQueue.TryEnqueue(() => StatusText.Text = p.StatusText);
        });

        var result = await _module.OptimizeAsync(plan, progress);

        var savedDisplay = result.BytesFreed switch
        {
            < 1024 * 1024 => $"{result.BytesFreed / 1024.0:F0} KB",
            < 1024L * 1024 * 1024 => $"{result.BytesFreed / (1024.0 * 1024):F0} MB",
            _ => $"{result.BytesFreed / (1024.0 * 1024 * 1024):F2} GB"
        };

        ResultTitle.Text = $"Compressed {result.ItemsProcessed} folder(s)";
        ResultDetail.Text = $"Estimated {savedDisplay} saved in {result.Duration.TotalSeconds:F1}s — fully reversible";
        ResultCard.Visibility = Visibility.Visible;
        StatusText.Text = S._("storage.compressionComplete");

        ScanBtn.IsEnabled = true;
        Progress.IsActive = false;
        Progress.Visibility = Visibility.Collapsed;
    }

    private void ApplyLocalization()
    {
        if (FindName("PageTitle") is Microsoft.UI.Xaml.Controls.TextBlock title)
            title.Text = S._("storage.title");
        if (FindName("PageSubtitle") is Microsoft.UI.Xaml.Controls.TextBlock subtitle)
            subtitle.Text = S._("storage.subtitle");
    }
}
