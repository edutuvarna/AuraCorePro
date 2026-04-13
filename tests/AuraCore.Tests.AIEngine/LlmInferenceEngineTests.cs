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
    public void BuildSystemPrompt_English_Has47Modules()
    {
        var prompt = LlmInferenceEngine.BuildSystemPrompt(50, 60, 30, null, "en");

        Assert.Contains("47 modules", prompt);
        Assert.Contains("Windows (27)", prompt);
        Assert.Contains("Linux (17)", prompt);
        Assert.Contains("macOS (16)", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_Turkish_Has47Modules()
    {
        var prompt = LlmInferenceEngine.BuildSystemPrompt(50, 60, 30, null, "tr");

        Assert.Contains("47 module", prompt);
        Assert.Contains("Windows (27)", prompt);
        Assert.Contains("Linux (17)", prompt);
        Assert.Contains("macOS (16)", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_English_ContainsNewModules()
    {
        var prompt = LlmInferenceEngine.BuildSystemPrompt(50, 60, 30, null, "en");

        // New Windows modules
        Assert.Contains("Network Monitor", prompt);
        Assert.Contains("DNS Benchmark", prompt);
        Assert.Contains("Hosts Editor", prompt);
        Assert.Contains("Process Monitor", prompt);
        Assert.Contains("System Health", prompt);

        // New Linux modules
        Assert.Contains("Snap & Flatpak Cleaner", prompt);
        Assert.Contains("GRUB Manager", prompt);

        // Cross-platform modules should appear in Linux/macOS sections
        Assert.Contains("Docker Cleaner", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_Turkish_ContainsNewModules()
    {
        var prompt = LlmInferenceEngine.BuildSystemPrompt(50, 60, 30, null, "tr");

        Assert.Contains("Ag Izleyici", prompt);
        Assert.Contains("DNS Benchmark", prompt);
        Assert.Contains("Hosts Duzenleyici", prompt);
        Assert.Contains("Islem Izleyici", prompt);
        Assert.Contains("Sistem Sagligi", prompt);
        Assert.Contains("Snap & Flatpak Temizleyici", prompt);
        Assert.Contains("GRUB Yoneticisi", prompt);
        Assert.Contains("Docker Temizleyici", prompt);
    }
}
