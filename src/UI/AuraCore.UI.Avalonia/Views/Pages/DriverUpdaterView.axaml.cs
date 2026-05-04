using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.DriverUpdater;
using AuraCore.Module.DriverUpdater.Models;
using AuraCore.UI.Avalonia.Helpers;
using AuraCore.UI.Avalonia.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

public record DriverDisplayItem(string Name, string Class, string Mfr, string Version, string Date,
    string Status, ISolidColorBrush StatusFg, ISolidColorBrush StatusBg);

[SupportedOSPlatform("windows")]
public partial class DriverUpdaterView : UserControl
{
    private readonly DriverUpdaterModule? _module;

    public DriverUpdaterView()
    {
        InitializeComponent();
        Loaded += (s, e) => ApplyLocalization();
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
        _module = App.Services.GetServices<IOptimizationModule>()
            .OfType<DriverUpdaterModule>().FirstOrDefault();
        Loaded += async (s, e) => await RunScan();
        Unloaded += (s, e) =>
            LocalizationService.LanguageChanged -= () =>
                global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
}

    private async Task RunScan()
    {
        if (_module is null) return;
        ScanLabel.Text = LocalizationService._("common.scanning");
        SubText.Text = LocalizationService._("driverUpdate.status.scanning");

        try
        {
            await _module.ScanAsync(new ScanOptions());
            var report = _module.LastReport;
            if (report is null) return;

            TotalDrivers.Text = report.TotalCount.ToString();
            CurrentDrivers.Text = report.CurrentCount.ToString();
            OutdatedDrivers.Text = report.OutdatedCount.ToString();
            ProblemDrivers.Text = report.ProblemCount.ToString();
            SubText.Text = report.HealthSummary;

            var items = report.Drivers.Select(d =>
            {
                var (fg, bg, label) = d.HasProblem ? (Parse("#EF4444"), Parse("#20EF4444"), LocalizationService._("driverUpdate.status.problem"))
                    : d.AgeCategory switch
                    {
                        "Current" => (Parse("#22C55E"), Parse("#2022C55E"), LocalizationService._("driverUpdate.status.current")),
                        "Recent"  => (Parse("#3B82F6"), Parse("#203B82F6"), LocalizationService._("driverUpdate.status.recent")),
                        "Aging"   => (Parse("#F59E0B"), Parse("#20F59E0B"), LocalizationService._("driverUpdate.status.aging")),
                        _         => (Parse("#EF4444"), Parse("#20EF4444"), LocalizationService._("driverUpdate.status.outdated"))
                    };
                return new DriverDisplayItem(d.DeviceName, d.DeviceClass, d.Manufacturer,
                    d.DriverVersion, d.DriverDateDisplay, label, fg, bg);
            }).ToList();

            DriverList.ItemsSource = items;
        }
        catch { SubText.Text = LocalizationService._("driverUpdate.status.scanFailed"); }
        finally { ScanLabel.Text = LocalizationService._("driverUpdate.action.scan"); }
    }

    private static SolidColorBrush Parse(string hex) => new(Color.Parse(hex));

    private async void Scan_Click(object? sender, RoutedEventArgs e) => await RunScan();

    private async void Backup_Click(object? sender, RoutedEventArgs e)
    {
        if (_module is null) return;
        BackupLabel.Text = LocalizationService._("common.scanning");
        try
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"AuraCore_DriverBackup_{DateTime.Now:yyyyMMdd_HHmmss}");
            var result = await _module.BackupDriversAsync(path);
            SubText.Text = result.Success
                ? string.Format(LocalizationService._("driverUpdate.status.backupSuccess"), result.DriversExported, result.SizeDisplay, path)
                : string.Format(LocalizationService._("driverUpdate.status.backupFailed"), result.Error);
        }
        catch (System.Exception ex) { SubText.Text = $"Backup error: {ex.Message}"; }
        finally { BackupLabel.Text = LocalizationService._("driverUpdate.action.backup"); }
    }

    private async void WinUpdate_Click(object? sender, RoutedEventArgs e)
    {
        if (_module is not null) await _module.OpenWindowsUpdateAsync();
    }

    private async void DevMgr_Click(object? sender, RoutedEventArgs e)
    {
        if (_module is not null) await _module.OpenDeviceManagerAsync();
    }

    private async Task<bool> EnsurePrivilegedHelperInstalledAsync()
    {
        var installer = App.Services?.GetService<PrivilegedHelperInstaller>();
        if (installer is null) return true; // DI not wired (tests/design-time) — don't block

        if (await installer.IsInstalledAsync(CancellationToken.None))
            return true;

        // Prompt for consent + install
        var topWindow = TopLevel.GetTopLevel(this) as Window;
        if (topWindow is null) return false;

        var dialog = new PrivilegedHelperInstallDialog(installer);
        await dialog.ShowDialog(topWindow);
        return dialog.Outcome == PrivilegedHelperInstallOutcome.Success;
    }

    private async void PrivScan_Click(object? sender, RoutedEventArgs e)
    {
        if (_module is null) return;
        if (!await EnsurePrivilegedHelperInstalledAsync())
        {
            SubText.Text = LocalizationService.Get("privhelper.notInstalled.toast");
            return;
        }
        PrivScanLabel.Text = LocalizationService._("common.scanning");
        HelperMissingBanner.IsVisible = false;
        try
        {
            var outcome = await _module.ScanDevicesAsync();
            if (outcome.HelperMissing)
            {
                HelperMissingBanner.IsVisible = true;
                SubText.Text = LocalizationService._("privhelper.notInstalled.toast");
            }
            else if (outcome.Success)
            {
                SubText.Text = LocalizationService._("driverUpdate.status.privScanComplete");
            }
            else
            {
                SubText.Text = $"Scan failed: {outcome.Error}";
            }
        }
        catch (System.Exception ex) { SubText.Text = $"Scan error: {ex.Message}"; }
        finally { PrivScanLabel.Text = LocalizationService._("driverUpdate.action.privScan"); }
}

    private void ApplyLocalization()
    {
        PageTitle.Text = LocalizationService._("nav.driverUpdater");
        var L = LocalizationService._;
        if (this.FindControl<global::AuraCore.UI.Avalonia.Views.Controls.ModuleHeader>("Header") is { } h)
        {
            h.Title = L("driverUpdate.title");
            h.Subtitle = L("driverUpdate.subtitle");
        }
        BackupLabel.Text = L("driverUpdate.action.backup");
        ScanLabel.Text = L("driverUpdate.action.scan");
        TotalLabel.Text = L("driverUpdate.stat.total");
        CurrentLabel.Text = L("driverUpdate.stat.current");
        OutdatedLabel.Text = L("driverUpdate.stat.outdated");
        ProblemsLabel.Text = L("driverUpdate.stat.problems");
        ColDevice.Text = L("driverUpdate.col.device");
        ColManufacturer.Text = L("driverUpdate.col.manufacturer");
        ColVersion.Text = L("driverUpdate.col.version");
        ColDate.Text = L("driverUpdate.col.date");
        ColStatus.Text = L("driverUpdate.col.status");
        WinUpdateLabel.Text = L("driverUpdate.action.winUpdate");
        DevMgrLabel.Text = L("driverUpdate.action.devMgr");
        PrivScanLabel.Text = L("driverUpdate.action.privScan");
        HelperMissingText.Text = L("privhelper.notInstalled.banner");
    }
}
