using AuraCore.Desktop.Services;
using AuraCore.Desktop.Services.Scheduler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.Desktop.Pages;

public sealed partial class SchedulerPage : Page
{
    private List<ScheduleEntry> _schedules;
    private BackgroundScheduler? _scheduler;

    public SchedulerPage()
    {
        InitializeComponent();
        ApplyLocalization();
        Services.S.LanguageChanged += () => DispatcherQueue?.TryEnqueue(ApplyLocalization);
        _schedules = ScheduleStore.Load();

        // Get or create scheduler (singleton via App)
        _scheduler = App.Scheduler;

        if (_scheduler is not null)
        {
            MasterToggle.IsOn = _scheduler.IsRunning;
            _scheduler.OnTaskCompleted += msg => DispatcherQueue.TryEnqueue(RefreshLog);
        }

        PopulateModuleCombo();
        RenderSchedules();
        RefreshLog();
    }

    private void PopulateModuleCombo()
    {
        var modules = App.Current.Services.GetServices<IOptimizationModule>();
        foreach (var mod in modules.OrderBy(m => m.DisplayName))
        {
            // Only show modules that make sense to schedule
            if (mod.Id is "system-health" or "junk-cleaner" or "ram-optimizer" or "registry-optimizer"
                or "bloatware-removal" or "storage-compression")
            {
                AddModuleBox.Items.Add(new ComboBoxItem { Content = mod.DisplayName, Tag = mod.Id });
            }
        }
        if (AddModuleBox.Items.Count > 0) AddModuleBox.SelectedIndex = 0;
    }

    private void RenderSchedules()
    {
        ScheduleList.Children.Clear();

        if (_schedules.Count == 0)
        {
            ScheduleList.Children.Add(new TextBlock { Text = "No schedules configured", Opacity = 0.5, FontSize = 13 });
            return;
        }

        foreach (var sched in _schedules)
        {
            var card = new Border
            {
                Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 12, 16, 12),
                BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(0.5)
            };

            var grid = new Grid { ColumnSpacing = 12 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var toggle = new ToggleSwitch { IsOn = sched.Enabled, OnContent = "", OffContent = "", VerticalAlignment = VerticalAlignment.Center };
            var captured = sched;
            toggle.Toggled += (s, e) => { captured.Enabled = toggle.IsOn; SaveSchedules(); UpdateStatus(); };
            Grid.SetColumn(toggle, 0);
            grid.Children.Add(toggle);

            var info = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(new TextBlock { Text = sched.ModuleName, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 14 });

            var detailParts = new List<string> { sched.Interval.ToDisplayString() };
            if (sched.OnlyWhenIdle) detailParts.Add("only when idle");
            if (sched.LastRun.HasValue) detailParts.Add($"last: {sched.LastRun.Value.LocalDateTime:g}");
            info.Children.Add(new TextBlock { Text = string.Join(" • ", detailParts), FontSize = 11, Opacity = 0.5 });

            if (!string.IsNullOrEmpty(sched.LastResult))
                info.Children.Add(new TextBlock { Text = sched.LastResult, FontSize = 11, Opacity = 0.4, FontStyle = Windows.UI.Text.FontStyle.Italic });

            Grid.SetColumn(info, 1);
            grid.Children.Add(info);

            // Interval selector
            var intervalBox = new ComboBox { Width = 140, VerticalAlignment = VerticalAlignment.Center };
            foreach (ScheduleInterval val in Enum.GetValues<ScheduleInterval>())
                intervalBox.Items.Add(new ComboBoxItem { Content = val.ToDisplayString(), Tag = val.ToString() });
            intervalBox.SelectedIndex = (int)sched.Interval;
            intervalBox.SelectionChanged += (s, e) =>
            {
                if (intervalBox.SelectedItem is ComboBoxItem ci && Enum.TryParse<ScheduleInterval>(ci.Tag?.ToString(), out var iv))
                { captured.Interval = iv; SaveSchedules(); }
            };
            Grid.SetColumn(intervalBox, 2);
            grid.Children.Add(intervalBox);

            // Remove button
            var removeBtn = new Button { Content = "\xE74D", FontFamily = new FontFamily("Segoe MDL2 Assets"), Padding = new Thickness(8), VerticalAlignment = VerticalAlignment.Center };
            removeBtn.Click += (s, e) => { _schedules.Remove(captured); SaveSchedules(); RenderSchedules(); UpdateStatus(); };
            Grid.SetColumn(removeBtn, 3);
            grid.Children.Add(removeBtn);

            card.Child = grid;
            ScheduleList.Children.Add(card);
        }

        UpdateStatus();
    }

    private void MasterToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_scheduler is null) return;
        if (MasterToggle.IsOn)
        {
            _scheduler.Reload();
            _scheduler.Start();
        }
        else
            _scheduler.Stop();
        UpdateStatus();
    }

    private void AddSchedule_Click(object sender, RoutedEventArgs e)
    {
        if (AddModuleBox.SelectedItem is not ComboBoxItem modItem) return;
        if (AddIntervalBox.SelectedItem is not ComboBoxItem intItem) return;

        var moduleId = modItem.Tag?.ToString() ?? "";
        var moduleName = modItem.Content?.ToString() ?? "";

        if (_schedules.Any(s => s.ModuleId == moduleId)) return; // Already exists

        var interval = Enum.TryParse<ScheduleInterval>(intItem.Tag?.ToString(), out var iv) ? iv : ScheduleInterval.Daily;

        _schedules.Add(new ScheduleEntry
        {
            ModuleId = moduleId,
            ModuleName = moduleName,
            Enabled = true,
            Interval = interval,
            OnlyWhenIdle = AddIdleCheck.IsChecked == true
        });

        SaveSchedules();
        RenderSchedules();
        _scheduler?.Reload();
    }

    private void SaveSchedules()
    {
        ScheduleStore.Save(_schedules);
        _scheduler?.Reload();
    }

    private void UpdateStatus()
    {
        var enabled = _schedules.Count(s => s.Enabled);
        SchedulerStatus.Text = _scheduler?.IsRunning == true
            ? $"Active — {enabled} task(s) scheduled"
            : "Inactive — toggle ON to start";
    }

    private void RefreshLog()
    {
        LogList.Children.Clear();
        if (_scheduler is null || _scheduler.Log.Count == 0)
        {
            NoLogText.Visibility = Visibility.Visible;
            return;
        }
        NoLogText.Visibility = Visibility.Collapsed;

        foreach (var entry in _scheduler.Log.Take(15))
        {
            LogList.Children.Add(new TextBlock
            {
                Text = entry, FontSize = 12, Opacity = 0.6,
                FontFamily = new FontFamily("Consolas")
            });
        }
    }

    private void ApplyLocalization()
    {
        if (FindName("PageTitle") is Microsoft.UI.Xaml.Controls.TextBlock title)
            title.Text = S._("sched.title");
        if (FindName("PageSubtitle") is Microsoft.UI.Xaml.Controls.TextBlock subtitle)
            subtitle.Text = S._("sched.subtitle");
    }
}
