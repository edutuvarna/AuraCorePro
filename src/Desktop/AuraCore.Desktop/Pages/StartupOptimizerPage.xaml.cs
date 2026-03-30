using AuraCore.Desktop.Services;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;

namespace AuraCore.Desktop.Pages;

public sealed partial class StartupOptimizerPage : Page
{
    private List<StartupItem> _items = new();

    private sealed record StartupItem
    {
        public string Name { get; init; } = "";
        public string Command { get; init; } = "";
        public string Location { get; init; } = "";  // Registry path
        public string Hive { get; init; } = "";       // HKCU or HKLM
        public bool IsEnabled { get; set; }
        public string Impact { get; init; } = "Unknown";
        public string Publisher { get; init; } = "";
    }

    // Known high-impact startup programs
    private static readonly HashSet<string> HighImpact = new(StringComparer.OrdinalIgnoreCase)
    {
        "OneDrive", "Teams", "Spotify", "Discord", "Steam", "EpicGamesLauncher",
        "GoogleDriveSync", "Dropbox", "AdobeCreativeCloud", "iTunesHelper",
        "Skype", "Slack", "Zoom", "MicrosoftEdgeAutoLaunch", "Opera",
    };

    private static readonly HashSet<string> LowImpact = new(StringComparer.OrdinalIgnoreCase)
    {
        "SecurityHealth", "Windows Defender", "RealTek", "Realtek", "ctfmon",
        "SynTPEnh", "igfxTray",
    };

    public StartupOptimizerPage()
    {
        InitializeComponent();
        ApplyLocalization();
        S.LanguageChanged += () => DispatcherQueue?.TryEnqueue(ApplyLocalization);
    }

    private void ApplyLocalization()
    {
        if (FindName("PageTitle") is TextBlock title) title.Text = S._("startup.title");
        if (FindName("PageSubtitle") is TextBlock subtitle) subtitle.Text = S._("startup.subtitle");
    }

    private async void ScanBtn_Click(object sender, RoutedEventArgs e)
    {
        ScanBtn.IsEnabled = false;
        Progress.IsActive = true; Progress.Visibility = Visibility.Visible;
        StatusText.Text = S._("startup.scanning");
        StartupList.Children.Clear();

        try
        {
            _items = await Task.Run(ScanStartupItems);

            if (_items.Count == 0)
            {
                StatusText.Text = S._("startup.noPrograms");
                return;
            }

            var enabled = _items.Count(i => i.IsEnabled);
            var disabled = _items.Count(i => !i.IsEnabled);
            var highCount = _items.Count(i => i.Impact == "High" && i.IsEnabled);

            TotalText.Text = _items.Count.ToString();
            EnabledText.Text = enabled.ToString();
            DisabledText.Text = disabled.ToString();
            ImpactText.Text = highCount > 0 ? $"{highCount} High" : "Low";
            SummaryCard.Visibility = Visibility.Visible;

            // Header
            var header = CreateRow("Program", "Command", "Source", "Impact", null, isHeader: true);
            StartupList.Children.Add(header);

            // Sort: high impact first, then by name
            var sorted = _items.OrderByDescending(i => i.Impact == "High")
                .ThenByDescending(i => i.Impact == "Medium")
                .ThenBy(i => i.Name).ToList();

            foreach (var item in sorted)
            {
                StartupList.Children.Add(CreateRow(item.Name, item.Command, item.Hive, item.Impact, item));
            }

            StatusText.Text = $"Found {_items.Count} startup programs — {enabled} enabled, {disabled} disabled";
        }
        catch (Exception ex) { StatusText.Text = S._("common.errorPrefix") + ex.Message; }
        finally
        {
            ScanBtn.IsEnabled = true;
            Progress.IsActive = false; Progress.Visibility = Visibility.Collapsed;
        }
    }

    private static List<StartupItem> ScanStartupItems()
    {
        var items = new List<StartupItem>();

        // HKCU Run
        ScanRegistryKey(items, Registry.CurrentUser,
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "HKCU", true);
        // HKLM Run
        ScanRegistryKey(items, Registry.LocalMachine,
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "HKLM", true);
        // HKCU RunOnce
        ScanRegistryKey(items, Registry.CurrentUser,
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce", "HKCU (Once)", true);

        // Disabled items (Windows stores them in a different location)
        ScanDisabledItems(items);

        // Deduplicate by name
        return items.GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First()).ToList();
    }

