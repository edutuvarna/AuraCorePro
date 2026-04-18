using System.Diagnostics;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Pages;

public record SystemdServiceItem(string Unit, string LoadState, string ActiveState, string SubState, string Description);

public partial class SystemdManagerView : UserControl
{
    private List<SystemdServiceItem> _allItems = new();

    public SystemdManagerView()
    {
        InitializeComponent();
        Loaded += (s, e) => ApplyLocalization();
        LocalizationService.LanguageChanged += OnLanguageChanged;
        Unloaded += OnUnloaded;
    }

    private void OnLanguageChanged() =>
        global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        LocalizationService.LanguageChanged -= OnLanguageChanged;
    }

    private async void Scan_Click(object? sender, RoutedEventArgs e)
    {
        if (!OperatingSystem.IsLinux()) { SubText.Text = "Linux only - systemctl not available"; return; }
        ScanBtn.IsEnabled = false;
        SubText.Text = "Scanning systemd services...";

        _allItems = await Task.Run(() =>
        {
            var list = new List<SystemdServiceItem>();
            try
            {
                var psi = new ProcessStartInfo("systemctl", "list-units --type=service --all --no-pager --no-legend")
                { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                using var proc = Process.Start(psi);
                if (proc == null) return list;
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(15000);

                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    // Format: UNIT LOAD ACTIVE SUB DESCRIPTION...
                    var parts = line.Trim().Split(new[] { ' ' }, 5, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4)
                    {
                        var unit = parts[0].Replace(".service", "");
                        var load = parts[1];
                        var active = parts[2];
                        var sub = parts[3];
                        var desc = parts.Length > 4 ? parts[4] : "";
                        list.Add(new SystemdServiceItem(unit, load, active, sub, desc));
                    }
                }
            }
            catch { }
            return list;
        });

        TotalSvc.Text = _allItems.Count.ToString();
        RunningSvc.Text = _allItems.Count(s => s.SubState == "running").ToString();
        FailedSvc.Text = _allItems.Count(s => s.ActiveState == "failed").ToString();
        EnabledSvc.Text = _allItems.Count(s => s.ActiveState == "active").ToString();
        SubText.Text = $"Found {_allItems.Count} services";
        ScanBtn.IsEnabled = true;
        ApplyFilter();
    }

    private void Search_Changed(object? sender, TextChangedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        var q = SearchBox.Text?.Trim() ?? "";
        var filtered = string.IsNullOrEmpty(q) ? _allItems
            : _allItems.Where(s => s.Unit.Contains(q, StringComparison.OrdinalIgnoreCase)
                || s.Description.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        SvcList.ItemsSource = filtered.Take(150).Select(s =>
        {
            var (statusColor, statusBg) = s.ActiveState switch
            {
                "active" => ("#22C55E", "#2022C55E"),
                "failed" => ("#EF4444", "#20EF4444"),
                _ => ("#8888A0", "#208888A0")
            };
            return new Border
            {
                CornerRadius = new global::Avalonia.CornerRadius(6),
                Background = new SolidColorBrush(Color.Parse("#12FFFFFF")),
                Padding = new global::Avalonia.Thickness(12, 8),
                Margin = new global::Avalonia.Thickness(0, 0, 0, 4),
                Child = new Grid
                {
                    ColumnDefinitions = global::Avalonia.Controls.ColumnDefinitions.Parse("*,Auto,Auto"),
                    Children =
                    {
                        new StackPanel { Children = {
                            new TextBlock { Text = s.Unit, FontSize = 12, FontWeight = FontWeight.SemiBold,
                                Foreground = new SolidColorBrush(Color.Parse("#E8E8F0")) },
                            new TextBlock { Text = s.Description, FontSize = 10,
                                Foreground = new SolidColorBrush(Color.Parse("#8888A0")),
                                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap }
                        }},
                        new Border { [Grid.ColumnProperty] = 1, CornerRadius = new global::Avalonia.CornerRadius(4),
                            Background = new SolidColorBrush(Color.Parse(statusBg)),
                            Padding = new global::Avalonia.Thickness(8, 3), Margin = new global::Avalonia.Thickness(8, 0),
                            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                            Child = new TextBlock { Text = $"{s.ActiveState} ({s.SubState})", FontSize = 10,
                                FontWeight = FontWeight.SemiBold,
                                Foreground = new SolidColorBrush(Color.Parse(statusColor)) }},
                    }
                }
            };
        }).ToList();
    }

    private void ApplyLocalization()
    {
        string L(string k) => LocalizationService._(k);
        PageTitle.Text = L("nav.systemdManager");
        SvcHeader.Title    = L("nav.systemdManager");
        SvcHeader.Subtitle = L("systemdManager.subtitle");
        TotalSvcStat.Label   = L("systemdManager.stat.total");
        RunningSvcStat.Label = L("systemdManager.stat.running");
        FailedSvcStat.Label  = L("systemdManager.stat.failed");
        EnabledSvcStat.Label = L("systemdManager.stat.enabled");
        SearchBox.Watermark  = L("systemdManager.search.watermark");
        ScanBtn.Content = L("systemdManager.action.scan");
        SubText.Text    = L("systemdManager.subtext.initial");
    }
}
