using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.Application;
using AuraCore.Module.SystemHealth;
using Xunit;

namespace AuraCore.Tests.Module;

public class SystemHealthModuleVirtualFsTests
{
    [Fact]
    public async Task ScanAsync_ProducesNoDrive_WithUnderflowedPercent()
    {
        var module = new SystemHealthModule();
        var result = await module.ScanAsync(new ScanOptions(), CancellationToken.None);
        Assert.True(result.Success);

        var report = module.LastReport;
        Assert.NotNull(report);

        foreach (var drive in report!.Drives)
        {
            Assert.InRange(drive.UsedPercent, 0, 100);
            if (OperatingSystem.IsLinux())
            {
                Assert.False(
                    drive.Name.StartsWith("/sys", StringComparison.Ordinal)
                    || drive.Name.StartsWith("/proc", StringComparison.Ordinal)
                    || drive.Name == "/dev/pts"
                    || drive.Name.StartsWith("/sys/kernel", StringComparison.Ordinal),
                    $"Virtual filesystem leaked: {drive.Name} fmt={drive.Format}");
            }
        }
    }

    [Fact]
    public void IsVirtualFilesystem_HelperExists_AndIsStatic()
    {
        var method = typeof(SystemHealthModule).GetMethod(
            "IsVirtualFilesystem",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        Assert.True(method!.IsStatic);
    }
}
