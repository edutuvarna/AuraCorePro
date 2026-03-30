using AuraCore.Desktop.Services;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using AuraCore.Desktop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.Desktop.Pages;

public sealed partial class DashboardPage : Page
{
    private DispatcherTimer? _timer;
    private static PerformanceCounter? s_cpuCounter;
    private static bool s_inited;
    private static string s_cpuName = "";
    private static string s_gpuName = "";
    private static string s_gpuVram = "";

    public DashboardPage()
    {
        InitializeComponent();
        ApplyLocalization();
        Services.S.LanguageChanged += () => DispatcherQueue?.TryEnqueue(ApplyLocalization);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        WelcomeText.Text = $"Welcome back, {LoginWindow.UserEmail ?? "User"}";

        // Show cached GPU/CPU instantly on re-navigation
        if (s_gpuName != "") GpuText.Text = s_gpuName;
        if (s_gpuVram != "") GpuDetail.Text = s_gpuVram;
        if (s_cpuName != "") CpuNameText.Text = s_cpuName;

        // One-time init: CPU counter + WMI queries (background, then await back to UI)
        if (!s_inited)
        {
            s_inited = true;
            _ = OneTimeInitAsync();
        }

        // Fast live stats timer
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += async (s, a) => await RefreshLiveStats();
        _timer.Start();
        _ = RefreshLiveStats();
        _ = LoadAiTipAsync();
        RefreshActivityLog();
    }

    // ── ONE-TIME INIT (await returns to UI thread - can set controls directly) ──

    private async Task OneTimeInitAsync()
    {
        // 1. PerformanceCounter (background)
        await Task.Run(() =>
        {
            try { s_cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"); s_cpuCounter.NextValue(); }
            catch { s_cpuCounter = null; }
        });

        // 2. CPU name via WMI (background, then UI)
        s_cpuName = await Task.Run(() =>
        {
            try
            {
                using var s = new ManagementObjectSearcher("select Name from Win32_Processor");
                foreach (ManagementObject o in s.Get())
                {
                    var n = o["Name"]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(n)) return $"{n} ({Environment.ProcessorCount} cores)";
                }
            }
            catch { }
            return $"CPU ({Environment.ProcessorCount} cores)";
        });
        CpuNameText.Text = s_cpuName; // await returned to UI thread - safe!

        // 3. GPU via WMI (background, then UI)
        (s_gpuName, s_gpuVram) = await Task.Run(() =>
        {
            try
            {
                string bestName = ""; int bestPri = -1; long bestVram = 0;
                using var gs = new ManagementObjectSearcher("select Name, AdapterRAM, AdapterCompatibility from Win32_VideoController");
                foreach (ManagementObject o in gs.Get())
                {
                    try
                    {
                        var name = o["Name"]?.ToString()?.Trim() ?? "";
                        var vendor = o["AdapterCompatibility"]?.ToString()?.Trim() ?? "";
                        if (string.IsNullOrEmpty(name)) continue;
                        var nu = name.ToUpperInvariant(); var vu = vendor.ToUpperInvariant();
                        int pri = 10;
                        if (nu.Contains("RTX") || nu.Contains("GTX") || nu.Contains("GEFORCE") || nu.Contains("QUADRO") || vu.Contains("NVIDIA")) pri = 100;
                        else if (nu.Contains("RADEON RX") || nu.Contains("RADEON PRO")) pri = 90;
                        else if (nu.Contains("ARC") && vu.Contains("INTEL")) pri = 80;
                        long vb = 0; try { vb = Convert.ToInt64(o["AdapterRAM"] ?? 0); } catch { }
                        if (pri > bestPri) { bestPri = pri; bestName = name; bestVram = vb; }
                    }
                    catch { }
                }
                if (!string.IsNullOrEmpty(bestName))
                {
                    var sn = bestName.Replace("NVIDIA ", "").Replace("AMD ", "").Replace("GeForce ", "").Replace(" Graphics", "");
                    long mb = bestVram / (1024 * 1024);
                    string vd;
                    if (mb <= 0 || (mb >= 4090 && bestPri >= 70)) vd = "Dedicated GPU";
                    else if (mb >= 1024) vd = $"{mb / 1024.0:F0} GB VRAM";
                    else if (mb > 0) vd = $"{mb} MB VRAM";
                    else vd = "";
                    return (sn, vd);
                }
            }
            catch { }
            return ("N/A", "");
        });
        GpuText.Text = s_gpuName; // await returned to UI thread - safe!
        GpuDetail.Text = s_gpuVram;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        _timer?.Stop();
        _timer = null;
        base.OnNavigatedFrom(e);
    }

