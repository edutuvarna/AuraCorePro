namespace AuraCore.Application.Interfaces.Infrastructure;

public interface IRegistryService
{
    Task<string?> ReadValueAsync(string keyPath, string valueName, CancellationToken ct = default);
    Task WriteValueAsync(string keyPath, string valueName, object value, CancellationToken ct = default);
    Task DeleteKeyAsync(string keyPath, CancellationToken ct = default);
    Task<IReadOnlyList<string>> EnumerateSubKeysAsync(string keyPath, CancellationToken ct = default);
}
