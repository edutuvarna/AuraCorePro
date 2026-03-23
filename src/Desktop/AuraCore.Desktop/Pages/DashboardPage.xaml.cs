using AuraCore.Desktop.Services;
using System.Diagnostics;
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
    private PerformanceCounter? _cpuCounter;

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

        // Static system info (loaded once)
        LoadStaticInfo();

        // Try to create CPU counter
        try { _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"); _cpuCounter.NextValue(); }
        catch { _cpuCounter = null; }

        // Start live refresh
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += async (s, e) => await RefreshLiveStats();
        _timer.Start();

        // Initial load
        _ = RefreshLiveStats();
        _ = LoadAiTipAsync();
        RefreshActivityLog();
    }

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
                AiTipTitle.Text = top.Title;
                AiTipDesc.Text = top.Description;
                _aiTipModuleId = top.ModuleId;

                var color = top.Priority switch
                {
                    Services.AI.RecommendationPriority.Critical => Windows.UI.Color.FromArgb(255, 198, 40, 40),
                    Services.AI.RecommendationPriority.High => Windows.UI.Color.FromArgb(255, 230, 81, 0),
                    _ => Windows.UI.Color.FromArgb(255, 21, 101, 192)
                };
                AiTipIcon.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(color);

                if (!string.IsNullOrEmpty(top.ActionLabel))
                {
                    AiTipAction.Content = top.ActionLabel;
                    AiTipAction.Visibility = Visibility.Visible;
                    if (top.Priority == Services.AI.RecommendationPriority.Critical)
                        AiTipAction.Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["AccentButtonStyle"];
                }

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
            "junk-cleaner" => typeof(JunkCleanerPage),
            "ram-optimizer" => typeof(RamOptimizerPage),
            "storage-compression" => typeof(StoragePage),
            "registry-optimizer" => typeof(RegistryPage),
            "bloatware-removal" => typeof(BloatwarePage),
            "network-optimizer" => typeof(NetworkPage),
            "explorer-tweaks" => typeof(ExplorerPage),
            "scheduler" => typeof(SchedulerPage),
            _ => (Type?)null
        };
        if (page is not null) this.Frame.Navigate(page);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        _timer?.Stop();
        _timer = null;
        _cpuCounter?.Dispose();
        _cpuCounter = null;
        base.OnNavigatedFrom(e);
    }

    private void LoadStaticInfo()
    {
        OsText.Text = $"OS: {RuntimeInformation.OSDescription} ({(Environment.OSVersion.Version.Build >= 22000 ? "Windows 11" : "Windows 10")})";
        CpuNameText.Text = $"CPU: {Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unknown"} ({Environment.ProcessorCount} cores)";

        var mem = new NativeMemory.MEMORYSTATUSEX();
        if (NativeMemory.GlobalMemoryStatusEx(ref mem))
            RamTotalText.Text = $"RAM: {mem.ullTotalPhys / (1024.0 * 1024 * 1024):F1} GB total";
    }

    private async Task RefreshLiveStats()
    {
        await Task.Run(() => { }); // yield

        // CPU
        try
        {
            var cpuPct = _cpuCounter?.NextValue() ?? 0;
            CpuText.Text = $"{cpuPct:F0}%";
            CpuBar.Value = cpuPct;
            CpuDetail.Text = $"{Environment.ProcessorCount} logical cores";
        }
        catch { CpuText.Text = $"{Environment.ProcessorCount} cores"; }

        // RAM
        var memInfo = new NativeMemory.MEMORYSTATUSEX();
        if (NativeMemory.GlobalMemoryStatusEx(ref memInfo))
        {
            var totalGb = memInfo.ullTotalPhys / (1024.0 * 1024 * 1024);
            var availGb = memInfo.ullAvailPhys / (1024.0 * 1024 * 1024);
            var usedGb = totalGb - availGb;
            RamText.Text = $"{memInfo.dwMemoryLoad}%";
            RamBar.Value = memInfo.dwMemoryLoad;
            RamDetail.Text = $"{usedGb:F1} / {totalGb:F1} GB used";
        }

        // Disk
        double diskFreeGb = 0, diskTotalGb = 0;
        try
        {
            var c = new DriveInfo("C");
            if (c.IsReady)
            {
                diskTotalGb = c.TotalSize / (1024.0 * 1024 * 1024);
                diskFreeGb = c.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                var usedPct = (int)((1.0 - diskFreeGb / diskTotalGb) * 100);
                DiskText.Text = $"{usedPct}%";
                DiskBar.Value = usedPct;
                DiskDetail.Text = $"{diskFreeGb:F0} GB free of {diskTotalGb:F0} GB";
            }
        }
        catch { }

        // Disk full prediction
        try
        {
            if (diskFreeGb > 0 && diskTotalGb > 0)
            {
                var usedPct = (1.0 - diskFreeGb / diskTotalGb) * 100;
                if (usedPct > 75)
                {
                    // Assume ~2GB/month usage growth (conservative estimate)
                    var monthsLeft = (int)(diskFreeGb / 2.0);
                    DiskPredictionText.Text = monthsLeft <= 1
                        ? $"⚠️ Your C: drive has only {diskFreeGb:F0} GB free — it may fill up within a month!"
                        : $"At current usage, your C: drive ({diskFreeGb:F0} GB free) may fill up in ~{monthsLeft} months";
                    DiskPredictionCard.Visibility = Visibility.Visible;
                }
                else
                {
                    DiskPredictionCard.Visibility = Visibility.Collapsed;
                }
            }
        }
        catch { }

        // Health score
        var ramLoad = (int)(memInfo.dwMemoryLoad);
        var diskLoad = (int)DiskBar.Value;
        var cpuLoad = (int)CpuBar.Value;
        var score = 100;
        if (ramLoad > 90) score -= 25; else if (ramLoad > 75) score -= 10;
        if (diskLoad > 95) score -= 25; else if (diskLoad > 85) score -= 10;
        if (cpuLoad > 90) score -= 15; else if (cpuLoad > 70) score -= 5;
        score = Math.Max(0, score);

        HealthText.Text = $"{score}/100";
        HealthBar.Value = score;
        HealthDetail.Text = score >= 80 ? "Excellent" : score >= 60 ? "Good" : score >= 40 ? "Fair" : "Poor";

        // Uptime
        var up = TimeSpan.FromMilliseconds(Environment.TickCount64);
        UptimeText.Text = $"Uptime: {(int)up.TotalDays}d {up.Hours}h {up.Minutes}m";
    }

    private async void QuickJunk_Click(object sender, RoutedEventArgs e)
    {
        QuickActionStatus.Text = "Scanning junk files...";
        var mod = App.Current.Services.GetServices<IOptimizationModule>().FirstOrDefault(m => m.Id == "junk-cleaner");
        if (mod is null) return;
        var r = await mod.ScanAsync(new ScanOptions());
        var msg = $"Junk scan: {r.ItemsFound} items found ({FormatBytes(r.EstimatedBytesFreed)})";
        QuickActionStatus.Text = msg;
        Services.NotificationService.Instance.Post("Junk Cleaner", msg, Services.NotificationType.Success, "junk-cleaner");
        ActivityLog.Add("🧹", msg);
        RefreshActivityLog();
    }

    private async void QuickRam_Click(object sender, RoutedEventArgs e)
    {
        QuickActionStatus.Text = "Optimizing RAM...";
        var mod = App.Current.Services.GetServices<IOptimizationModule>().FirstOrDefault(m => m.Id == "ram-optimizer");
        if (mod is null) return;
        var r = await mod.OptimizeAsync(new OptimizationPlan("ram-optimizer", new List<string>()), null);
        var msg = $"RAM optimized: freed {FormatBytes(r.BytesFreed)} from {r.ItemsProcessed} processes";
        QuickActionStatus.Text = msg;
        Services.NotificationService.Instance.Post("RAM Optimizer", msg, Services.NotificationType.Success, "ram-optimizer");
        ActivityLog.Add("💾", msg);
        RefreshActivityLog();
    }

    private async void QuickHealth_Click(object sender, RoutedEventArgs e)
    {
        QuickActionStatus.Text = "Running health scan...";
        var mod = App.Current.Services.GetServices<IOptimizationModule>().FirstOrDefault(m => m.Id == "system-health");
        if (mod is null) { QuickActionStatus.Text = "Health module not available."; return; }

        try
        {
            var r = await mod.ScanAsync(new ScanOptions());
            var msg = $"Health scan complete: {r.ItemsFound} categories analyzed";
            QuickActionStatus.Text = msg;
            Services.NotificationService.Instance.Post("System Health", msg, Services.NotificationType.Success, "system-health");
            ActivityLog.Add("🏥", msg);
            RefreshActivityLog();

            if (App.MainWindow is MainWindow mw)
            {
                var navView = (mw.Content as Grid)?.Children.OfType<NavigationView>().FirstOrDefault();
                var frame = navView?.Content as Frame;
                frame?.Navigate(typeof(SystemHealthPage));
            }
        }
        catch (Exception ex)
        {
            QuickActionStatus.Text = $"Health scan failed: {ex.Message}";
        }
    }

    private void RefreshActivityLog()
    {
        ActivityLogPanel.Children.Clear();
        var entries = Services.ActivityLog.Recent(6);
        if (entries.Count == 0)
        {
            ActivityLogPanel.Children.Add(new TextBlock { Text = "No recent activity", FontSize = 12, Opacity = 0.4 });
            return;
        }
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
        < 1024 => $"{b} B",
        < 1024 * 1024 => $"{b / 1024.0:F0} KB",
        < 1024L * 1024 * 1024 => $"{b / (1024.0 * 1024):F1} MB",
        _ => $"{b / (1024.0 * 1024 * 1024):F2} GB"
    };

    private void ApplyLocalization()
    {
        if (FindName("PageTitle") is Microsoft.UI.Xaml.Controls.TextBlock title)
            title.Text = S._("dash.title");
        if (FindName("PageSubtitle") is Microsoft.UI.Xaml.Controls.TextBlock subtitle)
            subtitle.Text = S._("dash.subtitle");
    }
}
