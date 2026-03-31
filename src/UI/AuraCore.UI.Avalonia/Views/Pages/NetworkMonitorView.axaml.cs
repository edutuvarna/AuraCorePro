using System.Net.NetworkInformation;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class NetworkMonitorView : UserControl
{
    public NetworkMonitorView()
    {
        InitializeComponent();
        Loaded += (s, e) => { RunScan(); ApplyLocalization(); };
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
    }

    private void Scan_Click(object? sender, RoutedEventArgs e) => RunScan();

    private void RunScan()
    {
        try
        {
            var ifaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .ToList();

            IfaceCount.Text = ifaces.Count.ToString();
            long totalSent = 0, totalRecv = 0;

            var items = ifaces.Select(n =>
            {
                var stats = n.GetIPv4Statistics();
                var sent = stats.BytesSent;
                var recv = stats.BytesReceived;
                totalSent += sent;
                totalRecv += recv;
                var speed = n.Speed > 0 ? $"{n.Speed / 1_000_000} Mbps" : "N/A";
                var typeStr = n.NetworkInterfaceType.ToString();

                return new Border
                {
                    CornerRadius = new global::Avalonia.CornerRadius(6),
                    Background = new SolidColorBrush(Color.Parse("#12FFFFFF")),
                    Padding = new global::Avalonia.Thickness(12, 10),
                    Margin = new global::Avalonia.Thickness(0, 0, 0, 4),
                    Child = new Grid
                    {
                        ColumnDefinitions = global::Avalonia.Controls.ColumnDefinitions.Parse("*,Auto,Auto"),
                        Children =
                        {
                            new StackPanel { Children = {
                                new TextBlock { Text = n.Name, FontSize = 13, FontWeight = FontWeight.SemiBold,
                                    Foreground = new SolidColorBrush(Color.Parse("#E8E8F0")) },
                                new TextBlock { Text = $"{typeStr} | {speed} | {n.Description}", FontSize = 10,
                                    Foreground = new SolidColorBrush(Color.Parse("#8888A0")),
                                    TextWrapping = global::Avalonia.Media.TextWrapping.Wrap }
                            }},
                            new StackPanel { [Grid.ColumnProperty] = 1, Margin = new global::Avalonia.Thickness(12, 0), Children = {
                                new TextBlock { Text = $"\u2B06 {FormatBytes(sent)}", FontSize = 11,
                                    Foreground = new SolidColorBrush(Color.Parse("#0080FF")) },
                                new TextBlock { Text = $"\u2B07 {FormatBytes(recv)}", FontSize = 11,
                                    Foreground = new SolidColorBrush(Color.Parse("#22C55E")) }
                            }},
                        }
                    }
                };
            }).ToList();

            TotalSent.Text = FormatBytes(totalSent);
            TotalRecv.Text = FormatBytes(totalRecv);
            IfaceList.ItemsSource = items;
        }
        catch { }
    }

    private static string FormatBytes(long b) => b switch
    {
        < 1024 => $"{b} B",
        < 1024 * 1024 => $"{b / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{b / (1024.0 * 1024):F1} MB",
        _ => $"{b / (1024.0 * 1024 * 1024):F2} GB"
    };

    private void ApplyLocalization() { PageTitle.Text = LocalizationService._("nav.networkMonitor"); }
}
