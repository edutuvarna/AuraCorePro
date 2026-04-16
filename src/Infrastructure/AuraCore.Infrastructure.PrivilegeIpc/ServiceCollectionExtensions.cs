using AuraCore.Application.Interfaces.Platform;
using AuraCore.Infrastructure.PrivilegeIpc.Linux;
using AuraCore.Infrastructure.PrivilegeIpc.MacOS;
using AuraCore.Infrastructure.PrivilegeIpc.Windows;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.InteropServices;

namespace AuraCore.Infrastructure.PrivilegeIpc;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IShellCommandService"/> as a singleton.
    /// Platform resolution: Windows → <see cref="WindowsShellCommandService"/> (5.2 stub),
    /// Linux → <see cref="LinuxShellCommandService"/>, macOS → <see cref="MacOSShellCommandService"/>.
    /// Set <paramref name="useInProcess"/>=true (tests / dev builds) to register
    /// <see cref="InProcessShellCommandService"/> regardless of platform.
    /// </summary>
    public static IServiceCollection AddPrivilegeIpc(
        this IServiceCollection services, bool useInProcess = false)
    {
        if (useInProcess)
        {
            services.AddSingleton<IShellCommandService, InProcessShellCommandService>();
            return services;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            services.AddSingleton<IShellCommandService, WindowsShellCommandService>();
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            services.AddSingleton<IPrivHelperConnectionFactory, DefaultPrivHelperConnectionFactory>();
            services.AddSingleton<IShellCommandService, LinuxShellCommandService>();

            // Phase 5.2.1.10: installer wiring — Linux-only
            services.AddSingleton<IPkexecInvoker, DefaultPkexecInvoker>();
            services.AddSingleton<IDaemonBinaryLocator, DefaultDaemonBinaryLocator>();
            services.AddSingleton<PrivHelperInstaller>();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            services.AddSingleton<IShellCommandService, MacOSShellCommandService>();
        else
            services.AddSingleton<IShellCommandService, InProcessShellCommandService>();

        return services;
    }
}
