using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application.Interfaces.Engines;
using AuraCore.Engine.AIAnalyzer.Config;
using AuraCore.Engine.AIAnalyzer.LLM;
using AuraCore.Engine.AIAnalyzer.Models;

namespace AuraCore.Engine.AIAnalyzer;

public static class AIAnalyzerRegistration
{
    public static IServiceCollection AddAIAnalyzer(this IServiceCollection services)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var aiDir = Path.Combine(appData, "AuraCorePro", "ai");
        Directory.CreateDirectory(aiDir);
        var dbPath = Path.Combine(aiDir, "metrics.db");
        var profilePath = Path.Combine(aiDir, "profile.db");
        services.AddSingleton<IAIAnalyzerEngine>(sp => new AIAnalyzerEngine(dbPath, profilePath));

        // ONNX + LLM + Config
        var modelsDir = Path.Combine(aiDir, "models");
        Directory.CreateDirectory(modelsDir);
        var onnxModelPath = Path.Combine(modelsDir, "auracore_anomaly.onnx");
        var onnxThresholdPath = Path.Combine(modelsDir, "anomaly_threshold.json");
        // Dynamic model selection based on user preference
        var aiModelConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AuraCorePro", "ai_model.json");
        var selectedModel = "phi3"; // default
        if (File.Exists(aiModelConfigPath))
        {
            try
            {
                var json = File.ReadAllText(aiModelConfigPath);
                var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("model", out var m))
                    selectedModel = m.GetString() ?? "phi3-mini";
            }
            catch { }
        }
        var modelFileName = ResolveModelFileName(selectedModel);
        var llmModelPath = Path.Combine(modelsDir, modelFileName);
        var configPath = Path.Combine(aiDir, "optimal_params.json");

        // RAG endpoint (default: localhost:5000, can be overridden via env var)
        var ragEndpoint = Environment.GetEnvironmentVariable("AURACORE_RAG_URL")
                          ?? "http://localhost:5000/api/retrieve";

        services.AddSingleton(sp => new OnnxAnomalyDetector(
            File.Exists(onnxModelPath) ? onnxModelPath : null,
            File.Exists(onnxThresholdPath) ? onnxThresholdPath : null));
        services.AddSingleton<IAuraCoreLLM>(sp => new LlmInferenceEngine(
            File.Exists(llmModelPath) ? llmModelPath : null,
            ragEndpointUrl: ragEndpoint));
        services.AddSingleton(sp => AIConfigProvider.LoadFromFile(configPath));

        return services;
    }

    /// <summary>
    /// Resolves a user-friendly model name to its GGUF filename.
    /// </summary>
    public static string ResolveModelFileName(string selectedModel)
    {
        return selectedModel switch
        {
            "tinyllama" => "auracore-tinyllama.gguf",
            "phi2" => "auracore-phi2.gguf",
            "phi3-mini" => "auracore-phi3-mini.gguf",
            "mistral-7b" => "auracore-mistral-7b.gguf",
            "llama31-8b" => "auracore-llama31-8b.gguf",
            "phi3-medium" => "auracore-phi3-medium.gguf",
            "qwen25-32b" => "auracore-qwen25-32b.gguf",
            // Legacy aliases
            "phi3" => "auracore-phi3-mini.gguf",
            "llama3-8b" => "auracore-llama31-8b.gguf",
            _ => "auracore-phi3-mini.gguf",
        };
    }
}
