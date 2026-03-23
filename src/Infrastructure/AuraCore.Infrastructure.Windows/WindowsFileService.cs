using AuraCore.Application.Interfaces.Infrastructure;
namespace AuraCore.Infrastructure.Windows;

public sealed class WindowsFileService : IFileService
{
    public Task<long> GetFileSizeAsync(string path, CancellationToken ct = default) => Task.FromResult(0L);
    public Task DeleteFileAsync(string path, CancellationToken ct = default) => Task.CompletedTask;
    public Task<IReadOnlyList<string>> EnumerateFilesAsync(string dir, string pattern, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    public Task CopyFileAsync(string source, string dest, CancellationToken ct = default) => Task.CompletedTask;
}
