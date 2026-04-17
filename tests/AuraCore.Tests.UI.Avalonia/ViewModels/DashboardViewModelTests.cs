using AuraCore.UI.Avalonia.ViewModels;
using AuraCore.UI.Avalonia.Views.Controls;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.ViewModels;

public class DashboardViewModelTests
{
    [Fact]
    public void Defaults_AllZeroExceptHealth()
    {
        var vm = new DashboardViewModel();
        Assert.Equal(0.0, vm.CpuPercent);
        Assert.Equal(0.0, vm.RamPercent);
        Assert.Equal(0.0, vm.DiskPercent);
        Assert.Equal(0.0, vm.GpuPercent);
        Assert.Equal(100.0, vm.HealthScore);
    }

    [Fact]
    public void GpuVisible_IsFalse_WhenGpuInfoIsNull()
    {
        var vm = new DashboardViewModel();
        Assert.Null(vm.GpuInfo);
        Assert.False(vm.GpuVisible);
    }

    [Fact]
    public void GpuVisible_IsTrue_WhenGpuInfoIsSet()
    {
        var vm = new DashboardViewModel();
        vm.SetGpuInfo(new AuraCore.UI.Avalonia.Helpers.GpuInfo("Radeon 780M", 28.0, 68.0));
        Assert.True(vm.GpuVisible);
        Assert.Equal("Radeon 780M", vm.GpuName);
    }

    [Fact]
    public void Insights_Defaults_ShowsLearningFallback()
    {
        var vm = new DashboardViewModel();
        Assert.NotEmpty(vm.Insights);
        Assert.Contains(vm.Insights, r => r.Title.Contains("Learning"));
    }

    [Fact]
    public void UpdateInsights_ReplacesLearningWithReal()
    {
        var vm = new DashboardViewModel();
        vm.UpdateInsights(new[]
        {
            new InsightRow { Title = "CPU spike", Description = "Brave 42%" }
        });
        Assert.Single(vm.Insights);
        Assert.Equal("CPU spike", vm.Insights[0].Title);
    }

    [Fact]
    public void SystemSummary_FormatsPlatform()
    {
        var vm = new DashboardViewModel { OsName = "Windows 11", CpuName = "Ryzen 7", RamTotalGb = 31.3 };
        Assert.Contains("Windows 11", vm.SystemSummary);
        Assert.Contains("Ryzen 7", vm.SystemSummary);
    }
}
