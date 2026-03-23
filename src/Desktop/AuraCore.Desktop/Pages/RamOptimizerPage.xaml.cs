using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Desktop.Services;
using AuraCore.Module.RamOptimizer;
using AuraCore.Module.RamOptimizer.Models;

namespace AuraCore.Desktop.Pages;

public sealed partial class RamOptimizerPage : Page
{
    private RamOptimizerModule? _module;
    private readonly MemoryTrendTracker _tracker = new();
    private DispatcherTimer? _refreshTimer;

    public RamOptimizerPage()
    {
        InitializeComponent();
        ApplyLocalization();
        Services.S.LanguageChanged += () => DispatcherQueue?.TryEnqueue(ApplyLocalization);
        var modules = App.Current.Services.GetServices<IOptimizationModule>();
        _module = modules.FirstOrDefault(m => m.Id == "ram-optimizer") as RamOptimizerModule;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        _refreshTimer?.Stop();
        _refreshTimer = null;
        base.OnNavigatedFrom(e);
    }

    private void TrendToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (TrendToggle.IsOn)
        {
            _tracker.Reset();
            _tracker.Start();
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
            _refreshTimer.Tick += (s, ev) => RefreshTrendDisplay();
            _refreshTimer.Start();
            StatusText.Text = "Trend tracking active — sampling every 5s...";
        }
        else
        {
            _tracker.Stop();
            _refreshTimer?.Stop();
            _refreshTimer = null;
            LeakAlertCard.Visibility = Visibility.Collapsed;
            StatusText.Text = "Trend tracking stopped.";
        }
    }

    private void RefreshTrendDisplay()
    {
        // Update leak alerts
        var leaks = _tracker.GetSuspectedLeaks();
        if (leaks.Count > 0)
        {
            LeakAlertCard.Visibility = Visibility.Visible;
            LeakDetailText.Text = $"{leaks.Count} process(es) showing continuous memory growth:";
            LeakList.Children.Clear();
            foreach (var leak in leaks.Take(5))
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                row.Children.Add(new TextBlock
                {
                    Text = $"{leak.Name} (PID {leak.Pid})",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 12
                });
                row.Children.Add(new TextBlock
                {
                    Text = $"+{leak.GrowthRateMbPerMin:F1} MB/min",
                    FontSize = 12, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 230, 81, 0))
                });
                if (leak.Samples.Count > 0)
                {
                    var current = leak.Samples.Last() / (1024.0 * 1024);
                    row.Children.Add(new TextBlock { Text = $"({current:F0} MB now)", FontSize = 11, Opacity = 0.5 });
                }
                LeakList.Children.Add(row);
            }
        }
        else
        {
            LeakAlertCard.Visibility = Visibility.Collapsed;
        }

        // Update sparklines in existing process rows (if any)
        UpdateSparklines();
    }

    private void UpdateSparklines()
    {
        foreach (var child in ProcessList.Children)
        {
            if (child is Grid grid && grid.Tag is int pid)
            {
                var trend = _tracker.GetTrend(pid);
                if (trend is null || trend.Samples.Count < 2) continue;

                // Find or create the sparkline canvas
                var spark = grid.Children.OfType<Canvas>().FirstOrDefault();
                if (spark is null) continue;

                RenderSparkline(spark, trend);
            }
        }
    }

    private static void RenderSparkline(Canvas canvas, MemoryTrendTracker.ProcessTrend trend)
    {
        canvas.Children.Clear();
        var samples = trend.Samples;
        if (samples.Count < 2) return;

        double w = 80, h = 16;
        var min = samples.Min();
        var max = samples.Max();
        var range = max - min;
        if (range == 0) range = 1;

        var color = trend.IsSuspectedLeak
            ? Windows.UI.Color.FromArgb(255, 230, 81, 0)
            : Windows.UI.Color.FromArgb(255, 33, 150, 243);

        for (int i = 0; i < samples.Count; i++)
        {
            var x = (double)i / (samples.Count - 1) * (w - 4);
            var barH = (double)(samples[i] - min) / range * h;
            barH = Math.Max(barH, 1);

            var rect = new Microsoft.UI.Xaml.Shapes.Rectangle
            {
                Width = Math.Max(w / samples.Count - 1, 2),
                Height = barH,
                Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(180, color.R, color.G, color.B)),
                RadiusX = 1, RadiusY = 1
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, h - barH);
            canvas.Children.Add(rect);
        }
    }

    private async void ScanBtn_Click(object sender, RoutedEventArgs e)
    {
        ScanBtn.IsEnabled = false;
        OptimizeBtn.IsEnabled = false;
        Progress.IsActive = true;
        Progress.Visibility = Visibility.Visible;
        StatusText.Text = "Analyzing memory usage...";
        ProcessList.Children.Clear();
        ResultCard.Visibility = Visibility.Collapsed;

        try
        {
            if (_module is null) { StatusText.Text = "Module not available."; return; }

            await _module.ScanAsync(new ScanOptions());
            var report = _module.LastReport;
            if (report is null) { StatusText.Text = "Scan returned no data."; return; }

            // Memory overview
            TotalRamText.Text = report.TotalDisplay;
            UsedRamText.Text = report.UsedDisplay;
            AvailRamText.Text = report.AvailableDisplay;
            ReclaimText.Text = report.ReclaimableDisplay;
            MemBar.Value = report.UsagePercent;
            MemPctText.Text = $"Memory usage: {report.UsagePercent}%";
            MemoryOverview.Visibility = Visibility.Visible;

            // Process list header
            var headerGrid = CreateProcessRow("Process", "Memory", "Category", isHeader: true);
            ProcessList.Children.Add(headerGrid);

            // Process rows
            foreach (var proc in report.TopProcesses.Take(30))
            {
                var row = CreateProcessRow(
                    $"{proc.Name} (PID {proc.Pid})",
                    proc.MemoryDisplay,
                    proc.Category,
                    isEssential: proc.IsEssential,
                    pid: proc.Pid);
                ProcessList.Children.Add(row);
            }

            StatusText.Text = $"Found {report.TopProcesses.Count} processes — {report.ReclaimableDisplay} estimated reclaimable";
            OptimizeBtn.IsEnabled = true;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            ScanBtn.IsEnabled = true;
            Progress.IsActive = false;
            Progress.Visibility = Visibility.Collapsed;
        }
    }

    private async void OptimizeBtn_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Optimize RAM?",
            Content = "This will trim the working set of non-essential processes.\nApplications will briefly use less RAM but can reclaim it as needed.\nThis is safe and non-destructive.",
            PrimaryButtonText = "Optimize",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot,
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        ScanBtn.IsEnabled = false;
        OptimizeBtn.IsEnabled = false;
        Progress.IsActive = true;
        Progress.Visibility = Visibility.Visible;
        StatusText.Text = "Optimizing RAM...";

        try
        {
            if (_module is null) return;

            var plan = new OptimizationPlan("ram-optimizer", new List<string>());
            var progress = new Progress<TaskProgress>(p =>
            {
                DispatcherQueue.TryEnqueue(() => StatusText.Text = p.StatusText);
            });

            var result = await _module.OptimizeAsync(plan, progress);

            var freedDisplay = result.BytesFreed switch
            {
                < 1024 * 1024 => $"{result.BytesFreed / 1024.0:F0} KB",
                < 1024L * 1024 * 1024 => $"{result.BytesFreed / (1024.0 * 1024):F0} MB",
                _ => $"{result.BytesFreed / (1024.0 * 1024 * 1024):F2} GB"
            };

            ResultTitle.Text = $"Freed {freedDisplay} of RAM";
            ResultDetail.Text = $"Optimized {result.ItemsProcessed} processes in {result.Duration.TotalSeconds:F1}s";
            ResultCard.Visibility = Visibility.Visible;
            StatusText.Text = "Optimization complete!";

            // Refresh the process list
            ProcessList.Children.Clear();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            ScanBtn.IsEnabled = true;
            OptimizeBtn.IsEnabled = false;
            Progress.IsActive = false;
            Progress.Visibility = Visibility.Collapsed;
        }
    }

    private Grid CreateProcessRow(string name, string memory, string category,
        bool isHeader = false, bool isEssential = false, int pid = 0)
    {
        var grid = new Grid
        {
            Padding = new Thickness(12, 8, 12, 8),
            ColumnSpacing = 8,
            Background = isHeader
                ? (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"]
                : null,
            CornerRadius = isHeader ? new CornerRadius(6) : new CornerRadius(0),
            BorderBrush = isHeader ? null : (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = isHeader ? new Thickness(0) : new Thickness(0, 0, 0, 0.5),
            Tag = isHeader ? null : (object)pid
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // sparkline
        if (!isHeader)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // end btn

        var nameText = new TextBlock
        {
            Text = name,
            FontWeight = isHeader ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
            FontSize = isHeader ? 12 : 13,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = isEssential ? 0.5 : 1.0
        };
        Grid.SetColumn(nameText, 0);
        grid.Children.Add(nameText);

        var memText = new TextBlock
        {
            Text = memory,
            FontWeight = isHeader ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
            FontSize = isHeader ? 12 : 13,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(memText, 1);
        grid.Children.Add(memText);

        // Category badge
        var catBorder = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 2, 8, 2),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = isHeader ? null : GetCategoryBrush(category)
        };
        var catText = new TextBlock
        {
            Text = category,
            FontSize = 11,
            FontWeight = isHeader ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
            Opacity = isHeader ? 1.0 : 0.9
        };
        catBorder.Child = catText;
        Grid.SetColumn(catBorder, 2);
        grid.Children.Add(catBorder);

        // End process button (not for header or essential processes)
        if (!isHeader && !isEssential && pid > 0)
        {
            // Sparkline canvas for trend
            var spark = new Canvas { Width = 80, Height = 16, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(spark, 3);
            grid.Children.Add(spark);

            // Pre-fill sparkline if tracking
            var trend = _tracker.GetTrend(pid);
            if (trend is not null && trend.Samples.Count >= 2)
                RenderSparkline(spark, trend);

            var endBtn = new Button
            {
                Content = "End",
                FontSize = 11,
                Padding = new Thickness(10, 2, 10, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Tag = pid
            };
            endBtn.Click += EndProcess_Click;
            Grid.SetColumn(endBtn, 4);
            grid.Children.Add(endBtn);
        }
        else if (!isHeader)
        {
            // Still add sparkline for essential processes
            var spark = new Canvas { Width = 80, Height = 16, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(spark, 3);
            grid.Children.Add(spark);
        }
        else
        {
            // Header: trend label
            var trendH = new TextBlock { Text = "Trend", FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(trendH, 3);
            grid.Children.Add(trendH);
        }

        return grid;
    }

    private async void EndProcess_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int pid) return;

        Process? proc = null;
        try { proc = Process.GetProcessById(pid); } catch { return; }

        var dialog = new ContentDialog
        {
            Title = "End process?",
            Content = $"Are you sure you want to end \"{proc.ProcessName}\" (PID {pid})?\nUnsaved work in this application will be lost.",
            PrimaryButtonText = "End Process",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot,
            DefaultButton = ContentDialogButton.Close
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        try
        {
            proc.Kill();
            StatusText.Text = $"Process \"{proc.ProcessName}\" ended.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Cannot end process: {ex.Message}";
        }
    }

    private static Brush GetCategoryBrush(string category)
    {
        var color = category switch
        {
            "System" => Windows.UI.Color.FromArgb(30, 156, 39, 176),
            "Browser" => Windows.UI.Color.FromArgb(30, 33, 150, 243),
            "Background" => Windows.UI.Color.FromArgb(30, 255, 152, 0),
            "Service" => Windows.UI.Color.FromArgb(30, 96, 125, 139),
            _ => Windows.UI.Color.FromArgb(15, 128, 128, 128)
        };
        return new SolidColorBrush(color);
    }

    private void ApplyLocalization()
    {
        if (FindName("PageTitle") is Microsoft.UI.Xaml.Controls.TextBlock title)
            title.Text = S._("ram.title");
        if (FindName("PageSubtitle") is Microsoft.UI.Xaml.Controls.TextBlock subtitle)
            subtitle.Text = S._("ram.subtitle");
    }
}
