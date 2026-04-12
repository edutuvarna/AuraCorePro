using Xunit;
using AuraCore.Application;
using AuraCore.Module.SystemdManager;
using AuraCore.Domain.Enums;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.Tests.Module;

public class SystemdManagerTests
{
    [Fact]
    public void SystemdManager_Metadata_IsValid()
    {
        var m = new SystemdManagerModule();
        Assert.Equal("systemd-manager", m.Id);
        Assert.False(string.IsNullOrWhiteSpace(m.DisplayName));
        Assert.Equal(OptimizationCategory.SystemHealth, m.Category);
        Assert.Equal(RiskLevel.Medium, m.Risk);
        Assert.Equal(SupportedPlatform.Linux, m.Platform);
        IOptimizationModule iface = m;
        Assert.False(iface.IsAdvanced);
    }

    [Fact]
    public async Task SystemdManager_Scan_OnNonLinux_ReturnsBlocked()
    {
        if (OperatingSystem.IsLinux()) return;
        var m = new SystemdManagerModule();
        var r = await m.ScanAsync(new ScanOptions());
        Assert.False(r.Success);
        Assert.Equal("systemd-manager", r.ModuleId);
    }

    [Fact]
    public async Task SystemdManager_Optimize_InvalidItemId_DoesNotCrash()
    {
        var m = new SystemdManagerModule();
        // Invalid format (no colon), malicious input, empty items
        var plan = new OptimizationPlan(m.Id, new List<string>
        {
            "invalid-no-colon",
            "stop:",  // empty service name
            "unknown-action:foo.service",  // action not in allowed list
            "stop:foo;rm -rf /"  // attempted injection
        });
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("systemd-manager", result.ModuleId);
        // Should not crash, processed should be 0 (all invalid)
        Assert.Equal(0, result.ItemsProcessed);
    }

    [Fact]
    public async Task SystemdManager_Optimize_WithEmptyPlan_ReturnsSuccess()
    {
        var m = new SystemdManagerModule();
        var plan = new OptimizationPlan(m.Id, new List<string>());
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("systemd-manager", result.ModuleId);
        Assert.NotEmpty(result.OperationId);
    }
}
