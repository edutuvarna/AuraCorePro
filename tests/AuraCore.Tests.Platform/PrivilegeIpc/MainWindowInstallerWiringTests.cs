using AuraCore.Application.Interfaces.Platform;
using AuraCore.Infrastructure.PrivilegeIpc.Linux;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace AuraCore.Tests.Platform.PrivilegeIpc;

/// <summary>
/// Covers the seam between MainWindow's install button and PrivHelperInstaller,
/// extracted as a static helper so it can be tested without an Avalonia
/// application host.
/// </summary>
public class PrivilegeInstallCoordinatorTests
{
    [Fact]
    public async Task RunInstallFlowAsync_reports_available_on_success()
    {
        var installer = CreateInstaller(new InstallOutcome(true, 0, "OK: installed\n", "", "/tmp/stage"));
        var availability = Substitute.For<IHelperAvailabilityService>();
        var outcome = await AuraCore.UI.Avalonia.Views.PrivilegeInstallCoordinator
            .RunInstallFlowAsync(installer, availability);

        outcome.Success.Should().BeTrue();
        availability.Received().ReportAvailable();
    }

    [Fact]
    public async Task RunInstallFlowAsync_does_not_report_available_on_failure()
    {
        var installer = CreateInstaller(new InstallOutcome(false, 1, "", "ERROR: cancelled\n", "/tmp/stage"));
        var availability = Substitute.For<IHelperAvailabilityService>();
        var outcome = await AuraCore.UI.Avalonia.Views.PrivilegeInstallCoordinator
            .RunInstallFlowAsync(installer, availability);

        outcome.Success.Should().BeFalse();
        availability.DidNotReceive().ReportAvailable();
    }

    [Fact]
    public async Task RunInstallFlowAsync_handles_null_installer_gracefully()
    {
        // Non-Linux platforms may not have installer registered. Coordinator
        // should treat null as "install not supported on this platform" and
        // return a failure outcome without throwing.
        var availability = Substitute.For<IHelperAvailabilityService>();
        var outcome = await AuraCore.UI.Avalonia.Views.PrivilegeInstallCoordinator
            .RunInstallFlowAsync(installer: null, availability);

        outcome.Success.Should().BeFalse();
        outcome.Stderr.Should().Contain("not supported");
        availability.DidNotReceive().ReportAvailable();
    }

    private static PrivHelperInstaller CreateInstaller(InstallOutcome outcome)
    {
        var pkexec = Substitute.For<IPkexecInvoker>();
        pkexec.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((outcome.ExitCode, outcome.Stdout, outcome.Stderr));

        var locator = Substitute.For<IDaemonBinaryLocator>();
        // Create a fake binary on disk
        var tmp = Path.Combine(Path.GetTempPath(), $"fake-bin-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(tmp, new byte[] { 0x7F, 0x45, 0x4C, 0x46 });
        locator.LocateDaemonBinary().Returns(tmp);

        return new PrivHelperInstaller(pkexec, locator, NullLogger<PrivHelperInstaller>.Instance);
    }
}
