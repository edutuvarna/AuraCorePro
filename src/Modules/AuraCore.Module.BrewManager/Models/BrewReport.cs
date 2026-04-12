namespace AuraCore.Module.BrewManager.Models;

public sealed record BrewReport(
    bool BrewInstalled,
    string? BrewPath,
    string BrewVersion,
    int InstalledFormulas,
    int InstalledCasks,
    IReadOnlyList<BrewPackageInfo> Outdated,
    long CacheBytes)
{
    public static BrewReport None() => new(false, null, "", 0, 0, Array.Empty<BrewPackageInfo>(), 0);
}
