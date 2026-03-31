using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.BloatwareRemoval;
using AuraCore.Module.BloatwareRemoval.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

public record BloatDisplayItem(string Name, string Publisher, string Category, string Size,
    string RiskText, ISolidColorBrush RiskFg, ISolidColorBrush RiskBg, bool CanRemove, string PkgName);

public partial class BloatwareRemovalView : UserControl
{
    private readonly BloatwareRemovalModule? _module;

    public BloatwareRemovalView()
    {
        InitializeComponent();
        Loaded += (s, e) => ApplyLocalization();
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
        _module = App.Services.GetServices<IOptimizationModule>()
            .OfType<BloatwareRemovalModule>().FirstOrDefault();
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
            TotalApps.Text = r.TotalApps.ToString();
            RemovableApps.Text = r.RemovableApps.ToString();
            TotalSize.Text = FormatBytes(r.TotalRemovableBytes);
            var items = r.Apps.Select(a =>
            {
                var canRemove = a.Risk != BloatRisk.System && !a.IsFramework;
                var (fg, bg) = a.Risk switch
                {
                    BloatRisk.Safe    => (P("#22C55E"), P("#2022C55E")),
                    BloatRisk.Caution => (P("#F59E0B"), P("#20F59E0B")),
                    BloatRisk.Warning => (P("#EF4444"), P("#20EF4444")),
                    _                 => (P("#8888A0"), P("#208888A0"))
                };
                return new BloatDisplayItem(a.DisplayName, a.Publisher, a.Category.ToString(),
                    a.SizeDisplay, a.Risk.ToString(), fg, bg, canRemove, a.PackageFullName);
            }).ToList();
            AppList.ItemsSource = items;
            RemoveBtn.IsEnabled = items.Any(i => i.CanRemove);
        }
        catch { SubText.Text = "Scan failed"; }
        finally { ScanLabel.Text = "Scan"; }
    }

    private static SolidColorBrush P(string hex) => new(Color.Parse(hex));

    private static string FormatBytes(long b) => b switch
    {
        >= 1073741824 => $"{b / 1073741824.0:F1} GB",
        >= 1048576    => $"{b / 1048576.0:F1} MB",
        >= 1024       => $"{b / 1024.0:F1} KB",
        _             => $"{b} B"
    };

    private async void Scan_Click(object? sender, RoutedEventArgs e) => await RunScan();

    private async void RemoveSelected_Click(object? sender, RoutedEventArgs e)
    {
        if (_module is null) return;
        var items = AppList.ItemsSource as IEnumerable<BloatDisplayItem>;
        if (items is null) return;
        var toRemove = items.Where(i => i.CanRemove).ToList();
        if (toRemove.Count == 0) return;
        RemoveBtn.IsEnabled = false;
        var removed = 0;
        long freed = 0;
        foreach (var item in toRemove)
        {
            StatusText.Text = $"Removing {item.Name}... ({removed + 1}/{toRemove.Count})";
            try
            {
                var plan = new OptimizationPlan(_module.Id, new[] { item.PkgName });
                var result = await _module.OptimizeAsync(plan);
                if (result.Success) { removed++; freed += result.BytesFreed; }
            }
            catch { }
        }
        StatusText.Text = $"Done! Removed {removed}/{toRemove.Count} apps. Freed {FormatBytes(freed)}";
        await RunScan();
        RemoveBtn.IsEnabled = true;
    }

    private void ApplyLocalization()
    {
        PageTitle.Text = LocalizationService._("nav.bloatware");
    }
}