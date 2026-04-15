using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.Threading;

namespace AuraCore.UI.Avalonia.Views.Pages.AI;

public partial class ScheduleSection : UserControl
{
    private readonly Dictionary<string, ScheduleInfo> _schedules = new();

    private static readonly (string Id, string Name, string Desc, string Icon, string Color)[] TaskDefs =
    {
        ("junk",     "Junk Cleaner",        "Clean temporary files and caches",     "\u2702", "#00D4AA"),
        ("ram",      "RAM Optimizer",        "Free up unused memory",               "\u26A1", "#3B82F6"),
        ("registry", "Registry Scan",        "Scan for registry issues",            "\u2699", "#8B5CF6"),
        ("privacy",  "Privacy Cleaner",      "Clear browser data and trackers",     "\u26D4", "#EC4899"),
        ("disk",     "Disk Cleanup",         "Remove unnecessary system files",     "\u267B", "#F59E0B"),
        ("health",   "System Health Check",  "Run full system diagnostics",         "\u2665", "#22C55E"),
    };

    private static readonly string[] IntervalLabels =
        { "Disabled", "Every 1 hour", "Every 6 hours", "Every 12 hours", "Daily", "Weekly" };

    private static readonly string[] IntervalKeys =
        { "disabled", "1h", "6h", "12h", "daily", "weekly" };

    public ScheduleSection()
    {
        InitializeComponent();
        Loaded += (_, _) => { BuildCards(); ApplyLocalization(); };
        LocalizationService.LanguageChanged += () =>
            Dispatcher.UIThread.Post(ApplyLocalization);
        Unloaded += (s, e) =>
        {
            foreach (var info in _schedules.Values)
            {
                if (info.Timer != null)
                {
                    if (info.TickHandler != null) info.Timer.Tick -= info.TickHandler;
                    info.Timer.Stop();
                }
            }
            _schedules.Clear();
        };
    }

    // ───────────────────────── Card Builder ─────────────────────────

    private void BuildCards()
    {
        TaskList.Children.Clear();

        foreach (var (id, name, desc, icon, color) in TaskDefs)
        {
            var info = new ScheduleInfo { TaskId = id, Interval = "disabled", IsEnabled = false };
            _schedules[id] = info;

            var accentColor = Color.Parse(color);

            // ── Icon badge ──
            var iconBorder = new Border
            {
                Width = 42, Height = 42, CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(accentColor, 0.15),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 14, 0),
                Child = new TextBlock
                {
                    Text = icon, FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(accentColor)
                }
            };

            // ── Name + Description ──
            var nameBlock = new TextBlock
            {
                Text = name, FontSize = 14, FontWeight = FontWeight.SemiBold,
                Foreground = FindBrush("TextPrimaryBrush", global::Avalonia.Media.Brushes.White)
            };
            var descBlock = new TextBlock
            {
                Text = desc, FontSize = 11,
                Foreground = FindBrush("TextMutedBrush", global::Avalonia.Media.Brushes.Gray)
            };

            // ── Timing labels ──
            var lastRunLabel = new TextBlock
            {
                Text = "Last run: Never", FontSize = 10, Tag = id + "_last",
                Foreground = FindBrush("TextMutedBrush", global::Avalonia.Media.Brushes.Gray)
            };
            var nextRunLabel = new TextBlock
            {
                Text = "Next run: --", FontSize = 10, Tag = id + "_next",
                Foreground = new SolidColorBrush(accentColor, 0.8)
            };

            info.LastRunLabel = lastRunLabel;
            info.NextRunLabel = nextRunLabel;

            var leftPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center, Spacing = 2,
                Children = { nameBlock, descBlock, lastRunLabel, nextRunLabel }
            };

            // ── Interval ComboBox ──
            var combo = new ComboBox
            {
                ItemsSource = IntervalLabels,
                SelectedIndex = 0,
                MinWidth = 140,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = id
            };
            combo.SelectionChanged += OnIntervalChanged;

            // ── Toggle switch ──
            var toggle = new ToggleSwitch
            {
                IsChecked = false,
                VerticalAlignment = VerticalAlignment.Center,
                OnContent = "On",
                OffContent = "Off",
                Tag = id
            };
            toggle.IsCheckedChanged += OnToggleChanged;

            info.Toggle = toggle;
            info.Combo = combo;

