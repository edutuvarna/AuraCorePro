namespace AuraCore.Module.BrewManager.Models;

public sealed record BrewPackageInfo(
    string Name,
    string CurrentVersion,
    string LatestVersion,
    bool IsCask);
