using global::Avalonia.Controls.ApplicationLifetimes;
using global::Avalonia.Markup.Xaml;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.Desktop.Services.Navigation;
using AuraCore.Desktop.Services.PrivilegeIpc;
using AuraCore.Desktop.Services.Responsive;
using AuraCore.Infrastructure.PrivilegeIpc;
using AuraCore.Module.ServiceManager;
using AuraCore.UI.Avalonia.Helpers;
using AuraCore.UI.Avalonia.Views;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia;

public partial class App : global::Avalonia.Application
{
    private static IServiceProvider? _services;
    public static IServiceProvider Services { get => _services!; internal set => _services = value; }

    /// <summary>
    /// Phase 5.3 Task 9: App-level singleton accessor for the narrow-mode service.
    /// Resolved after DI build; null-safe when DI is not initialized (test harness).
    /// StatRow subscribes to this via code-behind INPC to reflow Columns 4→2→1.
    /// </summary>
    public static INarrowModeService? NarrowMode { get; private set; }

    // Phase 6.1.D — URL scheme launch-path state (stashed by Program.Main before Avalonia starts).
    internal static string? PendingLaunchUrl { get; set; }
    internal static AuraCore.UI.Avalonia.Helpers.InstanceMutex? SingletonLock { get; set; }
    internal static AuraCore.UI.Avalonia.Helpers.UrlGatewayServer? UrlGateway { get; set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var sc = new ServiceCollection();


        // ── Phase 5.2.1: Privilege IPC + helper availability ──
        // IShellCommandService is required by the three migrated Linux modules
        // (SnapFlatpakCleaner, LinuxAppInstaller, GrubManager) injected via ctor.
        // IHelperAvailabilityService drives the PrivilegeHelperMissingBanner in MainWindow.
        sc.AddPrivilegeIpc();
        sc.AddSingleton<IHelperAvailabilityService, HelperAvailabilityService>();

        // ── Phase 5.3: Narrow-mode responsive service ──
        sc.AddSingleton<INarrowModeService, NarrowModeService>();

        // ── Phase 5.4: Navigation service (Dashboard Smart Optimize deep-link) ──
        sc.AddSingleton<INavigationService, NavigationService>();

        // ── Cross-platform modules (Windows + Linux + macOS) ──
        AuraCore.Module.HostsEditor.HostsEditorRegistration.AddHostsEditorModule(sc);
        AuraCore.Module.SystemHealth.SystemHealthRegistration.AddSystemHealthModule(sc);
        AuraCore.Module.JunkCleaner.JunkCleanerRegistration.AddJunkCleanerModule(sc);
        AuraCore.Module.RamOptimizer.RamOptimizerRegistration.AddRamOptimizerModule(sc);
        AuraCore.Module.ProcessMonitor.ProcessMonitorRegistration.AddProcessMonitorModule(sc);
        AuraCore.Module.FileShredder.FileShredderRegistration.AddFileShredderModule(sc);
        AuraCore.Module.SymlinkManager.SymlinkManagerRegistration.AddSymlinkManagerModule(sc);

        // ── Windows-only modules ──
        if (OperatingSystem.IsWindows())
        {
            AuraCore.Module.StorageCompression.StorageCompressionRegistration.AddStorageCompressionModule(sc);
            AuraCore.Module.RegistryOptimizer.RegistryOptimizerRegistration.AddRegistryOptimizerModule(sc);
            AuraCore.Module.BloatwareRemoval.BloatwareRemovalRegistration.AddBloatwareRemovalModule(sc);
            AuraCore.Module.NetworkOptimizer.NetworkOptimizerRegistration.AddNetworkOptimizerModule(sc);
            AuraCore.Module.GamingMode.GamingModeRegistration.AddGamingModeModule(sc);
            AuraCore.Module.AppInstaller.AppInstallerRegistration.AddAppInstallerModule(sc);
            AuraCore.Module.ContextMenu.ContextMenuRegistration.AddContextMenuModule(sc);
            AuraCore.Module.TaskbarTweaks.TaskbarTweaksRegistration.AddTaskbarTweaksModule(sc);
            AuraCore.Module.ExplorerTweaks.ExplorerTweaksRegistration.AddExplorerTweaksModule(sc);
            AuraCore.Module.DiskCleanup.DiskCleanupRegistration.AddDiskCleanupModule(sc);
            AuraCore.Module.DefenderManager.DefenderManagerRegistration.AddDefenderManagerModule(sc);
            AuraCore.Module.PrivacyCleaner.PrivacyCleanerRegistration.AddPrivacyCleanerModule(sc);
            AuraCore.Module.DriverUpdater.DriverUpdaterRegistration.AddDriverUpdaterModule(sc);
            AuraCore.Module.BatteryOptimizer.BatteryOptimizerRegistration.AddBatteryOptimizerModule(sc);
            AuraCore.Module.AutorunManager.AutorunManagerRegistration.AddAutorunManagerModule(sc);
            AuraCore.Module.EnvironmentVariables.EnvironmentVariablesRegistration.AddEnvironmentVariablesModule(sc);
            AuraCore.Module.FirewallRules.FirewallRulesRegistration.AddFirewallRulesModule(sc);
            AuraCore.Module.NetworkMonitor.NetworkMonitorRegistration.AddNetworkMonitorModule(sc);
            AuraCore.Module.DnsBenchmark.DnsBenchmarkRegistration.AddDnsBenchmarkModule(sc);
            AuraCore.Module.FontManager.FontManagerRegistration.AddFontManagerModule(sc);
            AuraCore.Module.WakeOnLan.WakeOnLanRegistration.AddWakeOnLanModule(sc);
            // ── Phase 5.5 Task 10: Service Manager ──
            sc.AddServiceManager();

            // ── Task A2: Windows named-pipe privileged helper installer ──
            sc.AddSingleton<IPipeProbe>(new NamedPipeProbe("AuraCorePro"));
            sc.AddSingleton<PrivilegedHelperInstaller>(sp =>
                new PrivilegedHelperInstaller(
                    sp.GetRequiredService<IPipeProbe>(),
                    PrivilegedHelperInstaller.DefaultElevatorInvoke));
        }

        // ── Linux-only modules (Faz 2+) ──
        if (OperatingSystem.IsLinux())
        {
            AuraCore.Module.SystemdManager.SystemdManagerRegistration.AddSystemdManagerModule(sc);
            AuraCore.Module.PackageCleaner.PackageCleanerRegistration.AddPackageCleanerModule(sc);
            AuraCore.Module.SwapOptimizer.SwapOptimizerRegistration.AddSwapOptimizerModule(sc);
            AuraCore.Module.CronManager.CronManagerRegistration.AddCronManagerModule(sc);
            // Journal Cleaner (Phase 4.3.1): register concrete first so the VM can inject
            // JournalCleanerModule directly, then alias it to IOptimizationModule so the
            // engine-wide multi-binding (_moduleMap in MainWindow) also sees it.
            sc.AddSingleton<AuraCore.Module.JournalCleaner.JournalCleanerModule>();
            sc.AddSingleton<AuraCore.Application.Interfaces.Modules.IOptimizationModule>(
                sp => sp.GetRequiredService<AuraCore.Module.JournalCleaner.JournalCleanerModule>());
            // Kernel Cleaner (Phase 4.3.4): same concrete + IOptimizationModule alias
            // pattern as JournalCleaner / SnapFlatpakCleaner above — lets the VM inject
            // the concrete module while keeping the engine-wide _moduleMap binding intact.
            sc.AddSingleton<AuraCore.Module.KernelCleaner.KernelCleanerModule>();
            sc.AddSingleton<AuraCore.Application.Interfaces.Modules.IOptimizationModule>(
                sp => sp.GetRequiredService<AuraCore.Module.KernelCleaner.KernelCleanerModule>());
            // Linux App Installer (Phase 4.3.5): same concrete + IOptimizationModule alias
            // pattern as JournalCleaner / SnapFlatpakCleaner / KernelCleaner above — lets the
            // VM inject the concrete module while keeping the engine-wide _moduleMap binding.
            sc.AddSingleton<AuraCore.Module.LinuxAppInstaller.LinuxAppInstallerModule>();
            sc.AddSingleton<AuraCore.Application.Interfaces.Modules.IOptimizationModule>(
                sp => sp.GetRequiredService<AuraCore.Module.LinuxAppInstaller.LinuxAppInstallerModule>());
            // Snap/Flatpak Cleaner (Phase 4.3.2): same concrete + IOptimizationModule alias
            // pattern as JournalCleaner above — lets the VM inject the concrete module
            // (with its additive Last* count properties) while keeping the engine-wide
            // _moduleMap binding intact.
            sc.AddSingleton<AuraCore.Module.SnapFlatpakCleaner.SnapFlatpakCleanerModule>();
            sc.AddSingleton<AuraCore.Application.Interfaces.Modules.IOptimizationModule>(
                sp => sp.GetRequiredService<AuraCore.Module.SnapFlatpakCleaner.SnapFlatpakCleanerModule>());
            // GRUB Manager (Phase 4.3.6): same concrete + IOptimizationModule alias
            // pattern as JournalCleaner / SnapFlatpakCleaner / KernelCleaner / LinuxAppInstaller
            // above — lets the VM inject the concrete module while keeping the
            // engine-wide _moduleMap binding intact.
            sc.AddSingleton<AuraCore.Module.GrubManager.GrubManagerModule>();
            sc.AddSingleton<AuraCore.Application.Interfaces.Modules.IOptimizationModule>(
                sp => sp.GetRequiredService<AuraCore.Module.GrubManager.GrubManagerModule>());
        }

        // ── macOS-only modules (Faz 3) ──
        if (OperatingSystem.IsMacOS())
        {
            AuraCore.Module.DefaultsOptimizer.DefaultsOptimizerRegistration.AddDefaultsOptimizerModule(sc);
            AuraCore.Module.LaunchAgentManager.LaunchAgentManagerRegistration.AddLaunchAgentManagerModule(sc);
            AuraCore.Module.BrewManager.BrewManagerRegistration.AddBrewManagerModule(sc);
            AuraCore.Module.TimeMachineManager.TimeMachineManagerRegistration.AddTimeMachineManagerModule(sc);
            // Xcode Cleaner (Phase 4.4.4): replaces the old single-line
            // AddXcodeCleanerModule registration — same concrete +
            // IOptimizationModule alias pattern as 4.3.1-4.4.3 so the VM can
            // inject the concrete module while keeping the engine-wide _moduleMap
            // binding intact.
            sc.AddSingleton<AuraCore.Module.XcodeCleaner.XcodeCleanerModule>();
            sc.AddSingleton<AuraCore.Application.Interfaces.Modules.IOptimizationModule>(
                sp => sp.GetRequiredService<AuraCore.Module.XcodeCleaner.XcodeCleanerModule>());
            // DNS Flusher (Phase 4.4.1): same concrete + IOptimizationModule alias
            // pattern as JournalCleaner / SnapFlatpakCleaner / KernelCleaner / LinuxAppInstaller
            // / GrubManager above — lets the VM inject the concrete module while keeping the
            // engine-wide _moduleMap binding intact.
            sc.AddSingleton<AuraCore.Module.DnsFlusher.DnsFlusherModule>();
            sc.AddSingleton<AuraCore.Application.Interfaces.Modules.IOptimizationModule>(
                sp => sp.GetRequiredService<AuraCore.Module.DnsFlusher.DnsFlusherModule>());
            // Purgeable Space Manager (Phase 4.4.2): replaces the old single-line
            // AddPurgeableSpaceManagerModule registration — same concrete +
            // IOptimizationModule alias pattern as 4.3.1-4.4.1 so the VM can
            // inject the concrete module while keeping the engine-wide _moduleMap
            // binding intact.
            sc.AddSingleton<AuraCore.Module.PurgeableSpaceManager.PurgeableSpaceManagerModule>();
            sc.AddSingleton<AuraCore.Application.Interfaces.Modules.IOptimizationModule>(
                sp => sp.GetRequiredService<AuraCore.Module.PurgeableSpaceManager.PurgeableSpaceManagerModule>());
            // Spotlight Manager (Phase 4.4.3): replaces the old single-line
            // AddSpotlightManagerModule registration — same concrete +
            // IOptimizationModule alias pattern as 4.3.1-4.4.2 so the VM can
            // inject the concrete module while keeping the engine-wide _moduleMap
            // binding intact.
            sc.AddSingleton<AuraCore.Module.SpotlightManager.SpotlightManagerModule>();
            sc.AddSingleton<AuraCore.Application.Interfaces.Modules.IOptimizationModule>(
                sp => sp.GetRequiredService<AuraCore.Module.SpotlightManager.SpotlightManagerModule>());
            // Mac App Installer (Phase 4.4.5): replaces the old single-line
            // AddMacAppInstallerModule registration — same concrete +
            // IOptimizationModule alias pattern as 4.3.1-4.4.4 so the VM can
            // inject the concrete module while keeping the engine-wide _moduleMap
            // binding intact.
            sc.AddSingleton<AuraCore.Module.MacAppInstaller.MacAppInstallerModule>();
            sc.AddSingleton<AuraCore.Application.Interfaces.Modules.IOptimizationModule>(
                sp => sp.GetRequiredService<AuraCore.Module.MacAppInstaller.MacAppInstallerModule>());
        }

        // ── Linux + macOS shared modules ──
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            // Docker Cleaner (Phase 4.3.3): concrete first for VM injection, alias to IOptimizationModule.
            sc.AddSingleton<AuraCore.Module.DockerCleaner.DockerCleanerModule>();
            sc.AddSingleton<AuraCore.Application.Interfaces.Modules.IOptimizationModule>(
                sp => sp.GetRequiredService<AuraCore.Module.DockerCleaner.DockerCleanerModule>());
        }

