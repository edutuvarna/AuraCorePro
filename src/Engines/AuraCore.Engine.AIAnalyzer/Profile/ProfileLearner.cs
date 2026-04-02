using System.Text.Json;

namespace AuraCore.Engine.AIAnalyzer.Profile;

public sealed class ProfileLearner
{
    private readonly UserProfileStore _profile;
    private readonly MetricBuffer _buffer;
    private int _totalSamples;

    public ProfileLearner(UserProfileStore profile, MetricBuffer buffer)
    {
        _profile = profile;
        _buffer = buffer;
    }

    public void LearnFromBuffer()
    {
        var samples = _buffer.GetSnapshot();
        if (samples.Count < 3) return;

        _totalSamples += samples.Count;
        var confidence = Math.Min(1.0, _totalSamples / 5000.0);

        LearnMetricRange("normal_cpu_range", samples.Select(s => (double)s.CpuPercent).ToArray(), confidence);
        LearnMetricRange("normal_ram_range", samples.Select(s => (double)s.RamPercent).ToArray(), confidence);
        LearnTypicalApps(samples, confidence);
    }

    private void LearnMetricRange(string key, double[] values, double confidence)
    {
        var existing = _profile.Get(key);
        double mean, stddev, min, max;

        if (existing is not null)
        {
            var prev = JsonSerializer.Deserialize<MetricRangeData>(existing.Value)!;
            var alpha = 0.3;
            mean = prev.Mean * (1 - alpha) + values.Average() * alpha;
            stddev = prev.Stddev * (1 - alpha) + StdDev(values) * alpha;
            min = Math.Min(prev.Min, values.Min());
            max = Math.Max(prev.Max, values.Max());
        }
        else
        {
            mean = values.Average();
            stddev = StdDev(values);
            min = values.Min();
            max = values.Max();
        }

        var data = JsonSerializer.Serialize(new MetricRangeData(
            Math.Round(min, 1), Math.Round(max, 1), Math.Round(mean, 1), Math.Round(stddev, 1)));
        _profile.Set(key, data, confidence, _totalSamples);
    }

    private void LearnTypicalApps(IReadOnlyList<MetricSample> samples, double confidence)
    {
        var appStats = new Dictionary<string, (long totalBytes, int appearances)>();

        foreach (var s in samples)
        {
            foreach (var p in s.TopProcesses)
            {
                if (!appStats.ContainsKey(p.Name))
                    appStats[p.Name] = (0, 0);
                var (bytes, count) = appStats[p.Name];
                appStats[p.Name] = (bytes + p.WorkingSetBytes, count + 1);
            }
        }

        if (appStats.Count == 0) return;

        var apps = appStats
            .OrderByDescending(kv => kv.Value.appearances)
            .Take(10)
            .ToDictionary(
                kv => kv.Key,
                kv => new AppProfile(
                    AvgRamMb: Math.Round(kv.Value.totalBytes / (double)kv.Value.appearances / 1024 / 1024, 0),
                    Frequency: Math.Round((double)kv.Value.appearances / samples.Count, 2)));

        var data = JsonSerializer.Serialize(apps);
        _profile.Set("typical_apps", data, confidence, _totalSamples);
    }

    private static double StdDev(double[] values)
    {
        if (values.Length < 2) return 0;
        var avg = values.Average();
        var sumSq = values.Sum(v => (v - avg) * (v - avg));
        return Math.Sqrt(sumSq / (values.Length - 1));
    }

    private sealed record MetricRangeData(double Min, double Max, double Mean, double Stddev);
    private sealed record AppProfile(double AvgRamMb, double Frequency);
}
