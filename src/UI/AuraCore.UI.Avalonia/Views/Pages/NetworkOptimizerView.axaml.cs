using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.NetworkOptimizer;
using AuraCore.Module.NetworkOptimizer.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

public record AdapterItem(string Name, string Desc, string Ip, string Speed, string Sent, string Recv);
public record DnsPresetItem(string Name, string Servers, string Category, string ActiveLabel, ISolidColorBrush ActiveBrush, string SwitchLabel);
public record BandwidthItem(string Rank, string ProcessName, string Details, string Connections, string Pid);

public partial class NetworkOptimizerView : UserControl
{
    private readonly NetworkOptimizerModule? _module;
    private bool _initialized;
    private double _preChangeDnsLatencyMs = -1;

    private static readonly (string Name, string Ip)[] BenchServers = new[]
    {
        ("Cloudflare", "1.1.1.1"), ("Cloudflare 2", "1.0.0.1"),
        ("Google", "8.8.8.8"), ("Google 2", "8.8.4.4"),
        ("Quad9", "9.9.9.9"), ("OpenDNS", "208.67.222.222"),
        ("Comodo", "8.26.56.26"), ("CleanBrowsing", "185.228.168.168"),
        ("AdGuard", "94.140.14.14"), ("Yandex", "77.88.8.8"),
    };

    public NetworkOptimizerView()
    {
        InitializeComponent();
        Loaded += (s, e) => ApplyLocalization();
        LocalizationService.LanguageChanged += OnLanguageChanged;
        Unloaded += (s, e) => LocalizationService.LanguageChanged -= OnLanguageChanged;
        _module = App.Services.GetServices<IOptimizationModule>().OfType<NetworkOptimizerModule>().FirstOrDefault();
        Loaded += async (s, e) =>
        {
            if (_initialized) return;
            _initialized = true;
            await RunScan();
            _ = LoadWifiSignalAsync();
            _ = LoadBandwidthAsync();
        };
    }

