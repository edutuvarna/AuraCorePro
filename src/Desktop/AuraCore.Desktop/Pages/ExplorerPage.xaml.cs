using AuraCore.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Desktop.Helpers;
using AuraCore.Module.ExplorerTweaks;
using AuraCore.Module.ExplorerTweaks.Models;

namespace AuraCore.Desktop.Pages;

public sealed partial class ExplorerPage : Page
{
    private ExplorerTweaksModule? _module;
    private readonly Dictionary<string, bool> _selections = new();

    public ExplorerPage()
    {
        InitializeComponent();
        ApplyLocalization();
        Services.S.LanguageChanged += () => DispatcherQueue?.TryEnqueue(ApplyLocalization);
        var modules = App.Current.Services.GetServices<IOptimizationModule>();
        _module = modules.FirstOrDefault(m => m.Id == "explorer-tweaks") as ExplorerTweaksModule;
    }

    private async void ScanBtn_Click(object sender, RoutedEventArgs e)
    {
        ScanBtn.IsEnabled = false; ApplyBtn.IsEnabled = false;
        Progress.IsActive = true; Progress.Visibility = Visibility.Visible;
        TweakList.Children.Clear(); ResultCard.Visibility = Visibility.Collapsed;
        _selections.Clear();

        try
        {
            if (_module is null) return;
            await _module.ScanAsync(new ScanOptions());
            var report = _module.LastReport;
            if (report is null) return;

            var grouped = report.Tweaks.GroupBy(t => t.Category).OrderBy(g => g.Key);
            foreach (var group in grouped)
            {
                var section = new StackPanel { Spacing = 6 };
                section.Children.Add(new TextBlock
                {
                    Text = group.Key,
                    Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["BodyStrongTextBlockStyle"],
                    Margin = new Thickness(0, 4, 0, 0)
                });
                foreach (var tweak in group)
                {
                    section.Children.Add(TweakPageHelper.CreateTweakCard(
                        tweak.Name, tweak.Description, tweak.Risk, tweak.IsApplied,
                        tweak.Id, _selections, UpdateApplyBtn));
                }
                TweakList.Children.Add(section);
            }
            StatusText.Text = $"Found {report.Tweaks.Count} settings — {report.AppliedCount} active";
        }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
        finally { ScanBtn.IsEnabled = true; Progress.IsActive = false; Progress.Visibility = Visibility.Collapsed; }
    }

    private void UpdateApplyBtn()
    {
        var count = _selections.Count(kv => kv.Value);
        ApplyBtn.Content = $"Apply Changes ({count})";
        ApplyBtn.IsEnabled = count > 0;
    }

    private void RecommendedBtn_Click(object sender, RoutedEventArgs e)
    {
        // Recommended tweaks for security, privacy, and usability
        var recommended = new HashSet<string>
        {
            "show-extensions",     // Security: see real file types
            "show-hidden",         // Visibility: see all files
            "show-full-path",      // Navigation: know where you are
            "open-to-this-pc",     // Navigation: start at drives
            "disable-recent",      // Privacy: no recent file tracking
            "disable-frequent",    // Privacy: no frequent folder tracking
            "compact-view",        // Appearance: see more files
        };

        // Set selections and update toggle switches in UI
        foreach (var key in _selections.Keys.ToList())
            _selections[key] = recommended.Contains(key);

        // Re-toggle the switches in the UI
        foreach (var child in TweakList.Children)
        {
            if (child is StackPanel section)
            {
                foreach (var card in section.Children)
                {
                    if (card is Border border && border.Child is Grid grid)
                    {
                        var toggle = grid.Children.OfType<ToggleSwitch>().FirstOrDefault();
                        if (toggle?.Tag is string id)
                            toggle.IsOn = recommended.Contains(id);
                    }
                }
            }
        }

        UpdateApplyBtn();
        StatusText.Text = $"Recommended preset applied — {recommended.Count} tweaks selected";
    }

    private async void ApplyBtn_Click(object sender, RoutedEventArgs e)
    {
        var ids = _selections.Where(kv => kv.Value).Select(kv => kv.Key).ToList();
        if (ids.Count == 0) return;

        var dialog = new ContentDialog
        {
            Title = $"Apply {ids.Count} Explorer change(s)?",
            Content = "This modifies registry settings and restarts Explorer. Your desktop will briefly flash.",
            PrimaryButtonText = "Apply", CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot, DefaultButton = ContentDialogButton.Primary
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        Progress.IsActive = true; Progress.Visibility = Visibility.Visible;
        StatusText.Text = "Applying...";

        var plan = new OptimizationPlan("explorer-tweaks", ids);
        var progress = new Progress<TaskProgress>(p => DispatcherQueue.TryEnqueue(() => StatusText.Text = p.StatusText));
        var result = await _module!.OptimizeAsync(plan, progress);

        ResultText.Text = $"Applied {result.ItemsProcessed} change(s) — Explorer restarted. All changes reversible.";
        ResultCard.Visibility = Visibility.Visible;
        StatusText.Text = "Done!";
        Progress.IsActive = false; Progress.Visibility = Visibility.Collapsed;
    }

    private void ApplyLocalization()
    {
        if (FindName("PageTitle") is Microsoft.UI.Xaml.Controls.TextBlock title)
            title.Text = S._("explorer.title");
        if (FindName("PageSubtitle") is Microsoft.UI.Xaml.Controls.TextBlock subtitle)
            subtitle.Text = S._("explorer.subtitle");
    }
}
