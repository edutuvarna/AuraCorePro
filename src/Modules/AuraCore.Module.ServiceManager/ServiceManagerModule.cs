using AuraCore.Application.Interfaces.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.Module.ServiceManager;

public static class ServiceManagerModule
{
    public static IServiceCollection AddServiceManager(this IServiceCollection services)
    {
        services.AddSingleton<ServiceManagerEngine>(sp =>
        {
            var shell = sp.GetService<IShellCommandService>();
            return shell is not null ? new ServiceManagerEngine(shell) : new ServiceManagerEngine();
        });
        return services;
    }
}
