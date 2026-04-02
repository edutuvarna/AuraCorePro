using Microsoft.ML;
using Microsoft.ML.Transforms.TimeSeries;

namespace AuraCore.Engine.AIAnalyzer.Models;

public sealed class DiskForecaster
{
    private const int MinDays = 7;
    private const int ForecastHorizon = 30;
    private readonly MLContext _ml = new(seed: 42);

    public ForecastResult? Forecast(IReadOnlyList<float> dailyUsagePercent)
    {
        if (dailyUsagePercent.Count < MinDays) return null;

        var trend = DetermineTrend(dailyUsagePercent);

        var data = dailyUsagePercent.Select(v => new DiskPoint { Usage = v }).ToList();
        var dataView = _ml.Data.LoadFromEnumerable(data);

        var windowSize = Math.Max(2, Math.Min(dailyUsagePercent.Count / 2, 14));

        var pipeline = _ml.Forecasting.ForecastBySsa(
            outputColumnName: nameof(DiskForecast.ForecastedUsage),
            inputColumnName: nameof(DiskPoint.Usage),
            windowSize: windowSize,
            seriesLength: dailyUsagePercent.Count,
            trainSize: dailyUsagePercent.Count,
            horizon: ForecastHorizon,
            confidenceLevel: 0.95f,
            confidenceLowerBoundColumn: "LowerBound",
            confidenceUpperBoundColumn: "UpperBound");

        var model = pipeline.Fit(dataView);
        var engine = model.CreateTimeSeriesEngine<DiskPoint, DiskForecast>(_ml);
        var forecast = engine.Predict();

        var daysUntilFull = -1;
        for (int i = 0; i < forecast.ForecastedUsage.Length; i++)
        {
            if (forecast.ForecastedUsage[i] >= 100f)
            {
                daysUntilFull = i + 1;
                break;
            }
        }

        if (daysUntilFull < 0)
        {
            var lastVal = forecast.ForecastedUsage[^1];
            if (lastVal > dailyUsagePercent[^1] && lastVal < 100f)
            {
                var dailyGrowth = (lastVal - dailyUsagePercent[^1]) / ForecastHorizon;
                daysUntilFull = dailyGrowth > 0.01
                    ? (int)((100f - dailyUsagePercent[^1]) / dailyGrowth)
                    : 999;
            }
            else
            {
                daysUntilFull = 999;
            }
        }

        var confidence = Math.Min(1.0, dailyUsagePercent.Count / 30.0);
        return new ForecastResult(daysUntilFull, Math.Round(confidence, 2), trend);
    }

    private static string DetermineTrend(IReadOnlyList<float> values)
    {
        int n = values.Count;
        double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
        for (int i = 0; i < n; i++)
        {
            sumX += i; sumY += values[i]; sumXY += i * values[i]; sumX2 += i * i;
        }
        var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
        return slope switch { > 0.3 => "increasing", < -0.3 => "decreasing", _ => "stable" };
    }

    private sealed class DiskPoint { public float Usage { get; set; } }
    private sealed class DiskForecast
    {
        public float[] ForecastedUsage { get; set; } = Array.Empty<float>();
        public float[] LowerBound { get; set; } = Array.Empty<float>();
        public float[] UpperBound { get; set; } = Array.Empty<float>();
    }
}
