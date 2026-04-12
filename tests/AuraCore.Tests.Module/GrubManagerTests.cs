using Xunit;
using AuraCore.Application;
using AuraCore.Module.GrubManager;
using AuraCore.Domain.Enums;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.Tests.Module;

public class GrubManagerTests
{
    [Fact]
    public void GrubManager_Metadata_IsValid()
    {
        var m = new GrubManagerModule();
        Assert.Equal("grub-manager", m.Id);
        Assert.Equal("GRUB Bootloader Manager", m.DisplayName);
        Assert.Equal(OptimizationCategory.SystemHealth, m.Category);
        Assert.Equal(RiskLevel.High, m.Risk);
        Assert.Equal(SupportedPlatform.Linux, m.Platform);
    }

    [Fact]
    public void GrubManager_IsAdvanced_ReturnsTrue()
    {
        IOptimizationModule m = new GrubManagerModule();
        Assert.True(m.IsAdvanced);
    }

    [Fact]
    public void GrubManager_Risk_IsHigh()
    {
        var m = new GrubManagerModule();
        Assert.Equal(RiskLevel.High, m.Risk);
    }

    [Fact]
    public async Task GrubManager_Scan_OnNonLinux_ReturnsBlocked()
    {
        if (OperatingSystem.IsLinux()) return;
        var m = new GrubManagerModule();
        var r = await m.ScanAsync(new ScanOptions());
        Assert.False(r.Success);
        Assert.Equal("grub-manager", r.ModuleId);
    }

    [Fact]
    public async Task GrubManager_Optimize_WithEmptyPlan_ReturnsSuccess()
    {
        var m = new GrubManagerModule();
        var plan = new OptimizationPlan(m.Id, new List<string>());
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("grub-manager", result.ModuleId);
        Assert.NotEmpty(result.OperationId);
    }

    [Fact]
    public async Task GrubManager_Optimize_InvalidValues_Rejected()
    {
        var m = new GrubManagerModule();
        var plan = new OptimizationPlan(m.Id, new List<string>
        {
            "set-timeout:-1",           // negative - out of range
            "set-timeout:31",           // exceeds max 30
            "set-timeout:abc",          // not a number
            "set-timeout:5;rm -rf /",   // injection attempt
            "set-default:-1",           // negative
            "set-default:11",           // exceeds max 10
            "set-default:abc",          // not valid (not "saved" or 0-10)
            "set-default:0;reboot",     // injection attempt
        });
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("grub-manager", result.ModuleId);
        // All items should be rejected - none processed
        Assert.Equal(0, result.ItemsProcessed);
    }

    [Fact]
    public void GrubManager_ParseGrubConfig_ExtractsValues()
    {
        var lines = new[]
        {
            "# GRUB configuration file",
            "GRUB_DEFAULT=2",
            "GRUB_TIMEOUT=10",
            "GRUB_CMDLINE_LINUX_DEFAULT=\"quiet splash nvidia_drm.modeset=1\"",
            "GRUB_DISABLE_OS_PROBER=true",
        };

        var settings = GrubManagerModule.ParseGrubConfig(lines);
        Assert.Equal("2", settings.GrubDefault);
        Assert.Equal(10, settings.Timeout);
        Assert.Equal("quiet splash nvidia_drm.modeset=1", settings.CmdlineLinuxDefault);
        Assert.True(settings.OsProberDisabled);
    }

    [Fact]
    public void GrubManager_ParseGrubConfig_Defaults_WhenEmpty()
    {
        var lines = Array.Empty<string>();
        var settings = GrubManagerModule.ParseGrubConfig(lines);
        Assert.Equal(5, settings.Timeout);
        Assert.Equal("0", settings.GrubDefault);
        Assert.Equal("quiet splash", settings.CmdlineLinuxDefault);
        Assert.False(settings.OsProberDisabled);
    }
}
