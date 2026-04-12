namespace AuraCore.Module.TimeMachineManager.Models;

public sealed record TimeMachineReport(
    bool Configured,
    string? Destination,
    IReadOnlyList<TimeMachineBackup> Backups,
    int LocalSnapshotCount,
    IReadOnlyList<string> LocalSnapshots,
    bool IsAvailable)
{
    public static TimeMachineReport None() => new(
        false, null, Array.Empty<TimeMachineBackup>(), 0, Array.Empty<string>(), false);
}
