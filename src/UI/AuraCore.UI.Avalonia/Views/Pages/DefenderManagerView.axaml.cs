using System.Threading;
using System.Threading.Tasks;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.DefenderManager;
using AuraCore.Module.DefenderManager.Models;
using AuraCore.UI.Avalonia.Helpers;
using AuraCore.UI.Avalonia.Views.Dialogs;
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

    private async Task<bool> EnsurePrivilegedHelperInstalledAsync()
    {
        var installer = App.Services?.GetService<PrivilegedHelperInstaller>();
        if (installer is null) return true; // DI not wired (tests/design-time) — don't block

        if (await installer.IsInstalledAsync(CancellationToken.None))
            return true;

        // Prompt for consent + install
        var topWindow = TopLevel.GetTopLevel(this) as Window;
        if (topWindow is null) return false;

        var dialog = new PrivilegedHelperInstallDialog(installer);
        await dialog.ShowDialog(topWindow);
        return dialog.Outcome == PrivilegedHelperInstallOutcome.Success;
    }

    private async void UpdateSigs_Click(object? sender, RoutedEventArgs e)
    {
        if (!await EnsurePrivilegedHelperInstalledAsync())
        {
            DefActionStatus.Text = LocalizationService.Get("privhelper.notInstalled.toast");
            return;
        }
        await RunDefenderAction(() => _module!.UpdateSignaturesAsync(), "Updating signatures...", "Signatures updated.");
    }

    private async void QuickScan_Click(object? sender, RoutedEventArgs e)
    {
        if (!await EnsurePrivilegedHelperInstalledAsync())
        {
            DefActionStatus.Text = LocalizationService.Get("privhelper.notInstalled.toast");
            return;
        }
        await RunDefenderAction(() => _module!.QuickScanAsync(), "Running quick scan...", "Quick scan started.");
    }

    private async void FullScan_Click(object? sender, RoutedEventArgs e)
    {
        if (!await EnsurePrivilegedHelperInstalledAsync())
        {
            DefActionStatus.Text = LocalizationService.Get("privhelper.notInstalled.toast");
            return;
        }
        await RunDefenderAction(() => _module!.FullScanAsync(), "Running full scan...", "Full scan started.");
    }

    private async Task RunDefenderAction(
        System.Func<System.Threading.Tasks.Task<DefenderManagerModule.DefenderOperationOutcome>> action,
        string inProgressText, string successText)
    {
        if (_module is null) { DefActionStatus.Text = "Module not available."; return; }
        DefActionStatus.Text = inProgressText;
        SetActionButtonsEnabled(false);
        try
        {
            var result = await action();
            if (result.HelperMissing)
                DefActionStatus.Text = "Privileged helper not installed — run scripts/install-privileged-service.ps1 as admin";
            else if (result.Success)
                DefActionStatus.Text = successText;
            else
                DefActionStatus.Text = $"Failed: {result.Error ?? "unknown error"}";
        }
        catch (System.Exception ex)
        {
            DefActionStatus.Text = $"Error: {ex.Message}";
        }
        finally
        {
            SetActionButtonsEnabled(true);
        }
    }

    private void SetActionButtonsEnabled(bool enabled)
    {
        UpdateSigsBtn.IsEnabled = enabled;
        QuickScanBtn.IsEnabled  = enabled;
        FullScanBtn.IsEnabled   = enabled;
    }

    private void ApplyLocalization()
    {
        PageTitle.Text = LocalizationService._("nav.defender");
    }
}