using AuraCore.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;
using Windows.Storage;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.AppInstaller;
using AuraCore.Module.AppInstaller.Models;

namespace AuraCore.Desktop.Pages;

public sealed partial class AppInstallerPage : Page
{
    private AppInstallerModule? _module;
    private readonly Dictionary<string, bool> _searchSelections = new();
    private readonly Dictionary<string, bool> _uninstallSelections = new();
    private List<InstalledApp> _allInstalledApps = new();
    private string _activeTab = "bundles";

    public AppInstallerPage()
    {
        InitializeComponent();
        ApplyLocalization();
        Services.S.LanguageChanged += () => DispatcherQueue?.TryEnqueue(ApplyLocalization);
        Loaded += Page_Loaded;
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        var modules = App.Current.Services.GetServices<IOptimizationModule>();
        _module = modules.FirstOrDefault(m => m.Id == "app-installer") as AppInstallerModule;

        if (_module is null) { WingetStatusText.Text = "Module not available."; return; }

        await _module.ScanAsync(new ScanOptions());
        var report = _module.LastReport;

        LoadProgress.IsActive = false;
        LoadProgress.Visibility = Visibility.Collapsed;

        if (report is null || !report.WinGetAvailable)
        {
            WingetStatusText.Text = "WinGet is not available. Please install App Installer from the Microsoft Store.";
            return;
        }

        WingetStatusText.Text = $"WinGet ready — {report.InstalledApps.Count} apps installed on this system";
        TabBar.Visibility = Visibility.Visible;
        RenderBundles(report);
    }

