using AuraCore.Application.Interfaces.Platform;
using AuraCore.Infrastructure.PrivilegeIpc;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AuraCore.Tests.Platform.PrivilegeIpc;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddPrivilegeIpc_registers_IShellCommandService_singleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPrivilegeIpc();
        var sp = services.BuildServiceProvider();

        var svc1 = sp.GetRequiredService<IShellCommandService>();
        var svc2 = sp.GetRequiredService<IShellCommandService>();
        svc1.Should().BeSameAs(svc2);
        svc1.Should().NotBeNull();
    }

    [Fact]
    public void AddPrivilegeIpc_resolves_platform_specific_impl()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPrivilegeIpc();
        var sp = services.BuildServiceProvider();
        var svc = sp.GetRequiredService<IShellCommandService>();

        if (OperatingSystem.IsWindows())
            svc.Should().BeOfType<AuraCore.Infrastructure.PrivilegeIpc.Windows.WindowsShellCommandService>();
        else if (OperatingSystem.IsLinux())
            svc.Should().BeOfType<AuraCore.Infrastructure.PrivilegeIpc.Linux.LinuxShellCommandService>();
        else if (OperatingSystem.IsMacOS())
            svc.Should().BeOfType<AuraCore.Infrastructure.PrivilegeIpc.MacOS.MacOSShellCommandService>();
    }

    [Fact]
    public void AddPrivilegeIpc_with_inprocess_override_uses_InProcess()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPrivilegeIpc(useInProcess: true);
        var svc = services.BuildServiceProvider().GetRequiredService<IShellCommandService>();
        svc.Should().BeOfType<InProcessShellCommandService>();
    }
}
