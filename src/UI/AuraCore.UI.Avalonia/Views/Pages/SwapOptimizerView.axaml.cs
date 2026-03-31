using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class SwapOptimizerView : UserControl
{
    public SwapOptimizerView()
    {
        InitializeComponent();
        Loaded += (s, e) => { RunScan(); ApplyLocalization(); };
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
    }

    private async void Scan_Click(object? sender, RoutedEventArgs e) => RunScan();

    private async void RunScan()
    {
        if (!OperatingSystem.IsLinux()) { SubText.Text = "Linux only"; return; }
        SubText.Text = "Reading swap info...";

        var (swaps, swappiness, totalKb, usedKb) = await Task.Run(() =>
        {
            var swapList = new List<(string File, string Type, long Size, long Used, int Priority)>();
            long total = 0, used = 0;
            int swapVal = 60;

            // Read /proc/swaps
            try
            {
                var lines = File.ReadAllLines("/proc/swaps");
                for (int i = 1; i < lines.Length; i++) // skip header
                {
                    var parts = lines[i].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 5)
                    {
                        var size = long.TryParse(parts[2], out var s) ? s : 0;
                        var usedVal = long.TryParse(parts[3], out var u) ? u : 0;
                        var prio = int.TryParse(parts[4], out var p) ? p : 0;
                        swapList.Add((parts[0], parts[1], size, usedVal, prio));
                        total += size;
                        used += usedVal;
                    }
                }
            }
            catch { }

            // Read swappiness
            try
            {
                var val = File.ReadAllText("/proc/sys/vm/swappiness").Trim();
                if (int.TryParse(val, out var sv)) swapVal = sv;
            }
            catch { }

            return (swapList, swapVal, total, used);
        });

        SwapTotal.Text = FormatKb(totalKb);
        SwapUsed.Text = FormatKb(usedKb);
        Swappiness.Text = swappiness.ToString();

        SubText.Text = $"{swaps.Count} swap partition(s)/file(s)";

        SwapList.ItemsSource = swaps.Select(s =>
        {
            var usePct = s.Size > 0 ? (double)s.Used / s.Size * 100 : 0;
            var color = usePct > 80 ? "#EF4444" : usePct > 50 ? "#F59E0B" : "#22C55E";

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
                            new TextBlock { Text = s.File, FontSize = 13, FontWeight = FontWeight.SemiBold,
                                Foreground = new SolidColorBrush(Color.Parse("#E8E8F0")) },
                            new TextBlock { Text = $"Type: {s.Type} | Priority: {s.Priority}", FontSize = 10,
                                Foreground = new SolidColorBrush(Color.Parse("#8888A0")) }
                        }},
                        new TextBlock { [Grid.ColumnProperty] = 1, Text = $"{FormatKb(s.Used)}/{FormatKb(s.Size)}",
                            FontSize = 12, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                            Margin = new global::Avalonia.Thickness(12, 0),
                            Foreground = new SolidColorBrush(Color.Parse("#C0C0D0")) },
                        new TextBlock { [Grid.ColumnProperty] = 2, Text = $"{usePct:F0}%",
                            FontSize = 14, FontWeight = FontWeight.Bold,
                            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                            Foreground = new SolidColorBrush(Color.Parse(color)) },
                    }
                }
            };
        }).ToList();

        // Recommendations
        var rec = swappiness switch
        {
            >= 80 => "High swappiness (80+): System aggressively uses swap. Good for servers, may slow desktops. Consider lowering to 10-30 for desktop use.",
            >= 40 => $"Moderate swappiness ({swappiness}): Balanced between RAM and swap usage. Suitable for most workloads.",
            >= 10 => $"Low swappiness ({swappiness}): System prefers RAM over swap. Good for desktops and workstations with enough RAM.",
            _ => $"Very low swappiness ({swappiness}): System almost never swaps. Ensure you have enough RAM or OOM killer may activate."
        };
        if (totalKb == 0)
            rec = "No swap detected! Consider adding a swap file for stability: 'sudo fallocate -l 2G /swapfile && sudo chmod 600 /swapfile && sudo mkswap /swapfile && sudo swapon /swapfile'";
        RecommendationText.Text = rec;
    }

    private static string FormatKb(long kb) => kb switch
    {
        < 1024 => $"{kb} KB",
        < 1024 * 1024 => $"{kb / 1024.0:F1} MB",
        _ => $"{kb / (1024.0 * 1024):F2} GB"
    };

    private void ApplyLocalization()
    {
        PageTitle.Text = LocalizationService._("nav.swapOptimizer");
    }
}
