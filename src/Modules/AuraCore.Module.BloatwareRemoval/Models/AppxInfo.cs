namespace AuraCore.Module.BloatwareRemoval.Models;

public sealed record AppxInfo
{
    public string PackageFullName { get; init; } = "";
    public string Name { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Publisher { get; init; } = "";
    public string Version { get; init; } = "";
    public string InstallLocation { get; init; } = "";
    public long EstimatedSizeBytes { get; init; }
    public BloatCategory Category { get; init; } = BloatCategory.UserInstalled;
    public BloatRisk Risk { get; init; } = BloatRisk.Safe;
    public string RiskReason { get; init; } = "";
    public bool IsFramework { get; init; }

    /// <summary>Community removal score: 0-100. Higher = more people recommend removal.</summary>
    public int CommunityScore { get; init; }
    /// <summary>Total community votes.</summary>
    public int CommunityVotes { get; init; }

    public string SizeDisplay => EstimatedSizeBytes switch
    {
        0 => "Unknown",
        < 1024 * 1024 => $"{EstimatedSizeBytes / 1024.0:F0} KB",
        < 1024L * 1024 * 1024 => $"{EstimatedSizeBytes / (1024.0 * 1024):F1} MB",
        _ => $"{EstimatedSizeBytes / (1024.0 * 1024 * 1024):F2} GB"
    };

    public string CommunityLabel => CommunityScore switch
    {
        >= 85 => "Strongly Recommended",
        >= 65 => "Recommended",
        >= 40 => "Mixed",
        >= 1 => "Keep",
        _ => ""
    };
}

public enum BloatCategory
{
    MicrosoftBloat,     // Ships with Windows, not needed
    OemBloat,           // Pre-installed by manufacturer
    UserInstalled,      // User chose to install
    SystemRequired,     // Required for Windows to function
    Framework           // Runtime dependency — never remove
}

public enum BloatRisk
{
    Safe,       // Can remove without any consequence
    Caution,    // Works fine but some users might want it
    Warning,    // Removing may affect other apps
    System      // Do NOT remove — system dependency
}

public sealed record BloatwareScanReport
{
    public List<AppxInfo> Apps { get; init; } = new();
    public int TotalApps => Apps.Count;
    public int RemovableApps => Apps.Count(a => a.Risk != BloatRisk.System && !a.IsFramework);
    public long TotalRemovableBytes => Apps.Where(a => a.Risk != BloatRisk.System && !a.IsFramework).Sum(a => a.EstimatedSizeBytes);
}
