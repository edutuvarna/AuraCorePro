namespace AuraCore.Application.Interfaces.Platform;

public interface IUpdateEngine
{
    Task<UpdateManifest?> CheckAsync(CancellationToken ct = default);
    Task DownloadAsync(UpdateManifest manifest, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default);
    Task<UpdateResult> InstallAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}

public sealed record UpdateManifest(string Version, long SizeBytes, string Hash, string? ReleaseNotes);
public sealed record DownloadProgress(long BytesDownloaded, long TotalBytes);
public sealed record UpdateResult(bool Success, string? Error);
