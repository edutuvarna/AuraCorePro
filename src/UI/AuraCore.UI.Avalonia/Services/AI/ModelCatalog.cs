namespace AuraCore.UI.Avalonia.Services.AI;

/// <summary>
/// Hardcoded 8-model catalog per Phase 3 spec §6.1.
/// Sizes in bytes are approximate (±1% rounding acceptable for UI display).
/// </summary>
public sealed class ModelCatalog : IModelCatalog
{
    private const long GB = 1024L * 1024 * 1024;

    private static readonly IReadOnlyList<ModelDescriptor> _models = new[]
    {
        new ModelDescriptor(
            Id: "tinyllama",
            DisplayName: "TinyLlama",
            Filename: "auracore-tinyllama.gguf",
            SizeBytes: (long)(2.1 * GB),
            EstimatedRamBytes: 2L * GB,
            Tier: ModelTier.Lite,
            Speed: SpeedClass.Fast,
            IsRecommended: false,
            DescriptionKey: "modelManager.model.tinyllama.description"),

        new ModelDescriptor(
            Id: "phi3-mini-q4km",
            DisplayName: "Phi-3 Mini Q4KM",
            Filename: "auracore-phi3-mini-q4km.gguf",
            SizeBytes: (long)(2.3 * GB),
            EstimatedRamBytes: 3L * GB,
            Tier: ModelTier.Lite,
            Speed: SpeedClass.Fast,
            IsRecommended: true,
            DescriptionKey: "modelManager.model.phi3-mini-q4km.description"),

        new ModelDescriptor(
            Id: "phi2",
            DisplayName: "Phi-2",
            Filename: "auracore-phi2.gguf",
            SizeBytes: (long)(5.3 * GB),
            EstimatedRamBytes: 6L * GB,
            Tier: ModelTier.Standard,
            Speed: SpeedClass.Medium,
            IsRecommended: false,
            DescriptionKey: "modelManager.model.phi2.description"),

        new ModelDescriptor(
            Id: "phi3-mini",
            DisplayName: "Phi-3 Mini",
            Filename: "auracore-phi3-mini.gguf",
            SizeBytes: (long)(7.3 * GB),
            EstimatedRamBytes: 8L * GB,
            Tier: ModelTier.Standard,
            Speed: SpeedClass.Medium,
            IsRecommended: false,
            DescriptionKey: "modelManager.model.phi3-mini.description"),

        new ModelDescriptor(
            Id: "mistral-7b",
            DisplayName: "Mistral 7B",
            Filename: "auracore-mistral-7b.gguf",
            SizeBytes: 14L * GB,
            EstimatedRamBytes: 16L * GB,
            Tier: ModelTier.Advanced,
            Speed: SpeedClass.Slow,
            IsRecommended: false,
            DescriptionKey: "modelManager.model.mistral-7b.description"),

        new ModelDescriptor(
            Id: "llama31-8b",
            DisplayName: "Llama 3.1 8B",
            Filename: "auracore-llama31-8b.gguf",
            SizeBytes: 15L * GB,
            EstimatedRamBytes: 18L * GB,
            Tier: ModelTier.Advanced,
            Speed: SpeedClass.Slow,
            IsRecommended: false,
            DescriptionKey: "modelManager.model.llama31-8b.description"),

        new ModelDescriptor(
            Id: "phi3-medium",
            DisplayName: "Phi-3 Medium",
            Filename: "auracore-phi3-medium.gguf",
            SizeBytes: (long)(26.6 * GB),
            EstimatedRamBytes: 32L * GB,
            Tier: ModelTier.Heavy,
            Speed: SpeedClass.Slow,
            IsRecommended: false,
            DescriptionKey: "modelManager.model.phi3-medium.description"),

        new ModelDescriptor(
            Id: "qwen25-32b",
            DisplayName: "Qwen 2.5 32B",
            Filename: "auracore-qwen25-32b.gguf",
            SizeBytes: (long)(62.5 * GB),
            EstimatedRamBytes: 70L * GB,
            Tier: ModelTier.Heavy,
            Speed: SpeedClass.Slow,
            IsRecommended: false,
            DescriptionKey: "modelManager.model.qwen25-32b.description"),
    };

    public IReadOnlyList<ModelDescriptor> All => _models;

    public ModelDescriptor? FindById(string id) =>
        _models.FirstOrDefault(m => m.Id == id);

    public ModelDescriptor? FindByFilename(string filename) =>
        _models.FirstOrDefault(m => m.Filename == filename);
}
