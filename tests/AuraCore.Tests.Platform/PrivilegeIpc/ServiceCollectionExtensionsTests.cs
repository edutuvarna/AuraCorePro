using AuraCore.Application.Interfaces.Platform;
using AuraCore.Infrastructure.PrivilegeIpc;
using AuraCore.Infrastructure.PrivilegeIpc.Linux;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
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

    [Fact]
    public void AddPrivilegeIpc_on_linux_registers_PrivHelperInstaller_and_dependencies()
    {
        if (!OperatingSystem.IsLinux()) return;  // Skip on non-Linux — Windows/macOS have their own paths

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPrivilegeIpc();
        services.AddSingleton<IHelperAvailabilityService>(Substitute.For<IHelperAvailabilityService>());
        var sp = services.BuildServiceProvider();

        sp.GetService<PrivHelperInstaller>().Should().NotBeNull();
        sp.GetService<IPkexecInvoker>().Should().NotBeNull();
        sp.GetService<IDaemonBinaryLocator>().Should().NotBeNull();
    }

    [Fact]
    public void AddPrivilegeIpc_on_non_linux_does_not_require_installer_resolution()
    {
        // On Windows / macOS, PrivHelperInstaller may or may not be registered
        // (it's Linux-specific). The key invariant is that IShellCommandService
        // still resolves. This is already covered by the other tests.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPrivilegeIpc();
        var sp = services.BuildServiceProvider();
        sp.GetRequiredService<IShellCommandService>().Should().NotBeNull();
    }
}
