using AuraCore.UI.Avalonia.ViewModels;
using System.Linq;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.ViewModels;

public class SidebarViewModelPhase3Tests
{
    [Fact]
    public void AIFeaturesCategory_HasSingleModule()
    {
        var vm = new SidebarViewModel();

        var aiCategory = vm.Categories.FirstOrDefault(c => c.Id == "ai-features");
        Assert.NotNull(aiCategory);
        Assert.Single(aiCategory!.Modules);
        Assert.Equal("ai-features", aiCategory.Modules[0].Id);
    }

    [Fact]
    public void AIFeaturesCategory_HasCORTEXBadge()
    {
        var vm = new SidebarViewModel();
        var aiCategory = vm.Categories.First(c => c.Id == "ai-features");
        Assert.Equal("CORTEX", aiCategory.Badge);
    }

    [Fact]
    public void AIFeaturesCategory_IconIsSparklesFilled()
    {
        var vm = new SidebarViewModel();
        var aiCategory = vm.Categories.First(c => c.Id == "ai-features");
        Assert.Equal("IconSparklesFilled", aiCategory.Icon);
    }

    [Fact]
    public void AIFeaturesCategory_IsAccent()
    {
        var vm = new SidebarViewModel();
        var aiCategory = vm.Categories.First(c => c.Id == "ai-features");
        Assert.True(aiCategory.IsAccent);
    }

    [Fact]
    public void OldAISubModules_NoLongerPresent()
    {
        var vm = new SidebarViewModel();
        var allModuleIds = vm.Categories.SelectMany(c => c.Modules).Select(m => m.Id).ToList();

        Assert.DoesNotContain("ai-insights", allModuleIds);
        Assert.DoesNotContain("ai-recommendations", allModuleIds);
        Assert.DoesNotContain("auto-schedule", allModuleIds);
        Assert.DoesNotContain("ai-chat", allModuleIds);
    }
}
