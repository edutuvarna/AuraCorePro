using System;
using System.Threading.Tasks;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.UI.Avalonia.Services;
using Avalonia.Headless.XUnit;
using Moq;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Services;

public class PrivilegedActionGuardTests
{
    [AvaloniaFact]
    public async Task TryGuardAsync_OnWindows_ReturnsTrue_WithoutPromptingUser()
    {
        if (!OperatingSystem.IsWindows()) return;
        var helper = new Mock<IHelperAvailabilityService>();
        helper.SetupGet(h => h.IsMissing).Returns(true); // even when reported missing, Windows path returns true
        var guard = new PrivilegedActionGuard(helper.Object);

        var result = await guard.TryGuardAsync("anything");

        Assert.True(result);
    }

    [AvaloniaFact]
    public async Task TryGuardAsync_OnLinux_HelperPresent_ReturnsTrue()
    {
        if (!OperatingSystem.IsLinux()) return;
        var helper = new Mock<IHelperAvailabilityService>();
        helper.SetupGet(h => h.IsMissing).Returns(false);
        var guard = new PrivilegedActionGuard(helper.Object);

        var result = await guard.TryGuardAsync("anything");

        Assert.True(result);
    }

    // Note: helper-missing modal-rendering path is integration-tested via the
    // Wave G smoke on the Linux VM (manual). Unit-testing the modal here would
    // require a parent Window context which Avalonia.Headless test mode doesn't
    // provide cleanly.
}
