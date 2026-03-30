using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using global::Avalonia.Threading;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.ProcessMonitor;
using AuraCore.Module.ProcessMonitor.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

public record ProcessDisplayItem(
    int Pid, string Name, string Description,
    string CpuText, string RamText, string Threads,
    ISolidColorBrush CpuBrush,
    string KillTag, string SuspendTag, string SuspendLabel);

public partial class ProcessMonitorView : UserControl
{
    private readonly ProcessMonitorModule? _module;
    private DispatcherTimer? _autoTimer;
    private bool _autoRefresh;
    private volatile bool _isScanning;
    private string _searchFilter = "";

    public ProcessMonitorView()
    {
        InitializeComponent();
        Loaded += (s, e) => ApplyLocalization();
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
_module = App.Services.GetServices<IOptimizationModule>()
            .OfType<ProcessMonitorModule>().FirstOrDefault();
        Loaded += async (s, e) => await RunScan();
        Unloaded += (s, e) => StopAutoRefresh();
}

    private async Task RunScan()
    {
        if (_module is null || _isScanning) return;
        _isScanning = true;
        ScanBtnLabel.Text = "Scanning...";

        try
        {
            await _module.ScanAsync(new ScanOptions());
            var report = _module.LastReport;
            if (report is null) return;

            ProcCount.Text = report.Processes.Count.ToString();
            TotalCpu.Text = $"{report.TotalCpuPercent:F1}%";
            TotalRam.Text = $"{report.TotalMemoryMb:N0} MB";
            LastScanTime.Text = $"Last: {DateTime.Now:HH:mm:ss}";

            RenderProcessList(report);
        }
        catch { SubtitleText.Text = "Scan failed"; }
        finally
        {
            _isScanning = false;
            ScanBtnLabel.Text = "Scan";
        }
    }

    private void RenderProcessList(ProcessReport report)
    {
        var procs = report.Processes.AsEnumerable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(_searchFilter))
        {
            var filter = _searchFilter.ToLowerInvariant();
            procs = procs.Where(p =>
                p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                p.Description.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                p.Pid.ToString().Contains(filter));
        }

        // Sort by CPU descending, take top 200
        var items = procs
            .OrderByDescending(p => p.CpuPercent)
            .Take(200)
            .Select(p =>
            {
                var cpuBrush = p.CpuPercent > 50 ? new SolidColorBrush(Color.Parse("#EF4444"))
                             : p.CpuPercent > 10 ? new SolidColorBrush(Color.Parse("#F59E0B"))
                             : new SolidColorBrush(Color.Parse("#8888A0"));

                return new ProcessDisplayItem(
                    p.Pid, p.Name, p.Description,
                    $"{p.CpuPercent:F1}", $"{p.MemoryMb:N0}", p.ThreadCount.ToString(),
                    cpuBrush,
                    $"kill:{p.Pid}", $"suspend:{p.Pid}",
                    p.Status == "Suspended" ? "Resume" : "Suspend");
            }).ToList();

        ProcessList.ItemsSource = items;
    }

    private async void Scan_Click(object? sender, RoutedEventArgs e) => await RunScan();

    private void AutoRefresh_Click(object? sender, RoutedEventArgs e)
    {
        _autoRefresh = !_autoRefresh;
        if (_autoRefresh)
        {
            AutoRefreshLabel.Text = "Auto: ON";
            _autoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _autoTimer.Tick += async (s, e) => await RunScan();
            _autoTimer.Start();
        }
        else
        {
            StopAutoRefresh();
        }
    }

    private void StopAutoRefresh()
    {
        _autoRefresh = false;
        AutoRefreshLabel.Text = "Auto: OFF";
        _autoTimer?.Stop();
        _autoTimer = null;
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchFilter = SearchBox.Text ?? "";
        if (_module?.LastReport is not null)
            RenderProcessList(_module.LastReport);
    }

    private async void Action_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || _module is null) return;
        var tag = btn.Tag?.ToString();
        if (string.IsNullOrEmpty(tag)) return;

        try
        {
            var plan = new OptimizationPlan(_module.Id, new[] { tag });
            await _module.OptimizeAsync(plan);
            await RunScan(); // refresh after action
        }
        catch { }
}

    private void ApplyLocalization()
    {
        PageTitle.Text = LocalizationService._("nav.processMonitor");
    }
}