using AuraCore.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.DriverUpdater;
using AuraCore.Module.DriverUpdater.Models;
using System.Security.Principal;

namespace AuraCore.Desktop.Pages;

public sealed partial class DriverUpdaterPage : Page
{
    private DriverUpdaterModule? _module;
    private string _currentFilter = "all";
    private static bool IsAdmin => new WindowsPrincipal(WindowsIdentity.GetCurrent())
        .IsInRole(WindowsBuiltInRole.Administrator);

    private static readonly Windows.UI.Color Green = Windows.UI.Color.FromArgb(255, 46, 125, 50);
    private static readonly Windows.UI.Color Red = Windows.UI.Color.FromArgb(255, 198, 40, 40);
    private static readonly Windows.UI.Color Amber = Windows.UI.Color.FromArgb(255, 230, 81, 0);
    private static readonly Windows.UI.Color Blue = Windows.UI.Color.FromArgb(255, 33, 150, 243);
    private static readonly Windows.UI.Color Purple = Windows.UI.Color.FromArgb(255, 124, 31, 162);

    public DriverUpdaterPage()
    {
        InitializeComponent();
        var modules = App.Current.Services.GetServices<IOptimizationModule>();
        _module = modules.FirstOrDefault(m => m.Id == "driver-updater") as DriverUpdaterModule;

        if (!IsAdmin) AdminWarning.IsOpen = true;

        ApplyLocalization();
        S.LanguageChanged += () => DispatcherQueue?.TryEnqueue(ApplyLocalization);
    }

