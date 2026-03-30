using System.Diagnostics;

namespace AuraCore.UI.Avalonia;

/// <summary>
/// Tracks per-process memory over time. Samples every 5 seconds.
/// Stores last 12 samples (~60 seconds window) per process.
/// Detects memory leaks: consistent upward trend over all samples.
/// </summary>
public sealed class MemoryTrendTracker : IDisposable
{
    public const int MaxSamples = 12;
    private readonly Dictionary<int, ProcessTrend> _trends = new();
    private Timer? _timer;
    private bool _running;

    public sealed record ProcessTrend
    {
        public int Pid { get; init; }
        public string Name { get; init; } = "";
        public List<long> Samples { get; } = new();
        public bool IsSuspectedLeak { get; set; }
        public double GrowthRateMbPerMin { get; set; }
    }

    public bool IsRunning => _running;

    public void Start()
    {
        if (_running) return;
        _running = true;
        _timer = new Timer(Sample, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        _running = false;
    }

    public void Reset() => _trends.Clear();

    public ProcessTrend? GetTrend(int pid)
        => _trends.TryGetValue(pid, out var t) ? t : null;

    public List<ProcessTrend> GetAllTrends()
        => _trends.Values.ToList();

    public List<ProcessTrend> GetSuspectedLeaks()
        => _trends.Values.Where(t => t.IsSuspectedLeak).OrderByDescending(t => t.GrowthRateMbPerMin).ToList();

    private void Sample(object? state)
    {
        try
        {
            var activeIds = new HashSet<int>();
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    var pid = proc.Id;
                    var mem = proc.WorkingSet64;
                    activeIds.Add(pid);

                    if (!_trends.TryGetValue(pid, out var trend))
                    {
                        trend = new ProcessTrend { Pid = pid, Name = proc.ProcessName };
                        _trends[pid] = trend;
                    }

                    trend.Samples.Add(mem);
                    if (trend.Samples.Count > MaxSamples)
                        trend.Samples.RemoveAt(0);

                    if (trend.Samples.Count >= 6)
                        AnalyzeTrend(trend);
                }
                catch { }
                finally { try { proc.Dispose(); } catch { } }
            }

            var dead = _trends.Keys.Where(k => !activeIds.Contains(k)).ToList();
            foreach (var d in dead) _trends.Remove(d);
        }
        catch { }
    }

    private static void AnalyzeTrend(ProcessTrend trend)
    {
        var samples = trend.Samples;
        int increases = 0;
        long totalGrowth = 0;

        for (int i = 1; i < samples.Count; i++)
        {
            var diff = samples[i] - samples[i - 1];
            if (diff > 0) increases++;
            totalGrowth += diff;
        }

        var intervals = samples.Count - 1;
        var growthRatio = (double)increases / intervals;
        var totalGrowthMb = totalGrowth / (1024.0 * 1024);
        var timeSpanMinutes = intervals * 5.0 / 60.0;

        trend.IsSuspectedLeak = growthRatio > 0.75 && totalGrowthMb > 5;
        trend.GrowthRateMbPerMin = timeSpanMinutes > 0 ? totalGrowthMb / timeSpanMinutes : 0;
    }

    public void Dispose() => Stop();
}
