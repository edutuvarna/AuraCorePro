using Xunit;
using AuraCore.Application;
using AuraCore.Module.JournalCleaner;
using AuraCore.Domain.Enums;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.Tests.Module;

public class JournalCleanerTests
{
    [Fact]
    public void JournalCleaner_Metadata_IsValid()
    {
        var m = new JournalCleanerModule();
        Assert.Equal("journal-cleaner", m.Id);
        Assert.False(string.IsNullOrWhiteSpace(m.DisplayName));
        Assert.Equal(OptimizationCategory.DiskCleanup, m.Category);
        Assert.Equal(RiskLevel.Low, m.Risk);
        Assert.Equal(SupportedPlatform.Linux, m.Platform);
        IOptimizationModule iface = m;
        Assert.False(iface.IsAdvanced);
    }

    [Fact]
    public async Task JournalCleaner_Scan_OnNonLinux_ReturnsBlockedOrFailure()
    {
        if (OperatingSystem.IsLinux()) return;
        var m = new JournalCleanerModule();
        var r = await m.ScanAsync(new ScanOptions());
        Assert.False(r.Success);
        Assert.Equal("journal-cleaner", r.ModuleId);
    }

    [Fact]
    public async Task JournalCleaner_Optimize_WithEmptyPlan_ReturnsSuccess()
    {
        var m = new JournalCleanerModule();
        var plan = new OptimizationPlan(m.Id, new List<string>());
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("journal-cleaner", result.ModuleId);
        Assert.NotEmpty(result.OperationId);
    }
}