        // ── AI Analyzer Engine ──
        AuraCore.Engine.AIAnalyzer.AIAnalyzerRegistration.AddAIAnalyzer(sc);

        // Drives the AI Analyzer engine: samples CPU/RAM/Disk every 2s and
        // triggers AnalyzeAsync every 60s. Without this service the engine
        // is registered but never receives samples — Cortex Insights UI
        // would stay on the "Cortex is learning" placeholder indefinitely.
        sc.AddSingleton<global::AuraCore.UI.Avalonia.Services.AI.AIMetricsCollectorService>();

        // ── Phase 3: AI Features services ──
        // Model catalog + installed models (singletons — read-only / cross-session state)
        sc.AddSingleton<global::AuraCore.UI.Avalonia.Services.AI.IModelCatalog,
                        global::AuraCore.UI.Avalonia.Services.AI.ModelCatalog>();
        sc.AddSingleton<global::AuraCore.UI.Avalonia.Services.AI.IInstalledModelStore>(
            sp => new global::AuraCore.UI.Avalonia.Services.AI.InstalledModelStore(
                sp.GetRequiredService<global::AuraCore.UI.Avalonia.Services.AI.IModelCatalog>()));

        // App settings (single instance — loaded from disk at startup)
        sc.AddSingleton<global::AuraCore.UI.Avalonia.AppSettings>(
            _ => global::AuraCore.UI.Avalonia.AppSettings.Load());