    private void OnLanguageChanged() =>
        global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);

    private async Task RunScan()
    {
        ScanLabel.Text = LocalizationService._("common.scanning");
        try
        {
            // Module DNS scan
            if (_module is not null)
            {
                await _module.ScanAsync(new ScanOptions());
                var r = _module.LastReport;
                if (r is not null)
                {
                    DnsPrimary.Text = !string.IsNullOrEmpty(r.CurrentDns.Primary) ? r.CurrentDns.Primary : GetSystemDns();
                    DnsSecondary.Text = r.CurrentDns.Secondary;
                    DnsProvider.Text = !string.IsNullOrEmpty(r.CurrentDns.ProviderName) ? r.CurrentDns.ProviderName : "ISP Default";
                    DnsLatency.Text = r.CurrentDns.ResponseTimeMs > 0 ? $"{r.CurrentDns.ResponseTimeMs:F0}ms" : MeasureDnsLatency();
                    DnsPresetList.ItemsSource = r.AvailableDnsPresets.Select(p => new DnsPresetItem(
                        p.Name, $"{p.Primary} / {p.Secondary}", p.Category,
                        p.IsCurrentlyActive ? "Active" : "",
                        new SolidColorBrush(Color.Parse(p.IsCurrentlyActive ? "#22C55E" : "#555570")),
                        LocalizationService._("common.switch")
                    )).ToList();
                }
            }
            else
            {
                DnsPrimary.Text = GetSystemDns();
                DnsProvider.Text = "ISP Default";
                DnsLatency.Text = MeasureDnsLatency();
            }

            // Network interface stats (from NetworkMonitor)
            var ifaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .ToList();
            IfaceCount.Text = ifaces.Count.ToString();
            long totalSent = 0, totalRecv = 0;
            var adapterItems = ifaces.Select(n =>
            {
                var stats = n.GetIPv4Statistics();
                totalSent += stats.BytesSent;
                totalRecv += stats.BytesReceived;
                var speed = n.Speed > 0 ? $"{n.Speed / 1_000_000} Mbps" : "N/A";
                var ip = n.GetIPProperties().UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString() ?? "--";
                return new AdapterItem(n.Name, n.Description, ip, speed,
                    $"\u2B06 {FormatBytes(stats.BytesSent)}", $"\u2B07 {FormatBytes(stats.BytesReceived)}");
            }).ToList();
            TotalSent.Text = FormatBytes(totalSent);
            TotalRecv.Text = FormatBytes(totalRecv);
            AdapterList.ItemsSource = adapterItems;
        }
        catch (System.Exception ex) { SubText.Text = $"Error: {ex.Message}"; }
        finally { ScanLabel.Text = LocalizationService._("common.scan"); }
    }

    private async void Bench_Click(object? sender, RoutedEventArgs e)
    {
        BenchBtn.IsEnabled = false;
        BenchLabel.Text = LocalizationService._("common.scanning");
        BenchHeader.IsVisible = true;
        RecommendText.IsVisible = true;
        RecommendText.Text = LocalizationService._("common.scanning");
        BenchResults.Children.Clear();

        var results = new List<(string Name, string Ip, double Ms, bool Ok)>();
        await Task.Run(() =>
        {
            var domains = new[] { "google.com", "github.com", "cloudflare.com" };
            foreach (var (name, ip) in BenchServers)
            {
                double totalMs = 0; int ok = 0;
                foreach (var domain in domains)
                {
                    var ms = PingDns(ip, domain);
                    if (ms >= 0) { totalMs += ms; ok++; }
                }
                results.Add((name, ip, ok > 0 ? totalMs / ok : -1, ok > 0));
            }
        });

        results.Sort((a, b) => a.Ms < 0 ? 1 : b.Ms < 0 ? -1 : a.Ms.CompareTo(b.Ms));
        var best = results.FirstOrDefault(r => r.Ok);
        RecommendText.Text = best.Ok ? $"Recommended: {best.Name} ({best.Ip}) - {best.Ms:F1}ms avg" : "Benchmark failed";

        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            var color = !r.Ok ? "#EF4444" : r.Ms < 20 ? "#22C55E" : r.Ms < 50 ? "#F59E0B" : "#EF4444";
            var msText = r.Ok ? $"{r.Ms:F1} ms" : "Timeout";
            var row = new Border
            {
                CornerRadius = new global::Avalonia.CornerRadius(6),
                Background = new SolidColorBrush(Color.Parse(i == 0 && r.Ok ? "#1522C55E" : "#12FFFFFF")),
                Padding = new global::Avalonia.Thickness(12, 8),
                Margin = new global::Avalonia.Thickness(0, 0, 0, 2),
                Child = new Grid
                {
                    ColumnDefinitions = ColumnDefinitions.Parse("Auto,*,Auto"),
                    Children =
                    {
                        new TextBlock { Text = $"#{i+1}", FontSize = 13, FontWeight = FontWeight.Bold,
                            Foreground = new SolidColorBrush(Color.Parse(i < 3 ? "#00D4AA" : "#555570")),
                            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                            Margin = new global::Avalonia.Thickness(0,0,10,0) },
                        SetCol(new StackPanel { Children = {
                            new TextBlock { Text = r.Name, FontSize = 12, FontWeight = FontWeight.SemiBold,
                                Foreground = new SolidColorBrush(Color.Parse("#E8E8F0")) },
                            new TextBlock { Text = r.Ip, FontSize = 10, Foreground = new SolidColorBrush(Color.Parse("#8888A0")) }
                        }}, 1),
                        SetCol(new TextBlock { Text = msText, FontSize = 14, FontWeight = FontWeight.Bold,
                            Foreground = new SolidColorBrush(Color.Parse(color)),
                            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center }, 2)
                    }
                }
            };
            BenchResults.Children.Add(row);
        }
        BenchBtn.IsEnabled = true;
        BenchLabel.Text = LocalizationService._("netOpt.benchDns");
    }

    private static Control SetCol(Control c, int col) { Grid.SetColumn(c, col); return c; }

    private async void Scan_Click(object? sender, RoutedEventArgs e) => await RunScan();
    private async void SwitchDns_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || _module is null) return;
        var presetName = btn.Tag?.ToString();
        if (string.IsNullOrEmpty(presetName)) return;
        SubText.Text = $"Switching DNS to {presetName}...";
        try
        {
            // Measure before latency
            _preChangeDnsLatencyMs = MeasureDnsLatencyMs();

            var plan = new OptimizationPlan(_module.Id, new[] { $"dns:{presetName}" });
            var result = await _module.OptimizeAsync(plan);
            SubText.Text = result.Success ? $"Switched to {presetName}" : "Failed - try as admin";
            await RunScan();

            // Measure after latency and show comparison
            if (result.Success && _preChangeDnsLatencyMs > 0)
            {
                await Task.Delay(500); // Allow DNS to settle
                var afterMs = MeasureDnsLatencyMs();
                ShowDnsComparison(_preChangeDnsLatencyMs, afterMs);
            }
        }
        catch (System.Exception ex) { SubText.Text = $"Error: {ex.Message}"; }
    }

    private void ShowDnsComparison(double beforeMs, double afterMs)
    {
        DnsComparisonPanel.IsVisible = true;
        DnsBeforeLatency.Text = $"{beforeMs:F0}ms";
        DnsAfterLatency.Text = $"{afterMs:F0}ms";

        if (afterMs > 0 && beforeMs > 0)
        {
            var delta = beforeMs - afterMs;
            var pct = beforeMs > 0 ? (delta / beforeMs * 100) : 0;
            if (delta > 0)
            {
                DnsImprovementText.Text = $"{pct:F0}% faster ({delta:F0}ms improvement)";
                DnsImprovementText.Foreground = new SolidColorBrush(Color.Parse("#22C55E"));
            }
            else if (delta < 0)
            {
                DnsImprovementText.Text = $"{-pct:F0}% slower ({-delta:F0}ms increase)";
                DnsImprovementText.Foreground = new SolidColorBrush(Color.Parse("#EF4444"));
            }
            else
            {
                DnsImprovementText.Text = "No change in latency";
                DnsImprovementText.Foreground = new SolidColorBrush(Color.Parse("#F59E0B"));
            }
        }
    }

    private static double MeasureDnsLatencyMs()
    {
        try
        {
            var sw = Stopwatch.StartNew();
            Dns.GetHostEntry("google.com");
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }
        catch { return -1; }
    }

    private static string GetSystemDns()
    {
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback))
            {
                var dns = nic.GetIPProperties().DnsAddresses;
                if (dns.Count > 0) return dns[0].ToString();
            }
        }
        catch { }
        return "--";
    }

    private static string MeasureDnsLatency()
    {
        try { var sw = Stopwatch.StartNew(); Dns.GetHostEntry("google.com"); sw.Stop(); return $"{sw.ElapsedMilliseconds}ms"; }
        catch { return "--"; }
    }

    private static double PingDns(string dnsIp, string domain)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            using var client = new UdpClient();
            client.Client.ReceiveTimeout = 2000;
            client.Client.SendTimeout = 2000;
            var query = BuildDnsQuery(domain);
            client.Send(query, query.Length, new IPEndPoint(IPAddress.Parse(dnsIp), 53));
            var ep = new IPEndPoint(IPAddress.Any, 0);
            client.Receive(ref ep);
            sw.Stop();
            return sw.Elapsed.TotalMilliseconds;
        }
        catch { return -1; }
    }

    private static byte[] BuildDnsQuery(string domain)
    {
        var ms = new System.IO.MemoryStream();
        ms.Write(new byte[] { 0x12, 0x34, 0x01, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
        foreach (var part in domain.Split('.'))
        {
            ms.WriteByte((byte)part.Length);
            ms.Write(System.Text.Encoding.ASCII.GetBytes(part));
        }
        ms.WriteByte(0);
        ms.Write(new byte[] { 0x00, 0x01, 0x00, 0x01 });
        return ms.ToArray();
    }

    private static string FormatBytes(long b) => b switch
    {
        < 1024 => $"{b} B", < 1024 * 1024 => $"{b / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{b / (1024.0 * 1024):F1} MB",
        _ => $"{b / (1024.0 * 1024 * 1024):F2} GB"
    };

    // ======================== Best DNS Auto-Detect ========================
    private async void FindBestDns_Click(object? sender, RoutedEventArgs e)
    {
        FindBestDnsBtn.IsEnabled = false;
        FindBestDnsLabel.Text = LocalizationService._("common.scanning");
        BestDnsHeader.IsVisible = true;
        BestDnsRecommend.IsVisible = true;
        BestDnsRecommend.Text = LocalizationService._("common.scanning");
        BestDnsResults.Children.Clear();

        var servers = new (string Name, string Ip)[]
        {
            ("Cloudflare", "1.1.1.1"), ("Cloudflare 2", "1.0.0.1"),
            ("Google", "8.8.8.8"), ("Google 2", "8.8.4.4"),
            ("Quad9", "9.9.9.9"), ("Quad9 2", "149.112.112.112"),
            ("OpenDNS", "208.67.222.222"), ("OpenDNS 2", "208.67.220.220"),
            ("AdGuard", "94.140.14.14"), ("CleanBrowsing", "185.228.168.168"),
            ("Comodo", "8.26.56.26"), ("Yandex", "77.88.8.8"),
        };

        var results = new List<(string Name, string Ip, double Ms, bool Ok)>();

        await Task.Run(async () =>
        {
            // Ping all servers in parallel using ICMP ping for raw latency
            var tasks = servers.Select(async s =>
            {
                double totalMs = 0; int ok = 0;
                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        using var ping = new Ping();
                        var reply = await ping.SendPingAsync(s.Ip, 2000);
                        if (reply.Status == IPStatus.Success) { totalMs += reply.RoundtripTime; ok++; }
                    }
                    catch { }
                }
                return (s.Name, s.Ip, Ms: ok > 0 ? totalMs / ok : -1, Ok: ok > 0);
            }).ToArray();

            var all = await Task.WhenAll(tasks);
            results.AddRange(all);
        });

        results.Sort((a, b) => a.Ms < 0 ? 1 : b.Ms < 0 ? -1 : a.Ms.CompareTo(b.Ms));
        var best = results.FirstOrDefault(r => r.Ok);

        BestDnsRecommend.Text = best.Ok
            ? $"Best for you: {best.Name} ({best.Ip}) - {best.Ms:F1}ms avg latency"
            : "Could not reach any DNS servers";

        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            var color = !r.Ok ? "#EF4444" : r.Ms < 10 ? "#22C55E" : r.Ms < 30 ? "#60A5FA" : r.Ms < 60 ? "#F59E0B" : "#EF4444";
            var msText = r.Ok ? $"{r.Ms:F1} ms" : "Timeout";
            var barWidth = r.Ok ? Math.Max(8, Math.Min(200, (int)(200 * (1 - r.Ms / 150.0)))) : 0;

            var row = new Border
            {
                CornerRadius = new global::Avalonia.CornerRadius(6),
                Background = new SolidColorBrush(Color.Parse(i == 0 && r.Ok ? "#1522C55E" : "#12FFFFFF")),
                Padding = new global::Avalonia.Thickness(12, 6),
                Margin = new global::Avalonia.Thickness(0, 0, 0, 2),
                Child = new Grid
                {
                    ColumnDefinitions = ColumnDefinitions.Parse("Auto,*,120,Auto"),
                    Children =
                    {
                        new TextBlock { Text = $"#{i+1}", FontSize = 12, FontWeight = FontWeight.Bold,
                            Foreground = new SolidColorBrush(Color.Parse(i < 3 ? "#00D4AA" : "#555570")),
                            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                            Margin = new global::Avalonia.Thickness(0,0,10,0) },
                        SetCol(new StackPanel { Children = {
                            new TextBlock { Text = r.Name, FontSize = 11, FontWeight = FontWeight.SemiBold,
                                Foreground = new SolidColorBrush(Color.Parse("#E8E8F0")) },
                            new TextBlock { Text = r.Ip, FontSize = 9, Foreground = new SolidColorBrush(Color.Parse("#8888A0")) }
                        }}, 1),
                        SetCol(new Border {
                            Height = 6, CornerRadius = new global::Avalonia.CornerRadius(3),
                            Background = new SolidColorBrush(Color.Parse("#15FFFFFF")),
                            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                            Child = new Border {
                                Height = 6, Width = barWidth, CornerRadius = new global::Avalonia.CornerRadius(3),
                                Background = new SolidColorBrush(Color.Parse(color)),
                                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left
                            }
                        }, 2),
                        SetCol(new TextBlock { Text = msText, FontSize = 13, FontWeight = FontWeight.Bold,
                            Foreground = new SolidColorBrush(Color.Parse(color)),
                            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                            Margin = new global::Avalonia.Thickness(10,0,0,0) }, 3)
                    }
                }
            };

            // Add "Apply" button for the best result
            if (i == 0 && r.Ok && _module is not null)
            {
                var grid = (Grid)row.Child;
                grid.ColumnDefinitions = ColumnDefinitions.Parse("Auto,*,120,Auto,Auto");
                var applyBtn = new Button
                {
                    Content = "Apply Best", Classes = { "action-btn" },
                    Padding = new global::Avalonia.Thickness(8, 3), FontSize = 9,
                    Tag = r.Ip,
                    Margin = new global::Avalonia.Thickness(8, 0, 0, 0),
                    VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                    Background = new SolidColorBrush(Color.Parse("#22C55E")),
                    Foreground = new SolidColorBrush(Color.Parse("#0A0A0F")),
                    FontWeight = FontWeight.Bold
                };
                applyBtn.Click += ApplyBestDns_Click;
                Grid.SetColumn(applyBtn, 4);
                grid.Children.Add(applyBtn);
            }

            BestDnsResults.Children.Add(row);
        }

        FindBestDnsBtn.IsEnabled = true;
        FindBestDnsLabel.Text = LocalizationService._("netOpt.bestDns");
    }

    private async void ApplyBestDns_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || _module is null) return;
        var bestIp = btn.Tag?.ToString();
        if (string.IsNullOrEmpty(bestIp)) return;

        // Measure before
        _preChangeDnsLatencyMs = MeasureDnsLatencyMs();

        SubText.Text = $"Applying best DNS ({bestIp})...";
        try
        {
            // Find the matching preset or use raw IP
            var secondary = bestIp switch
            {
                "1.1.1.1" => "1.0.0.1", "1.0.0.1" => "1.1.1.1",
                "8.8.8.8" => "8.8.4.4", "8.8.4.4" => "8.8.8.8",
                "9.9.9.9" => "149.112.112.112", "149.112.112.112" => "9.9.9.9",
                "208.67.222.222" => "208.67.220.220", "208.67.220.220" => "208.67.222.222",
                "94.140.14.14" => "94.140.15.15",
                _ => "1.1.1.1" // fallback secondary
            };

            var plan = new OptimizationPlan(_module.Id, new[] { "change-dns", bestIp, secondary });
            var result = await _module.OptimizeAsync(plan);
            SubText.Text = result.Success ? $"Applied best DNS: {bestIp}" : "Failed - try as admin";
            await RunScan();

            if (result.Success && _preChangeDnsLatencyMs > 0)
            {
                await Task.Delay(500);
                var afterMs = MeasureDnsLatencyMs();
                ShowDnsComparison(_preChangeDnsLatencyMs, afterMs);
            }
        }
        catch (System.Exception ex) { SubText.Text = $"Error: {ex.Message}"; }
    }

    // ======================== Wi-Fi Signal Strength ========================
    private async Task LoadWifiSignalAsync()
    {
        try
        {
            var wifiInfo = await Task.Run(() => GetWifiInfo());
            if (wifiInfo.Signal >= 0)
            {
                WifiPanel.IsVisible = true;
                WifiSignalText.Text = $"{wifiInfo.Signal}%";
                WifiSsid.Text = !string.IsNullOrEmpty(wifiInfo.Ssid) ? $"({wifiInfo.Ssid})" : "";
                WifiChannel.Text = wifiInfo.Channel;
                WifiBand.Text = wifiInfo.Band;
                WifiSpeed.Text = wifiInfo.Speed;

                // Set bar width proportional to signal (assume parent is ~400px)
                WifiBar.Width = Math.Max(4, wifiInfo.Signal * 3.5);

                // Color based on signal strength
                var barColor = wifiInfo.Signal > 70 ? "#22C55E" : wifiInfo.Signal > 40 ? "#F59E0B" : "#EF4444";
                WifiBar.Background = new SolidColorBrush(Color.Parse(barColor));
                WifiSignalText.Foreground = new SolidColorBrush(Color.Parse(barColor));
            }
        }
        catch { /* WiFi not available */ }
    }

    private static (int Signal, string Ssid, string Channel, string Band, string Speed) GetWifiInfo()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return GetWifiInfoWindows();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return GetWifiInfoLinux();
        }
        return (-1, "", "--", "--", "--");
    }

    private static (int Signal, string Ssid, string Channel, string Band, string Speed) GetWifiInfoWindows()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "wlan show interfaces",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc is null) return (-1, "", "--", "--", "--");
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            int signal = -1;
            string ssid = "", channel = "--", band = "--", speed = "--";

            foreach (var rawLine in output.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.StartsWith("Signal", StringComparison.OrdinalIgnoreCase) && line.Contains(':'))
                {
                    var val = line.Split(':')[1].Trim().Replace("%", "");
                    int.TryParse(val, out signal);
                }
                else if (line.StartsWith("SSID", StringComparison.OrdinalIgnoreCase) && !line.StartsWith("BSSID", StringComparison.OrdinalIgnoreCase) && line.Contains(':'))
                {
                    ssid = line.Split(':', 2)[1].Trim();
                }
                else if (line.StartsWith("Channel", StringComparison.OrdinalIgnoreCase) && line.Contains(':'))
                {
                    channel = line.Split(':')[1].Trim();
                }
                else if (line.StartsWith("Radio type", StringComparison.OrdinalIgnoreCase) && line.Contains(':'))
                {
                    band = line.Split(':')[1].Trim();
                }
                else if (line.StartsWith("Receive rate", StringComparison.OrdinalIgnoreCase) && line.Contains(':'))
                {
                    speed = line.Split(':')[1].Trim();
                }
            }
            return (signal, ssid, channel, band, speed);
        }
        catch { return (-1, "", "--", "--", "--"); }
    }

    private static (int Signal, string Ssid, string Channel, string Band, string Speed) GetWifiInfoLinux()
    {
        try
        {
            // Try /proc/net/wireless first
            var psi = new ProcessStartInfo
            {
                FileName = "bash",
                Arguments = "-c \"cat /proc/net/wireless 2>/dev/null; iwconfig 2>/dev/null\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc is null) return (-1, "", "--", "--", "--");
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            int signal = -1;
            string ssid = "", channel = "--", band = "--", speed = "--";

            foreach (var rawLine in output.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Contains("ESSID:"))
                {
                    var idx = line.IndexOf("ESSID:");
                    ssid = line[(idx + 6)..].Trim('"', ' ');
                }
                if (line.Contains("Signal level="))
                {
                    var idx = line.IndexOf("Signal level=") + 13;
                    var end = line.IndexOf(' ', idx);
                    if (end < 0) end = line.Length;
                    var val = line[idx..end].Trim();
                    if (int.TryParse(val.Replace("dBm", ""), out int dbm))
                    {
                        // Convert dBm to percentage (rough): -30=100%, -90=0%
                        signal = Math.Clamp((dbm + 90) * 100 / 60, 0, 100);
                    }
                }
                if (line.Contains("Bit Rate=") || line.Contains("Bit Rate:"))
                {
                    var idx = line.IndexOf("Bit Rate") + 9;
                    var end = line.IndexOf(' ', idx + 1);
                    if (end > idx) speed = line[idx..end].Trim('=', ':');
                }
            }
            return (signal, ssid, channel, band, speed);
        }
        catch { return (-1, "", "--", "--", "--"); }
    }

    // ======================== Bandwidth Per Process ========================
    private async void RefreshBandwidth_Click(object? sender, RoutedEventArgs e) => await LoadBandwidthAsync();

    private async Task LoadBandwidthAsync()
    {
        try
        {
            RefreshBwBtn.IsEnabled = false;
            var items = await Task.Run(() => GetBandwidthByProcess());
            BandwidthList.ItemsSource = items;
        }
        catch { }
        finally { RefreshBwBtn.IsEnabled = true; }
    }

    private static List<BandwidthItem> GetBandwidthByProcess()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return GetBandwidthWindows();
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return GetBandwidthLinux();
        return new List<BandwidthItem>();
    }

    private static List<BandwidthItem> GetBandwidthWindows()
    {
        try
        {
            // Use Get-NetTCPConnection to map connections to processes
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-NoProfile -Command \"Get-NetTCPConnection -State Established -ErrorAction SilentlyContinue | Group-Object OwningProcess | Sort-Object Count -Descending | Select-Object -First 15 | ForEach-Object { $proc = Get-Process -Id $_.Name -ErrorAction SilentlyContinue; if($proc) { '{0}|{1}|{2}|{3}' -f $_.Name, $proc.ProcessName, $_.Count, ($_.Group | Select-Object -First 3 | ForEach-Object { $_.RemoteAddress + ':' + $_.RemotePort }) -join ',' } }\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc is null) return new List<BandwidthItem>();
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            var items = new List<BandwidthItem>();
            int rank = 1;
            foreach (var rawLine in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;
                var parts = line.Split('|');
                if (parts.Length >= 3)
                {
                    var pid = parts[0].Trim();
                    var name = parts[1].Trim();
                    var count = parts[2].Trim();
                    var remotes = parts.Length > 3 ? parts[3].Trim() : "";

                    // Truncate remote info for display
                    if (remotes.Length > 60) remotes = remotes[..57] + "...";

                    items.Add(new BandwidthItem(
                        $"#{rank}",
                        name,
                        !string.IsNullOrEmpty(remotes) ? remotes : "Local connections",
                        $"{count} conn",
                        $"PID {pid}"
                    ));
                    rank++;
                    if (rank > 10) break;
                }
            }

            // Fallback: if PowerShell gave no results, try netstat
            if (items.Count == 0)
                return GetBandwidthViaNetstat();

            return items;
        }
        catch
        {
            return GetBandwidthViaNetstat();
        }
    }

    private static List<BandwidthItem> GetBandwidthViaNetstat()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c netstat -no -p tcp",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc is null) return new List<BandwidthItem>();
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            // Group by PID
            var pidCounts = new Dictionary<string, (int Count, string Sample)>();
            foreach (var rawLine in output.Split('\n'))
            {
                var line = rawLine.Trim();
                if (!line.StartsWith("TCP", StringComparison.OrdinalIgnoreCase)) continue;
                var cols = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (cols.Length < 5) continue;
                var state = cols[3];
                if (!state.Equals("ESTABLISHED", StringComparison.OrdinalIgnoreCase)) continue;
                var pid = cols[4];
                var remote = cols[2];
                if (pidCounts.TryGetValue(pid, out var existing))
                    pidCounts[pid] = (existing.Count + 1, existing.Sample);
                else
                    pidCounts[pid] = (1, remote);
            }

            var items = new List<BandwidthItem>();
            int rank = 1;
            foreach (var kv in pidCounts.OrderByDescending(k => k.Value.Count).Take(10))
            {
                var processName = "Unknown";
                try
                {
                    if (int.TryParse(kv.Key, out int pid))
                    {
                        var p = Process.GetProcessById(pid);
                        processName = p.ProcessName;
                    }
                }
                catch { }

                items.Add(new BandwidthItem(
                    $"#{rank}",
                    processName,
                    kv.Value.Sample,
                    $"{kv.Value.Count} conn",
                    $"PID {kv.Key}"
                ));
                rank++;
            }
            return items;
        }
        catch { return new List<BandwidthItem>(); }
    }

    private static List<BandwidthItem> GetBandwidthLinux()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "bash",
                Arguments = "-c \"ss -tnp state established 2>/dev/null | tail -n +2 | awk '{print $NF}' | grep -oP 'pid=\\K[0-9]+' | sort | uniq -c | sort -rn | head -10\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc is null) return new List<BandwidthItem>();
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            var items = new List<BandwidthItem>();
            int rank = 1;
            foreach (var rawLine in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = rawLine.Trim().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;
                var count = parts[0];
                var pid = parts[1];

                var processName = "Unknown";
                try
                {
                    if (int.TryParse(pid, out int pidInt))
                    {
                        var p = Process.GetProcessById(pidInt);
                        processName = p.ProcessName;
                    }
                }
                catch { }

                items.Add(new BandwidthItem($"#{rank}", processName, "", $"{count} conn", $"PID {pid}"));
                rank++;
            }
            return items;
        }
        catch { return new List<BandwidthItem>(); }
    }

    private void ApplyLocalization()
    {
        var L = LocalizationService._;
        PageTitle.Text = L("nav.network");
        ModuleHdr.Title = L("netOpt.title");
        ModuleHdr.Subtitle = L("netOpt.subtitle");
        ScanLabel.Text = L("common.scan");
        BenchLabel.Text = L("netOpt.benchDns");
        FindBestDnsLabel.Text = L("netOpt.bestDns");
        LblCurrentDns.Text = L("netOpt.currentDns");
        LblProvider.Text = L("netOpt.provider");
        LblResponseTime.Text = L("netOpt.responseTime");
        LblInterfaces.Text = L("netOpt.interfaces");
        LblSent.Text = L("netOpt.sent");
        LblRecv.Text = L("netOpt.recv");
        LblWifiSignal.Text = L("netOpt.wifiSignal");
        LblChannel.Text = L("netOpt.channel");
        LblBand.Text = L("netOpt.band");
        LblSpeed.Text = L("netOpt.speed");
        LblDnsChangeResults.Text = L("netOpt.dnsChangeResults");
        LblBefore.Text = L("netOpt.before");
        LblAfter.Text = L("netOpt.after");
        LblNetworkAdapters.Text = L("netOpt.networkAdapters");
        LblDnsPresets.Text = L("netOpt.dnsPresets");
        LblBandwidthByProcess.Text = L("netOpt.bandwidthByProcess");
        BenchHeader.Text = L("netOpt.dnsBenchmark");
        BestDnsHeader.Text = L("netOpt.bestDnsForYou");
        RefreshBwBtn.Content = L("common.refresh");
    }
}
