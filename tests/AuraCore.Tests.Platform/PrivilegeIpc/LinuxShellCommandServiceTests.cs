using AuraCore.Application.Interfaces.Platform;
using AuraCore.Infrastructure.PrivilegeIpc.Linux;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace AuraCore.Tests.Platform.PrivilegeIpc;

public class LinuxShellCommandServiceTests
{
    private static LinuxShellCommandService CreateService(
        IPrivHelper? proxy,
        IHelperAvailabilityService? availability = null)
    {
        var factory = Substitute.For<IPrivHelperConnectionFactory>();
        factory.TryConnectAsync(Arg.Any<CancellationToken>()).Returns(proxy);
        return new LinuxShellCommandService(
            factory,
            availability ?? Substitute.For<IHelperAvailabilityService>(),
            NullLogger<LinuxShellCommandService>.Instance);
    }

    [Fact]
    public async Task RunPrivilegedAsync_returns_HelperMissing_when_daemon_unreachable()
    {
        var availability = Substitute.For<IHelperAvailabilityService>();
        var svc = CreateService(proxy: null, availability);

        var result = await svc.RunPrivilegedAsync(
            new PrivilegedCommand("journal", "journalctl", new[] { "--vacuum-size=500M" }));

        result.AuthResult.Should().Be(PrivilegeAuthResult.HelperMissing);
        result.Success.Should().BeFalse();
        availability.Received().ReportMissing();
    }

    [Fact]
    public async Task RunPrivilegedAsync_forwards_action_id_and_args_to_daemon()
    {
        var proxy = Substitute.For<IPrivHelper>();
        proxy.RunActionAsync("journal", Arg.Any<string[]>(), 60)
            .Returns(new PrivHelperResult { ExitCode = 0, Stdout = "freed 250MB\n", Stderr = "", AuthState = "cached" });

        var svc = CreateService(proxy);
        var result = await svc.RunPrivilegedAsync(
            new PrivilegedCommand("journal", "journalctl", new[] { "--vacuum-size=500M" }, TimeoutSeconds: 60));

        result.Success.Should().BeTrue();
        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Be("freed 250MB\n");
        result.AuthResult.Should().Be(PrivilegeAuthResult.AlreadyAuthorized);

        await proxy.Received().RunActionAsync(
            "journal",
            Arg.Is<string[]>(a => a.Length == 1 && a[0] == "--vacuum-size=500M"),
            60);
    }

    [Fact]
    public async Task RunPrivilegedAsync_ignores_client_executable_hint()
    {
        // Client says Executable="whatever" — doesn't matter, daemon ignores.
        // This test verifies the client-side code doesn't try to pass Executable
        // to the daemon (spec §9).
        var proxy = Substitute.For<IPrivHelper>();
        proxy.RunActionAsync(Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<int>())
            .Returns(new PrivHelperResult { ExitCode = 0, AuthState = "cached", Stdout = "", Stderr = "" });

        var svc = CreateService(proxy);
        await svc.RunPrivilegedAsync(new PrivilegedCommand("journal", "SUSPICIOUS", new[] { "--rotate" }));

        // Verify only the action id + args reached the daemon, not the Executable hint
        await proxy.Received().RunActionAsync("journal", Arg.Any<string[]>(), Arg.Any<int>());
    }

    [Theory]
    [InlineData("cached",        PrivilegeAuthResult.AlreadyAuthorized)]
    [InlineData("prompted",      PrivilegeAuthResult.Prompted)]
    [InlineData("denied",        PrivilegeAuthResult.Denied)]
    [InlineData("rejected",      PrivilegeAuthResult.HelperMissing)]
    [InlineData("something-weird", PrivilegeAuthResult.HelperMissing)]
    public async Task RunPrivilegedAsync_maps_auth_state_to_enum(string authState, PrivilegeAuthResult expected)
    {
        var proxy = Substitute.For<IPrivHelper>();
        proxy.RunActionAsync(Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<int>())
            .Returns(new PrivHelperResult { ExitCode = 0, Stdout = "", Stderr = "", AuthState = authState });

        var svc = CreateService(proxy);
        var result = await svc.RunPrivilegedAsync(new PrivilegedCommand("journal", "x", Array.Empty<string>()));

        result.AuthResult.Should().Be(expected);
    }

    [Fact]
    public async Task RunPrivilegedAsync_marks_availability_as_available_on_successful_call()
    {
        var proxy = Substitute.For<IPrivHelper>();
        proxy.RunActionAsync(Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<int>())
            .Returns(new PrivHelperResult { ExitCode = 0, Stdout = "", Stderr = "", AuthState = "cached" });

        var availability = Substitute.For<IHelperAvailabilityService>();
        var svc = CreateService(proxy, availability);
        await svc.RunPrivilegedAsync(new PrivilegedCommand("journal", "x", Array.Empty<string>()));

        availability.Received().ReportAvailable();
    }

    [Fact]
    public async Task RunPrivilegedAsync_nonzero_exit_is_failure_even_if_authorized()
    {
        var proxy = Substitute.For<IPrivHelper>();
        proxy.RunActionAsync(Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<int>())
            .Returns(new PrivHelperResult { ExitCode = 42, Stdout = "", Stderr = "whatever", AuthState = "cached" });

        var svc = CreateService(proxy);
        var result = await svc.RunPrivilegedAsync(new PrivilegedCommand("journal", "x", Array.Empty<string>()));

        result.Success.Should().BeFalse();
        result.ExitCode.Should().Be(42);
        result.AuthResult.Should().Be(PrivilegeAuthResult.AlreadyAuthorized);
    }

    [Fact]
    public async Task RunPrivilegedAsync_denied_is_failure()
    {
        var proxy = Substitute.For<IPrivHelper>();
        proxy.RunActionAsync(Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<int>())
            .Returns(new PrivHelperResult { ExitCode = 0, Stdout = "", Stderr = "user cancelled", AuthState = "denied" });

        var svc = CreateService(proxy);
        var result = await svc.RunPrivilegedAsync(new PrivilegedCommand("journal", "x", Array.Empty<string>()));

        result.Success.Should().BeFalse();
        result.AuthResult.Should().Be(PrivilegeAuthResult.Denied);
    }
}
