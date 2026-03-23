namespace AuraCore.Application.Interfaces.Infrastructure;

public interface IFileService
{
    Task<long> GetFileSizeAsync(string path, CancellationToken ct = default);
    Task DeleteFileAsync(string path, CancellationToken ct = default);
    Task<IReadOnlyList<string>> EnumerateFilesAsync(string directory, string pattern, CancellationToken ct = default);
    Task CopyFileAsync(string source, string destination, CancellationToken ct = default);
}
