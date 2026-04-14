using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class LaunchAgentManagerView : UserControl
{
    public LaunchAgentManagerView()
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
        if (!OperatingSystem.IsMacOS()) { SubText.Text = "macOS only"; return; }
        SubText.Text = "Scanning LaunchAgents/Daemons...";

        var items = await Task.Run(() =>
        {
            var list = new List<(string Name, string Path, string Source)>();
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var dirs = new[]
            {
                (Path.Combine(home, "Library/LaunchAgents"), "User Agent"),
                ("/Library/LaunchAgents", "System Agent"),
                ("/Library/LaunchDaemons", "Daemon"),
            };
            foreach (var (dir, source) in dirs)
            {
                if (!Directory.Exists(dir)) continue;
                try
                {
                    foreach (var f in Directory.GetFiles(dir, "*.plist"))
                        list.Add((System.IO.Path.GetFileNameWithoutExtension(f), f, source));
                }
                catch { }
            }
            return list;
        });

        UserAgents.Text = items.Count(i => i.Source == "User Agent").ToString();
        SystemAgents.Text = items.Count(i => i.Source == "System Agent").ToString();
        DaemonCount.Text = items.Count(i => i.Source == "Daemon").ToString();
        SubText.Text = $"Found {items.Count} launch items";

        AgentList.ItemsSource = items.Select(i =>
        {
            var color = i.Source switch { "User Agent" => "#22C55E", "Daemon" => "#F59E0B", _ => "#0080FF" };
            return new Border
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
                        new StackPanel { Children = {
                            new TextBlock { Text = i.Name, FontSize = 12, FontWeight = FontWeight.SemiBold,
                                Foreground = new SolidColorBrush(Color.Parse("#E8E8F0")) },
                            new TextBlock { Text = i.Path, FontSize = 9, Foreground = new SolidColorBrush(Color.Parse("#555570")) }
                        }},
                        new Border { [Grid.ColumnProperty] = 1, CornerRadius = new global::Avalonia.CornerRadius(4),
                            Background = new SolidColorBrush(Color.Parse($"#20{color[1..]}")),
                            Padding = new global::Avalonia.Thickness(8, 3),
                            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                            Child = new TextBlock { Text = i.Source, FontSize = 10, FontWeight = FontWeight.SemiBold,
                                Foreground = new SolidColorBrush(Color.Parse(color)) }}
                    }
                }
            };
        }).ToList();
    }

    private void ApplyLocalization() { PageTitle.Text = LocalizationService._("nav.launchAgentManager"); }
}
