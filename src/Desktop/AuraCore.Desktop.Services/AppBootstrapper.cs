using AuraCore.Application.Interfaces.Platform;
using AuraCore.Desktop.Services.PrivilegeIpc;
using AuraCore.Infrastructure.PrivilegeIpc;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.Desktop;

/// <summary>
/// Builds the application's platform-agnostic service registrations.
/// Module and Windows-specific registrations are added by the caller (App.xaml.cs)
/// after receiving the <see cref="ServiceCollection"/> via <see cref="CreateServices"/>.
/// </summary>
public static class AppBootstrapper
{
    /// <summary>
    /// Returns a fully-built <see cref="IServiceProvider"/> containing the
    /// platform-agnostic core registrations (privilege IPC, helper availability).
    /// Use this overload in integration tests.
    /// </summary>
    public static IServiceProvider BuildServices() =>
        CreateServices().BuildServiceProvider();

    /// <summary>
    /// Creates and populates a <see cref="ServiceCollection"/> with the
    /// platform-agnostic core registrations, ready for callers to add further
    /// registrations before building the provider.
    /// </summary>
    public static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddPrivilegeIpc();
        services.AddSingleton<IHelperAvailabilityService, HelperAvailabilityService>();

        return services;
    }
}
