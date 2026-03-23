using Xunit;
using AuraCore.Platform.Configuration;
namespace AuraCore.Tests.Platform;

public class ConfigTests
{
    [Fact]
    public void Get_Default()
    {
        using var svc = new ConfigurationService();
        var s = svc.Get<TestSettings>("test");
        Assert.Equal("default", s.Name);
    }
    private class TestSettings { public string Name { get; set; } = "default"; }
}
