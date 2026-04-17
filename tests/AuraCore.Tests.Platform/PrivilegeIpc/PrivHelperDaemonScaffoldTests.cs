using AuraCore.PrivHelper.Linux;
using FluentAssertions;
using Xunit;

namespace AuraCore.Tests.Platform.PrivilegeIpc;

public class PrivHelperDaemonScaffoldTests
{
    [Fact]
    public void HelperVersion_constant_is_set_and_parseable()
    {
        HelperVersion.Current.Should().NotBeNullOrWhiteSpace();
        // Format: Major.Minor.Patch, e.g. "5.2.1"
        Version.TryParse(HelperVersion.Current, out var v).Should().BeTrue();
        v!.Major.Should().BeGreaterOrEqualTo(5);
    }

    [Fact]
    public void IdleExitTimeoutSeconds_matches_spec_default()
    {
        // Spec §3.2 D2: idle-exit 300 s
        HelperRuntimeOptions.IdleExitTimeoutSeconds.Should().Be(300);
    }

    [Fact]
    public void DBusObjectPath_matches_contract()
    {
        // Must match the IPrivHelper interface (pro.auracore.PrivHelper1)
        HelperRuntimeOptions.BusName.Should().Be("pro.auracore.PrivHelper");
        HelperRuntimeOptions.ObjectPath.Should().Be("/pro/auracore/PrivHelper");
    }
}
