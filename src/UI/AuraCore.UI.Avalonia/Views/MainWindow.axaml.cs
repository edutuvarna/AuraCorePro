using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using global::Avalonia.Threading;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.Desktop.Services.Navigation;
using AuraCore.Desktop.Services.Responsive;
using AuraCore.Domain.Enums;
using AuraCore.UI.Avalonia.Services.Update;
using AuraCore.UI.Avalonia.Views.Banners;
using AuraCore.UI.Avalonia.Views.Controls;
using AuraCore.UI.Avalonia.Views.Dialogs;
using AuraCore.UI.Avalonia.Views.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views;

public sealed partial class MainWindow : Window
{
    private readonly Dictionary<string, IOptimizationModule> _moduleMap = new();
    private readonly AuraCore.UI.Avalonia.ViewModels.SidebarViewModel _sidebarVm;

    // Phase 5.2.0 Task 11: privilege helper availability banner
    private readonly IHelperAvailabilityService? _helperAvailability;

    // Phase 5.3 Task 6: responsive narrow-mode service
    private readonly INarrowModeService? _narrowMode;

    // Phase 5.4 Task 11: navigation service for deep-links (e.g. Dashboard Smart Optimize)
    private readonly INavigationService? _nav;
    private EventHandler<NavigationRequestedEventArgs>? _navSectionRequestedHandler;

