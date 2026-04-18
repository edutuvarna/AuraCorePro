using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Pages.AI;

public record RecommendationItem(string Title, string Description, string Icon, string Module,
    string Priority, string Impact, ISolidColorBrush PriorityFg, ISolidColorBrush PriorityBg);

public partial class RecommendationsSection : UserControl
{
    public RecommendationsSection()
    {
        InitializeComponent();
        Loaded += (s, e) => { LoadRecs(); ApplyLocalization(); };
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
    }

    private void LoadRecs()
    {
        var items = new List<RecommendationItem>
        {
            new("Clean Temporary Files", "3.2 GB of temporary files can be safely removed to free disk space.",
                "\u267B", "Junk Cleaner", "High", "+3.2 GB", P("#EF4444"), P("#20EF4444")),
            new("Optimize RAM Usage", "Several background processes are using excessive memory. Trimming working sets could free 800 MB.",
                "\u26A1", "RAM Optimizer", "High", "+800 MB", P("#EF4444"), P("#20EF4444")),
            new("Remove Bloatware", "12 pre-installed apps detected that are rarely used and consuming resources.",
                "\u2702", "Bloatware Removal", "Medium", "12 apps", P("#F59E0B"), P("#20F59E0B")),
            new("Fix Registry Issues", "47 broken registry entries found. Cleaning may improve system stability.",
                "\u2699", "Registry Optimizer", "Medium", "47 issues", P("#F59E0B"), P("#20F59E0B")),
            new("Update Drivers", "2 drivers are more than 6 months old. Updating may improve performance.",
                "\u2B06", "Driver Updater", "Low", "2 drivers", P("#3B82F6"), P("#203B82F6")),
            new("Enable Privacy Protection", "Browser tracking data found. Running Privacy Cleaner can improve privacy.",
                "\u26D4", "Privacy Cleaner", "Low", "Privacy", P("#3B82F6"), P("#203B82F6")),
        };
        RecList.ItemsSource = items;
    }

    private static SolidColorBrush P(string h) => new(Color.Parse(h));
    private void Refresh_Click(object? s, RoutedEventArgs e) => LoadRecs();

    private void ApplyLocalization()
    {
        PageTitle.Text = LocalizationService.Get("nav.aiRecommendations");
        if (RefreshLabel is not null)
            RefreshLabel.Text = LocalizationService.Get("recs.analyzeButton");
        if (RecsSubtitle is not null)
            RecsSubtitle.Text = LocalizationService.Get("recs.subtitle");
        if (RecsBadgeLabel is not null)
            RecsBadgeLabel.Text = LocalizationService.Get("recs.badgeLabel");
    }
}
