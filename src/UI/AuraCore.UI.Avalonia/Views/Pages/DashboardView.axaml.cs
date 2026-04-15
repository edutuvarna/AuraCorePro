using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using global::Avalonia.Controls;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;
using AuraCore.Application.Interfaces.Engines;
using AuraCore.UI.Avalonia.Helpers;
using AuraCore.UI.Avalonia.ViewModels;
using AuraCore.UI.Avalonia.Views.Controls;
using AuraCore.UI.Avalonia.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class DashboardView : UserControl
{
    private readonly DashboardViewModel _vm = new();
    private DispatcherTimer? _timer;
    private IAIAnalyzerEngine? _aiEngine;
    private bool _initialized;
    private bool _narrow;

    // GPU polling runs on background thread to avoid blocking UI with typeperf process spawn.
    // Cached value is read synchronously per tick; fresh sample kicked off periodically.
    private double _cachedGpuUsage = 0;
    private DateTime _lastGpuSampleUtc = DateTime.MinValue;
    private int _gpuSampleInFlight = 0; // 0 = free, 1 = busy (Interlocked)
    private static readonly TimeSpan GpuSampleInterval = TimeSpan.FromSeconds(2);

    // CPU polling via Windows PerformanceCounter (fast, ~microseconds per NextValue).
    // Linux uses /proc/stat delta; both give real system CPU% not per-process time.
    private global::System.Diagnostics.PerformanceCounter? _cpuCounter;
    private (ulong Total, ulong Idle)? _lastProcStatSnapshot;

    public DashboardView()
    {
        InitializeComponent();
        DataContext = _vm;
        Loaded += (s, e) =>
        {
            if (_initialized) return;
            _initialized = true;
            try { _aiEngine = App.Services.GetService<IAIAnalyzerEngine>(); } catch { }
            DetectGpu();
            LoadStaticSystemInfo();
            StartPolling();
            HookHeroButton();
            HookResponsiveBreakpoint();
        };
        Unloaded += (s, e) => StopPolling();
    }

    private void DetectGpu()
    {
        var info = GpuInfoHelper.Detect();
        _vm.SetGpuInfo(info);
        if (info is not null)
        {
            GpuGauge.IsVisible = true;
            // Name goes in the footer Insight slot so it doesn't overflow the gauge ring center.
            // Keep SubLabel empty to avoid cluttering the center.
            GpuGauge.SubLabel = "%";
            GpuGauge.Insight = TruncateForDisplay(info.Name, 22);
        }
        else
        {
            // No GPU detected: hide the GpuGauge AND collapse its column
            // so remaining gauges evenly fill the row (no awkward empty slot).
            GpuGauge.IsVisible = false;
            if (GaugeRow.ColumnDefinitions.Count >= 3)
                GaugeRow.ColumnDefinitions[2].Width = new global::Avalonia.Controls.GridLength(0);
        }

        // Update SystemInfo GPU row (may have been called before LoadStaticSystemInfo)
        if (GpuText is not null)
            GpuText.Text = _vm.GpuInfo is not null ? _vm.GpuInfo.Name : "—";
    }

    private void LoadStaticSystemInfo()
    {
        _vm.OsName = GetOsName();
        _vm.CpuName = GetCpuName();
        _vm.RamTotalGb = GetTotalRamGb();

        // Populate the multi-row SYSTEM card
        OsText.Text = _vm.OsName;
        CpuText.Text = _vm.CpuName;
        GpuText.Text = _vm.GpuInfo is not null ? _vm.GpuInfo.Name : "—";
        RamText.Text = $"{_vm.RamTotalGb:0.#} GB";
        UpdateUptime();
    }

    private void UpdateUptime()
    {
        try
        {
            var ts = TimeSpan.FromMilliseconds(Environment.TickCount64);
            UptimeText.Text = ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}h {ts.Minutes}m"
                : $"{ts.Minutes}m";
        }
        catch { UptimeText.Text = "—"; }
    }

    private static string TruncateForDisplay(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
        return s.Substring(0, max - 1) + "…";
    }

    private void HookHeroButton()
    {
        HeroCta.PrimaryCommand = new RelayCommand(ShowSmartOptimizeDialog);
    }

    private void ShowSmartOptimizeDialog()
    {
        var dlg = new SmartOptimizePlaceholderDialog();
        dlg.GoToRecommendationsRequested += (_, _) =>
        {
            if (this.GetVisualRoot() is Window w && w is Views.MainWindow main)
                main.NavigateToModule("ai-recommendations");
        };
        if (this.GetVisualRoot() is Window owner)
            dlg.ShowDialog(owner);
    }

    private void HookResponsiveBreakpoint()
    {
        if (this.GetVisualRoot() is not Window win) return;
        win.SizeChanged += (_, e) => ApplyResponsiveLayout(e.NewSize.Width);
        ApplyResponsiveLayout(win.Width);
    }

    private void ApplyResponsiveLayout(double width)
    {
        var narrow = width < 1000;
        if (narrow == _narrow) return;
        _narrow = narrow;

        SystemInfoCard.IsVisible = !narrow;
        MonitoringText.Text = narrow ? "Cortex monitoring" : "Cortex is monitoring · Auto-detected";

        // In narrow mode, Quick Actions card expands to fill the full bottom row
        // (since SystemInfoCard is hidden, we avoid a lonely Quick Actions on the right).
        var quickActionsCard = SystemInfoCard.GetVisualParent() is global::Avalonia.Controls.Grid bottomRow
            && bottomRow.Children.Count > 1
                ? bottomRow.Children[1] as global::Avalonia.Controls.Border
                : null;
        if (quickActionsCard is not null)
        {
            global::Avalonia.Controls.Grid.SetColumnSpan(quickActionsCard, narrow ? 2 : 1);
            global::Avalonia.Controls.Grid.SetColumn(quickActionsCard, narrow ? 0 : 1);
            quickActionsCard.Margin = narrow
                ? new global::Avalonia.Thickness(0)
                : new global::Avalonia.Thickness(8, 0, 0, 0);
        }
    }

    private void StartPolling()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += (_, _) => TickOnce();
        _timer.Start();
        TickOnce();
    }

    private void StopPolling()
    {
        if (_timer is null) return;
        _timer.Stop();
        _timer = null;
    }

    private void TickOnce()
    {
        try
        {
            _vm.CpuPercent = SampleCpu();
            _vm.RamPercent = SampleRamPercent();
            _vm.DiskPercent = SampleDiskPercent();
            if (_vm.GpuVisible)
            {
                // Read cached GPU value (instant) and trigger background refresh if stale.
                _vm.GpuPercent = _cachedGpuUsage;
                GpuGauge.Value = _cachedGpuUsage;
                MaybeRefreshGpuInBackground();
            }
            _vm.HealthScore = ComputeHealth(_vm.CpuPercent, _vm.RamPercent, _vm.DiskPercent);
            _vm.HealthLabel = _vm.HealthScore >= 85 ? "Excellent" : _vm.HealthScore >= 60 ? "Good" : "Needs attention";

            CpuGauge.Value = _vm.CpuPercent;
            RamGauge.Value = _vm.RamPercent;
            DiskGauge.Value = _vm.DiskPercent;
            HealthGauge.Value = _vm.HealthScore;
            HealthGauge.Insight = _vm.HealthLabel;
            UpdateUptime();
        }
        catch { }
    }

    /// <summary>
    /// Kicks off a background GPU usage sample if the cached value is stale and no sample is in flight.
    /// GpuInfoHelper.GetCurrentUsage spawns `typeperf` on Windows which can block 100-2000ms — MUST NOT
    /// run on the UI thread. Caller (TickOnce) keeps rendering at full 500ms cadence without hitching.
    /// </summary>
    private void MaybeRefreshGpuInBackground()
    {
        if (DateTime.UtcNow - _lastGpuSampleUtc < GpuSampleInterval) return;
        // Interlocked guard: single in-flight sample at a time
        if (global::System.Threading.Interlocked.Exchange(ref _gpuSampleInFlight, 1) == 1) return;

        _ = global::System.Threading.Tasks.Task.Run(() =>
        {
            double usage = 0;
            try { usage = GpuInfoHelper.GetCurrentUsage(); } catch { }
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _cachedGpuUsage = usage;
                _lastGpuSampleUtc = DateTime.UtcNow;
                global::System.Threading.Interlocked.Exchange(ref _gpuSampleInFlight, 0);
            });
        });
    }

    private double SampleCpu()
    {
        if (OperatingSystem.IsWindows()) return SampleCpuWindows();
        if (OperatingSystem.IsLinux()) return SampleCpuLinux();
        return 0;
    }

    [global::System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private double SampleCpuWindows()
    {
        try
        {
            if (_cpuCounter is null)
            {
                _cpuCounter = new global::System.Diagnostics.PerformanceCounter(
                    "Processor", "% Processor Time", "_Total", readOnly: true);
                _cpuCounter.NextValue(); // first call always returns 0 — prime it
                return 0;
            }
            return Math.Clamp(_cpuCounter.NextValue(), 0, 100);
        }
        catch
        {
            _cpuCounter = null; // reset; retry on next tick
            return 0;
        }
    }

    [global::System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private double SampleCpuLinux()
    {
        try
        {
            // /proc/stat line "cpu  user nice system idle iowait irq softirq steal ..."
            // CPU% = 100 * (delta_total - delta_idle) / delta_total between two samples.
            var firstLine = File.ReadLines("/proc/stat").First();
            var parts = firstLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5 || parts[0] != "cpu") return 0;

            ulong user = ulong.Parse(parts[1]);
            ulong nice = ulong.Parse(parts[2]);
            ulong system = ulong.Parse(parts[3]);
            ulong idle = ulong.Parse(parts[4]);
            ulong iowait = parts.Length > 5 ? ulong.Parse(parts[5]) : 0;
            ulong irq = parts.Length > 6 ? ulong.Parse(parts[6]) : 0;
            ulong softirq = parts.Length > 7 ? ulong.Parse(parts[7]) : 0;
            ulong steal = parts.Length > 8 ? ulong.Parse(parts[8]) : 0;

            ulong total = user + nice + system + idle + iowait + irq + softirq + steal;
            ulong totalIdle = idle + iowait;

            if (_lastProcStatSnapshot is null)
            {
                _lastProcStatSnapshot = (total, totalIdle);
                return 0;
            }

            var (prevTotal, prevIdle) = _lastProcStatSnapshot.Value;
            ulong dTotal = total - prevTotal;
            ulong dIdle = totalIdle - prevIdle;
            _lastProcStatSnapshot = (total, totalIdle);

            if (dTotal == 0) return 0;
            return Math.Clamp(100.0 * (dTotal - dIdle) / dTotal, 0, 100);
        }
        catch { return 0; }
    }

    private double SampleRamPercent()
    {
        if (OperatingSystem.IsWindows())
        {
            var ms = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref ms))
                return 100.0 * (ms.ullTotalPhys - ms.ullAvailPhys) / Math.Max(ms.ullTotalPhys, 1UL);
            return 0;
        }
        if (OperatingSystem.IsLinux())
        {
            try
            {
                var lines = File.ReadAllLines("/proc/meminfo");
                ulong total = 0, avail = 0;
                foreach (var l in lines)
                {
                    if (l.StartsWith("MemTotal:")) total = ParseKb(l);
                    else if (l.StartsWith("MemAvailable:")) avail = ParseKb(l);
                }
                if (total > 0) return 100.0 * (total - avail) / total;
            }
            catch { }
        }
        return 0;
    }

    private double SampleDiskPercent()
    {
        try
        {
            var root = OperatingSystem.IsWindows() ? "C:\\" : "/";
            var di = new DriveInfo(root);
            if (di.TotalSize > 0)
                return 100.0 * (di.TotalSize - di.AvailableFreeSpace) / di.TotalSize;
        }
        catch { }
        return 0;
    }

    private static ulong ParseKb(string memInfoLine)
    {
        var parts = memInfoLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && ulong.TryParse(parts[1], out var v) ? v * 1024 : 0;
    }

    private static double ComputeHealth(double cpu, double ram, double disk)
    {
        var score = 100.0;
        if (cpu > 80) score -= 15;
        if (ram > 85) score -= 15;
        if (disk > 90) score -= 10;
        return Math.Clamp(score, 0, 100);
    }

    private static string GetOsName() =>
        RuntimeInformation.OSDescription ?? Environment.OSVersion.ToString();

    private static string GetCpuName()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = "wmic", Arguments = "cpu get name",
                    RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
                });
                if (p is not null)
                {
                    var o = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(2000);
                    var lines = o.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l) && l != "Name").ToList();
                    if (lines.Count > 0) return lines[0];
                }
            }
            catch { }
        }
        if (OperatingSystem.IsLinux())
        {
            try
            {
                var lines = File.ReadAllLines("/proc/cpuinfo");
                foreach (var l in lines)
                    if (l.StartsWith("model name"))
                        return l.Split(':', 2)[1].Trim();
            }
            catch { }
        }
        return $"{Environment.ProcessorCount} cores";
    }

    private static double GetTotalRamGb()
    {
        if (OperatingSystem.IsWindows())
        {
            var ms = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref ms)) return ms.ullTotalPhys / (1024.0 * 1024 * 1024);
        }
        if (OperatingSystem.IsLinux())
        {
            try
            {
                var lines = File.ReadAllLines("/proc/meminfo");
                foreach (var l in lines)
                    if (l.StartsWith("MemTotal:")) return ParseKb(l) / (1024.0 * 1024 * 1024);
            }
            catch { }
        }
        return 0;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    private sealed class RelayCommand : System.Windows.Input.ICommand
    {
        private readonly Action _action;
        public RelayCommand(Action action) => _action = action;
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _action();
    }
}
