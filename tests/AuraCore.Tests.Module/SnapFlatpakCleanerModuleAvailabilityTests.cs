using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using AuraCore.Module.SnapFlatpakCleaner;
using Xunit;

namespace AuraCore.Tests.Module;

public class SnapFlatpakCleanerModuleAvailabilityTests
{
    [Fact]
    public async Task CheckRuntimeAvailabilityAsync_OnNonLinux_ReturnsWrongPlatform()
    {
        if (OperatingSystem.IsLinux()) return;  // skip on Linux
        // Override never reaches the stored _shell field on the WrongPlatform path,
        // so passing null! is safe for this test.
        var module = new SnapFlatpakCleanerModule(null!);
        var r = await module.CheckRuntimeAvailabilityAsync();
        Assert.False(r.IsAvailable);
        Assert.Equal(AvailabilityCategory.WrongPlatform, r.Category);
    }
}
