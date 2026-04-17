using AuraCore.Application.Interfaces.Platform;
using AuraCore.UI.Avalonia.Views;
using System.Reflection;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Responsive;

public class MainWindowWidthSubscriptionTests
{
    [Fact]
    public void MainWindow_has_narrow_mode_service_field()
    {
        var fields = typeof(MainWindow)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.True(
            fields.Any(f => f.FieldType == typeof(INarrowModeService)),
            "MainWindow should hold a field typed as INarrowModeService for pushing width updates");
    }

    [Fact]
    public void MainWindow_has_OnWindowBoundsChanged_handler()
    {
        var method = typeof(MainWindow)
            .GetMethod("OnWindowBoundsChanged",
                BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
    }
}
