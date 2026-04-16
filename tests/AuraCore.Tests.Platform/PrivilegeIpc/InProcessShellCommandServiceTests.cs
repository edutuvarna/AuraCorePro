using AuraCore.Application.Interfaces.Platform;
using AuraCore.Infrastructure.PrivilegeIpc;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AuraCore.Tests.Platform.PrivilegeIpc;

public class InProcessShellCommandServiceTests
{
    [Fact]
    public async Task RunPrivilegedAsync_runs_executable_and_returns_stdout()
    {
        var svc = new InProcessShellCommandService(NullLogger<InProcessShellCommandService>.Instance);
        var exe = OperatingSystem.IsWindows() ? "cmd" : "/bin/sh";
        var args = OperatingSystem.IsWindows()
            ? new[] { "/c", "echo hello" }
            : new[] { "-c", "echo hello" };
        var result = await svc.RunPrivilegedAsync(
            new PrivilegedCommand("test.echo", exe, args, TimeoutSeconds: 10));

        result.Success.Should().BeTrue();
        result.ExitCode.Should().Be(0);
        result.Stdout.Trim().Should().Be("hello");
        result.AuthResult.Should().Be(PrivilegeAuthResult.AlreadyAuthorized);
    }

    [Fact]
    public async Task RunPrivilegedAsync_returns_failure_for_nonzero_exit()
    {
        var svc = new InProcessShellCommandService(NullLogger<InProcessShellCommandService>.Instance);
        var exe = OperatingSystem.IsWindows() ? "cmd" : "/bin/sh";
        var args = OperatingSystem.IsWindows()
            ? new[] { "/c", "exit 42" }
            : new[] { "-c", "exit 42" };
        var result = await svc.RunPrivilegedAsync(
            new PrivilegedCommand("test.fail", exe, args, TimeoutSeconds: 10));

        result.Success.Should().BeFalse();
        result.ExitCode.Should().Be(42);
        result.AuthResult.Should().Be(PrivilegeAuthResult.AlreadyAuthorized);
    }

    [Fact]
    public async Task RunPrivilegedAsync_honours_timeout_with_cancelled_exit()
    {
        var svc = new InProcessShellCommandService(NullLogger<InProcessShellCommandService>.Instance);
        var exe = OperatingSystem.IsWindows() ? "ping" : "/bin/sh";
        var args = OperatingSystem.IsWindows()
            ? new[] { "-n", "10", "127.0.0.1" }
            : new[] { "-c", "sleep 5" };
        var result = await svc.RunPrivilegedAsync(
            new PrivilegedCommand("test.slow", exe, args, TimeoutSeconds: 1));

        result.Success.Should().BeFalse();
        result.ExitCode.Should().BeNegative();
    }
}
