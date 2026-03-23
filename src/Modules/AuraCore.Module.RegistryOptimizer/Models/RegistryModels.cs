namespace AuraCore.Module.RegistryOptimizer.Models;

public sealed record RegistryScanReport
{
    public List<RegistryIssue> Issues { get; init; } = new();
    public int TotalIssues => Issues.Count;
    public int SafeIssues => Issues.Count(i => i.Risk == "Safe");
    public int CautionIssues => Issues.Count(i => i.Risk == "Caution");
    public string? BackupPath { get; set; }
}

public sealed record RegistryIssue
{
    public string Id { get; init; } = "";
    public string Category { get; init; } = "";
    public string Description { get; init; } = "";
    public string KeyPath { get; init; } = "";
    public string? ValueName { get; init; }
    public string Risk { get; init; } = "Safe";
    public string Detail { get; init; } = "";
    public IssueType Type { get; init; }
}

public enum IssueType
{
    OrphanedUninstallEntry,
    BrokenFileAssociation,
    InvalidSharedDll,
    ObsoleteMuiCache,
    StaleAppPath,
    OrphanedComEntry,
    EmptyRegistryKey
}
