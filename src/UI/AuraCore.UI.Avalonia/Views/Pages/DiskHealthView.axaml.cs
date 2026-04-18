using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Pages;

public record DiskDisplayItem(string Model, string Info, string Size, string MediaType,
    string Interface, string Partitions, string Health, ISolidColorBrush HealthFg, ISolidColorBrush HealthBg,
    string SizeLabel = "", string TypeLabel = "", string FormatLabel = "", string PartitionsLabel = "");

public partial class DiskHealthView : UserControl
{
    public DiskHealthView()
    {
        InitializeComponent();
        Loaded += async (s, e) => { await RunScan(); ApplyLocalization(); };
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
        Unloaded += (s, e) =>
            LocalizationService.LanguageChanged -= () =>
                global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
    }

    private async Task RunScan()
    {
        ScanLabel.Text = LocalizationService._("common.scanning");
        SubText.Text = LocalizationService._("diskHealth.status.scanningDrives");
        try
        {
            var rawData = await Task.Run(() =>
            {
                var list = new List<(string Name, string Info, string Size, string Type, string Format, string Health, double UsedPct)>();
                foreach (var d in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (!d.IsReady) continue;
                        var totalGb = d.TotalSize / (1024.0 * 1024 * 1024);
                        var freeGb = d.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                        var usedPct = totalGb > 0 ? (totalGb - freeGb) / totalGb * 100 : 0;
                        var health = usedPct > 95 ? "Critical" : usedPct > 85 ? "Warning" : "Healthy";
                        list.Add(($"{d.Name} ({d.VolumeLabel})",
                            $"{d.DriveFormat} - {freeGb:F1} GB free of {totalGb:F1} GB ({usedPct:F0}% used)",
                            $"{totalGb:F1} GB", d.DriveType.ToString(),
                            d.DriveFormat, health, usedPct));
                    }
                    catch { }
                }
                return list;
            });

            if (rawData.Count == 0)
            {
                SubText.Text = LocalizationService._("diskHealth.status.noDrives");
                return;
            }

            var items = rawData.Select(d =>
            {
                var (fg, bg) = d.Health switch
                {
                    "Critical" => (P("#EF4444"), P("#20EF4444")),
                    "Warning"  => (P("#F59E0B"), P("#20F59E0B")),
                    _          => (P("#22C55E"), P("#2022C55E"))
                };
                return new DiskDisplayItem(d.Name, d.Info, d.Size, d.Type, d.Format, "N/A", d.Health, fg, bg,
                    LocalizationService._("diskHealth.col.size"),
                    LocalizationService._("diskHealth.col.type"),
                    LocalizationService._("diskHealth.col.format"),
                    LocalizationService._("diskHealth.col.partitions"));
            }).ToList();
            DriveList.ItemsSource = items;
            SubText.Text = $"Found {items.Count} drive(s)";
        }
        catch (System.Exception ex) { SubText.Text = $"Error: {ex.Message}"; }
        finally { ScanLabel.Text = LocalizationService._("diskHealth.action.scan"); }
    }

    private static SolidColorBrush P(string h) => new(Color.Parse(h));
    private async void Scan_Click(object? s, RoutedEventArgs e) => await RunScan();

    private void ApplyLocalization()
    {
        PageTitle.Text = LocalizationService._("nav.diskHealth");
        var L = LocalizationService._;
        if (this.FindControl<global::AuraCore.UI.Avalonia.Views.Controls.ModuleHeader>("Header") is { } h)
        {
            h.Title = L("diskHealth.title");
            h.Subtitle = L("diskHealth.subtitle");
        }
        ScanLabel.Text = L("diskHealth.action.scan");
        DetectedDrivesLabel.Text = L("diskHealth.section.detectedDrives");
    }
}
