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
            var prompt = "Sen AuraCore AI Asistan'issin. AuraCore Pro asagidaki ozelliklere sahiptir:\n\n";
            prompt += "Optimizasyon Modulleri (27 Windows, 17 Linux, 16 macOS):\n";
            prompt += "Windows: AI Cop Temizleyici, RAM Optimizer, Dosya Imhaci, Depolama Sikistrima, Registry Optimizer, Bloatware Kaldirma, Disk Temizleme Pro, Gizlilik Temizleyici, Ag Optimizer, Oyun Modu, Uygulama Yukleyici, Baglam Menusu Ozellestirici, Gorev Cubugu Ayarlari, Gezgin Ayarlari, Ortam Degiskenleri, Font Yoneticisi, Hosts Dosya Duzenleyici, Islem Izleyici, Otomatik Baslatma Yoneticisi, Guvenlik Duvari Kurallari, Wake-on-LAN, Defender Yoneticisi, Surucu Guncelleyici, Pil Optimizer, Sistem Saglik Analizcisi, Ag Izleyici, DNS Benchmark.\n";
            prompt += "Linux: Systemd Yoneticisi, Paket Temizleyici, Swap Optimizer, Cron Yoneticisi, Journal Temizleyici, Kernel Temizleyici, Sembolik Link Yoneticisi, Linux Uygulama Yukleyici, Docker Temizleyici, Snap & Flatpak Temizleyici, GRUB Yoneticisi, Cop Temizleyici, RAM Optimizer, Dosya Imhaci, Hosts Dosya Duzenleyici, Islem Izleyici, Sistem Saglik Analizcisi.\n";
            prompt += "macOS: Defaults Optimizer, Launch Agent Yoneticisi, Brew Yoneticisi, Time Machine Yoneticisi, Xcode Temizleyici, DNS Temizleyici, Temizlenebilir Alan Yoneticisi, Spotlight Yoneticisi, Mac Uygulama Yukleyici, Docker Temizleyici, Cop Temizleyici, RAM Optimizer, Dosya Imhaci, Hosts Dosya Duzenleyici, Islem Izleyici, Sistem Saglik Analizcisi.\n";
            prompt += "Araclar: Disk Sagligi (SMART izleme), Alan Analizcisi (disk kullanim goruntuleme), Baslangic Optimizer (baslangic programlari yonetimi), Servis Yoneticisi (Windows servisleri), Otomatik Zamanlama (modul calistirma zamanlama), ISO Builder (12 adimli Windows ISO ozellestirme sihirbazi).\n";
            prompt += "AI Ozellikleri: AI Onerileri (akilli sistem analizi), AI Insights (saglik skoru, anomali tespiti, disk tahmini, bellek sizintisi tespiti), AI Chat (bu konusma).\n\n";

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
            var prompt = "You are AuraCore AI Assistant. AuraCore Pro has the following features:\n\n";
            prompt += "Optimization Modules (27 Windows, 17 Linux, 16 macOS):\n";
            prompt += "Windows: AI Junk Cleaner, RAM Optimizer, File Shredder, Storage Compression, Registry Optimizer, Bloatware Removal, Disk Cleanup Pro, Privacy Cleaner, Network Optimizer, Gaming Mode, App Installer, Context Menu Customizer, Taskbar Tweaks, Explorer Tweaks, Environment Variables, Font Manager, Hosts File Editor, Process Monitor, Autorun Manager, Firewall Rules, Wake-on-LAN, Defender Manager, Driver Updater, Battery Optimizer, System Health Analyzer, Network Monitor, DNS Benchmark.\n";
            prompt += "Linux: Systemd Manager, Package Cleaner, Swap Optimizer, Cron Manager, Journal Cleaner, Kernel Cleaner, Symlink Manager, Linux App Installer, Docker Cleaner, Snap & Flatpak Cleaner, GRUB Manager, Junk Cleaner, RAM Optimizer, File Shredder, Hosts File Editor, Process Monitor, System Health Analyzer.\n";
            prompt += "macOS: Defaults Optimizer, Launch Agent Manager, Brew Manager, Time Machine Manager, Xcode Cleaner, DNS Flusher, Purgeable Space Manager, Spotlight Manager, Mac App Installer, Docker Cleaner, Junk Cleaner, RAM Optimizer, File Shredder, Hosts File Editor, Process Monitor, System Health Analyzer.\n";
            prompt += "Tools: Disk Health (SMART monitoring), Space Analyzer (disk usage visualization), Startup Optimizer (startup program management), Service Manager (Windows services), Auto-Schedule (automated module execution scheduling), ISO Builder (12-step Windows ISO customization wizard).\n";
            prompt += "AI Features: AI Recommendations (smart system analysis), AI Insights (health score, anomaly detection, disk prediction, memory leak detection), AI Chat (this conversation).\n\n";

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
