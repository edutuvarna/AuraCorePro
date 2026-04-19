using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.Application.Interfaces.Engines;

namespace AuraCore.UI.Avalonia.Services.AI;

/// <summary>
/// Background driver for <see cref="IAIAnalyzerEngine"/>. Samples CPU/RAM/Disk
/// on a cheap cadence, pushes each sample into the engine's metric buffer, and
/// triggers <see cref="IAIAnalyzerEngine.AnalyzeAsync"/> every 60s.
///
/// Before this existed the engine was registered in DI and UI sections
/// (Dashboard Cortex card, InsightsSection) subscribed to
/// <see cref="IAIAnalyzerEngine.AnalysisCompleted"/>, but nobody drove the
/// engine — no Push, no AnalyzeAsync. InsightsSection stayed on the
/// "Cortex is learning" placeholder forever because the event never fired.
///
/// Singleton scope. Started once at App.Initialize() after the DI container
/// is built; runs until app shutdown.
/// </summary>
public sealed class AIMetricsCollectorService : IDisposable
{
    private readonly IAIAnalyzerEngine _engine;
    private readonly CancellationTokenSource _cts = new();
    private readonly TimeSpan _sampleInterval;
    private readonly TimeSpan _analyzeInterval;
    private Task? _loop;

    // Sampling overrides — production defaults are platform-specific OS reads.
    // Tests swap these for deterministic values before Start().
    internal Func<float> CpuSampler { get; set; } = SampleCpuDefault;
    internal Func<float> RamSampler { get; set; } = SampleRamDefault;
    internal Func<float> DiskSampler { get; set; } = SampleDiskDefault;
    internal Func<DateTimeOffset> NowProvider { get; set; } = () => DateTimeOffset.UtcNow;

    public AIMetricsCollectorService(
        IAIAnalyzerEngine engine,
        TimeSpan? sampleInterval = null,
        TimeSpan? analyzeInterval = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _sampleInterval = sampleInterval ?? TimeSpan.FromSeconds(2);
        _analyzeInterval = analyzeInterval ?? TimeSpan.FromSeconds(60);
    }

    /// <summary>
    /// Spawns the background loop. Idempotent — second call is a no-op.
    /// </summary>
    public void Start()
    {
        if (_loop is not null) return;
        _loop = Task.Run(() => RunLoop(_cts.Token));
    }

    /// <summary>
    /// Signals the loop to stop and returns a task that completes when it has.
    /// </summary>
    public Task StopAsync()
    {
        try { _cts.Cancel(); } catch { }
        return _loop ?? Task.CompletedTask;
    }

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { }
        _cts.Dispose();
    }

    private async Task RunLoop(CancellationToken ct)
    {
        var lastAnalyze = NowProvider();
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var sample = new AIMetricSample(
                    NowProvider(),
                    CpuSampler(),
                    RamSampler(),
                    DiskSampler(),
                    Array.Empty<AIProcessMetric>());
                _engine.Push(sample);

                if (NowProvider() - lastAnalyze >= _analyzeInterval)
                {
                    try
                    {
                        await _engine.AnalyzeAsync(ct).ConfigureAwait(false);
                    }
                    catch { /* analysis exceptions shouldn't kill the loop */ }
                    lastAnalyze = NowProvider();
                }
            }
            catch
            {
                // Defensive — a sampler exception should never kill the loop.
                // Dropping one sample is fine; the next tick will try again.
            }

            try { await Task.Delay(_sampleInterval, ct).ConfigureAwait(false); }
            catch { break; }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  Default sampler implementations
    //
    //  Deliberately simple + cheap. Dashboard.TickOnce has richer versions
    //  (incl. GPU + process enumeration) but those are UI-rate and would be
    //  wasteful here. Samples produced by this service only need to
    //  characterise CPU/RAM/Disk trends for anomaly detection.
    // ══════════════════════════════════════════════════════════════════

    private static System.Diagnostics.PerformanceCounter? _cpuCounter;

    private static float SampleCpuDefault()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                if (_cpuCounter is null)
                {
                    _cpuCounter = new System.Diagnostics.PerformanceCounter(
                        "Processor", "% Processor Time", "_Total", readOnly: true);
                    _cpuCounter.NextValue(); // prime — first read always 0
                    return 0f;
                }
                return Math.Clamp(_cpuCounter.NextValue(), 0f, 100f);
            }
            catch
            {
                _cpuCounter = null;
                return 0f;
            }
        }
        if (OperatingSystem.IsLinux())
        {
            try
            {
                // /proc/stat line 1: cpu  user nice system idle iowait irq softirq steal
                var first = File.ReadLines("/proc/stat").First();
                var parts = first.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5 || parts[0] != "cpu") return 0f;
                ulong idle = ulong.Parse(parts[4]);
                ulong total = 0;
                for (int i = 1; i < parts.Length; i++) total += ulong.Parse(parts[i]);
                return Math.Clamp((float)(100.0 * (total - idle) / Math.Max(total, 1UL)), 0f, 100f);
            }
            catch { return 0f; }
        }
        // macOS / other: future work. Return 0 so samples still flow.
        return 0f;
    }

    private static float SampleRamDefault()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var ms = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
                if (GlobalMemoryStatusEx(ref ms) && ms.ullTotalPhys > 0)
                    return (float)(100.0 * (ms.ullTotalPhys - ms.ullAvailPhys) / ms.ullTotalPhys);
            }
            catch { }
            return 0f;
        }
        if (OperatingSystem.IsLinux())
        {
            try
            {
                ulong total = 0, avail = 0;
                foreach (var line in File.ReadAllLines("/proc/meminfo"))
                {
                    if (line.StartsWith("MemTotal:")) total = ParseKb(line);
                    else if (line.StartsWith("MemAvailable:")) avail = ParseKb(line);
                }
                if (total > 0) return (float)(100.0 * (total - avail) / total);
            }
            catch { }
        }
        return 0f;
    }

    private static float SampleDiskDefault()
    {
        try
        {
            var root = OperatingSystem.IsWindows() ? "C:\\" : "/";
            var di = new DriveInfo(root);
            if (di.TotalSize > 0)
                return (float)(100.0 * (di.TotalSize - di.AvailableFreeSpace) / di.TotalSize);
        }
        catch { }
        return 0f;
    }

    private static ulong ParseKb(string line)
    {
        var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && ulong.TryParse(parts[1], out var v) ? v * 1024 : 0;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
