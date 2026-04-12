namespace AuraCore.Module.SymlinkManager.Models;

public sealed record SymlinkReport(
    IReadOnlyList<SymlinkInfo> Symlinks,
    int TotalCount,
    int BrokenCount,
    int CircularCount,
    IReadOnlyList<string> ScannedDirectories,
    bool IsAvailable)
{
    public static SymlinkReport None() => new(
        Array.Empty<SymlinkInfo>(), 0, 0, 0, Array.Empty<string>(), false);
}
