using AuraCore.Engine.AIAnalyzer.LLM;
using Xunit;

namespace AuraCore.Tests.AIEngine;

public class LlmInferenceEngineTests
{
    [Fact]
    public void IsAvailable_NoModel_ReturnsFalse()
    {
        using var engine = new LlmInferenceEngine(null);
        Assert.False(engine.IsAvailable);
    }

    [Fact]
    public async Task AskAsync_NoModel_ReturnsUnavailableMessage()
    {
        using var engine = new LlmInferenceEngine(null);
        var response = await engine.AskAsync("Hello", null);
        Assert.Contains("unavailable", response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSystemPrompt_WithProfile_IncludesContext()
    {
        var prompt = LlmInferenceEngine.BuildSystemPrompt(
            cpu: 65.5,
            ram: 72.3,
            disk: 45.0,
            profileType: "Developer",
            language: "en");

        Assert.Contains("AuraCore AI Assistant", prompt);
        Assert.Contains("65.5%", prompt);
        Assert.Contains("72.3%", prompt);
        Assert.Contains("45.0%", prompt);
        Assert.Contains("Developer", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_Turkish_ReturnsTurkish()
    {
        var prompt = LlmInferenceEngine.BuildSystemPrompt(
            cpu: 50.0,
            ram: 60.0,
            disk: 30.0,
            profileType: null,
            language: "tr");

        Assert.Contains("AuraCore AI Asistan", prompt);
        Assert.Contains("CPU Kullanimi", prompt);
        Assert.Contains("Turkce", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_English_ContainsAllFeatureCategories()
    {
        var prompt = LlmInferenceEngine.BuildSystemPrompt(50, 60, 30, null, "en");

        // Module counts
        Assert.Contains("27 Windows", prompt);
        Assert.Contains("17 Linux", prompt);
        Assert.Contains("16 macOS", prompt);

        // Corrected feature names matching app navigation
        Assert.Contains("Disk Cleanup Pro", prompt);
        Assert.Contains("Context Menu Customizer", prompt);
        Assert.Contains("Hosts File Editor", prompt);
        Assert.Contains("System Health Analyzer", prompt);

        // Tools section
        Assert.Contains("Disk Health", prompt);
        Assert.Contains("Space Analyzer", prompt);
        Assert.Contains("Startup Optimizer", prompt);
        Assert.Contains("Service Manager", prompt);
        Assert.Contains("Auto-Schedule", prompt);
        Assert.Contains("ISO Builder", prompt);

        // AI section
        Assert.Contains("AI Recommendations", prompt);
        Assert.Contains("AI Insights", prompt);
        Assert.Contains("AI Chat", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_Turkish_ContainsAllFeatureCategories()
    {
        var prompt = LlmInferenceEngine.BuildSystemPrompt(50, 60, 30, null, "tr");

        // Module counts
        Assert.Contains("27 Windows", prompt);
        Assert.Contains("17 Linux", prompt);
        Assert.Contains("16 macOS", prompt);

        // Corrected names
        Assert.Contains("Disk Temizleme Pro", prompt);
        Assert.Contains("Baglam Menusu Ozellestirici", prompt);
        Assert.Contains("Hosts Dosya Duzenleyici", prompt);
        Assert.Contains("Sistem Saglik Analizcisi", prompt);

        // Tools
        Assert.Contains("Disk Sagligi", prompt);
        Assert.Contains("Alan Analizcisi", prompt);
        Assert.Contains("Baslangic Optimizer", prompt);
        Assert.Contains("Servis Yoneticisi", prompt);
        Assert.Contains("Otomatik Zamanlama", prompt);
        Assert.Contains("ISO Builder", prompt);

        // AI
        Assert.Contains("AI Onerileri", prompt);
        Assert.Contains("AI Insights", prompt);
        Assert.Contains("AI Chat", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_English_ContainsModules()
    {
        var prompt = LlmInferenceEngine.BuildSystemPrompt(50, 60, 30, null, "en");

        Assert.Contains("Network Monitor", prompt);
        Assert.Contains("DNS Benchmark", prompt);
        Assert.Contains("Snap & Flatpak Cleaner", prompt);
        Assert.Contains("GRUB Manager", prompt);
        Assert.Contains("Docker Cleaner", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_Turkish_ContainsModules()
    {
        var prompt = LlmInferenceEngine.BuildSystemPrompt(50, 60, 30, null, "tr");

        Assert.Contains("Ag Izleyici", prompt);
        Assert.Contains("DNS Benchmark", prompt);
        Assert.Contains("Snap & Flatpak Temizleyici", prompt);
        Assert.Contains("GRUB Yoneticisi", prompt);
        Assert.Contains("Docker Temizleyici", prompt);
    }
}
