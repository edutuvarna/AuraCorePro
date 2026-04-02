using System.Diagnostics;
using System.Runtime.InteropServices;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Shapes;
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

    // Sparkline history (last 30 data points = 60 seconds at 2s intervals)
    private readonly Queue<double> _cpuHistory = new(30);
    private readonly Queue<double> _ramHistory = new(30);

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

        // AI badges
        CpuAnomalyText.Text = "\u26A0 " + LocalizationService._("ai.badge.cpuAnomaly");
        RamAnomalyText.Text = "\u26A0 " + LocalizationService._("ai.badge.ramAnomaly");
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

                // Sparkline: enqueue CPU value
                if (_cpuHistory.Count >= 30) _cpuHistory.Dequeue();
                _cpuHistory.Enqueue(cpuPct);
                UpdateSparkline(CpuSparkCanvas, CpuSparkLine, _cpuHistory);
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

            // Sparkline: enqueue RAM value
            if (_ramHistory.Count >= 30) _ramHistory.Dequeue();
            _ramHistory.Enqueue(ramPct);
            UpdateSparkline(RamSparkCanvas, RamSparkLine, _ramHistory);

            // Uptime
            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            UptimeValue.Text = uptime.Days > 0
                ? $"{uptime.Days}d {uptime.Hours}h"
                : $"{uptime.Hours}h {uptime.Minutes}m";

            // ── AI Engine: push metrics (reuse allProcs from CPU section) ──
            if (_aiEngine != null)
            {
                try
                {
                    // Collect top 10 processes by working set — reuse the snapshot
                    // We re-enumerate because allProcs was already disposed above.
                    // Use a single call and extract both CPU + top-process data.
                    var aiProcs = Process.GetProcesses();
                    var topProcs = new List<AIProcessMetric>();
                    try
                    {
                        topProcs = aiProcs
                            .Select(p => { try { return new { p.ProcessName, p.WorkingSet64 }; } catch { return null; } })
                            .Where(x => x != null)
                            .OrderByDescending(x => x!.WorkingSet64)
                            .Take(10)
                            .Select(x => new AIProcessMetric(x!.ProcessName, x.WorkingSet64))
                            .ToList();
                    }
                    finally
                    {
                        foreach (var p in aiProcs) try { p.Dispose(); } catch { }
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
                        _ = _aiEngine.AnalyzeAsync().ContinueWith(t =>
                        {
                            if (t.IsFaulted) System.Diagnostics.Debug.WriteLine($"AI analysis error: {t.Exception?.InnerException?.Message}");
                        }, TaskContinuationOptions.OnlyOnFaulted);
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
            RamLeakText.Text = "\U0001F50D " + string.Format(LocalizationService._("ai.badge.memoryLeak"), leak.ProcessName);
        }
        else
        {
            RamLeakBadge.IsVisible = false;
        }

        // Disk prediction badge
        if (result.DiskPrediction is { } dp)
        {
            DiskPredictionBadge.IsVisible = true;
            DiskPredictionText.Text = "\U0001F4C8 " + string.Format(LocalizationService._("ai.badge.diskPrediction"), dp.DaysUntilFull);

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

    private void UpdateSparkline(Canvas canvas, Polyline line, Queue<double> history)
    {
        if (history.Count < 2) return;
        var w = canvas.Bounds.Width > 0 ? canvas.Bounds.Width : 100;
        var h = canvas.Bounds.Height > 0 ? canvas.Bounds.Height : 20;
        var pts = new global::Avalonia.Collections.AvaloniaList<global::Avalonia.Point>();
        var arr = history.ToArray();
        for (int i = 0; i < arr.Length; i++)
        {
            var x = w * i / (arr.Length - 1);
            var y = h - (h * Math.Clamp(arr[i], 0, 100) / 100.0);
            pts.Add(new global::Avalonia.Point(x, y));
        }
        line.Points = pts;
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
