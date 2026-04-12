namespace AuraCore.Module.PackageCleaner.Models;

public sealed record PackageCleanerReport(
    string PackageManager,
    long CacheBytes,
    int InstalledPackages,
    int OrphanedPackages,
    bool IsAvailable)
{
    public static PackageCleanerReport None() => new("none", 0, 0, 0, false);
}
