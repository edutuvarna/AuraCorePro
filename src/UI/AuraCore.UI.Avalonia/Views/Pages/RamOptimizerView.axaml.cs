using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using global::Avalonia.Threading;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.RamOptimizer;
using AuraCore.Module.RamOptimizer.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

public record RamProcessItem(
    string Pid, string Name, string Memory, string Category,
    ISolidColorBrush EssentialBrush, ISolidColorBrush WhitelistFg, ISolidColorBrush BlacklistFg);

public partial class RamOptimizerView : UserControl
{
    private readonly RamOptimizerModule? _module;
    private DispatcherTimer? _monitorTimer;

    // Historical RAM graph data — sample every 10 seconds, keep 360 points (1 hour)
    private const int MaxHistoryPoints = 360;
    private readonly Queue<double> _ramHistory = new();
    private bool _autoOptimizeEnabled;
    private bool _isOptimizing; // guard against re-entrant auto-optimize

    private static readonly ISolidColorBrush ActiveWhitelistBrush = new SolidColorBrush(Color.Parse("#22C55E"));
    private static readonly ISolidColorBrush ActiveBlacklistBrush = new SolidColorBrush(Color.Parse("#EF4444"));
    private static readonly ISolidColorBrush InactiveBrush = new SolidColorBrush(Color.Parse("#444460"));

    public RamOptimizerView()
    {
        InitializeComponent();
        Loaded += (s, e) => ApplyLocalization();
        LocalizationService.LanguageChanged += () =>
            Dispatcher.UIThread.Post(ApplyLocalization);
        _module = App.Services.GetServices<IOptimizationModule>()
            .OfType<RamOptimizerModule>().FirstOrDefault();
        Loaded += async (s, e) =>
        {
            await RunScan();
            StartMonitoring();
        };
        DetachedFromVisualTree += (s, e) => StopMonitoring();
    }

    // ── Monitoring timer — 10-second ticks for graph + auto-optimize ──
    private void StartMonitoring()
    {
        _monitorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _monitorTimer.Tick += async (s, e) => await MonitorTick();
        _monitorTimer.Start();
    }

    private void StopMonitoring()
    {
        if (_monitorTimer is not null)
        {
            _monitorTimer.Stop();
            _monitorTimer = null;
        }
    }

    private async Task MonitorTick()
    {
        if (_module is null) return;
        try
        {
            await _module.ScanAsync(new ScanOptions());
            var r = _module.LastReport;
            if (r is null) return;

            // Update RAM bar display
            UsedRam.Text = r.UsedDisplay;
            TotalRam.Text = $"/ {r.TotalDisplay}";
            RamPct.Text = $"{r.UsagePercent}%";
            Reclaimable.Text = $"Reclaimable: {r.ReclaimableDisplay}";
            if (RamBar.Parent is Border parent && parent.Bounds.Width > 0)
                RamBar.Width = parent.Bounds.Width * r.UsagePercent / 100.0;
            OptBtn.IsEnabled = r.TotalReclaimableBytes > 0;

            // Add to history
            _ramHistory.Enqueue(r.UsagePercent);
            while (_ramHistory.Count > MaxHistoryPoints)
                _ramHistory.Dequeue();

            // Update graph
            UpdateRamGraph();

            // Auto-optimize when above 85%
            if (_autoOptimizeEnabled && r.UsagePercent > 85 && !_isOptimizing)
            {
                _isOptimizing = true;
                StatusText.Text = "Auto-optimizing (RAM > 85%)...";
                try
                {
                    var plan = new OptimizationPlan(_module.Id, new[] { "all" });
                    var progress = new Progress<TaskProgress>(p =>
                        Dispatcher.UIThread.Post(() => StatusText.Text = p.StatusText));
                    var result = await _module.OptimizeAsync(plan, progress);
                    StatusText.Text = $"Auto-optimized: freed from {result.ItemsProcessed} processes";
                    await RunScan();
                }
                catch { }
                finally { _isOptimizing = false; }
            }
        }
        catch { }
    }

    private void UpdateRamGraph()
    {
        if (_ramHistory.Count < 2) return;
        var w = RamGraphCanvas.Bounds.Width > 0 ? RamGraphCanvas.Bounds.Width : 200;
        var h = RamGraphCanvas.Bounds.Height > 0 ? RamGraphCanvas.Bounds.Height : 40;
        var arr = _ramHistory.ToArray();
        var pts = new global::Avalonia.Collections.AvaloniaList<global::Avalonia.Point>();
        for (int i = 0; i < arr.Length; i++)
        {
            var x = w * i / (arr.Length - 1);
            var y = h - (h * Math.Clamp(arr[i], 0, 100) / 100.0);
            pts.Add(new global::Avalonia.Point(x, y));
        }
        RamGraphLine.Points = pts;

        // Show min/max
        var min = arr.Min();
        var max = arr.Max();
        GraphMinMax.Text = $"Min: {min:F0}%  Max: {max:F0}%";
    }

