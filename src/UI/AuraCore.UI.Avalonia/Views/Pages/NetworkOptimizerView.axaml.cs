using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.NetworkOptimizer;
using AuraCore.Module.NetworkOptimizer.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

public record AdapterItem(string Name, string Desc, string Ip, string Speed);
public record DnsPresetItem(string Name, string Servers, string Category, string ActiveLabel, ISolidColorBrush ActiveBrush);

public partial class NetworkOptimizerView : UserControl
{
    private readonly NetworkOptimizerModule? _module;
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
        if (_module is null) return;
        ScanLabel.Text = "Scanning...";
        try
        {
            await _module.ScanAsync(new ScanOptions());
            var r = _module.LastReport; if (r is null) return;
            DnsPrimary.Text = !string.IsNullOrEmpty(r.CurrentDns.Primary) ? r.CurrentDns.Primary : GetSystemDns();
            DnsSecondary.Text = r.CurrentDns.Secondary;
            DnsProvider.Text = !string.IsNullOrEmpty(r.CurrentDns.ProviderName) ? r.CurrentDns.ProviderName : "ISP Default";
            DnsLatency.Text = r.CurrentDns.ResponseTimeMs > 0 ? $"{r.CurrentDns.ResponseTimeMs:F0}ms" : MeasureDnsLatency();
            AdapterList.ItemsSource = r.Adapters.Select(a => new AdapterItem(a.Name, a.Description, a.IpAddress, a.Speed)).ToList();
            DnsPresetList.ItemsSource = r.AvailableDnsPresets.Select(p => new DnsPresetItem(
                p.Name, $"{p.Primary} / {p.Secondary}", p.Category,
                p.IsCurrentlyActive ? "Active" : "",
                new SolidColorBrush(Color.Parse(p.IsCurrentlyActive ? "#22C55E" : "#555570"))
            )).ToList();
        }
        catch { SubText.Text = "Scan failed"; }
        finally { ScanLabel.Text = "Scan"; }
}
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
            var nics = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up
                    && n.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback);
            foreach (var nic in nics)
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
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            System.Net.Dns.GetHostEntry("google.com");
            sw.Stop();
            return $"{sw.ElapsedMilliseconds}ms";
        }
        catch { return "--"; }
    }

    private void ApplyLocalization()
    {
        PageTitle.Text = LocalizationService._("nav.network");
    }
}