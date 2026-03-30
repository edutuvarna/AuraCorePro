using AuraCore.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.ContextMenu;
using AuraCore.Module.ContextMenu.Models;

namespace AuraCore.Desktop.Pages;

public sealed partial class ContextMenuPage : Page
{
    private ContextMenuModule? _module;
    private readonly Dictionary<string, bool> _tweakSelections = new();
    private bool _classicMenuChanged;
    private bool _classicMenuTarget;

    public ContextMenuPage()
    {
        InitializeComponent();
        ApplyLocalization();
        Services.S.LanguageChanged += () => DispatcherQueue?.TryEnqueue(ApplyLocalization);
        var modules = App.Current.Services.GetServices<IOptimizationModule>();
        _module = modules.FirstOrDefault(m => m.Id == "context-menu") as ContextMenuModule;
    }

    private async void ScanBtn_Click(object sender, RoutedEventArgs e)
    {
        ScanBtn.IsEnabled = false;
        ApplyBtn.IsEnabled = false;
        Progress.IsActive = true;
        Progress.Visibility = Visibility.Visible;
        StatusText.Text = S._("ctx.scanningSettings");
        TweakList.Children.Clear();
        ResultCard.Visibility = Visibility.Collapsed;
        _tweakSelections.Clear();
        _classicMenuChanged = false;

        try
        {
            if (_module is null) { StatusText.Text = S._("common.moduleUnavailable"); return; }

            await _module.ScanAsync(new ScanOptions());
            var report = _module.LastReport;
            if (report is null) { StatusText.Text = S._("common.noData"); return; }

            // Classic menu toggle
            ClassicMenuToggle.IsOn = report.IsClassicMenuEnabled;
            ClassicMenuStatus.Text = report.IsClassicMenuEnabled ? "Active — using classic menu" : "Inactive — using Win11 menu";
            _classicMenuTarget = report.IsClassicMenuEnabled;
            ClassicMenuToggle.Toggled += (s, ev) =>
            {
                _classicMenuChanged = ClassicMenuToggle.IsOn != report.IsClassicMenuEnabled;
                _classicMenuTarget = ClassicMenuToggle.IsOn;
                UpdateApplyBtn();
            };
            ClassicMenuCard.Visibility = Environment.OSVersion.Version.Build >= 22000 ? Visibility.Visible : Visibility.Collapsed;

            // Group tweaks by category
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
                    _tweakSelections[tweak.Id] = false; // Start with nothing selected

                    var card = new Border
                    {
                        Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(16, 10, 16, 10),
                        BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],
                        BorderThickness = new Thickness(0.5)
                    };

                    var grid = new Grid { ColumnSpacing = 12 };
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var toggle = new ToggleSwitch
                    {
                        IsOn = tweak.IsApplied,
                        OnContent = "",
                        OffContent = "",
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    var capturedId = tweak.Id;
                    var wasApplied = tweak.IsApplied;
                    toggle.Toggled += (s, ev) =>
                    {
                        _tweakSelections[capturedId] = toggle.IsOn != wasApplied;
                        UpdateApplyBtn();
                    };
                    Grid.SetColumn(toggle, 0);
                    grid.Children.Add(toggle);

                    var info = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
                    info.Children.Add(new TextBlock
                    {
                        Text = tweak.Name,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        FontSize = 13
                    });
                    info.Children.Add(new TextBlock { Text = tweak.Description, FontSize = 11, Opacity = 0.6, TextWrapping = TextWrapping.Wrap });
                    Grid.SetColumn(info, 1);
                    grid.Children.Add(info);

                    // Risk badge
                    var riskColor = tweak.Risk switch
                    {
                        "Safe" => Windows.UI.Color.FromArgb(255, 46, 125, 50),
                        "Caution" => Windows.UI.Color.FromArgb(255, 230, 81, 0),
                        _ => Windows.UI.Color.FromArgb(255, 158, 158, 158)
                    };
                    var badge = new Border
                    {
                        Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, riskColor.R, riskColor.G, riskColor.B)),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(6, 3, 6, 3),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    badge.Child = new TextBlock
                    {
                        Text = tweak.Risk.ToUpper(),
                        FontSize = 9,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(riskColor)
                    };
                    Grid.SetColumn(badge, 2);
                    grid.Children.Add(badge);

                    // Status
                    if (tweak.IsApplied)
                    {
                        var statusBadge = new Border
                        {
                            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(25, 33, 150, 243)),
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(6, 3, 6, 3),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        statusBadge.Child = new TextBlock { Text = "ACTIVE", FontSize = 9, Opacity = 0.7 };
                        Grid.SetColumn(statusBadge, 3);
                        grid.Children.Add(statusBadge);
                    }

                    card.Child = grid;
                    section.Children.Add(card);
                }

