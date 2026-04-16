using AuraCore.Application.Interfaces.Platform;
using FluentAssertions;
using Xunit;

namespace AuraCore.Tests.Platform.PrivilegeIpc;

public class IShellCommandServiceContractTests
{
    [Fact]
    public void PrivilegedCommand_record_has_required_properties()
    {
        var cmd = new PrivilegedCommand("journal", "journalctl", new[] { "--vacuum-size=500M" });
        cmd.Id.Should().Be("journal");
        cmd.Executable.Should().Be("journalctl");
        cmd.Arguments.Should().ContainSingle().Which.Should().Be("--vacuum-size=500M");
        cmd.TimeoutSeconds.Should().Be(60);
    }

    [Fact]
    public void PrivilegedCommand_supports_custom_timeout()
    {
        var cmd = new PrivilegedCommand("slow-op", "apt-get", new[] { "upgrade" }, TimeoutSeconds: 600);
        cmd.TimeoutSeconds.Should().Be(600);
    }

    [Fact]
    public void ShellResult_captures_full_outcome()
    {
        var res = new ShellResult(
            Success: true, ExitCode: 0, Stdout: "ok\n", Stderr: "",
            AuthResult: PrivilegeAuthResult.AlreadyAuthorized);
        res.Success.Should().BeTrue();
        res.ExitCode.Should().Be(0);
        res.Stdout.Should().Be("ok\n");
        res.AuthResult.Should().Be(PrivilegeAuthResult.AlreadyAuthorized);
    }

    [Fact]
    public void PrivilegeAuthResult_enum_has_four_states()
    {
        var values = Enum.GetValues<PrivilegeAuthResult>();
        values.Should().HaveCount(4);
        values.Should().Contain(new[]
        {
            PrivilegeAuthResult.AlreadyAuthorized,
            PrivilegeAuthResult.Prompted,
            PrivilegeAuthResult.Denied,
            PrivilegeAuthResult.HelperMissing,
        });
    }
}
