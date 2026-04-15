using System;
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
    private readonly Dictionary<string, IOptimizationModule> _moduleMap = new();
    private readonly AuraCore.UI.Avalonia.ViewModels.SidebarViewModel _sidebarVm = new();

    public MainWindow()
    {
        InitializeComponent();

        // Populate module map for view resolution (TweakListView / CategoryCleanView need IOptimizationModule)
        try
        {
            foreach (var m in App.Services.GetServices<IOptimizationModule>())
                _moduleMap[m.Id] = m;
        }
        catch { /* DI not available during design time */ }

        BuildNavigation();
        RefreshUserChip();

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

        // Rebuild sidebar on language change
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                RebuildSidebar();
                RefreshUserChip();
            });

        // Status bar wiring
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

    // ─── NAVIGATION (SidebarViewModel-driven) ────────────────────────

    /// <summary>Public entry so other components (e.g., Dashboard) can navigate.</summary>
    public void NavigateToModule(string moduleId)
    {
        _sidebarVm.NavigateTo(moduleId);
        SetActiveContent(moduleId);
        RebuildSidebar();
    }

    private void BuildNavigation() => RebuildSidebar();

    private void RebuildSidebar()
    {
        NavPanel.Children.Clear();

        // Dashboard pinned at top
        NavPanel.Children.Add(CreateNavItem("nav.dashboard", "IconDashboard", null,
            isActive: _sidebarVm.ActiveModuleId == "dashboard",
            trailingChipText: null,
            onClick: () => { _sidebarVm.NavigateTo("dashboard"); SetActiveContent("dashboard"); RebuildSidebar(); }));

        foreach (var cat in _sidebarVm.VisibleCategories())
        {
            var catIdCapture = cat.Id;
            var isExpanded = _sidebarVm.ExpandedCategoryId == cat.Id;
            var accent = cat.IsAccent
                ? (global::Avalonia.Media.IBrush)this.FindResource("AccentPurpleBrush")!
                : (global::Avalonia.Media.IBrush)this.FindResource("AccentTealBrush")!;

            NavPanel.Children.Add(CreateNavItem(cat.LocalizationKey, cat.Icon, accent,
                isActive: false,
                trailingChipText: cat.IsAccent ? "CORTEX" : null,
                onClick: () => { _sidebarVm.ToggleCategory(catIdCapture); RebuildSidebar(); }));

            if (isExpanded)
            {
                foreach (var module in cat.Modules)
                {
                    var moduleIdCapture = module.Id;
                    NavPanel.Children.Add(CreateNavItem("  " + LocalizationService._(module.LocalizationKey), null, accent,
                        isActive: _sidebarVm.ActiveModuleId == moduleIdCapture,
                        trailingChipText: null,
                        onClick: () => { _sidebarVm.NavigateTo(moduleIdCapture); SetActiveContent(moduleIdCapture); RebuildSidebar(); },
                        isLiteralLabel: true));
                }
            }
        }

        NavPanel.Children.Add(new Controls.SidebarSectionDivider { Label = LocalizationService._("nav.categoryAdvanced") });
        foreach (var item in _sidebarVm.VisibleAdvancedItems())
        {
            var moduleIdCapture = item.Id;
            NavPanel.Children.Add(CreateNavItem(item.LocalizationKey, null,
                (global::Avalonia.Media.IBrush)this.FindResource("TextMutedBrush")!,
                isActive: _sidebarVm.ActiveModuleId == moduleIdCapture,
                trailingChipText: null,
                onClick: () => { _sidebarVm.NavigateTo(moduleIdCapture); SetActiveContent(moduleIdCapture); RebuildSidebar(); }));
        }
    }

    private Controls.SidebarNavItem CreateNavItem(
        string labelOrKey,
        string? iconResourceKey,
        global::Avalonia.Media.IBrush? accent,
        bool isActive,
        string? trailingChipText,
        Action onClick,
        bool isLiteralLabel = false)
    {
        var item = new Controls.SidebarNavItem
        {
            Label = isLiteralLabel ? labelOrKey : LocalizationService._(labelOrKey),
            IsActive = isActive,
            TrailingChipText = trailingChipText ?? string.Empty,
            Command = new RelayCommand(onClick),
        };
        if (iconResourceKey is not null)
            item.Icon = (global::Avalonia.Media.Geometry)this.FindResource(iconResourceKey)!;
        if (accent is not null)
            item.AccentBrush = accent;
        return item;
    }

    private void SetActiveContent(string moduleId)
    {
        ContentArea.Content = moduleId switch
        {
            "dashboard" => new Pages.DashboardView(),
            "ai-insights" => new Pages.AIInsightsView(),
            "ai-recommendations" => new Pages.RecommendationsView(),
            "ai-chat" => new Pages.AIChatView(),
            "auto-schedule" => new Pages.SchedulerView(),
            _ => CreateModuleView(moduleId),
        };
    }

    private UserControl CreateModuleView(string moduleId)
    {
        return moduleId switch
        {
            "ram-optimizer" => new Pages.RamOptimizerView(),
            "startup-optimizer" => new Pages.StartupOptimizerView(),
            "network-optimizer" => new Pages.NetworkOptimizerView(),
            "battery-optimizer" => new Pages.BatteryOptimizerView(),
            "junk-cleaner" => CreateCategoryCleanView("junk-cleaner"),
            "disk-cleanup" => CreateCategoryCleanView("disk-cleanup"),
            "privacy-cleaner" => CreateCategoryCleanView("privacy-cleaner"),
            "registry-cleaner" => new Pages.RegistryOptimizerView(),
            "bloatware-removal" => new Pages.BloatwareRemovalView(),
            "app-installer" => new Pages.AppInstallerView(),
            "gaming-mode" => new Pages.GamingModeView(),
            "defender-manager" => new Pages.DefenderManagerView(),
            "firewall-rules" => new Pages.FirewallRulesView(),
            "file-shredder" => new Pages.FileShredderView(),
            "hosts-editor" => new Pages.HostsEditorView(),
            "driver-updater" => new Pages.DriverUpdaterView(),
            "service-manager" => new Pages.ServiceManagerView(),
            "iso-builder" => new Pages.IsoBuilderView(),
            "disk-health" => new Pages.DiskHealthView(),
            "space-analyzer" => new Pages.SpaceAnalyzerView(),
            "registry-deep" => new Pages.RegistryOptimizerView(),
            "environment-variables" => new Pages.EnvironmentVariablesView(),
            "symlink-manager" => new Pages.SymlinkManagerView(),
            "process-monitor" => new Pages.ProcessMonitorView(),
            "font-manager" => new Pages.FontManagerView(),
            "context-menu" => CreateTweakListView("context-menu"),
            "taskbar-tweaks" => CreateTweakListView("taskbar-tweaks"),
            "explorer-tweaks" => CreateTweakListView("explorer-tweaks"),
            "autorun-manager" => new Pages.AutorunManagerView(),
            "wake-on-lan" => new Pages.WakeOnLanView(),
            _ => new Pages.DashboardView(),
        };
    }

    private UserControl CreateCategoryCleanView(string moduleId)
    {
        _moduleMap.TryGetValue(moduleId, out var module);
        return new Pages.CategoryCleanView(module);
    }

    private UserControl CreateTweakListView(string moduleId)
    {
        return _moduleMap.TryGetValue(moduleId, out var module)
            ? new Pages.TweakListView(module)
            : new Pages.TweakListView();
    }

    private void Settings_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        _sidebarVm.NavigateTo("settings");
        ContentArea.Content = new Pages.SettingsView();
        RebuildSidebar();
    }

    // ─── USER CHIP / SESSION REFRESH ─────────────────────────────────

    private void RefreshUserChip()
    {
        var email = SessionState.UserEmail;
        var tier = (SessionState.UserTier ?? "free").ToUpper();
        var isAdmin = SessionState.IsAdmin;
        var tierText = isAdmin ? "ADMIN" : tier;

        UserChipHost.Email = string.IsNullOrEmpty(email) ? "Guest" : email;
        UserChipHost.Role = tierText;
        UserChipHost.StatusText = SessionState.IsAuthenticated ? "Signed in" : "Not signed in";

        if (!string.IsNullOrEmpty(email))
        {
            var parts = email.Split('@')[0].Split('.');
            var initials = parts.Length >= 2
                ? $"{char.ToUpper(parts[0][0])}{char.ToUpper(parts[1][0])}"
                : $"{char.ToUpper(parts[0][0])}";
            UserChipHost.AvatarInitial = initials;
        }
        else
        {
            UserChipHost.AvatarInitial = "G";
        }
    }

    private static SubscriptionTier GetCurrentTier()
        => Enum.TryParse<SubscriptionTier>(SessionState.UserTier, true, out var t)
            ? t : SubscriptionTier.Free;

    /// <summary>Called after login/upgrade to refresh UI.</summary>
    public void RefreshSession()
    {
        RefreshUserChip();
        RebuildSidebar();
    }

    private sealed class RelayCommand : System.Windows.Input.ICommand
    {
        private readonly Action _action;
        public RelayCommand(Action action) => _action = action;
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _action();
    }
}
