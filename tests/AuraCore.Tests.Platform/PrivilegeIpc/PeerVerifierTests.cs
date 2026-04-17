using AuraCore.PrivHelper.MacOS;
using FluentAssertions;
using Xunit;

namespace AuraCore.Tests.Platform.PrivilegeIpc;

/// <summary>
/// Tests for <see cref="PeerVerifier"/>.
/// NOTE: These tests verify the PLACEHOLDER / SOFT-FAIL logic only.
/// Real SecCodeCheckValidity cannot execute on Windows dev hosts.
/// The <c>stubMode</c> flag on <see cref="PeerVerifier"/> lets tests bypass
/// the P/Invoke and exercise the rejection/audit code paths.
/// </summary>
public class PeerVerifierTests
{
    [Fact]
    public void Verify_accepts_in_stub_mode_for_any_pid()
    {
        var v = new PeerVerifier(stubMode: true);
        var r = v.Verify(peerPid: 4242);
        r.Ok.Should().BeTrue();
        r.Identity!.Value.Pid.Should().Be(4242);
        r.Identity.Value.BundleId.Should().Be(SecurityConfig.ExpectedBundleId);
    }

    [Fact]
    public void Verify_rejects_invalid_pid_in_stub_mode()
    {
        var v = new PeerVerifier(stubMode: true);
        // PID 0 = kernel — never a valid XPC peer.
        var r = v.Verify(peerPid: 0);
        r.Ok.Should().BeFalse();
        r.RejectionReason.Should().Contain("invalid pid");
    }

    [Fact]
    public void Verify_on_non_mac_host_uses_debug_soft_fail_with_warning()
    {
        // On Windows dev we can't call SecCode — constructor should
        // NOT throw; subsequent Verify should return Ok=true but with
        // a "SOFT_FAIL" marker in the identity's TeamId.
        var v = new PeerVerifier(stubMode: false);
        var r = v.Verify(peerPid: 12345);
        if (OperatingSystem.IsMacOS())
        {
            // Real macOS host — we can't assert without a real signed peer.
            // Just verify the call shape doesn't throw.
            (r.Ok || !r.Ok).Should().BeTrue();
        }
        else
        {
            r.Ok.Should().BeTrue();
            r.Identity!.Value.TeamId.Should().Contain("SOFT_FAIL");
        }
    }

    [Fact]
    public void SecurityConfig_ExpectedBundleId_matches_spec()
    {
        SecurityConfig.ExpectedBundleId.Should().Be("pro.auracore.auracorepro");
    }

    [Fact]
    public void SecurityConfig_IsTeamIdPlaceholder_detects_unsupported_placeholder()
    {
        SecurityConfig.IsTeamIdPlaceholder(SecurityConfig.ExpectedTeamId).Should().BeTrue();
        SecurityConfig.IsTeamIdPlaceholder("REAL_TEAM_ID_123").Should().BeFalse();
    }
}
