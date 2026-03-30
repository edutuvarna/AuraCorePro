using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.BatteryOptimizer;
using AuraCore.Module.BatteryOptimizer.Models;
using Microsoft.Extensions.DependencyInjection;

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
        if (_module is null) return; ScanLabel.Text = "Scanning...";
        try
        {
            await _module.ScanAsync(new ScanOptions());
            var b = _module.LastStatus;
            if (b is null || !b.HasBattery) { SubText.Text = b?.Error ?? "No battery detected"; return; }
            ChargePct.Text = $"{b.ChargePercent}%";
            ChargeStatus.Text = b.ChargeStatus;
            EstRemaining.Text = b.EstRemainingDisplay;
            HealthGrade.Text = b.HealthGrade;
            HealthGrade.Foreground = new SolidColorBrush(Color.Parse(b.HealthColor switch { "Green" => "#22C55E", "Blue" => "#3B82F6", "Amber" => "#F59E0B", _ => "#EF4444" }));
            WearPct.Text = $"{b.WearPercent}%";
            DesignCap.Text = b.DesignCapacityDisplay;
            FullCap.Text = b.FullChargeCapacityDisplay;
            PlanList.ItemsSource = _module.LastPowerPlans.Select(p => new PowerPlanItem(
                p.Name, p.IsActive ? "Active" : "", new SolidColorBrush(Color.Parse(p.IsActive ? "#22C55E" : "#555570")))).ToList();
            DrainList.ItemsSource = _module.LastDrainApps.Select(d => new DrainAppItem(
                d.Name, $"{d.CpuPercent:F1}%", d.Impact,
                new SolidColorBrush(Color.Parse(d.Impact == "High" ? "#EF4444" : d.Impact == "Medium" ? "#F59E0B" : "#22C55E")))).ToList();
        }
        catch { SubText.Text = "Scan failed"; } finally { ScanLabel.Text = "Scan"; }
}
    private async void Scan_Click(object? s, RoutedEventArgs e) => await RunScan();

    private void ApplyLocalization()
    {
        PageTitle.Text = LocalizationService._("nav.batteryOptimizer");
    }
}