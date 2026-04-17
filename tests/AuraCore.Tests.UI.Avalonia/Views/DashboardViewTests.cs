using global::Avalonia.Controls;
using global::Avalonia.Headless.XUnit;
using AuraCore.UI.Avalonia.Views.Pages;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

public class DashboardViewTests
{
    [AvaloniaFact]
    public void DashboardView_Instantiates()
    {
        var v = new DashboardView();
        Assert.NotNull(v);
    }

    [AvaloniaFact]
    public void DashboardView_RendersInWindow()
    {
        var v = new DashboardView();
        using var win = AvaloniaTestBase.RenderInWindow(v, 1000, 640);
        Assert.True(v.IsMeasureValid);
    }
}
