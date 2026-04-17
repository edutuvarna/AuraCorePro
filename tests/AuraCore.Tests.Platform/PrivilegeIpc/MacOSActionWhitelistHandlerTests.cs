using AuraCore.PrivHelper.MacOS;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace AuraCore.Tests.Platform.PrivilegeIpc;

/// <summary>
/// Tests for <see cref="ActionWhitelistHandler"/> — the <see cref="IActionHandler"/>
/// implementation that routes through <see cref="ActionWhitelist"/> and spawns the
/// privileged process via <see cref="IProcessInvoker"/> (Task 27, spec §3.9).
/// </summary>
public class MacOSActionWhitelistHandlerTests
{
    private static readonly PeerIdentity FakePeer =
        new PeerIdentity(4242, "pro.auracore.auracorepro", "TEAM123");

    private static (ActionWhitelistHandler Handler, IProcessInvoker InvokerSub) BuildHandler()
    {
        var whitelist = new ActionWhitelist();
        var invoker   = Substitute.For<IProcessInvoker>();
        var audit     = Substitute.For<IAuditLogger>();
        var logger    = NullLogger<ActionWhitelistHandler>.Instance;
        return (new ActionWhitelistHandler(whitelist, invoker, audit, logger), invoker);
    }

    // -----------------------------------------------------------------------
    // Rejected action: short-circuit before IProcessInvoker
    // -----------------------------------------------------------------------

    [Fact]
    public void Invoke_rejected_action_returns_exit_minus_100_without_invoking_process()
    {
        var (handler, invoker) = BuildHandler();
        var request = new XpcRequest("rm-rf-slash", new[] { "-rf", "/" }, 30);

        var result = handler.Invoke(request, FakePeer);

        result.ExitCode.Should().Be(-100);
        result.AuthState.Should().Be("rejected");
        result.Stderr.Should().Contain("unknown action");
        invoker.DidNotReceive().InvokeAsync(
            Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Invoke_rejected_by_validator_returns_exit_minus_100_without_invoking_process()
    {
        var (handler, invoker) = BuildHandler();
        // dns-flush with invalid args (extra unknown flag) → validator rejects
        var request = new XpcRequest("dns-flush", new[] { "--malicious-flag" }, 30);

        var result = handler.Invoke(request, FakePeer);

        result.ExitCode.Should().Be(-100);
        result.AuthState.Should().Be("rejected");
        result.Stderr.Should().NotBeNullOrEmpty();
        invoker.DidNotReceive().InvokeAsync(
            Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Accepted action: invokes IProcessInvoker with correct exe + argv
    // -----------------------------------------------------------------------

    [Fact]
    public void Invoke_dns_flush_invokes_dscacheutil_with_flushcache()
    {
        var (handler, invoker) = BuildHandler();
        invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProcessInvokerResult(0, "Cache flushed\n", "", false)));

        var request = new XpcRequest("dns-flush", new[] { "-flushcache" }, 30);
        var result  = handler.Invoke(request, FakePeer);

        result.ExitCode.Should().Be(0);
        result.AuthState.Should().Be("cached");
        result.Stdout.Should().Contain("Cache flushed");
        invoker.Received().InvokeAsync(
            "/usr/bin/dscacheutil",
            Arg.Is<string[]>(a => a.SequenceEqual(new[] { "-flushcache" })),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Invoke_purgeable_invokes_tmutil_with_thinlocalsnapshots()
    {
        var (handler, invoker) = BuildHandler();
        invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProcessInvokerResult(0, "", "", false)));

        var argv    = new[] { "thinlocalsnapshots", "/", "1073741824", "4" };
        var request = new XpcRequest("purgeable", argv, 60);
        var result  = handler.Invoke(request, FakePeer);

        result.AuthState.Should().Be("cached");
        invoker.Received().InvokeAsync(
            "/usr/bin/tmutil",
            Arg.Is<string[]>(a => a.SequenceEqual(argv)),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Invoke_time_machine_stopbackup_invokes_tmutil()
    {
        var (handler, invoker) = BuildHandler();
        invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProcessInvokerResult(0, "", "", false)));

        var request = new XpcRequest("time-machine", new[] { "stopbackup" }, 30);
        var result  = handler.Invoke(request, FakePeer);

        result.AuthState.Should().Be("cached");
        invoker.Received().InvokeAsync(
            "/usr/bin/tmutil",
            Arg.Is<string[]>(a => a.SequenceEqual(new[] { "stopbackup" })),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // ProcessInvokerResult → ActionHandlerResult mapping
    // -----------------------------------------------------------------------

    [Fact]
    public void Invoke_maps_exit_code_stdout_stderr_from_invoker_result()
    {
        var (handler, invoker) = BuildHandler();
        invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProcessInvokerResult(42, "some output\n", "some error\n", false)));

        var request = new XpcRequest("dns-flush", Array.Empty<string>(), 30);
        var result  = handler.Invoke(request, FakePeer);

        result.ExitCode.Should().Be(42);
        result.Stdout.Should().Be("some output\n");
        result.Stderr.Should().Be("some error\n");
        result.AuthState.Should().Be("cached");
    }

    [Fact]
    public void Invoke_timeout_clamped_to_MaxAllowedTimeoutSeconds()
    {
        var (handler, invoker) = BuildHandler();
        invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProcessInvokerResult(0, "", "", false)));

        // Request timeout WAY over the max (3600s)
        var request = new XpcRequest("dns-flush", Array.Empty<string>(), 99999);
        handler.Invoke(request, FakePeer);

        invoker.Received().InvokeAsync(
            Arg.Any<string>(),
            Arg.Any<string[]>(),
            Arg.Is<int>(t => t <= (int)HelperRuntimeOptions.MaxAllowedTimeoutSeconds),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Invoke_timeout_minimum_is_1_second()
    {
        var (handler, invoker) = BuildHandler();
        invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProcessInvokerResult(0, "", "", false)));

        // Request timeout of 0 → should clamp to 1
        var request = new XpcRequest("dns-flush", Array.Empty<string>(), 0);
        handler.Invoke(request, FakePeer);

        invoker.Received().InvokeAsync(
            Arg.Any<string>(),
            Arg.Any<string[]>(),
            Arg.Is<int>(t => t >= 1),
            Arg.Any<CancellationToken>());
    }
}