    private static void ScanRegistryKey(List<StartupItem> items, RegistryKey root,
        string path, string hive, bool isEnabled)
    {
        try
        {
            using var key = root.OpenSubKey(path);
            if (key is null) return;

            foreach (var name in key.GetValueNames())
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                var cmd = key.GetValue(name)?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(cmd)) continue;

                var impact = ClassifyImpact(name, cmd);
                var publisher = ExtractPublisher(cmd);

                items.Add(new StartupItem
                {
                    Name = name,
                    Command = cmd.Length > 120 ? cmd[..120] + "..." : cmd,
                    Location = path,
                    Hive = hive,
                    IsEnabled = isEnabled,
                    Impact = impact,
                    Publisher = publisher
                });
            }
        }
        catch { }
    }

    private static void ScanDisabledItems(List<StartupItem> items)
    {
        try
        {
            // Task Manager stores disabled startup items in:
            // HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run");
            if (key is null) return;

            foreach (var name in key.GetValueNames())
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                var data = key.GetValue(name) as byte[];
                if (data is null || data.Length < 4) continue;

                // First 4 bytes: 02 = enabled, 03 = disabled, 06 = disabled by user
                var isDisabled = data[0] == 0x03 || data[0] == 0x06;

                // Check if this item already exists in our list
                var existing = items.FirstOrDefault(i =>
                    string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));

                if (existing is not null && isDisabled)
                {
                    // Mark as disabled
                    items.Remove(existing);
                    items.Add(existing with { IsEnabled = false });
                }
            }
        }
        catch { }
    }

    private static string ClassifyImpact(string name, string cmd)
    {
        if (HighImpact.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase) ||
            cmd.Contains(h, StringComparison.OrdinalIgnoreCase)))
            return "High";
        if (LowImpact.Any(l => name.Contains(l, StringComparison.OrdinalIgnoreCase) ||
            cmd.Contains(l, StringComparison.OrdinalIgnoreCase)))
            return "Low";
        return "Medium";
    }

    private static string ExtractPublisher(string cmd)
    {
        if (cmd.Contains("Microsoft", StringComparison.OrdinalIgnoreCase)) return "Microsoft";
        if (cmd.Contains("Google", StringComparison.OrdinalIgnoreCase)) return "Google";
        if (cmd.Contains("Adobe", StringComparison.OrdinalIgnoreCase)) return "Adobe";
        if (cmd.Contains("Apple", StringComparison.OrdinalIgnoreCase)) return "Apple";
        return "";
    }

    private Grid CreateRow(string name, string cmd, string source, string impact, StartupItem? item, bool isHeader = false)
    {
        var grid = new Grid
        {
            ColumnSpacing = 8,
            Padding = new Thickness(12, isHeader ? 8 : 6, 12, isHeader ? 8 : 6),
            Background = isHeader
                ? (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"]
                : null,
            CornerRadius = isHeader ? new CornerRadius(6) : new CornerRadius(0),
            BorderBrush = isHeader ? null : (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = isHeader ? new Thickness(0) : new Thickness(0, 0, 0, 0.5)
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });          // Toggle/header
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }); // Name+cmd
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) }); // Source
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });          // Impact badge

        var wt = isHeader ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        var sz = isHeader ? 11 : 12;

        if (!isHeader && item is not null)
        {
            // Toggle switch
            var toggle = new ToggleSwitch
            {
                IsOn = item.IsEnabled,
                OnContent = "", OffContent = "",
                MinWidth = 50, VerticalAlignment = VerticalAlignment.Center
            };
            var capturedItem = item;
            toggle.Toggled += async (s, ev) =>
            {
                var newState = toggle.IsOn;
                var success = await Task.Run(() => ToggleStartupItem(capturedItem, newState));
                if (success)
                {
                    capturedItem.IsEnabled = newState;
                    StatusText.Text = $"{capturedItem.Name} {(newState ? "enabled" : "disabled")}";
                    // Update counters
                    var enabled = _items.Count(i => i.IsEnabled);
                    EnabledText.Text = enabled.ToString();
                    DisabledText.Text = (_items.Count - enabled).ToString();
                }
                else
                {
                    toggle.IsOn = !newState; // Revert
                    StatusText.Text = $"Failed to {(newState ? "enable" : "disable")} {capturedItem.Name}. Run as admin.";
                }
            };
            Grid.SetColumn(toggle, 0); grid.Children.Add(toggle);
        }
        else
        {
            var h = new TextBlock { Text = "On/Off", FontSize = sz, FontWeight = wt, VerticalAlignment = VerticalAlignment.Center, MinWidth = 50 };
            Grid.SetColumn(h, 0); grid.Children.Add(h);
        }

        // Name + command
        var nameStack = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        nameStack.Children.Add(new TextBlock { Text = name, FontSize = isHeader ? sz : 13, FontWeight = wt });
        if (!isHeader)
        {
            nameStack.Children.Add(new TextBlock
            {
                Text = cmd, FontSize = 10, Opacity = 0.35,
                TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 350
            });
        }
        Grid.SetColumn(nameStack, 1); grid.Children.Add(nameStack);

        // Source
        var srcText = new TextBlock { Text = source, FontSize = sz, FontWeight = wt, Opacity = 0.6, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(srcText, 2); grid.Children.Add(srcText);

        // Impact badge
        if (!isHeader && item is not null)
        {
            var impactColor = impact switch
            {
                "High" => Windows.UI.Color.FromArgb(255, 198, 40, 40),
                "Medium" => Windows.UI.Color.FromArgb(255, 230, 81, 0),
                _ => Windows.UI.Color.FromArgb(255, 46, 125, 50)
            };
            var badge = new Border
            {
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, impactColor.R, impactColor.G, impactColor.B)),
                CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 3, 8, 3), VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock
            {
                Text = impact.ToUpper(), FontSize = 10,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(impactColor)
            };
            Grid.SetColumn(badge, 3); grid.Children.Add(badge);
        }
        else
        {
            var impH = new TextBlock { Text = "Impact", FontSize = sz, FontWeight = wt, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(impH, 3); grid.Children.Add(impH);
        }

        return grid;
    }

    private static bool ToggleStartupItem(StartupItem item, bool enable)
    {
        try
        {
            // Method: Toggle via StartupApproved\Run registry
            var approvedPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
            using var key = Registry.CurrentUser.OpenSubKey(approvedPath, writable: true);
            if (key is null) return false;

            var data = key.GetValue(item.Name) as byte[];
            if (data is null || data.Length < 12)
            {
                // Create new entry: 12 bytes, first byte = 02 (enabled) or 03 (disabled)
                data = new byte[12];
            }

            data[0] = enable ? (byte)0x02 : (byte)0x03;
            key.SetValue(item.Name, data, RegistryValueKind.Binary);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ── BOOT TIME BENCHMARK ───────────────────────────────────

    private async void BenchmarkBtn_Click(object sender, RoutedEventArgs e)
    {
        BenchmarkBtn.IsEnabled = false;
        Progress.IsActive = true; Progress.Visibility = Visibility.Visible;
        StatusText.Text = S._("startup.benchmarking");

        try
        {
            var (bootTimes, lastBootMs, uptimeMs) = await Task.Run(GetBootData);

            // System uptime
            var uptime = TimeSpan.FromMilliseconds(uptimeMs);
            UptimeText.Text = uptime.TotalHours >= 1
                ? $"{uptime.Hours}h {uptime.Minutes}m"
                : $"{uptime.Minutes}m {uptime.Seconds}s";

            // Last boot time
            if (lastBootMs > 0)
            {
                var bootSec = lastBootMs / 1000.0;
                LastBootText.Text = $"{bootSec:F1}s";
            }
            else
            {
                LastBootText.Text = "N/A";
            }

            // Count high-impact enabled startup items
            var highEnabled = _items.Count(i => i.IsEnabled && i.Impact == "High");
            var medEnabled = _items.Count(i => i.IsEnabled && i.Impact == "Medium");
            var totalEnabled = _items.Count(i => i.IsEnabled);

            // Estimate startup load
            // High impact: ~3-5s each, Medium: ~1-2s each, Low: ~0.3s each
            var estimatedLoadMs = (highEnabled * 4000) + (medEnabled * 1500) + ((totalEnabled - highEnabled - medEnabled) * 300);
            var estimatedLoadSec = estimatedLoadMs / 1000.0;
            StartupLoadText.Text = $"~{estimatedLoadSec:F0}s";

            // Estimated savings if all High items disabled
            var potentialSavingsMs = (highEnabled * 4000) + (medEnabled * 1500);
            var savingsSec = potentialSavingsMs / 1000.0;
            SavingsText.Text = savingsSec > 0 ? $"-{savingsSec:F0}s" : "0s";

            // Before/After bars
            var currentBootSec = lastBootMs > 0 ? lastBootMs / 1000.0 : estimatedLoadSec + 15; // estimate if no data
            var afterBootSec = Math.Max(currentBootSec - savingsSec, 8); // minimum 8s boot

            BeforeBar.Maximum = currentBootSec;
            BeforeBar.Value = currentBootSec;
            BeforeTimeText.Text = $"{currentBootSec:F0}s";

            AfterBar.Maximum = currentBootSec;
            AfterBar.Value = afterBootSec;
            AfterTimeText.Text = $"{afterBootSec:F0}s";

            // Boot history
            if (bootTimes.Count > 0)
            {
                BootHistoryHeader.Text = $"Recent Boot Times ({bootTimes.Count} entries from Event Log)";
                BootHistoryList.Children.Clear();

                foreach (var (timestamp, ms) in bootTimes.Take(8))
                {
                    var sec = ms / 1000.0;
                    var isGood = sec < 30;
                    var row = new Grid { ColumnSpacing = 8, Padding = new Thickness(4, 2, 4, 2) };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    var dateText = new TextBlock { Text = timestamp.ToString("MMM dd, HH:mm"), FontSize = 11, Opacity = 0.5, VerticalAlignment = VerticalAlignment.Center };
                    Grid.SetColumn(dateText, 0); row.Children.Add(dateText);

                    var timeText = new TextBlock
                    {
                        Text = $"{sec:F1}s", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(isGood
                            ? Windows.UI.Color.FromArgb(255, 16, 185, 129)
                            : Windows.UI.Color.FromArgb(255, 239, 68, 68)),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(timeText, 1); row.Children.Add(timeText);

                    var bar = new ProgressBar
                    {
                        Minimum = 0, Maximum = 120, Value = Math.Min(sec, 120),
                        Height = 4, CornerRadius = new CornerRadius(2), VerticalAlignment = VerticalAlignment.Center,
                        Foreground = new SolidColorBrush(isGood
                            ? Windows.UI.Color.FromArgb(255, 16, 185, 129)
                            : Windows.UI.Color.FromArgb(255, 239, 68, 68))
                    };
                    Grid.SetColumn(bar, 2); row.Children.Add(bar);

                    BootHistoryList.Children.Add(row);
                }
            }
            else
            {
                BootHistoryHeader.Text = S._("startup.noHistory");
            }

            BenchmarkCard.Visibility = Visibility.Visible;
            StatusText.Text = highEnabled > 0
                ? $"You could save ~{savingsSec:F0}s by disabling {highEnabled} high-impact startup programs"
                : "Your startup looks optimized!";
        }
        catch (Exception ex) { StatusText.Text = S._("common.errorPrefix") + ex.Message; }
        finally
        {
            BenchmarkBtn.IsEnabled = true;
            Progress.IsActive = false; Progress.Visibility = Visibility.Collapsed;
        }
    }

    private static (List<(DateTimeOffset timestamp, long bootMs)> bootTimes, long lastBootMs, long uptimeMs) GetBootData()
    {
        var bootTimes = new List<(DateTimeOffset, long)>();
        long lastBootMs = 0;
        long uptimeMs = Environment.TickCount64;

        try
        {
            // Read from Windows Diagnostics-Performance event log (Event ID 100 = Boot Duration)
            var query = new EventLogQuery(
                "Microsoft-Windows-Diagnostics-Performance/Operational",
                PathType.LogName,
                "*[System[EventID=100]]");

            using var reader = new EventLogReader(query);
            EventRecord? entry;
            while ((entry = reader.ReadEvent()) is not null)
            {
                try
                {
                    var ts = entry.TimeCreated ?? DateTime.Now;
                    // Property index 1 = BootTime in ms
                    var bootTimeMs = 0L;
                    if (entry.Properties.Count > 1)
                    {
                        bootTimeMs = Convert.ToInt64(entry.Properties[1].Value);
                    }
                    if (bootTimeMs > 0)
                    {
                        bootTimes.Add((new DateTimeOffset(ts), bootTimeMs));
                    }
                }
                catch { }
                finally { entry.Dispose(); }
            }

            // Sort newest first
            bootTimes = bootTimes.OrderByDescending(b => b.Item1).ToList();
            if (bootTimes.Count > 0) lastBootMs = bootTimes[0].Item2;
        }
        catch
        {
            // Event log not accessible — try alternative: last boot from WMI
            try
            {
                var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT LastBootUpTime FROM Win32_OperatingSystem");
                foreach (var obj in searcher.Get())
                {
                    var bootStr = obj["LastBootUpTime"]?.ToString();
                    if (!string.IsNullOrEmpty(bootStr))
                    {
                        var bootTime = System.Management.ManagementDateTimeConverter.ToDateTime(bootStr);
                        var sinceBootMs = (long)(DateTime.Now - bootTime).TotalMilliseconds;
                        // We can't get actual boot duration from WMI, just uptime
                        uptimeMs = sinceBootMs;
                    }
                }
            }
            catch { }
        }

        return (bootTimes, lastBootMs, uptimeMs);
    }
}