        // Ambient CORTEX state aggregator
        sc.AddSingleton<global::AuraCore.UI.Avalonia.Services.AI.ICortexAmbientService,
                        global::AuraCore.UI.Avalonia.Services.AI.CortexAmbientService>();

        // Tier service for sidebar IsLocked
        sc.AddSingleton<global::AuraCore.UI.Avalonia.Services.AI.ITierService,
                        global::AuraCore.UI.Avalonia.Services.AI.TierService>();

        // HttpClient for model downloads — configured with User-Agent to bypass Bot Fight Mode
        sc.AddSingleton<global::System.Net.Http.HttpClient>(_ =>
        {
            var client = new global::System.Net.Http.HttpClient
            {
                Timeout = TimeSpan.FromMinutes(30),
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AuraCorePro/1.0 (+https://auracore.pro)");
            return client;
        });

        // Download settings (consumed by ModelDownloadService)
        sc.AddSingleton<global::AuraCore.UI.Avalonia.Services.AI.ModelDownloadSettings>(_ =>
            new global::AuraCore.UI.Avalonia.Services.AI.ModelDownloadSettings(
                BaseUrl: "https://models.auracore.pro",
                InstallDirectory: global::AuraCore.UI.Avalonia.Services.AI.InstalledModelStore.DefaultInstallDir(),
                TimeoutMinutes: 30,
                BufferKb: 256,
                UserAgent: "AuraCorePro/1.0 (+https://auracore.pro)"));

        sc.AddTransient<global::AuraCore.UI.Avalonia.Services.AI.IModelDownloadService,
                        global::AuraCore.UI.Avalonia.Services.AI.ModelDownloadService>();

        sc.AddTransient<global::AuraCore.UI.Avalonia.Views.Pages.AIFeaturesView>();
        sc.AddTransient<global::AuraCore.UI.Avalonia.ViewModels.AIFeaturesViewModel>();
        sc.AddTransient<global::AuraCore.UI.Avalonia.Views.Pages.AI.ScheduleSection>();
        sc.AddTransient<global::AuraCore.UI.Avalonia.Views.Pages.AI.InsightsSection>();
        sc.AddTransient<global::AuraCore.UI.Avalonia.Views.Pages.AI.RecommendationsSection>();
        sc.AddTransient<global::AuraCore.UI.Avalonia.Views.Pages.AI.ChatSection>();
        sc.AddTransient<global::AuraCore.UI.Avalonia.ViewModels.ChatOptInDialogViewModel>();
        sc.AddTransient<global::AuraCore.UI.Avalonia.Views.Dialogs.ChatOptInDialog>();
        sc.AddTransient<global::AuraCore.UI.Avalonia.ViewModels.ModelManagerDialogViewModel>();
        sc.AddTransient<global::AuraCore.UI.Avalonia.Views.Dialogs.ModelManagerDialog>();
        sc.AddTransient<global::AuraCore.UI.Avalonia.Views.Dialogs.TierUpgradePlaceholderDialog>();

        // ── Phase 4.3.1: Journal Cleaner VM ──
        sc.AddTransient<global::AuraCore.UI.Avalonia.ViewModels.JournalCleanerViewModel>();

        // ── Phase 4.3.2: Snap/Flatpak Cleaner VM ──
        sc.AddTransient<global::AuraCore.UI.Avalonia.ViewModels.SnapFlatpakCleanerViewModel>();

        // ── Phase 4.3.3: Docker Cleaner VM ──
        sc.AddTransient<global::AuraCore.UI.Avalonia.ViewModels.DockerCleanerViewModel>();

        // ── Phase 4.3.4: Kernel Cleaner VM ──
        sc.AddTransient<global::AuraCore.UI.Avalonia.ViewModels.KernelCleanerViewModel>();

        // ── Phase 4.3.5: Linux App Installer VM ──
        sc.AddTransient<global::AuraCore.UI.Avalonia.ViewModels.LinuxAppInstallerViewModel>();

        // ── Phase 4.3.6: GRUB Manager VM ──
        sc.AddTransient<global::AuraCore.UI.Avalonia.ViewModels.GrubManagerViewModel>();

        // ── Phase 4.4.1: DNS Flusher VM ──
        sc.AddTransient<global::AuraCore.UI.Avalonia.ViewModels.DnsFlusherViewModel>();

        // ── Phase 4.4.2: Purgeable Space Manager VM ──
        sc.AddTransient<global::AuraCore.UI.Avalonia.ViewModels.PurgeableSpaceManagerViewModel>();

        // ── Phase 4.4.3: Spotlight Manager VM ──
        sc.AddTransient<global::AuraCore.UI.Avalonia.ViewModels.SpotlightManagerViewModel>();

        // ── Phase 4.4.4: Xcode Cleaner VM ──
        sc.AddTransient<global::AuraCore.UI.Avalonia.ViewModels.XcodeCleanerViewModel>();

        // ── Phase 4.4.5: Mac App Installer VM ──
        sc.AddTransient<global::AuraCore.UI.Avalonia.ViewModels.MacAppInstallerViewModel>();

        // SidebarViewModel with tier service.
        // Phase 4.2 interim: defaults to Admin (bypasses all locks) so every module
        // is reachable for manual QA. Phase 5 wires real UserSession.Tier lookup
        // (keyed off the signed-in account's plan — admin@auracore.pro => Admin,
        // other users => Free / Pro / Enterprise from their entitlement row).
        sc.AddSingleton<global::AuraCore.UI.Avalonia.ViewModels.SidebarViewModel>(sp =>
            new global::AuraCore.UI.Avalonia.ViewModels.SidebarViewModel(
                sp.GetRequiredService<global::AuraCore.UI.Avalonia.Services.AI.ITierService>(),
                currentTier: global::AuraCore.UI.Avalonia.Services.AI.UserTier.Admin));
        // ── end Phase 3 ──

        Services = sc.BuildServiceProvider();

        // Phase 5.3 Task 9: expose the narrow-mode service for StatRow code-behind binding.
        NarrowMode = Services.GetService<INarrowModeService>();

        // Initialize theme (loads saved preference)
        ThemeService.Initialize();

        // Initialize localization (loads saved language)
        LocalizationService.Load();

        // Initialize crash reporting
        CrashReportService.Initialize();

        // Start update checker (background, non-blocking)
        UpdateChecker.Instance.Start();

        // Drive the AI Analyzer engine (see AIMetricsCollectorService for
        // rationale). Background loop; disposed when the app exits.
        try
        {
            Services.GetRequiredService<global::AuraCore.UI.Avalonia.Services.AI.AIMetricsCollectorService>()
                    .Start();
        }
        catch { /* non-fatal — AI features will stay in placeholder state */ }

        // Background AI metric sync (consent-controlled, daily)
        _ = Task.Run(async () =>
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var dbPath = System.IO.Path.Combine(appData, "AuraCorePro", "ai", "metrics.db");
                if (System.IO.File.Exists(dbPath))
                {
                    using var db = new AuraCore.Engine.AIAnalyzer.LocalMetricDb(dbPath);
                    await AuraCore.Engine.AIAnalyzer.Sync.MetricSyncService.TrySyncAsync(db);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AI sync startup error: {ex.Message}");
            }
        });

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Views.LoginWindow();

#if DEBUG
            // Register Ctrl+F12 as a class-level handler on Window so it works on
            // any window (LoginWindow, MainWindow, and any future window) — not
            // just the one assigned at startup.
            global::Avalonia.Controls.Window.KeyDownEvent.AddClassHandler<global::Avalonia.Controls.Window>(
                (window, e) =>
                {
                    if (e.Key == global::Avalonia.Input.Key.F12 &&
                        e.KeyModifiers.HasFlag(global::Avalonia.Input.KeyModifiers.Control))
                    {
                        new Views.Dev.ComponentGalleryWindow().Show();
                        e.Handled = true;
                    }
                });
#endif
        }

