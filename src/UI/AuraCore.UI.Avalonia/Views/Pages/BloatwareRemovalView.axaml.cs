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

    private async void Remove_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || _module is null) return;
        var pkg = btn.Tag?.ToString();
        if (string.IsNullOrEmpty(pkg)) return;
        StatusText.Text = "Removing...";
        try
        {
            var plan = new OptimizationPlan(_module.Id, new[] { pkg });
            var result = await _module.OptimizeAsync(plan);
            StatusText.Text = result.Success ? $"Removed. Freed {FormatBytes(result.BytesFreed)}" : "Failed - try as admin";
            await RunScan();
        }
        catch (System.Exception ex) { StatusText.Text = ex.Message; }
}

    private void ApplyLocalization()
    {
        PageTitle.Text = LocalizationService._("nav.bloatware");
    }
}