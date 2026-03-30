using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.DriverUpdater;
using AuraCore.Module.DriverUpdater.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

public record DriverDisplayItem(string Name, string Class, string Mfr, string Version, string Date,
    string Status, ISolidColorBrush StatusFg, ISolidColorBrush StatusBg);

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
}

    private async Task RunScan()
    {
        if (_module is null) return;
        ScanLabel.Text = "Scanning...";
        SubText.Text = "Scanning drivers via WMI (this may take a moment)...";

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
                var (fg, bg, label) = d.HasProblem ? (Parse("#EF4444"), Parse("#20EF4444"), "Problem")
                    : d.AgeCategory switch
                    {
                        "Current" => (Parse("#22C55E"), Parse("#2022C55E"), "Current"),
                        "Recent"  => (Parse("#3B82F6"), Parse("#203B82F6"), "Recent"),
                        "Aging"   => (Parse("#F59E0B"), Parse("#20F59E0B"), "Aging"),
                        _         => (Parse("#EF4444"), Parse("#20EF4444"), "Outdated")
                    };
                return new DriverDisplayItem(d.DeviceName, d.DeviceClass, d.Manufacturer,
                    d.DriverVersion, d.DriverDateDisplay, label, fg, bg);
            }).ToList();

            DriverList.ItemsSource = items;
        }
        catch { SubText.Text = "Scan failed"; }
        finally { ScanLabel.Text = "Scan"; }
    }

    private static SolidColorBrush Parse(string hex) => new(Color.Parse(hex));

    private async void Scan_Click(object? sender, RoutedEventArgs e) => await RunScan();

    private async void Backup_Click(object? sender, RoutedEventArgs e)
    {
        if (_module is null) return;
        BackupLabel.Text = "Backing up...";
        try
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"AuraCore_DriverBackup_{DateTime.Now:yyyyMMdd_HHmmss}");
            var result = await _module.BackupDriversAsync(path);
            SubText.Text = result.Success
                ? $"Backed up {result.DriversExported} drivers ({result.SizeDisplay}) to {path}"
                : $"Backup failed: {result.Error}";
        }
        catch (System.Exception ex) { SubText.Text = $"Backup error: {ex.Message}"; }
        finally { BackupLabel.Text = "Backup Drivers"; }
    }

    private async void WinUpdate_Click(object? sender, RoutedEventArgs e)
    {
        if (_module is not null) await _module.OpenWindowsUpdateAsync();
    }

    private async void DevMgr_Click(object? sender, RoutedEventArgs e)
    {
        if (_module is not null) await _module.OpenDeviceManagerAsync();
}

    private void ApplyLocalization()
    {
        PageTitle.Text = LocalizationService._("nav.driverUpdater");
    }
}