    // ── LIVE STATS (fast only - no WMI, runs every 3s) ──────

    private async Task RefreshLiveStats()
    {
        try
        {
            var data = await Task.Run(() =>
            {
                var d = new LiveData();
                try { d.CpuPct = s_cpuCounter?.NextValue() ?? 0f; } catch { }

                var m = new NativeMemory.MEMORYSTATUSEX();
                if (NativeMemory.GlobalMemoryStatusEx(ref m))
                {
                    d.RamLoad = (int)m.dwMemoryLoad;
                    var tg = m.ullTotalPhys / (1024.0 * 1024 * 1024);
                    var ag = m.ullAvailPhys / (1024.0 * 1024 * 1024);
                    d.RamUsed = $"{tg - ag:F1} / {tg:F1} GB used";
                    d.RamTotal = $"{tg:F1} GB total";
                }

                try
                {
                    var c = new DriveInfo("C");
                    if (c.IsReady)
                    {
                        d.DiskTotalGb = c.TotalSize / (1024.0 * 1024 * 1024);
                        d.DiskFreeGb = c.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                        d.DiskUsedPct = (int)((1.0 - d.DiskFreeGb / d.DiskTotalGb) * 100);
                    }
                }
                catch { }

                var up = TimeSpan.FromMilliseconds(Environment.TickCount64);
                d.Uptime = $"Uptime: {(int)up.TotalDays}d {up.Hours}h {up.Minutes}m";
                d.OsText = $"{RuntimeInformation.OSDescription} ({(Environment.OSVersion.Version.Build >= 22000 ? "Windows 11" : "Windows 10")})";
                return d;
            });

            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    CpuText.Text = $"{data.CpuPct:F0}%";
                    CpuBar.Value = data.CpuPct;
                    CpuDetail.Text = $"{Environment.ProcessorCount} logical cores";
                    RamText.Text = $"{data.RamLoad}%";
                    RamBar.Value = data.RamLoad;
                    RamDetail.Text = data.RamUsed;
                    DiskText.Text = $"{data.DiskUsedPct}%";
                    DiskBar.Value = data.DiskUsedPct;
                    DiskDetail.Text = $"{data.DiskFreeGb:F0} GB free of {data.DiskTotalGb:F0} GB";
                    UptimeText.Text = data.Uptime;
                    OsText.Text = data.OsText;
                    RamTotalText.Text = data.RamTotal;

                    if (data.DiskFreeGb > 0 && data.DiskTotalGb > 0 && data.DiskUsedPct > 75)
                    {
                        var months = (int)(data.DiskFreeGb / 2.0);
                        DiskPredictionText.Text = months <= 1
                            ? $"Your C: drive has only {data.DiskFreeGb:F0} GB free - it may fill up within a month!"
                            : $"At current usage ({data.DiskFreeGb:F0} GB free) may fill up in ~{months} months";
                        DiskPredictionCard.Visibility = Visibility.Visible;
                    }
                    else DiskPredictionCard.Visibility = Visibility.Collapsed;
                }
                catch { }
            });
        }
        catch { }
    }

    private class LiveData
    {
        public float CpuPct; public int RamLoad;
        public string RamUsed = "", RamTotal = "", OsText = "";
        public double DiskFreeGb, DiskTotalGb; public int DiskUsedPct;
        public string Uptime = "";
    }

    // ── AI TIP ───────────────────────────────────────────────

    private string? _aiTipModuleId;
    private async Task LoadAiTipAsync()
    {
        try
        {
            var engine = new AuraCore.Desktop.Services.AI.RecommendationEngine(App.Current.Services);
            var recs = await engine.AnalyzeAsync();
            var top = recs.FirstOrDefault(r => r.Priority >= Services.AI.RecommendationPriority.Medium && !string.IsNullOrEmpty(r.ModuleId));
            if (top is null) return;
            DispatcherQueue.TryEnqueue(() =>
            {
                AiTipTitle.Text = top.Title; AiTipDesc.Text = top.Description; _aiTipModuleId = top.ModuleId;
                var color = top.Priority switch
                {
                    Services.AI.RecommendationPriority.Critical => Windows.UI.Color.FromArgb(255, 198, 40, 40),
                    Services.AI.RecommendationPriority.High => Windows.UI.Color.FromArgb(255, 230, 81, 0),
                    _ => Windows.UI.Color.FromArgb(255, 21, 101, 192)
                };
                AiTipIcon.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(color);
                if (!string.IsNullOrEmpty(top.ActionLabel)) { AiTipAction.Content = top.ActionLabel; AiTipAction.Visibility = Visibility.Visible; }
                AiTipCard.Visibility = Visibility.Visible;
            });
        }
        catch { }
    }

    private void AiTipAction_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_aiTipModuleId) || this.Frame is null) return;
        var page = _aiTipModuleId switch
        {
            "junk-cleaner" => typeof(JunkCleanerPage), "ram-optimizer" => typeof(RamOptimizerPage),
            "storage-compression" => typeof(StoragePage), "registry-optimizer" => typeof(RegistryPage),
            "bloatware-removal" => typeof(BloatwarePage), "network-optimizer" => typeof(NetworkPage),
            "explorer-tweaks" => typeof(ExplorerPage), "scheduler" => typeof(SchedulerPage), _ => (Type?)null
        };
        if (page is not null) this.Frame.Navigate(page);
    }

    // ── QUICK ACTIONS ────────────────────────────────────────

    private async void QuickCleanup_Click(object sender, RoutedEventArgs e)
    {
        QuickCleanupBtn.IsEnabled = false;
        long totalFreed = 0;
        int totalDeleted = 0;
        var isAdmin = new System.Security.Principal.WindowsPrincipal(
            System.Security.Principal.WindowsIdentity.GetCurrent())
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);

        if (!isAdmin)
            QuickActionStatus.Text = S._("dash.cleanupNoAdmin");

        try
        {
            // Phase 1: Junk Cleaner
            QuickActionStatus.Text = S._("dash.cleanupJunk");
            var junkMod = App.Current.Services.GetServices<IOptimizationModule>()
                .FirstOrDefault(m => m.Id == "junk-cleaner");
            if (junkMod != null)
            {
                await junkMod.ScanAsync(new ScanOptions());
                var r1 = await junkMod.OptimizeAsync(new OptimizationPlan("junk-cleaner", new List<string>()), null);
                totalFreed += r1.BytesFreed;
                totalDeleted += r1.ItemsProcessed;
            }

            // Phase 2: Disk Cleanup Pro
            QuickActionStatus.Text = S._("dash.cleanupDisk");
            var diskMod = App.Current.Services.GetServices<IOptimizationModule>()
                .FirstOrDefault(m => m.Id == "disk-cleanup");
            if (diskMod != null)
            {
                await diskMod.ScanAsync(new ScanOptions());
                var r2 = await diskMod.OptimizeAsync(new OptimizationPlan("disk-cleanup", new List<string>()), null);
                totalFreed += r2.BytesFreed;
                totalDeleted += r2.ItemsProcessed;
            }

            var msg = string.Format(S._("dash.cleanupDone"), FormatBytes(totalFreed), totalDeleted);
            if (!isAdmin)
                msg += " " + S._("dash.cleanupAdminNote");
            QuickActionStatus.Text = msg;
            NotificationService.Instance.Post("Deep Cleanup", msg, NotificationType.Success, "disk-cleanup");
            ActivityLog.Add("\U0001f9f9", msg); RefreshActivityLog();
        }
        catch (Exception ex)
        {
            QuickActionStatus.Text = string.Format(S._("dash.cleanupError"), ex.Message);
        }
        finally
        {
            QuickCleanupBtn.IsEnabled = true;
        }
    }

    private async void QuickRam_Click(object sender, RoutedEventArgs e)
    {
        QuickActionStatus.Text = "Optimizing RAM...";
        var mod = App.Current.Services.GetServices<IOptimizationModule>().FirstOrDefault(m => m.Id == "ram-optimizer");
        if (mod is null) return;
        var r = await mod.OptimizeAsync(new OptimizationPlan("ram-optimizer", new List<string>()), null);
        var msg = $"RAM optimized: freed {FormatBytes(r.BytesFreed)} from {r.ItemsProcessed} processes";
        QuickActionStatus.Text = msg;
        NotificationService.Instance.Post("RAM Optimizer", msg, NotificationType.Success, "ram-optimizer");
        ActivityLog.Add("\U0001f4be", msg); RefreshActivityLog();
    }

    private async void QuickHealth_Click(object sender, RoutedEventArgs e)
    {
        QuickActionStatus.Text = "Running health scan...";
        var mod = App.Current.Services.GetServices<IOptimizationModule>().FirstOrDefault(m => m.Id == "system-health");
        if (mod is null) { QuickActionStatus.Text = S._("dash.healthUnavailable"); return; }
        try
        {
            var r = await mod.ScanAsync(new ScanOptions());
            var msg = $"Health scan complete: {r.ItemsFound} categories analyzed";
            QuickActionStatus.Text = msg;
            NotificationService.Instance.Post("System Health", msg, NotificationType.Success, "system-health");
            ActivityLog.Add("\U0001f3e5", msg); RefreshActivityLog();
            if (App.MainWindow is MainWindow mw)
            {
                var navView = (mw.Content as Grid)?.Children.OfType<NavigationView>().FirstOrDefault();
                (navView?.Content as Frame)?.Navigate(typeof(SystemHealthPage));
            }
        }
        catch (Exception ex) { QuickActionStatus.Text = string.Format(S._("dash.healthFailed"), ex.Message); }
    }

    // ── ACTIVITY LOG ─────────────────────────────────────────

    private void RefreshActivityLog()
    {
        ActivityLogPanel.Children.Clear();
        var entries = ActivityLog.Recent(6);
        if (entries.Count == 0) { ActivityLogPanel.Children.Add(new TextBlock { Text = "No recent activity", FontSize = 12, Opacity = 0.4 }); return; }
        foreach (var entry in entries)
        {
            var ago = (DateTimeOffset.Now - entry.Timestamp).TotalMinutes;
            var timeStr = ago < 1 ? "just now" : ago < 60 ? $"{(int)ago}m ago" : $"{(int)(ago / 60)}h ago";
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Padding = new Thickness(0, 2, 0, 2) };
            row.Children.Add(new TextBlock { Text = entry.Icon, FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(new TextBlock { Text = entry.Message, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.7 });
            row.Children.Add(new TextBlock { Text = timeStr, FontSize = 10, Opacity = 0.3, VerticalAlignment = VerticalAlignment.Center });
            ActivityLogPanel.Children.Add(row);
        }
    }

    private static string FormatBytes(long b) => b switch
    {
        < 1024 => $"{b} B", < 1024 * 1024 => $"{b / 1024.0:F0} KB",
        < 1024L * 1024 * 1024 => $"{b / (1024.0 * 1024):F1} MB", _ => $"{b / (1024.0 * 1024 * 1024):F2} GB"
    };

    private void ApplyLocalization()
    {
        if (FindName("PageTitle") is TextBlock title) title.Text = S._("dash.title");
        if (FindName("PageSubtitle") is TextBlock subtitle) subtitle.Text = S._("dash.subtitle");
        if (FindName("QuickCleanupText") is TextBlock ct) ct.Text = S._("dash.deepCleanup");
    }
}
