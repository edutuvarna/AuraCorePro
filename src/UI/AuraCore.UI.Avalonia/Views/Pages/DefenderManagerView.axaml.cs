using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.DefenderManager;
using AuraCore.Module.DefenderManager.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

public record ThreatDisplayItem(string Name, string Path, string Severity, string Status,
    ISolidColorBrush SevFg, ISolidColorBrush SevBg);

public partial class DefenderManagerView : UserControl
{
    private readonly DefenderManagerModule? _module;

    public DefenderManagerView()
    {
        InitializeComponent();
        Loaded += (s, e) => ApplyLocalization();
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
        _module = App.Services.GetServices<IOptimizationModule>()
            .OfType<DefenderManagerModule>().FirstOrDefault();
        Loaded += async (s, e) => await RunScan();
}

    private async Task RunScan()
    {
        if (_module is null) return;
        ScanLabel.Text = "Scanning...";
        try
        {
            await _module.ScanAsync(new ScanOptions());
            var s = _module.LastStatus;
            if (s is null) { SubText.Text = s?.Error ?? "Failed"; return; }

            ProtLevel.Text = s.ProtectionLevel;
            ProtLevel.Foreground = new SolidColorBrush(Color.Parse(s.ProtectionLevel switch
            {
                "Excellent" => "#22C55E", "Good" => "#00D4AA", "Partial" => "#F59E0B", _ => "#EF4444"
            }));
            EnabledCount.Text = $"{s.EnabledCount}/6 protections enabled";

            // Protection toggles
            TogglePanel.Children.Clear();
            var toggles = new (string label, bool on)[]
            {
                ("Real-Time Protection", s.RealTimeProtection),
                ("Cloud Protection", s.CloudProtection),
                ("Behavior Monitoring", s.BehaviorMonitoring),
                ("PUA Protection", s.PotentiallyUnwantedApps),
                ("Network Protection", s.NetworkProtection),
                ("Tamper Protection", s.TamperProtection),
            };
            foreach (var (label, on) in toggles)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                row.Children.Add(new Border
                {
                    Width = 8, Height = 8, CornerRadius = new CornerRadius(4),
                    Background = new SolidColorBrush(Color.Parse(on ? "#22C55E" : "#EF4444")),
                    VerticalAlignment = VerticalAlignment.Center
                });
                row.Children.Add(new TextBlock
                {
                    Text = label, FontSize = 11, Foreground = new SolidColorBrush(Color.Parse("#E8E8F0")),
                    VerticalAlignment = VerticalAlignment.Center
                });
                row.Children.Add(new TextBlock
                {
                    Text = on ? "ON" : "OFF", FontSize = 10, FontWeight = FontWeight.Bold,
                    Foreground = new SolidColorBrush(Color.Parse(on ? "#22C55E" : "#EF4444")),
                    VerticalAlignment = VerticalAlignment.Center
                });
                TogglePanel.Children.Add(row);
            }

            // Firewall
            SetFw(FwDomain, s.FirewallDomain);
            SetFw(FwPrivate, s.FirewallPrivate);
            SetFw(FwPublic, s.FirewallPublic);

            // Signatures
            SigVer.Text = s.AntivirusSignatureVersion;
            EngineVer.Text = s.EngineVersion;
            SigDate.Text = s.AntivirusSignatureLastUpdated?.ToString("yyyy-MM-dd HH:mm") ?? "Unknown";
            if (s.SignaturesOutdated) SigDate.Foreground = new SolidColorBrush(Color.Parse("#EF4444"));

            // Threats
            var threats = _module.LastThreats.Select(t =>
            {
                var (fg, bg) = t.Severity switch
                {
                    "Severe" => (P("#EF4444"), P("#20EF4444")),
                    "High"   => (P("#F59E0B"), P("#20F59E0B")),
                    _        => (P("#3B82F6"), P("#203B82F6"))
                };
                return new ThreatDisplayItem(t.ThreatName, t.Path, t.Severity, t.Status, fg, bg);
            }).ToList();
            ThreatList.ItemsSource = threats;
        }
        catch (System.Exception ex) { SubText.Text = $"Error: {ex.Message}"; }
        finally { ScanLabel.Text = "Refresh"; }
    }

    private static void SetFw(TextBlock tb, bool on)
    {
        tb.Text = on ? "Enabled" : "Disabled";
        tb.Foreground = new SolidColorBrush(Color.Parse(on ? "#22C55E" : "#EF4444"));
}

    private static SolidColorBrush P(string hex) => new(Color.Parse(hex));
    private async void Scan_Click(object? sender, RoutedEventArgs e) => await RunScan();

    private void ApplyLocalization()
    {
        PageTitle.Text = LocalizationService._("nav.defender");
    }
}