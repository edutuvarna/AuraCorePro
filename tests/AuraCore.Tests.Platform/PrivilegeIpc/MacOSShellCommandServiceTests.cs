using AuraCore.Application.Interfaces.Platform;
using AuraCore.Infrastructure.PrivilegeIpc.MacOS;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AuraCore.Tests.Platform.PrivilegeIpc;

public class MacOSShellCommandServiceTests
{
    [Fact]
    public async Task RunPrivilegedAsync_returns_HelperMissing_before_macos_impl_lands()
    {
        var svc = new MacOSShellCommandService(NullLogger<MacOSShellCommandService>.Instance);
        var result = await svc.RunPrivilegedAsync(
            new PrivilegedCommand("dns-flush", "dscacheutil", new[] { "-flushcache" }));
        result.AuthResult.Should().Be(PrivilegeAuthResult.HelperMissing);
        result.Success.Should().BeFalse();
    }
}
