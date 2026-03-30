using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.RegistryOptimizer;
using AuraCore.Module.RegistryOptimizer.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

public record RegIssueItem(string Desc, string KeyPath, string Category, string Risk, ISolidColorBrush RiskFg, ISolidColorBrush RiskBg);

public partial class RegistryOptimizerView : UserControl
{
    private readonly RegistryOptimizerModule? _module;
    public RegistryOptimizerView()
    {
        InitializeComponent();
        Loaded += (s, e) => ApplyLocalization();
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
        _module = App.Services.GetServices<IOptimizationModule>().OfType<RegistryOptimizerModule>().FirstOrDefault();
        Loaded += async (s, e) => await RunScan();
}
    private async Task RunScan()
    {
        if (_module is null) return; ScanLabel.Text = "Scanning...";
        try
        {
            await _module.ScanAsync(new ScanOptions(DeepScan: true));
            var r = _module.LastReport; if (r is null) return;
            TotalIssues.Text = r.TotalIssues.ToString();
            SafeIssues.Text = r.SafeIssues.ToString();
            CautionIssues.Text = r.CautionIssues.ToString();
            FixBtn.IsEnabled = r.TotalIssues > 0;
            IssueList.ItemsSource = r.Issues.Select(i =>
            {
                var (fg, bg) = i.Risk == "Caution" ? (P("#F59E0B"), P("#20F59E0B")) : (P("#22C55E"), P("#2022C55E"));
                return new RegIssueItem(i.Description, i.KeyPath, i.Category, i.Risk, fg, bg);
            }).ToList();
        }
        catch { SubText.Text = "Scan failed"; } finally { ScanLabel.Text = "Scan"; }
    }
    private static SolidColorBrush P(string h) => new(Color.Parse(h));
    private async void Scan_Click(object? s, RoutedEventArgs e) => await RunScan();
    private async void Fix_Click(object? s, RoutedEventArgs e)
    {
        if (_module is null) return; FixBtn.IsEnabled = false;
        try
        {
            var plan = new OptimizationPlan(_module.Id, new[] { "all" });
            var progress = new Progress<TaskProgress>(p => global::Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusText.Text = p.StatusText));
            var result = await _module.OptimizeAsync(plan, progress);
            StatusText.Text = $"Fixed {result.ItemsProcessed} issues in {result.Duration.TotalSeconds:F1}s";
            await RunScan();
        }
        catch (System.Exception ex) { StatusText.Text = ex.Message; } finally { FixBtn.IsEnabled = true; }
}

    private void ApplyLocalization()
    {
        PageTitle.Text = LocalizationService._("nav.registry");
    }
}