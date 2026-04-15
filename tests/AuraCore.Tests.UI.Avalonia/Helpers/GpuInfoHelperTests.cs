using AuraCore.UI.Avalonia.Helpers;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Helpers;

public class GpuInfoHelperTests
{
    [Fact]
    public void Detect_DoesNotThrow()
    {
        // Should never throw regardless of platform.
        // Returns null if no GPU detected, otherwise a GpuInfo with non-null Name.
        var result = GpuInfoHelper.Detect();
        if (result is not null)
        {
            Assert.False(string.IsNullOrEmpty(result.Name));
            Assert.InRange(result.UsagePercent, 0.0, 100.0);
        }
    }

    [Fact]
    public void GetCurrentUsage_ReturnsNonNegativeDouble()
    {
        var usage = GpuInfoHelper.GetCurrentUsage();
        Assert.InRange(usage, 0.0, 100.0);
    }

    [Fact]
    public void GpuInfo_Record_StoresAllFields()
    {
        var info = new GpuInfo("Radeon 780M", 28.5, 68.0);
        Assert.Equal("Radeon 780M", info.Name);
        Assert.Equal(28.5, info.UsagePercent);
        Assert.Equal(68.0, info.TemperatureC);
    }
}
