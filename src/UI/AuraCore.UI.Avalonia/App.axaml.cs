using global::Avalonia.Controls.ApplicationLifetimes;
using global::Avalonia.Markup.Xaml;
using AuraCore.UI.Avalonia.Views;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia;

public partial class App : global::Avalonia.Application
{
    private static ServiceProvider? _services;
    public static IServiceProvider Services => _services!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var sc = new ServiceCollection();


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
            AuraCore.Module.LinuxAppInstaller.LinuxAppInstallerRegistration.AddLinuxAppInstallerModule(sc);
            // Snap/Flatpak Cleaner (Phase 4.3.2): same concrete + IOptimizationModule alias
            // pattern as JournalCleaner above — lets the VM inject the concrete module
            // (with its additive Last* count properties) while keeping the engine-wide
            // _moduleMap binding intact.
            sc.AddSingleton<AuraCore.Module.SnapFlatpakCleaner.SnapFlatpakCleanerModule>();
            sc.AddSingleton<AuraCore.Application.Interfaces.Modules.IOptimizationModule>(
                sp => sp.GetRequiredService<AuraCore.Module.SnapFlatpakCleaner.SnapFlatpakCleanerModule>());
            AuraCore.Module.GrubManager.GrubManagerRegistration.AddGrubManagerModule(sc);
        }

        // ── macOS-only modules (Faz 3) ──
        if (OperatingSystem.IsMacOS())
        {
            AuraCore.Module.DefaultsOptimizer.DefaultsOptimizerRegistration.AddDefaultsOptimizerModule(sc);
            AuraCore.Module.LaunchAgentManager.LaunchAgentManagerRegistration.AddLaunchAgentManagerModule(sc);
            AuraCore.Module.BrewManager.BrewManagerRegistration.AddBrewManagerModule(sc);
            AuraCore.Module.TimeMachineManager.TimeMachineManagerRegistration.AddTimeMachineManagerModule(sc);
            AuraCore.Module.XcodeCleaner.XcodeCleanerRegistration.AddXcodeCleanerModule(sc);
            AuraCore.Module.DnsFlusher.DnsFlusherRegistration.AddDnsFlusherModule(sc);
            AuraCore.Module.PurgeableSpaceManager.PurgeableSpaceManagerRegistration.AddPurgeableSpaceManagerModule(sc);
            AuraCore.Module.SpotlightManager.SpotlightManagerRegistration.AddSpotlightManagerModule(sc);
            AuraCore.Module.MacAppInstaller.MacAppInstallerRegistration.AddMacAppInstallerModule(sc);
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

        _services = sc.BuildServiceProvider();

        // Initialize theme (loads saved preference)
        ThemeService.Initialize();

        // Initialize localization (loads saved language)
        LocalizationService.Load();

        // Initialize crash reporting
        CrashReportService.Initialize();

        // Start update checker (background, non-blocking)
        UpdateChecker.Instance.Start();

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

        base.OnFrameworkInitializationCompleted();
    }
}