    private async void ScanBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_module is null) return;
        ScanBtn.IsEnabled = false; BackupBtn.IsEnabled = false;
        Progress.IsActive = true; Progress.Visibility = Visibility.Visible;
        StatusText.Text = S._("drv.scanning");
        DriverList.Children.Clear();
        BackupResult.Visibility = Visibility.Collapsed;

        try
        {
            await _module.ScanAsync(new ScanOptions());
            var report = _module.LastReport;
            if (report is null || report.TotalCount == 0)
            {
                StatusText.Text = S._("drv.noDrivers");
                return;
            }

            TotalText.Text = report.TotalCount.ToString();
            CurrentText.Text = report.CurrentCount.ToString();
            OutdatedText.Text = report.OutdatedCount.ToString();
            ProblemsText.Text = report.ProblemCount.ToString();

            var bannerColor = report.ProblemCount > 0 ? Red
                : report.OutdatedCount > 5 ? Amber
                : Green;
            SummaryBanner.Background = new SolidColorBrush(
                Windows.UI.Color.FromArgb(20, bannerColor.R, bannerColor.G, bannerColor.B));
            SummaryBanner.BorderBrush = new SolidColorBrush(
                Windows.UI.Color.FromArgb(60, bannerColor.R, bannerColor.G, bannerColor.B));
            SummaryBanner.Visibility = Visibility.Visible;
            FilterBar.Visibility = Visibility.Visible;
            BackupBtn.IsEnabled = true;

            _currentFilter = "all";
            BuildDriverList();
            StatusText.Text = report.HealthSummary;
        }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
        finally
        {
            ScanBtn.IsEnabled = true;
            Progress.IsActive = false; Progress.Visibility = Visibility.Collapsed;
        }
    }

    private void FilterAll_Click(object sender, RoutedEventArgs e) { _currentFilter = "all"; BuildDriverList(); }
    private void FilterOutdated_Click(object sender, RoutedEventArgs e) { _currentFilter = "outdated"; BuildDriverList(); }
    private void FilterProblems_Click(object sender, RoutedEventArgs e) { _currentFilter = "problems"; BuildDriverList(); }

    private void BuildDriverList()
    {
        DriverList.Children.Clear();
        if (_module?.LastReport is null) return;

        var drivers = _currentFilter switch
        {
            "outdated" => _module.LastReport.Drivers.Where(d => d.AgeCategory is "Aging" or "Outdated").ToList(),
            "problems" => _module.LastReport.Drivers.Where(d => d.HasProblem).ToList(),
            _ => _module.LastReport.Drivers
        };

        FilterCountText.Text = $"Showing {drivers.Count} of {_module.LastReport.TotalCount}";

        foreach (var drv in drivers)
        {
            var ageColor = drv.AgeColor switch
            {
                "Green" => Green,
                "Blue" => Blue,
                "Amber" => Amber,
                "Red" => Red,
                _ => Blue
            };

            var card = new Border
            {
                Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 10, 14, 10),
                BorderBrush = drv.HasProblem
                    ? new SolidColorBrush(Windows.UI.Color.FromArgb(60, Red.R, Red.G, Red.B))
                    : (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(drv.HasProblem ? 1 : 0.5)
            };

            var grid = new Grid { ColumnSpacing = 12 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // icon
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // info
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // version
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // age badge

            // Icon
            var icon = new FontIcon
            {
                Glyph = drv.ClassIcon, FontSize = 18,
                Foreground = new SolidColorBrush(drv.HasProblem ? Red : Blue),
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Segoe MDL2 Assets")
            };
            Grid.SetColumn(icon, 0);
            grid.Children.Add(icon);

            // Info
            var info = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(new TextBlock
            {
                Text = drv.DeviceName,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 13,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            var detailRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            if (!string.IsNullOrEmpty(drv.Manufacturer))
                detailRow.Children.Add(new TextBlock { Text = drv.Manufacturer, FontSize = 11, Opacity = 0.45 });
            if (!string.IsNullOrEmpty(drv.DeviceClass))
                detailRow.Children.Add(new TextBlock { Text = drv.DeviceClass, FontSize = 11, Opacity = 0.35 });
            if (drv.HasProblem)
                detailRow.Children.Add(new TextBlock
                {
                    Text = $"Error {drv.ProblemCode}", FontSize = 11,
                    Foreground = new SolidColorBrush(Red),
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                });
            info.Children.Add(detailRow);
            Grid.SetColumn(info, 1);
            grid.Children.Add(info);

            // Version + Date
            var verStack = new StackPanel { Spacing = 0, VerticalAlignment = VerticalAlignment.Center, MinWidth = 120 };
            verStack.Children.Add(new TextBlock
            {
                Text = drv.DriverVersion, FontSize = 11,
                FontFamily = new FontFamily("Consolas"),
                Opacity = 0.6, TextAlignment = TextAlignment.Right
            });
            verStack.Children.Add(new TextBlock
            {
                Text = drv.DriverDateDisplay, FontSize = 10,
                Opacity = 0.35, TextAlignment = TextAlignment.Right
            });
            Grid.SetColumn(verStack, 2);
            grid.Children.Add(verStack);

            // Age badge
            var badge = new Border
            {
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(25, ageColor.R, ageColor.G, ageColor.B)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 3, 8, 3),
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 70
            };
            badge.Child = new TextBlock
            {
                Text = drv.AgeCategory.ToUpper(), FontSize = 9,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(ageColor),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(badge, 3);
            grid.Children.Add(badge);

            card.Child = grid;
            DriverList.Children.Add(card);
        }
    }

    private async void BackupBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_module is null) return;

        var backupPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            $"AuraCore-DriverBackup-{DateTime.Now:yyyyMMdd-HHmmss}");

        var dialog = new ContentDialog
        {
            Title = S._("drv.backupTitle"),
            Content = string.Format(S._("drv.backupMsg"), backupPath),
            PrimaryButtonText = S._("drv.backupStart"),
            CloseButtonText = S._("priv.cancel"),
            XamlRoot = this.XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        BackupBtn.IsEnabled = false;
        Progress.IsActive = true; Progress.Visibility = Visibility.Visible;
        StatusText.Text = S._("drv.backingUp");

        try
        {
            var result = await _module.BackupDriversAsync(backupPath);
            if (result.Success)
            {
                BackupTitle.Text = string.Format(S._("drv.backupDone"), result.DriversExported);
                BackupDetail.Text = $"{result.SizeDisplay} - {result.BackupPath}";
                BackupResult.Visibility = Visibility.Visible;
                StatusText.Text = S._("drv.backupSuccess");
            }
            else
            {
                StatusText.Text = result.Error ?? S._("drv.backupFailed");
            }
        }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
        finally
        {
            BackupBtn.IsEnabled = true;
            Progress.IsActive = false; Progress.Visibility = Visibility.Collapsed;
        }
    }

    private async void WinUpdateBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_module is null) return;
        WinUpdateBtn.IsEnabled = false;
        Progress.IsActive = true; Progress.Visibility = Visibility.Visible;
        StatusText.Text = S._("drv.checkingUpdates");
        WinUpdateList.Children.Clear();

        try
        {
            var updates = await _module.CheckWindowsUpdateDriversAsync();

            if (updates.Count == 0)
            {
                WinUpdateTitle.Text = S._("drv.noUpdates");
                WinUpdateList.Children.Add(new TextBlock
                {
                    Text = S._("drv.allUpToDate"), FontSize = 12, Opacity = 0.5,
                    Foreground = new SolidColorBrush(Green)
                });
            }
            else
            {
                WinUpdateTitle.Text = string.Format(S._("drv.updatesFound"), updates.Count);
                foreach (var u in updates)
                {
                    WinUpdateList.Children.Add(new TextBlock
                    {
                        Text = u, FontSize = 12, Opacity = 0.7,
                        TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2)
                    });
                }

                var openBtn = new Button
                {
                    Content = S._("drv.openWinUpdate"),
                    Padding = new Thickness(16, 6, 16, 6),
                    Margin = new Thickness(0, 8, 0, 0)
                };
                openBtn.Click += async (s, ev) => await _module.OpenWindowsUpdateAsync();
                WinUpdateList.Children.Add(openBtn);
            }

            WinUpdateCard.Visibility = Visibility.Visible;
            StatusText.Text = updates.Count > 0
                ? string.Format(S._("drv.updatesFound"), updates.Count)
                : S._("drv.noUpdates");
        }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
        finally
        {
            WinUpdateBtn.IsEnabled = true;
            Progress.IsActive = false; Progress.Visibility = Visibility.Collapsed;
        }
    }

    private async void DevMgrBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_module is not null) await _module.OpenDeviceManagerAsync();
    }

    private void ApplyLocalization()
    {
        try
        {
            PageTitle.Text = S._("drv.title");
            PageSubtitle.Text = S._("drv.subtitle");
            ScanBtn.Content = S._("drv.scanBtn");
            BackupBtn.Content = S._("drv.backupBtn");
            WinUpdateBtn.Content = S._("drv.winUpdateBtn");
            DevMgrBtn.Content = S._("drv.devMgrBtn");
            StatusText.Text = S._("drv.scanStatus");
        }
        catch { }
    }
}
