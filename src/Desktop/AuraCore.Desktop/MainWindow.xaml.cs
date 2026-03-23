using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using AuraCore.Application;
using AuraCore.Desktop.Pages;
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
        ThemeService.Initialize(Content as FrameworkElement ?? (FrameworkElement)Content);

        // Initialize background scheduler
        App.InitScheduler(DispatcherQueue);

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
                var headerText = header.Content?.ToString() ?? "";
                if (headerText is "Optimization" or "Optimizasyon")
                    header.Content = Services.S._("nav.optimization");
                else if (headerText is "Customization" or "Kişiselleştirme")
                    header.Content = Services.S._("nav.customization");
                else if (headerText is "Tools" or "Araçlar")
                    header.Content = Services.S._("nav.tools");
            }
        }

        // Localize built-in Settings nav item
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

    // ── KEYBOARD SHORTCUTS ────────────────────────────────────
    // Ctrl+1=Dashboard, Ctrl+2=SystemHealth, Ctrl+3=JunkCleaner, Ctrl+4=RAM,
    // Ctrl+5=Storage, Ctrl+6=Registry, Ctrl+7=Network, Ctrl+8=AppInstaller,
    // Ctrl+9=Bloatware, Ctrl+G=GamingMode, F5=Refresh, Ctrl+,=Settings

    private void Shortcut_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        var key = sender.Key;
        var ctrl = sender.Modifiers.HasFlag(Windows.System.VirtualKeyModifiers.Control);

        Type? page = null;

        if (ctrl)
        {
            page = key switch
            {
                Windows.System.VirtualKey.Number1 => typeof(DashboardPage),
                Windows.System.VirtualKey.Number2 => typeof(SystemHealthPage),
                Windows.System.VirtualKey.Number3 => typeof(JunkCleanerPage),
                Windows.System.VirtualKey.Number4 => typeof(RamOptimizerPage),
                Windows.System.VirtualKey.Number5 => typeof(StoragePage),
                Windows.System.VirtualKey.Number6 => typeof(RegistryPage),
                Windows.System.VirtualKey.Number7 => typeof(NetworkPage),
                Windows.System.VirtualKey.Number8 => typeof(AppInstallerPage),
                Windows.System.VirtualKey.Number9 => typeof(BloatwarePage),
                Windows.System.VirtualKey.G => typeof(GamingModePage),
                _ => null
            };
        }
        else if (key == Windows.System.VirtualKey.F5)
        {
            // Refresh: re-navigate to current page
            if (ContentFrame.CurrentSourcePageType is not null)
            {
                var current = ContentFrame.CurrentSourcePageType;
                ContentFrame.Navigate(current);
            }
            return;
        }

        if (page is null) return;

        // Check tier locking
        var tag = page.Name switch
        {
            nameof(StoragePage) => "storage-compression",
            nameof(RegistryPage) => "registry-optimizer",
            nameof(BloatwarePage) => "bloatware-removal",
            nameof(GamingModePage) => "gaming-mode",
            _ => ""
        };

        if (!string.IsNullOrEmpty(tag) && !TierFeatures.IsModuleAllowed(tag, GetCurrentTier()))
        {
            ContentFrame.Navigate(typeof(UpgradePage));
            return;
        }

        ContentFrame.Navigate(page);

        // Try to sync the nav selection
        foreach (var item in NavView.MenuItems.OfType<NavigationViewItem>())
        {
            var itemTag = GetTagForPageType(page);
            if (item.Tag?.ToString() == itemTag)
            {
                NavView.SelectedItem = item;
                break;
            }
        }
    }

    private static string GetTagForPageType(Type pageType) => pageType.Name switch
    {
        nameof(DashboardPage) => "dashboard",
        nameof(SystemHealthPage) => "system-health",
        nameof(JunkCleanerPage) => "junk-cleaner",
        nameof(RamOptimizerPage) => "ram-optimizer",
        nameof(StoragePage) => "storage-compression",
        nameof(RegistryPage) => "registry-optimizer",
        nameof(BloatwarePage) => "bloatware-removal",
        nameof(NetworkPage) => "network-optimizer",
        nameof(GamingModePage) => "gaming-mode",
        nameof(AppInstallerPage) => "app-installer",
        nameof(SpaceAnalyzerPage) => "space-analyzer",
        nameof(DiskHealthPage) => "disk-health",
        nameof(StartupOptimizerPage) => "startup-optimizer",
        nameof(SettingsPage) => "settings",
        _ => ""
    };
}
