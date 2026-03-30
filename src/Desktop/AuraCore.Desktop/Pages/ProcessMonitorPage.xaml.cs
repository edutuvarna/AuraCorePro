using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using AuraCore.Application;
using AuraCore.Desktop.Services;
using AuraCore.Module.ProcessMonitor;
using AuraCore.Module.ProcessMonitor.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AuraCore.Desktop.Pages;

/// <summary>Bindable row for ListView virtualization — only visible rows render.</summary>
public sealed class ProcessRowVM : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? n = null)
    { if (!EqualityComparer<T>.Default.Equals(field, value)) { field = value; PropertyChanged?.Invoke(this, new(n)); } }

    private string _name = ""; public string Name { get => _name; set => Set(ref _name, value); }
    private string _desc = ""; public string Desc { get => _desc; set => Set(ref _desc, value); }
    private string _cpu = "0%"; public string Cpu { get => _cpu; set => Set(ref _cpu, value); }
    private string _ram = "0"; public string Ram { get => _ram; set => Set(ref _ram, value); }
    private int _pid; public int Pid { get => _pid; set => Set(ref _pid, value); }
    private SolidColorBrush _cpuBrush = new(ParseHex("#4CAF50"));
    public SolidColorBrush CpuBrush { get => _cpuBrush; set => Set(ref _cpuBrush, value); }
    private SolidColorBrush _ramBrush = new(ParseHex("#FFFFFF"));
    public SolidColorBrush RamBrush { get => _ramBrush; set => Set(ref _ramBrush, value); }
    private string _suspLabel = "Suspend"; public string SuspLabel { get => _suspLabel; set => Set(ref _suspLabel, value); }
    private bool _isSuspended; public bool IsSuspended { get => _isSuspended; set => Set(ref _isSuspended, value); }

    public void UpdateFrom(ProcessInfo p)
    {
        Name = p.Name;
        Desc = string.IsNullOrEmpty(p.Description) ? "" : p.Description;
        Pid = p.Pid;
        Cpu = $"{p.CpuPercent:F1}%";
        Ram = $"{p.MemoryMb:N0}";
        CpuBrush = new SolidColorBrush(ParseHex(p.CpuPercent > 50 ? "#F44336" : p.CpuPercent > 20 ? "#FF9800" : "#4CAF50"));
        RamBrush = new SolidColorBrush(ParseHex(p.MemoryMb > 1000 ? "#9C27B0" : p.MemoryMb > 500 ? "#2196F3" : "#BBBBBB"));
        IsSuspended = p.Status == "Suspended";
        SuspLabel = IsSuspended ? "Resume" : "Suspend";
    }

    private static Windows.UI.Color ParseHex(string hex)
    {
        hex = hex.TrimStart('#');
        return Windows.UI.Color.FromArgb(255,
            Convert.ToByte(hex[..2], 16), Convert.ToByte(hex[2..4], 16), Convert.ToByte(hex[4..6], 16));
    }
}

public sealed partial class ProcessMonitorPage : Page
{
    private ProcessMonitorModule? _module;
    private List<ProcessInfo> _allProcs = new();
    private DispatcherTimer? _autoTimer;
    private volatile bool _isScanning;

    private readonly ObservableCollection<ProcessRowVM> _rows = new();

