using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AuraCore.UI.Avalonia.Services.AI;

/// <summary>
/// Progress snapshot reported during a download.
/// </summary>
public record DownloadProgress(
    long BytesReceived,
    long TotalBytes,
    double BytesPerSecond,
    TimeSpan? EstimatedTimeRemaining);

/// <summary>
/// Configuration for the download service. Read from appsettings.json.
/// </summary>
public record ModelDownloadSettings(
    string BaseUrl,
    string InstallDirectory,
    int TimeoutMinutes,
    int BufferKb,
    string UserAgent);

/// <summary>
/// Thrown when the downloaded file size does not match the catalog's declared size.
/// </summary>
public sealed class ModelSizeMismatchException : Exception
{
    public long ExpectedBytes { get; }
    public long ActualBytes { get; }
    public ModelSizeMismatchException(long expected, long actual)
        : base($"Model size mismatch: expected {expected} bytes, got {actual} bytes.")
    { ExpectedBytes = expected; ActualBytes = actual; }
}

/// <summary>
/// Downloads model files from the catalog base URL with progress reporting.
/// Phase 3 minimum: no resume, no checksum. Size verification only.
/// </summary>
public interface IModelDownloadService
{
    Task<FileInfo> DownloadAsync(
        ModelDescriptor model,
        IProgress<DownloadProgress> progress,
        CancellationToken ct);
}
