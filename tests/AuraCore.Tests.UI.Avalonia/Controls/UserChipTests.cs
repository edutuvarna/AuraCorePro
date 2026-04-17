using Avalonia.Headless.XUnit;
using AuraCore.UI.Avalonia.Views.Controls;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Controls;

public class UserChipTests
{
    [AvaloniaFact]
    public void Chip_Defaults()
    {
        var c = new UserChip();
        Assert.Equal(string.Empty, c.Email);
        Assert.Equal(string.Empty, c.Role);
    }

    [AvaloniaFact]
    public void Chip_Accepts_EmailAndRole()
    {
        var c = new UserChip { Email = "admin@aura.pro", Role = "ADMIN" };
        Assert.Equal("admin@aura.pro", c.Email);
        Assert.Equal("ADMIN", c.Role);
    }

    [AvaloniaFact]
    public void Chip_RendersInWindow()
    {
        var c = new UserChip { Email = "admin@aura.pro", Role = "ADMIN" };
        using var window = AvaloniaTestBase.RenderInWindow(c, 220, 40);
        Assert.True(c.IsMeasureValid);
    }
}
