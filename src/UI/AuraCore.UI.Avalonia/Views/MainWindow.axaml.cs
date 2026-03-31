using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
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
        OptimizationCategory.SystemHealth        => "\u2665",  // heart
        OptimizationCategory.DiskCleanup         => "\u2702",  // scissors (clean)
        OptimizationCategory.MemoryOptimization  => "\u25A0",  // square (chip)
        OptimizationCategory.RegistryOptimization=> "\u2692",  // hammer & pick
        OptimizationCategory.StorageCompression  => "\u25CB",  // circle
        OptimizationCategory.BloatwareRemoval    => "\u2716",  // heavy X
        OptimizationCategory.NetworkOptimization => "\u2B24",  // black circle
        OptimizationCategory.GamingPerformance   => "\u2B50",  // star
        OptimizationCategory.ShellCustomization  => "\u2699",  // gear
        OptimizationCategory.ApplicationManagement=> "\u2B07", // down arrow
        OptimizationCategory.Privacy             => "\u2BD1",  // shield (fallback to lock below if missing)
        OptimizationCategory.AutorunManagement   => "\u26A1",  // lightning
        OptimizationCategory.ProcessManagement   => "\u2630",  // trigram (list)
        OptimizationCategory.NetworkTools        => "\u260E",  // telephone
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
                ContentArea.Content = new DashboardView();
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
    }

    private void BuildNavigation()
    {
        var modules = App.Services.GetServices<IOptimizationModule>().ToList();
        foreach (var m in modules) _moduleMap[m.Id] = m;

        // Dashboard button (always first)
        var dashBtn = MakeNavButton("dashboard", "\u2302", LocalizationService._("nav.dashboard"));
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
                Text = LocalizationService._(labelKey), FontSize = 10, FontWeight = global::Avalonia.Media.FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse("#555570")),
                Margin = new global::Avalonia.Thickness(12, 16, 0, 6)
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
                Text = LocalizationService._("nav.other"), FontSize = 10, FontWeight = global::Avalonia.Media.FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse("#555570")),
                Margin = new global::Avalonia.Thickness(12, 16, 0, 6)
            });
            foreach (var m in remaining)
                NavPanel.Children.Add(MakeNavButton(m.Id, "\u2699", m.DisplayName));
        }

        // Standalone pages (not DI modules)
        NavPanel.Children.Add(new TextBlock
        {
            Text = LocalizationService._("nav.tools"), FontSize = 10, FontWeight = global::Avalonia.Media.FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#555570")),
            Margin = new global::Avalonia.Thickness(12, 16, 0, 6)
        });
        NavPanel.Children.Add(MakeNavButton("disk-health", "\u2764", LocalizationService._("nav.diskHealth")));
        NavPanel.Children.Add(MakeNavButton("space-analyzer", "\u25CE", LocalizationService._("nav.spaceAnalyzer")));
        NavPanel.Children.Add(MakeNavButton("startup-optimizer", "\u26A1", LocalizationService._("nav.startupOptimizer")));
        NavPanel.Children.Add(MakeNavButton("service-manager", "\u2699", LocalizationService._("nav.serviceManager")));
        NavPanel.Children.Add(MakeNavButton("scheduler", "\u23F0", LocalizationService._("nav.autoSchedule")));
        NavPanel.Children.Add(MakeNavButton("recommendations", "\u2605", LocalizationService._("nav.aiRecommendations")));

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
                Text = LocalizationService._("nav.linuxTools"), FontSize = 10, FontWeight = global::Avalonia.Media.FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse("#555570")),
                Margin = new global::Avalonia.Thickness(12, 16, 0, 6)
            });
            NavPanel.Children.Add(MakeNavButton("systemd-manager", "\u2699", LocalizationService._("nav.systemdManager")));
            NavPanel.Children.Add(MakeNavButton("package-cleaner", "\u267B", LocalizationService._("nav.packageCleaner")));
            NavPanel.Children.Add(MakeNavButton("swap-optimizer", "\u26A1", LocalizationService._("nav.swapOptimizer")));
            NavPanel.Children.Add(MakeNavButton("cron-manager", "\u23F0", LocalizationService._("nav.cronManager")));
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

        var stack = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, Spacing = 10 };
        stack.Children.Add(new TextBlock
        {
            Text = icon, FontSize = 14,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        });
        stack.Children.Add(new TextBlock
        {
            Text = label,
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
                    && sp.Children[1] is TextBlock tb && !tb.Text!.EndsWith(" \u1F512"))
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