            // ── Right-side controls stack ──
            var controlsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { combo, toggle }
            };

            // ── Grid layout ──
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                Children = { iconBorder, leftPanel, controlsPanel }
            };
            Grid.SetColumn(iconBorder, 0);
            Grid.SetColumn(leftPanel, 1);
            Grid.SetColumn(controlsPanel, 2);

            // ── Card border (glassmorphic) ──
            var card = new Border
            {
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16, 14),
                Background = new SolidColorBrush(Colors.White, 0.025),
                BorderBrush = new SolidColorBrush(Colors.White, 0.05),
                Child = grid
            };

            TaskList.Children.Add(card);
        }
    }

    // ───────────────────────── Event Handlers ─────────────────────────

    private void OnIntervalChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox combo || combo.Tag is not string taskId) return;
        if (!_schedules.TryGetValue(taskId, out var info)) return;

        var idx = combo.SelectedIndex;
        info.Interval = idx >= 0 && idx < IntervalKeys.Length ? IntervalKeys[idx] : "disabled";

        ReconfigureTimer(info);
    }

    private void OnToggleChanged(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch toggle || toggle.Tag is not string taskId) return;
        if (!_schedules.TryGetValue(taskId, out var info)) return;

        info.IsEnabled = toggle.IsChecked == true;
        ReconfigureTimer(info);
    }

    // ───────────────────────── Timer Logic ─────────────────────────

    private void ReconfigureTimer(ScheduleInfo info)
    {
        // Stop existing timer
        if (info.Timer != null)
        {
            info.Timer.Stop();
            info.Timer.Tick -= info.TickHandler;
            info.Timer = null;
            info.TickHandler = null;
        }

        if (!info.IsEnabled || info.Interval == "disabled")
        {
            info.NextRunLabel!.Text = "Next run: --";
            return;
        }

        var span = IntervalToTimeSpan(info.Interval);
        if (span == TimeSpan.Zero)
        {
            info.NextRunLabel!.Text = "Next run: --";
            return;
        }

        var timer = new DispatcherTimer { Interval = span };
        EventHandler handler = (_, _) => OnTimerFired(info);
        timer.Tick += handler;
        timer.Start();

        info.Timer = timer;
        info.TickHandler = handler;

        var nextRun = DateTime.Now.Add(span);
        info.NextRunLabel!.Text = $"Next run: {nextRun:g}";
    }

    private void OnTimerFired(ScheduleInfo info)
    {
        info.LastRun = DateTime.Now;
        info.LastRunLabel!.Text = $"Last run: {info.LastRun:T}";

        // Recalculate next run
        var span = IntervalToTimeSpan(info.Interval);
        if (span > TimeSpan.Zero)
        {
            var nextRun = DateTime.Now.Add(span);
            info.NextRunLabel!.Text = $"Next run: {nextRun:g}";
        }

        // Fire the appropriate task (placeholder -- real modules would be injected)
        _ = RunTaskAsync(info.TaskId);
    }

    private static async Task RunTaskAsync(string taskId)
    {
        // v1.7 placeholder: logs to debug. Real implementation would call engine modules.
        await Task.CompletedTask;
        System.Diagnostics.Debug.WriteLine($"[Scheduler] Task '{taskId}' executed at {DateTime.Now:T}");
    }

    private static TimeSpan IntervalToTimeSpan(string interval) => interval switch
    {
        "1h"     => TimeSpan.FromHours(1),
        "6h"     => TimeSpan.FromHours(6),
        "12h"    => TimeSpan.FromHours(12),
        "daily"  => TimeSpan.FromHours(24),
        "weekly" => TimeSpan.FromDays(7),
        _        => TimeSpan.Zero
    };

    // ───────────────────────── Localization ─────────────────────────

    private void ApplyLocalization()
    {
        PageTitle.Text = LocalizationService._("nav.autoSchedule");
    }

    // ───────────────────────── Theme-variant-aware brush lookup ─────────────────────────
    // Phase 2 hotfix pattern (see MainWindow.FindBrush, commit 442518f).
    // AuraCoreThemeV2 brushes live under ThemeDictionaries.Dark; the default
    // FindResource uses ThemeVariant.Default and throws UnsetValue → IBrush cast.
    private global::Avalonia.Media.IBrush FindBrush(string key, global::Avalonia.Media.IBrush fallback)
    {
        var variant = this.ActualThemeVariant ?? global::Avalonia.Styling.ThemeVariant.Dark;
        if (this.TryFindResource(key, variant, out var v) && v is global::Avalonia.Media.IBrush b)
            return b;
        if (this.TryFindResource(key, global::Avalonia.Styling.ThemeVariant.Dark, out var v2) && v2 is global::Avalonia.Media.IBrush b2)
            return b2;
        if (global::Avalonia.Application.Current is { } app &&
            app.TryFindResource(key, global::Avalonia.Styling.ThemeVariant.Dark, out var v3) && v3 is global::Avalonia.Media.IBrush b3)
            return b3;
        return fallback;
    }

    // ───────────────────────── Inner Types ─────────────────────────

    private class ScheduleInfo
    {
        public string TaskId { get; set; } = "";
        public string Interval { get; set; } = "disabled";
        public bool IsEnabled { get; set; }
        public DateTime? LastRun { get; set; }
        public DispatcherTimer? Timer { get; set; }
        public EventHandler? TickHandler { get; set; }
        public ToggleSwitch? Toggle { get; set; }
        public ComboBox? Combo { get; set; }
        public TextBlock? LastRunLabel { get; set; }
        public TextBlock? NextRunLabel { get; set; }
    }
}
