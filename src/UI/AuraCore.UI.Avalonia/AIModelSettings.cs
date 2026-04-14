using System.Text.Json;

namespace AuraCore.UI.Avalonia;

/// <summary>
/// Persists user's AI model choice to disk.
/// </summary>
public static class AIModelSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AuraCorePro", "ai_model.json");

    public static string SelectedModel { get; private set; } = "phi3-mini"; // default

    public static readonly ModelInfo[] AvailableModels = new[]
    {
        new ModelInfo("phi2", "Phi-2 2.7B", "1.7 GB", "~3 GB RAM", "Basic", "8 GB+ RAM"),
        new ModelInfo("tinyllama", "TinyLlama 1.1B", "2.1 GB", "~3 GB RAM", "Basic", "8 GB+ RAM"),
        new ModelInfo("phi3-mini", "Phi-3 Mini 3.8B", "2.3 GB", "~4 GB RAM", "Good", "12 GB+ RAM"),
        new ModelInfo("mistral-7b", "Mistral 7B", "4.1 GB", "~7 GB RAM", "Great", "16 GB RAM"),
        new ModelInfo("llama3-8b", "Llama 3 8B", "4.6 GB", "~7 GB RAM", "Great", "16 GB RAM"),
        new ModelInfo("llama2-13b", "Llama 2 13B", "7.4 GB", "~12 GB RAM", "Excellent", "24 GB+ RAM"),
        new ModelInfo("phi3-medium", "Phi-3 Medium 14B", "8.0 GB", "~12 GB RAM", "Best", "24 GB+ RAM"),
    };

    public static void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("model", out var m))
                    SelectedModel = m.GetString() ?? "phi3";
            }
        }
        catch { }
    }

    public static void Save(string modelKey)
    {
        SelectedModel = modelKey;
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (dir != null) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(new { model = modelKey });
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }

    /// <summary>Returns the GGUF filename for the selected model.</summary>
    public static string GetModelFileName()
    {
        return SelectedModel switch
        {
            "phi2" => "auracore-phi2-q4.gguf",
            "tinyllama" => "auracore-tinyllama.gguf",
            "phi3-mini" => "auracore-phi3-mini-q4.gguf",
            "mistral-7b" => "auracore-mistral-7b-q4.gguf",
            "llama3-8b" => "auracore-llama3-8b-q4.gguf",
            "llama2-13b" => "auracore-llama2-13b-q4.gguf",
            "phi3-medium" => "auracore-phi3-medium-q4.gguf",
            _ => "auracore-phi3-mini-q4.gguf",
        };
    }
}

public sealed record ModelInfo(string Key, string DisplayName, string DiskSize, string RamUsage, string Quality, string Recommended);
