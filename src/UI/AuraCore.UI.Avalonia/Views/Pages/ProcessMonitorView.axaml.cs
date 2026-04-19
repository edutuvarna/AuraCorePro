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
    string KillTag, string KillLabel, string SuspendTag, string SuspendLabel);

public partial class ProcessMonitorView : UserControl
{
    private readonly ProcessMonitorModule? _module;
    private DispatcherTimer? _autoTimer;
    private bool _autoRefresh;
    private volatile bool _isScanning;
    private string _searchFilter = "";
    private bool _initialized;

    public ProcessMonitorView()
    {
        InitializeComponent();
        Loaded += (s, e) => ApplyLocalization();
        LocalizationService.LanguageChanged += OnLanguageChanged;
        Unloaded += (s, e) => LocalizationService.LanguageChanged -= OnLanguageChanged;
        _module = App.Services.GetServices<IOptimizationModule>()
            .OfType<ProcessMonitorModule>().FirstOrDefault();
        Loaded += async (s, e) =>
        {
            if (_initialized) return;
            _initialized = true;
            await RunScan();
        };
        Unloaded += (s, e) => { StopAutoRefresh(); LocalizationService.LanguageChanged -= OnLanguageChanged; };
    }

    private void OnLanguageChanged() =>
        global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);

    private async Task RunScan()
    {
        if (_module is null || _isScanning) return;
        _isScanning = true;
        ScanBtnLabel.Text = LocalizationService._("common.scanning");

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
        catch { SubtitleText.Text = LocalizationService._("common.scanFailed"); }
        finally
        {
            _isScanning = false;
            ScanBtnLabel.Text = LocalizationService._("common.scan");
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
                    $"kill:{p.Pid}", LocalizationService._("common.kill"),
                    $"suspend:{p.Pid}",
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
            AutoRefreshLabel.Text = LocalizationService._("procMon.autoOn");
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
        AutoRefreshLabel.Text = LocalizationService._("procMon.autoOff");
        if (_autoTimer is not null)
        {
            _autoTimer.Stop();
            _autoTimer = null;
        }
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
        var L = LocalizationService._;
        PageTitle.Text = L("nav.processMonitor");
        ModuleHdr.Title = L("procMon.title");
        ModuleHdr.Subtitle = L("procMon.subtitle");
        AutoRefreshLabel.Text = _autoRefresh ? L("procMon.autoOn") : L("procMon.autoOff");
        ScanBtnLabel.Text = L("common.scan");
        LblProcesses.Text = L("procMon.processes");
        LblTotalCpu.Text = L("procMon.totalCpu");
        LblTotalRam.Text = L("procMon.totalRam");
        ColDescription.Text = L("procMon.colDescription");
        ColRamMb.Text = L("procMon.colRamMb");
        ColThreads.Text = L("procMon.colThreads");
        ColActions.Text = L("procMon.colActions");
        SearchBox.Watermark = L("procMon.searchPlaceholder");
        // Re-render process list so Kill labels update
        if (_module?.LastReport is not null) RenderProcessList(_module.LastReport);
    }
}