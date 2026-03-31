using System.Diagnostics;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class DefaultsOptimizerView : UserControl
{
    private readonly List<(string Name, string Domain, string Key, string Type, string Value, string Desc)> _tweaks = new()
    {
        ("Show Hidden Files", "com.apple.finder", "AppleShowAllFiles", "bool", "true", "Show hidden files in Finder"),
        ("Show Path Bar", "com.apple.finder", "ShowPathbar", "bool", "true", "Show path bar at bottom of Finder"),
        ("Show Status Bar", "com.apple.finder", "ShowStatusBar", "bool", "true", "Show status bar in Finder"),
        ("Dock Auto-Hide", "com.apple.dock", "autohide", "bool", "true", "Automatically hide the Dock"),
        ("Disable Dock Bounce", "com.apple.dock", "no-bouncing", "bool", "true", "Stop icons bouncing in Dock"),
        ("Fast Dock Animation", "com.apple.dock", "autohide-time-modifier", "float", "0.12", "Speed up Dock show/hide"),
        ("Disable .DS_Store on Network", "com.apple.desktopservices", "DSDontWriteNetworkStores", "bool", "true", "No .DS_Store on network volumes"),
        ("Screenshot Format PNG", "com.apple.screencapture", "type", "string", "png", "Save screenshots as PNG"),
        ("Disable Auto-Correct", "NSGlobalDomain", "NSAutomaticSpellingCorrectionEnabled", "bool", "false", "Disable auto-correct system-wide"),
        ("Expand Save Dialog", "NSGlobalDomain", "NSNavPanelExpandedStateForSaveMode", "bool", "true", "Expand save dialogs by default"),
    };

    public DefaultsOptimizerView()
    {
        InitializeComponent();
        Loaded += (s, e) => { LoadTweaks(); ApplyLocalization(); };
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
    }

    private void LoadTweaks()
    {
        TweakList.ItemsSource = _tweaks.Select(t => new Border
        {
            CornerRadius = new global::Avalonia.CornerRadius(6),
            Background = new SolidColorBrush(Color.Parse("#12FFFFFF")),
            Padding = new global::Avalonia.Thickness(12, 8),
            Margin = new global::Avalonia.Thickness(0, 0, 0, 4),
            Child = new Grid
            {
                ColumnDefinitions = global::Avalonia.Controls.ColumnDefinitions.Parse("Auto,*,Auto"),
                Children =
                {
                    new CheckBox { Tag = t.Name, IsChecked = false, Margin = new global::Avalonia.Thickness(0, 0, 8, 0),
                        VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center },
                    new StackPanel { [Grid.ColumnProperty] = 1, Children = {
                        new TextBlock { Text = t.Name, FontSize = 12, FontWeight = FontWeight.SemiBold,
                            Foreground = new SolidColorBrush(Color.Parse("#E8E8F0")) },
                        new TextBlock { Text = t.Desc, FontSize = 10, Foreground = new SolidColorBrush(Color.Parse("#8888A0")) }
                    }},
                    new TextBlock { [Grid.ColumnProperty] = 2, Text = $"{t.Domain}", FontSize = 9,
                        Foreground = new SolidColorBrush(Color.Parse("#555570")),
                        VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center }
                }
            }
        }).ToList();
    }

    private async void Apply_Click(object? sender, RoutedEventArgs e)
    {
        if (!OperatingSystem.IsMacOS()) { SubText.Text = "macOS only"; return; }
        int applied = 0;
        foreach (var t in _tweaks)
        {
            try
            {
                var psi = new ProcessStartInfo("defaults", $"write {t.Domain} {t.Key} -{t.Type} {t.Value}")
                { UseShellExecute = false, CreateNoWindow = true };
                Process.Start(psi)?.WaitForExit(5000);
                applied++;
            }
            catch { }
        }
        SubText.Text = $"Applied {applied} tweaks. Some may require logout/restart.";
    }

    private async void ReadDefault_Click(object? sender, RoutedEventArgs e)
    {
        if (!OperatingSystem.IsMacOS()) { ReadResult.Text = "macOS only"; return; }
        var domain = DomainBox.Text?.Trim() ?? "";
        var key = KeyBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(domain)) { ReadResult.Text = "Enter a domain"; return; }
        try
        {
            var args = string.IsNullOrEmpty(key) ? $"read {domain}" : $"read {domain} {key}";
            var psi = new ProcessStartInfo("defaults", args)
            { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            using var proc = Process.Start(psi);
            var output = proc?.StandardOutput.ReadToEnd() ?? "";
            var error = proc?.StandardError.ReadToEnd() ?? "";
            proc?.WaitForExit(5000);
            ReadResult.Text = string.IsNullOrEmpty(error) ? output.Trim() : $"Error: {error.Trim()}";
        }
        catch (Exception ex) { ReadResult.Text = $"Error: {ex.Message}"; }
    }

    private void ApplyLocalization() { PageTitle.Text = LocalizationService._("nav.defaultsOptimizer"); }
}
