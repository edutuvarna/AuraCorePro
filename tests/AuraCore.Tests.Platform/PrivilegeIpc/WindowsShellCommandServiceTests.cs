using AuraCore.Application.Interfaces.Platform;
using AuraCore.Infrastructure.PrivilegeIpc.Windows;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AuraCore.Tests.Platform.PrivilegeIpc;

public class WindowsShellCommandServiceTests
{
    // Phase 5.5.1.6: WindowsShellCommandService is now a real Named Pipe client.
    // These tests validate failure-mode contract WITHOUT a live pipe server.

    [Fact]
    public async Task RunPrivilegedAsync_returns_Denied_for_unknown_action_id()
    {
        // An action id that is not in the BuildRequest whitelist should be caught
        // BEFORE the pipe is touched, returning Denied immediately.
        var svc = new WindowsShellCommandService(NullLogger<WindowsShellCommandService>.Instance);
        var cmd = new PrivilegedCommand("unknown.action.xyz", "whoami", Array.Empty<string>());

        var result = await svc.RunPrivilegedAsync(cmd);

        result.Success.Should().BeFalse();
        result.AuthResult.Should().Be(PrivilegeAuthResult.Denied);
        result.Stderr.Should().Contain("unknown action");
    }

    [Fact]
    public async Task RunPrivilegedAsync_returns_HelperMissing_when_pipe_server_not_running()
    {
        // With no named pipe server listening, ConnectAsync times out (2 s default).
        // The service should map that to HelperMissing with an informative message.
        var svc = new WindowsShellCommandService(NullLogger<WindowsShellCommandService>.Instance);
        var cmd = new PrivilegedCommand("driver.scan", "", Array.Empty<string>());

        var result = await svc.RunPrivilegedAsync(cmd);

        result.Success.Should().BeFalse();
        result.AuthResult.Should().Be(PrivilegeAuthResult.HelperMissing);
        result.Stderr.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RunPrivilegedAsync_propagates_OperationCanceledException()
    {
        // A pre-cancelled token must surface as OperationCanceledException,
        // not be swallowed into a ShellResult.
        var svc = new WindowsShellCommandService(NullLogger<WindowsShellCommandService>.Instance);
        var cmd = new PrivilegedCommand("driver.scan", "", Array.Empty<string>());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = () => svc.RunPrivilegedAsync(cmd, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
