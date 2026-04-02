using Xunit;
using AuraCore.Engine.AIAnalyzer;

namespace AuraCore.Tests.AIEngine;

public class MetricBufferTests
{
    private static MetricSample MakeSample(float cpu = 50f, float ram = 60f, float disk = 70f)
        => new(DateTimeOffset.UtcNow, cpu, ram, disk, Array.Empty<ProcessMetric>());

    [Fact]
    public void Push_SingleSample_ReturnsInSnapshot()
    {
        var buf = new MetricBuffer(capacity: 10);
        buf.Push(MakeSample(cpu: 42f));
        var snap = buf.GetSnapshot();
        Assert.Single(snap);
        Assert.Equal(42f, snap[0].CpuPercent);
    }

    [Fact]
    public void Push_ExceedsCapacity_OldestDropped()
    {
        var buf = new MetricBuffer(capacity: 3);
        buf.Push(MakeSample(cpu: 1f));
        buf.Push(MakeSample(cpu: 2f));
        buf.Push(MakeSample(cpu: 3f));
        buf.Push(MakeSample(cpu: 4f));
        var snap = buf.GetSnapshot();
        Assert.Equal(3, snap.Count);
        Assert.Equal(2f, snap[0].CpuPercent);
        Assert.Equal(4f, snap[2].CpuPercent);
    }

    [Fact]
    public void GetSnapshot_ReturnsChronologicalOrder()
    {
        var buf = new MetricBuffer(capacity: 100);
        var t1 = DateTimeOffset.UtcNow;
        var t2 = t1.AddSeconds(2);
        var t3 = t1.AddSeconds(4);
        buf.Push(new MetricSample(t1, 10f, 20f, 30f, Array.Empty<ProcessMetric>()));
        buf.Push(new MetricSample(t2, 20f, 30f, 40f, Array.Empty<ProcessMetric>()));
        buf.Push(new MetricSample(t3, 30f, 40f, 50f, Array.Empty<ProcessMetric>()));
        var snap = buf.GetSnapshot();
        Assert.Equal(t1, snap[0].Timestamp);
        Assert.Equal(t3, snap[2].Timestamp);
    }

    [Fact]
    public void GetCpuSeries_ReturnsOnlyCpuValues()
    {
        var buf = new MetricBuffer(capacity: 10);
        buf.Push(MakeSample(cpu: 10f));
        buf.Push(MakeSample(cpu: 20f));
        buf.Push(MakeSample(cpu: 30f));
        var series = buf.GetCpuSeries();
        Assert.Equal(3, series.Count);
        Assert.Equal(10f, series[0]);
        Assert.Equal(30f, series[2]);
    }

    [Fact]
    public void GetRamSeries_ReturnsOnlyRamValues()
    {
        var buf = new MetricBuffer(capacity: 10);
        buf.Push(MakeSample(ram: 55f));
        buf.Push(MakeSample(ram: 65f));
        var series = buf.GetRamSeries();
        Assert.Equal(2, series.Count);
        Assert.Equal(55f, series[0]);
        Assert.Equal(65f, series[1]);
    }

    [Fact]
    public void Count_ReturnsCurrentSize()
    {
        var buf = new MetricBuffer(capacity: 5);
        Assert.Equal(0, buf.Count);
        buf.Push(MakeSample());
        buf.Push(MakeSample());
        Assert.Equal(2, buf.Count);
    }

    [Fact]
    public void ThreadSafety_ConcurrentPushAndRead()
    {
        var buf = new MetricBuffer(capacity: 100);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var writer = Task.Run(() =>
        {
            for (int i = 0; i < 1000 && !cts.IsCancellationRequested; i++)
                buf.Push(MakeSample(cpu: i));
        });
        var reader = Task.Run(() =>
        {
            for (int i = 0; i < 100 && !cts.IsCancellationRequested; i++)
            {
                var snap = buf.GetSnapshot();
                Assert.True(snap.Count <= 100);
            }
        });
        Task.WaitAll(writer, reader);
    }
}
