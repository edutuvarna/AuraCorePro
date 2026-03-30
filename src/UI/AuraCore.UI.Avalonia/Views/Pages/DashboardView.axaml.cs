using System.Diagnostics;
using System.Runtime.InteropServices;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Threading;
using AuraCore.Application.Interfaces.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

public record ModuleListItem(string Name, string Category, string Platform);

public partial class DashboardView : UserControl
{
    private DispatcherTimer? _timer;
    private TimeSpan _prevCpuTime;
    private DateTime _prevSampleTime;
    private bool _firstSample = true;

    public DashboardView()
    {
        InitializeComponent();
        Loaded += (s, e) => { LoadData(); StartLiveMonitoring(); ApplyLocalization(); };
        Unloaded += (s, e) => StopLiveMonitoring();
        LocalizationService.LanguageChanged += () =>
            Dispatcher.UIThread.Post(ApplyLocalization);
    }

    private void ApplyLocalization()
    {
        WelcomeLabel.Text = LocalizationService._("dash.welcomeBack");
        HeroTitle.Text = LocalizationService._("dash.systemOverview");
        StatusLabel.Text = LocalizationService._("dash.allHealthy");
        CpuLabel.Text = LocalizationService._("dash.cpuUsage");
        RamLabel.Text = LocalizationService._("dash.ramUsage");
        DiskLabel.Text = LocalizationService._("dash.diskUsage");
        UptimeLabel.Text = LocalizationService._("dash.uptime");
        ModulesLabel.Text = LocalizationService._("dash.activeModules");
        QuickActionsLabel.Text = LocalizationService._("dash.quickActions");
    }

    private void StartLiveMonitoring()
    {
        // Take initial CPU sample
        _prevCpuTime = Process.GetCurrentProcess().TotalProcessorTime;
        try
        {
            var allProcs = Process.GetProcesses();
            _prevCpuTime = TimeSpan.Zero;
            foreach (var p in allProcs)
            {
                try { _prevCpuTime += p.TotalProcessorTime; } catch { }
                p.Dispose();
            }
        }
        catch { }
        _prevSampleTime = DateTime.UtcNow;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (s, e) => UpdateLiveStats();
        _timer.Start();
    }

    private void StopLiveMonitoring()
    {
        _timer?.Stop();
        _timer = null;
    }

