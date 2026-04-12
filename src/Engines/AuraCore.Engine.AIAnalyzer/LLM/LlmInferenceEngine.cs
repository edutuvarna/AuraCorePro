using AuraCore.Application.Interfaces.Engines;
using AuraCore.Engine.AIAnalyzer.Rag;
using LLama;
using LLama.Common;
using LLama.Sampling;

namespace AuraCore.Engine.AIAnalyzer.LLM;

/// <summary>
/// LLM inference engine using LLamaSharp for local GGUF model execution.
/// Lazy-loads the model on first query. Thread-safe.
/// Supports optional RAG retrieval to inject relevant source code context.
/// </summary>
public sealed class LlmInferenceEngine : IAuraCoreLLM
{
    private readonly string? _modelPath;
    private readonly QdrantRetriever? _ragRetriever;
    private readonly object _lock = new();
    private bool _initialized;
    private bool _loadFailed;
    private LLamaWeights? _model;
    private LLamaContext? _context;
    private bool _disposed;

    private const int MaxTokens = 512;
    private const float InferenceTemperature = 0.7f;
    private const uint ContextSize = 2048;

    public LlmInferenceEngine(string? modelPath, string? ragEndpointUrl = null)
    {
        _modelPath = modelPath;
        if (!string.IsNullOrEmpty(ragEndpointUrl))
            _ragRetriever = new QdrantRetriever(ragEndpointUrl);
    }

    public bool IsAvailable => _modelPath is not null && File.Exists(_modelPath);

    public async Task<string> AskAsync(string question, LlmContext? context, CancellationToken ct = default)
    {
        if (!IsAvailable)
            return "AI Assistant is currently unavailable. Model file not found.";

        // Initialize model under lock (one-time)
        lock (_lock)
        {
            if (_disposed) return "AI Assistant is currently unavailable.";

            if (!_initialized)
            {
                _initialized = true;
                try
                {
                    InitializeModel();
                }
                catch (Exception ex)
                {
                    _loadFailed = true;
                    return $"Failed to load AI model: {ex.Message}";
                }
            }

            if (_loadFailed || _model is null || _context is null)
                return "AI model could not be loaded. Please check that the model file is valid.";
        }

        try
        {
            return await RunInferenceAsync(question, context, ct);
        }
        catch (Exception ex)
        {
            return $"Inference error: {ex.Message}";
        }
    }

    private void InitializeModel()
    {
        if (_modelPath is null) { _loadFailed = true; return; }

        var modelParams = new ModelParams(_modelPath)
        {
            ContextSize = ContextSize,
            GpuLayerCount = 20,
        };

        _model = LLamaWeights.LoadFromFile(modelParams);
        _context = _model.CreateContext(modelParams);
    }

    private async Task<string> RunInferenceAsync(string question, LlmContext? context, CancellationToken ct)
    {
        if (_model is null || _context is null)
            return "Model not loaded.";

        var systemPrompt = context is not null
            ? BuildSystemPrompt(context.CpuPercent, context.RamPercent, context.DiskPercent,
                context.ProfileType, context.Language)
            : "You are AuraCore AI Assistant. You help users optimize their computer using AuraCore Pro modules.";

        // RAG: retrieve relevant source code context (gracefully degrades if unavailable)
        if (_ragRetriever is not null)
        {
            try
            {
                var chunks = await _ragRetriever.RetrieveContextAsync(question, topK: 3, ct);
                if (chunks.Count > 0)
                {
                    var ragContext = string.Join("\n---\n", chunks);
                    systemPrompt += "\n\nRelevant source code context:\n" + ragContext;
                }
            }
            catch { /* RAG unavailable — proceed without context */ }
        }

        var prompt = $"<|system|>\n{systemPrompt}<|end|>\n<|user|>\n{question}<|end|>\n<|assistant|>\n";

        var executor = new StatelessExecutor(_model, _context.Params);
        var inferenceParams = new InferenceParams
        {
            MaxTokens = MaxTokens,
            AntiPrompts = new[] { "<|end|>", "<|user|>" },
            SamplingPipeline = new DefaultSamplingPipeline { Temperature = InferenceTemperature },
        };

        var result = new System.Text.StringBuilder();
        await foreach (var token in executor.InferAsync(prompt, inferenceParams, ct))
        {
            result.Append(token);
            if (ct.IsCancellationRequested) break;
        }

        return result.ToString().Trim();
    }

