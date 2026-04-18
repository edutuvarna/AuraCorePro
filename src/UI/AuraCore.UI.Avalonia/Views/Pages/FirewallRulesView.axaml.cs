using System.Diagnostics;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class FirewallRulesView : UserControl
{
    public FirewallRulesView()
    {
        InitializeComponent();
        Loaded += (s, e) => ApplyLocalization();
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
        Unloaded += (s, e) =>
            LocalizationService.LanguageChanged -= () =>
                global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
    }

    private async void Scan_Click(object? sender, RoutedEventArgs e)
    {
        if (!OperatingSystem.IsWindows()) { _isShowingHint = false; SubText.Text = LocalizationService._("common.windowsOnly"); return; }
        ScanBtn.IsEnabled = false;
        _isShowingHint = false;
        SubText.Text = LocalizationService._("firewall.scanning");

        try
        {
            var rules = await Task.Run(() =>
            {
                var list = new List<(string Name, string Dir, string Action, string Enabled, string Profile)>();
                try
                {
                    var psi = new ProcessStartInfo("netsh", "advfirewall firewall show rule name=all")
                    {
                        RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    if (proc == null) return list;
                    var output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(15000);

                    string? name = null, dir = null, action = null, enabled = null, profile = null;
                    foreach (var line in output.Split('\n'))
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("Rule Name:", StringComparison.OrdinalIgnoreCase))
                            name = trimmed[10..].Trim();
                        else if (trimmed.StartsWith("Direction:", StringComparison.OrdinalIgnoreCase))
                            dir = trimmed[10..].Trim();
                        else if (trimmed.StartsWith("Action:", StringComparison.OrdinalIgnoreCase))
                            action = trimmed[7..].Trim();
                        else if (trimmed.StartsWith("Enabled:", StringComparison.OrdinalIgnoreCase))
                            enabled = trimmed[8..].Trim();
                        else if (trimmed.StartsWith("Profiles:", StringComparison.OrdinalIgnoreCase))
                            profile = trimmed[9..].Trim();
                        else if (string.IsNullOrWhiteSpace(trimmed) && name != null)
                        {
                            list.Add((name, dir ?? "?", action ?? "?", enabled ?? "?", profile ?? "?"));
                            name = dir = action = enabled = profile = null;
                        }
                    }
                    if (name != null) list.Add((name, dir ?? "?", action ?? "?", enabled ?? "?", profile ?? "?"));
                }
                catch { }
                return list;
            });

            TotalRules.Text = rules.Count.ToString();
            EnabledRules.Text = rules.Count(r => r.Enabled.Contains("Yes", StringComparison.OrdinalIgnoreCase)).ToString();
            BlockedRules.Text = rules.Count(r => r.Action.Contains("Block", StringComparison.OrdinalIgnoreCase)).ToString();
            SubText.Text = string.Format(LocalizationService._("firewall.foundRules"), rules.Count);

            var items = rules.Take(200).Select(r =>
            {
                var isBlock = r.Action.Contains("Block", StringComparison.OrdinalIgnoreCase);
                var actionColor = isBlock ? "#EF4444" : "#22C55E";
                var dirIcon = r.Dir.Contains("In", StringComparison.OrdinalIgnoreCase) ? "\u2B07" : "\u2B06";

                return new Border
                {
                    CornerRadius = new global::Avalonia.CornerRadius(6),
                    Background = new SolidColorBrush(Color.Parse("#12FFFFFF")),
                    Padding = new global::Avalonia.Thickness(12, 8),
                    Margin = new global::Avalonia.Thickness(0, 0, 0, 4),
                    Child = new Grid
                    {
                        ColumnDefinitions = global::Avalonia.Controls.ColumnDefinitions.Parse("Auto,*,Auto,Auto"),
                        Children =
                        {
                            new TextBlock { Text = dirIcon, FontSize = 14, Margin = new global::Avalonia.Thickness(0,0,8,0),
                                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center },
                            new StackPanel { [Grid.ColumnProperty] = 1, Children = {
                                new TextBlock { Text = r.Name, FontSize = 12, FontWeight = FontWeight.SemiBold,
                                    Foreground = new SolidColorBrush(Color.Parse("#E8E8F0")) },
                                new TextBlock { Text = $"{r.Dir} | {r.Profile}", FontSize = 10,
                                    Foreground = new SolidColorBrush(Color.Parse("#8888A0")) }
                            }},
                            new Border { [Grid.ColumnProperty] = 2, CornerRadius = new global::Avalonia.CornerRadius(4),
                                Background = new SolidColorBrush(Color.Parse($"#20{actionColor[1..]}")),
                                Padding = new global::Avalonia.Thickness(8,3), Margin = new global::Avalonia.Thickness(8,0),
                                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                                Child = new TextBlock { Text = r.Action, FontSize = 10, FontWeight = FontWeight.SemiBold,
                                    Foreground = new SolidColorBrush(Color.Parse(actionColor)) }},
                        }
                    }
                };
            }).ToList();
            RulesList.ItemsSource = items;
        }
        catch (Exception ex) { SubText.Text = $"{LocalizationService._("common.error")}: {ex.Message}"; }
        finally { ScanBtn.IsEnabled = true; }
    }

    private void ApplyLocalization()
    {
        var L = LocalizationService._;
        PageTitle.Text      = L("nav.firewallRules");
        ModuleHdr.Title     = L("firewall.title");
        ModuleHdr.Subtitle  = L("firewall.subtitle");
        ScanRulesLabel.Text = L("firewall.action.scan");
        SearchBox.Watermark = L("firewall.searchWatermark");
        StatTotal.Label     = L("firewall.stat.total");
        StatEnabled.Label   = L("firewall.stat.enabled");
        StatBlocked.Label   = L("firewall.stat.blocked");
        // Only reset hint text when idle (not showing a live scan result)
        if (string.IsNullOrEmpty(SubText.Text) || _isShowingHint)
        {
            SubText.Text = L("firewall.hint");
            _isShowingHint = true;
        }
    }

    private bool _isShowingHint = true;
}
