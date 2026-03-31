using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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
public record DnsPresetItem(string Name, string Servers, string Category, string ActiveLabel, ISolidColorBrush ActiveBrush);

public partial class NetworkOptimizerView : UserControl
{
    private readonly NetworkOptimizerModule? _module;

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
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
        _module = App.Services.GetServices<IOptimizationModule>().OfType<NetworkOptimizerModule>().FirstOrDefault();
        Loaded += async (s, e) => await RunScan();
    }

    private async Task RunScan()
    {
        ScanLabel.Text = "Scanning...";
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
                        new SolidColorBrush(Color.Parse(p.IsCurrentlyActive ? "#22C55E" : "#555570"))
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
        finally { ScanLabel.Text = "Scan"; }
    }

    private async void Bench_Click(object? sender, RoutedEventArgs e)
    {
        BenchBtn.IsEnabled = false;
        BenchLabel.Text = "Running...";
        BenchHeader.IsVisible = true;
        RecommendText.IsVisible = true;
        RecommendText.Text = "Testing 10 DNS servers...";
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
        BenchLabel.Text = "Benchmark DNS";
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
            var plan = new OptimizationPlan(_module.Id, new[] { $"dns:{presetName}" });
            var result = await _module.OptimizeAsync(plan);
            SubText.Text = result.Success ? $"Switched to {presetName}" : "Failed - try as admin";
            await RunScan();
        }
        catch (System.Exception ex) { SubText.Text = $"Error: {ex.Message}"; }
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

    private void ApplyLocalization() { PageTitle.Text = LocalizationService._("nav.network"); }
}
