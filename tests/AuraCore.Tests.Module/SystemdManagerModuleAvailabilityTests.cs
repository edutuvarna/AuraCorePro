using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using AuraCore.Module.SystemdManager;
using Xunit;

namespace AuraCore.Tests.Module;

public class SystemdManagerModuleAvailabilityTests
{
    [Fact]
    public async Task CheckRuntimeAvailabilityAsync_OnNonLinux_ReturnsWrongPlatform()
    {
        if (OperatingSystem.IsLinux()) return;  // skip on Linux
        var module = new SystemdManagerModule();
        var r = await module.CheckRuntimeAvailabilityAsync();
        Assert.False(r.IsAvailable);
        Assert.Equal(AvailabilityCategory.WrongPlatform, r.Category);
    }
}