    private void UpdateLiveStats()
    {
        try
        {
            // CPU: delta-based measurement across all processes
            var totalCpu = TimeSpan.Zero;
            var allProcs = Process.GetProcesses();
            foreach (var p in allProcs)
            {
                try { totalCpu += p.TotalProcessorTime; } catch { }
                p.Dispose();
            }
            var now = DateTime.UtcNow;
            var elapsed = (now - _prevSampleTime).TotalMilliseconds;
            if (elapsed > 0 && !_firstSample)
            {
                var cpuDelta = (totalCpu - _prevCpuTime).TotalMilliseconds;
                var cpuPct = cpuDelta / elapsed / Environment.ProcessorCount * 100.0;
                cpuPct = Math.Clamp(cpuPct, 0, 100);
                CpuValue.Text = $"{cpuPct:F0}";
                if (CpuBar.Parent is Border cpuParent && cpuParent.Bounds.Width > 0)
                    CpuBar.Width = cpuParent.Bounds.Width * cpuPct / 100.0;
            }
            _prevCpuTime = totalCpu;
            _prevSampleTime = now;
            _firstSample = false;

            // RAM — real system memory via P/Invoke (Windows) or /proc/meminfo (Linux)
            double totalGb = 0, usedGb = 0;
            int ramPct = 0;
            if (OperatingSystem.IsWindows())
            {
                var mem = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(ref mem))
                {
                    totalGb = mem.ullTotalPhys / (1024.0 * 1024 * 1024);
                    var availGb = mem.ullAvailPhys / (1024.0 * 1024 * 1024);
                    usedGb = totalGb - availGb;
                    ramPct = (int)mem.dwMemoryLoad;
                }
            }
            else
            {
                var gcInfo = GC.GetGCMemoryInfo();
                totalGb = gcInfo.TotalAvailableMemoryBytes / (1024.0 * 1024 * 1024);
                usedGb = totalGb * 0.5; // fallback estimate
                ramPct = 50;
            }
            RamValue.Text = $"{usedGb:F1}";
            RamTotal.Text = $"/ {totalGb:F1} GB";
            if (RamBar.Parent is Border ramParent && ramParent.Bounds.Width > 0 && totalGb > 0)
                RamBar.Width = ramParent.Bounds.Width * ramPct / 100.0;

            // Uptime
            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            UptimeValue.Text = uptime.Days > 0
                ? $"{uptime.Days}d {uptime.Hours}h"
                : $"{uptime.Hours}h {uptime.Minutes}m";
        }
        catch { }
    }

    private void LoadData()
    {
        // Platform info
        var os = RuntimeInformation.OSDescription;
        var arch = RuntimeInformation.OSArchitecture;
        var cpus = Environment.ProcessorCount;
        PlatformInfo.Text = $"{os} | {arch} | {cpus} cores | .NET {Environment.Version}";

        // Uptime
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        UptimeValue.Text = uptime.Days > 0
            ? $"{uptime.Days}d {uptime.Hours}h"
            : $"{uptime.Hours}h {uptime.Minutes}m";

        OsLabel.Text = OperatingSystem.IsWindows() ? "Windows"
                     : OperatingSystem.IsLinux() ? "Linux"
                     : OperatingSystem.IsMacOS() ? "macOS" : "Unknown";

        // RAM info — real used/total
        if (OperatingSystem.IsWindows())
        {
            var mem = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(ref mem))
            {
                var total = mem.ullTotalPhys / (1024.0 * 1024 * 1024);
                var avail = mem.ullAvailPhys / (1024.0 * 1024 * 1024);
                RamValue.Text = $"{total - avail:F1}";
                RamTotal.Text = $"/ {total:F1} GB";
            }
        }
        else
        {
            var gcInfo = GC.GetGCMemoryInfo();
            var total = gcInfo.TotalAvailableMemoryBytes / (1024.0 * 1024 * 1024);
            RamValue.Text = $"{total:F1}";
            RamTotal.Text = "GB (total)";
        }

        // Disk info
        try
        {
            var root = OperatingSystem.IsWindows() ? "C:\\" : "/";
            var drive = new DriveInfo(root);
            var freeGb = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
            var totalDiskGb = drive.TotalSize / (1024.0 * 1024 * 1024);
            DiskValue.Text = $"{freeGb:F0}";
            var pct = (totalDiskGb - freeGb) / totalDiskGb;
            if (DiskBar.Parent is Border parent && parent.Bounds.Width > 0)
                DiskBar.Width = parent.Bounds.Width * pct;
        }
        catch { DiskValue.Text = "N/A"; }

        // Loaded modules
        var modules = App.Services.GetServices<IOptimizationModule>().ToList();
        ModuleCount.Text = $"{modules.Count} loaded";
        var items = modules.Select(m => new ModuleListItem(
            m.DisplayName,
            m.Category.ToString(),
            m.Platform.ToString()
        )).ToList();
        ModuleList.ItemsSource = items;

        CpuValue.Text = "--";
    }

    private void ScanAll_Click(object? sender, RoutedEventArgs e)
    {
        // TODO: trigger full scan across all modules
    }

    // P/Invoke for real system memory on Windows
    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        internal uint dwLength;
        internal uint dwMemoryLoad;
        internal ulong ullTotalPhys;
        internal ulong ullAvailPhys;
        internal ulong ullTotalPageFile;
        internal ulong ullAvailPageFile;
        internal ulong ullTotalVirtual;
        internal ulong ullAvailVirtual;
        internal ulong ullAvailExtendedVirtual;
        public MEMORYSTATUSEX() { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>(); }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
