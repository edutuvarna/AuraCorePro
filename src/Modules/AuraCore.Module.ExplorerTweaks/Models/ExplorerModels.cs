namespace AuraCore.Module.ExplorerTweaks.Models;

public sealed record ExplorerReport
{
    public List<ExplorerTweak> Tweaks { get; init; } = new();
    public int AppliedCount => Tweaks.Count(t => t.IsApplied);
}

public sealed record ExplorerTweak
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Category { get; init; } = "";
    public string Risk { get; init; } = "Safe";
    public bool IsApplied { get; init; }
    public string RegistryPath { get; init; } = "";
    public string ValueName { get; init; } = "";
    public int EnabledValue { get; init; }
    public int DisabledValue { get; init; }
}
