using Xunit;
using AuraCore.Engine.AIAnalyzer.Models;

namespace AuraCore.Tests.AIEngine;

public class MemoryLeakDetectorTests
{
    [Fact]
    public void Detect_SteadyGrowth_FlagsLeak()
    {
        var detector = new MemoryLeakDetector();
        var samples = new List<ProcessMemorySeries>
        {
            new("Chrome", Enumerable.Range(0, 60)
                .Select(i => (long)(500_000_000 + i * 25_000_000)).ToList())
        };
        var leaks = detector.Detect(samples);
        Assert.Single(leaks);
        Assert.Equal("Chrome", leaks[0].ProcessName);
        Assert.True(leaks[0].GrowthRateMbPerMin > 0);
    }

    [Fact]
    public void Detect_StableMemory_NoLeak()
    {
        var detector = new MemoryLeakDetector();
        var rng = new Random(42);
        var samples = new List<ProcessMemorySeries>
        {
            new("Notepad", Enumerable.Range(0, 60)
                .Select(i => 500_000_000L + rng.Next(-10_000_000, 10_000_000)).ToList())
        };
        var leaks = detector.Detect(samples);
        Assert.Empty(leaks);
    }

    [Fact]
    public void Detect_EmptyInput_ReturnsEmpty()
    {
        var detector = new MemoryLeakDetector();
        var leaks = detector.Detect(new List<ProcessMemorySeries>());
        Assert.Empty(leaks);
    }
}
