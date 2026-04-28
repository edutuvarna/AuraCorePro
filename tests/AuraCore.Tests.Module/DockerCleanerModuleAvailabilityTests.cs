using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using AuraCore.Module.DockerCleaner;
using Xunit;

namespace AuraCore.Tests.Module;

public class DockerCleanerModuleAvailabilityTests
{
    [Fact]
    public async Task CheckRuntimeAvailabilityAsync_OnNonLinuxNonMac_ReturnsWrongPlatform()
    {
        // Skip if running on Linux or macOS — DockerCleaner is supported on both.
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) return;
        var module = new DockerCleanerModule();
        var r = await module.CheckRuntimeAvailabilityAsync();
        Assert.False(r.IsAvailable);
        Assert.Equal(AvailabilityCategory.WrongPlatform, r.Category);
    }
}
