using System.Runtime.Versioning;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.AutorunManager;
using AuraCore.Module.AutorunManager.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

public record AutorunDisplayItem(
    string Name, string Command, string Location,
    string RiskLevel, ISolidColorBrush RiskFg, ISolidColorBrush RiskBg,
    string StatusText, ISolidColorBrush StatusBrush,
    bool IsEnabled, string ToggleTag, string DeleteTag,
    string DeleteLabel);

// Phase 6.16.F: this view is registered only inside the IsWindows() block in MainWindow,
// so it's safe to mark Windows-only and let CA1416 propagate.
[SupportedOSPlatform("windows")]
public partial class AutorunManagerView : UserControl
{
    private readonly AutorunManagerModule? _module;

    public AutorunManagerView()
    {
        InitializeComponent();
        Loaded += (s, e) => ApplyLocalization();
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
        _module = App.Services.GetServices<IOptimizationModule>()
            .OfType<AutorunManagerModule>().FirstOrDefault();
        Loaded += async (s, e) => await RunScan();
}

    private async Task RunScan()
    {
        if (_module is null) return;
        ScanLabel.Text = LocalizationService._("common.scanning");
        try
        {
            await _module.ScanAsync(new ScanOptions());
            var report = _module.LastReport;
            if (report is null) return;

            TotalCount.Text = report.Entries.Count.ToString();
            EnabledCount.Text = report.EnabledCount.ToString();
            DisabledCount.Text = report.DisabledCount.ToString();
            HighRiskCount.Text = report.Entries.Count(e => e.RiskLevel == "High").ToString();

            RenderEntries(report);
        }
        catch { SubtitleText.Text = LocalizationService._("autorun.scanFailed"); }
        finally { ScanLabel.Text = LocalizationService._("autorun.scanBtn"); }
    }

    private void RenderEntries(AutorunReport report)
    {
        var deleteLabel = LocalizationService._("common.delete");
        var items = report.Entries.Select(e =>
        {
            var (riskFg, riskBg) = e.RiskLevel switch
            {
                "High"   => (Parse("#EF4444"), Parse("#20EF4444")),
                "Medium" => (Parse("#F59E0B"), Parse("#20F59E0B")),
                "Safe"   => (Parse("#22C55E"), Parse("#2022C55E")),
                _        => (Parse("#8888A0"), Parse("#208888A0"))
            };
            var statusBrush = e.IsEnabled ? Parse("#22C55E") : Parse("#8888A0");
            var statusText = e.IsEnabled
                ? LocalizationService._("autorun.enabledStatus")
                : LocalizationService._("autorun.disabledStatus");

            return new AutorunDisplayItem(
                e.Name, e.Command, e.Location,
                e.RiskLevel, riskFg, riskBg,
                statusText, statusBrush,
                e.IsEnabled,
                $"{(e.IsEnabled ? "disable" : "enable")}:{e.Name}",
                $"delete:{e.Name}",
                deleteLabel);
        }).ToList();

        AutorunList.ItemsSource = items;
    }

    private static SolidColorBrush Parse(string hex) => new(Color.Parse(hex));

    private async void Scan_Click(object? sender, RoutedEventArgs e) => await RunScan();

    private async void Toggle_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb || _module is null) return;
        var tag = cb.Tag?.ToString();
        if (string.IsNullOrEmpty(tag)) return;

        try
        {
            var plan = new OptimizationPlan(_module.Id, new[] { tag });
            await _module.OptimizeAsync(plan);
            await RunScan();
        }
        catch { }
    }

    private async void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || _module is null) return;
        var tag = btn.Tag?.ToString();
        if (string.IsNullOrEmpty(tag)) return;

        try
        {
            var plan = new OptimizationPlan(_module.Id, new[] { tag });
            await _module.OptimizeAsync(plan);
            await RunScan();
        }
        catch { }
}

    private void ApplyLocalization()
    {
        PageTitle.Text       = LocalizationService._("nav.autorunManager");
        PageHeader.Title     = LocalizationService._("autorun.title");
        PageHeader.Subtitle  = LocalizationService._("autorun.subtitle");
        ScanLabel.Text       = LocalizationService._("autorun.scanBtn");
        SubtitleText.Text    = LocalizationService._("autorun.subtitle");
        ColName.Text         = LocalizationService._("autorun.colName");
        ColLocation.Text     = LocalizationService._("autorun.colLocation");
        ColRisk.Text         = LocalizationService._("autorun.colRisk");
        ColStatus.Text       = LocalizationService._("autorun.colStatus");
        ColActions.Text      = LocalizationService._("autorun.colActions");
        StatTotal.Label      = LocalizationService._("autorun.statTotal");
        StatEnabled.Label    = LocalizationService._("autorun.statEnabled");
        StatDisabled.Label   = LocalizationService._("autorun.statDisabled");
        StatHighRisk.Label   = LocalizationService._("autorun.statHighRisk");
    }
}
