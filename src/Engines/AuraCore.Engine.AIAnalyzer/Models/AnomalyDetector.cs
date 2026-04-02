using Microsoft.ML;
using Microsoft.ML.TimeSeries;

namespace AuraCore.Engine.AIAnalyzer.Models;

public sealed class AnomalyDetector
{
    private const int MinSamples = 12;
    private readonly MLContext _ml = new(seed: 42);

    public IReadOnlyList<AnomalyResult> Detect(IReadOnlyList<float> series)
    {
        if (series.Count < MinSamples)
            return series.Select(_ => new AnomalyResult(false, 0, 0)).ToList();

        var data = series.Select(v => new TimeSeriesPoint { Value = v }).ToList();
        var dataView = _ml.Data.LoadFromEnumerable(data);

        var windowSize = Math.Max(4, Math.Min(64, series.Count / 4));
        var backAddWindowSize = Math.Max(1, Math.Min(5, windowSize / 2));
        var lookaheadWindowSize = Math.Max(1, Math.Min(5, windowSize / 2));
        var judgementWindowSize = Math.Max(1, Math.Min(24, windowSize));

        var pipeline = _ml.Transforms.DetectAnomalyBySrCnn(
            outputColumnName: nameof(SrCnnAnomalyOutput.Prediction),
            inputColumnName: nameof(TimeSeriesPoint.Value),
            windowSize: windowSize,
            backAddWindowSize: backAddWindowSize,
            lookaheadWindowSize: lookaheadWindowSize,
            averagingWindowSize: 3,
            judgementWindowSize: judgementWindowSize,
            threshold: 0.3);

        var model = pipeline.Fit(dataView);
        var transformed = model.Transform(dataView);
        var predictions = _ml.Data.CreateEnumerable<SrCnnAnomalyOutput>(transformed, reuseRowObject: false).ToList();

        return predictions.Select(p => new AnomalyResult(
            IsAnomaly: p.Prediction[0] != 0,
            Score: p.Prediction[1],
            ExpectedValue: p.Prediction[2]
        )).ToList();
    }

    private sealed class TimeSeriesPoint { public float Value { get; set; } }
    private sealed class SrCnnAnomalyOutput { public double[] Prediction { get; set; } = Array.Empty<double>(); }
}
