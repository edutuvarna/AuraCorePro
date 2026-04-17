namespace AuraCore.UI.Avalonia.Services.AI;

/// <summary>
/// Capability tier of an AI model, tied to approximate RAM requirements.
/// </summary>
public enum ModelTier
{
    Lite,      // < 4 GB RAM
    Standard,  // 4-8 GB RAM
    Advanced,  // 16 GB RAM
    Heavy      // 32+ GB RAM
}

/// <summary>
/// Rough inference speed class for a model on typical hardware.
/// </summary>
public enum SpeedClass
{
    Fast,
    Medium,
    Slow
}

/// <summary>
/// Metadata for an AI model available in the AuraCore catalog.
/// Used by IModelCatalog; localized display via DescriptionKey.
/// </summary>
public record ModelDescriptor(
    string Id,
    string DisplayName,
    string Filename,
    long SizeBytes,
    long EstimatedRamBytes,
    ModelTier Tier,
    SpeedClass Speed,
    bool IsRecommended,
    string DescriptionKey);
