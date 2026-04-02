using System.Diagnostics;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using global::Avalonia.Threading;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using AuraCore.UI.Avalonia.Views.Dialogs;
using AuraCore.UI.Avalonia.Views.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views;

public sealed partial class MainWindow : Window
{
    private Button? _activeNav;
    private readonly Dictionary<string, IOptimizationModule> _moduleMap = new();

    // Category display order + labels (localization keys)
    private static readonly (string LabelKey, OptimizationCategory[] Cats)[] Sections = new[]
    {
        ("nav.overview", new[] { OptimizationCategory.SystemHealth }),
        ("nav.optimization", new[] {
            OptimizationCategory.DiskCleanup,
            OptimizationCategory.MemoryOptimization,
            OptimizationCategory.StorageCompression,
            OptimizationCategory.RegistryOptimization,
            OptimizationCategory.BloatwareRemoval,
            OptimizationCategory.Privacy
        }),
        ("nav.performance", new[] {
            OptimizationCategory.NetworkOptimization,
            OptimizationCategory.GamingPerformance
        }),
        ("nav.customization", new[] {
            OptimizationCategory.ShellCustomization,
            OptimizationCategory.ApplicationManagement
        }),
        ("nav.advancedTools", new[] {
            OptimizationCategory.AutorunManagement,
            OptimizationCategory.ProcessManagement,
            OptimizationCategory.NetworkTools
        }),
    };

    // Category -> icon mapping
    // Segoe Fluent Icons (Windows) / fallback ASCII symbols
    private static string CatIcon(OptimizationCategory c) => c switch
    {
        OptimizationCategory.SystemHealth        => "\u2661",  // ♡ heart outline
        OptimizationCategory.DiskCleanup         => "\u2737",  // ✷ star burst (clean)
        OptimizationCategory.MemoryOptimization  => "\u2B1A",  // ⬚ dotted square
        OptimizationCategory.RegistryOptimization=> "\u2692",  // ⚒ hammer & pick
        OptimizationCategory.StorageCompression  => "\u2B21",  // ⬡ hexagon
        OptimizationCategory.BloatwareRemoval    => "\u2716",  // ✖ heavy X
        OptimizationCategory.NetworkOptimization => "\u25C9",  // ◉ fisheye
        OptimizationCategory.GamingPerformance   => "\u2B50",  // ⭐ star
        OptimizationCategory.ShellCustomization  => "\u2699",  // ⚙ gear
        OptimizationCategory.ApplicationManagement=> "\u25A3", // ▣ white square with small black square
        OptimizationCategory.Privacy             => "\u2616",  // ☖ white shogi piece (shield-like)
        OptimizationCategory.AutorunManagement   => "\u26A1",  // ⚡ lightning
        OptimizationCategory.ProcessManagement   => "\u2630",  // ☰ trigram (list)
        OptimizationCategory.NetworkTools        => "\u2301",  // ⌁ electric arrow
        _ => "\u2699"
    };

