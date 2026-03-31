using System.Diagnostics;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class BrewManagerView : UserControl
{
    public BrewManagerView()
    {
        InitializeComponent();
        Loaded += (s, e) => ApplyLocalization();
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
    }

    private async void Scan_Click(object? sender, RoutedEventArgs e)
    {
        if (!OperatingSystem.IsMacOS()) { SubText.Text = "macOS only"; return; }
        SubText.Text = "Scanning Homebrew...";

        var (formulae, casks, outdated) = await Task.Run(() =>
        {
            var f = RunBrew("list --formula").Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
            var c = RunBrew("list --cask").Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
            var o = RunBrew("outdated").Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
            return (f, c, o);
        });

        FormulaCount.Text = formulae.Count.ToString();
        CaskCount.Text = casks.Count.ToString();
        OutdatedCount.Text = outdated.Count.ToString();
        SubText.Text = $"{formulae.Count} formulae, {casks.Count} casks, {outdated.Count} outdated";

        var items = new List<Border>();
        foreach (var pkg in formulae.Take(100))
        {
            var isOutdated = outdated.Any(o => o.StartsWith(pkg, StringComparison.OrdinalIgnoreCase));
            items.Add(MakePkgItem(pkg, "Formula", isOutdated));
        }
        foreach (var pkg in casks.Take(50))
        {
            var isOutdated = outdated.Any(o => o.StartsWith(pkg, StringComparison.OrdinalIgnoreCase));
            items.Add(MakePkgItem(pkg, "Cask", isOutdated));
        }
        PkgList.ItemsSource = items;
    }

    private async void Cleanup_Click(object? sender, RoutedEventArgs e)
    {
        if (!OperatingSystem.IsMacOS()) return;
        SubText.Text = "Running brew cleanup...";
        var result = await Task.Run(() => RunBrew("cleanup --prune=all"));
        SubText.Text = $"Cleanup done: {result.Trim()}";
    }

    private static Border MakePkgItem(string name, string type, bool outdated)
    {
        var typeColor = type == "Cask" ? "#0080FF" : "#22C55E";
        return new Border
        {
            CornerRadius = new global::Avalonia.CornerRadius(6),
            Background = new SolidColorBrush(Color.Parse("#12FFFFFF")),
            Padding = new global::Avalonia.Thickness(12, 6),
            Margin = new global::Avalonia.Thickness(0, 0, 0, 3),
            Child = new Grid
            {
                ColumnDefinitions = global::Avalonia.Controls.ColumnDefinitions.Parse("*,Auto,Auto"),
                Children =
                {
                    new TextBlock { Text = name, FontSize = 12, FontWeight = FontWeight.SemiBold,
                        Foreground = new SolidColorBrush(Color.Parse(outdated ? "#F59E0B" : "#E8E8F0")),
                        VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center },
                    new Border { [Grid.ColumnProperty] = 1, CornerRadius = new global::Avalonia.CornerRadius(4),
                        Background = new SolidColorBrush(Color.Parse($"20{typeColor[1..]}")),
                        Padding = new global::Avalonia.Thickness(6, 2), Margin = new global::Avalonia.Thickness(8, 0),
                        VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                        Child = new TextBlock { Text = type, FontSize = 9, FontWeight = FontWeight.SemiBold,
                            Foreground = new SolidColorBrush(Color.Parse(typeColor)) }},
                }
            }
        };
    }

    private static string RunBrew(string args)
    {
        try
        {
            var psi = new ProcessStartInfo("/opt/homebrew/bin/brew", args)
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            using var proc = Process.Start(psi);
            var output = proc?.StandardOutput.ReadToEnd() ?? "";
            proc?.WaitForExit(30000);
            return output;
        }
        catch { return ""; }
    }

    private void ApplyLocalization() { PageTitle.Text = LocalizationService._("nav.brewManager"); }
}