    private async Task RunScan()
    {
        if (_module is null) return;
        ScanLabel.Text = "Scanning...";
        try
        {
            await _module.ScanAsync(new ScanOptions());
            var r = _module.LastReport;
            if (r is null) return;
            UsedRam.Text = r.UsedDisplay;
            TotalRam.Text = $"/ {r.TotalDisplay}";
            RamPct.Text = $"{r.UsagePercent}%";
            Reclaimable.Text = $"Reclaimable: {r.ReclaimableDisplay}";
            if (RamBar.Parent is Border parent && parent.Bounds.Width > 0)
                RamBar.Width = parent.Bounds.Width * r.UsagePercent / 100.0;
            OptBtn.IsEnabled = r.TotalReclaimableBytes > 0;
            RebuildProcessList(r);
        }
        catch { SubText.Text = "Scan failed"; }
        finally { ScanLabel.Text = "Scan"; }
    }

    private void RebuildProcessList(RamReport r)
    {
        if (_module is null) return;
        var items = r.TopProcesses.Select(p => new RamProcessItem(
            p.Pid.ToString(), p.Name, p.MemoryDisplay, p.Category,
            new SolidColorBrush(Color.Parse(p.IsEssential ? "#22C55E" : "#555570")),
            _module.IsWhitelisted(p.Name) ? ActiveWhitelistBrush : InactiveBrush,
            _module.IsBlacklisted(p.Name) ? ActiveBlacklistBrush : InactiveBrush
        )).ToList();
        ProcessList.ItemsSource = items;
    }

    private async void Scan_Click(object? sender, RoutedEventArgs e) => await RunScan();

    private async void Optimize_Click(object? sender, RoutedEventArgs e)
    {
        if (_module is null) return;
        OptBtn.IsEnabled = false; OptLabel.Text = "Working...";
        try
        {
            var plan = new OptimizationPlan(_module.Id, new[] { "all" });
            var progress = new Progress<TaskProgress>(p =>
                Dispatcher.UIThread.Post(() => StatusText.Text = p.StatusText));
            var result = await _module.OptimizeAsync(plan, progress);
            StatusText.Text = $"Freed memory from {result.ItemsProcessed} processes in {result.Duration.TotalSeconds:F1}s";
            await RunScan();
        }
        catch (System.Exception ex) { StatusText.Text = ex.Message; }
        finally { OptLabel.Text = "Optimize"; OptBtn.IsEnabled = true; }
    }

    private async void Boost_Click(object? sender, RoutedEventArgs e)
    {
        if (_module is null) return;
        BoostBtn.IsEnabled = false; BoostLabel.Text = "Boosting...";
        try
        {
            var progress = new Progress<TaskProgress>(p =>
                Dispatcher.UIThread.Post(() => StatusText.Text = p.StatusText));
            var result = await _module.BoostOptimizeAsync(progress);
            StatusText.Text = $"Boost freed memory from {result.ItemsProcessed} processes in {result.Duration.TotalSeconds:F1}s";
            await RunScan();
        }
        catch (System.Exception ex) { StatusText.Text = ex.Message; }
        finally { BoostLabel.Text = "Boost"; BoostBtn.IsEnabled = true; }
    }

    private void AutoOpt_Toggle(object? sender, RoutedEventArgs e)
    {
        _autoOptimizeEnabled = AutoOptToggle.IsChecked == true;
        StatusText.Text = _autoOptimizeEnabled
            ? "Auto-optimize enabled (triggers at 85% RAM)"
            : "Auto-optimize disabled";
    }

    private async void ToggleWhitelist_Click(object? sender, RoutedEventArgs e)
    {
        if (_module is null || sender is not Button btn) return;
        var name = btn.Tag?.ToString();
        if (string.IsNullOrEmpty(name)) return;
        _module.ToggleWhitelist(name);
        if (_module.LastReport is not null) RebuildProcessList(_module.LastReport);
    }

    private async void ToggleBlacklist_Click(object? sender, RoutedEventArgs e)
    {
        if (_module is null || sender is not Button btn) return;
        var name = btn.Tag?.ToString();
        if (string.IsNullOrEmpty(name)) return;
        _module.ToggleBlacklist(name);
        if (_module.LastReport is not null) RebuildProcessList(_module.LastReport);
    }

    private void ApplyLocalization()
    {
        PageTitle.Text = LocalizationService._("nav.ramOptimizer");
    }
}
