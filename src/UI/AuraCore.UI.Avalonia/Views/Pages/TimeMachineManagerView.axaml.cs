using System.Diagnostics;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class TimeMachineManagerView : UserControl
{
    public TimeMachineManagerView()
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
        SubText.Text = "Checking Time Machine...";

        var (status, details, exclusions) = await Task.Run(() =>
        {
            var s = RunCmd("tmutil", "currentphase");
            var d = RunCmd("tmutil", "status");
            var excl = new List<string>();
            try
            {
                var exclOutput = RunCmd("tmutil", "isexcluded /Applications /System /Users");
                foreach (var line in exclOutput.Split('\n'))
                    if (line.Contains("[Excluded]")) excl.Add(line.Trim());
            }
            catch { }

            // Last backup
            var latest = RunCmd("tmutil", "latestbackup");

            return (s.Trim(), $"Latest: {latest.Trim()}\n\nStatus:\n{d.Trim()}", excl);
        });

        TmStatus.Text = string.IsNullOrEmpty(status) ? "Idle" : status;
        LastBackup.Text = details.Contains("Latest:") ? details.Split('\n')[0].Replace("Latest: ", "") : "--";
        DetailsText.Text = details;
        SubText.Text = "Time Machine status loaded";

        ExclusionList.ItemsSource = exclusions.Select(ex => new Border
        {
            CornerRadius = new global::Avalonia.CornerRadius(6),
            Background = new SolidColorBrush(Color.Parse("#12FFFFFF")),
            Padding = new global::Avalonia.Thickness(12, 6),
            Margin = new global::Avalonia.Thickness(0, 0, 0, 3),
            Child = new TextBlock { Text = ex, FontSize = 11, Foreground = new SolidColorBrush(Color.Parse("#F59E0B")) }
        }).ToList();
    }

    private static string RunCmd(string cmd, string args)
    {
        try
        {
            var psi = new ProcessStartInfo(cmd, args)
            { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            using var proc = Process.Start(psi);
            var output = proc?.StandardOutput.ReadToEnd() ?? "";
            proc?.WaitForExit(10000);
            return output;
        }
        catch { return ""; }
    }

    private void ApplyLocalization()
    {
        string L(string k) => LocalizationService._(k);
        PageTitle.Text = L("nav.timeMachineManager");
        TmHeader.Title    = L("nav.timeMachineManager");
        TmHeader.Subtitle = L("timeMachineManager.subtitle");
        LastBackupStat.Label = L("timeMachineManager.stat.lastBackup");
        TmStatusStat.Label   = L("timeMachineManager.stat.status");
        TmRefreshBtn.Content    = L("timeMachineManager.action.refresh");
        TmDetailsHeading.Text   = L("timeMachineManager.heading.details");
        SubText.Text            = L("timeMachineManager.subtext.initial");
        ExclusionsHeading.Text  = L("timeMachineManager.heading.exclusions");
    }
}
