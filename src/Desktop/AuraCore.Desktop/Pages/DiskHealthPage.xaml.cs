using AuraCore.Desktop.Services;
using System.Management;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AuraCore.Desktop.Pages;

public sealed partial class DiskHealthPage : Page
{
    public DiskHealthPage()
    {
        InitializeComponent();
        ApplyLocalization();
        Services.S.LanguageChanged += () => DispatcherQueue?.TryEnqueue(ApplyLocalization);
    }

    private sealed record DiskInfo
    {
        public string Model { get; init; } = "";
        public string SerialNumber { get; init; } = "";
        public string InterfaceType { get; init; } = "";
        public string MediaType { get; init; } = "";
        public long SizeBytes { get; init; }
        public string FirmwareRevision { get; init; } = "";
        public int Partitions { get; init; }
        public string Status { get; init; } = "";
        public int? Temperature { get; init; }
        public string HealthStatus { get; init; } = "";
        public int? PowerOnHours { get; init; }
        public int? WearLevel { get; init; }
        public int? ReadErrors { get; init; }
        public int? ReallocatedSectors { get; init; }
        public List<SmartAttribute> SmartAttributes { get; init; } = new();
    }

    private sealed record SmartAttribute(string Name, string Value, string Threshold, string Status);

    private async void ScanBtn_Click(object sender, RoutedEventArgs e)
    {
        ScanBtn.IsEnabled = false;
        Progress.IsActive = true; Progress.Visibility = Visibility.Visible;
        StatusText.Text = "Scanning drives...";
        DriveList.Children.Clear();

        try
        {
            var disks = await Task.Run(ScanDisks);

            if (disks.Count == 0)
            {
                StatusText.Text = "No drives found.";
                return;
            }

            foreach (var disk in disks)
            {
                DriveList.Children.Add(BuildDriveCard(disk));
            }

            var healthyCount = disks.Count(d => d.HealthStatus is "Healthy" or "OK");
            StatusText.Text = $"Found {disks.Count} drive(s) — {healthyCount} healthy";

            // Summary card
            TotalDrivesText.Text = disks.Count.ToString();
            HealthyDrivesText.Text = $"{healthyCount} / {disks.Count}";
            var tempsWithData = disks.Where(d => d.Temperature.HasValue).ToList();
            AvgTempText.Text = tempsWithData.Count > 0
                ? $"{tempsWithData.Average(d => d.Temperature!.Value):F0}°C"
                : "N/A";
            var totalGb = disks.Sum(d => d.SizeBytes) / (1024.0 * 1024 * 1024);
            TotalCapText.Text = totalGb >= 1000
                ? $"{totalGb / 1024:F1} TB"
                : $"{totalGb:F0} GB";
            SummaryCard.Visibility = Visibility.Visible;
        }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
        finally
        {
            ScanBtn.IsEnabled = true;
            Progress.IsActive = false; Progress.Visibility = Visibility.Collapsed;
        }
    }

    private static List<DiskInfo> ScanDisks()
    {
        var disks = new List<DiskInfo>();

        try
        {
            // Basic disk info from Win32_DiskDrive
            using var searcher = new ManagementObjectSearcher(
                "SELECT Model, SerialNumber, InterfaceType, MediaType, Size, FirmwareRevision, Partitions, Status FROM Win32_DiskDrive");

            foreach (ManagementObject obj in searcher.Get())
            {
                var model = obj["Model"]?.ToString()?.Trim() ?? "Unknown";
                var size = obj["Size"] is ulong s ? (long)s : 0;

                var disk = new DiskInfo
                {
                    Model = model,
                    SerialNumber = obj["SerialNumber"]?.ToString()?.Trim() ?? "",
                    InterfaceType = obj["InterfaceType"]?.ToString() ?? "",
                    MediaType = DetectMediaType(obj["MediaType"]?.ToString() ?? "", model),
                    SizeBytes = size,
                    FirmwareRevision = obj["FirmwareRevision"]?.ToString()?.Trim() ?? "",
                    Partitions = obj["Partitions"] is uint p ? (int)p : 0,
                    Status = obj["Status"]?.ToString() ?? "Unknown",
                };

                // Try to get SMART/health data from MSFT_PhysicalDisk
                var enhanced = EnhanceWithSmartData(disk);
                disks.Add(enhanced);
            }
        }
        catch { }

        return disks;
    }

