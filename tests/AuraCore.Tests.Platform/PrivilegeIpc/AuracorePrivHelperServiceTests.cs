using AuraCore.Infrastructure.PrivilegeIpc.Linux;
using AuraCore.PrivHelper.Linux;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace AuraCore.Tests.Platform.PrivilegeIpc;

public class AuracorePrivHelperServiceTests
{
    private static AuracorePrivHelperService CreateService(IProcessInvoker invoker) =>
        new(new ActionWhitelist(), invoker, NullLogger<AuracorePrivHelperService>.Instance);

    [Fact]
    public async Task GetVersionAsync_returns_helper_version()
    {
        var svc = CreateService(Substitute.For<IProcessInvoker>());
        var version = await svc.GetVersionAsync();
        version.Should().Be(HelperVersion.Current);
    }

    [Fact]
    public async Task ObjectPath_matches_contract()
    {
        var svc = CreateService(Substitute.For<IProcessInvoker>());
        svc.ObjectPath.ToString().Should().Be(HelperRuntimeOptions.ObjectPath);
    }

    [Fact]
    public async Task RunActionAsync_rejects_unknown_action_without_spawning_process()
    {
        var invoker = Substitute.For<IProcessInvoker>();
        var svc = CreateService(invoker);
        var result = await svc.RunActionAsync("nope", Array.Empty<string>(), 30);
        result.ExitCode.Should().Be(-100);
        result.AuthState.Should().Be("rejected");
        result.Stderr.Should().Contain("unknown action");
        await invoker.DidNotReceive().InvokeAsync(
            Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunActionAsync_rejects_invalid_argv_without_spawning_process()
    {
        var invoker = Substitute.For<IProcessInvoker>();
        var svc = CreateService(invoker);
        var result = await svc.RunActionAsync("journal", new[] { "--unknown-flag" }, 30);
        result.ExitCode.Should().Be(-100);
        result.AuthState.Should().Be("rejected");
        await invoker.DidNotReceive().InvokeAsync(
            Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunActionAsync_invokes_whitelisted_executable_and_returns_outcome()
    {
        var invoker = Substitute.For<IProcessInvoker>();
        invoker.InvokeAsync("/usr/bin/journalctl", Arg.Any<string[]>(), 30, Arg.Any<CancellationToken>())
            .Returns(new ProcessInvokerResult(0, "freed 250MB\n", "", TimedOut: false));

        var svc = CreateService(invoker);
        var result = await svc.RunActionAsync("journal", new[] { "--vacuum-size=500M" }, 30);

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Be("freed 250MB\n");
        result.AuthState.Should().Be("cached");
    }

    [Fact]
    public async Task RunActionAsync_reports_timeout_via_special_exit_code()
    {
        var invoker = Substitute.For<IProcessInvoker>();
        invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string[]>(), 1, Arg.Any<CancellationToken>())
            .Returns(new ProcessInvokerResult(-1, "", "killed by timeout", TimedOut: true));

        var svc = CreateService(invoker);
        var result = await svc.RunActionAsync("journal", new[] { "--vacuum-size=500M" }, 1);

        result.ExitCode.Should().Be(-1);
        result.AuthState.Should().Be("cached");
        result.Stderr.Should().Contain("timeout");
    }

    [Fact]
    public async Task RunActionAsync_uses_whitelist_resolved_executable_not_client_hint()
    {
        // Even if a buggy/malicious client says Executable="rm", daemon
        // dispatches to the whitelist-resolved /usr/bin/journalctl.
        var invoker = Substitute.For<IProcessInvoker>();
        invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessInvokerResult(0, "", "", false));

        var svc = CreateService(invoker);
        await svc.RunActionAsync("journal", new[] { "--vacuum-size=500M" }, 30);

        await invoker.Received().InvokeAsync(
            "/usr/bin/journalctl",
            Arg.Any<string[]>(),
            30,
            Arg.Any<CancellationToken>());
    }
}
