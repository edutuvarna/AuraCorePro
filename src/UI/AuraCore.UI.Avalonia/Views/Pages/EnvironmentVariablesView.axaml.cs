using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class EnvironmentVariablesView : UserControl
{
    public EnvironmentVariablesView()
    {
        InitializeComponent();
        Loaded += (s, e) => { LoadVariables(); ApplyLocalization(); };
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
        Unloaded += (s, e) =>
            LocalizationService.LanguageChanged -= () =>
                global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
    }

    private void LoadVariables()
    {
        // Load PATH entries
        var path = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
        var entries = path.Split(';', StringSplitOptions.RemoveEmptyEntries);
        PathCount.Text = string.Format(LocalizationService._("envVars.path.entryCount"), entries.Length);

        PathList.ItemsSource = entries.Select(e => new Border
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
                    new TextBlock { Text = e, FontSize = 12, Foreground = new SolidColorBrush(Color.Parse("#E8E8F0")),
                        VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center },
                }
            }
        }).ToList();

        // Load all user env vars
        var vars = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.User);
        var items = new List<Border>();
        foreach (System.Collections.DictionaryEntry kv in vars)
        {
            var key = kv.Key?.ToString() ?? "";
            var val = kv.Value?.ToString() ?? "";
            if (key.Equals("PATH", StringComparison.OrdinalIgnoreCase)) continue;

            items.Add(new Border
            {
                CornerRadius = new global::Avalonia.CornerRadius(6),
                Background = new SolidColorBrush(Color.Parse("#12FFFFFF")),
                Padding = new global::Avalonia.Thickness(12, 8),
                Margin = new global::Avalonia.Thickness(0, 0, 0, 4),
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = key, FontSize = 12, FontWeight = FontWeight.SemiBold,
                            Foreground = new SolidColorBrush(Color.Parse("#00D4AA")) },
                        new TextBlock { Text = val, FontSize = 11, Foreground = new SolidColorBrush(Color.Parse("#8888A0")),
                            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap }
                    }
                }
            });
        }
        VarList.ItemsSource = items;
    }

    private void AddPath_Click(object? sender, RoutedEventArgs e)
    {
        var newPath = NewPathBox.Text?.Trim();
        if (string.IsNullOrEmpty(newPath)) return;

        try
        {
            var current = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
            if (!current.Contains(newPath, StringComparison.OrdinalIgnoreCase))
            {
                var updated = string.IsNullOrEmpty(current) ? newPath : current + ";" + newPath;
                Environment.SetEnvironmentVariable("PATH", updated, EnvironmentVariableTarget.User);
                NewPathBox.Text = "";
                LoadVariables();
                NotificationService.Instance.Post("Environment Variables", $"Added to PATH: {newPath}", NotificationType.Success);
            }
        }
        catch (Exception ex)
        {
            NotificationService.Instance.Post("Environment Variables", $"Error: {ex.Message}", NotificationType.Error);
        }
    }

    private void Refresh_Click(object? sender, RoutedEventArgs e) => LoadVariables();

    private void ApplyLocalization()
    {
        PageTitle.Text = LocalizationService._("nav.environmentVariables");
        var L = LocalizationService._;
        if (this.FindControl<global::AuraCore.UI.Avalonia.Views.Controls.ModuleHeader>("Header") is { } h)
        {
            h.Title = L("envVars.title");
            h.Subtitle = L("envVars.subtitle");
        }
        PathSectionLabel.Text = L("envVars.path.section");
        NewPathBox.Watermark = L("envVars.path.placeholder");
        AddPathBtn.Content = L("envVars.action.add");
        UserVarsLabel.Text = L("envVars.section.userVars");
        RefreshBtn.Content = L("envVars.action.refresh");
        SearchBox.Watermark = L("envVars.search.placeholder");
    }
}
