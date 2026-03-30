using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Pages;

public record DiskDisplayItem(string Model, string Info, string Size, string MediaType,
    string Interface, string Partitions, string Health, ISolidColorBrush HealthFg, ISolidColorBrush HealthBg);

public partial class DiskHealthView : UserControl
{

    public DiskHealthView()
    {
        InitializeComponent();
        Loaded += (s, e) => ApplyLocalization();
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
        Loaded += async (s, e) => { await RunScan(); ApplyLocalization(); };
}
private async Task RunScan()
    {
        ScanLabel.Text = "Scanning...";
        try
        {
            var items = await Task.Run(() =>
            {
                var drives = DriveInfo.GetDrives().Where(d => d.IsReady).ToList();
                return drives.Select(d =>
                {
                    var totalGb = d.TotalSize / (1024.0 * 1024 * 1024);
                    var freeGb = d.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                    var usedPct = (totalGb - freeGb) / totalGb * 100;
                    var health = usedPct > 95 ? "Critical" : usedPct > 85 ? "Warning" : "Healthy";
                    var (fg, bg) = health switch
                    {
                        "Critical" => (P("#EF4444"), P("#20EF4444")),
                        "Warning"  => (P("#F59E0B"), P("#20F59E0B")),
                        _          => (P("#22C55E"), P("#2022C55E"))
                    };
                    return new DiskDisplayItem(
                        $"{d.Name} ({d.VolumeLabel})",
                        $"{d.DriveFormat} - {freeGb:F1} GB free of {totalGb:F1} GB ({usedPct:F0}% used)",
                        $"{totalGb:F1} GB", d.DriveType.ToString(),
                        d.DriveFormat, "N/A", health, fg, bg
                    );
                }).ToList();
            });
            DriveList.ItemsSource = items;
        }
        catch { }
        finally { ScanLabel.Text = "Scan"; }
}

    private static SolidColorBrush P(string h) => new(Color.Parse(h));
    private async void Scan_Click(object? s, RoutedEventArgs e) => await RunScan();

    private void ApplyLocalization()
    {
        PageTitle.Text = LocalizationService._("nav.diskHealth");
    }
}