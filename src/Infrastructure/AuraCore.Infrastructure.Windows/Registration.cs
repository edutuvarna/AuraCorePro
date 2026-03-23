using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application.Interfaces.Infrastructure;
namespace AuraCore.Infrastructure.Windows;

public static class WindowsInfrastructureRegistration
{
    public static IServiceCollection AddWindowsInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IRegistryService, WindowsRegistryService>();
        services.AddSingleton<IServiceController, WindowsServiceController>();
        services.AddSingleton<IFileService, WindowsFileService>();
        return services;
    }
}
