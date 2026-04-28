using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using Xunit;

namespace AuraCore.Tests.Unit;

public class ModuleAvailabilityTests
{
    [Fact]
    public void Available_HasIsAvailableTrue_AndCorrectCategory()
    {
        var r = ModuleAvailability.Available;
        Assert.True(r.IsAvailable);
        Assert.Equal(AvailabilityCategory.Available, r.Category);
        Assert.Null(r.Reason);
        Assert.Null(r.RemediationCommand);
    }

    [Fact]
    public void WrongPlatform_HasIsAvailableFalse_AndDescriptiveReason()
    {
        var r = ModuleAvailability.WrongPlatform(SupportedPlatform.Linux);
        Assert.False(r.IsAvailable);
        Assert.Equal(AvailabilityCategory.WrongPlatform, r.Category);
        Assert.Contains("Linux", r.Reason!);
    }

    [Fact]
    public void HelperNotRunning_IncludesRemediationCommand()
    {
        var r = ModuleAvailability.HelperNotRunning("sudo bash /opt/install.sh");
        Assert.False(r.IsAvailable);
        Assert.Equal(AvailabilityCategory.HelperNotRunning, r.Category);
        Assert.Equal("sudo bash /opt/install.sh", r.RemediationCommand);
    }

    [Fact]
    public void ToolNotInstalled_IncludesToolName_InReason()
    {
        var r = ModuleAvailability.ToolNotInstalled("systemctl", "Use systemd distro");
        Assert.False(r.IsAvailable);
        Assert.Equal(AvailabilityCategory.ToolNotInstalled, r.Category);
        Assert.Contains("systemctl", r.Reason!);
        Assert.Equal("Use systemd distro", r.RemediationCommand);
    }

    [Fact]
    public void FeatureDisabled_HasReason_NoRemediation()
    {
        var r = ModuleAvailability.FeatureDisabled("Disabled by config");
        Assert.False(r.IsAvailable);
        Assert.Equal(AvailabilityCategory.FeatureDisabled, r.Category);
        Assert.Equal("Disabled by config", r.Reason);
        Assert.Null(r.RemediationCommand);
    }
}
