using AuraCore.Application.Interfaces.Platform;
using AuraCore.Infrastructure.PrivilegeIpc.Linux;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AuraCore.Tests.Platform.PrivilegeIpc;

public class LinuxShellCommandServiceTests
{
    [Fact]
    public async Task RunPrivilegedAsync_returns_HelperMissing_before_linux_impl_lands()
    {
        var svc = new LinuxShellCommandService(NullLogger<LinuxShellCommandService>.Instance);
        var result = await svc.RunPrivilegedAsync(
            new PrivilegedCommand("journal", "journalctl", new[] { "--vacuum-size=500M" }));
        result.AuthResult.Should().Be(PrivilegeAuthResult.HelperMissing);
        result.Success.Should().BeFalse();
    }
}
