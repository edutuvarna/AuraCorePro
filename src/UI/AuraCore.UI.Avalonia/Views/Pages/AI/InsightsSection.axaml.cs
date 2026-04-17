using System.Diagnostics;
using global::Avalonia.Controls;
using global::Avalonia.Media;
using global::Avalonia.Threading;
using AuraCore.Application.Interfaces.Engines;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages.AI;

public partial class InsightsSection : UserControl
{
    private IAIAnalyzerEngine? _aiEngine;
    private bool _initialized;

    public InsightsSection()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        LocalizationService.LanguageChanged += () =>
            Dispatcher.UIThread.Post(ApplyLocalization);
    }

    private void OnLoaded(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;

        try { _aiEngine = App.Services.GetService<IAIAnalyzerEngine>(); } catch { }

        if (_aiEngine != null)
        {
            _aiEngine.AnalysisCompleted += OnAnalysisCompleted;

            // Populate immediately from latest result
            if (_aiEngine.LatestResult is { } latest)
                UpdateAllSections(latest);
        }

        ApplyLocalization();
        UpdateAIStatus();
    }

    private void ApplyLocalization()
    {
        // Hero
        PageTitle.Text = LocalizationService._("ai.title");
        HeroSubtitle.Text = LocalizationService._("ai.subtitle");
        EngineStatusLabel.Text = LocalizationService._("ai.engine");

        // Section titles
        HealthSectionTitle.Text = LocalizationService._("ai.health.title");
        AlertsSectionTitle.Text = LocalizationService._("ai.alerts.title");
        DiskSectionTitle.Text = LocalizationService._("ai.disk.title");
        MemorySectionTitle.Text = LocalizationService._("ai.memory.title");
        ProfileSectionTitle.Text = LocalizationService._("ai.profile.title");
        StatusSectionTitle.Text = LocalizationService._("ai.status.title");

        // Status labels
        AnomalyDetectorLabel.Text = LocalizationService._("ai.status.anomalyDetector");
        DiskModelLabel.Text = LocalizationService._("ai.status.diskModel");
        ProfileMaturityLabel.Text = LocalizationService._("ai.status.profileMaturity");
        TelemetryLabel.Text = LocalizationService._("ai.status.telemetryPermission");
        LastAnalysisLabel.Text = LocalizationService._("ai.status.lastAnalysis");

        // Footer
        AutoRefreshText.Text = LocalizationService._("ai.status.autoRefresh");

        // Defaults for waiting states (if no result yet)
        if (HealthBadgeText.Text == "" || HealthBadgeText.Text == null)
            HealthBadgeText.Text = LocalizationService._("ai.health.waiting");
        if (HealthDetails.Text == "" || HealthDetails.Text == null)
            HealthDetails.Text = LocalizationService._("ai.health.firstWaiting");
        if (ProfileStatusText.Text == "" || ProfileStatusText.Text == null)
            ProfileStatusText.Text = LocalizationService._("ai.profile.collecting");
        if (DiskTrendText.Text == "" || DiskTrendText.Text == null)
            DiskTrendText.Text = LocalizationService._("ai.disk.collecting");
        if (DiskDaysUnit.Text == "" || DiskDaysUnit.Text == null)
            DiskDaysUnit.Text = LocalizationService._("ai.disk.days");

        // Re-apply dynamic data if engine has a result
        if (_aiEngine?.LatestResult is { } result)
            UpdateAllSections(result);
        else
            UpdateAIStatus();
    }

    private void OnUnloaded(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_aiEngine != null)
            _aiEngine.AnalysisCompleted -= OnAnalysisCompleted;
    }

    private void OnAnalysisCompleted(AIAnalysisResult result)
    {
        Dispatcher.UIThread.Post(() => UpdateAllSections(result));
    }

    private void UpdateAllSections(AIAnalysisResult result)
    {
        UpdateHealthScore(result);
        UpdateAlerts(result);
        UpdateDiskPrediction(result);
        UpdateMemoryAnalysis(result);
        UpdateAIStatus(result);
    }

    // ── SECTION 1: Health Score ──

    private void UpdateHealthScore(AIAnalysisResult result)
    {
        int score = 100;
        var penalties = new List<string>();

        if (result.CpuAnomaly)
        {
            score -= 15;
            penalties.Add(LocalizationService._("ai.penalty.cpu"));
        }

        if (result.RamAnomaly)
        {
            score -= 15;
            penalties.Add(LocalizationService._("ai.penalty.ram"));
        }

        foreach (var leak in result.MemoryLeaks)
        {
            score -= 10;
            penalties.Add(string.Format(LocalizationService._("ai.penalty.leak"), leak.ProcessName));
        }

        if (result.DiskPrediction is { } dp)
        {
            if (dp.DaysUntilFull < 30)
            {
                score -= 20;
                penalties.Add("Disk <30 gun (-20)");
            }
            else if (dp.DaysUntilFull < 90)
            {
                score -= 10;
                penalties.Add("Disk 30-90 gun (-10)");
            }
        }

        score = Math.Max(score, 0);
        HealthScoreValue.Text = score.ToString();

        // Badge
        string label, color;
        if (score >= 85) { label = LocalizationService._("ai.health.excellent"); color = "#22C55E"; }
        else if (score >= 70) { label = LocalizationService._("ai.health.good"); color = "#3B82F6"; }
        else if (score >= 50) { label = LocalizationService._("ai.health.fair"); color = "#F59E0B"; }
        else { label = LocalizationService._("ai.health.poor"); color = "#EF4444"; }

        HealthBadgeText.Text = label;
        var parsed = Color.Parse(color);
        HealthBadgeText.Foreground = new SolidColorBrush(parsed);
        HealthBadge.Background = new SolidColorBrush(parsed) { Opacity = 0.15 };
        HealthBadge.BorderBrush = new SolidColorBrush(parsed) { Opacity = 0.3 };

        HealthDetails.Text = penalties.Count == 0
            ? LocalizationService._("ai.health.allNormal")
            : string.Join(" | ", penalties);
    }

    // ── SECTION 2: Active Alerts ──

    private void UpdateAlerts(AIAnalysisResult result)
    {
        AlertsPanel.Children.Clear();

        bool hasAlerts = false;

        if (result.CpuAnomaly)
        {
            hasAlerts = true;
            AlertsPanel.Children.Add(MakeAlertCard(
                LocalizationService._("ai.alerts.cpuAnomaly"),
                string.Format(LocalizationService._("ai.alerts.cpuAnomalyDesc"), result.CpuAnomalyScore.ToString("F2")),
                "#F59E0B"));
        }

        if (result.RamAnomaly)
        {
            hasAlerts = true;
            AlertsPanel.Children.Add(MakeAlertCard(
                LocalizationService._("ai.alerts.ramAnomaly"),
                string.Format(LocalizationService._("ai.alerts.ramAnomalyDesc"), result.RamAnomalyScore.ToString("F2")),
                "#F59E0B"));
        }

        foreach (var leak in result.MemoryLeaks)
        {
            hasAlerts = true;
            AlertsPanel.Children.Add(MakeAlertCard(
                string.Format(LocalizationService._("ai.alerts.memoryLeak"), leak.ProcessName),
                string.Format(LocalizationService._("ai.alerts.memoryLeakDesc"), leak.GrowthRateMbPerMin.ToString("F2"), leak.ChangePointScore.ToString("F2")),
                "#EF4444"));
        }

        if (!hasAlerts)
        {
            AlertsPanel.Children.Add(new TextBlock
            {
                Text = LocalizationService._("ai.alerts.noAnomalies"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.Parse("#22C55E")),
                FontWeight = FontWeight.SemiBold
            });
        }
    }

    private static Border MakeAlertCard(string title, string detail, string color)
    {
        var parsed = Color.Parse(color);
        var card = new Border
        {
            CornerRadius = new global::Avalonia.CornerRadius(8),
            Padding = new global::Avalonia.Thickness(10, 8),
            BorderThickness = new global::Avalonia.Thickness(1),
            Background = new SolidColorBrush(parsed) { Opacity = 0.08 },
            BorderBrush = new SolidColorBrush(parsed) { Opacity = 0.2 }
        };

        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(parsed)
        });
        stack.Children.Add(new TextBlock
        {
            Text = detail,
            FontSize = 9,
            Foreground = new SolidColorBrush(Color.Parse("#A0A0C0"))
        });
        card.Child = stack;
        return card;
    }

    // ── SECTION 3: Disk Prediction ──

    private void UpdateDiskPrediction(AIAnalysisResult result)
    {
        if (result.DiskPrediction is not { } dp)
        {
            DiskDaysValue.Text = "--";
            DiskDaysUnit.Text = "";
            DiskTrendText.Text = LocalizationService._("ai.disk.collecting");
            DiskConfidenceText.Text = "";
            DiskConfidenceFill.Width = 0;
            return;
        }

        DiskDaysValue.Text = dp.DaysUntilFull.ToString();
        DiskDaysUnit.Text = LocalizationService._("ai.disk.days");
        DiskTrendText.Text = $"Trend: {dp.Trend}";

        // Color by urgency
        string color;
        if (dp.DaysUntilFull > 90) color = "#22C55E";
        else if (dp.DaysUntilFull > 30) color = "#F59E0B";
        else color = "#EF4444";

        DiskDaysValue.Foreground = new SolidColorBrush(Color.Parse(color));

        // Confidence bar
        DiskConfidenceText.Text = string.Format(LocalizationService._("ai.disk.confidence"), (dp.Confidence * 100).ToString("F0"));
        DiskConfidenceFill.Background = new SolidColorBrush(Color.Parse(color));
        if (DiskConfidenceBar.Bounds.Width > 0)
            DiskConfidenceFill.Width = DiskConfidenceBar.Bounds.Width * dp.Confidence;
    }

    // ── SECTION 4: Memory Analysis ──

    private void UpdateMemoryAnalysis(AIAnalysisResult result)
    {
        MemoryProcessPanel.Children.Clear();

        // Get top 5 processes from latest metric sample via system snapshot
        var leakNames = new HashSet<string>(
            result.MemoryLeaks.Select(l => l.ProcessName),
            StringComparer.OrdinalIgnoreCase);

        try
        {
            var procs = Process.GetProcesses();
            var top5 = procs
                .Select(p =>
                {
                    try { return new { p.ProcessName, p.WorkingSet64 }; }
                    catch { return null; }
                })
                .Where(x => x != null)
                .OrderByDescending(x => x!.WorkingSet64)
                .Take(5)
                .ToList();

            foreach (var p2 in procs)
                try { p2.Dispose(); } catch { }

            if (top5.Count == 0)
            {
                MemoryProcessPanel.Children.Add(new TextBlock
                {
                    Text = LocalizationService._("ai.memory.unavailable"),
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.Parse("#666680"))
                });
                return;
            }

            // Header row
            var header = new Grid { ColumnDefinitions = global::Avalonia.Controls.ColumnDefinitions.Parse("*,Auto") };
            header.Children.Add(new TextBlock
            {
                Text = LocalizationService._("ai.memory.process"),
                FontSize = 9,
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse("#666680"))
            });
            var hdrMem = new TextBlock
            {
                Text = LocalizationService._("ai.memory.memoryCol"),
                FontSize = 9,
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse("#666680")),
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right
            };
            Grid.SetColumn(hdrMem, 1);
            header.Children.Add(hdrMem);
            MemoryProcessPanel.Children.Add(header);

            foreach (var p in top5)
            {
                bool isLeak = leakNames.Contains(p!.ProcessName);
                var foreground = isLeak
                    ? new SolidColorBrush(Color.Parse("#EF4444"))
                    : new SolidColorBrush(Color.Parse("#A0A0C0"));

                var row = new Grid { ColumnDefinitions = global::Avalonia.Controls.ColumnDefinitions.Parse("*,Auto") };

                var nameText = new TextBlock
                {
                    Text = isLeak ? $"{p.ProcessName} \u26A0" : p.ProcessName,
                    FontSize = 10,
                    FontWeight = isLeak ? FontWeight.SemiBold : FontWeight.Normal,
                    Foreground = foreground
                };
                row.Children.Add(nameText);

                var memMb = p.WorkingSet64 / (1024.0 * 1024);
                var memText = new TextBlock
                {
                    Text = memMb >= 1024 ? $"{memMb / 1024:F1} GB" : $"{memMb:F0} MB",
                    FontSize = 10,
                    FontWeight = FontWeight.Medium,
                    Foreground = foreground,
                    HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right
                };
                Grid.SetColumn(memText, 1);
                row.Children.Add(memText);

                MemoryProcessPanel.Children.Add(row);
            }
        }
        catch
        {
            MemoryProcessPanel.Children.Add(new TextBlock
            {
                Text = "Proses verisi alinamadi",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.Parse("#666680"))
            });
        }
    }

    // ── SECTION 6: AI Status ──

    private void UpdateAIStatus(AIAnalysisResult? result = null)
    {
        bool engineActive = _aiEngine != null;

        AnomalyDetectorStatus.Text = engineActive
            ? LocalizationService._("ai.status.active")
            : LocalizationService._("ai.status.inactive");
        AnomalyDetectorStatus.Foreground = new SolidColorBrush(
            Color.Parse(engineActive ? "#22C55E" : "#EF4444"));

        bool hasDiskData = result?.DiskPrediction != null;
        DiskModelStatus.Text = hasDiskData
            ? LocalizationService._("ai.status.active")
            : LocalizationService._("ai.status.waitingData");
        DiskModelStatus.Foreground = new SolidColorBrush(
            Color.Parse(hasDiskData ? "#22C55E" : "#F59E0B"));

        ProfileMaturityStatus.Text = LocalizationService._("ai.status.creating");
        ProfileMaturityStatus.Foreground = new SolidColorBrush(Color.Parse("#F59E0B"));

        TelemetryStatus.Text = LocalizationService._("ai.status.granted");
        TelemetryStatus.Foreground = new SolidColorBrush(Color.Parse("#22C55E"));

        if (result != null)
        {
            var local = result.Timestamp.ToLocalTime();
            LastAnalysisTime.Text = local.ToString("HH:mm:ss dd/MM/yyyy");
            LastAnalysisTime.Foreground = new SolidColorBrush(Color.Parse("#A0A0C0"));
        }
        else
        {
            LastAnalysisTime.Text = LocalizationService._("ai.status.noAnalysis");
            LastAnalysisTime.Foreground = new SolidColorBrush(Color.Parse("#666680"));
        }
    }
}
