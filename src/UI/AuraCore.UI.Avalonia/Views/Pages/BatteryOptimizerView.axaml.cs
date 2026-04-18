using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.BatteryOptimizer;
using AuraCore.Module.BatteryOptimizer.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace AuraCore.UI.Avalonia.Views.Pages;

public record PowerPlanItem(string Name, string ActiveLabel, ISolidColorBrush ActiveBrush);
public record DrainAppItem(string Name, string Cpu, string Impact, ISolidColorBrush ImpactBrush);

public partial class BatteryOptimizerView : UserControl
{
    private readonly BatteryOptimizerModule? _module;
    public BatteryOptimizerView()
    {
        InitializeComponent();
        Loaded += (s, e) => ApplyLocalization();
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
        _module = App.Services.GetServices<IOptimizationModule>().OfType<BatteryOptimizerModule>().FirstOrDefault();
        Loaded += async (s, e) => await RunScan();
}
    private async Task RunScan()
    {
        if (_module is null) return;
        ScanLabel.Text = LocalizationService._("common.scanning");
        try
        {
            await _module.ScanAsync(new ScanOptions());
            var b = _module.LastStatus;
            if (b is null || !b.HasBattery)
            {
                SubText.Text = b?.Error ?? LocalizationService._("battery.noBattery");
                return;
            }
            ChargePct.Text = $"{b.ChargePercent}%";
            ChargeStatus.Text = b.ChargeStatus;
            EstRemaining.Text = b.EstRemainingDisplay;
            HealthGrade.Text = b.HealthGrade;
            HealthGrade.Foreground = new SolidColorBrush(Color.Parse(b.HealthColor switch { "Green" => "#22C55E", "Blue" => "#3B82F6", "Amber" => "#F59E0B", _ => "#EF4444" }));
            WearPct.Text = $"{b.WearPercent}%";
            DesignCap.Text = b.DesignCapacityDisplay;
            FullCap.Text = b.FullChargeCapacityDisplay;
            var activeLabel = LocalizationService._("battery.active");
            PlanList.ItemsSource = _module.LastPowerPlans.Select(p => new PowerPlanItem(
                p.Name, p.IsActive ? activeLabel : "", new SolidColorBrush(Color.Parse(p.IsActive ? "#22C55E" : "#555570")))).ToList();
            DrainList.ItemsSource = _module.LastDrainApps.Select(d => new DrainAppItem(
                d.Name, $"{d.CpuPercent:F1}%", d.Impact,
                new SolidColorBrush(Color.Parse(d.Impact == "High" ? "#EF4444" : d.Impact == "Medium" ? "#F59E0B" : "#22C55E")))).ToList();
        }
        catch { SubText.Text = LocalizationService._("battery.scanFailed"); }
        finally { ScanLabel.Text = LocalizationService._("battery.scanBtn"); }
}
    private async void Scan_Click(object? s, RoutedEventArgs e) => await RunScan();

    private async void BatteryReport_Click(object? s, RoutedEventArgs e)
    {
        try
        {
            var reportPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "battery-report.html");

            var psi = new ProcessStartInfo("powercfg", $"/batteryreport /output \"{reportPath}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                Verb = "runas"
            };
            using var proc = Process.Start(psi);
            if (proc != null) await proc.WaitForExitAsync();

            if (System.IO.File.Exists(reportPath))
            {
                Process.Start(new ProcessStartInfo(reportPath) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            SubText.Text = $"{LocalizationService._("battery.reportFailed")}: {ex.Message}";
        }
    }

    private void ApplyLocalization()
    {
        PageTitle.Text         = LocalizationService._("nav.batteryOptimizer");
        PageHeader.Title       = LocalizationService._("battery.title");
        PageHeader.Subtitle    = LocalizationService._("battery.subtitle");
        ScanLabel.Text         = LocalizationService._("battery.scanBtn");
        BatteryReportLabel.Text = LocalizationService._("battery.reportBtn");
        SubText.Text           = LocalizationService._("battery.subtitle");
        HealthLabel.Text       = LocalizationService._("battery.healthLabel");
        WearLabel.Text         = LocalizationService._("battery.wearLabel");
        DesignLabel.Text       = LocalizationService._("battery.designLabel");
        FullChargeLabel.Text   = LocalizationService._("battery.fullChargeLabel");
        PowerPlansLabel.Text   = LocalizationService._("battery.powerPlans");
        HighDrainLabel.Text    = LocalizationService._("battery.highDrainApps");
    }
}
