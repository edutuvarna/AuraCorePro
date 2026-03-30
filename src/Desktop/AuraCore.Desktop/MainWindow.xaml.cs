using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using AuraCore.Application;
using AuraCore.Desktop.Pages;
using AuraCore.Module.DiskCleanup;
using AuraCore.Desktop.Services;
using AuraCore.Domain.Enums;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace AuraCore.Desktop;

public sealed partial class MainWindow : Microsoft.UI.Xaml.Window
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private static DateTimeOffset _lastTierCheck = DateTimeOffset.MinValue;

    public MainWindow()
    {
        InitializeComponent();
        Title = "Aura Core Pro";
        ExtendsContentIntoTitleBar = true;
        UserInfoText.Text = LoginWindow.UserEmail ?? "Offline Mode";
        UpdateTierBadge();
        ApplyTierLocking();

        // Initialize theme
        // Initialize theme on root Grid (not just Content)
        if (Content is FrameworkElement rootEl)
        {
            ThemeService.Initialize(rootEl);
            // Also explicitly set on NavView for sidebar
            var et = ThemeService.CurrentTheme switch
            {
                ThemeService.AppTheme.Light => ElementTheme.Light,
                ThemeService.AppTheme.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
            NavView.RequestedTheme = et;
        }

        // Initialize background scheduler
        App.InitScheduler(DispatcherQueue);

        // Start auto-update checker
        UpdateChecker.Instance.UpdateFound += OnUpdateFound;
        UpdateChecker.Instance.Start();

        // Subscribe to notifications
        NotificationService.Instance.UnreadCountChanged += (count) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                NotifBadge.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
                NotifBadgeText.Text = count > 9 ? "9+" : count.ToString();
            });
        };

        NavView.SelectedItem = NavView.MenuItems[0];

        // Show onboarding on first launch, otherwise go to Dashboard
        if (!Pages.OnboardingPage.IsCompleted)
            ContentFrame.Navigate(typeof(Pages.OnboardingPage));
        else
            ContentFrame.Navigate(typeof(DashboardPage));

        // Localize navigation items
        LocalizeNavigation();

        // Re-localize when language changes from Settings
        Services.S.LanguageChanged += () =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                LocalizeNavigation();
                UpdateTierBadge();
            });
        };

        // Minimize to tray on close (X button minimizes, doesn't exit)
        this.Closed += (s, e) =>
        {
            if (_reallyClose) return;
            e.Handled = true;
            // Minimize to taskbar instead of closing
            var appWindow = GetAppWindow();
            if (appWindow is not null)
                appWindow.Hide();
            _isHidden = true;
        };

        // Re-apply tier locking when theme changes (fixes color mismatch)
        if (Content is FrameworkElement root)
        {
            root.ActualThemeChanged += (s, e) =>
            {
                DispatcherQueue?.TryEnqueue(ApplyTierLocking);
            };
        }
    }

    private bool _isHidden = false;
    private bool _reallyClose = false;

    /// <summary>Restore window from tray</summary>
    public void RestoreFromTray()
    {
        if (!_isHidden) return;
        var appWindow = GetAppWindow();
        appWindow?.Show();
        _isHidden = false;
        this.Activate();
    }

    private Microsoft.UI.Windowing.AppWindow? GetAppWindow()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        return Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
    }

    /// <summary>Actually close the application (called from Exit menu)</summary>
    public void ExitApplication()
    {
        _reallyClose = true;
        this.Close();
    }

    private void LocalizeNavigation()
    {
        var tagToKey = new Dictionary<string, string>
        {
            ["dashboard"] = "nav.dashboard",
            ["system-health"] = "nav.systemHealth",
            ["recommendations"] = "nav.recommendations",
            ["junk-cleaner"] = "nav.junkCleaner",
            ["ram-optimizer"] = "nav.ramOptimizer",
            ["storage-compression"] = "nav.storage",
            ["registry-optimizer"] = "nav.registry",
            ["bloatware-removal"] = "nav.bloatware",
            ["network-optimizer"] = "nav.network",
            ["gaming-mode"] = "nav.gamingMode",
            ["context-menu"] = "nav.contextMenu",
            ["taskbar-tweaks"] = "nav.taskbar",
            ["explorer-tweaks"] = "nav.explorer",
            ["app-installer"] = "nav.appInstaller",
            ["space-analyzer"] = "nav.spaceAnalyzer",
            ["disk-health"] = "nav.diskHealth",
            ["startup-optimizer"] = "nav.startupOptimizer",
            ["scheduler"] = "nav.scheduler",
            ["disk-cleanup"] = "nav.diskCleanup",
            ["defender-manager"] = "nav.defender",
            ["iso-builder"] = "nav.isoBuilder",
            ["service-manager"] = "nav.serviceManager",
            ["autorun-manager"] = "nav.autorunManager",
            ["process-monitor"] = "nav.processMonitor",
            ["hosts-editor"] = "nav.hostsEditor",
            ["privacy-cleaner"] = "nav.privacyCleaner",
            ["driver-updater"] = "nav.driverUpdater",
            ["battery-optimizer"] = "nav.batteryOptimizer",
            ["startup-optimizer"] = "nav.startupOptimizer",
            ["admin"] = "nav.admin",
        };

        foreach (var item in NavView.MenuItems)
        {
            if (item is NavigationViewItem navItem && navItem.Tag is string tag && tagToKey.TryGetValue(tag, out var key))
            {
                navItem.Content = Services.S._(key);
            }
            else if (item is NavigationViewItemHeader header)
            {
                var ht = header.Content?.ToString() ?? "";
                if (ht.Contains("Optim", StringComparison.OrdinalIgnoreCase))
                    header.Content = Services.S._("nav.optimization");
                else if (ht.Contains("Custom", StringComparison.OrdinalIgnoreCase) || ht.Contains("Kişisel", StringComparison.OrdinalIgnoreCase))
                    header.Content = Services.S._("nav.customization");
                else if (ht.Contains("Advanced", StringComparison.OrdinalIgnoreCase) || ht.Contains("Geli", StringComparison.OrdinalIgnoreCase))
                    header.Content = Services.S._("nav.advancedTools");
                else if (ht.Contains("Tools", StringComparison.OrdinalIgnoreCase) || ht.Contains("Araç", StringComparison.OrdinalIgnoreCase))
                    header.Content = Services.S._("nav.tools");
            }
        }

        // WinUI3 built-in Settings item label
        if (NavView.SettingsItem is NavigationViewItem settingsItem)
            settingsItem.Content = Services.S._("nav.settings");
    }

    private void UpdateTierBadge()
    {
        var isAdmin = LoginWindow.UserRole == "admin";
        var tierText = isAdmin ? "ADMIN" : (LoginWindow.UserTier ?? "free").ToUpper();
        TierBadgeText.Text = tierText;
        var badgeColor = tierText switch
        {
            "ADMIN" => Windows.UI.Color.FromArgb(255, 198, 40, 40),
            "ENTERPRISE" => Windows.UI.Color.FromArgb(255, 106, 27, 154),
            "PRO" => Windows.UI.Color.FromArgb(255, 21, 101, 192),
            _ => Windows.UI.Color.FromArgb(255, 96, 125, 139),
        };
        TierBadge.Background = new SolidColorBrush(badgeColor);
        if (isAdmin) AdminNavItem.Visibility = Visibility.Visible;
    }
    private void ApplyTierLocking()
    {
        var userTier = GetCurrentTier();
        foreach (var item in NavView.MenuItems.OfType<NavigationViewItem>())
        {
            var tag = item.Tag?.ToString();
            if (string.IsNullOrEmpty(tag) || tag == "dashboard" || tag == "admin"
                || tag == "recommendations" || tag == "scheduler") continue;
            var originalContent = item.Content?.ToString()?.Replace(" (PRO)", "") ?? "";
            if (!TierFeatures.IsModuleAllowed(tag, userTier))
            { item.IsEnabled = true; item.Content = $"{originalContent} (PRO)"; item.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(160, 120, 120, 140)); }
            else
            { item.IsEnabled = true; item.Content = originalContent; item.ClearValue(NavigationViewItem.ForegroundProperty); }
        }
    }
    /// <summary>Called after successful payment to refresh tier badge and unlock features</summary>
    public void RefreshAfterPayment()
    {
        DispatcherQueue?.TryEnqueue(() =>
        {
            UpdateTierBadge();
            ApplyTierLocking();
        });
    }

    private static SubscriptionTier GetCurrentTier()
    {
        if (LoginWindow.UserRole == "admin") return SubscriptionTier.Admin;
        return Enum.TryParse<SubscriptionTier>(LoginWindow.UserTier, true, out var t) ? t : SubscriptionTier.Free;
    }

    private async void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItem is not NavigationViewItem item) return;
        var tag = item.Tag?.ToString();

        // Refresh tier every 5 min
        if (DateTimeOffset.UtcNow - _lastTierCheck > TimeSpan.FromMinutes(5) && LoginWindow.AccessToken is not null)
        {
            await RefreshTierAsync();
            UpdateTierBadge();
            ApplyTierLocking();
        }

        var userTier = GetCurrentTier();
        if (tag is not null and not "dashboard" and not "admin" and not "recommendations" and not "scheduler"
            && !TierFeatures.IsModuleAllowed(tag, userTier))
        {
            ContentFrame.Navigate(typeof(UpgradePage));
            return;
        }

        var page = tag switch
        {
            "dashboard" => typeof(DashboardPage),
            "system-health" => typeof(SystemHealthPage),
            "recommendations" => typeof(RecommendationsPage),
            "junk-cleaner" => typeof(JunkCleanerPage),
            "ram-optimizer" => typeof(RamOptimizerPage),
            "storage-compression" => typeof(StoragePage),
            "registry-optimizer" => typeof(RegistryPage),
            "bloatware-removal" => typeof(BloatwarePage),
            "network-optimizer" => typeof(NetworkPage),
            "gaming-mode" => typeof(GamingModePage),
            "context-menu" => typeof(ContextMenuPage),
            "taskbar-tweaks" => typeof(TaskbarPage),
            "explorer-tweaks" => typeof(ExplorerPage),
            "app-installer" => typeof(AppInstallerPage),
            "space-analyzer" => typeof(SpaceAnalyzerPage),
            "disk-health" => typeof(DiskHealthPage),
            "startup-optimizer" => typeof(StartupOptimizerPage),
            "scheduler" => typeof(SchedulerPage),
            "iso-builder" => typeof(IsoBuilderPage),
            "service-manager" => typeof(ServiceManagerPage),
            "disk-cleanup" => typeof(DiskCleanupPage),
            "defender-manager" => typeof(DefenderPage),
            "privacy-cleaner" => typeof(PrivacyCleanerPage),
            "driver-updater" => typeof(DriverUpdaterPage),
            "battery-optimizer" => typeof(BatteryOptimizerPage),
            "autorun-manager" => typeof(AutorunManagerPage),
            "process-monitor" => typeof(ProcessMonitorPage),
            "hosts-editor" => typeof(HostsEditorPage),
            "admin" => typeof(AdminPanelPage),
            _ => typeof(DashboardPage)
        };
        ContentFrame.Navigate(page);
    }

    private static async Task RefreshTierAsync()
    {
        try
        {
            Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", LoginWindow.AccessToken);
            var resp = await Http.GetAsync($"{LoginWindow.ApiBaseUrl}/api/license/validate?key=self&device=self");
            if (resp.IsSuccessStatusCode)
            {
                var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                if (doc.RootElement.TryGetProperty("tier", out var tierProp))
                {
                    var newTier = tierProp.GetString() ?? "free";
                    LoginWindow.UserTier = newTier;
                    SessionState.UserTier = newTier;
                }
            }
            _lastTierCheck = DateTimeOffset.UtcNow;
        }
        catch { }
    }

    // ── AUTO UPDATE ─────────────────────────────────────────

    private void OnUpdateFound(UpdateInfo info)
    {
        DispatcherQueue?.TryEnqueue(async () =>
        {
            if (info.IsMandatory)
            {
                await ShowMandatoryUpdateDialog(info);
            }
            else
            {
                await ShowOptionalUpdateDialog(info);
            }
        });
    }

    private async Task ShowMandatoryUpdateDialog(UpdateInfo info)
    {
        var panel = new StackPanel { Spacing = 12, Width = 400 };
        panel.Children.Add(new TextBlock
        {
            Text = "A mandatory update is required to continue using AuraCore Pro.",
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
            Opacity = 0.7
        });

        if (!string.IsNullOrEmpty(info.ReleaseNotes))
        {
            panel.Children.Add(new TextBlock
            {
                Text = "What's new:",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 13
            });
            panel.Children.Add(new TextBlock
            {
                Text = info.ReleaseNotes,
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                Opacity = 0.6,
                FontSize = 12
            });
        }

        var progressBar = new ProgressBar
        {
            IsIndeterminate = false,
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 4, 0, 0)
        };
        panel.Children.Add(progressBar);

        var statusText = new TextBlock
        {
            Text = "",
            FontSize = 12,
            Opacity = 0.5,
            Visibility = Visibility.Collapsed
        };
        panel.Children.Add(statusText);

        var dialog = new ContentDialog
        {
            Title = $"Mandatory Update - v{info.Version}",
            Content = panel,
            PrimaryButtonText = "Download & Install",
            CloseButtonText = "",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        while (true)
        {
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                // Download in-app
                dialog.IsPrimaryButtonEnabled = false;
                progressBar.Visibility = Visibility.Visible;
                statusText.Visibility = Visibility.Visible;
                statusText.Text = "Downloading update...";

                UpdateChecker.Instance.DownloadProgressChanged += (pct) =>
                {
                    DispatcherQueue?.TryEnqueue(() =>
                    {
                        progressBar.Value = pct;
                        statusText.Text = $"Downloading... {pct:F0}%";
                    });
                };

                var filePath = await UpdateChecker.Instance.DownloadUpdateAsync();
                if (filePath != null)
                {
                    statusText.Text = "Download complete! Installing update...";
                    await Task.Delay(500);
                    if (UpdateChecker.Instance.LaunchInstaller(silent: true))
                    {
                        statusText.Text = "Installer launched. App will restart automatically...";
                        await Task.Delay(3000);
                        // Graceful exit - installer has CloseApplications=yes as safety net
                        App.MainWindow = null;
                        _reallyClose = true;
                        this.Close();
                        System.Environment.Exit(0);
                    }
                    else
                    {
                        statusText.Text = "Could not launch installer. Opening download folder...";
                        var folder = System.IO.Path.GetDirectoryName(filePath);
                        if (folder != null)
                            await Windows.System.Launcher.LaunchFolderPathAsync(folder);
                    }
                    break;
                }
                else
                {
                    statusText.Text = "Download failed. Opening browser instead...";
                    dialog.IsPrimaryButtonEnabled = true;
                    progressBar.Visibility = Visibility.Collapsed;
                    await Task.Delay(1500);
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(info.DownloadUrl));
                    break;
                }
            }
        }
    }

    private async Task ShowOptionalUpdateDialog(UpdateInfo info)
    {
        // Notification
        NotificationService.Instance.Post(
            $"Update v{info.Version} Available",
            string.IsNullOrEmpty(info.ReleaseNotes) ? "A new version is available." : info.ReleaseNotes,
            NotificationType.Info
        );

        var panel = new StackPanel { Spacing = 12, Width = 400 };

        panel.Children.Add(new TextBlock
        {
            Text = $"You are running v{UpdateChecker.CurrentVersion}. Version {info.Version} is available!",
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
            Opacity = 0.7
        });

        if (!string.IsNullOrEmpty(info.ReleaseNotes))
        {
            panel.Children.Add(new TextBlock
            {
                Text = "What's new:",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 13
            });
            panel.Children.Add(new TextBlock
            {
                Text = info.ReleaseNotes,
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                Opacity = 0.6,
                FontSize = 12
            });
        }

        var progressBar = new ProgressBar
        {
            IsIndeterminate = false,
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 4, 0, 0)
        };
        panel.Children.Add(progressBar);

        var statusText = new TextBlock
        {
            Text = "",
            FontSize = 12,
            Opacity = 0.5,
            Visibility = Visibility.Collapsed
        };
        panel.Children.Add(statusText);

        var dialog = new ContentDialog
        {
            Title = $"Update Available - v{info.Version}",
            Content = panel,
            PrimaryButtonText = "Download & Install",
            SecondaryButtonText = "Skip This Version",
            CloseButtonText = "Later",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            // Download in-app
            dialog.IsPrimaryButtonEnabled = false;
            dialog.IsSecondaryButtonEnabled = false;
            progressBar.Visibility = Visibility.Visible;
            statusText.Visibility = Visibility.Visible;
            statusText.Text = "Downloading update...";

            // Show a new dialog for progress since the old one closed
            var progressPanel = new StackPanel { Spacing = 8, Width = 380 };
            var dlProgressBar = new ProgressBar
            {
                IsIndeterminate = false, Minimum = 0, Maximum = 100, Value = 0
            };
            var dlStatusText = new TextBlock
            {
                Text = "Downloading update...",
                FontSize = 12, Opacity = 0.6
            };
            progressPanel.Children.Add(dlStatusText);
            progressPanel.Children.Add(dlProgressBar);

            var progressDialog = new ContentDialog
            {
                Title = $"Downloading v{info.Version}",
                Content = progressPanel,
                CloseButtonText = "",
                XamlRoot = Content.XamlRoot
            };

            UpdateChecker.Instance.DownloadProgressChanged += (pct) =>
            {
                DispatcherQueue?.TryEnqueue(() =>
                {
                    dlProgressBar.Value = pct;
                    dlStatusText.Text = $"Downloading... {pct:F0}%";
                });
            };

            // Show progress dialog and download concurrently
            var downloadTask = UpdateChecker.Instance.DownloadUpdateAsync();
            _ = progressDialog.ShowAsync();

            var filePath = await downloadTask;

            // Close progress dialog
            progressDialog.Hide();

            if (filePath != null)
            {
                var installDialog = new ContentDialog
                {
                    Title = "Download Complete",
                    Content = "The update has been downloaded. Click Install Now to update silently. The app will close and restart automatically.",
                    PrimaryButtonText = "Install Now",
                    CloseButtonText = "Install Later",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = Content.XamlRoot
                };

                if (await installDialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    if (UpdateChecker.Instance.LaunchInstaller(silent: true))
                    {
                        // Wait for installer to start, then exit gracefully
                        // Installer has CloseApplications=yes as safety net
                        await Task.Delay(3000);
                        App.MainWindow = null;
                        _reallyClose = true;
                        this.Close();
                        System.Environment.Exit(0);
                    }
                }
            }
            else
            {
                // Fallback to browser
                var failDialog = new ContentDialog
                {
                    Title = "Download Failed",
                    Content = "The in-app download failed. Would you like to download from the browser instead?",
                    PrimaryButtonText = "Open Browser",
                    CloseButtonText = "Cancel",
                    XamlRoot = Content.XamlRoot
                };

                if (await failDialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(info.DownloadUrl));
                }
            }
        }
        else if (result == ContentDialogResult.Secondary)
        {
            // Skip this version
            UpdateChecker.Instance.SkipVersion(info.Version);
        }
        // else: Later - do nothing, will check again in 5 min
    }

    // ── NOTIFICATION BELL ───────────────────────────────────
    private void NotificationBell_Click(object sender, RoutedEventArgs e)
    {
        var ns = NotificationService.Instance;
        ns.MarkAllRead();

        var notifications = ns.GetAll();

        var flyout = new Flyout { Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.RightEdgeAlignedBottom };
        var panel = new StackPanel { Width = 340, Spacing = 4 };

        // Header
        var header = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock { Text = Services.S._("nav.notifications"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 16 });
        var clearBtn = new Button { Content = Services.S._("nav.clearAll"), Padding = new Thickness(8, 4, 8, 4), FontSize = 11 };
        clearBtn.Click += (s, ev) => { ns.Clear(); flyout.Hide(); };
        Grid.SetColumn(clearBtn, 1);
        header.Children.Add(clearBtn);
        panel.Children.Add(header);

        if (notifications.Count == 0)
        {
            panel.Children.Add(new TextBlock { Text = Services.S._("nav.noNotifications"), Opacity = 0.5, FontSize = 13 });
        }
        else
        {
            var scroll = new ScrollViewer { MaxHeight = 400 };
            var list = new StackPanel { Spacing = 4 };

            foreach (var n in notifications.Take(20))
            {
                var typeColor = n.Type switch
                {
                    NotificationType.Success => Windows.UI.Color.FromArgb(255, 46, 125, 50),
                    NotificationType.Warning => Windows.UI.Color.FromArgb(255, 230, 81, 0),
                    NotificationType.Error => Windows.UI.Color.FromArgb(255, 198, 40, 40),
                    _ => Windows.UI.Color.FromArgb(255, 33, 150, 243)
                };

                var card = new Border
                {
                    Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12, 8, 12, 8),
                    BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(40, typeColor.R, typeColor.G, typeColor.B)),
                    BorderThickness = new Thickness(0, 0, 0, 2)
                };

                var stack = new StackPanel { Spacing = 2 };
                var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

                var dot = new Border
                {
                    Width = 8, Height = 8, CornerRadius = new CornerRadius(4),
                    Background = new SolidColorBrush(typeColor),
                    VerticalAlignment = VerticalAlignment.Center
                };
                titleRow.Children.Add(dot);
                titleRow.Children.Add(new TextBlock { Text = n.Title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 12 });
                stack.Children.Add(titleRow);

                stack.Children.Add(new TextBlock { Text = n.Message, FontSize = 11, Opacity = 0.7, TextWrapping = TextWrapping.Wrap });
                stack.Children.Add(new TextBlock { Text = n.Timestamp.ToString("HH:mm:ss"), FontSize = 10, Opacity = 0.35 });

                card.Child = stack;
                list.Children.Add(card);
            }

            scroll.Content = list;
            panel.Children.Add(scroll);
        }

        flyout.Content = panel;
        flyout.ShowAt(NotificationBell);
    }
}

