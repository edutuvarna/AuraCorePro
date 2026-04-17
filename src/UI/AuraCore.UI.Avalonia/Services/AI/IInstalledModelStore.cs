using System.IO;

namespace AuraCore.UI.Avalonia.Services.AI;

/// <summary>
/// Represents a model file present on disk, resolved against the catalog.
/// </summary>
public record InstalledModel(
    string ModelId,
    FileInfo File,
    long SizeBytes,
    DateTime DownloadedAt);

/// <summary>
/// Enumerates locally-installed models (gguf files in the install directory).
/// Phase 3 read-only; Phase 4+ adds DeleteAsync.
/// </summary>
public interface IInstalledModelStore
{
    IReadOnlyList<InstalledModel> Enumerate();
    bool IsInstalled(string modelId);
    FileInfo? GetFile(string modelId);
}
