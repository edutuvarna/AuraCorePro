namespace AuraCore.Module.PurgeableSpaceManager.Models;

/// <summary>
/// Purgeable space report from <see cref="PurgeableSpaceManagerModule"/>.
/// Phase 4.4.2 added the trailing optional <see cref="TotalCapacityBytes"/>
/// field so the UI can compute Used/Purgeable/Free proportions for the hero
/// stacked-bar visualization. The additive positional parameter keeps the
/// original record shape compatible with existing callers.
/// </summary>
public sealed record PurgeableReport(
    long VolumeFreeBytes,
    long ContainerFreeBytes,
    long PurgeableBytes,
    int LocalSnapshotCount,
    IReadOnlyList<string> LocalSnapshots,
    bool IsAvailable,
    long TotalCapacityBytes = 0)
{
    public static PurgeableReport None() => new(0, 0, 0, 0, Array.Empty<string>(), false, 0);
}
