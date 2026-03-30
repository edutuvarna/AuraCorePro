namespace AuraCore.Module.AutorunManager.Models;

public enum AutorunType { Registry, StartupFolder, ScheduledTask }

public sealed class AutorunEntry
{
    public string Name         { get; set; } = "";
    public string Command      { get; set; } = "";
    public string Location     { get; set; } = "";
    public AutorunType Type    { get; set; }
    public bool IsEnabled      { get; set; } = true;
    public string Publisher    { get; set; } = "";
    public string RiskLevel    { get; set; } = "Unknown";
    public string FilePath     { get; set; } = "";
    // Registry-specific
    public string RegistryHive      { get; set; } = "";
    public string RegistrySubKey    { get; set; } = "";
    public string RegistryValueName { get; set; } = "";
}

public sealed class AutorunReport
{
    public List<AutorunEntry> Entries { get; set; } = new();
    public int EnabledCount  => Entries.Count(e => e.IsEnabled);
    public int DisabledCount => Entries.Count(e => !e.IsEnabled);
}
