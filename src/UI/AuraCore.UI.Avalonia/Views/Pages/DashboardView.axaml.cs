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
            GpuGauge.SubLabel = info.Name;
        }
        else
        {
            // No GPU detected: hide the GpuGauge AND collapse its column
            // so remaining gauges evenly fill the row (no awkward empty slot).
            GpuGauge.IsVisible = false;
            if (GaugeRow.ColumnDefinitions.Count >= 3)
                GaugeRow.ColumnDefinitions[2].Width = new global::Avalonia.Controls.GridLength(0);
        }
    }

    private void LoadStaticSystemInfo()
    {
        _vm.OsName = GetOsName();
        _vm.CpuName = GetCpuName();
        _vm.RamTotalGb = GetTotalRamGb();
        SystemSummaryText.Text = _vm.SystemSummary;
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
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
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
                _vm.GpuPercent = GpuInfoHelper.GetCurrentUsage();
                GpuGauge.Value = _vm.GpuPercent;
            }
            _vm.HealthScore = ComputeHealth(_vm.CpuPercent, _vm.RamPercent, _vm.DiskPercent);
            _vm.HealthLabel = _vm.HealthScore >= 85 ? "Excellent" : _vm.HealthScore >= 60 ? "Good" : "Needs attention";

            CpuGauge.Value = _vm.CpuPercent;
            RamGauge.Value = _vm.RamPercent;
            DiskGauge.Value = _vm.DiskPercent;
            HealthGauge.Value = _vm.HealthScore;
            HealthGauge.Insight = _vm.HealthLabel;
            SystemSummaryText.Text = _vm.SystemSummary;
        }
        catch { }
    }

    private double SampleCpu()
    {
        return Math.Clamp(Environment.ProcessorCount > 0
            ? (Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds * 0.5) % 100
            : 0, 0, 100);
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
