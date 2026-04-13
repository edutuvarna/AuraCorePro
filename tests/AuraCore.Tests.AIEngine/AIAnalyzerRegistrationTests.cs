using Xunit;

namespace AuraCore.Tests.AIEngine;

public class AIAnalyzerRegistrationTests
{
    [Theory]
    [InlineData("tinyllama", "auracore-tinyllama.gguf")]
    [InlineData("phi2", "auracore-phi2.gguf")]
    [InlineData("phi3-mini", "auracore-phi3-mini.gguf")]
    [InlineData("mistral-7b", "auracore-mistral-7b.gguf")]
    [InlineData("llama31-8b", "auracore-llama31-8b.gguf")]
    [InlineData("phi3-medium", "auracore-phi3-medium.gguf")]
    [InlineData("qwen25-32b", "auracore-qwen25-32b.gguf")]
    [InlineData("phi3", "auracore-phi3-mini.gguf")]
    [InlineData("llama3-8b", "auracore-llama31-8b.gguf")]
    [InlineData("unknown-model", "auracore-phi3-mini.gguf")]
    public void ResolveModelFileName_ReturnsCorrectFile(string modelName, string expectedFile)
    {
        var result = AuraCore.Engine.AIAnalyzer.AIAnalyzerRegistration.ResolveModelFileName(modelName);
        Assert.Equal(expectedFile, result);
    }

    [Fact]
    public void ResolveModelFileName_Llama33_NotSupported()
    {
        var result = AuraCore.Engine.AIAnalyzer.AIAnalyzerRegistration.ResolveModelFileName("llama33-70b");
        Assert.Equal("auracore-phi3-mini.gguf", result);
    }

    [Fact]
    public void ResolveModelFileName_Llama2Legacy_NotSupported()
    {
        var result = AuraCore.Engine.AIAnalyzer.AIAnalyzerRegistration.ResolveModelFileName("llama2-13b");
        Assert.Equal("auracore-phi3-mini.gguf", result);
    }
}
