using Xunit;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.WakeOnLan;
using AuraCore.Module.FontManager;

namespace AuraCore.Tests.Module;

public class AdvancedModulesTests
{
    [Fact]
    public void WakeOnLan_IsAdvanced()
    {
        IOptimizationModule m = new WakeOnLanModule();
        Assert.True(m.IsAdvanced);
    }

    [Fact]
    public void FontManager_IsAdvanced()
    {
        IOptimizationModule m = new FontManagerModule();
        Assert.True(m.IsAdvanced);
    }
}
