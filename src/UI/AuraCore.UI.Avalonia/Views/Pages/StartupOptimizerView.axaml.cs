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
        if (!OperatingSystem.IsWindows()) { SubText.Text = "Windows only feature"; return; }
        ScanLabel.Text = "Scanning...";
        try
        {
            var items = await Task.Run(() =>
            {
                var list = new List<StartupDisplayItem>();
                ScanRegistryRun(list, Registry.CurrentUser, "HKCU");
                ScanRegistryRun(list, Registry.LocalMachine, "HKLM");
                return list;
            });
            ItemList.ItemsSource = items;
            TotalItems.Text = items.Count.ToString();
            EnabledItems.Text = items.Count(i => i.IsEnabled).ToString();
            HighImpactItems.Text = items.Count(i => i.Impact == "High").ToString();
        }
        catch (System.Exception ex) { SubText.Text = $"Error: {ex.Message}"; }
        finally { ScanLabel.Text = "Scan"; }
    }

    private static void ScanRegistryRun(List<StartupDisplayItem> list, RegistryKey hive, string hiveName)
    {
        try
        {
            using var key = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
            if (key == null) return;
            foreach (var name in key.GetValueNames())
            {
                var cmd = key.GetValue(name)?.ToString() ?? "";
                var impact = HighImpact.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase)) ? "High" : "Low";
                var (fg, bg) = impact == "High" ? (P("#F59E0B"), P("#20F59E0B")) : (P("#22C55E"), P("#2022C55E"));
                list.Add(new StartupDisplayItem(name, cmd, hiveName, impact, fg, bg, "", true));
            }
        }
        catch { }
}

    private static SolidColorBrush P(string h) => new(Color.Parse(h));
    private async void Scan_Click(object? s, RoutedEventArgs e) => await RunScan();

    private void ApplyLocalization()
    {
        PageTitle.Text = LocalizationService._("nav.startupOptimizer");
    }
}