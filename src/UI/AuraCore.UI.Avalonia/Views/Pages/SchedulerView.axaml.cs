using global::Avalonia.Controls;
using global::Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Pages;

public record ScheduleItem(string Name, string Description, string Schedule, string Icon,
    ISolidColorBrush IconBg, string Status, ISolidColorBrush StatusFg, ISolidColorBrush StatusBg);

public partial class SchedulerView : UserControl
{
    public SchedulerView()
    {
        InitializeComponent();
        Loaded += (s, e) => ApplyLocalization();
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
        Loaded += (s, e) => { LoadTasks(); ApplyLocalization(); };
}

    private void LoadTasks()
    {
        var items = new List<ScheduleItem>
        {
            new("Junk Cleaner", "Remove temporary files and cache", "Every 24 hours",
                "\u2702", P("#2000D4AA"), "Active", P("#22C55E"), P("#2022C55E")),
            new("RAM Optimizer", "Free up unused memory", "Every 4 hours",
                "\u26A1", P("#203B82F6"), "Active", P("#22C55E"), P("#2022C55E")),
            new("Registry Scan", "Check for broken registry entries", "Weekly",
                "\u2699", P("#20F59E0B"), "Paused", P("#F59E0B"), P("#20F59E0B")),
            new("Privacy Cleaner", "Clear browser data and tracking cookies", "Daily at 2:00 AM",
                "\u26D4", P("#208B5CF6"), "Active", P("#22C55E"), P("#2022C55E")),
            new("Disk Cleanup", "Deep system cleanup", "Weekly on Sunday",
                "\u267B", P("#20EF4444"), "Disabled", P("#8888A0"), P("#208888A0")),
            new("System Health", "Full system health check", "Daily at 9:00 AM",
                "\u2665", P("#2000D4AA"), "Active", P("#22C55E"), P("#2022C55E")),
        };
        TaskList.ItemsSource = items;
}

    private static SolidColorBrush P(string h) => new(Color.Parse(h));

    private void ApplyLocalization()
    {
        PageTitle.Text = LocalizationService._("nav.autoSchedule");
    }
}