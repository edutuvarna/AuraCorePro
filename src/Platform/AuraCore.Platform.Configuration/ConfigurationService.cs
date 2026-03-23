using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Reactive.Subjects;
using AuraCore.Application.Interfaces.Platform;

namespace AuraCore.Platform.Configuration;

public sealed class ConfigurationService : IConfigurationService, IDisposable
{
    private readonly ConcurrentDictionary<string, object> _cache = new();
    private readonly Subject<ConfigChangeEvent> _changes = new();
    public IObservable<ConfigChangeEvent> Changes => _changes;
    public T Get<T>(string section) where T : class, new() => _cache.GetOrAdd(section, _ => new T()) as T ?? new T();
    public Task SetAsync<T>(string section, T value, CancellationToken ct = default) { _cache[section] = value!; return Task.CompletedTask; }
    public Task<string?> GetSecureAsync(string key, CancellationToken ct = default) => Task.FromResult<string?>(null);
    public Task SetSecureAsync(string key, string value, CancellationToken ct = default) => Task.CompletedTask;
    public Task RefreshRemoteAsync(CancellationToken ct = default) => Task.CompletedTask;
    public void Dispose() => _changes.Dispose();
}

public static class ConfigurationRegistration
{
    public static IServiceCollection AddAuraCoreConfiguration(this IServiceCollection services)
    {
        services.AddSingleton<ConfigurationService>();
        return services.AddSingleton<IConfigurationService>(sp => sp.GetRequiredService<ConfigurationService>());
    }
}
