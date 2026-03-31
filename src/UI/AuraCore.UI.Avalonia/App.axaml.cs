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
        }

        // ── Linux-only modules (Faz 2+) ──
        if (OperatingSystem.IsLinux())
        {
            AuraCore.Module.SystemdManager.SystemdManagerRegistration.AddSystemdManagerModule(sc);
            AuraCore.Module.PackageCleaner.PackageCleanerRegistration.AddPackageCleanerModule(sc);
            AuraCore.Module.SwapOptimizer.SwapOptimizerRegistration.AddSwapOptimizerModule(sc);
            AuraCore.Module.CronManager.CronManagerRegistration.AddCronManagerModule(sc);
        }

        // ── macOS-only modules (Faz 3) ──
        if (OperatingSystem.IsMacOS())
        {
            // TODO: DefaultsOptimizer, LaunchAgentManager, BrewManager
        }

        _services = sc.BuildServiceProvider();

        // Initialize theme (loads saved preference)
        ThemeService.Initialize();

        // Initialize localization (loads saved language)
        LocalizationService.Load();

        // Initialize crash reporting
        CrashReportService.Initialize();

        // Start update checker (background, non-blocking)
        UpdateChecker.Instance.Start();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Views.LoginWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