    private void Tab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string tab) return;
        _activeTab = tab;

        // Reset tab button styles
        TabBundles.Style = tab == "bundles" ? (Style)Microsoft.UI.Xaml.Application.Current.Resources["AccentButtonStyle"] : null;
        TabCustom.Style = tab == "custom" ? (Style)Microsoft.UI.Xaml.Application.Current.Resources["AccentButtonStyle"] : null;
        TabSearch.Style = tab == "search" ? (Style)Microsoft.UI.Xaml.Application.Current.Resources["AccentButtonStyle"] : null;
        TabInstalled.Style = tab == "installed" ? (Style)Microsoft.UI.Xaml.Application.Current.Resources["AccentButtonStyle"] : null;
        TabUpdates.Style = tab == "updates" ? (Style)Microsoft.UI.Xaml.Application.Current.Resources["AccentButtonStyle"] : null;

        BundlesPanel.Visibility = tab == "bundles" ? Visibility.Visible : Visibility.Collapsed;
        CustomBundlesPanel.Visibility = tab == "custom" ? Visibility.Visible : Visibility.Collapsed;
        SearchPanel.Visibility = tab == "search" ? Visibility.Visible : Visibility.Collapsed;
        InstalledPanel.Visibility = tab == "installed" ? Visibility.Visible : Visibility.Collapsed;
        UpdatesPanel.Visibility = tab == "updates" ? Visibility.Visible : Visibility.Collapsed;
        ResultCard.Visibility = Visibility.Collapsed;

        if (tab == "installed" && InstalledList.Children.Count == 0)
            RefreshInstalled_Click(sender, e);
        if (tab == "custom")
            RenderCustomBundles();
    }

    // ── BUNDLES TAB ──────────────────────────────────────────

    private void RenderBundles(AppInstallerReport report)
    {
        BundlesPanel.Children.Clear();

        foreach (var bundle in report.Bundles)
        {
            var card = new Border
            {
                Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20),
                BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1)
            };

            var stack = new StackPanel { Spacing = 10 };

            // Header
            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            header.Children.Add(new FontIcon { Glyph = ((char)Convert.ToInt32(bundle.Icon, 16)).ToString(), FontSize = 22 });
            var headerText = new StackPanel { Spacing = 2 };
            headerText.Children.Add(new TextBlock
            {
                Text = bundle.Name,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 16
            });
            headerText.Children.Add(new TextBlock { Text = bundle.Description, FontSize = 12, Opacity = 0.6 });
            header.Children.Add(headerText);
            stack.Children.Add(header);

            // App list within bundle
            var bundleSelections = new Dictionary<string, bool>();

            foreach (var app in bundle.Apps)
            {
                var row = new Grid { ColumnSpacing = 8, Padding = new Thickness(0, 4, 0, 4) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var check = new CheckBox
                {
                    IsChecked = !app.IsInstalled,
                    IsEnabled = !app.IsInstalled,
                    MinWidth = 0,
                    Tag = app.WinGetId,
                    VerticalAlignment = VerticalAlignment.Center
                };
                bundleSelections[app.WinGetId] = !app.IsInstalled;
                check.Checked += (s, e) => bundleSelections[app.WinGetId] = true;
                check.Unchecked += (s, e) => bundleSelections[app.WinGetId] = false;
                Grid.SetColumn(check, 0);
                row.Children.Add(check);

                var info = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
                info.Children.Add(new TextBlock { Text = app.Name, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                info.Children.Add(new TextBlock { Text = app.Description, FontSize = 11, Opacity = 0.5 });
                Grid.SetColumn(info, 1);
                row.Children.Add(info);

                if (app.IsInstalled)
                {
                    var badge = new Border
                    {
                        Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, 46, 125, 50)),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(8, 3, 8, 3),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    badge.Child = new TextBlock
                    {
                        Text = "INSTALLED",
                        FontSize = 10,
                        Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 46, 125, 50)),
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                    };
                    Grid.SetColumn(badge, 2);
                    row.Children.Add(badge);
                }

                stack.Children.Add(row);
            }

            // Install bundle button
            var notInstalled = bundle.Apps.Count(a => !a.IsInstalled);
            var installBtn = new Button
            {
                Content = notInstalled > 0 ? $"Install {notInstalled} App(s)" : "All Installed",
                IsEnabled = notInstalled > 0,
                Padding = new Thickness(16, 8, 16, 8),
                Margin = new Thickness(0, 4, 0, 0)
            };
            if (notInstalled > 0)
            {
                installBtn.Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["AccentButtonStyle"];
            }
            var capturedSelections = bundleSelections;
            var capturedBundleName = bundle.Name;
            installBtn.Click += async (s, e) =>
            {
                var toInstall = capturedSelections.Where(kv => kv.Value).Select(kv => $"install:{kv.Key}").ToList();
                if (toInstall.Count == 0) return;

                var dialog = new ContentDialog
                {
                    Title = $"Install {capturedBundleName}?",
                    Content = $"This will install {toInstall.Count} application(s) using WinGet.\nApps will be installed silently in the background.",
                    PrimaryButtonText = "Install",
                    CloseButtonText = "Cancel",
                    XamlRoot = this.XamlRoot,
                    DefaultButton = ContentDialogButton.Primary
                };
                if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
                await RunBulkAction(toInstall, "Installing");
            };
            stack.Children.Add(installBtn);

            card.Child = stack;
            BundlesPanel.Children.Add(card);
        }
    }

    // ── SEARCH TAB ───────────────────────────────────────────

    private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
            SearchBtn_Click(sender, e);
    }

    private async void SearchBtn_Click(object sender, RoutedEventArgs e)
    {
        var query = SearchBox.Text.Trim();
        if (string.IsNullOrEmpty(query) || _module is null) return;

        SearchBtn.IsEnabled = false;
        SearchProgress.IsActive = true;
        SearchProgress.Visibility = Visibility.Visible;
        SearchResults.Children.Clear();
        _searchSelections.Clear();
        BulkInstallBtn.Visibility = Visibility.Collapsed;

        try
        {
            var results = await _module.SearchAsync(query);

            if (results.Count == 0)
            {
                SearchResults.Children.Add(new TextBlock
                {
                    Text = $"No results for \"{query}\"",
                    Opacity = 0.6, Margin = new Thickness(0, 8, 0, 0)
                });
                return;
            }

            foreach (var app in results)
            {
                var row = new Grid
                {
                    ColumnSpacing = 8,
                    Padding = new Thickness(12, 8, 12, 8),
                    BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],
                    BorderThickness = new Thickness(0, 0, 0, 0.5)
                };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var check = new CheckBox { MinWidth = 0, Tag = app.Id, VerticalAlignment = VerticalAlignment.Center };
                check.Checked += (s, ev) => { _searchSelections[app.Id] = true; UpdateBulkInstallBtn(); };
                check.Unchecked += (s, ev) => { _searchSelections[app.Id] = false; UpdateBulkInstallBtn(); };
                Grid.SetColumn(check, 0);
                row.Children.Add(check);

                var info = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
                info.Children.Add(new TextBlock { Text = app.Name, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                info.Children.Add(new TextBlock { Text = app.Id, FontSize = 11, Opacity = 0.4 });
                Grid.SetColumn(info, 1);
                row.Children.Add(info);

                var verText = new TextBlock { Text = app.Version, FontSize = 12, Opacity = 0.5, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(verText, 2);
                row.Children.Add(verText);

                var installSingle = new Button { Content = "Install", Padding = new Thickness(12, 4, 12, 4), Tag = app.Id };
                installSingle.Click += async (s, ev) =>
                {
                    await RunBulkAction(new List<string> { $"install:{app.Id}" }, "Installing");
                };
                Grid.SetColumn(installSingle, 3);
                row.Children.Add(installSingle);

                SearchResults.Children.Add(row);
            }

            BulkInstallBtn.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            SearchResults.Children.Add(new TextBlock { Text = $"Search error: {ex.Message}", Opacity = 0.6 });
        }
        finally
        {
            SearchBtn.IsEnabled = true;
            SearchProgress.IsActive = false;
            SearchProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateBulkInstallBtn()
    {
        var count = _searchSelections.Count(kv => kv.Value);
        BulkInstallBtn.Content = $"Install Selected ({count})";
        BulkInstallBtn.IsEnabled = count > 0;
    }

    private async void BulkInstall_Click(object sender, RoutedEventArgs e)
    {
        var toInstall = _searchSelections.Where(kv => kv.Value).Select(kv => $"install:{kv.Key}").ToList();
        if (toInstall.Count == 0) return;

        var dialog = new ContentDialog
        {
            Title = $"Install {toInstall.Count} app(s)?",
            Content = "Selected apps will be installed silently using WinGet.",
            PrimaryButtonText = "Install All",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot,
            DefaultButton = ContentDialogButton.Primary
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        await RunBulkAction(toInstall, "Installing");
    }

    // ── INSTALLED TAB ────────────────────────────────────────

    private async void RefreshInstalled_Click(object sender, RoutedEventArgs e)
    {
        InstalledProgress.IsActive = true;
        InstalledProgress.Visibility = Visibility.Visible;
        InstalledStatusText.Text = "Loading installed apps...";
        InstalledList.Children.Clear();
        _uninstallSelections.Clear();
        _allInstalledApps.Clear();
        InstalledFilterBox.Text = "";

        try
        {
            if (_module is null) return;
            await _module.ScanAsync(new ScanOptions());
            var report = _module.LastReport;
            if (report is null) return;

            _allInstalledApps = report.InstalledApps.OrderBy(a => a.Name).ToList();
            RenderInstalledApps(_allInstalledApps);

            InstalledStatusText.Text = $"{report.InstalledApps.Count} apps found";
        }
        catch (Exception ex) { InstalledStatusText.Text = $"Error: {ex.Message}"; }
        finally
        {
            InstalledProgress.IsActive = false;
            InstalledProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void RenderInstalledApps(List<InstalledApp> apps)
    {
        InstalledList.Children.Clear();
        _uninstallSelections.Clear();

        foreach (var app in apps)
        {
            var row = new Grid
            {
                ColumnSpacing = 8,
                Padding = new Thickness(12, 6, 12, 6),
                BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(0, 0, 0, 0.5)
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var check = new CheckBox { MinWidth = 0, Tag = app.Id, VerticalAlignment = VerticalAlignment.Center };
            check.Checked += (s, ev) => { _uninstallSelections[app.Id] = true; UpdateBulkUninstallBtn(); };
            check.Unchecked += (s, ev) => { _uninstallSelections[app.Id] = false; UpdateBulkUninstallBtn(); };
            Grid.SetColumn(check, 0);
            row.Children.Add(check);

            var info = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(new TextBlock { Text = app.Name, FontSize = 13 });
            info.Children.Add(new TextBlock { Text = $"{app.Id}  v{app.Version}", FontSize = 11, Opacity = 0.4 });
            Grid.SetColumn(info, 1);
            row.Children.Add(info);

            var pubText = new TextBlock { Text = app.Publisher, FontSize = 11, Opacity = 0.5, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 150 };
            Grid.SetColumn(pubText, 2);
            row.Children.Add(pubText);

            var uninstallSingle = new Button { Content = "Remove", Padding = new Thickness(10, 3, 10, 3), Tag = app.Id, FontSize = 12 };
            uninstallSingle.Click += async (s, ev) =>
            {
                var dlg = new ContentDialog
                {
                    Title = $"Uninstall {app.Name}?",
                    Content = "This will remove the application from your system.",
                    PrimaryButtonText = "Uninstall",
                    CloseButtonText = "Cancel",
                    XamlRoot = this.XamlRoot,
                    DefaultButton = ContentDialogButton.Close
                };
                if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
                await RunBulkAction(new List<string> { $"uninstall:{app.Id}" }, "Removing");
            };
            Grid.SetColumn(uninstallSingle, 3);
            row.Children.Add(uninstallSingle);

            InstalledList.Children.Add(row);
        }

        UpdateBulkUninstallBtn();
    }

    private void InstalledFilter_TextChanged(object sender, TextChangedEventArgs e)
    {
        var filter = InstalledFilterBox.Text.Trim();
        if (string.IsNullOrEmpty(filter))
        {
            RenderInstalledApps(_allInstalledApps);
            return;
        }

        var filtered = _allInstalledApps.Where(a =>
            a.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            a.Id.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            a.Publisher.Contains(filter, StringComparison.OrdinalIgnoreCase)
        ).ToList();

        RenderInstalledApps(filtered);
        InstalledStatusText.Text = $"{filtered.Count} of {_allInstalledApps.Count} apps matching \"{filter}\"";
    }

    // ── EXPORT / IMPORT ───────────────────────────────────────

    private async void ExportList_Click(object sender, RoutedEventArgs e)
    {
        if (_module is null) return;

        try
        {
            InstalledStatusText.Text = "Exporting...";
            var json = await _module.ExportInstalledAppsAsync();

            var savePicker = new FileSavePicker();
            savePicker.SuggestedStartLocation = PickerLocationId.Desktop;
            savePicker.FileTypeChoices.Add("JSON File", new List<string> { ".json" });
            savePicker.SuggestedFileName = $"AuraCorePro_Apps_{DateTime.Now:yyyyMMdd}";

            // WinUI 3 requires window handle
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

            var file = await savePicker.PickSaveFileAsync();
            if (file is null) { InstalledStatusText.Text = "Export cancelled."; return; }

            await FileIO.WriteTextAsync(file, json);
            InstalledStatusText.Text = $"Exported {_allInstalledApps.Count} apps to {file.Name}";

            ResultCard.Visibility = Visibility.Visible;
            ResultText.Text = $"App list exported to {file.Path}";
        }
        catch (Exception ex)
        {
            InstalledStatusText.Text = $"Export error: {ex.Message}";
        }
    }

    private async void ImportList_Click(object sender, RoutedEventArgs e)
    {
        if (_module is null) return;

        try
        {
            var openPicker = new FileOpenPicker();
            openPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            openPicker.FileTypeFilter.Add(".json");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hwnd);

            var file = await openPicker.PickSingleFileAsync();
            if (file is null) return;

            var json = await FileIO.ReadTextAsync(file);
            var apps = AppInstallerModule.ParseImportFile(json);

            if (apps.Count == 0)
            {
                InstalledStatusText.Text = "No valid apps found in the import file.";
                return;
            }

            // Show what will be installed
            var dialog = new ContentDialog
            {
                Title = $"Import {apps.Count} app(s)?",
                Content = $"This will attempt to install {apps.Count} application(s) from the exported list:\n\n" +
                    string.Join("\n", apps.Take(10).Select(a => $"  • {a.Name} ({a.Id})")) +
                    (apps.Count > 10 ? $"\n  ...and {apps.Count - 10} more" : "") +
                    "\n\nAlready installed apps will be skipped by WinGet.",
                PrimaryButtonText = "Install All",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot,
                DefaultButton = ContentDialogButton.Primary
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            var toInstall = apps.Select(a => $"install:{a.Id}").ToList();
            await RunBulkAction(toInstall, "Installing from import");
        }
        catch (Exception ex)
        {
            InstalledStatusText.Text = $"Import error: {ex.Message}";
        }
    }

    private void UpdateBulkUninstallBtn()
    {
        var count = _uninstallSelections.Count(kv => kv.Value);
        BulkUninstallBtn.Content = $"Uninstall Selected ({count})";
        BulkUninstallBtn.IsEnabled = count > 0;
    }

    private async void BulkUninstall_Click(object sender, RoutedEventArgs e)
    {
        var toRemove = _uninstallSelections.Where(kv => kv.Value).Select(kv => $"uninstall:{kv.Key}").ToList();
        if (toRemove.Count == 0) return;

        var dialog = new ContentDialog
        {
            Title = $"Uninstall {toRemove.Count} app(s)?",
            Content = "Selected apps will be removed from your system.\nThis cannot be easily undone.",
            PrimaryButtonText = "Uninstall All",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot,
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        await RunBulkAction(toRemove, "Removing");
    }

    // ── SHARED ACTION RUNNER ─────────────────────────────────

    private async Task RunBulkAction(List<string> items, string verb)
    {
        if (_module is null) return;

        ResultCard.Visibility = Visibility.Visible;
        ResultText.Text = $"{verb} {items.Count} app(s)...";

        var plan = new OptimizationPlan("app-installer", items);
        var progress = new Progress<TaskProgress>(p =>
        {
            DispatcherQueue.TryEnqueue(() => ResultText.Text = p.StatusText);
        });

        var result = await _module.OptimizeAsync(plan, progress);

        ResultText.Text = $"Done — {result.ItemsProcessed} of {items.Count} succeeded ({result.Duration.TotalSeconds:F0}s)";
    }

    // ── UPDATES TAB ──────────────────────────────────────────

    private readonly Dictionary<string, bool> _updateSelections = new();

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        if (_module is null) return;
        CheckUpdatesBtn.IsEnabled = false;
        UpdateProgress.IsActive = true; UpdateProgress.Visibility = Visibility.Visible;
        UpdateStatusText.Text = "Checking for updates...";
        UpdateList.Children.Clear();
        _updateSelections.Clear();
        UpdateAllBtn.IsEnabled = false; UpdateSelectedBtn.IsEnabled = false;

        try
        {
            var outdated = await _module.GetOutdatedAppsAsync();

            if (outdated.Count == 0)
            {
                UpdateStatusText.Text = "All apps are up to date!";
                return;
            }

            foreach (var app in outdated)
            {
                _updateSelections[app.Id] = true;

                var row = new Grid
                {
                    ColumnSpacing = 8, Padding = new Thickness(12, 8, 12, 8),
                    BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],
                    BorderThickness = new Thickness(0, 0, 0, 0.5)
                };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var check = new CheckBox { IsChecked = true, MinWidth = 0, VerticalAlignment = VerticalAlignment.Center };
                var capturedId = app.Id;
                check.Checked += (s, ev) => { _updateSelections[capturedId] = true; UpdateSelectedCount(); };
                check.Unchecked += (s, ev) => { _updateSelections[capturedId] = false; UpdateSelectedCount(); };
                Grid.SetColumn(check, 0);
                row.Children.Add(check);

                var info = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
                info.Children.Add(new TextBlock { Text = app.Name, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                info.Children.Add(new TextBlock { Text = app.Id, FontSize = 11, Opacity = 0.4 });
                Grid.SetColumn(info, 1);
                row.Children.Add(info);

                var versionText = new TextBlock
                {
                    Text = $"{app.CurrentVersion} → {app.AvailableVersion}",
                    FontSize = 12, Opacity = 0.6, VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(versionText, 2);
                row.Children.Add(versionText);

                var updateSingle = new Button { Content = "Update", Padding = new Thickness(12, 4, 12, 4), Tag = app.Id };
                updateSingle.Click += async (s, ev) =>
                {
                    updateSingle.IsEnabled = false;
                    updateSingle.Content = "Updating...";
                    var (ok, _) = await _module.UpdateAppsAsync(new List<string> { app.Id });
                    updateSingle.Content = ok > 0 ? "Updated!" : "Failed";
                };
                Grid.SetColumn(updateSingle, 3);
                row.Children.Add(updateSingle);

                UpdateList.Children.Add(row);
            }

            UpdateAllBtn.IsEnabled = true;
            UpdateSelectedCount();
            UpdateStatusText.Text = $"{outdated.Count} update(s) available";
        }
        catch (Exception ex) { UpdateStatusText.Text = $"Error: {ex.Message}"; }
        finally
        {
            CheckUpdatesBtn.IsEnabled = true;
            UpdateProgress.IsActive = false; UpdateProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateSelectedCount()
    {
        var count = _updateSelections.Count(kv => kv.Value);
        UpdateSelectedBtn.Content = $"Update Selected ({count})";
        UpdateSelectedBtn.IsEnabled = count > 0;
    }

    private async void UpdateAll_Click(object sender, RoutedEventArgs e)
    {
        if (_module is null) return;
        var dialog = new ContentDialog
        {
            Title = "Update all apps?",
            Content = "This will update all outdated applications using WinGet.\nApps will be updated silently in the background.",
            PrimaryButtonText = "Update All", CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot, DefaultButton = ContentDialogButton.Primary
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        UpdateAllBtn.IsEnabled = false; UpdateSelectedBtn.IsEnabled = false;
        UpdateProgress.IsActive = true; UpdateProgress.Visibility = Visibility.Visible;
        UpdateStatusText.Text = "Updating all apps...";

        var progress = new Progress<TaskProgress>(p =>
            DispatcherQueue.TryEnqueue(() => UpdateStatusText.Text = p.StatusText));
        var (updated, failed) = await _module.UpdateAppsAsync(new List<string>(), progress);

        UpdateStatusText.Text = $"Update complete — {updated} updated, {failed} failed";
        UpdateProgress.IsActive = false; UpdateProgress.Visibility = Visibility.Collapsed;
    }

    private async void UpdateSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_module is null) return;
        var selected = _updateSelections.Where(kv => kv.Value).Select(kv => kv.Key).ToList();
        if (selected.Count == 0) return;

        UpdateAllBtn.IsEnabled = false; UpdateSelectedBtn.IsEnabled = false;
        UpdateProgress.IsActive = true; UpdateProgress.Visibility = Visibility.Visible;

        var progress = new Progress<TaskProgress>(p =>
            DispatcherQueue.TryEnqueue(() => UpdateStatusText.Text = p.StatusText));
        var (updated, failed) = await _module.UpdateAppsAsync(selected, progress);

        UpdateStatusText.Text = $"Done — {updated} updated, {failed} failed";
        UpdateProgress.IsActive = false; UpdateProgress.Visibility = Visibility.Collapsed;
    }

    // ── CUSTOM BUNDLES ─────────────────────────────────────────

    private void RenderCustomBundles()
    {
        CustomBundlesList.Children.Clear();
        var bundles = CustomBundleStore.Load();

        if (bundles.Count == 0)
        {
            CustomBundleStatus.Text = "No custom bundles yet. Create one!";
            return;
        }

        CustomBundleStatus.Text = $"{bundles.Count} custom bundle(s)";

        foreach (var bundle in bundles)
        {
            var card = new Border
            {
                Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                CornerRadius = new CornerRadius(8), Padding = new Thickness(20),
                BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1)
            };

            var stack = new StackPanel { Spacing = 10 };

            // Header with delete button
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleStack = new StackPanel { Spacing = 2 };
            titleStack.Children.Add(new TextBlock { Text = bundle.Name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 16 });
            titleStack.Children.Add(new TextBlock { Text = $"{bundle.Description} — {bundle.Apps.Count} apps", FontSize = 12, Opacity = 0.6 });
            Grid.SetColumn(titleStack, 0); header.Children.Add(titleStack);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

            // Install all button
            var installBtn = new Button { Content = $"Install All ({bundle.Apps.Count})", Padding = new Thickness(12, 6, 12, 6), FontSize = 12 };
            var capturedApps = bundle.Apps;
            installBtn.Click += async (s, ev) =>
            {
                if (_module is null) return;
                installBtn.IsEnabled = false;
                installBtn.Content = "Installing...";
                var ids = capturedApps.Select(a => a.WinGetId).ToList();
                var plan = new OptimizationPlan("app-installer", ids);
                var progress = new Progress<TaskProgress>(p =>
                {
                    DispatcherQueue.TryEnqueue(() => installBtn.Content = p.StatusText);
                });
                var result = await _module.OptimizeAsync(plan, progress);
                installBtn.Content = $"Done! ({result.ItemsProcessed} installed)";
                installBtn.IsEnabled = true;
            };
            btnPanel.Children.Add(installBtn);

            // Delete button
            var deleteBtn = new Button { Content = "Delete", Padding = new Thickness(10, 6, 10, 6), FontSize = 12 };
            var capturedId = bundle.Id;
            deleteBtn.Click += async (s, ev) =>
            {
                var dlg = new ContentDialog
                {
                    Title = $"Delete \"{bundle.Name}\"?",
                    Content = "This bundle will be permanently removed.",
                    PrimaryButtonText = "Delete", CloseButtonText = "Cancel",
                    XamlRoot = this.XamlRoot, DefaultButton = ContentDialogButton.Close
                };
                if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
                CustomBundleStore.Remove(capturedId);
                RenderCustomBundles();
            };
            btnPanel.Children.Add(deleteBtn);

            Grid.SetColumn(btnPanel, 1); header.Children.Add(btnPanel);
            stack.Children.Add(header);

            // App list
            foreach (var app in bundle.Apps)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Padding = new Thickness(0, 2, 0, 2) };
                row.Children.Add(new TextBlock { Text = "•", Opacity = 0.3, VerticalAlignment = VerticalAlignment.Center });
                row.Children.Add(new TextBlock { Text = app.Name, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
                row.Children.Add(new TextBlock { Text = app.WinGetId, FontSize = 10, Opacity = 0.3, VerticalAlignment = VerticalAlignment.Center });
                stack.Children.Add(row);
            }

            card.Child = stack;
            CustomBundlesList.Children.Add(card);
        }
    }

    private async void CreateBundle_Click(object sender, RoutedEventArgs e)
    {
        // Step 1: Get bundle name
        var nameBox = new TextBox { PlaceholderText = "e.g. My Dev Tools", Width = 350 };
        var descBox = new TextBox { PlaceholderText = "e.g. Tools I use for development", Width = 350 };
        var nameDialog = new ContentDialog
        {
            Title = "Create Custom Bundle",
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = "Bundle Name:" },
                    nameBox,
                    new TextBlock { Text = "Description (optional):" },
                    descBox,
                }
            },
            PrimaryButtonText = "Next — Add Apps",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };
        if (await nameDialog.ShowAsync() != ContentDialogResult.Primary) return;

        var bundleName = nameBox.Text.Trim();
        if (string.IsNullOrEmpty(bundleName)) return;

        // Step 2: Let user add apps by WinGet ID
        var appsBox = new TextBox
        {
            PlaceholderText = "One WinGet ID per line, e.g.:\nGoogle.Chrome\nMicrosoft.VisualStudioCode\nPython.Python.3.12",
            Width = 400, Height = 200,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap
        };
        var appsDialog = new ContentDialog
        {
            Title = $"Add Apps to \"{bundleName}\"",
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "Enter WinGet package IDs (one per line):", FontSize = 13 },
                    appsBox,
                    new TextBlock { Text = "Tip: Use the Search tab to find exact WinGet IDs", FontSize = 11, Opacity = 0.5 }
                }
            },
            PrimaryButtonText = "Create Bundle",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };
        if (await appsDialog.ShowAsync() != ContentDialogResult.Primary) return;

        var lines = appsBox.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0) return;

        var apps = lines.Select(id => new CustomBundleApp
        {
            WinGetId = id.Trim(),
            Name = id.Trim().Split('.').Last() // Simple name extraction
        }).ToList();

        var bundle = new CustomBundle
        {
            Name = bundleName,
            Description = descBox.Text.Trim(),
            Apps = apps
        };

        CustomBundleStore.Add(bundle);
        RenderCustomBundles();
        CustomBundleStatus.Text = $"Created \"{bundleName}\" with {apps.Count} apps";
    }

    private void ApplyLocalization()
    {
        if (FindName("PageTitle") is Microsoft.UI.Xaml.Controls.TextBlock title)
            title.Text = S._("apps.title");
        if (FindName("PageSubtitle") is Microsoft.UI.Xaml.Controls.TextBlock subtitle)
            subtitle.Text = S._("apps.subtitle");
    }
}
