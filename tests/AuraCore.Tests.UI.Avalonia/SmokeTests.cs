using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia;

public class SmokeTests
{
    [AvaloniaFact]
    public void HeadlessPlatform_CanCreateControl()
    {
        var border = new Border();
        Assert.NotNull(border);
    }
}
