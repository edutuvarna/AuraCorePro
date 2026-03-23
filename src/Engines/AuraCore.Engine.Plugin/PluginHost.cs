using Microsoft.Extensions.DependencyInjection;
namespace AuraCore.Engine.Plugin;

public sealed class PluginHost { }

public static class PluginHostRegistration
{
    public static IServiceCollection AddPluginHost(this IServiceCollection services)
        => services.AddSingleton<PluginHost>();
}
