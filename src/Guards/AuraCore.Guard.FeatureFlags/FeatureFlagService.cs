using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application.Interfaces.Guards;

namespace AuraCore.Guard.FeatureFlags;

public sealed class FeatureFlagService : IFeatureFlagService
{
    public bool IsEnabled(string flagName) => true;
    public T GetValue<T>(string flagName, T defaultValue) => defaultValue;
    public Task RefreshAsync(CancellationToken ct = default) => Task.CompletedTask;
}

public static class FeatureFlagRegistration
{
    public static IServiceCollection AddFeatureFlags(this IServiceCollection services)
    {
        services.AddSingleton<IFeatureFlagService, FeatureFlagService>();
        return services;
    }
}
