using AuraCore.Desktop.Services;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using AuraCore.Desktop;
using AuraCore.Desktop.Helpers;
using Windows.Storage.Pickers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace AuraCore.Desktop.Pages;

public sealed partial class SystemHealthPage : Page
{
    // ── STATIC CACHE (survive page re-navigation) ──
    private static bool s_oneTimeLoaded;
    private static string s_cpuName = "";
    private static string s_osDesc = "";
    private static string s_osBuild = "";
    private static string s_osArch = "";
    private static string s_machineName = "";
    private static List<GpuInfo> s_gpus = new();
    private static List<StartupInfo> s_startups = new();
    private static PerformanceCounter? s_cpuCounter;

    private DispatcherTimer? _timer;
    private HealthReportPdf.HealthData? _lastScanData;
    private int _lastCpu, _lastRam, _lastWorstDisk;
    private bool _isActive;

    // Safe theme resource brush getter (full path to avoid AuraCore.Application collision)
    private static Brush ThemeBrush(string key)
    {
        try
        {
            var res = Microsoft.UI.Xaml.Application.Current.Resources[key];
            if (res is Brush b) return b;
        }
        catch { }
        return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 160, 160, 160));
    }

    public SystemHealthPage()
    {
        InitializeComponent();
        ApplyLocalization();
        S.LanguageChanged += () => DispatcherQueue?.TryEnqueue(ApplyLocalization);
    }

    // ── NAVIGATION ──

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        // Restore cached static data instantly
        if (s_oneTimeLoaded)
        {
            PopulateOsCard();
            PopulateCpuName();
            PopulateGpuCard();
            PopulateStartupCard();
        }

        _isActive = true;
        // Kick off first scan + timer
        _ = InitialLoadAsync();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _isActive = false;
        _timer?.Stop();
        _timer = null;
    }

    // ── INITIAL LOAD ──

    private async Task InitialLoadAsync()
    {
        // One-time heavy data (WMI GPU, Startup, CPU name, OS, PerfCounter)
        if (!s_oneTimeLoaded)
        {
            s_oneTimeLoaded = true;
            await LoadOneTimeDataAsync();
        }

        // First live stats refresh
        await RefreshLiveStatsAsync();

        // Start 3-second timer
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += async (_, _) => await RefreshLiveStatsAsync();
        _timer.Start();
    }

    // ── ONE-TIME DATA (GPU, Startup, CPU name, OS, PerfCounter) ──

    private async Task LoadOneTimeDataAsync()
    {
        // PerformanceCounter for CPU (background)
        await Task.Run(() =>
        {
            try
            {
                s_cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                s_cpuCounter.NextValue(); // prime it
            }
            catch { s_cpuCounter = null; }
        });

        // OS info
        s_osDesc = RuntimeInformation.OSDescription;
        s_osBuild = Environment.OSVersion.Version.ToString();
        s_osArch = RuntimeInformation.OSArchitecture.ToString();
        s_machineName = Environment.MachineName;
        PopulateOsCard();

        // CPU name via WMI (background)
        s_cpuName = await Task.Run(() =>
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("select Name from Win32_Processor");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var n = obj["Name"]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(n)) return n;
                }
            }
            catch { }
            return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unknown";
        });
        PopulateCpuName();

        // GPU via WMI (background)
        s_gpus = await Task.Run(() =>
        {
            var list = new List<GpuInfo>();
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "select Name, DriverVersion, AdapterRAM, VideoModeDescription from Win32_VideoController");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var name = obj["Name"]?.ToString()?.Trim() ?? "Unknown";
                    var driver = obj["DriverVersion"]?.ToString() ?? "";
                    var adapterRam = obj["AdapterRAM"];
                    long ramBytes = adapterRam is uint r ? r : 0;
                    // uint32 overflow fix: >4GB GPUs report 0 or wrong value
                    var ramDisplay = ramBytes > 1024 * 1024 * 1024
                        ? $"{ramBytes / (1024.0 * 1024 * 1024):F1} GB"
                        : ramBytes > 0 ? $"{ramBytes / (1024 * 1024)} MB" : "N/A";
                    var resolution = obj["VideoModeDescription"]?.ToString() ?? "";
                    list.Add(new GpuInfo(name, driver, ramDisplay, resolution));
                }
            }
            catch { }
            return list;
        });
        PopulateGpuCard();

        // Startup programs via registry (background)
        s_startups = await Task.Run(() =>
        {
            var list = new List<StartupInfo>();
            var regPaths = new[]
            {
                (@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "HKLM"),
                (@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "HKCU"),
                (@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "HKLM"),
            };
            foreach (var (regPath, hive) in regPaths)
            {
                try
                {
                    var root = hive == "HKLM"
                        ? Microsoft.Win32.Registry.LocalMachine
                        : Microsoft.Win32.Registry.CurrentUser;
                    using var key = root.OpenSubKey(regPath);
                    if (key is null) continue;
                    foreach (var name in key.GetValueNames())
                    {
                        var cmd = key.GetValue(name)?.ToString() ?? "";
                        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(cmd)) continue;
                        // Avoid duplicates
                        if (list.Any(s => s.Name == name)) continue;
                        var impact = cmd.Contains("update", StringComparison.OrdinalIgnoreCase)
                            || cmd.Contains("helper", StringComparison.OrdinalIgnoreCase) ? "Low" : "Medium";
                        list.Add(new StartupInfo(name, cmd, hive, impact));
                    }
                }
                catch { }
            }
            return list;
        });
        PopulateStartupCard();

        // Battery (one-time check)
        await LoadBatteryAsync();
    }

    // ── LIVE STATS (every 3s) ──

    private async Task RefreshLiveStatsAsync()
    {
        if (!_isActive) return;
        try
        {
            // CPU usage (background)
            var cpuPct = await Task.Run(() =>
            {
                if (s_cpuCounter != null)
                {
                    try { return (int)s_cpuCounter.NextValue(); }
                    catch { }
                }
                return EstimateCpuSync();
            });
            cpuPct = Math.Clamp(cpuPct, 0, 100);
            _lastCpu = cpuPct;

            CpuMiniValue.Text = cpuPct.ToString();
            CpuMiniBar.Value = cpuPct;
            CpuLine3.Text = $"Current: {cpuPct}% load";
            CpuDetailBar.Value = cpuPct;

            // Memory (P/Invoke, very fast)
            var mem = new MEMORYSTATUSEX();
            int ramPct = 0;
            double totalGb = 0, availGb = 0, usedGb = 0;
            if (GlobalMemoryStatusEx(ref mem))
            {
                totalGb = mem.ullTotalPhys / (1024.0 * 1024 * 1024);
                availGb = mem.ullAvailPhys / (1024.0 * 1024 * 1024);
                usedGb = totalGb - availGb;
                ramPct = (int)mem.dwMemoryLoad;
            }
            _lastRam = ramPct;

            RamMiniValue.Text = ramPct.ToString();
            RamMiniBar.Value = ramPct;
            MemLine1.Text = $"Total: {totalGb:F1} GB";
            MemLine2.Text = $"Available: {availGb:F1} GB";
            MemLine3.Text = $"Used: {usedGb:F1} GB ({ramPct}%)";
            MemDetailBar.Value = ramPct;

            // Drives
            var drives = DriveInfo.GetDrives().Where(d => d.IsReady).ToList();
            int worstDiskPct = 0;
            string worstDiskName = "C:";
            DrivesDetailList.Children.Clear();
            foreach (var drive in drives)
            {
                var tGb = drive.TotalSize / (1024.0 * 1024 * 1024);
                var fGb = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                var pct = (int)((1.0 - fGb / tGb) * 100);
                if (pct > worstDiskPct) { worstDiskPct = pct; worstDiskName = drive.Name.TrimEnd('\\'); }
                AddDriveRow(drive.Name.TrimEnd('\\'), drive.VolumeLabel, drive.DriveFormat, tGb, fGb, pct);
            }
            _lastWorstDisk = worstDiskPct;

            DiskMiniLabel.Text = $"Disk ({worstDiskName})";
            DiskMiniValue.Text = worstDiskPct.ToString();
            DiskMiniBar.Value = worstDiskPct;

            // Processes
            var procs = await Task.Run(() => Process.GetProcesses());
            ProcMiniValue.Text = procs.Length.ToString();
            string topName = "";
            long topRam = 0;
            ProcDetailList.Children.Clear();
            var topProcs = procs
                .Select(p => { try { return (p.ProcessName, Ram: p.WorkingSet64); } catch { return (p.ProcessName, Ram: 0L); } })
                .OrderByDescending(p => p.Ram)
                .Take(5)
                .ToList();
            int rank = 1;
            foreach (var (name, ram) in topProcs)
            {
                if (rank == 1) { topName = name; topRam = ram; }
                ProcDetailList.Children.Add(new TextBlock
                {
                    Text = $"{rank}. {name} ({ram / (1024 * 1024)} MB)",
                    FontSize = 12,
                    Foreground = ThemeBrush("TextFillColorPrimaryBrush")
                });
                rank++;
            }
            ProcMiniTop.Text = topRam > 0 ? $"Top: {topName} ({topRam / (1024 * 1024)} MB)" : "";

            // Uptime (refreshes each tick)
            var up = TimeSpan.FromMilliseconds(Environment.TickCount64);
            OsLine4.Text = $"Uptime: {(int)up.TotalDays}d {up.Hours}h {up.Minutes}m";

            // Score + Alerts + Colors
            int score = CalculateScore(cpuPct, ramPct, drives);
            UpdateScoreDisplay(score);
            UpdateAlerts(cpuPct, ramPct, drives);
            UpdateBarColors(cpuPct, ramPct, worstDiskPct);

            LastRefreshText.Text = $"Last refresh: {DateTime.Now:HH:mm:ss}";
            ExportPdfBtn.IsEnabled = true;

            // Build PDF data
            BuildPdfData(cpuPct, ramPct, totalGb, availGb, procs.Length, topProcs, drives);
        }
        catch { }
    }

    // ── UI POPULATION HELPERS ──

    private void PopulateOsCard()
    {
        OsLine1.Text = s_osDesc;
        OsLine2.Text = $"Build {s_osBuild}";
        OsLine3.Text = $"{s_osArch} - {s_machineName}";
    }

    private void PopulateCpuName()
    {
        CpuLine1.Text = s_cpuName;
        CpuLine2.Text = $"{Environment.ProcessorCount} logical cores";
    }

    private void PopulateGpuCard()
    {
        GpuDetailList.Children.Clear();
        foreach (var gpu in s_gpus)
        {
            var panel = new StackPanel { Spacing = 2 };
            panel.Children.Add(new TextBlock
            {
                Text = gpu.Name,
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            panel.Children.Add(new TextBlock
            {
                Text = $"VRAM: {gpu.Vram}  |  Driver: {gpu.Driver}",
                FontSize = 12,
                Foreground = ThemeBrush("TextFillColorSecondaryBrush")
            });
            if (!string.IsNullOrEmpty(gpu.Resolution))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = $"Resolution: {gpu.Resolution}",
                    FontSize = 11,
                    Foreground = ThemeBrush("TextFillColorTertiaryBrush")
                });
            }
            GpuDetailList.Children.Add(panel);
        }
        if (s_gpus.Count == 0)
        {
            GpuDetailList.Children.Add(new TextBlock
            {
                Text = "No GPU detected",
                FontSize = 12,
                Foreground = ThemeBrush("TextFillColorSecondaryBrush")
            });
        }
        GpuLoadedOnce.Visibility = Visibility.Visible;
    }

    private void PopulateStartupCard()
    {
        StartupDetailList.Children.Clear();
        var shown = s_startups.Take(5).ToList();
        foreach (var s in shown)
        {
            var impactColor = s.Impact == "Low"
                ? Windows.UI.Color.FromArgb(255, 34, 197, 94)   // green
                : Windows.UI.Color.FromArgb(255, 245, 158, 11); // amber

            var row = new Grid { ColumnSpacing = 6, Padding = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameText = new TextBlock { Text = s.Name, FontSize = 12 };
            Grid.SetColumn(nameText, 0);
            row.Children.Add(nameText);

            var badge = new Border
            {
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, impactColor.R, impactColor.G, impactColor.B)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 1, 6, 1),
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock
            {
                Text = s.Impact.ToUpper(),
                FontSize = 9,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(impactColor)
            };
            Grid.SetColumn(badge, 1);
            row.Children.Add(badge);

            StartupDetailList.Children.Add(row);
        }
        if (s_startups.Count > 5)
        {
            StartupDetailList.Children.Add(new TextBlock
            {
                Text = $"+{s_startups.Count - 5} more...",
                FontSize = 11,
                Foreground = ThemeBrush("TextFillColorTertiaryBrush")
            });
        }
        StartupCardHeader.Text = $"Startup programs ({s_startups.Count})";
        StartupLoadedOnce.Visibility = Visibility.Visible;
    }

    private void AddDriveRow(string name, string label, string format, double totalGb, double freeGb, int pct)
    {
        var color = pct > 90 ? Windows.UI.Color.FromArgb(255, 239, 68, 68)     // red
                  : pct > 70 ? Windows.UI.Color.FromArgb(255, 245, 158, 11)    // amber
                  : Windows.UI.Color.FromArgb(255, 34, 197, 94);               // green

        var panel = new StackPanel { Spacing = 3 };

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var nameText = new TextBlock { Text = $"{name} {label} ({format})", FontSize = 12 };
        Grid.SetColumn(nameText, 0);
        header.Children.Add(nameText);
        var pctText = new TextBlock
        {
            Text = $"{pct}%", FontSize = 12,
            Foreground = new SolidColorBrush(color)
        };
        Grid.SetColumn(pctText, 1);
        header.Children.Add(pctText);
        panel.Children.Add(header);

        var bar = new ProgressBar
        {
            Minimum = 0, Maximum = 100, Value = pct,
            Height = 4, CornerRadius = new CornerRadius(2),
            Foreground = new SolidColorBrush(color)
        };
        panel.Children.Add(bar);

        panel.Children.Add(new TextBlock
        {
            Text = $"{freeGb:F1} GB free of {totalGb:F1} GB",
            FontSize = 11,
            Foreground = ThemeBrush("TextFillColorTertiaryBrush")
        });

        DrivesDetailList.Children.Add(panel);
    }

    private async Task LoadBatteryAsync()
    {
        var bat = await Task.Run(() =>
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("select * from Win32_Battery");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var charge = obj["EstimatedChargeRemaining"] is ushort c ? c : 0;
                    var statusCode = obj["BatteryStatus"];
                    var status = statusCode switch
                    {
                        (ushort)1 => "Discharging",
                        (ushort)2 => "Plugged In",
                        (ushort)3 => "Fully Charged",
                        (ushort)4 => "Low",
                        (ushort)5 => "Critical",
                        _ => "Unknown"
                    };
                    var runtime = obj["EstimatedRunTime"] is uint rt && rt < 71582788
                        ? $"{rt / 60}h {rt % 60}m remaining" : status;
                    return new BatteryInfo(true, charge, status, runtime);
                }
            }
            catch { }
            return new BatteryInfo(false, 0, "", "");
        });

        if (bat.HasBattery)
        {
            BatteryLine1.Text = $"Charge: {bat.Charge}%  |  Status: {bat.Status}";
            BatteryDetailBar.Value = bat.Charge;
            BatteryLine2.Text = bat.Runtime;
            BatteryCard.Visibility = Visibility.Visible;
        }
    }

    // ── SCORE CALCULATION ──

    private static int CalculateScore(int cpu, int ram, List<DriveInfo> drives)
    {
        var score = 100;

        if (ram > 90) score -= 30;
        else if (ram > 75) score -= 15;
        else if (ram > 60) score -= 5;

        if (cpu > 90) score -= 20;
        else if (cpu > 75) score -= 10;
        else if (cpu > 50) score -= 3;

        foreach (var d in drives.Where(d => d.IsReady))
        {
            var tGb = d.TotalSize / (1024.0 * 1024 * 1024);
            var fGb = d.AvailableFreeSpace / (1024.0 * 1024 * 1024);
            var pct = (int)((1.0 - fGb / tGb) * 100);
            if (pct > 95) score -= 25;
            else if (pct > 85) score -= 10;
            else if (pct > 75) score -= 5;
        }

        return Math.Clamp(score, 0, 100);
    }

    private void UpdateScoreDisplay(int score)
    {
        ScoreValue.Text = score.ToString();
        ScoreBar.Value = score;

        string label;
        Windows.UI.Color color;

        if (score >= 80)
        {
            label = S._("health.excellent");
            color = Windows.UI.Color.FromArgb(255, 34, 197, 94); // green
        }
        else if (score >= 60)
        {
            label = S._("health.good");
            color = Windows.UI.Color.FromArgb(255, 245, 158, 11); // amber
        }
        else if (score >= 40)
        {
            label = S._("health.needsAttention");
            color = Windows.UI.Color.FromArgb(255, 249, 115, 22); // orange
        }
        else
        {
            label = S._("health.critical");
            color = Windows.UI.Color.FromArgb(255, 239, 68, 68); // red
        }

        ScoreLabel.Text = label;
        ScoreLabel.Foreground = new SolidColorBrush(color);
        ScoreValue.Foreground = new SolidColorBrush(color);
        ScoreBar.Foreground = new SolidColorBrush(color);
    }

    private void UpdateBarColors(int cpu, int ram, int disk)
    {
        CpuMiniBar.Foreground = new SolidColorBrush(GetPctColor(cpu));
        RamMiniBar.Foreground = new SolidColorBrush(GetPctColor(ram));
        DiskMiniBar.Foreground = new SolidColorBrush(GetPctColor(disk));
        CpuDetailBar.Foreground = new SolidColorBrush(GetPctColor(cpu));
        MemDetailBar.Foreground = new SolidColorBrush(GetPctColor(ram));
    }

    private static Windows.UI.Color GetPctColor(int pct)
    {
        if (pct > 85) return Windows.UI.Color.FromArgb(255, 239, 68, 68);     // red
        if (pct > 60) return Windows.UI.Color.FromArgb(255, 245, 158, 11);    // amber
        return Windows.UI.Color.FromArgb(255, 59, 130, 246);                  // blue (normal)
    }

    // ── SMART ALERTS ──

    private void UpdateAlerts(int cpu, int ram, List<DriveInfo> drives)
    {
        AlertsPanel.Children.Clear();

        if (ram > 85)
        {
            var mem = new MEMORYSTATUSEX();
            GlobalMemoryStatusEx(ref mem);
            var totalGb = mem.ullTotalPhys / (1024.0 * 1024 * 1024);
            var usedGb = totalGb - mem.ullAvailPhys / (1024.0 * 1024 * 1024);
            AddAlert(true,
                S._("health.alert.highRam"),
                string.Format(S._("health.alert.highRamDesc"), ram, $"{usedGb:F1}", $"{totalGb:F1}"));
        }
        else if (ram > 70)
        {
            var mem = new MEMORYSTATUSEX();
            GlobalMemoryStatusEx(ref mem);
            var totalGb = mem.ullTotalPhys / (1024.0 * 1024 * 1024);
            var usedGb = totalGb - mem.ullAvailPhys / (1024.0 * 1024 * 1024);
            AddAlert(false,
                S._("health.alert.highRam"),
                string.Format(S._("health.alert.highRamDesc"), ram, $"{usedGb:F1}", $"{totalGb:F1}"));
        }

        if (cpu > 85)
            AddAlert(true, S._("health.alert.highCpu"),
                string.Format(S._("health.alert.highCpuDesc"), cpu));
        else if (cpu > 70)
            AddAlert(false, S._("health.alert.highCpu"),
                string.Format(S._("health.alert.highCpuDesc"), cpu));

        foreach (var d in drives.Where(d => d.IsReady))
        {
            var tGb = d.TotalSize / (1024.0 * 1024 * 1024);
            var fGb = d.AvailableFreeSpace / (1024.0 * 1024 * 1024);
            var pct = (int)((1.0 - fGb / tGb) * 100);
            if (pct > 85)
                AddAlert(pct > 92, S._("health.alert.lowDisk"),
                    string.Format(S._("health.alert.lowDiskDesc"),
                        d.Name.TrimEnd('\\'), pct, $"{fGb:F1}", $"{tGb:F1}"));
        }
    }

    private void AddAlert(bool isDanger, string title, string desc)
    {
        var dotColor = isDanger
            ? Windows.UI.Color.FromArgb(255, 239, 68, 68)
            : Windows.UI.Color.FromArgb(255, 245, 158, 11);
        var titleColor = isDanger
            ? Windows.UI.Color.FromArgb(255, 252, 165, 165)
            : Windows.UI.Color.FromArgb(255, 252, 211, 77);
        var bgColor = isDanger
            ? Windows.UI.Color.FromArgb(20, 239, 68, 68)
            : Windows.UI.Color.FromArgb(20, 245, 158, 11);
        var borderColor = isDanger
            ? Windows.UI.Color.FromArgb(50, 239, 68, 68)
            : Windows.UI.Color.FromArgb(50, 245, 158, 11);

        var card = new Border
        {
            Background = new SolidColorBrush(bgColor),
            BorderBrush = new SolidColorBrush(borderColor),
            BorderThickness = new Thickness(0.5),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 10, 14, 10)
        };

        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var dot = new Border
        {
            Width = 8, Height = 8,
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(dotColor),
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 4, 0, 0)
        };
        Grid.SetColumn(dot, 0);
        row.Children.Add(dot);

        var content = new StackPanel { Spacing = 2 };
        content.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(titleColor)
        });
        content.Children.Add(new TextBlock
        {
            Text = desc,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ThemeBrush("TextFillColorSecondaryBrush")
        });
        Grid.SetColumn(content, 1);
        row.Children.Add(content);

        card.Child = row;
        AlertsPanel.Children.Add(card);
    }

    // ── CPU FALLBACK ──

    private static int EstimateCpuSync()
    {
        try
        {
            var procs = Process.GetProcesses();
            double t1 = 0;
            foreach (var p in procs) { try { t1 += p.TotalProcessorTime.TotalMilliseconds; } catch { } }
            var sw = Stopwatch.StartNew();
            Thread.Sleep(300);
            sw.Stop();
            var procs2 = Process.GetProcesses();
            double t2 = 0;
            foreach (var p in procs2) { try { t2 += p.TotalProcessorTime.TotalMilliseconds; } catch { } }
            var pct = (int)((t2 - t1) / (sw.Elapsed.TotalMilliseconds * Environment.ProcessorCount) * 100);
            return Math.Clamp(pct, 0, 100);
        }
        catch { return 0; }
    }

    // ── PDF EXPORT ──

    private async void ExportPdfBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_lastScanData is null) return;
        try
        {
            var savePicker = new FileSavePicker();
            savePicker.SuggestedStartLocation = PickerLocationId.Desktop;
            savePicker.FileTypeChoices.Add("PDF Document", new List<string> { ".pdf" });
            savePicker.SuggestedFileName = $"AuraCorePro_HealthReport_{DateTime.Now:yyyyMMdd_HHmmss}";

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

            var file = await savePicker.PickSaveFileAsync();
            if (file is null) return;

            LastRefreshText.Text = S._("health.generatingPdf");
            await Task.Run(() => HealthReportPdf.Generate(_lastScanData, file.Path));
            LastRefreshText.Text = string.Format(S._("health.pdfExported"), file.Name);
        }
        catch (Exception ex)
        {
            LastRefreshText.Text = $"PDF error: {ex.Message}";
        }
    }

    private void BuildPdfData(int cpuPct, int ramPct, double totalGb, double availGb,
        int procCount, List<(string ProcessName, long Ram)> topProcs, List<DriveInfo> drives)
    {
        var driveData = new List<HealthReportPdf.DriveData>();
        foreach (var d in drives.Where(d => d.IsReady))
        {
            var tGb = d.TotalSize / (1024.0 * 1024 * 1024);
            var fGb = d.AvailableFreeSpace / (1024.0 * 1024 * 1024);
            driveData.Add(new HealthReportPdf.DriveData(
                $"{d.Name} {d.VolumeLabel} ({d.DriveFormat})",
                tGb, fGb, (int)((1.0 - fGb / tGb) * 100)));
        }

        var gpuData = s_gpus.Select(g =>
            new HealthReportPdf.GpuData(g.Name, g.Vram, g.Driver)).ToList();

        var startupData = s_startups.Select(s =>
            new HealthReportPdf.StartupData(s.Name, s.Command, s.Hive, s.Impact)).ToList();

        _lastScanData = new HealthReportPdf.HealthData
        {
            OsName = s_osDesc,
            OsVersion = s_osBuild,
            OsArch = s_osArch,
            MachineName = s_machineName,
            Uptime = OsLine4.Text.Replace("Uptime: ", ""),
            CpuName = s_cpuName,
            CpuCores = Environment.ProcessorCount.ToString(),
            CpuLoad = cpuPct,
            MemTotal = $"{totalGb:F1} GB",
            MemAvail = $"{availGb:F1} GB",
            MemUsagePct = ramPct,
            Drives = driveData,
            ProcessCount = procCount,
            TopProcesses = string.Join(", ", topProcs.Select(p => $"{p.ProcessName} ({p.Ram / (1024 * 1024)} MB)")),
            Gpus = gpuData,
            BatteryInfo = BatteryLine1.Text ?? "",
            BatteryPct = (int)BatteryDetailBar.Value,
            StartupPrograms = startupData,
        };
    }

    // ── LOCALIZATION ──

    private void ApplyLocalization()
    {
        PageTitle.Text = S._("health.monitor.title");
        PageSubtitle.Text = S._("health.monitor.subtitle");
        LiveBadge.Text = S._("health.live");
        RefreshInfo.Text = S._("health.autoRefresh");
        ScoreCaption.Text = S._("health.scoreCaption");
        CpuMiniLabel.Text = S._("health.cpuUsage");
        RamMiniLabel.Text = S._("health.memoryLabel");
        ProcMiniLabel.Text = S._("health.processesLabel");
        DetailsHeader.Text = S._("health.details");
        OsCardHeader.Text = S._("health.os");
        CpuCardHeader.Text = S._("health.processor");
        MemCardHeader.Text = S._("health.memory");
        GpuCardHeader.Text = S._("health.gpu");
        DrivesCardHeader.Text = S._("health.drives");
        ProcCardHeader.Text = S._("health.topByRam");
        BatteryCardHeader.Text = S._("health.battery");
        GpuLoadedOnce.Text = S._("health.loadedOnce");
        StartupLoadedOnce.Text = S._("health.loadedOnce");
        ExportPdfBtn.Content = S._("health.exportPdf");
    }

    // ── P/INVOKE ──

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

    // ── RECORDS ──

    private sealed record GpuInfo(string Name, string Driver, string Vram, string Resolution);
    private sealed record StartupInfo(string Name, string Command, string Hive, string Impact);
    private sealed record BatteryInfo(bool HasBattery, int Charge, string Status, string Runtime);
}