    public MainWindow()
    {
        InitializeComponent();

        // Platform label
        PlatformLabel.Text = OperatingSystem.IsLinux() ? "Linux"
                           : OperatingSystem.IsMacOS() ? "macOS" : "Windows";

        BuildNavigation();
        UpdateTierBadge();
        ApplyTierLocking();

        // Show onboarding on first run, otherwise Dashboard
        if (!OnboardingView.IsCompleted)
        {
            var onboarding = new OnboardingView();
            onboarding.OnboardingCompleted += (s, e) =>
            {
                if (!AIConsentSettings.HasBeenShown())
                {
                    var consent = new AIConsentDialog();
                    consent.ConsentCompleted += (_, _) =>
                    {
                        ContentArea.Content = new DashboardView();
                    };
                    ContentArea.Content = consent;
                }
                else
                {
                    ContentArea.Content = new DashboardView();
                }
            };
            ContentArea.Content = onboarding;
        }
        else
        {
            ContentArea.Content = new DashboardView();
        }

        // Localize settings button
        SettingsNavLabel.Text = LocalizationService._("nav.settings");

        // Rebuild sidebar on language change
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                NavPanel.Children.Clear();
                _activeNav = null;
                BuildNavigation();
                UpdateTierBadge();
                ApplyTierLocking();
                SettingsNavLabel.Text = LocalizationService._("nav.settings");
            });

        // ── Status bar wiring ────────────────────────────
        StatusBarService.StatusChanged += text =>
            Dispatcher.UIThread.Post(() => GlobalStatusText.Text = text);

        StatusBarService.ProgressChanged += (op, fraction) =>
            Dispatcher.UIThread.Post(() =>
            {
                if (fraction < 0 || string.IsNullOrEmpty(op))
                {
                    GlobalProgressText.Text = "";
                    GlobalProgressBarBorder.IsVisible = false;
                    GlobalProgressBarFill.Width = 0;
                }
                else
                {
                    GlobalProgressText.Text = op;
                    GlobalProgressBarBorder.IsVisible = true;
                    GlobalProgressBarFill.Width = 80 * Math.Clamp(fraction, 0, 1);
                }
            });

        // Memory usage timer (updates every 5 seconds)
        var memTimer = new global::Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        memTimer.Tick += (_, _) => UpdateMemoryDisplay();
        memTimer.Start();
        UpdateMemoryDisplay();
    }

    private void UpdateMemoryDisplay()
    {
        try
        {
            var proc = Process.GetCurrentProcess();
            var usedMb = proc.WorkingSet64 / (1024.0 * 1024);
            GlobalMemoryText.Text = $"RAM: {usedMb:F0} MB";
        }
        catch
        {
            GlobalMemoryText.Text = "";
        }
    }

    private void BuildNavigation()
    {
        // Modules merged into other views (hide from sidebar)
        var hiddenModules = new HashSet<string> { "network-monitor", "dns-benchmark" };
        var modules = App.Services.GetServices<IOptimizationModule>()
            .Where(m => !hiddenModules.Contains(m.Id)).ToList();
        foreach (var m in modules) _moduleMap[m.Id] = m;

        // Dashboard button (always first)
        var dashBtn = MakeNavButton("dashboard", "\u25C6", LocalizationService._("nav.dashboard"));
        dashBtn.Classes.Add("active");
        _activeNav = dashBtn;
        NavPanel.Children.Add(dashBtn);

        // Group modules by section
        var placed = new HashSet<string>();
        foreach (var (labelKey, cats) in Sections)
        {
            var sectionModules = modules
                .Where(m => cats.Contains(m.Category) && !placed.Contains(m.Id))
                .ToList();
            if (sectionModules.Count == 0) continue;

            // Section header
            NavPanel.Children.Add(new TextBlock
            {
                Text = LocalizationService._(labelKey).ToUpper(), FontSize = 8,
                FontWeight = global::Avalonia.Media.FontWeight.Bold,
                Foreground = new SolidColorBrush(Color.Parse("#3A3A50")),
                Margin = new global::Avalonia.Thickness(12, 12, 0, 4)
            });

            foreach (var m in sectionModules)
            {
                var icon = CatIcon(m.Category);
                NavPanel.Children.Add(MakeNavButton(m.Id, icon, m.DisplayName));
                placed.Add(m.Id);
            }
        }

        // Any uncategorized modules
        var remaining = modules.Where(m => !placed.Contains(m.Id)).ToList();
        if (remaining.Count > 0)
        {
            NavPanel.Children.Add(new TextBlock
            {
                Text = LocalizationService._("nav.other").ToUpper(), FontSize = 8,
                FontWeight = global::Avalonia.Media.FontWeight.Bold,
                Foreground = new SolidColorBrush(Color.Parse("#3A3A50")),
                Margin = new global::Avalonia.Thickness(12, 12, 0, 4)
            });
            foreach (var m in remaining)
                NavPanel.Children.Add(MakeNavButton(m.Id, "\u2699", m.DisplayName));
        }

        // Standalone pages (not DI modules)
        NavPanel.Children.Add(new TextBlock
        {
            Text = LocalizationService._("nav.tools").ToUpper(), FontSize = 8,
            FontWeight = global::Avalonia.Media.FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.Parse("#3A3A50")),
            Margin = new global::Avalonia.Thickness(12, 12, 0, 4)
        });
        NavPanel.Children.Add(MakeNavButton("disk-health", "\u2661", LocalizationService._("nav.diskHealth")));
        NavPanel.Children.Add(MakeNavButton("space-analyzer", "\u25C9", LocalizationService._("nav.spaceAnalyzer")));
        NavPanel.Children.Add(MakeNavButton("startup-optimizer", "\u26A1", LocalizationService._("nav.startupOptimizer")));
        NavPanel.Children.Add(MakeNavButton("service-manager", "\u2699", LocalizationService._("nav.serviceManager")));
        NavPanel.Children.Add(MakeNavButton("scheduler", "\u25F4", LocalizationService._("nav.autoSchedule")));
        NavPanel.Children.Add(MakeNavButton("recommendations", "\u2726", LocalizationService._("nav.aiRecommendations")));
        NavPanel.Children.Add(MakeNavButton("ai-insights", "\u25C8", LocalizationService._("nav.aiInsights")));

        // ISO Builder (Windows-only)
        if (OperatingSystem.IsWindows())
        {
            NavPanel.Children.Add(MakeNavButton("iso-builder", "\u25CE", LocalizationService._("nav.isoBuilder")));
        }

        // Linux-only tools
        if (OperatingSystem.IsLinux())
        {
            NavPanel.Children.Add(new TextBlock
            {
                Text = LocalizationService._("nav.linuxTools").ToUpper(), FontSize = 8,
                FontWeight = global::Avalonia.Media.FontWeight.Bold,
                Foreground = new SolidColorBrush(Color.Parse("#3A3A50")),
                Margin = new global::Avalonia.Thickness(12, 12, 0, 4)
            });
            NavPanel.Children.Add(MakeNavButton("systemd-manager", "\u2699", LocalizationService._("nav.systemdManager")));
            NavPanel.Children.Add(MakeNavButton("package-cleaner", "\u267B", LocalizationService._("nav.packageCleaner")));
            NavPanel.Children.Add(MakeNavButton("swap-optimizer", "\u26A1", LocalizationService._("nav.swapOptimizer")));
            NavPanel.Children.Add(MakeNavButton("cron-manager", "\u23F0", LocalizationService._("nav.cronManager")));
        }

        // macOS-only tools
        if (OperatingSystem.IsMacOS())
        {
            NavPanel.Children.Add(new TextBlock
            {
                Text = LocalizationService._("nav.macosTools").ToUpper(), FontSize = 8,
                FontWeight = global::Avalonia.Media.FontWeight.Bold,
                Foreground = new SolidColorBrush(Color.Parse("#3A3A50")),
                Margin = new global::Avalonia.Thickness(12, 12, 0, 4)
            });
            NavPanel.Children.Add(MakeNavButton("defaults-optimizer", "\u2699", LocalizationService._("nav.defaultsOptimizer")));
            NavPanel.Children.Add(MakeNavButton("launchagent-manager", "\u26A1", LocalizationService._("nav.launchAgentManager")));
            NavPanel.Children.Add(MakeNavButton("brew-manager", "\u267B", LocalizationService._("nav.brewManager")));
            NavPanel.Children.Add(MakeNavButton("timemachine-manager", "\u23F0", LocalizationService._("nav.timeMachineManager")));
        }

        // Admin Panel (admin only)
        if (SessionState.IsAdmin)
        {
            NavPanel.Children.Add(MakeNavButton("admin-panel", "\u2692", LocalizationService._("nav.adminPanel")));
        }
    }

    private Button MakeNavButton(string tag, string icon, string label)
    {
        var btn = new Button { Tag = tag };
        btn.Classes.Add("nav-item");
        btn.Click += Nav_Click;

        var stack = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
        stack.Children.Add(new TextBlock
        {
            Text = icon, FontSize = 12, Width = 16,
            TextAlignment = global::Avalonia.Media.TextAlignment.Center,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            Opacity = 0.6
        });
        stack.Children.Add(new TextBlock
        {
            Text = label, FontSize = 11,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        });
        btn.Content = stack;
        return btn;
    }

    private void Nav_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var tag = btn.Tag?.ToString();

        // Update active state
        if (_activeNav is not null) _activeNav.Classes.Remove("active");
        btn.Classes.Add("active");
        _activeNav = btn;

        // Navigate to appropriate view
        if (tag == "dashboard")
        {
            ContentArea.Content = new DashboardView();
            return;
        }

        if (tag == "settings")
        {
            ContentArea.Content = new SettingsView();
            return;
        }

        // Standalone pages (not DI modules)
        switch (tag)
        {
            case "disk-health":        ContentArea.Content = new DiskHealthView(); return;
            case "space-analyzer":     ContentArea.Content = new SpaceAnalyzerView(); return;
            case "startup-optimizer":  ContentArea.Content = new StartupOptimizerView(); return;
            case "service-manager":    ContentArea.Content = new ServiceManagerView(); return;
            case "scheduler":          ContentArea.Content = new SchedulerView(); return;
            case "recommendations":    ContentArea.Content = new RecommendationsView(); return;
            case "ai-insights":        ContentArea.Content = new AIInsightsView(); return;
            case "iso-builder":        ContentArea.Content = new IsoBuilderView(); return;
            case "admin-panel":        ContentArea.Content = new AdminPanelView(); return;
        }

        // Module views
        if (_moduleMap.TryGetValue(tag!, out var module))
        {
            // Tier check — redirect to upgrade if locked
            var userTier = GetCurrentTier();
            if (tag != null && !TierFeatures.IsModuleAllowed(tag, userTier))
            {
                ContentArea.Content = new UpgradeView(tag, module.DisplayName);
                return;
            }

            ContentArea.Content = tag switch
            {
                // Dedicated views
                "system-health"      => new SystemHealthView(),
                "process-monitor"    => new ProcessMonitorView(),
                "hosts-editor"       => new HostsEditorView(),
                "autorun-manager"    => new AutorunManagerView(),
                "gaming-mode"        => new GamingModeView(),
                "driver-updater"     => new DriverUpdaterView(),
                "bloatware-removal"  => new BloatwareRemovalView(),
                "ram-optimizer"      => new RamOptimizerView(),
                "defender-manager"   => new DefenderManagerView(),
                "network-optimizer"  => new NetworkOptimizerView(),
                "registry-optimizer" => new RegistryOptimizerView(),
                "battery-optimizer"  => new BatteryOptimizerView(),
                // New modules (Session 18)
                "environment-variables" => new EnvironmentVariablesView(),
                "firewall-rules"     => new FirewallRulesView(),
                "symlink-manager"    => new SymlinkManagerView(),
                "file-shredder"      => new FileShredderView(),
                // Linux-only modules
                "systemd-manager"    => new SystemdManagerView(),
                "package-cleaner"    => new PackageCleanerView(),
                "swap-optimizer"     => new SwapOptimizerView(),
                "cron-manager"       => new CronManagerView(),
                // macOS-only modules
                "defaults-optimizer" => new DefaultsOptimizerView(),
                "launchagent-manager"=> new LaunchAgentManagerView(),
                "brew-manager"       => new BrewManagerView(),
                "timemachine-manager"=> new TimeMachineManagerView(),
                // New Windows modules
                "font-manager"       => new FontManagerView(),
                "wake-on-lan"        => new WakeOnLanView(),
                // App Installer (dedicated view)
                "app-installer"      => new AppInstallerView(),
                // Tweak toggle list (shared view)
                "context-menu"       => new TweakListView(module),
                "taskbar-tweaks"     => new TweakListView(module),
                "explorer-tweaks"    => new TweakListView(module),
                // Category cleanup (shared view)
                "junk-cleaner"       => new CategoryCleanView(module),
                "disk-cleanup"       => new CategoryCleanView(module),
                "privacy-cleaner"    => new CategoryCleanView(module),
                // Smart generic (StorageCompression, AppInstaller, etc)
                _ => new ScanOptimizeView(module)
            };
        }
    }

    // ── TIER BADGE + LOCKING ──────────────────────────────

    private void UpdateTierBadge()
    {
        var email = SessionState.UserEmail;
        var tier = (SessionState.UserTier ?? "free").ToUpper();
        var isAdmin = SessionState.IsAdmin;

        UserEmailLabel.Text = string.IsNullOrEmpty(email) ? "Guest" : email;

        // Update avatar initials
        if (!string.IsNullOrEmpty(email) && email.Length > 0)
        {
            var parts = email.Split('@')[0].Split('.');
            var initials = parts.Length >= 2
                ? $"{char.ToUpper(parts[0][0])}{char.ToUpper(parts[1][0])}"
                : $"{char.ToUpper(parts[0][0])}";
            UserAvatarInitials.Text = initials;
        }
        else
        {
            UserAvatarInitials.Text = "G";
        }

        UserStatusLabel.Text = SessionState.IsAuthenticated ? "Signed in" : "Not signed in";

        var tierText = isAdmin ? "ADMIN" : tier;
        TierBadgeText.Text = tierText;

        var (color, bg) = tierText switch
        {
            "ADMIN"      => ("#F59E0B", "#20F59E0B"),
            "ENTERPRISE" => ("#8B5CF6", "#208B5CF6"),
            "PRO"        => ("#00D4AA", "#2000D4AA"),
            _            => ("#8888A0", "#208888A0")
        };
        TierBadgeText.Foreground = new SolidColorBrush(Color.Parse(color));
        TierBadge.Background = new SolidColorBrush(Color.Parse(bg));
    }

    private void ApplyTierLocking()
    {
        var userTier = GetCurrentTier();
        foreach (var child in NavPanel.Children)
        {
            if (child is not Button btn) continue;
            var tag = btn.Tag?.ToString();
            if (string.IsNullOrEmpty(tag) || tag == "dashboard") continue;

            if (!TierFeatures.IsModuleAllowed(tag, userTier))
            {
                btn.Opacity = 0.5;
                // Add lock icon to label
                if (btn.Content is StackPanel sp && sp.Children.Count >= 2
                    && sp.Children[1] is TextBlock tb && !tb.Text!.EndsWith(" \u26BF"))
                {
                    tb.Text += " \u26BF"; // lock symbol
                }
            }
            else
            {
                btn.Opacity = 1.0;
            }
        }
    }

    private static SubscriptionTier GetCurrentTier()
        => Enum.TryParse<SubscriptionTier>(SessionState.UserTier, true, out var t)
            ? t : SubscriptionTier.Free;

    /// <summary>Called after login/upgrade to refresh UI</summary>
    public void RefreshSession()
    {
        UpdateTierBadge();
        ApplyTierLocking();
    }
}
