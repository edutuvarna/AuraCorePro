using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using Microsoft.Win32;

namespace AuraCore.UI.Avalonia.Views.Pages;

public record StartupDisplayItem(string Name, string Command, string Location, string Impact,
    ISolidColorBrush ImpactFg, ISolidColorBrush ImpactBg, string Publisher, bool IsEnabled);

public partial class StartupOptimizerView : UserControl
{
    private static readonly HashSet<string> HighImpact = new(StringComparer.OrdinalIgnoreCase)
    {
        "OneDrive", "Teams", "Spotify", "Discord", "Steam", "EpicGamesLauncher",
        "GoogleDriveSync", "Dropbox", "Skype", "Slack", "Zoom", "Opera",
    };

    public StartupOptimizerView()
    {
        InitializeComponent();
        Loaded += async (s, e) => { await RunScan(); ApplyLocalization(); };
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
    }

    private async Task RunScan()
    {
        if (!OperatingSystem.IsWindows()) { SubText.Text = LocalizationService._("startup.windowsOnly"); return; }
        ScanLabel.Text = LocalizationService._("common.scanning");
        try
        {
            // Collect raw data on background thread (no UI objects)
            var rawData = await Task.Run(() =>
            {
                var list = new List<(string Name, string Cmd, string Hive, string Impact, bool Enabled)>();
                ScanReg(list, Registry.CurrentUser, "HKCU");
                ScanReg(list, Registry.LocalMachine, "HKLM");
                return list;
            });
            // Create UI brushes on UI thread
            var items = rawData.Select(r =>
            {
                var (fg, bg) = r.Impact == "High" ? (P("#F59E0B"), P("#20F59E0B")) : (P("#22C55E"), P("#2022C55E"));
                return new StartupDisplayItem(r.Name, r.Cmd, r.Hive, r.Impact, fg, bg, "", r.Enabled);
            }).ToList();
            ItemList.ItemsSource = items;
            TotalItems.Text = items.Count.ToString();
            EnabledItems.Text = items.Count(i => i.IsEnabled).ToString();
            HighImpactItems.Text = items.Count(i => i.Impact == "High").ToString();
        }
        catch (System.Exception ex) { SubText.Text = $"{LocalizationService._("common.errorPrefix")}{ex.Message}"; }
        finally { ScanLabel.Text = LocalizationService._("common.scan"); }
    }

    private static void ScanReg(List<(string, string, string, string, bool)> list, RegistryKey hive, string hiveName)
    {
        try
        {
            using var key = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
            if (key == null) return;
            foreach (var name in key.GetValueNames())
            {
                var cmd = key.GetValue(name)?.ToString() ?? "";
                var impact = HighImpact.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase)) ? "High" : "Low";
                list.Add((name, cmd, hiveName, impact, true));
            }
        }
        catch { }
    }

    private static SolidColorBrush P(string h) => new(Color.Parse(h));
    private async void Scan_Click(object? s, RoutedEventArgs e) => await RunScan();

    private void ApplyLocalization()
    {
        PageTitle.Text = LocalizationService._("nav.startupOptimizer");
        ModuleHdr.Title = LocalizationService._("nav.startupOptimizer");
        ModuleHdr.Subtitle = LocalizationService._("startup.subtitleShort");
        ScanLabel.Text = LocalizationService._("common.scan");
        StatTotal.Label      = LocalizationService._("common.statTotal");
        StatEnabled.Label    = LocalizationService._("startup.statEnabled");
        StatHighImpact.Label = LocalizationService._("startup.statHighImpact");
    }
}
