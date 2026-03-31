using System.Net;
using System.Net.Sockets;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class WakeOnLanView : UserControl
{
    private readonly List<(string Mac, DateTime Time)> _history = new();

    public WakeOnLanView()
    {
        InitializeComponent();
        Loaded += (s, e) => ApplyLocalization();
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
    }

    private async void Send_Click(object? sender, RoutedEventArgs e)
    {
        var mac = MacBox.Text?.Trim() ?? "";
        var broadcast = BroadcastBox.Text?.Trim() ?? "255.255.255.255";

        if (string.IsNullOrEmpty(mac))
        {
            StatusText.Text = "Enter a MAC address.";
            return;
        }

        try
        {
            var macBytes = ParseMac(mac);
            if (macBytes == null || macBytes.Length != 6)
            {
                StatusText.Text = "Invalid MAC format. Use AA:BB:CC:DD:EE:FF or AA-BB-CC-DD-EE-FF";
                return;
            }

            StatusText.Text = "Sending magic packet...";

            await Task.Run(() =>
            {
                // Build magic packet: 6x 0xFF + 16x MAC address
                var packet = new byte[6 + 16 * 6];
                for (int i = 0; i < 6; i++) packet[i] = 0xFF;
                for (int i = 0; i < 16; i++)
                    Array.Copy(macBytes, 0, packet, 6 + i * 6, 6);

                using var client = new UdpClient();
                client.EnableBroadcast = true;
                client.Send(packet, packet.Length, new IPEndPoint(IPAddress.Parse(broadcast), 9));
                // Also send on port 7
                client.Send(packet, packet.Length, new IPEndPoint(IPAddress.Parse(broadcast), 7));
            });

            StatusText.Text = $"Magic packet sent to {mac} via {broadcast}:9";
            StatusText.Foreground = new SolidColorBrush(Color.Parse("#22C55E"));

            _history.Insert(0, (mac, DateTime.Now));
            if (_history.Count > 10) _history.RemoveRange(10, _history.Count - 10);
            UpdateHistory();

            NotificationService.Instance.Post("Wake-on-LAN", $"Magic packet sent to {mac}", NotificationType.Success);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
            StatusText.Foreground = new SolidColorBrush(Color.Parse("#EF4444"));
        }
    }

    private void UpdateHistory()
    {
        HistoryStatus.Text = $"{_history.Count} device(s) in history";
        HistoryList.ItemsSource = _history.Select(h => new Border
        {
            CornerRadius = new global::Avalonia.CornerRadius(6),
            Background = new SolidColorBrush(Color.Parse("#12FFFFFF")),
            Padding = new global::Avalonia.Thickness(12, 8),
            Margin = new global::Avalonia.Thickness(0, 0, 0, 4),
            Child = new Grid
            {
                ColumnDefinitions = global::Avalonia.Controls.ColumnDefinitions.Parse("*,Auto"),
                Children =
                {
                    new TextBlock { Text = h.Mac, FontSize = 13, FontWeight = FontWeight.SemiBold,
                        Foreground = new SolidColorBrush(Color.Parse("#E8E8F0")),
                        FontFamily = new FontFamily("Courier New, monospace") },
                    new TextBlock { [Grid.ColumnProperty] = 1, Text = h.Time.ToString("HH:mm:ss"), FontSize = 11,
                        Foreground = new SolidColorBrush(Color.Parse("#8888A0")),
                        VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center }
                }
            }
        }).ToList();
    }

    private static byte[]? ParseMac(string mac)
    {
        try
        {
            var clean = mac.Replace(":", "").Replace("-", "").Replace(".", "");
            if (clean.Length != 12) return null;
            var bytes = new byte[6];
            for (int i = 0; i < 6; i++)
                bytes[i] = Convert.ToByte(clean.Substring(i * 2, 2), 16);
            return bytes;
        }
        catch { return null; }
    }

    private void ApplyLocalization() { PageTitle.Text = LocalizationService._("nav.wakeOnLan"); }
}
