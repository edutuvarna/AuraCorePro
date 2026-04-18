using System.Collections.Generic;
using AuraCore.UI.Avalonia.Helpers;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Dashboard;

public class DiskHealthScannerSmartTests
{
    [Fact]
    public void PickWorstTempCelsius_returns_highest_drive_temp()
    {
        var samples = new List<DiskHealthScanner.SmartSample>
        {
            new("disk0", 38),
            new("disk1", 45),
            new("disk2", 32),
        };
        var worst = DiskHealthScanner.PickWorstTempCelsius(samples);
        Assert.Equal(45, worst);
    }

    [Fact]
    public void PickWorstTempCelsius_on_empty_returns_null()
    {
        var worst = DiskHealthScanner.PickWorstTempCelsius(new List<DiskHealthScanner.SmartSample>());
        Assert.Null(worst);
    }

    [Fact]
    public void PickWorstTempCelsius_on_null_returns_null()
    {
        var worst = DiskHealthScanner.PickWorstTempCelsius(null!);
        Assert.Null(worst);
    }

    [Fact]
    public void FormatWorstTemp_null_returns_placeholder_dash()
    {
        Assert.Equal("—", DiskHealthScanner.FormatWorstTemp(null));
    }

    [Fact]
    public void FormatWorstTemp_value_returns_celsius_string()
    {
        Assert.Equal("45°C", DiskHealthScanner.FormatWorstTemp(45));
        Assert.Equal("0°C", DiskHealthScanner.FormatWorstTemp(0));
    }

    [Fact]
    public void ScanCore_with_throwing_probe_falls_back_to_placeholder()
    {
        var probe = new ThrowingSmartProbe();
        var result = DiskHealthScanner.ScanCore(probe);
        Assert.Equal("—", result.WorstTempText);
    }

    [Fact]
    public void ScanCore_with_samples_includes_worst_temp_formatted()
    {
        var probe = new StaticSmartProbe(new[]
        {
            new DiskHealthScanner.SmartSample("d0", 40),
            new DiskHealthScanner.SmartSample("d1", 55),
        });
        var result = DiskHealthScanner.ScanCore(probe);
        Assert.Equal("55°C", result.WorstTempText);
    }

    private sealed class ThrowingSmartProbe : DiskHealthScanner.ISmartProbe
    {
        public IReadOnlyList<DiskHealthScanner.SmartSample> Sample()
            => throw new System.Exception("boom");
    }

    private sealed class StaticSmartProbe : DiskHealthScanner.ISmartProbe
    {
        private readonly IReadOnlyList<DiskHealthScanner.SmartSample> _samples;
        public StaticSmartProbe(IReadOnlyList<DiskHealthScanner.SmartSample> samples) { _samples = samples; }
        public IReadOnlyList<DiskHealthScanner.SmartSample> Sample() => _samples;
    }
}
