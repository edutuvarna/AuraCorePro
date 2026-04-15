using System.IO;

namespace AuraCore.UI.Avalonia.Services.AI;

public sealed class InstalledModelStore : IInstalledModelStore
{
    private readonly IModelCatalog _catalog;
    private readonly string _installDir;

    public InstalledModelStore(IModelCatalog catalog, string? installDir = null)
    {
        _catalog = catalog;
        _installDir = installDir ?? DefaultInstallDir();
    }

    public static string DefaultInstallDir() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AuraCorePro", "models");

    public IReadOnlyList<InstalledModel> Enumerate()
    {
        if (!Directory.Exists(_installDir))
            return Array.Empty<InstalledModel>();

        var results = new List<InstalledModel>();
        foreach (var file in Directory.EnumerateFiles(_installDir, "auracore-*.gguf"))
        {
            var info = new FileInfo(file);
            var descriptor = _catalog.FindByFilename(info.Name);
            if (descriptor is null) continue; // orphan file — ignore

            results.Add(new InstalledModel(
                ModelId: descriptor.Id,
                File: info,
                SizeBytes: info.Length,
                DownloadedAt: info.CreationTimeUtc));
        }
        return results;
    }

    public bool IsInstalled(string modelId) =>
        GetFile(modelId) is not null;

    public FileInfo? GetFile(string modelId)
    {
        var descriptor = _catalog.FindById(modelId);
        if (descriptor is null) return null;

        var path = Path.Combine(_installDir, descriptor.Filename);
        return File.Exists(path) ? new FileInfo(path) : null;
    }
}
