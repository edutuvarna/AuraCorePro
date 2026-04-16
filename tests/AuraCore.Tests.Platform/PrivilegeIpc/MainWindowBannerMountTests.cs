using System.Reflection;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.UI.Avalonia.Views;
using FluentAssertions;
using Xunit;

namespace AuraCore.Tests.Platform.PrivilegeIpc;

/// <summary>
/// Verifies that the MainWindow shell declares the privilege-banner wiring
/// introduced in Phase 5.2.0 Task 11.
///
/// Full render is not possible in headless tests (MainWindow construction
/// requires App.Services / full DI). These are compile+reflection guards
/// that confirm the mount is present in source without requiring a live window.
/// </summary>
public class MainWindowBannerMountTests
{
    [Fact]
    public void MainWindow_codebehind_has_HelperAvailability_field()
    {
        // The code-behind stores the resolved IHelperAvailabilityService in a field
        // named _helperAvailability so the DismissClicked handler can call it.
        typeof(MainWindow)
            .GetField("_helperAvailability",
                BindingFlags.NonPublic | BindingFlags.Instance)
            .Should().NotBeNull(
                "MainWindow must store IHelperAvailabilityService for the DismissClicked handler");
    }

    [Fact]
    public void MainWindow_codebehind_has_OnPrivilegeInstallNowClicked_handler()
    {
        typeof(MainWindow)
            .GetMethod("OnPrivilegeInstallNowClicked",
                BindingFlags.NonPublic | BindingFlags.Instance)
            .Should().NotBeNull(
                "MainWindow must declare OnPrivilegeInstallNowClicked to handle banner events");
    }

    [Fact]
    public void MainWindow_codebehind_has_OnPrivilegeDismissClicked_handler()
    {
        typeof(MainWindow)
            .GetMethod("OnPrivilegeDismissClicked",
                BindingFlags.NonPublic | BindingFlags.Instance)
            .Should().NotBeNull(
                "MainWindow must declare OnPrivilegeDismissClicked to handle banner events");
    }
}
