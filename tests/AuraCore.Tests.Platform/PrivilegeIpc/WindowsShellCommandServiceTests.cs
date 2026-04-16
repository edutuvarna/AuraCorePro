using AuraCore.Application.Interfaces.Platform;
using AuraCore.Infrastructure.PrivilegeIpc.Windows;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AuraCore.Tests.Platform.PrivilegeIpc;

public class WindowsShellCommandServiceTests
{
    [Fact]
    public async Task RunPrivilegedAsync_returns_HelperMissing_until_phase_5_5()
    {
        var svc = new WindowsShellCommandService(NullLogger<WindowsShellCommandService>.Instance);
        var cmd = new PrivilegedCommand("test", "whoami", Array.Empty<string>());
        var result = await svc.RunPrivilegedAsync(cmd);

        result.Success.Should().BeFalse();
        result.AuthResult.Should().Be(PrivilegeAuthResult.HelperMissing);
        result.Stderr.Should().Contain("not implemented");
    }
}
