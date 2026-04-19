using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.Application.Interfaces.Engines;
using AuraCore.UI.Avalonia.Services.AI;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Services.AI;

/// <summary>
/// Regression gate for the AI Insights fix (see
/// project_phase_6_item_X_ai_insights_fix_complete.md). The engine was
/// registered but never driven; InsightsSection stayed on "Cortex is learning"
/// forever. This service fixes that by feeding samples + triggering analysis.
/// Tests verify the loop pushes + analyzes on cadence.
/// </summary>
public class AIMetricsCollectorServiceTests
{
    [Fact]
    public async Task Loop_pushes_samples_at_configured_cadence()
    {
        var engine = new RecordingEngine();
        using var svc = new AIMetricsCollectorService(
            engine,
            sampleInterval: TimeSpan.FromMilliseconds(20),
            analyzeInterval: TimeSpan.FromMilliseconds(500))
        {
            CpuSampler = () => 42f,
            RamSampler = () => 51f,
            DiskSampler = () => 68f,
        };

        svc.Start();
        await Task.Delay(250);
        await svc.StopAsync();

        Assert.True(engine.PushCount >= 3, $"Expected at least 3 pushes after 250ms at 20ms cadence, got {engine.PushCount}");

        var sample = engine.Samples[0];
        Assert.Equal(42f, sample.CpuPercent);
        Assert.Equal(51f, sample.RamPercent);
        Assert.Equal(68f, sample.DiskUsedPercent);
        Assert.Empty(sample.TopProcesses);
    }

    [Fact]
    public async Task Loop_triggers_analyze_on_cadence()
    {
        var engine = new RecordingEngine();
        using var svc = new AIMetricsCollectorService(
            engine,
            sampleInterval: TimeSpan.FromMilliseconds(20),
            analyzeInterval: TimeSpan.FromMilliseconds(100))
        {
            // Use trivial samplers — default CpuSampler opens a Windows
            // PerformanceCounter which has nontrivial first-call cost and
            // can throw under test-runner sandboxes.
            CpuSampler = () => 10f,
            RamSampler = () => 20f,
            DiskSampler = () => 30f,
        };

        svc.Start();
        await Task.Delay(600);
        await svc.StopAsync();

        Assert.True(engine.AnalyzeCount >= 2,
            $"Expected >=2 AnalyzeAsync calls after 600ms with 100ms interval. " +
            $"Got AnalyzeCount={engine.AnalyzeCount}, PushCount={engine.PushCount}.");
    }

    [Fact]
    public async Task Sampler_exception_does_not_kill_the_loop()
    {
        var engine = new RecordingEngine();
        var throwOnce = 0;
        using var svc = new AIMetricsCollectorService(
            engine,
            sampleInterval: TimeSpan.FromMilliseconds(20),
            analyzeInterval: TimeSpan.FromMilliseconds(10_000))
        {
            CpuSampler = () =>
            {
                if (Interlocked.Increment(ref throwOnce) == 1)
                    throw new InvalidOperationException("simulated sampler failure");
                return 50f;
            },
        };

        svc.Start();
        await Task.Delay(200);
        await svc.StopAsync();

        Assert.True(engine.PushCount >= 3,
            $"Loop should keep ticking after a sampler throw; got {engine.PushCount} pushes");
    }

    [Fact]
    public async Task StopAsync_terminates_the_loop()
    {
        var engine = new RecordingEngine();
        using var svc = new AIMetricsCollectorService(
            engine,
            sampleInterval: TimeSpan.FromMilliseconds(20),
            analyzeInterval: TimeSpan.FromSeconds(10));

        svc.Start();
        await Task.Delay(100);
        await svc.StopAsync();

        var countAtStop = engine.PushCount;
        await Task.Delay(150);
        var countAfterStop = engine.PushCount;

        Assert.True(countAfterStop - countAtStop <= 1,
            $"Loop should stop pushing after StopAsync; saw {countAfterStop - countAtStop} extra pushes");
    }

    [Fact]
    public async Task Start_is_idempotent()
    {
        var engine = new RecordingEngine();
        using var svc = new AIMetricsCollectorService(
            engine,
            sampleInterval: TimeSpan.FromMilliseconds(50),
            analyzeInterval: TimeSpan.FromSeconds(10));

        svc.Start();
        svc.Start();
        svc.Start();
        await Task.Delay(200);
        await svc.StopAsync();

        // A single loop produces ~4 pushes in 200ms at 50ms cadence.
        // Three concurrent loops would produce ~12. Enforce single loop.
        Assert.InRange(engine.PushCount, 2, 7);
    }

    // ── Test double ─────────────────────────────────────────────────

    private sealed class RecordingEngine : IAIAnalyzerEngine
    {
        private int _pushCount;
        private int _analyzeCount;
        private readonly List<AIMetricSample> _samples = new();
        private readonly object _lock = new();

        public int PushCount => _pushCount;
        public int AnalyzeCount => _analyzeCount;

        public IReadOnlyList<AIMetricSample> Samples
        {
            get { lock (_lock) return _samples.ToArray(); }
        }

        public AIAnalysisResult? LatestResult => null;

#pragma warning disable CS0067 // Event is never used — interface contract requirement.
        public event Action<AIAnalysisResult>? AnalysisCompleted;
#pragma warning restore CS0067

        public void Push(AIMetricSample sample)
        {
            Interlocked.Increment(ref _pushCount);
            lock (_lock) _samples.Add(sample);
        }

        public Task<AIAnalysisResult> AnalyzeAsync(CancellationToken ct = default)
        {
            Interlocked.Increment(ref _analyzeCount);
            return Task.FromResult(new AIAnalysisResult(
                DateTimeOffset.UtcNow, false, 0, false, 0, null, Array.Empty<MemoryLeakAlert>()));
        }
    }
}
