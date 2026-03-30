using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.SystemHealth;
using AuraCore.Module.SystemHealth.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

public record DriveDisplayItem(
    string Name, string Label, string SizeText, string FreeText,
    string UsedPct, double BarWidth, ISolidColorBrush BarBrush, ISolidColorBrush PctBrush);

public record GpuDisplayItem(string Name, string Detail);

public partial class SystemHealthView : UserControl
{
    private readonly SystemHealthModule? _module;

    public SystemHealthView()
    {
        InitializeComponent();
        Loaded += (s, e) => ApplyLocalization();
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
        _module = App.Services.GetServices<IOptimizationModule>()
            .OfType<SystemHealthModule>().FirstOrDefault();
        Loaded += async (s, e) => await RunScan();
}

    private async Task RunScan()
    {
        if (_module is null) return;
        ScanBtnText.Text = "Scanning...";

        try
        {
            await _module.ScanAsync(new ScanOptions(DeepScan: true));
            var r = _module.LastReport;
            if (r is null) return;

            // Health Score
            ScoreValue.Text = r.HealthScore.ToString();
            var (label, color) = r.HealthScore switch
            {
                >= 85 => ("Excellent", "#22C55E"),
                >= 70 => ("Good", "#00D4AA"),
                >= 50 => ("Fair", "#F59E0B"),
                _     => ("Needs Attention", "#EF4444")
            };
            ScoreLabel.Text = label;
            ScoreLabel.Foreground = new SolidColorBrush(Color.Parse(color));
            ScoreBadge.Background = new SolidColorBrush(Color.Parse(color), 0.08);

            // OS
            OsName.Text = r.OsName;
            OsVersion.Text = $"{r.Architecture} - {r.OsVersion}";

            // CPU
            CpuName.Text = r.ProcessorName;
            CpuCores.Text = $"{r.ProcessorCount} cores";

            // RAM
            var usedRam = r.TotalRamGb - r.AvailableRamGb;
            RamUsed.Text = $"{usedRam:F1}";
            RamTotal.Text = $"/ {r.TotalRamGb:F1} GB";
            if (RamBar.Parent is Border ramParent && ramParent.Bounds.Width > 0)
                RamBar.Width = ramParent.Bounds.Width * r.RamUsagePercent / 100.0;

            // Uptime
            UptimeVal.Text = r.Uptime.Days > 0
                ? $"{r.Uptime.Days}d {r.Uptime.Hours}h"
                : $"{r.Uptime.Hours}h {r.Uptime.Minutes}m";
            ProcessCount.Text = $"{r.RunningProcesses} processes";

            // Drives
            var driveItems = r.Drives.Select(d =>
            {
                var pct = d.UsedPercent;
                var barBrush = pct > 90 ? new SolidColorBrush(Color.Parse("#EF4444"))
                             : pct > 75 ? new SolidColorBrush(Color.Parse("#F59E0B"))
                             : new SolidColorBrush(Color.Parse("#00D4AA"));
                return new DriveDisplayItem(
                    d.Name, string.IsNullOrEmpty(d.Label) ? d.Format : d.Label,
                    $"{d.TotalGb:F1} GB", $"{d.FreeGb:F1} GB free",
                    $"{pct}%", Math.Max(0, pct * 3.0), barBrush, barBrush);
            }).ToList();
            DriveList.ItemsSource = driveItems;

            // GPUs
            if (r.Gpus.Count > 0)
            {
                GpuHeader.IsVisible = true;
                GpuList.ItemsSource = r.Gpus.Select(g =>
                    new GpuDisplayItem(g.Name,
                        $"Driver: {g.DriverVersion} | VRAM: {g.VideoMemory} | {g.Resolution}")
                ).ToList();
            }
        }
        catch { ScoreValue.Text = "!"; ScoreLabel.Text = "Scan Failed"; }
        finally { ScanBtnText.Text = "Scan Now"; }
}

    private async void Scan_Click(object? sender, RoutedEventArgs e) => await RunScan();

    private void ApplyLocalization()
    {
        PageTitle.Text = LocalizationService._("nav.systemHealth");
    }
}