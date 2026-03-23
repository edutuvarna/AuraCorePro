using Xunit;
using AuraCore.Application;
using AuraCore.Module.JunkCleaner;
namespace AuraCore.Tests.Module;

public class ModuleContractTests
{
    [Fact]
    public async Task JunkCleaner_Scan()
    {
        var m = new JunkCleanerModule();
        var r = await m.ScanAsync(new ScanOptions());
        Assert.True(r.Success);
        Assert.Equal("junk-cleaner", r.ModuleId);
    }
}