    public ProcessMonitorPage()
    {
        InitializeComponent();
        ProcessListView.ItemsSource = _rows;
        ApplyLocalization();
        S.LanguageChanged += () => DispatcherQueue?.TryEnqueue(ApplyLocalization);
        Loaded += Page_Loaded;
        Unloaded += (s, e) => { _autoTimer?.Stop(); };
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _module = App.Current.Services.GetServices<AuraCore.Application.Interfaces.Modules.IOptimizationModule>()
            .FirstOrDefault(m => m.Id == "process-monitor") as ProcessMonitorModule;
        await FullScan();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await FullScan();

    private void AutoRefresh_Changed(object sender, RoutedEventArgs e)
    {
        if (AutoRefreshBtn.IsChecked == true)
        {
            _autoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _autoTimer.Tick += AutoTimer_Tick;
            _autoTimer.Start();
        }
        else
        {
            _autoTimer?.Stop();
            _autoTimer = null;
        }
    }

    private async void AutoTimer_Tick(object? sender, object e)
    {
        if (_isScanning) return;
        await LightweightRefresh();
    }

    private void Search_Changed(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        var q = SearchBox.Text.Trim().ToLower();
        var filtered = string.IsNullOrEmpty(q)
            ? _allProcs
            : _allProcs.Where(p => p.Name.ToLower().Contains(q) || p.Pid.ToString().Contains(q)).ToList();
        RebuildRows(filtered);
    }

    /// <summary>Full scan: background data fetch + rebuild rows.</summary>
    private async Task FullScan()
    {
        if (_module is null || _isScanning) return;
        _isScanning = true;
        _autoTimer?.Stop();
        try
        {
            ScanProgress.IsActive = true;
            RefreshBtn.IsEnabled = false;

            await Task.Run(() => _module.ScanAsync(new ScanOptions()));
            _allProcs = _module.LastReport?.Processes ?? new();

            UpdateStats();
            ApplyFilter();
        }
        catch { }
        finally
        {
            ScanProgress.IsActive = false;
            RefreshBtn.IsEnabled = true;
            _isScanning = false;
            if (AutoRefreshBtn.IsChecked == true) _autoTimer?.Start();
        }
    }

    /// <summary>Lightweight: fetch data in bg, update existing row VMs in-place. NO UI rebuild.</summary>
    private async Task LightweightRefresh()
    {
        if (_module is null || _isScanning) return;
        _isScanning = true;
        _autoTimer?.Stop();
        try
        {
            await Task.Run(() => _module.ScanAsync(new ScanOptions()));
            _allProcs = _module.LastReport?.Processes ?? new();
            UpdateStats();

            // Build PID lookup from fresh data
            var lookup = new Dictionary<int, ProcessInfo>();
            foreach (var p in _allProcs) lookup[p.Pid] = p;

            // Update existing rows in-place (no UI element creation)
            foreach (var row in _rows)
            {
                if (lookup.TryGetValue(row.Pid, out var fresh))
                    row.UpdateFrom(fresh);
            }
        }
        catch { }
        finally
        {
            _isScanning = false;
            if (AutoRefreshBtn.IsChecked == true) _autoTimer?.Start();
        }
    }

    private void UpdateStats()
    {
        StatCount.Text = _allProcs.Count.ToString();
        StatCpu.Text = $"{_module?.LastReport?.TotalCpuPercent:F1}%";
        StatRam.Text = $"{_module?.LastReport?.TotalMemoryMb:N0} MB";
    }

    /// <summary>Rebuild ObservableCollection from filtered list. ListView virtualizes = only visible rows render.</summary>
    private void RebuildRows(List<ProcessInfo> procs)
    {
        _rows.Clear();
        foreach (var p in procs.Take(200))
        {
            var vm = new ProcessRowVM();
            vm.UpdateFrom(p);
            _rows.Add(vm);
        }
    }

    // --- Button click handlers (Tag = PID) ---
    private async void Kill_Click(object sender, RoutedEventArgs e)
    {
        if (_module is null || sender is not Button btn || btn.Tag is not int pid) return;
        await _module.OptimizeAsync(new OptimizationPlan("process-monitor", new List<string> { $"kill:{pid}" }));
        await FullScan();
    }

    private async void KillTree_Click(object sender, RoutedEventArgs e)
    {
        if (_module is null || sender is not Button btn || btn.Tag is not int pid) return;
        await _module.OptimizeAsync(new OptimizationPlan("process-monitor", new List<string> { $"killtree:{pid}" }));
        await FullScan();
    }

    private async void Suspend_Click(object sender, RoutedEventArgs e)
    {
        if (_module is null || sender is not Button btn || btn.Tag is not int pid) return;
        var vm = _rows.FirstOrDefault(r => r.Pid == pid);
        if (vm is null) return;
        var act = vm.IsSuspended ? "resume" : "suspend";
        await _module.OptimizeAsync(new OptimizationPlan("process-monitor", new List<string> { $"{act}:{pid}" }));
        await FullScan();
    }

    private void ApplyLocalization()
    {
        PageTitle.Text    = S._("proc.title");
        PageSubtitle.Text = S._("proc.subtitle");
    }
}
