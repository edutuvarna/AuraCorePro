using AuraCore.PrivHelper.MacOS;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace AuraCore.Tests.Platform.PrivilegeIpc;

/// <summary>
/// Tests for <see cref="AuracorePrivHelperDelegate"/> using
/// <see cref="AuracorePrivHelperDelegate.HandleFake"/> — the internal
/// test-only overload that accepts already-decoded values, bypassing real
/// XPC pointer dereference. Accessible via
/// <c>InternalsVisibleTo("AuraCore.Tests.Platform")</c>.
/// </summary>
public class AuracorePrivHelperDelegateTests
{
    private static AuracorePrivHelperDelegate CreateDelegate(
        IPeerVerifier?  peer   = null,
        IActionHandler? action = null,
        IAuditLogger?   audit  = null)
    {
        return new AuracorePrivHelperDelegate(
            peer   ?? Substitute.For<IPeerVerifier>(),
            action ?? Substitute.For<IActionHandler>(),
            audit  ?? Substitute.For<IAuditLogger>(),
            NullLogger<AuracorePrivHelperDelegate>.Instance);
    }

    // -----------------------------------------------------------------------
    // Peer rejection
    // -----------------------------------------------------------------------

    [Fact]
    public void Handle_rejects_when_peer_verification_fails()
    {
        var peer = Substitute.For<IPeerVerifier>();
        peer.Verify(Arg.Any<int>())
            .Returns(new PeerVerificationResult(false, null, "signature invalid"));
        var action = Substitute.For<IActionHandler>();
        var d = CreateDelegate(peer: peer, action: action);

        var reply = d.HandleFake(peerPid: 4242,
            request: new XpcRequest("dns-flush", Array.Empty<string>(), 30));

        reply.AuthState.Should().Be("rejected");
        reply.ExitCode.Should().Be(-102);
        reply.Stderr.Should().Contain("signature invalid");
        action.DidNotReceive().Invoke(Arg.Any<XpcRequest>(), Arg.Any<PeerIdentity>());
    }

    // -----------------------------------------------------------------------
    // Successful dispatch
    // -----------------------------------------------------------------------

    [Fact]
    public void Handle_dispatches_to_action_handler_when_peer_verified()
    {
        var peer = Substitute.For<IPeerVerifier>();
        peer.Verify(Arg.Any<int>())
            .Returns(new PeerVerificationResult(
                true,
                new PeerIdentity(4242, SecurityConfig.ExpectedBundleId, "TEAMID"),
                null));
        var action = Substitute.For<IActionHandler>();
        action.Invoke(Arg.Any<XpcRequest>(), Arg.Any<PeerIdentity>())
            .Returns(new ActionHandlerResult(0, "ok\n", "", "cached"));

        var d = CreateDelegate(peer: peer, action: action);
        var reply = d.HandleFake(peerPid: 4242,
            request: new XpcRequest("dns-flush", new[] { "-flushcache" }, 30));

        reply.AuthState.Should().Be("cached");
        reply.ExitCode.Should().Be(0);
        reply.Stdout.Should().Be("ok\n");
        action.Received().Invoke(
            Arg.Is<XpcRequest>(r => r.ActionId == "dns-flush"),
            Arg.Any<PeerIdentity>());
    }

    // -----------------------------------------------------------------------
    // Audit privacy — argv must NOT be logged on peer rejection
    // -----------------------------------------------------------------------

    [Fact]
    public void Handle_audits_peer_rejection_but_not_argv()
    {
        // Privacy: when peer verification fails, argv should NOT be logged
        // (could contain sensitive paths); only the peer PID + reason.
        var peer = Substitute.For<IPeerVerifier>();
        peer.Verify(Arg.Any<int>())
            .Returns(new PeerVerificationResult(false, null, "wrong team id"));
        var audit = Substitute.For<IAuditLogger>();

        var d = CreateDelegate(peer: peer, audit: audit);
        d.HandleFake(peerPid: 4242,
            request: new XpcRequest("dns-flush", new[] { "/sensitive/path" }, 30));

        audit.Received().LogPeerRejection(4242, Arg.Is<string>(s => s.Contains("wrong team id")));
        audit.DidNotReceive().LogAction(
            Arg.Any<PeerIdentity>(), Arg.Any<XpcRequest>(), Arg.Any<ActionHandlerResult>());
    }

    // -----------------------------------------------------------------------
    // StubActionHandler
    // -----------------------------------------------------------------------

    [Fact]
    public void Handle_stub_action_handler_returns_rejected_for_any_request()
    {
        // Verifies the Task 27-pending stub short-circuits correctly.
        var peer = Substitute.For<IPeerVerifier>();
        peer.Verify(Arg.Any<int>())
            .Returns(new PeerVerificationResult(
                true,
                new PeerIdentity(4242, SecurityConfig.ExpectedBundleId, "TEAMID"),
                null));

        var stub = new StubActionHandler();
        var d    = CreateDelegate(peer: peer, action: stub);
        var reply = d.HandleFake(peerPid: 4242,
            request: new XpcRequest("dns-flush", new[] { "-flushcache" }, 30));

        reply.ExitCode.Should().Be(-255);
        reply.AuthState.Should().Be("rejected");
        reply.Stderr.Should().Contain("ActionWhitelist");
    }

    // -----------------------------------------------------------------------
    // Malformed message (null decoded request)
    // -----------------------------------------------------------------------

    [Fact]
    public void Handle_returns_malformed_reply_when_decoded_request_is_null()
    {
        var peer = Substitute.For<IPeerVerifier>();
        peer.Verify(Arg.Any<int>())
            .Returns(new PeerVerificationResult(
                true,
                new PeerIdentity(4242, SecurityConfig.ExpectedBundleId, "TEAMID"),
                null));
        var action = Substitute.For<IActionHandler>();
        var audit  = Substitute.For<IAuditLogger>();
        var d = CreateDelegate(peer: peer, action: action, audit: audit);

        // Pass null as the decoded request (simulates codec returning null for malformed input)
        var reply = d.HandleFake(peerPid: 4242, request: null);

        reply.ExitCode.Should().Be(-101);
        reply.AuthState.Should().Be("rejected");
        reply.Stderr.Should().Contain("malformed");
        action.DidNotReceive().Invoke(Arg.Any<XpcRequest>(), Arg.Any<PeerIdentity>());
        audit.Received().LogMalformedMessage(Arg.Any<PeerIdentity?>());
    }
}
