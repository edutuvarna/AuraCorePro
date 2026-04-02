using System.Diagnostics;
using global::Avalonia.Controls;
using global::Avalonia.Media;
using global::Avalonia.Threading;
using AuraCore.Application.Interfaces.Engines;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class AIInsightsView : UserControl
{
    private IAIAnalyzerEngine? _aiEngine;
    private bool _initialized;

    public AIInsightsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
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
            penalties.Add("CPU anomali (-15)");
        }

        if (result.RamAnomaly)
        {
            score -= 15;
            penalties.Add("RAM anomali (-15)");
        }

        foreach (var leak in result.MemoryLeaks)
        {
            score -= 10;
            penalties.Add($"Bellek sizintisi: {leak.ProcessName} (-10)");
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
        if (score >= 85) { label = "Excellent"; color = "#22C55E"; }
        else if (score >= 70) { label = "Good"; color = "#3B82F6"; }
        else if (score >= 50) { label = "Fair"; color = "#F59E0B"; }
        else { label = "Poor"; color = "#EF4444"; }

        HealthBadgeText.Text = label;
        var parsed = Color.Parse(color);
        HealthBadgeText.Foreground = new SolidColorBrush(parsed);
        HealthBadge.Background = new SolidColorBrush(parsed) { Opacity = 0.15 };
        HealthBadge.BorderBrush = new SolidColorBrush(parsed) { Opacity = 0.3 };

        HealthDetails.Text = penalties.Count == 0
            ? "Tum sistemler normal calisiyor."
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
                "\u26A0 CPU Anomali",
                $"Anormal CPU yukselme tespit edildi (skor: {result.CpuAnomalyScore:F2})",
                "#F59E0B"));
        }

        if (result.RamAnomaly)
        {
            hasAlerts = true;
            AlertsPanel.Children.Add(MakeAlertCard(
                "\u26A0 RAM Anomali",
                $"Anormal RAM kullanimi tespit edildi (skor: {result.RamAnomalyScore:F2})",
                "#F59E0B"));
        }

        foreach (var leak in result.MemoryLeaks)
        {
            hasAlerts = true;
            AlertsPanel.Children.Add(MakeAlertCard(
                $"\U0001F50D Bellek Sizintisi: {leak.ProcessName}",
                $"Buyume hizi: {leak.GrowthRateMbPerMin:F2} MB/dk | Skor: {leak.ChangePointScore:F2}",
                "#EF4444"));
        }

        if (!hasAlerts)
        {
            AlertsPanel.Children.Add(new TextBlock
            {
                Text = "Anomali yok \u2713",
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
            DiskTrendText.Text = "Veri toplaniyor...";
            DiskConfidenceText.Text = "";
            DiskConfidenceFill.Width = 0;
            return;
        }

        DiskDaysValue.Text = dp.DaysUntilFull.ToString();
        DiskDaysUnit.Text = "gun";
        DiskTrendText.Text = $"Trend: {dp.Trend}";

        // Color by urgency
        string color;
        if (dp.DaysUntilFull > 90) color = "#22C55E";
        else if (dp.DaysUntilFull > 30) color = "#F59E0B";
        else color = "#EF4444";

        DiskDaysValue.Foreground = new SolidColorBrush(Color.Parse(color));

        // Confidence bar
        DiskConfidenceText.Text = $"Guven: %{dp.Confidence * 100:F0}";
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
                    Text = "Proses verisi alinamadi",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.Parse("#666680"))
                });
                return;
            }

            // Header row
            var header = new Grid { ColumnDefinitions = global::Avalonia.Controls.ColumnDefinitions.Parse("*,Auto") };
            header.Children.Add(new TextBlock
            {
                Text = "Proses",
                FontSize = 9,
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse("#666680"))
            });
            var hdrMem = new TextBlock
            {
                Text = "Bellek",
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

        AnomalyDetectorStatus.Text = engineActive ? "Aktif" : "Inaktif";
        AnomalyDetectorStatus.Foreground = new SolidColorBrush(
            Color.Parse(engineActive ? "#22C55E" : "#EF4444"));

        bool hasDiskData = result?.DiskPrediction != null;
        DiskModelStatus.Text = hasDiskData ? "Aktif" : "Veri bekleniyor";
        DiskModelStatus.Foreground = new SolidColorBrush(
            Color.Parse(hasDiskData ? "#22C55E" : "#F59E0B"));

        ProfileMaturityStatus.Text = "Olusturuluyor";
        ProfileMaturityStatus.Foreground = new SolidColorBrush(Color.Parse("#F59E0B"));

        TelemetryStatus.Text = "Izin verildi";
        TelemetryStatus.Foreground = new SolidColorBrush(Color.Parse("#22C55E"));

        if (result != null)
        {
            var local = result.Timestamp.ToLocalTime();
            LastAnalysisTime.Text = local.ToString("HH:mm:ss dd/MM/yyyy");
            LastAnalysisTime.Foreground = new SolidColorBrush(Color.Parse("#A0A0C0"));
        }
        else
        {
            LastAnalysisTime.Text = "Henuz analiz yapilmadi";
            LastAnalysisTime.Foreground = new SolidColorBrush(Color.Parse("#666680"));
        }
    }
}
