using System.Diagnostics;
using System.Runtime.InteropServices;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using global::Avalonia.Threading;
using AuraCore.Application.Interfaces.Engines;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class DashboardView : UserControl
{
    private DispatcherTimer? _timer;
    private TimeSpan _prevCpuTime;
    private DateTime _prevSampleTime;
    private bool _firstSample = true;
    private bool _initialized;

    // AI Engine integration
    private IAIAnalyzerEngine? _aiEngine;
    private int _aiTickCounter;
    private float _lastCpuPct;
    private float _lastRamPct;
    private float _lastDiskUsedPct;

    public DashboardView()
    {
        InitializeComponent();
        Loaded += (s, e) =>
        {
            if (_initialized) return;
            _initialized = true;

            // Initialize AI engine
            try { _aiEngine = App.Services.GetService<IAIAnalyzerEngine>(); } catch { }
            if (_aiEngine != null)
                _aiEngine.AnalysisCompleted += OnAIAnalysisCompleted;

            LoadData(); StartLiveMonitoring(); ApplyLocalization();
        };
        Unloaded += (s, e) =>
        {
            StopLiveMonitoring();
            if (_aiEngine != null)
                _aiEngine.AnalysisCompleted -= OnAIAnalysisCompleted;
        };
        LocalizationService.LanguageChanged += () =>
            Dispatcher.UIThread.Post(ApplyLocalization);
    }

    private void ApplyLocalization()
    {
        HeroTitle.Text = "Dashboard";
        WelcomeLabel.Text = "AI Powered System Intelligence";
        StatusLabel.Text = "LIVE";
        CpuLabel.Text = "CPU";
        RamLabel.Text = "RAM";
        DiskLabel.Text = OperatingSystem.IsWindows() ? "Disk (C:)" : "Disk (/)";
        UptimeLabel.Text = "Uptime";
        QuickActionsLabel.Text = "Quick Actions";
    }

    private void StartLiveMonitoring()
    {
        // Take initial CPU sample
        _prevCpuTime = Process.GetCurrentProcess().TotalProcessorTime;
        try
        {
            var allProcs = Process.GetProcesses();
            try
            {
                _prevCpuTime = TimeSpan.Zero;
                foreach (var p in allProcs)
                {
                    try { _prevCpuTime += p.TotalProcessorTime; } catch { }
                }
            }
            finally
            {
                foreach (var p in allProcs) try { p.Dispose(); } catch { }
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
        if (_timer is not null)
        {
            _timer.Stop();
            _timer = null;
        }
    }

    private void UpdateLiveStats()
    {
        try
        {
            // CPU: delta-based measurement across all processes
            var totalCpu = TimeSpan.Zero;
            var allProcs = Process.GetProcesses();
            try
            {
                foreach (var p in allProcs)
                {
                    try { totalCpu += p.TotalProcessorTime; } catch { }
                }
            }
            finally
            {
                foreach (var p in allProcs) try { p.Dispose(); } catch { }
            }
            var now = DateTime.UtcNow;
            var elapsed = (now - _prevSampleTime).TotalMilliseconds;
            if (elapsed > 0 && !_firstSample)
            {
                var cpuDelta = (totalCpu - _prevCpuTime).TotalMilliseconds;
                var cpuPct = cpuDelta / elapsed / Environment.ProcessorCount * 100.0;
                cpuPct = Math.Clamp(cpuPct, 0, 100);
                _lastCpuPct = (float)cpuPct;
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
            else if (OperatingSystem.IsLinux())
            {
                // Linux: read /proc/meminfo
                try
                {
                    long totalKb = 0, availKb = 0;
                    foreach (var line in File.ReadLines("/proc/meminfo"))
                    {
                        if (line.StartsWith("MemTotal:"))
                            totalKb = ParseMemKb(line);
                        else if (line.StartsWith("MemAvailable:"))
                            availKb = ParseMemKb(line);
                    }
                    totalGb = totalKb / (1024.0 * 1024);
                    var availGb = availKb / (1024.0 * 1024);
                    usedGb = totalGb - availGb;
                    ramPct = totalGb > 0 ? (int)(usedGb / totalGb * 100) : 0;
                }
                catch { }
            }
            else
            {
                // macOS/other fallback
                var gcInfo = GC.GetGCMemoryInfo();
                totalGb = gcInfo.TotalAvailableMemoryBytes / (1024.0 * 1024 * 1024);
                usedGb = totalGb * 0.5;
                ramPct = 50;
            }
            _lastRamPct = totalGb > 0 ? (float)(usedGb / totalGb * 100.0) : 0f;
            RamValue.Text = $"{usedGb:F1}";
            RamTotal.Text = $"/ {totalGb:F1} GB";
            if (RamBar.Parent is Border ramParent && ramParent.Bounds.Width > 0 && totalGb > 0)
                RamBar.Width = ramParent.Bounds.Width * ramPct / 100.0;

            // Uptime
            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            UptimeValue.Text = uptime.Days > 0
                ? $"{uptime.Days}d {uptime.Hours}h"
                : $"{uptime.Hours}h {uptime.Minutes}m";

            // ── AI Engine: push metrics ──
            if (_aiEngine != null)
            {
                try
                {
                    // Collect top 10 processes by working set
                    var procs = Process.GetProcesses();
                    var topProcs = new List<AIProcessMetric>();
                    try
                    {
                        topProcs = procs
                            .Select(p => { try { return new { p.ProcessName, p.WorkingSet64 }; } catch { return null; } })
                            .Where(x => x != null)
                            .OrderByDescending(x => x!.WorkingSet64)
                            .Take(10)
                            .Select(x => new AIProcessMetric(x!.ProcessName, x.WorkingSet64))
                            .ToList();
                    }
                    finally
                    {
                        foreach (var p in procs) try { p.Dispose(); } catch { }
                    }

                    // Compute disk used percent for the sample
                    float diskUsedPct = _lastDiskUsedPct;
                    try
                    {
                        var root = OperatingSystem.IsWindows()
                            ? (DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady)?.Name ?? "C:\\")
                            : "/";
                        var drv = new DriveInfo(root);
                        diskUsedPct = (float)((drv.TotalSize - drv.AvailableFreeSpace) * 100.0 / drv.TotalSize);
                        _lastDiskUsedPct = diskUsedPct;
                    }
                    catch { }

                    var sample = new AIMetricSample(
                        DateTimeOffset.UtcNow,
                        _lastCpuPct,
                        _lastRamPct,
                        diskUsedPct,
                        topProcs);

                    _aiEngine.Push(sample);

                    // Every 30 ticks (60 seconds) trigger analysis
                    _aiTickCounter++;
                    if (_aiTickCounter >= 30)
                    {
                        _aiTickCounter = 0;
                        _ = _aiEngine.AnalyzeAsync();
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    private void LoadData()
    {
        // System info card
        SysOsText.Text = RuntimeInformation.OSDescription;
        SysCpuText.Text = $"{Environment.ProcessorCount} cores | {RuntimeInformation.OSArchitecture}";

        // Uptime
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        var uptimeStr = uptime.Days > 0
            ? $"{uptime.Days}d {uptime.Hours}h"
            : $"{uptime.Hours}h {uptime.Minutes}m";
        UptimeValue.Text = uptimeStr;
        SysUptimeText.Text = uptimeStr;

        OsLabel.Text = OperatingSystem.IsWindows() ? "\u2B22 Windows"
                     : OperatingSystem.IsLinux() ? "\u2B22 Linux"
                     : OperatingSystem.IsMacOS() ? "\u2B22 macOS" : "Unknown";

        OsLabel.Foreground = OperatingSystem.IsWindows()
            ? new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#0080FF"))
            : OperatingSystem.IsLinux()
            ? new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#F59E0B"))
            : new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#8B5CF6"));

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
                SysRamText.Text = $"{total - avail:F1} / {total:F1} GB";
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            try
            {
                long totalKb = 0, availKb = 0;
                foreach (var line in File.ReadLines("/proc/meminfo"))
                {
                    if (line.StartsWith("MemTotal:")) totalKb = ParseMemKb(line);
                    else if (line.StartsWith("MemAvailable:")) availKb = ParseMemKb(line);
                }
                var total = totalKb / (1024.0 * 1024);
                var avail = availKb / (1024.0 * 1024);
                RamValue.Text = $"{total - avail:F1}";
                RamTotal.Text = $"/ {total:F1} GB";
                SysRamText.Text = $"{total - avail:F1} / {total:F1} GB";
            }
            catch { RamValue.Text = "N/A"; }
        }
        else
        {
            var gcInfo = GC.GetGCMemoryInfo();
            var total = gcInfo.TotalAvailableMemoryBytes / (1024.0 * 1024 * 1024);
            RamValue.Text = $"{total:F1}";
            RamTotal.Text = "GB (total)";
            SysRamText.Text = $"{total:F1} GB (total)";
        }

        // Disk info
        try
        {
            var root = OperatingSystem.IsWindows()
                ? (DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady)?.Name ?? "C:\\")
                : "/";
            var drive = new DriveInfo(root);
            var freeGb = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
            var totalDiskGb = drive.TotalSize / (1024.0 * 1024 * 1024);
            DiskValue.Text = $"{freeGb:F0}";
            var pct = (totalDiskGb - freeGb) / totalDiskGb;
            if (DiskBar.Parent is Border parent && parent.Bounds.Width > 0)
                DiskBar.Width = parent.Bounds.Width * pct;
        }
        catch { DiskValue.Text = "N/A"; }

        CpuValue.Text = "--";

        // OS-aware Quick Actions
        BuildQuickActions();
    }

    private void BuildQuickActions()
    {
        QuickActionsPanel.Children.Clear();

        // Common action - Full System Scan
        QuickActionsPanel.Children.Add(MakeQuickAction("\u2699", "Full System Scan", "Analyze all modules", ScanAll_Click));

        if (OperatingSystem.IsWindows())
        {
            QuickActionsPanel.Children.Add(MakeQuickAction("\u267B", "Clean Junk Files", "Temp, cache, logs cleanup", null));
            QuickActionsPanel.Children.Add(MakeQuickAction("\u26A1", "Optimize RAM", "Free up working set memory", null));
            QuickActionsPanel.Children.Add(MakeQuickAction("\u2692", "Registry Clean", "Scan for registry issues", null));
        }
        else if (OperatingSystem.IsLinux())
        {
            QuickActionsPanel.Children.Add(MakeQuickAction("\u267B", "Clean Package Cache", "APT/pip/npm cache cleanup", null));
            QuickActionsPanel.Children.Add(MakeQuickAction("\u2699", "Systemd Status", "Check failed services", null));
            QuickActionsPanel.Children.Add(MakeQuickAction("\u26A1", "Swap Status", "Monitor swap usage", null));
        }
        else if (OperatingSystem.IsMacOS())
        {
            QuickActionsPanel.Children.Add(MakeQuickAction("\u267B", "Brew Cleanup", "Clean Homebrew cache", null));
            QuickActionsPanel.Children.Add(MakeQuickAction("\u2699", "Defaults Tweaks", "Apply macOS optimizations", null));
            QuickActionsPanel.Children.Add(MakeQuickAction("\u26A1", "Launch Agents", "Manage startup items", null));
        }
    }

    private static Button MakeQuickAction(string icon, string title, string subtitle, EventHandler<RoutedEventArgs>? handler)
    {
        var btn = new Button();
        btn.Classes.Add("action-btn");
        if (handler != null) btn.Click += handler;
        btn.Content = new StackPanel
        {
            Orientation = global::Avalonia.Layout.Orientation.Horizontal,
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = icon, FontSize = 16, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center },
                new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = title, FontSize = 12, FontWeight = global::Avalonia.Media.FontWeight.SemiBold },
                        new TextBlock { Text = subtitle, FontSize = 10,
                            Foreground = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#8888A0")) }
                    }
                }
            }
        };
        return btn;
    }

    private void ScanAll_Click(object? sender, RoutedEventArgs e)
    {
        // TODO: trigger full scan across all modules
    }

    // ── AI Analysis event handler (fires on background thread) ──
    private void OnAIAnalysisCompleted(AIAnalysisResult result)
    {
        Dispatcher.UIThread.Post(() => UpdateAIBadges(result));
    }

    private void UpdateAIBadges(AIAnalysisResult result)
    {
        // CPU anomaly badge
        CpuAnomalyBadge.IsVisible = result.CpuAnomaly;

        // RAM anomaly badge
        RamAnomalyBadge.IsVisible = result.RamAnomaly;

        // Memory leak badge
        if (result.MemoryLeaks.Count > 0)
        {
            var leak = result.MemoryLeaks[0];
            RamLeakBadge.IsVisible = true;
            RamLeakText.Text = $"\U0001F50D {leak.ProcessName} \u2014 sizinti suphesi";
        }
        else
        {
            RamLeakBadge.IsVisible = false;
        }

        // Disk prediction badge
        if (result.DiskPrediction is { } dp)
        {
            DiskPredictionBadge.IsVisible = true;
            DiskPredictionText.Text = $"\U0001F4C8 Tahmini dolum: {dp.DaysUntilFull} gun";

            // Color based on days remaining
            string color;
            if (dp.DaysUntilFull > 90)
                color = "#22C55E"; // green
            else if (dp.DaysUntilFull > 30)
                color = "#F59E0B"; // yellow
            else
                color = "#EF4444"; // red

            var parsedColor = Color.Parse(color);
            DiskPredictionText.Foreground = new SolidColorBrush(parsedColor);
            DiskPredictionBadge.Background = new SolidColorBrush(parsedColor) { Opacity = 0.15 };
            DiskPredictionBadge.BorderBrush = new SolidColorBrush(parsedColor) { Opacity = 0.3 };
        }
        else
        {
            DiskPredictionBadge.IsVisible = false;
        }
    }

    private static long ParseMemKb(string line)
    {
        var parts = line.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length < 2) return 0;
        var numStr = parts[1].Replace("kB", "").Trim();
        return long.TryParse(numStr, out var kb) ? kb : 0;
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