    /// <summary>
    /// Builds a context-aware system prompt incorporating current system metrics.
    /// </summary>
    public static string BuildSystemPrompt(
        double cpu, double ram, double disk,
        string? profileType, string language)
    {
        if (language == "tr")
        {
            var prompt = "Sen AuraCore AI Asistan'issin. AuraCore Pro toplam 45+ module sahiptir:\n\n";
            prompt += "Windows (22): Cop Temizleyici, RAM Optimizer, Depolama Sikistrima, Registry Optimizer, Bloatware Kaldirma, Gizlilik Temizleyici, Ag Optimizer, Oyun Modu, Uygulama Yukleyici, Dosya Imhaci, Guvenlik Duvari Kurallari, Defender Yoneticisi, Surucu Guncelleyici, Pil Optimizer, Otomatik Baslatma Yoneticisi, Baglam Menusu, Gorev Cubugu Ayarlari, Gezgin Ayarlari, Disk Temizleme, Ortam Degiskenleri, Font Yoneticisi, Wake-on-LAN.\n";
            prompt += "Linux (9): Systemd Yoneticisi, Paket Temizleyici, Swap Optimizer, Cron Yoneticisi, Journal Temizleyici, Kernel Temizleyici, Sembolik Link Yoneticisi, Linux Uygulama Yukleyici (141 uygulama), Docker Temizleyici.\n";
            prompt += "macOS (10): Defaults Optimizer, Launch Agent Yoneticisi, Brew Yoneticisi, Time Machine Yoneticisi, Xcode Temizleyici, DNS Temizleyici, Temizlenebilir Alan Yoneticisi, Spotlight Yoneticisi, Mac Uygulama Yukleyici (141 uygulama), Docker Temizleyici.\n";
            prompt += "AI Ozellikleri: AI Insights (saglik skoru, anomali tespiti, disk tahmini, bellek sizintisi tespiti), AI Chat (bu konusma).\n\n";

            prompt += "Mevcut Sistem Durumu:\n";
            prompt += $"- CPU Kullanimi: %{cpu:F1}\n";
            prompt += $"- RAM Kullanimi: %{ram:F1}\n";
            prompt += $"- Disk Kullanimi: %{disk:F1}\n";

            if (profileType is not null)
                prompt += $"- Kullanici Profili: {profileType}\n";

            prompt += "\nKullanicinin sorularina Turkce olarak kisa ve net cevaplar ver. ";
            prompt += "Sistem optimizasyonu, performans iyilestirme ve sorun giderme konularinda uzmansin.";
            return prompt;
        }
        else
        {
            var prompt = "You are AuraCore AI Assistant. AuraCore Pro has 45+ modules total:\n\n";
            prompt += "Windows (22): Junk Cleaner, RAM Optimizer, Storage Compression, Registry Optimizer, Bloatware Removal, Privacy Cleaner, Network Optimizer, Gaming Mode, App Installer, File Shredder, Firewall Rules, Defender Manager, Driver Updater, Battery Optimizer, Autorun Manager, Context Menu, Taskbar Tweaks, Explorer Tweaks, Disk Cleanup, Environment Variables, Font Manager, Wake-on-LAN.\n";
            prompt += "Linux (9): Systemd Manager, Package Cleaner, Swap Optimizer, Cron Manager, Journal Cleaner, Kernel Cleaner, Symlink Manager, Linux App Installer (141 apps), Docker Cleaner.\n";
            prompt += "macOS (10): Defaults Optimizer, Launch Agent Manager, Brew Manager, Time Machine Manager, Xcode Cleaner, DNS Flusher, Purgeable Space Manager, Spotlight Manager, Mac App Installer (141 apps), Docker Cleaner.\n";
            prompt += "AI Features: AI Insights (health score, anomaly detection, disk prediction, memory leak detection), AI Chat (this conversation).\n\n";

            prompt += "Current System Status:\n";
            prompt += $"- CPU Usage: {cpu:F1}%\n";
            prompt += $"- RAM Usage: {ram:F1}%\n";
            prompt += $"- Disk Usage: {disk:F1}%\n";

            if (profileType is not null)
                prompt += $"- User Profile: {profileType}\n";

            prompt += "\nProvide concise, actionable answers about system optimization, performance tuning, and troubleshooting.";
            return prompt;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _context?.Dispose();
        _model?.Dispose();
        _ragRetriever?.Dispose();

        _model = null;
        _context = null;
    }
}