        // Phase 6.1.D — start URL gateway server in the primary instance.
        if (OperatingSystem.IsWindows() && SingletonLock is not null)
        {
            try
            {
                var logger = Services?.GetService(typeof(Microsoft.Extensions.Logging.ILogger<AuraCore.UI.Avalonia.Helpers.UrlGatewayServer>))
                    as Microsoft.Extensions.Logging.ILogger<AuraCore.UI.Avalonia.Helpers.UrlGatewayServer>
                    ?? (Microsoft.Extensions.Logging.ILogger<AuraCore.UI.Avalonia.Helpers.UrlGatewayServer>)
                       Microsoft.Extensions.Logging.Abstractions.NullLogger<AuraCore.UI.Avalonia.Helpers.UrlGatewayServer>.Instance;

                UrlGateway = new AuraCore.UI.Avalonia.Helpers.UrlGatewayServer(logger);
                UrlGateway.InstanceIntentReceived += OnInstanceIntentReceived;
                UrlGateway.Start();
            }
            catch { /* best-effort */ }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void OnInstanceIntentReceived(object? sender, AuraCore.UI.Avalonia.Helpers.InstanceIntentEventArgs e)
    {
        // Marshal to UI thread, then dispatch through INavigationService.
        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            try
            {
                AuraCore.UI.Avalonia.Helpers.Win32Interop.FocusWindowByTitle("AuraCorePro");

                var nav = Services?.GetService(typeof(AuraCore.Application.Interfaces.Platform.INavigationService))
                    as AuraCore.Application.Interfaces.Platform.INavigationService;
                if (nav is null) return;

                var knownSections = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal)
                {
                    "dashboard", "settings", "disk-health",
                    "ai-recommendations", "ai-insights", "ai-schedule"
                };

                var intent = AuraCore.UI.Avalonia.Helpers.UrlSchemeHandler.Parse(
                    e.Url, knownSections, AuraCore.UI.Avalonia.Helpers.ModuleIdsRegistry.All);
                if (intent is not null)
                {
                    nav.NavigateTo(intent.Id);
                }
            }
            catch { /* deep-link failure must not crash */ }
        });
    }
}
