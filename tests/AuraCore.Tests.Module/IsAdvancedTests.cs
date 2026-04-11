using Xunit;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.JunkCleaner;

namespace AuraCore.Tests.Module;

public class IsAdvancedTests
{
    [Fact]
    public void DefaultModule_IsNotAdvanced()
    {
        IOptimizationModule m = new JunkCleanerModule();
        Assert.False(m.IsAdvanced);
    }
}
