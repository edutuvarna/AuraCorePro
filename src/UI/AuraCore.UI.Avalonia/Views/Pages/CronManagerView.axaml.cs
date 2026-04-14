using System.Diagnostics;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class CronManagerView : UserControl
{
    public CronManagerView()
    {
        InitializeComponent();
        Loaded += (s, e) => { RunScan(); ApplyLocalization(); };
        LocalizationService.LanguageChanged += OnLanguageChanged;
        Unloaded += OnUnloaded;
    }

    private void OnLanguageChanged() =>
        global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        LocalizationService.LanguageChanged -= OnLanguageChanged;
    }

    private async void Scan_Click(object? sender, RoutedEventArgs e) => RunScan();

    private async void RunScan()
    {
        try
        {
        if (!OperatingSystem.IsLinux()) { SubText.Text = "Linux only"; return; }
        SubText.Text = "Reading crontab...";

        var (userJobs, systemDirs) = await Task.Run(() =>
        {
            var jobs = new List<(string Schedule, string Command, bool IsComment)>();
            var sysDirs = new List<(string Dir, int Count)>();

            // User crontab
            try
            {
                var psi = new ProcessStartInfo("crontab", "-l")
                { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    var output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(5000);
                    foreach (var line in output.Split('\n'))
                    {
                        var trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed)) continue;
                        if (trimmed.StartsWith("#"))
                        {
                            jobs.Add(("", trimmed, true));
                        }
                        else if (trimmed.StartsWith("@"))
                        {
                            // Special schedule entries (@reboot, @daily, etc.)
                            var spaceIdx = trimmed.IndexOf(' ');
                            var schedule = spaceIdx > 0 ? trimmed[..spaceIdx] : trimmed;
                            var command = spaceIdx > 0 ? trimmed[(spaceIdx + 1)..].Trim() : "";
                            jobs.Add((schedule, command, false));
                        }
                        else
                        {
                            var parts = trimmed.Split(new[] { ' ', '\t' }, 6, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 6)
                            {
                                var schedule = string.Join(" ", parts.Take(5));
                                var cmd = parts[5];
                                jobs.Add((schedule, cmd, false));
                            }
                            else
                            {
                                jobs.Add(("", trimmed, false));
                            }
                        }
                    }
                }
            }
            catch { }

            // System cron directories
            var cronDirs = new[] { "/etc/cron.d", "/etc/cron.daily", "/etc/cron.hourly", "/etc/cron.weekly", "/etc/cron.monthly" };
            foreach (var dir in cronDirs)
            {
                if (!Directory.Exists(dir)) continue;
                try
                {
                    var count = Directory.GetFiles(dir).Length;
                    sysDirs.Add((dir, count));
                }
                catch { sysDirs.Add((dir, 0)); }
            }

            return (jobs, sysDirs);
        });

        var activeJobs = userJobs.Count(j => !j.IsComment);
        UserJobCount.Text = activeJobs.ToString();
        SystemJobCount.Text = systemDirs.Sum(d => d.Count).ToString();
        SubText.Text = $"{activeJobs} active cron job(s)";

        // User jobs
        CronList.ItemsSource = userJobs.Select(j =>
        {
            if (j.IsComment)
            {
                return new Border
                {
                    Padding = new global::Avalonia.Thickness(12, 4),
                    Child = new TextBlock { Text = j.Command, FontSize = 11, FontStyle = global::Avalonia.Media.FontStyle.Italic,
                        Foreground = new SolidColorBrush(Color.Parse("#555570")) }
                };
            }

            return new Border
            {
                CornerRadius = new global::Avalonia.CornerRadius(6),
                Background = new SolidColorBrush(Color.Parse("#12FFFFFF")),
                Padding = new global::Avalonia.Thickness(12, 8),
                Margin = new global::Avalonia.Thickness(0, 0, 0, 4),
                Child = new StackPanel
                {
                    Children =
                    {
                        new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Children = {
                            new Border { CornerRadius = new global::Avalonia.CornerRadius(4), Padding = new global::Avalonia.Thickness(6, 2),
                                Background = new SolidColorBrush(Color.Parse("#200080FF")),
                                Child = new TextBlock { Text = j.Schedule, FontSize = 10, FontWeight = FontWeight.SemiBold,
                                    Foreground = new SolidColorBrush(Color.Parse("#0080FF")),
                                    FontFamily = new FontFamily("Courier New, monospace") }},
                            new TextBlock { Text = DescribeSchedule(j.Schedule), FontSize = 10,
                                Foreground = new SolidColorBrush(Color.Parse("#8888A0")),
                                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center }
                        }},
                        new TextBlock { Text = j.Command, FontSize = 12, Foreground = new SolidColorBrush(Color.Parse("#E8E8F0")),
                            FontFamily = new FontFamily("Courier New, monospace"),
                            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap, Margin = new global::Avalonia.Thickness(0, 4, 0, 0) }
                    }
                }
            };
        }).ToList();

        // System cron dirs
        SystemCronList.ItemsSource = systemDirs.Select(d => new Border
        {
            CornerRadius = new global::Avalonia.CornerRadius(6),
            Background = new SolidColorBrush(Color.Parse("#12FFFFFF")),
            Padding = new global::Avalonia.Thickness(12, 8),
            Margin = new global::Avalonia.Thickness(0, 0, 0, 4),
            Child = new Grid
            {
                ColumnDefinitions = global::Avalonia.Controls.ColumnDefinitions.Parse("*,Auto"),
                Children =
                {
                    new TextBlock { Text = d.Dir, FontSize = 12, FontWeight = FontWeight.SemiBold,
                        Foreground = new SolidColorBrush(Color.Parse("#E8E8F0")) },
                    new TextBlock { [Grid.ColumnProperty] = 1, Text = $"{d.Count} script(s)", FontSize = 12,
                        Foreground = new SolidColorBrush(Color.Parse("#00D4AA")) }
                }
            }
        }).ToList();
        }
        catch (Exception ex)
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                SubText.Text = $"Error scanning cron: {ex.Message}");
        }
    }

    private static string DescribeSchedule(string schedule)
    {
        var s = schedule.Trim();
        // Check special schedules first (before splitting)
        if (s.StartsWith("@reboot")) return "At startup";
        if (s.StartsWith("@daily") || s.StartsWith("@midnight")) return "Daily at midnight";
        if (s.StartsWith("@hourly")) return "Every hour";
        if (s.StartsWith("@weekly")) return "Weekly";
        if (s.StartsWith("@monthly")) return "Monthly";
        if (s.StartsWith("@yearly") || s.StartsWith("@annually")) return "Yearly";

        var parts = s.Split(' ');
        if (parts.Length < 5) return "";
        return (parts[0], parts[1], parts[2], parts[3], parts[4]) switch
        {
            ("*/5", "*", "*", "*", "*") => "Every 5 minutes",
            ("*/15", "*", "*", "*", "*") => "Every 15 minutes",
            ("0", "*", "*", "*", "*") => "Every hour",
            ("0", "*/6", "*", "*", "*") => "Every 6 hours",
            ("0", "0", "*", "*", "*") => "Daily at midnight",
            ("0", "0", "*", "*", "0") => "Weekly (Sunday midnight)",
            ("0", "0", "1", "*", "*") => "Monthly (1st at midnight)",
            _ when parts[0] == "*" && parts[1] == "*" => "Every minute",
            _ => $"Custom schedule"
        };
    }

    private void ApplyLocalization()
    {
        PageTitle.Text = LocalizationService._("nav.cronManager");
    }
}
