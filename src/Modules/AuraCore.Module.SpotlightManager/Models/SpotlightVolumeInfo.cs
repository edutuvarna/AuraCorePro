namespace AuraCore.Module.SpotlightManager.Models;

public sealed record SpotlightVolumeInfo(
    string MountPoint,
    bool IndexingEnabled,
    long IndexSizeBytes);
