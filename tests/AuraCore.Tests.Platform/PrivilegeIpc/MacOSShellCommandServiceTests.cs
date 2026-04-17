using AuraCore.Application.Interfaces.Platform;
using AuraCore.Infrastructure.PrivilegeIpc.MacOS;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace AuraCore.Tests.Platform.PrivilegeIpc;

public class MacOSShellCommandServiceTests
{
    private static MacOSShellCommandService CreateService(
        IXpcConnection? conn,
        IHelperAvailabilityService? availability = null)
    {
        var factory = Substitute.For<IXpcConnectionFactory>();
        factory.TryConnectAsync(Arg.Any<CancellationToken>()).Returns(conn);
        return new MacOSShellCommandService(
            factory,
            availability ?? Substitute.For<IHelperAvailabilityService>(),
            NullLogger<MacOSShellCommandService>.Instance);
    }

    [Fact]
    public async Task RunPrivilegedAsync_returns_HelperMissing_when_daemon_unreachable()
    {
        var availability = Substitute.For<IHelperAvailabilityService>();
        var svc = CreateService(conn: null, availability);

        var result = await svc.RunPrivilegedAsync(
            new PrivilegedCommand("dns-flush", "dscacheutil", new[] { "-flushcache" }));

        result.AuthResult.Should().Be(PrivilegeAuthResult.HelperMissing);
        result.Success.Should().BeFalse();
        availability.Received().ReportMissing();
    }

    [Fact]
    public async Task RunPrivilegedAsync_forwards_action_id_and_args_and_timeout()
    {
        var conn = Substitute.For<IXpcConnection>();
        conn.SendRequestAsync(Arg.Any<XpcClientRequest>(), Arg.Any<CancellationToken>())
            .Returns(new XpcClientReply(0, "flushed\n", "", "cached"));

        var svc = CreateService(conn);
        var result = await svc.RunPrivilegedAsync(
            new PrivilegedCommand("dns-flush", "dscacheutil", new[] { "-flushcache" }, TimeoutSeconds: 45));

        result.Success.Should().BeTrue();
        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Be("flushed\n");
        result.AuthResult.Should().Be(PrivilegeAuthResult.AlreadyAuthorized);

        await conn.Received().SendRequestAsync(
            Arg.Is<XpcClientRequest>(r =>
                r.ActionId == "dns-flush" &&
                r.Arguments.Length == 1 && r.Arguments[0] == "-flushcache" &&
                r.TimeoutSeconds == 45),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunPrivilegedAsync_ignores_client_executable_hint()
    {
        // Client's Executable field is a documentation hint; daemon hardcodes
        // executables per action-id. Verify the client doesn't smuggle it over.
        var conn = Substitute.For<IXpcConnection>();
        conn.SendRequestAsync(Arg.Any<XpcClientRequest>(), Arg.Any<CancellationToken>())
            .Returns(new XpcClientReply(0, "", "", "cached"));

        var svc = CreateService(conn);
        await svc.RunPrivilegedAsync(
            new PrivilegedCommand("dns-flush", "SUSPICIOUS_EXE", new[] { "-flushcache" }));

        // Verify the XpcClientRequest doesn't contain SUSPICIOUS_EXE anywhere
        await conn.Received().SendRequestAsync(
            Arg.Is<XpcClientRequest>(r =>
                r.ActionId == "dns-flush" &&
                !r.Arguments.Any(a => a.Contains("SUSPICIOUS"))),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("cached",   PrivilegeAuthResult.AlreadyAuthorized)]
    [InlineData("rejected", PrivilegeAuthResult.HelperMissing)]
    [InlineData("denied",   PrivilegeAuthResult.HelperMissing)]
    [InlineData("prompted", PrivilegeAuthResult.HelperMissing)]
    [InlineData("weird",    PrivilegeAuthResult.HelperMissing)]
    public async Task RunPrivilegedAsync_maps_auth_state(string authState, PrivilegeAuthResult expected)
    {
        var conn = Substitute.For<IXpcConnection>();
        conn.SendRequestAsync(Arg.Any<XpcClientRequest>(), Arg.Any<CancellationToken>())
            .Returns(new XpcClientReply(0, "", "", authState));

        var svc = CreateService(conn);
        var result = await svc.RunPrivilegedAsync(
            new PrivilegedCommand("dns-flush", "x", Array.Empty<string>()));
        result.AuthResult.Should().Be(expected);
    }

    [Fact]
    public async Task RunPrivilegedAsync_marks_availability_available_on_successful_call()
    {
        var conn = Substitute.For<IXpcConnection>();
        conn.SendRequestAsync(Arg.Any<XpcClientRequest>(), Arg.Any<CancellationToken>())
            .Returns(new XpcClientReply(0, "", "", "cached"));

        var availability = Substitute.For<IHelperAvailabilityService>();
        var svc = CreateService(conn, availability);
        await svc.RunPrivilegedAsync(new PrivilegedCommand("dns-flush", "x", Array.Empty<string>()));

        availability.Received().ReportAvailable();
    }

    [Fact]
    public async Task RunPrivilegedAsync_nonzero_exit_is_failure_even_if_authorized()
    {
        var conn = Substitute.For<IXpcConnection>();
        conn.SendRequestAsync(Arg.Any<XpcClientRequest>(), Arg.Any<CancellationToken>())
            .Returns(new XpcClientReply(2, "", "something went wrong", "cached"));

        var svc = CreateService(conn);
        var result = await svc.RunPrivilegedAsync(
            new PrivilegedCommand("dns-flush", "x", Array.Empty<string>()));

        result.Success.Should().BeFalse();
        result.ExitCode.Should().Be(2);
        result.AuthResult.Should().Be(PrivilegeAuthResult.AlreadyAuthorized);
        result.Stderr.Should().Contain("something went wrong");
    }

    [Fact]
    public async Task RunPrivilegedAsync_handles_xpc_exception_as_HelperMissing()
    {
        var conn = Substitute.For<IXpcConnection>();
        conn.SendRequestAsync(Arg.Any<XpcClientRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<XpcClientReply?>>(_ => throw new InvalidOperationException("wire broke"));

        var availability = Substitute.For<IHelperAvailabilityService>();
        var svc = CreateService(conn, availability);
        var result = await svc.RunPrivilegedAsync(
            new PrivilegedCommand("dns-flush", "x", Array.Empty<string>()));

        result.AuthResult.Should().Be(PrivilegeAuthResult.HelperMissing);
        result.ExitCode.Should().Be(-4);
        availability.Received().ReportMissing();
    }

    [Fact]
    public async Task RunPrivilegedAsync_handles_null_reply_as_HelperMissing()
    {
        var conn = Substitute.For<IXpcConnection>();
        conn.SendRequestAsync(Arg.Any<XpcClientRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<XpcClientReply?>>(_ => Task.FromResult<XpcClientReply?>(null));

        var availability = Substitute.For<IHelperAvailabilityService>();
        var svc = CreateService(conn, availability);
        var result = await svc.RunPrivilegedAsync(
            new PrivilegedCommand("dns-flush", "x", Array.Empty<string>()));

        result.AuthResult.Should().Be(PrivilegeAuthResult.HelperMissing);
        availability.Received().ReportMissing();
    }
}
