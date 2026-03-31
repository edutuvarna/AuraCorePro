using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class DnsBenchmarkView : UserControl
{
    private static readonly (string Name, string Ip)[] DnsServers = new[]
    {
        ("Cloudflare", "1.1.1.1"),
        ("Cloudflare 2", "1.0.0.1"),
        ("Google", "8.8.8.8"),
        ("Google 2", "8.8.4.4"),
        ("Quad9", "9.9.9.9"),
        ("OpenDNS", "208.67.222.222"),
        ("Comodo", "8.26.56.26"),
        ("CleanBrowsing", "185.228.168.168"),
        ("AdGuard", "94.140.14.14"),
        ("Yandex", "77.88.8.8"),
    };

    public DnsBenchmarkView()
    {
        InitializeComponent();
        Loaded += (s, e) => ApplyLocalization();
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
    }

    private async void Bench_Click(object? sender, RoutedEventArgs e)
    {
        BenchBtn.IsEnabled = false;
        SubText.Text = "Running benchmark... (testing 10 DNS servers)";
        RecommendText.Text = "";

        var results = new List<(string Name, string Ip, double Ms, bool Ok)>();

        await Task.Run(() =>
        {
            var testDomains = new[] { "google.com", "github.com", "cloudflare.com" };
            foreach (var (name, ip) in DnsServers)
            {
                double totalMs = 0;
                int ok = 0;
                foreach (var domain in testDomains)
                {
                    var ms = PingDns(ip, domain);
                    if (ms >= 0) { totalMs += ms; ok++; }
                }
                var avg = ok > 0 ? totalMs / ok : -1;
                results.Add((name, ip, avg, ok > 0));
            }
        });

        results.Sort((a, b) => a.Ms < 0 ? 1 : b.Ms < 0 ? -1 : a.Ms.CompareTo(b.Ms));
        SubText.Text = $"Benchmark complete - tested {DnsServers.Length} servers";

        var best = results.FirstOrDefault(r => r.Ok);
        if (best.Ok)
            RecommendText.Text = $"Recommended: {best.Name} ({best.Ip}) - {best.Ms:F1}ms average";

        DnsList.ItemsSource = results.Select((r, i) =>
        {
            var color = !r.Ok ? "#EF4444" : r.Ms < 20 ? "#22C55E" : r.Ms < 50 ? "#F59E0B" : "#EF4444";
            var msText = r.Ok ? $"{r.Ms:F1} ms" : "Timeout";
            var rank = i + 1;

            return new Border
            {
                CornerRadius = new global::Avalonia.CornerRadius(6),
                Background = new SolidColorBrush(Color.Parse(rank == 1 && r.Ok ? "#1522C55E" : "#12FFFFFF")),
                Padding = new global::Avalonia.Thickness(12, 10),
                Margin = new global::Avalonia.Thickness(0, 0, 0, 4),
                Child = new Grid
                {
                    ColumnDefinitions = global::Avalonia.Controls.ColumnDefinitions.Parse("Auto,*,Auto,Auto"),
                    Children =
                    {
                        new TextBlock { Text = $"#{rank}", FontSize = 14, FontWeight = FontWeight.Bold,
                            Foreground = new SolidColorBrush(Color.Parse(rank <= 3 ? "#00D4AA" : "#555570")),
                            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                            Margin = new global::Avalonia.Thickness(0, 0, 12, 0) },
                        new StackPanel { [Grid.ColumnProperty] = 1, Children = {
                            new TextBlock { Text = r.Name, FontSize = 13, FontWeight = FontWeight.SemiBold,
                                Foreground = new SolidColorBrush(Color.Parse("#E8E8F0")) },
                            new TextBlock { Text = r.Ip, FontSize = 10, Foreground = new SolidColorBrush(Color.Parse("#8888A0")) }
                        }},
                        new TextBlock { [Grid.ColumnProperty] = 3, Text = msText, FontSize = 16, FontWeight = FontWeight.Bold,
                            Foreground = new SolidColorBrush(Color.Parse(color)),
                            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center }
                    }
                }
            };
        }).ToList();

        BenchBtn.IsEnabled = true;
    }

    private static double PingDns(string dnsIp, string domain)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            using var client = new UdpClient();
            client.Client.ReceiveTimeout = 2000;
            client.Client.SendTimeout = 2000;

            // Build minimal DNS query
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
        // Header: ID=0x1234, flags=0x0100 (standard query), 1 question
        ms.Write(new byte[] { 0x12, 0x34, 0x01, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
        foreach (var part in domain.Split('.'))
        {
            ms.WriteByte((byte)part.Length);
            ms.Write(System.Text.Encoding.ASCII.GetBytes(part));
        }
        ms.WriteByte(0); // end
        ms.Write(new byte[] { 0x00, 0x01, 0x00, 0x01 }); // Type A, Class IN
        return ms.ToArray();
    }

    private void ApplyLocalization() { PageTitle.Text = LocalizationService._("nav.dnsBenchmark"); }
}
