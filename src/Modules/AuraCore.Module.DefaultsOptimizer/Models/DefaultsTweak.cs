namespace AuraCore.Module.DefaultsOptimizer.Models;

public sealed record DefaultsTweak(
    string Id,           // kebab-case unique identifier
    string Category,     // "Finder", "Dock", "System", "Screenshots"
    string Name,         // display name
    string Description,  // user-facing description
    string Domain,       // e.g. "com.apple.finder"
    string Key,          // e.g. "AppleShowAllFiles"
    string Type,         // "bool", "int", "float", "string"
    string RecommendedValue)
{
    public string? CurrentValue { get; init; }
    public bool IsApplied => CurrentValue == RecommendedValue;
}
