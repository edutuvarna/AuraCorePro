using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application.Interfaces.Guards;

namespace AuraCore.Guard.Security;

public sealed class SecurityGuard : ISecurityGuard
{
    public bool ValidatePath(string path) => !path.Contains("..");
    public bool ValidateRegistryPath(string registryPath) => true;
}

public static class SecurityGuardRegistration
{
    public static IServiceCollection AddSecurityGuard(this IServiceCollection services)
    {
        services.AddSingleton<ISecurityGuard, SecurityGuard>();
        return services;
    }
}
