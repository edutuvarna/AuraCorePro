namespace AuraCore.UI.Avalonia.Services.AI;

/// <summary>
/// Read-only catalog of AI models known to AuraCore.
/// Hardcoded in Phase 3; future phases may load from remote manifest.
/// </summary>
public interface IModelCatalog
{
    IReadOnlyList<ModelDescriptor> All { get; }
    ModelDescriptor? FindById(string id);
    ModelDescriptor? FindByFilename(string filename);
}
