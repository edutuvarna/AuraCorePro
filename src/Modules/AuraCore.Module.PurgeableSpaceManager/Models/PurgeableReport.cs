namespace AuraCore.Module.PurgeableSpaceManager.Models;

public sealed record PurgeableReport(
    long VolumeFreeBytes,
    long ContainerFreeBytes,
    long PurgeableBytes,
    int LocalSnapshotCount,
    IReadOnlyList<string> LocalSnapshots,
    bool IsAvailable)
{
    public static PurgeableReport None() => new(0, 0, 0, 0, Array.Empty<string>(), false);
}