    public MainWindow()
    {
        InitializeComponent();

        // Resolve SidebarViewModel from DI (registered in App.axaml.cs Phase 3 block)
        try
        {
            _sidebarVm = App.Services.GetRequiredService<AuraCore.UI.Avalonia.ViewModels.SidebarViewModel>();
        }
        catch
        {
            // Design-time / test fallback: construct directly with default (Free) tier
            _sidebarVm = new AuraCore.UI.Avalonia.ViewModels.SidebarViewModel();
        }

        // Populate module map for view resolution (TweakListView / CategoryCleanView need IOptimizationModule)
        try
        {
            foreach (var m in App.Services.GetServices<IOptimizationModule>())
                _moduleMap[m.Id] = m;
        }
        catch { /* DI not available during design time */ }

        // Phase 5.2.0 Task 11: resolve helper availability service and wire banner visibility
        try
        {
            _helperAvailability = App.Services.GetRequiredService<IHelperAvailabilityService>();
            SyncBannerVisibility();
            _helperAvailability.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(IHelperAvailabilityService.IsBannerVisible) or null)
                    Dispatcher.UIThread.Post(SyncBannerVisibility);
            };
        }
        catch { /* DI / design-time fallback — banner stays hidden */ }

        // Phase 5.3 Task 6: resolve narrow-mode service and subscribe to window Bounds changes
        try
        {
            _narrowMode = App.Services?.GetService<INarrowModeService>();
            // Subscribe to Bounds changes — BoundsProperty is AvaloniaProperty<Rect> on Visual/Window.
            // Use GetObservable + a typed IObserver<Rect> wrapper (avoids hard System.Reactive dep).
            this.GetObservable(BoundsProperty)
                .Subscribe(new BoundsObserver(OnWindowBoundsChanged));
            // Push initial width (may be zero before first render; subsequent emissions will correct it)
            if (_narrowMode is NarrowModeService concreteInit)
                concreteInit.UpdateWidth(Bounds.Width);
        }
        catch { /* DI / design-time fallback — narrow mode stays at default wide state */ }

        // Phase 5.4 Task 11: subscribe to navigation service for Smart Optimize deep-link
        try
        {
            _nav = App.Services?.GetService<INavigationService>();
            if (_nav is not null)
            {
                _navSectionRequestedHandler = OnNavigationSectionRequested;
                _nav.SectionRequested += _navSectionRequestedHandler;
            }
        }
        catch { /* DI / design-time fallback — navigation deep-links unavailable */ }

        // Phase 6.1.D Task 15: subscribe to Loaded event to dispatch pending launch URL
        this.Loaded += MainWindow_Loaded;

        ApplyMainWindowLocalization();
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
                ApplyMainWindowLocalization();
                RebuildSidebar();
                RefreshUserChip();
            });

        // Status bar wiring — transient status from StatusBarService overrides
        // the CORTEX baseline; when the transient clears we fall back to
        // ambient state ("✦ Cortex · Active · Learning day N" / "Paused" / "Ready to start").
        StatusBarService.StatusChanged += text =>
            Dispatcher.UIThread.Post(() =>
            {
                GlobalStatusText.Text = string.IsNullOrEmpty(text)
                    ? FormatCortexStatus()
                    : text;
            });

        // Phase 3 Task 33: bind status bar baseline to CortexAmbientService
        try
        {
            if (App.Services.GetService<AuraCore.UI.Avalonia.Services.AI.ICortexAmbientService>() is { } ambient)
            {
                GlobalStatusText.Text = ambient.FormattedStatusText;
                ambient.PropertyChanged += (_, _) =>
                    Dispatcher.UIThread.Post(() => GlobalStatusText.Text = ambient.FormattedStatusText);
            }
        }
        catch { /* DI / design-time fallback — leaves hardcoded "Ready" */ }

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

    // Phase 5.4 Task 11: INavigationService subscription handler + cleanup

    private void OnNavigationSectionRequested(object? sender, NavigationRequestedEventArgs e)
    {
        if (e.SectionId.StartsWith("ai-", StringComparison.Ordinal))
        {
            var subId = e.SectionId.Substring("ai-".Length);
            // Navigate to the AI features page (mirrors the sidebar click path)
            NavigateToModule("ai-features");
            // Forward the sub-section to the view that is now active in ContentArea
            if (ContentArea.Content is Pages.AIFeaturesView aiView)
                aiView.ShowSection(subId);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine(
                $"[MainWindow.OnNavigationSectionRequested] unhandled section id: {e.SectionId}");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_nav is not null && _navSectionRequestedHandler is not null)
            _nav.SectionRequested -= _navSectionRequestedHandler;
        base.OnClosed(e);
    }

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
            var accent = cat.HasBadge
                ? FindBrush("AccentPurpleBrush", global::Avalonia.Media.Brushes.MediumPurple)
                : FindBrush("AccentTealBrush", global::Avalonia.Media.Brushes.Teal);

            NavPanel.Children.Add(CreateNavItem(cat.LocalizationKey, cat.Icon, accent,
                isActive: false,
                trailingChipText: cat.HasBadge ? cat.Badge : null,
                onClick: () => { _sidebarVm.ToggleCategory(catIdCapture); RebuildSidebar(); }));

            if (isExpanded)
            {
                foreach (var module in cat.Modules)
                {
                    var moduleIdCapture = module.Id;
                    var isLockedCapture = module.IsLocked;
                    NavPanel.Children.Add(CreateNavItem("  " + LocalizationService._(module.LocalizationKey), null, accent,
                        isActive: _sidebarVm.ActiveModuleId == moduleIdCapture,
                        trailingChipText: null,
                        isLocked: isLockedCapture,
                        onClick: () => OnSidebarItemClick(moduleIdCapture),
                        isLiteralLabel: true));
                }
            }
        }

        NavPanel.Children.Add(new Controls.SidebarSectionDivider { Label = LocalizationService._("nav.categoryAdvanced") });
        foreach (var item in _sidebarVm.VisibleAdvancedItems())
        {
            var moduleIdCapture = item.Id;
            var isLockedCapture = item.IsLocked;
            NavPanel.Children.Add(CreateNavItem(item.LocalizationKey, null,
                FindBrush("TextMutedBrush", global::Avalonia.Media.Brushes.Gray),
                isActive: _sidebarVm.ActiveModuleId == moduleIdCapture,
                trailingChipText: null,
                isLocked: isLockedCapture,
                onClick: () => OnSidebarItemClick(moduleIdCapture)));
        }
    }

    private Controls.SidebarNavItem CreateNavItem(
        string labelOrKey,
        string? iconResourceKey,
        global::Avalonia.Media.IBrush? accent,
        bool isActive,
        string? trailingChipText,
        Action onClick,
        bool isLiteralLabel = false,
        bool isLocked = false)
    {
        var item = new Controls.SidebarNavItem
        {
            Label = isLiteralLabel ? labelOrKey : LocalizationService._(labelOrKey),
            IsActive = isActive,
            IsLocked = isLocked,
            TrailingChipText = trailingChipText ?? string.Empty,
            Command = new RelayCommand(onClick),
        };
        if (iconResourceKey is not null)
        {
            var iconGeo = FindGeometry(iconResourceKey);
            if (iconGeo is not null) item.Icon = iconGeo;
        }
        if (accent is not null)
            item.AccentBrush = accent;
        return item;
    }

    /// <summary>
    /// Resolves a brush resource across theme variants + style dictionaries.
    /// Theme-scoped brushes (under ThemeDictionaries.Dark) aren't found by the default
    /// FindResource which assumes ThemeVariant.Default. Falls back to a safe brush.
    /// </summary>
    private global::Avalonia.Media.IBrush FindBrush(string key, global::Avalonia.Media.IBrush fallback)
    {
        // Prefer this window's actual theme variant; fall back to Dark (the app is dark-only in v1)
        var variant = this.ActualThemeVariant ?? global::Avalonia.Styling.ThemeVariant.Dark;
        if (this.TryFindResource(key, variant, out var v) && v is global::Avalonia.Media.IBrush b)
            return b;
        if (this.TryFindResource(key, global::Avalonia.Styling.ThemeVariant.Dark, out var v2) && v2 is global::Avalonia.Media.IBrush b2)
            return b2;
        if (global::Avalonia.Application.Current is { } app &&
            app.TryFindResource(key, global::Avalonia.Styling.ThemeVariant.Dark, out var v3) && v3 is global::Avalonia.Media.IBrush b3)
            return b3;
        return fallback;
    }

    /// <summary>
    /// CORTEX status bar baseline string. Re-resolves the ambient each call
    /// (cheap — singleton) so MainWindow doesn't need to cache a field.
    /// Used by StatusBarService.StatusChanged fallback when the transient clears.
    /// </summary>
    private string FormatCortexStatus()
    {
        try
        {
            var ambient = App.Services.GetService<AuraCore.UI.Avalonia.Services.AI.ICortexAmbientService>();
            return ambient?.FormattedStatusText ?? LocalizationService._("main.statusReady");
        }
        catch { return LocalizationService._("main.statusReady"); }
    }

    /// <summary>Resolves a Geometry resource (icons are theme-independent, in shared resources).</summary>
    private global::Avalonia.Media.Geometry? FindGeometry(string key)
    {
        if (this.TryFindResource(key, null, out var v) && v is global::Avalonia.Media.Geometry g)
            return g;
        if (global::Avalonia.Application.Current is { } app &&
            app.TryFindResource(key, null, out var v2) && v2 is global::Avalonia.Media.Geometry g2)
            return g2;
        return null;
    }

    // ─── LOCKED ITEM CLICK HANDLER ───────────────────────────────────

    private async void OnSidebarItemClick(string moduleKey)
    {
        // Find the module across both categories and advanced items
        var module = _sidebarVm.Categories
            .SelectMany(c => c.Modules)
            .Concat(_sidebarVm.AdvancedItems)
            .FirstOrDefault(m => m.Id == moduleKey);

        if (module?.IsLocked == true)
        {
            try
            {
                var tierService = App.Services.GetRequiredService<AuraCore.UI.Avalonia.Services.AI.ITierService>();
                var required = tierService.GetRequiredTier(moduleKey);
                var dialog = new Views.Dialogs.TierUpgradePlaceholderDialog(moduleKey, required);
                await dialog.ShowDialog(this);
            }
            catch
            {
                // Silently swallow if DI not available (design time / tests)
            }
            return;
        }

        _sidebarVm.NavigateTo(moduleKey);
        SetActiveContent(moduleKey);
        RebuildSidebar();
    }

    private void SetActiveContent(string moduleId)
    {
        ContentArea.Content = moduleId switch
        {
            "dashboard" => new Pages.DashboardView(),
            "ai-features" => App.Services.GetRequiredService<global::AuraCore.UI.Avalonia.Views.Pages.AIFeaturesView>(),
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
            // Phase 5.1.10: font-manager soft-hidden; falls through to Dashboard default.
            "context-menu" => CreateTweakListView("context-menu"),
            "taskbar-tweaks" => CreateTweakListView("taskbar-tweaks"),
            "explorer-tweaks" => CreateTweakListView("explorer-tweaks"),
            "autorun-manager" => new Pages.AutorunManagerView(),
            "wake-on-lan" => new Pages.WakeOnLanView(),
            "system-health" => new Pages.SystemHealthView(),
            "admin-panel" => new Pages.AdminPanelView(),
            "storage-compression" => new Pages.GenericModuleView(), // placeholder — feature dev deferred
            "journal-cleaner" => CreateJournalCleanerView(),
            "snap-flatpak-cleaner" => CreateSnapFlatpakCleanerView(),
            "docker-cleaner" => CreateDockerCleanerView(),
            "kernel-cleaner" => CreateKernelCleanerView(),
            "linux-app-installer" => CreateLinuxAppInstallerView(),
            "grub-manager" => CreateGrubManagerView(),
            "dns-flusher" => CreateDnsFlusherView(),
            "purgeable-space-manager" => CreatePurgeableSpaceView(),
            "spotlight-manager" => CreateSpotlightManagerView(),
            "xcode-cleaner" => CreateXcodeCleanerView(),
            "mac-app-installer" => CreateMacAppInstallerView(),
            _ => new Pages.DashboardView(),
        };
    }

    private UserControl CreateCategoryCleanView(string moduleId)
    {
        _moduleMap.TryGetValue(moduleId, out var module);
        return new Pages.CategoryCleanView(module);
    }

    private UserControl CreateJournalCleanerView()
    {
        var v = new Pages.JournalCleanerView();
        try
        {
            v.DataContext = App.Services.GetRequiredService<AuraCore.UI.Avalonia.ViewModels.JournalCleanerViewModel>();
        }
        catch { /* design-time fallback: Loaded handler will try again */ }
        return v;
    }

    private UserControl CreateSnapFlatpakCleanerView()
    {
        var v = new Pages.SnapFlatpakCleanerView();
        try
        {
            v.DataContext = App.Services.GetRequiredService<AuraCore.UI.Avalonia.ViewModels.SnapFlatpakCleanerViewModel>();
        }
        catch { /* design-time fallback: Loaded handler will try again */ }
        return v;
    }

    private UserControl CreateDockerCleanerView()
    {
        var v = new Pages.DockerCleanerView();
        try
        {
            v.DataContext = App.Services.GetRequiredService<AuraCore.UI.Avalonia.ViewModels.DockerCleanerViewModel>();
        }
        catch { /* design-time fallback: Loaded handler will try again */ }
        return v;
    }

    private UserControl CreateKernelCleanerView()
    {
        var v = new Pages.KernelCleanerView();
        try
        {
            v.DataContext = App.Services.GetRequiredService<AuraCore.UI.Avalonia.ViewModels.KernelCleanerViewModel>();
        }
        catch { /* design-time fallback: Loaded handler will try again */ }
        return v;
    }

    private UserControl CreateLinuxAppInstallerView()
    {
        var v = new Pages.LinuxAppInstallerView();
        try
        {
            v.DataContext = App.Services.GetRequiredService<AuraCore.UI.Avalonia.ViewModels.LinuxAppInstallerViewModel>();
        }
        catch { /* design-time fallback: Loaded handler will try again */ }
        return v;
    }

    private UserControl CreateGrubManagerView()
    {
        var v = new Pages.GrubManagerView();
        try
        {
            v.DataContext = App.Services.GetRequiredService<AuraCore.UI.Avalonia.ViewModels.GrubManagerViewModel>();
        }
        catch { /* design-time fallback: Loaded handler will try again */ }
        return v;
    }

    private UserControl CreateDnsFlusherView()
    {
        var v = new Pages.DnsFlusherView();
        try
        {
            v.DataContext = App.Services.GetRequiredService<AuraCore.UI.Avalonia.ViewModels.DnsFlusherViewModel>();
        }
        catch { /* design-time fallback: Loaded handler will try again */ }
        return v;
    }

    private UserControl CreatePurgeableSpaceView()
    {
        var v = new Pages.PurgeableSpaceManagerView();
        try
        {
            v.DataContext = App.Services.GetRequiredService<AuraCore.UI.Avalonia.ViewModels.PurgeableSpaceManagerViewModel>();
        }
        catch { /* design-time fallback: Loaded handler will try again */ }
        return v;
    }

    private UserControl CreateSpotlightManagerView()
    {
        var v = new Pages.SpotlightManagerView();
        try
        {
            v.DataContext = App.Services.GetRequiredService<AuraCore.UI.Avalonia.ViewModels.SpotlightManagerViewModel>();
        }
        catch { /* design-time fallback: Loaded handler will try again */ }
        return v;
    }

    private UserControl CreateXcodeCleanerView()
    {
        var v = new Pages.XcodeCleanerView();
        try
        {
            v.DataContext = App.Services.GetRequiredService<AuraCore.UI.Avalonia.ViewModels.XcodeCleanerViewModel>();
        }
        catch { /* design-time fallback: Loaded handler will try again */ }
        return v;
    }

    private UserControl CreateMacAppInstallerView()
    {
        var v = new Pages.MacAppInstallerView();
        try
        {
            v.DataContext = App.Services.GetRequiredService<AuraCore.UI.Avalonia.ViewModels.MacAppInstallerViewModel>();
        }
        catch { /* design-time fallback: Loaded handler will try again */ }
        return v;
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

    // ─── MAIN WINDOW LOCALIZATION ────────────────────────────────────

    private void ApplyMainWindowLocalization()
    {
        Title = LocalizationService._("login.title");
        SettingsBtn.Content = LocalizationService._("main.settingsButton");
        // Set baseline status text (will be overridden by CortexAmbientService if available)
        GlobalStatusText.Text = FormatCortexStatus();
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

    // ─── RESPONSIVE NARROW MODE (Phase 5.3 Task 6) ──────────────────

    private void OnWindowBoundsChanged(Rect bounds)
    {
        if (_narrowMode is NarrowModeService concrete)
            concrete.UpdateWidth(bounds.Width);
    }

    // ─── PRIVILEGE BANNER HANDLERS (Phase 5.2.0 Task 11) ────────────

    private void SyncBannerVisibility()
    {
        if (this.FindControl<PrivilegeHelperMissingBanner>("PrivilegeMissingBanner") is { } banner)
            banner.IsVisible = _helperAvailability?.IsBannerVisible ?? false;
    }

    private async void OnPrivilegeInstallNowClicked(object? sender, EventArgs e)
    {
        // Phase 5.2.1 Task 22: resolve installer (may be null on Windows/macOS — coordinator handles it)
        var installer = App.Services?.GetService<AuraCore.Infrastructure.PrivilegeIpc.Linux.PrivHelperInstaller>();
        var availability = App.Services?.GetService<IHelperAvailabilityService>();

        if (availability is null)
        {
            System.Diagnostics.Debug.WriteLine("[privilege] IHelperAvailabilityService not resolvable; aborting install");
            return;
        }

        var outcome = await PrivilegeInstallCoordinator.RunInstallFlowAsync(installer, availability);
        if (outcome.Success)
        {
            System.Diagnostics.Debug.WriteLine("[privilege] install succeeded: " + outcome.Stdout.Trim());
        }
        else
        {
            System.Diagnostics.Debug.WriteLine(
                $"[privilege] install failed (exit={outcome.ExitCode}): {outcome.Stderr.Trim()}");
        }
    }

    private void OnPrivilegeDismissClicked(object? sender, EventArgs e)
    {
        _helperAvailability?.DismissBanner();
    }

    private sealed class RelayCommand : System.Windows.Input.ICommand
    {
        private readonly Action _action;
        public RelayCommand(Action action) => _action = action;
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _action();
    }

    /// <summary>
    /// Minimal IObserver&lt;Rect&gt; wrapper so we can subscribe to
    /// GetObservable(BoundsProperty) without a System.Reactive dependency.
    /// </summary>
    private sealed class BoundsObserver : IObserver<Rect>
    {
        private readonly Action<Rect> _onNext;
        public BoundsObserver(Action<Rect> onNext) => _onNext = onNext;
        public void OnNext(Rect value) => _onNext(value);
        public void OnError(Exception error) { /* no-op */ }
        public void OnCompleted() { /* no-op */ }
    }

    // ─── DEEP-LINK LAUNCH URL DISPATCH (Phase 6.1.D Task 15) ──────────

    private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        // Phase 6.1.D — dispatch pending launch-URL (if we were opened via auracore://).
        var pendingUrl = App.PendingLaunchUrl;
        App.PendingLaunchUrl = null; // consume once
        if (!string.IsNullOrEmpty(pendingUrl))
        {
            try
            {
                var knownSections = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal)
                {
                    "dashboard", "settings", "disk-health",
                    "ai-recommendations", "ai-insights", "ai-schedule"
                };
                var intent = AuraCore.UI.Avalonia.Helpers.UrlSchemeHandler.Parse(
                    pendingUrl, knownSections, AuraCore.UI.Avalonia.Helpers.ModuleIdsRegistry.All);

                if (intent is not null)
                {
                    var nav = App.Services?.GetService(typeof(AuraCore.Application.Interfaces.Platform.INavigationService))
                        as AuraCore.Application.Interfaces.Platform.INavigationService;
                    nav?.NavigateTo(intent.Id);
                }
            }
            catch { /* deep-link failure must not crash main window */ }
        }

        // Phase 6.6.G — subscribe to UpdateChecker.UpdateFound to show banner or mandatory dialog.
        try
        {
            UpdateChecker.Instance.UpdateFound += OnUpdateFound;
        }
        catch { /* non-fatal — update banner stays hidden */ }
    }

    // ─── UPDATE BANNER / MANDATORY DIALOG (Phase 6.6.G) ─────────────

    private void OnUpdateFound(UpdateInfo info)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (info.IsMandatory)
                ShowMandatoryDialog(info);
            else
                ShowBanner(info);
        });
    }

    private void ShowBanner(UpdateInfo info)
    {
        var banner = this.FindControl<UpdateBanner>("UpdateBanner");
        if (banner is null) return;

        var text = banner.FindControl<TextBlock>("BannerText");
        if (text is not null)
            text.Text = string.Format(LocalizationService.Get("UpdateBanner_Message"), info.Version);

        var updateBtn = banner.FindControl<Button>("UpdateBtn");
        var laterBtn  = banner.FindControl<Button>("LaterBtn");

        if (updateBtn is not null)
            updateBtn.Click += async (_, _) => await StartDownloadFlow(info);
        if (laterBtn is not null)
            laterBtn.Click += (_, _) => { banner.IsVisible = false; };

        banner.IsVisible = true;
    }

    private async void ShowMandatoryDialog(UpdateInfo info)
    {
        try
        {
            var dialog = new MandatoryUpdateDialog();

            var msgText = dialog.FindControl<TextBlock>("MessageText");
            if (msgText is not null)
                msgText.Text = string.Format(LocalizationService.Get("UpdateBanner_Mandatory_Message"), info.Version);

            var updateBtn = dialog.FindControl<Button>("UpdateBtn");
            if (updateBtn is not null)
                updateBtn.Click += async (_, _) =>
                {
                    updateBtn.IsEnabled = false;
                    await StartDownloadFlow(info);
                };

            await dialog.ShowDialog(this);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UpdateFlow] Mandatory dialog failed: {ex.Message}");
        }
    }

    private async Task StartDownloadFlow(UpdateInfo info)
    {
        try
        {
            var downloader = App.Services.GetRequiredService<IUpdateDownloader>();
            var avail = new AvailableUpdate(info.Version, info.DownloadUrl, info.SignatureHash, info.IsMandatory);
            var progress = new Progress<double>(_ => { /* progress bar wiring is future polish */ });
            var path = await downloader.DownloadAsync(avail, progress, CancellationToken.None);
            downloader.InstallAndExit(path);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UpdateFlow] Download failed: {ex.Message}");
            // Non-fatal: banner stays visible for retry
        }
    }
}
