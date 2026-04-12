namespace AuraCore.Module.XcodeCleaner.Models;

public sealed record XcodeCleanerReport(
    bool XcodeInstalled,
    string HomeDir,
    IReadOnlyList<XcodeCacheCategory> Categories,
    long TotalBytes)
{
    public static XcodeCleanerReport None() => new(false, "", Array.Empty<XcodeCacheCategory>(), 0);
}
