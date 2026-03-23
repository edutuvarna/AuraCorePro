using AuraCore.Desktop.Services;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using AuraCore.Desktop;
using AuraCore.Desktop.Helpers;
using Windows.Storage.Pickers;
using Windows.Storage;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AuraCore.Desktop.Pages;

public sealed partial class SystemHealthPage : Page
{
    private HealthReportPdf.HealthData? _lastScanData;

    public SystemHealthPage()
    {
        InitializeComponent();
        ApplyLocalization();
        Services.S.LanguageChanged += () => DispatcherQueue?.TryEnqueue(ApplyLocalization);
    }

    private async void ScanBtn_Click(object sender, RoutedEventArgs e)
    {
        ScanBtn.IsEnabled = false;
        Progress.IsActive = true;
        Progress.Visibility = Visibility.Visible;
        StatusText.Text = "Scanning...";

        try
        {
            await Task.Run(() => { });

            // OS
            OsName.Text = $"OS: {RuntimeInformation.OSDescription}";
            OsVersion.Text = $"Version: {Environment.OSVersion.Version}";
            OsArch.Text = $"Architecture: {RuntimeInformation.OSArchitecture}";
            OsMachine.Text = $"Machine: {Environment.MachineName}";
            var up = TimeSpan.FromMilliseconds(Environment.TickCount64);
            OsUptime.Text = $"Uptime: {(int)up.TotalDays} days, {up.Hours} hours, {up.Minutes} minutes";
            OsSection.Visibility = Visibility.Visible;

            StatusText.Text = "Scanning CPU...";
            await Task.Delay(300);

            // CPU
            CpuName.Text = $"Processor: {Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unknown"}";
            CpuCores.Text = $"Logical cores: {Environment.ProcessorCount}";
            var cpuLoad = await EstimateCpuUsageAsync();
            CpuBar.Value = cpuLoad;
            CpuUsage.Text = $"Current load: ~{cpuLoad}%";
            CpuSection.Visibility = Visibility.Visible;

            StatusText.Text = "Scanning memory...";
            await Task.Delay(300);

            // Memory
            var mem = new NativeMemory.MEMORYSTATUSEX();
            if (NativeMemory.GlobalMemoryStatusEx(ref mem))
            {
                var totalGb = mem.ullTotalPhys / (1024.0 * 1024 * 1024);
                var availGb = mem.ullAvailPhys / (1024.0 * 1024 * 1024);
                var usedGb = totalGb - availGb;
                MemTotal.Text = $"Total: {totalGb:F1} GB";
                MemAvail.Text = $"Available: {availGb:F1} GB  |  Used: {usedGb:F1} GB";
                MemBar.Value = mem.dwMemoryLoad;
                MemUsage.Text = $"Memory usage: {mem.dwMemoryLoad}%";
            }
            MemSection.Visibility = Visibility.Visible;

            StatusText.Text = "Scanning drives...";
            await Task.Delay(300);

            // Drives
            DriveList.Children.Clear();
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                var totalGb = drive.TotalSize / (1024.0 * 1024 * 1024);
                var freeGb = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                var usedPct = (int)((1.0 - freeGb / totalGb) * 100);

                var panel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 8) };
                panel.Children.Add(new TextBlock
                {
                    Text = $"{drive.Name}  {drive.VolumeLabel}  ({drive.DriveFormat})",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                });
                panel.Children.Add(new TextBlock
                {
                    Text = $"{freeGb:F1} GB free of {totalGb:F1} GB  ({usedPct}% used)",
                    Opacity = 0.7
                });
                panel.Children.Add(new ProgressBar
                {
                    Minimum = 0, Maximum = 100, Value = usedPct,
                    Height = 6, CornerRadius = new CornerRadius(3)
                });
                DriveList.Children.Add(panel);
            }
            DiskSection.Visibility = Visibility.Visible;

            StatusText.Text = "Scanning processes...";
            await Task.Delay(300);

            // Processes
            var procs = Process.GetProcesses();
            ProcCount.Text = $"Running processes: {procs.Length}";
            try
            {
                var top = procs
                    .Select(p => { try { return (p.ProcessName, Ram: p.WorkingSet64); } catch { return (p.ProcessName, Ram: 0L); } })
                    .OrderByDescending(p => p.Ram)
                    .Take(5)
                    .Select(p => $"{p.ProcessName} ({p.Ram / (1024 * 1024)} MB)");
                ProcTopRam.Text = $"Top memory: {string.Join(",  ", top)}";
            }
            catch { ProcTopRam.Text = "Could not read process details"; }
            ProcSection.Visibility = Visibility.Visible;

            StatusText.Text = "Scanning GPU...";
            await Task.Delay(200);

            // GPU
            GpuList.Children.Clear();
            try
            {
                using var gpuSearcher = new ManagementObjectSearcher("select Name, DriverVersion, AdapterRAM, VideoModeDescription from Win32_VideoController");
                foreach (ManagementObject obj in gpuSearcher.Get())
                {
                    var name = obj["Name"]?.ToString()?.Trim() ?? "Unknown";
                    var driver = obj["DriverVersion"]?.ToString() ?? "";
                    var adapterRam = obj["AdapterRAM"];
                    var ramMb = adapterRam is uint ram ? ram / (1024 * 1024) : 0;
                    var ramDisplay = ramMb > 1024 ? $"{ramMb / 1024.0:F1} GB" : $"{ramMb} MB";
                    var resolution = obj["VideoModeDescription"]?.ToString() ?? "";

                    var panel = new StackPanel { Spacing = 3 };
                    panel.Children.Add(new TextBlock { Text = name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 14 });
                    panel.Children.Add(new TextBlock { Text = $"VRAM: {ramDisplay}  |  Driver: {driver}", FontSize = 12, Opacity = 0.6 });
                    if (!string.IsNullOrEmpty(resolution))
                        panel.Children.Add(new TextBlock { Text = $"Resolution: {resolution}", FontSize = 12, Opacity = 0.5 });
                    GpuList.Children.Add(panel);
                }
                if (GpuList.Children.Count > 0) GpuSection.Visibility = Visibility.Visible;
            }
            catch { }

            StatusText.Text = "Checking battery...";
            await Task.Delay(200);

            // Battery
            try
            {
                using var batSearcher = new ManagementObjectSearcher("select * from Win32_Battery");
                foreach (ManagementObject obj in batSearcher.Get())
                {
                    var charge = obj["EstimatedChargeRemaining"] is ushort c ? c : 0;
                    var status = obj["BatteryStatus"] switch
                    {
                        (ushort)1 => "Discharging",
                        (ushort)2 => "Plugged In",
                        (ushort)3 => "Fully Charged",
                        (ushort)4 => "Low",
                        (ushort)5 => "Critical",
                        _ => "Unknown"
                    };
                    var runtime = obj["EstimatedRunTime"] is uint rt && rt < 71582788
                        ? $"{rt / 60}h {rt % 60}m remaining" : "";

                    BatteryCharge.Text = $"Charge: {charge}%  |  Status: {status}";
                    BatteryBar.Value = charge;
                    BatteryStatus.Text = !string.IsNullOrEmpty(runtime) ? runtime : status;
                    BatterySection.Visibility = Visibility.Visible;
                    break;
                }
            }
            catch { }

            StatusText.Text = "Scanning startup programs...";
            await Task.Delay(200);

            // Startup Programs
            StartupList.Children.Clear();
            var startupPaths = new (string path, string hive)[]
            {
                (@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "HKLM"),
                (@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "HKCU"),
            };
            int startupCount = 0;
            foreach (var (regPath, hive) in startupPaths)
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
                        if (string.IsNullOrWhiteSpace(name)) continue;

                        var impact = cmd.Contains("update", StringComparison.OrdinalIgnoreCase) ? "Low"
                            : cmd.Contains("helper", StringComparison.OrdinalIgnoreCase) ? "Low" : "Medium";
                        var impactColor = impact == "Low"
                            ? Windows.UI.Color.FromArgb(255, 46, 125, 50)
                            : Windows.UI.Color.FromArgb(255, 230, 81, 0);

                        var row = new Grid { ColumnSpacing = 8, Padding = new Thickness(4, 4, 4, 4) };
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                        var info = new StackPanel { Spacing = 1 };
                        info.Children.Add(new TextBlock { Text = name, FontSize = 13 });
                        info.Children.Add(new TextBlock
                        {
                            Text = cmd.Length > 80 ? cmd[..80] + "..." : cmd,
                            FontSize = 10, Opacity = 0.4, FontFamily = new FontFamily("Consolas")
                        });
                        Grid.SetColumn(info, 0);
                        row.Children.Add(info);

                        var hiveText = new TextBlock { Text = hive, FontSize = 10, Opacity = 0.4, VerticalAlignment = VerticalAlignment.Center };
                        Grid.SetColumn(hiveText, 1);
                        row.Children.Add(hiveText);

                        var badge = new Border
                        {
                            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(30, impactColor.R, impactColor.G, impactColor.B)),
                            CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2), VerticalAlignment = VerticalAlignment.Center
                        };
                        badge.Child = new TextBlock
                        {
                            Text = impact.ToUpper(), FontSize = 9,
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(impactColor)
                        };
                        Grid.SetColumn(badge, 2);
                        row.Children.Add(badge);

                        StartupList.Children.Add(row);
                        startupCount++;
                    }
                }
                catch { }
            }
            if (startupCount > 0)
            {
                StartupHeader.Text = $"Startup Programs ({startupCount})";
                StartupSection.Visibility = Visibility.Visible;
            }

            var totalSections = 5 + (GpuSection.Visibility == Visibility.Visible ? 1 : 0)
                + (BatterySection.Visibility == Visibility.Visible ? 1 : 0)
                + (StartupSection.Visibility == Visibility.Visible ? 1 : 0);
            StatusText.Text = $"Scan complete — {totalSections} sections analyzed";

            // Build PDF data from scan results
            var drives = new List<HealthReportPdf.DriveData>();
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                var tGb = drive.TotalSize / (1024.0 * 1024 * 1024);
                var fGb = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                drives.Add(new HealthReportPdf.DriveData(
                    $"{drive.Name} {drive.VolumeLabel} ({drive.DriveFormat})",
                    tGb, fGb, (int)((1.0 - fGb / tGb) * 100)));
            }

            var gpus = new List<HealthReportPdf.GpuData>();
            try
            {
                using var gs = new ManagementObjectSearcher("select Name, DriverVersion, AdapterRAM from Win32_VideoController");
                foreach (ManagementObject o in gs.Get())
                {
                    var vram = o["AdapterRAM"] is uint r ? (r > 1024 * 1024 * 1024 ? $"{r / (1024.0 * 1024 * 1024):F1} GB" : $"{r / (1024 * 1024)} MB") : "N/A";
                    gpus.Add(new HealthReportPdf.GpuData(o["Name"]?.ToString() ?? "", vram, o["DriverVersion"]?.ToString() ?? ""));
                }
            } catch { }

            var startups = new List<HealthReportPdf.StartupData>();
            var regPaths2 = new (string p, string h)[] {
                (@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "HKLM"),
                (@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "HKCU") };
            foreach (var (rp, hv) in regPaths2)
            {
                try
                {
                    var rt = hv == "HKLM" ? Microsoft.Win32.Registry.LocalMachine : Microsoft.Win32.Registry.CurrentUser;
                    using var k = rt.OpenSubKey(rp);
                    if (k is null) continue;
                    foreach (var n in k.GetValueNames())
                    {
                        if (string.IsNullOrWhiteSpace(n)) continue;
                        var cmd = k.GetValue(n)?.ToString() ?? "";
                        var imp = cmd.Contains("update", StringComparison.OrdinalIgnoreCase) || cmd.Contains("helper", StringComparison.OrdinalIgnoreCase) ? "Low" : "Medium";
                        startups.Add(new HealthReportPdf.StartupData(n, cmd, hv, imp));
                    }
                } catch { }
            }

            _lastScanData = new HealthReportPdf.HealthData
            {
                OsName = OsName.Text.Replace("OS: ", ""),
                OsVersion = OsVersion.Text.Replace("Version: ", ""),
                OsArch = OsArch.Text.Replace("Architecture: ", ""),
                MachineName = Environment.MachineName,
                Uptime = OsUptime.Text.Replace("Uptime: ", ""),
                CpuName = CpuName.Text.Replace("Processor: ", ""),
                CpuCores = CpuCores.Text.Replace("Logical cores: ", ""),
                CpuLoad = (int)CpuBar.Value,
                MemTotal = MemTotal.Text.Replace("Total: ", ""),
                MemAvail = MemAvail.Text.Replace("Available: ", ""),
                MemUsagePct = (int)MemBar.Value,
                Drives = drives,
                ProcessCount = procs.Length,
                TopProcesses = ProcTopRam.Text.Replace("Top memory: ", ""),
                Gpus = gpus,
                BatteryInfo = BatteryCharge.Text,
                BatteryPct = (int)BatteryBar.Value,
                StartupPrograms = startups,
            };
            ExportPdfBtn.IsEnabled = true;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            ScanBtn.IsEnabled = true;
            Progress.IsActive = false;
            Progress.Visibility = Visibility.Collapsed;
        }
    }

    private static async Task<int> EstimateCpuUsageAsync()
    {
        var procs = Process.GetProcesses();
        double total1 = 0;
        foreach (var p in procs) { try { total1 += p.TotalProcessorTime.TotalMilliseconds; } catch { } }
        var time1 = DateTime.UtcNow;

        await Task.Delay(500);

        var procs2 = Process.GetProcesses();
        double total2 = 0;
        foreach (var p in procs2) { try { total2 += p.TotalProcessorTime.TotalMilliseconds; } catch { } }
        var time2 = DateTime.UtcNow;

        var cpuUsed = total2 - total1;
        var elapsed = (time2 - time1).TotalMilliseconds;
        var pct = (int)(cpuUsed / (elapsed * Environment.ProcessorCount) * 100);
        return Math.Clamp(pct, 0, 100);
    }

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

            StatusText.Text = "Generating PDF report...";
            await Task.Run(() => HealthReportPdf.Generate(_lastScanData, file.Path));
            StatusText.Text = $"PDF report exported to {file.Name}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"PDF export error: {ex.Message}";
        }
    }

    private void ApplyLocalization()
    {
        if (FindName("PageTitle") is Microsoft.UI.Xaml.Controls.TextBlock title)
            title.Text = S._("health.title");
        if (FindName("PageSubtitle") is Microsoft.UI.Xaml.Controls.TextBlock subtitle)
            subtitle.Text = S._("health.subtitle");
    }
}