    private static DiskInfo EnhanceWithSmartData(DiskInfo disk)
    {
        try
        {
            // MSFT_PhysicalDisk (Storage namespace) for health + temperature
            using var physSearcher = new ManagementObjectSearcher(
                @"\\.\root\Microsoft\Windows\Storage",
                "SELECT HealthStatus, OperationalStatus, MediaType, SpindleSpeed FROM MSFT_PhysicalDisk");

            foreach (ManagementObject phys in physSearcher.Get())
            {
                var physModel = phys["FriendlyName"]?.ToString() ?? "";

                // Match by partial model name
                if (!string.IsNullOrEmpty(physModel) &&
                    (disk.Model.Contains(physModel, StringComparison.OrdinalIgnoreCase) ||
                     physModel.Contains(disk.Model.Split(' ').FirstOrDefault() ?? "xxx", StringComparison.OrdinalIgnoreCase)))
                {
                    var healthCode = phys["HealthStatus"];
                    var healthStatus = healthCode switch
                    {
                        (ushort)0 => "Healthy",
                        (ushort)1 => "Warning",
                        (ushort)2 => "Unhealthy",
                        _ => healthCode?.ToString() ?? "Unknown"
                    };

                    var mediaType = phys["MediaType"] switch
                    {
                        (ushort)3 => "HDD",
                        (ushort)4 => "SSD",
                        (ushort)5 => "SCM",
                        _ => disk.MediaType
                    };

                    disk = disk with
                    {
                        HealthStatus = healthStatus,
                        MediaType = mediaType
                    };
                    break;
                }
            }
        }
        catch { }

        // Try MSFT_StorageReliabilityCounter for detailed stats
        try
        {
            using var relSearcher = new ManagementObjectSearcher(
                @"\\.\root\Microsoft\Windows\Storage",
                "SELECT Temperature, PowerOnHours, Wear, ReadErrorsTotal, ReadErrorsCorrected FROM MSFT_StorageReliabilityCounter");

            foreach (ManagementObject rel in relSearcher.Get())
            {
                // Take the first result (primary disk)
                var temp = rel["Temperature"];
                var hours = rel["PowerOnHours"];
                var wear = rel["Wear"];
                var readErr = rel["ReadErrorsTotal"];

                disk = disk with
                {
                    Temperature = temp is uint t ? (int)t : null,
                    PowerOnHours = hours is uint h ? (int)h : null,
                    WearLevel = wear is byte w ? (int)w : null,
                    ReadErrors = readErr is ulong re ? (int)re : null,
                };
                break;
            }
        }
        catch { }

        // If health status still unknown, infer from WMI Status
        if (string.IsNullOrEmpty(disk.HealthStatus) || disk.HealthStatus == "Unknown")
        {
            disk = disk with { HealthStatus = disk.Status == "OK" ? "Healthy" : disk.Status };
        }

        return disk;
    }

    private static string DetectMediaType(string wmiMediaType, string model)
    {
        if (wmiMediaType.Contains("SSD", StringComparison.OrdinalIgnoreCase)) return "SSD";
        if (wmiMediaType.Contains("Fixed", StringComparison.OrdinalIgnoreCase))
        {
            // Guess from model name
            var upper = model.ToUpperInvariant();
            if (upper.Contains("SSD") || upper.Contains("NVME") || upper.Contains("M.2"))
                return "SSD";
            return "HDD";
        }
        return wmiMediaType;
    }