                TweakList.Children.Add(section);
            }

            StatusText.Text = string.Format(S._("ctx.foundOptions"), report.Tweaks.Count, report.AppliedCount);

            TotalText.Text = report.Tweaks.Count.ToString();
            AppliedText.Text = report.AppliedCount.ToString();
            ClassicMenuStatText.Text = report.IsClassicMenuEnabled ? "Active" : "Inactive";
            ClassicMenuStatText.Foreground = report.IsClassicMenuEnabled
                ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 46, 125, 50))
                : new SolidColorBrush(Windows.UI.Color.FromArgb(160, 128, 128, 128));
            SummaryCard.Visibility = Visibility.Visible;
        }
        catch (Exception ex) { StatusText.Text = S._("common.errorPrefix") + ex.Message; }
        finally
        {
            ScanBtn.IsEnabled = true;
            Progress.IsActive = false;
            Progress.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateApplyBtn()
    {
        var changes = _tweakSelections.Count(kv => kv.Value) + (_classicMenuChanged ? 1 : 0);
        ApplyBtn.Content = $"Apply Changes ({changes})";
        ApplyBtn.IsEnabled = changes > 0;
    }

    private async void ApplyBtn_Click(object sender, RoutedEventArgs e)
    {
        var ids = _tweakSelections.Where(kv => kv.Value).Select(kv => kv.Key).ToList();
        if (_classicMenuChanged)
            ids.Insert(0, _classicMenuTarget ? "classic-menu-enable" : "classic-menu-disable");

        if (ids.Count == 0) return;

        var dialog = new ContentDialog
        {
            Title = string.Format(S._("ctx.applyChangesConfirm"), ids.Count),
            Content = S._("ctx.modifyRegistryWarning"),
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot,
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        ScanBtn.IsEnabled = false;
        ApplyBtn.IsEnabled = false;
        Progress.IsActive = true;
        Progress.Visibility = Visibility.Visible;
        StatusText.Text = S._("ctx.applyingChanges");

        try
        {
            if (_module is null) return;
            var plan = new OptimizationPlan("context-menu", ids);
            var progress = new Progress<TaskProgress>(p =>
            {
                DispatcherQueue.TryEnqueue(() => StatusText.Text = p.StatusText);
            });

            var result = await _module.OptimizeAsync(plan, progress);

            ResultTitle.Text = $"{result.ItemsProcessed} " + (result.ItemsProcessed == 1 ? "değişiklik" : "değişiklik") + " uygulandı";
            ResultDetail.Text = "Explorer has been restarted. Right-click to see the changes.\nAll changes are reversible — run Scan again to toggle them off.";
            ResultCard.Visibility = Visibility.Visible;
            StatusText.Text = S._("ctx.changesApplied");
        }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
        finally
        {
            ScanBtn.IsEnabled = true;
            Progress.IsActive = false;
            Progress.Visibility = Visibility.Collapsed;
        }
    }

    private void ApplyLocalization()
    {
        if (FindName("PageTitle") is Microsoft.UI.Xaml.Controls.TextBlock title)
            title.Text = S._("ctx.title");
        if (FindName("PageSubtitle") is Microsoft.UI.Xaml.Controls.TextBlock subtitle)
            subtitle.Text = S._("ctx.subtitle");
    }
}
