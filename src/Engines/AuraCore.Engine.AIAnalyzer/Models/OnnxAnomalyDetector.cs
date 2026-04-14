using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace AuraCore.Engine.AIAnalyzer.Models;

public sealed class OnnxAnomalyDetector : IDisposable
{
    private readonly InferenceSession? _session;
    private readonly AnomalyDetector _fallback = new();
    private readonly double _threshold;
    private readonly double _mean;
    private readonly double _std;
    private readonly int _windowSize;

    public bool IsOnnxAvailable => _session is not null;

    public OnnxAnomalyDetector(string? onnxModelPath, string? thresholdConfigPath = null)
    {
        // Load threshold config
        _threshold = 0.5;
        _mean = 0.0;
        _std = 1.0;
        _windowSize = 64;

        if (thresholdConfigPath is not null && File.Exists(thresholdConfigPath))
        {
            try
            {
                var json = File.ReadAllText(thresholdConfigPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("threshold", out var t))
                    _threshold = t.GetDouble();
                if (root.TryGetProperty("mean", out var m))
                    _mean = m.GetDouble();
                if (root.TryGetProperty("std", out var s) && s.GetDouble() > 0)
                    _std = s.GetDouble();
                if (root.TryGetProperty("windowSize", out var w))
                    _windowSize = w.GetInt32();
            }
            catch
            {
                // keep defaults
            }
        }

        // Load ONNX model
        if (onnxModelPath is not null && File.Exists(onnxModelPath))
        {
            try
            {
                _session = new InferenceSession(onnxModelPath);
            }
            catch
            {
                _session = null;
            }
        }
    }

    public IReadOnlyList<AnomalyResult> Detect(IReadOnlyList<float> series)
    {
        if (!IsOnnxAvailable || series.Count < _windowSize)
            return _fallback.Detect(series);

        try
        {
            return DetectWithOnnx(series);
        }
        catch
        {
            // ONNX inference failed — fallback to SR-CNN
            return _fallback.Detect(series);
        }
    }

    private IReadOnlyList<AnomalyResult> DetectWithOnnx(IReadOnlyList<float> series)
    {
        var results = new AnomalyResult[series.Count];

        // Initialize leading elements as non-anomalous
        for (int i = 0; i < _windowSize - 1 && i < series.Count; i++)
            results[i] = new AnomalyResult(false, 0, series[i]);

        // Sliding window inference
        for (int i = 0; i <= series.Count - _windowSize; i++)
        {
            // Build normalized window
            var window = new float[_windowSize];
            for (int j = 0; j < _windowSize; j++)
                window[j] = (float)((series[i + j] - _mean) / _std);

            // Create tensor [1, windowSize]
            var tensor = new DenseTensor<float>(window, new[] { 1, _windowSize });
            var inputName = _session!.InputMetadata.Keys.First();
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputName, tensor)
            };

            using var output = _session.Run(inputs);
            var reconstructed = output.First().AsTensor<float>();

            // Compute MSE for the window
            double mse = 0;
            for (int j = 0; j < _windowSize; j++)
            {
                var diff = window[j] - reconstructed[0, j];
                mse += diff * diff;
            }
            mse /= _windowSize;

            // Assign result to the last element of the window
            int idx = i + _windowSize - 1;
            bool isAnomaly = mse > _threshold;
            results[idx] = new AnomalyResult(isAnomaly, mse, series[idx]);
        }

        return results;
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}
