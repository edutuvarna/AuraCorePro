namespace AuraCore.API.Application.Services.Releases;

public interface IR2Client
{
    Task<PresignedPutResult> GeneratePresignedPutUrlAsync(
        string objectKey, TimeSpan ttl, long maxSizeBytes, CancellationToken ct);

    Task<R2ObjectHead?> HeadObjectAsync(string objectKey, CancellationToken ct);

    Task CopyObjectAsync(string sourceKey, string destinationKey, CancellationToken ct);

    Task DeleteObjectAsync(string objectKey, CancellationToken ct);

    /// <summary>Streams the object from R2 and returns SHA256 hex (lowercase).</summary>
    Task<string> ComputeSha256Async(string objectKey, CancellationToken ct);

    /// <summary>Streams the object into the caller-provided Stream (for GitHub mirror).</summary>
    Task DownloadToStreamAsync(string objectKey, Stream destination, CancellationToken ct);
}
