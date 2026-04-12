namespace AuraCore.Module.SpotlightManager.Models;

public sealed record SpotlightReport(
    IReadOnlyList<SpotlightVolumeInfo> Volumes,
    int TotalVolumes,
    int EnabledCount,
    int DisabledCount,
    bool IsAvailable)
{
    public static SpotlightReport None() => new(
        Array.Empty<SpotlightVolumeInfo>(), 0, 0, 0, false);
}
