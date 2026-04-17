using AuraCore.PrivHelper.MacOS;
using FluentAssertions;
using System.Reflection;
using Xunit;

namespace AuraCore.Tests.Platform.PrivilegeIpc;

public class PrivHelperMacOSDaemonScaffoldTests
{
    [Fact]
    public void HelperVersion_constant_is_set_and_parseable()
    {
        HelperVersion.Current.Should().NotBeNullOrWhiteSpace();
        Version.TryParse(HelperVersion.Current, out var v).Should().BeTrue();
        v!.Major.Should().BeGreaterOrEqualTo(5);
    }

    [Fact]
    public void IdleExitTimeoutSeconds_matches_spec()
    {
        HelperRuntimeOptions.IdleExitTimeoutSeconds.Should().Be(300);
    }

    [Fact]
    public void MachServiceName_matches_reverse_dns_convention()
    {
        HelperRuntimeOptions.MachServiceName.Should().Be("pro.auracore.PrivHelper");
    }

    [Fact]
    public void BundleId_matches_main_app_team_namespace()
    {
        // Used by entitlements + SMAppService registration (Task 30)
        HelperRuntimeOptions.BundleIdentifier.Should().Be("pro.auracore.PrivHelper");
    }

    [Fact]
    public void MinimumMacOSVersion_is_13_for_SMAppService()
    {
        // SMAppService API requires macOS 13+ (Ventura)
        HelperRuntimeOptions.MinimumMacOSVersion.Should().Be(13);
    }

    [Fact]
    public void LibXpc_interop_exposes_required_xpc_functions()
    {
        // Reflect on Interop.LibXpc — verify the P/Invoke signatures we'll
        // need for Tasks 26/28 are declared. We don't *call* them (would
        // fail on Windows test host), just verify the API surface.
        var libxpc = typeof(AuraCore.PrivHelper.MacOS.Interop.LibXpc);
        libxpc.GetMethod("xpc_connection_create_mach_service",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
            .Should().NotBeNull();
        libxpc.GetMethod("xpc_dictionary_create",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
            .Should().NotBeNull();
    }

    [Fact]
    public void SecCode_interop_exposes_signature_verification_functions()
    {
        // Tasks 26 will use these to verify the client's code signature
        // before accepting XPC calls.
        var seccode = typeof(AuraCore.PrivHelper.MacOS.Interop.SecCode);
        seccode.GetMethod("SecCodeCopyGuestWithAttributes",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
            .Should().NotBeNull();
    }
}
