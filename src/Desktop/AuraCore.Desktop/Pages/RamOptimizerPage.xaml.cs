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
        Brush? headerBg = null;
        Brush? rowBorder = null;
        try { headerBg = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"]; } catch { }
        try { rowBorder = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"]; } catch { }

        var grid = new Grid
        {
            Padding = new Thickness(12, 8, 12, 8),
            ColumnSpacing = 12,
            Background = isHeader ? headerBg : null,
            CornerRadius = isHeader ? new CornerRadius(6) : new CornerRadius(0),
            BorderBrush = isHeader ? null : rowBorder,
            BorderThickness = isHeader ? new Thickness(0) : new Thickness(0, 0, 0, 0.5),
            Tag = isHeader ? null : (object)pid
        };
        // Fixed 5-column layout for ALL rows (header + data)
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) }); // 0: Process name
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });                  // 1: Memory
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });                  // 2: Category
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });                   // 3: Trend/Sparkline
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });                   // 4: End btn

        // Col 0: Process name
        var nameText = new TextBlock
        {
            Text = name,
            FontWeight = isHeader ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
            FontSize = isHeader ? 12 : 13,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Opacity = isEssential ? 0.5 : 1.0
        };
        Grid.SetColumn(nameText, 0);
        grid.Children.Add(nameText);

        // Col 1: Memory (right-aligned)
        var memText = new TextBlock
        {
            Text = memory,
            FontWeight = isHeader ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
            FontSize = isHeader ? 12 : 13,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(memText, 1);
        grid.Children.Add(memText);

        // Col 2: Category badge (center-aligned)
        if (isHeader)
        {
            var catLabel = new TextBlock
            {
                Text = category, FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(catLabel, 2);
            grid.Children.Add(catLabel);
        }
        else
        {
            var catBorder = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 2, 8, 2),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = GetCategoryBrush(category)
            };
            catBorder.Child = new TextBlock
            {
                Text = category, FontSize = 10,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Opacity = 0.9
            };
            Grid.SetColumn(catBorder, 2);
            grid.Children.Add(catBorder);
        }

        // Col 3: Trend sparkline / header label
        if (isHeader)
        {
            var trendH = new TextBlock
            {
                Text = "Trend", FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(trendH, 3);
            grid.Children.Add(trendH);
        }
        else
        {
            var spark = new Canvas
            {
                Width = 80, Height = 16,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(spark, 3);
            grid.Children.Add(spark);

            var trend = _tracker.GetTrend(pid);
            if (trend is not null && trend.Samples.Count >= 2)
                RenderSparkline(spark, trend);
        }

        // Col 4: End button (data rows only, non-essential)
        if (!isHeader && !isEssential && pid > 0)
        {
            var endBtn = new Button
            {
                Content = "End",
                FontSize = 11,
                Padding = new Thickness(8, 2, 8, 2),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Tag = pid
            };
            endBtn.Click += EndProcess_Click;
            Grid.SetColumn(endBtn, 4);
            grid.Children.Add(endBtn);
        }

        return grid;
    }

    private async void EndProcess_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int pid) return;

        Process? proc = null;
        try { proc = Process.GetProcessById(pid); } catch { StatusText.Text = "Process no longer exists."; return; }

        var procName = proc.ProcessName;
        var dialog = new ContentDialog
        {
            Title = "End process?",
            Content = $"Are you sure you want to end \"{procName}\" (PID {pid})?\nUnsaved work in this application will be lost.",
            PrimaryButtonText = "End Process",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot,
            DefaultButton = ContentDialogButton.Close
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var killed = false;

        // Attempt 1: .NET Kill with entire process tree
        try
        {
            proc.Kill(entireProcessTree: true);
            await proc.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));
            killed = proc.HasExited;
        }
        catch { }

        // Attempt 2: taskkill /F /T /PID (force + tree)
        if (!killed)
        {
            try
            {
                var psi = new ProcessStartInfo("taskkill", $"/F /T /PID {pid}")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                var tk = Process.Start(psi);
                if (tk != null)
                {
                    await tk.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));
                    killed = tk.ExitCode == 0;
                }
            }
            catch { }
        }

        // Attempt 3: taskkill /F /IM name.exe (kill ALL instances by name)
        // Apps like Discord, Chrome, Teams run multiple processes
        if (!killed || Process.GetProcessesByName(procName).Length > 0)
        {
            try
            {
                var psi = new ProcessStartInfo("taskkill", $"/F /IM {procName}.exe")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                var tk = Process.Start(psi);
                if (tk != null)
                {
                    await tk.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));
                    killed = true;
                }
            }
            catch { }
        }

        // Verify - check if ANY instance of this process still exists
        await Task.Delay(500); // brief wait for processes to fully exit
        var remaining = Process.GetProcessesByName(procName).Length;
        killed = remaining == 0;

        if (killed)
        {
            StatusText.Text = $"Process \"{procName}\" ended.";
            // Remove all rows matching this process name from the list
            for (int i = ProcessList.Children.Count - 1; i >= 0; i--)
            {
                if (ProcessList.Children[i] is Grid g && g.Children.OfType<TextBlock>().FirstOrDefault() is TextBlock tb
                    && tb.Text.StartsWith(procName, StringComparison.OrdinalIgnoreCase))
                {
                    ProcessList.Children.RemoveAt(i);
                }
            }
        }
        else
        {
            StatusText.Text = $"Cannot end \"{procName}\" - may require administrator privileges.";
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
        if (FindName("PageTitle") is TextBlock title) title.Text = S._("ram.title");
        if (FindName("PageSubtitle") is TextBlock subtitle) subtitle.Text = S._("ram.subtitle");
        if (FindName("ScanBtn") is Button scan) scan.Content = S._("ram.analyze");
        if (FindName("OptimizeBtn") is Button opt) opt.Content = S._("ram.optimize");
        if (FindName("LblTotalRam") is TextBlock lt) lt.Text = S._("ram.totalRam");
        if (FindName("LblInUse") is TextBlock li) li.Text = S._("ram.inUse");
        if (FindName("LblAvail") is TextBlock la) la.Text = S._("ram.available");
        if (FindName("LblReclaim") is TextBlock lr) lr.Text = S._("ram.reclaimable");
    }
}
