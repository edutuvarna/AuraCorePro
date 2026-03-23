namespace AuraCore.Module.ContextMenu.Models;

public sealed record ContextMenuReport
{
    public bool IsClassicMenuEnabled { get; init; }
    public List<ContextMenuTweak> Tweaks { get; init; } = new();
    public int AppliedCount => Tweaks.Count(t => t.IsApplied);
}

public sealed record ContextMenuTweak
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Category { get; init; } = "";
    public string Risk { get; init; } = "Safe";
    public bool IsApplied { get; init; }
    public string RegistryPath { get; init; } = "";
    public string RegistryValue { get; init; } = "";
    public RegistryAction Action { get; init; }
}

public enum RegistryAction
{
    CreateKey,
    DeleteKey,
    SetDword,
    DeleteValue
}
