using AuraCore.Application.Interfaces.Infrastructure;
namespace AuraCore.Infrastructure.Windows;

public sealed class WindowsRegistryService : IRegistryService
{
    public Task<string?> ReadValueAsync(string keyPath, string valueName, CancellationToken ct = default) => Task.FromResult<string?>(null);
    public Task WriteValueAsync(string keyPath, string valueName, object value, CancellationToken ct = default) => Task.CompletedTask;
    public Task DeleteKeyAsync(string keyPath, CancellationToken ct = default) => Task.CompletedTask;
    public Task<IReadOnlyList<string>> EnumerateSubKeysAsync(string keyPath, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
}