    private Border BuildDriveCard(DiskInfo disk)
    {
        var isHealthy = disk.HealthStatus is "Healthy" or "OK";
        var isWarning = disk.HealthStatus == "Warning";
        var borderColor = isHealthy
            ? Windows.UI.Color.FromArgb(255, 46, 125, 50)
            : isWarning
                ? Windows.UI.Color.FromArgb(255, 230, 81, 0)
                : Windows.UI.Color.FromArgb(255, 198, 40, 40);

        var card = new Border
        {
            Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(24),
            BorderBrush = new SolidColorBrush(borderColor),
            BorderThickness = new Thickness(0, 0, 0, 3)
        };

        var stack = new StackPanel { Spacing = 16 };

        // Header: model + health badge
        var header = new Grid { ColumnSpacing = 12 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new FontIcon { Glyph = "\uEDA2", FontSize = 28, VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(borderColor) };
        Grid.SetColumn(icon, 0); header.Children.Add(icon);

        var titleStack = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        titleStack.Children.Add(new TextBlock { Text = disk.Model, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 16 });
        var sizeGb = disk.SizeBytes / (1024.0 * 1024 * 1024);
        titleStack.Children.Add(new TextBlock
        {
            Text = $"{disk.MediaType}  •  {sizeGb:F0} GB  •  {disk.InterfaceType}  •  {disk.Partitions} partition(s)",
            FontSize = 12, Opacity = 0.6
        });
        Grid.SetColumn(titleStack, 1); header.Children.Add(titleStack);

        // Health badge
        var badge = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, borderColor.R, borderColor.G, borderColor.B)),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 6, 12, 6), VerticalAlignment = VerticalAlignment.Center
        };
        badge.Child = new TextBlock
        {
            Text = disk.HealthStatus.ToUpper(), FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new SolidColorBrush(borderColor)
        };
        Grid.SetColumn(badge, 2); header.Children.Add(badge);
        stack.Children.Add(header);

        // Stats grid
        var statsGrid = new Grid { ColumnSpacing = 24 };
        statsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        statsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        statsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        statsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        statsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        AddStatCell(statsGrid, 0, "Temperature", disk.Temperature.HasValue ? $"{disk.Temperature}°C" : "N/A",
            disk.Temperature > 50 ? "#E65100" : "#2E7D32");
        AddStatCell(statsGrid, 1, "Power-On Hours", disk.PowerOnHours.HasValue ? FormatHours(disk.PowerOnHours.Value) : "N/A", null);
        AddStatCell(statsGrid, 2, "Wear Level", disk.WearLevel.HasValue ? $"{100 - disk.WearLevel}% remaining" : "N/A",
            disk.WearLevel > 80 ? "#C62828" : disk.WearLevel > 50 ? "#E65100" : "#2E7D32");
        AddStatCell(statsGrid, 3, "Read Errors", disk.ReadErrors.HasValue ? disk.ReadErrors.Value.ToString() : "N/A",
            disk.ReadErrors > 0 ? "#E65100" : "#2E7D32");
        AddStatCell(statsGrid, 4, "Firmware", disk.FirmwareRevision, null);

        stack.Children.Add(statsGrid);

        // Wear bar (for SSDs)
        if (disk.WearLevel.HasValue)
        {
            var remaining = 100 - disk.WearLevel.Value;
            var wearColor = remaining > 70 ? Windows.UI.Color.FromArgb(255, 46, 125, 50)
                : remaining > 30 ? Windows.UI.Color.FromArgb(255, 230, 81, 0)
                : Windows.UI.Color.FromArgb(255, 198, 40, 40);

            var wearStack = new StackPanel { Spacing = 4 };
            wearStack.Children.Add(new TextBlock { Text = $"SSD Lifespan: {remaining}% remaining", FontSize = 12, Opacity = 0.7 });
            wearStack.Children.Add(new ProgressBar
            {
                Minimum = 0, Maximum = 100, Value = remaining, Height = 8, CornerRadius = new CornerRadius(4),
                Foreground = new SolidColorBrush(wearColor)
            });
            stack.Children.Add(wearStack);
        }

        // Temperature bar
        if (disk.Temperature.HasValue)
        {
            var temp = disk.Temperature.Value;
            var tempColor = temp > 55 ? Windows.UI.Color.FromArgb(255, 198, 40, 40)
                : temp > 45 ? Windows.UI.Color.FromArgb(255, 230, 81, 0)
                : Windows.UI.Color.FromArgb(255, 46, 125, 50);

            var tempStack = new StackPanel { Spacing = 4 };
            tempStack.Children.Add(new TextBlock { Text = $"Temperature: {temp}°C (safe range: 0-50°C)", FontSize = 12, Opacity = 0.7 });
            tempStack.Children.Add(new ProgressBar
            {
                Minimum = 0, Maximum = 80, Value = temp, Height = 8, CornerRadius = new CornerRadius(4),
                Foreground = new SolidColorBrush(tempColor)
            });
            stack.Children.Add(tempStack);
        }

        // Serial number (subtle)
        if (!string.IsNullOrEmpty(disk.SerialNumber))
        {
            stack.Children.Add(new TextBlock
            {
                Text = $"S/N: {disk.SerialNumber}", FontSize = 10, Opacity = 0.3,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas")
            });
        }

        card.Child = stack;
        return card;
    }

    private static void AddStatCell(Grid grid, int col, string label, string value, string? colorHex)
    {
        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(new TextBlock { Text = label, FontSize = 11, Opacity = 0.5 });
        var valText = new TextBlock { Text = value, FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        if (colorHex is not null)
        {
            var c = ParseColor(colorHex);
            valText.Foreground = new SolidColorBrush(c);
        }
        stack.Children.Add(valText);
        Grid.SetColumn(stack, col);
        grid.Children.Add(stack);
    }

    private static string FormatHours(int hours)
    {
        if (hours < 24) return $"{hours}h";
        var days = hours / 24;
        if (days < 365) return $"{days}d ({hours:N0}h)";
        var years = days / 365.0;
        return $"{years:F1}y ({hours:N0}h)";
    }

    private static Windows.UI.Color ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        return Windows.UI.Color.FromArgb(255,
            byte.Parse(hex[..2], System.Globalization.NumberStyles.HexNumber),
            byte.Parse(hex[2..4], System.Globalization.NumberStyles.HexNumber),
            byte.Parse(hex[4..6], System.Globalization.NumberStyles.HexNumber));
    }

    private void ApplyLocalization()
    {
        if (FindName("PageTitle") is Microsoft.UI.Xaml.Controls.TextBlock title)
            title.Text = S._("disk.title");
        if (FindName("PageSubtitle") is Microsoft.UI.Xaml.Controls.TextBlock subtitle)
            subtitle.Text = S._("disk.subtitle");
    }
}
