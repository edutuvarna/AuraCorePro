using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.RamOptimizer;
using AuraCore.Module.RamOptimizer.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

public record RamProcessItem(string Pid, string Name, string Memory, string Category, ISolidColorBrush EssentialBrush);

public partial class RamOptimizerView : UserControl
{
    private readonly RamOptimizerModule? _module;

    public RamOptimizerView()
    {
        InitializeComponent();
        Loaded += (s, e) => ApplyLocalization();
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
        _module = App.Services.GetServices<IOptimizationModule>()
            .OfType<RamOptimizerModule>().FirstOrDefault();
        Loaded += async (s, e) => await RunScan();
}

    private async Task RunScan()
    {
        if (_module is null) return;
        ScanLabel.Text = "Scanning...";
        try
        {
            await _module.ScanAsync(new ScanOptions());
            var r = _module.LastReport;
            if (r is null) return;
            UsedRam.Text = r.UsedDisplay;
            TotalRam.Text = $"/ {r.TotalDisplay}";
            RamPct.Text = $"{r.UsagePercent}%";
            Reclaimable.Text = $"Reclaimable: {r.ReclaimableDisplay}";
            if (RamBar.Parent is Border parent && parent.Bounds.Width > 0)
                RamBar.Width = parent.Bounds.Width * r.UsagePercent / 100.0;
            OptBtn.IsEnabled = r.TotalReclaimableBytes > 0;
            var items = r.TopProcesses.Select(p => new RamProcessItem(
                p.Pid.ToString(), p.Name, p.MemoryDisplay, p.Category,
                new SolidColorBrush(Color.Parse(p.IsEssential ? "#22C55E" : "#555570"))
            )).ToList();
            ProcessList.ItemsSource = items;
        }
        catch { SubText.Text = "Scan failed"; }
        finally { ScanLabel.Text = "Scan"; }
    }

    private async void Scan_Click(object? sender, RoutedEventArgs e) => await RunScan();

    private async void Optimize_Click(object? sender, RoutedEventArgs e)
    {
        if (_module is null) return;
        OptBtn.IsEnabled = false; OptLabel.Text = "Working...";
        try
        {
            var plan = new OptimizationPlan(_module.Id, new[] { "all" });
            var progress = new Progress<TaskProgress>(p =>
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusText.Text = p.StatusText));
            var result = await _module.OptimizeAsync(plan, progress);
            StatusText.Text = $"Freed memory from {result.ItemsProcessed} processes in {result.Duration.TotalSeconds:F1}s";
            await RunScan();
        }
        catch (System.Exception ex) { StatusText.Text = ex.Message; }
        finally { OptLabel.Text = "Optimize"; OptBtn.IsEnabled = true; }
}

    private void ApplyLocalization()
    {
        PageTitle.Text = LocalizationService._("nav.ramOptimizer");
    }
}