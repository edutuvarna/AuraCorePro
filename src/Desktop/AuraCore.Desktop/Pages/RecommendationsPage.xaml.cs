using AuraCore.Desktop.Services;
using AuraCore.Desktop.Services.AI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AuraCore.Desktop.Pages;

public sealed partial class RecommendationsPage : Page
{
    private readonly RecommendationEngine _engine;

    public RecommendationsPage()
    {
        InitializeComponent();
        ApplyLocalization();
        Services.S.LanguageChanged += () => DispatcherQueue?.TryEnqueue(ApplyLocalization);
        _engine = new RecommendationEngine(App.Current.Services);
    }

    private async void AnalyzeBtn_Click(object sender, RoutedEventArgs e)
    {
        AnalyzeBtn.IsEnabled = false;
        Progress.IsActive = true; Progress.Visibility = Visibility.Visible;
        StatusText.Text = "Analyzing your system...";
        RecList.Children.Clear();
        AllGoodCard.Visibility = Visibility.Collapsed;
        SummaryCard.Visibility = Visibility.Collapsed;

        try
        {
            var recs = await _engine.AnalyzeAsync();

            if (recs.Count == 0 || recs.All(r => r.Priority == RecommendationPriority.Low))
            {
                AllGoodCard.Visibility = Visibility.Visible;
                StatusText.Text = "Analysis complete — no issues found!";
                return;
            }

            // Summary
            CriticalCount.Text = recs.Count(r => r.Priority == RecommendationPriority.Critical).ToString();
            HighCount.Text = recs.Count(r => r.Priority == RecommendationPriority.High).ToString();
            MediumCount.Text = recs.Count(r => r.Priority == RecommendationPriority.Medium).ToString();
            LowCount.Text = recs.Count(r => r.Priority == RecommendationPriority.Low).ToString();
            SummaryCard.Visibility = Visibility.Visible;

            foreach (var rec in recs)
            {
                var priorityColor = rec.Priority switch
                {
                    RecommendationPriority.Critical => Windows.UI.Color.FromArgb(255, 198, 40, 40),
                    RecommendationPriority.High => Windows.UI.Color.FromArgb(255, 230, 81, 0),
                    RecommendationPriority.Medium => Windows.UI.Color.FromArgb(255, 21, 101, 192),
                    _ => Windows.UI.Color.FromArgb(255, 96, 125, 139)
                };

                var card = new Border
                {
                    Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                    CornerRadius = new CornerRadius(8), Padding = new Thickness(20, 16, 20, 16),
                    BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(60, priorityColor.R, priorityColor.G, priorityColor.B)),
                    BorderThickness = new Thickness(1)
                };

                var grid = new Grid { ColumnSpacing = 16 };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Priority indicator
                var indicator = new Border
                {
                    Width = 4, CornerRadius = new CornerRadius(2),
                    Background = new SolidColorBrush(priorityColor),
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                Grid.SetColumn(indicator, 0);
                grid.Children.Add(indicator);

                // Content
                var content = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
                var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                titleRow.Children.Add(new FontIcon
                {
                    Glyph = ((char)Convert.ToInt32(rec.Icon, 16)).ToString(),
                    FontSize = 16, Foreground = new SolidColorBrush(priorityColor)
                });
                titleRow.Children.Add(new TextBlock { Text = rec.Title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 14 });

                // Priority badge
                var badge = new Border
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, priorityColor.R, priorityColor.G, priorityColor.B)),
                    CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2), VerticalAlignment = VerticalAlignment.Center
                };
                badge.Child = new TextBlock
                {
                    Text = rec.Priority.ToString().ToUpper(), FontSize = 9,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(priorityColor)
                };
                titleRow.Children.Add(badge);

                // Category badge
                var catBadge = new Border
                {
                    Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
                    CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2), VerticalAlignment = VerticalAlignment.Center
                };
                catBadge.Child = new TextBlock { Text = rec.Category, FontSize = 9, Opacity = 0.6 };
                titleRow.Children.Add(catBadge);

                content.Children.Add(titleRow);
                content.Children.Add(new TextBlock { Text = rec.Description, FontSize = 12, Opacity = 0.7, TextWrapping = TextWrapping.Wrap });
                Grid.SetColumn(content, 1);
                grid.Children.Add(content);

                // Action button
                if (!string.IsNullOrEmpty(rec.ModuleId) && !string.IsNullOrEmpty(rec.ActionLabel))
                {
                    var actionBtn = new Button { Content = rec.ActionLabel, Padding = new Thickness(16, 8, 16, 8), VerticalAlignment = VerticalAlignment.Center };
                    if (rec.Priority == RecommendationPriority.Critical)
                        actionBtn.Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["AccentButtonStyle"];

                    var capturedModule = rec.ModuleId;
                    actionBtn.Click += (s, ev) =>
                    {
                        // Navigate to the module
                        if (this.Frame is not null)
                        {
                            var page = capturedModule switch
                            {
                                "junk-cleaner" => typeof(JunkCleanerPage),
                                "ram-optimizer" => typeof(RamOptimizerPage),
                                "storage-compression" => typeof(StoragePage),
                                "registry-optimizer" => typeof(RegistryPage),
                                "bloatware-removal" => typeof(BloatwarePage),
                                "network-optimizer" => typeof(NetworkPage),
                                "explorer-tweaks" => typeof(ExplorerPage),
                                "scheduler" => typeof(SchedulerPage),
                                _ => (Type?)null
                            };
                            if (page is not null) this.Frame.Navigate(page);
                        }
                    };
                    Grid.SetColumn(actionBtn, 2);
                    grid.Children.Add(actionBtn);
                }

                card.Child = grid;
                RecList.Children.Add(card);
            }

            StatusText.Text = $"Found {recs.Count} recommendations";
        }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
        finally
        {
            AnalyzeBtn.IsEnabled = true;
            Progress.IsActive = false; Progress.Visibility = Visibility.Collapsed;
        }
    }

    private void ApplyLocalization()
    {
        if (FindName("PageTitle") is Microsoft.UI.Xaml.Controls.TextBlock title)
            title.Text = S._("rec.title");
        if (FindName("PageSubtitle") is Microsoft.UI.Xaml.Controls.TextBlock subtitle)
            subtitle.Text = S._("rec.subtitle");
    }
}
