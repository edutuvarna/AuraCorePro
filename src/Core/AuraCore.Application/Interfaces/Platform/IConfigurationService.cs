namespace AuraCore.Application.Interfaces.Platform;

public interface IConfigurationService
{
    T Get<T>(string section) where T : class, new();
    Task SetAsync<T>(string section, T value, CancellationToken ct = default);
    Task<string?> GetSecureAsync(string key, CancellationToken ct = default);
    Task SetSecureAsync(string key, string value, CancellationToken ct = default);
    Task RefreshRemoteAsync(CancellationToken ct = default);
    IObservable<ConfigChangeEvent> Changes { get; }
}

public sealed record ConfigChangeEvent(string Key, string? OldValue, string? NewValue, string SourceLayer);
