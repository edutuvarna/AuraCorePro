using AuraCore.Desktop.Services;
using AuraCore.Desktop.Services.Scheduler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace AuraCore.Desktop;

public partial class App : Microsoft.UI.Xaml.Application
{
    private Window? m_window;
    private static ServiceProvider? _services;
    private static BackgroundScheduler? _scheduler;
    private static GameWatcher? _gameWatcher;

    public static new App Current => (App)Microsoft.UI.Xaml.Application.Current;
    public IServiceProvider Services => _services!;
    public static BackgroundScheduler? Scheduler => _scheduler;
    public static GameWatcher? GameWatcher => _gameWatcher;
    public static Window? MainWindow { get; set; }

    public App()
    {
        InitializeComponent();
        // Hook crash reporting before anything else
        CrashReportService.Initialize(this);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var sc = new ServiceCollection();

        // ── Cross-platform modules (register on ALL platforms) ──
        AuraCore.Module.HostsEditor.HostsEditorRegistration.AddHostsEditorModule(sc);

        // ── Windows-only modules ──
        if (OperatingSystem.IsWindows())
        {
            // System & Health
            AuraCore.Module.SystemHealth.SystemHealthRegistration.AddSystemHealthModule(sc);

            // Cleaning & Optimization
            AuraCore.Module.JunkCleaner.JunkCleanerRegistration.AddJunkCleanerModule(sc);
            AuraCore.Module.RamOptimizer.RamOptimizerRegistration.AddRamOptimizerModule(sc);
            AuraCore.Module.StorageCompression.StorageCompressionRegistration.AddStorageCompressionModule(sc);
            AuraCore.Module.RegistryOptimizer.RegistryOptimizerRegistration.AddRegistryOptimizerModule(sc);
            AuraCore.Module.BloatwareRemoval.BloatwareRemovalRegistration.AddBloatwareRemovalModule(sc);
            AuraCore.Module.DiskCleanup.DiskCleanupRegistration.AddDiskCleanupModule(sc);
            AuraCore.Module.PrivacyCleaner.PrivacyCleanerRegistration.AddPrivacyCleanerModule(sc);

            // Performance & Network
            AuraCore.Module.NetworkOptimizer.NetworkOptimizerRegistration.AddNetworkOptimizerModule(sc);
            AuraCore.Module.GamingMode.GamingModeRegistration.AddGamingModeModule(sc);

            // Shell & Customization
            AuraCore.Module.ContextMenu.ContextMenuRegistration.AddContextMenuModule(sc);
            AuraCore.Module.TaskbarTweaks.TaskbarTweaksRegistration.AddTaskbarTweaksModule(sc);
            AuraCore.Module.ExplorerTweaks.ExplorerTweaksRegistration.AddExplorerTweaksModule(sc);

            // Tools
            AuraCore.Module.AppInstaller.AppInstallerRegistration.AddAppInstallerModule(sc);
            AuraCore.Module.DefenderManager.DefenderManagerRegistration.AddDefenderManagerModule(sc);
            AuraCore.Module.DriverUpdater.DriverUpdaterRegistration.AddDriverUpdaterModule(sc);
            AuraCore.Module.BatteryOptimizer.BatteryOptimizerRegistration.AddBatteryOptimizerModule(sc);
            AuraCore.Module.AutorunManager.AutorunManagerRegistration.AddAutorunManagerModule(sc);
            AuraCore.Module.ProcessMonitor.ProcessMonitorRegistration.AddProcessMonitorModule(sc);
        }

        // ── Linux-only modules (future) ──
        // if (OperatingSystem.IsLinux())
        // {
        //     // sc.AddSystemdManagerModule();
        //     // sc.AddPackageCleanerModule();
        //     // sc.AddSwapOptimizerModule();
        //     // sc.AddCronManagerModule();
        // }

        // ── macOS-only modules (future) ──
        // if (OperatingSystem.IsMacOS())
        // {
        //     // sc.AddDefaultsOptimizerModule();
        //     // sc.AddLaunchAgentManagerModule();
        //     // sc.AddBrewManagerModule();
        // }

        // Guards
        AuraCore.Guard.Licensing.LicensingRegistration.AddLicensingGuard(sc);

        _services = sc.BuildServiceProvider();

        // Load language preference before any UI
        S.Load();

        m_window = new LoginWindow();
        m_window.Activate();
    }

    /// <summary>Called by MainWindow after login to initialize the scheduler</summary>
    public static void InitScheduler(Microsoft.UI.Dispatching.DispatcherQueue dispatcher)
    {
        if (_scheduler is not null) return;
        _scheduler = new BackgroundScheduler(_services!, dispatcher);
        // Auto-start if there are enabled schedules
        var schedules = ScheduleStore.Load();
        if (schedules.Any(s => s.Enabled))
            _scheduler.Start();

        // Initialize GameWatcher (auto-starts if was previously enabled)
        if (_gameWatcher is null)
            _gameWatcher = new GameWatcher(_services!, dispatcher);
    }
}
