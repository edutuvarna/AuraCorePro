using AuraCore.UI.Avalonia.Services.AI;
using System.Linq;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Services.AI;

public class ModelCatalogTests
{
    [Fact]
    public void All_ReturnsExactly8Models()
    {
        var catalog = new ModelCatalog();
        Assert.Equal(8, catalog.All.Count);
    }

    [Fact]
    public void All_ContainsExpectedIds()
    {
        var catalog = new ModelCatalog();
        var ids = catalog.All.Select(m => m.Id).ToHashSet();
        Assert.Contains("tinyllama", ids);
        Assert.Contains("phi3-mini-q4km", ids);
        Assert.Contains("phi2", ids);
        Assert.Contains("phi3-mini", ids);
        Assert.Contains("mistral-7b", ids);
        Assert.Contains("llama31-8b", ids);
        Assert.Contains("phi3-medium", ids);
        Assert.Contains("qwen25-32b", ids);
    }

    [Fact]
    public void Phi3MiniQ4KM_IsRecommended()
    {
        var catalog = new ModelCatalog();
        var recommended = catalog.All.Where(m => m.IsRecommended).ToList();
        Assert.Single(recommended);
        Assert.Equal("phi3-mini-q4km", recommended[0].Id);
    }

    [Fact]
    public void HeavyTier_RequiresAtLeast32GbRam()
    {
        var catalog = new ModelCatalog();
        var heavy = catalog.All.Where(m => m.Tier == ModelTier.Heavy);
        Assert.All(heavy, m => Assert.True(m.EstimatedRamBytes >= 32L * 1024 * 1024 * 1024));
    }

    [Fact]
    public void FindById_ExistingId_ReturnsDescriptor()
    {
        var catalog = new ModelCatalog();
        var model = catalog.FindById("phi3-mini-q4km");
        Assert.NotNull(model);
        Assert.Equal("Phi-3 Mini Q4KM", model!.DisplayName);
    }

    [Fact]
    public void FindById_UnknownId_ReturnsNull()
    {
        var catalog = new ModelCatalog();
        var model = catalog.FindById("does-not-exist");
        Assert.Null(model);
    }

    [Fact]
    public void FindByFilename_ExistingFile_ReturnsDescriptor()
    {
        var catalog = new ModelCatalog();
        var model = catalog.FindByFilename("auracore-tinyllama.gguf");
        Assert.NotNull(model);
        Assert.Equal("tinyllama", model!.Id);
    }

    [Fact]
    public void FindByFilename_UnknownFile_ReturnsNull()
    {
        var catalog = new ModelCatalog();
        var model = catalog.FindByFilename("random-model.gguf");
        Assert.Null(model);
    }

    [Fact]
    public void AllFilenames_StartWithAuracorePrefix()
    {
        var catalog = new ModelCatalog();
        Assert.All(catalog.All, m => Assert.StartsWith("auracore-", m.Filename));
    }

    [Fact]
    public void AllFilenames_EndWithGguf()
    {
        var catalog = new ModelCatalog();
        Assert.All(catalog.All, m => Assert.EndsWith(".gguf", m.Filename));
    }
}
