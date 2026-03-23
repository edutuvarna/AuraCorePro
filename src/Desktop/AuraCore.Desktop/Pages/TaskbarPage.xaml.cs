using AuraCore.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Desktop.Helpers;
using AuraCore.Module.TaskbarTweaks;
using AuraCore.Module.TaskbarTweaks.Models;

namespace AuraCore.Desktop.Pages;

public sealed partial class TaskbarPage : Page
{
    private TaskbarTweaksModule? _module;
    private readonly Dictionary<string, bool> _selections = new();

    public TaskbarPage()
    {
        InitializeComponent();
        ApplyLocalization();
        Services.S.LanguageChanged += () => DispatcherQueue?.TryEnqueue(ApplyLocalization);
        var modules = App.Current.Services.GetServices<IOptimizationModule>();
        _module = modules.FirstOrDefault(m => m.Id == "taskbar-tweaks") as TaskbarTweaksModule;
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

    private async void ApplyBtn_Click(object sender, RoutedEventArgs e)
    {
        var ids = _selections.Where(kv => kv.Value).Select(kv => kv.Key).ToList();
        if (ids.Count == 0) return;

        var dialog = new ContentDialog
        {
            Title = $"Apply {ids.Count} taskbar change(s)?",
            Content = "This modifies registry settings and restarts Explorer. Your desktop will briefly flash.",
            PrimaryButtonText = "Apply", CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot, DefaultButton = ContentDialogButton.Primary
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        Progress.IsActive = true; Progress.Visibility = Visibility.Visible;
        StatusText.Text = "Applying...";

        var plan = new OptimizationPlan("taskbar-tweaks", ids);
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
            title.Text = S._("taskbar.title");
        if (FindName("PageSubtitle") is Microsoft.UI.Xaml.Controls.TextBlock subtitle)
            subtitle.Text = S._("taskbar.subtitle");
    }
}
