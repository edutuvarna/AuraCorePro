using Avalonia.Headless.XUnit;
using AuraCore.UI.Avalonia.Views.Controls;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Controls;

public class StatusChipTests
{
    [AvaloniaFact]
    public void Chip_Defaults()
    {
        var chip = new StatusChip();
        Assert.Equal(string.Empty, chip.Label);
        Assert.True(chip.ShowDot);
    }

    [AvaloniaFact]
    public void Chip_AcceptsLabel()
    {
        var chip = new StatusChip { Label = "LIVE" };
        Assert.Equal("LIVE", chip.Label);
    }

    [AvaloniaFact]
    public void Chip_RendersInWindow()
    {
        var chip = new StatusChip { Label = "LIVE" };
        using var window = AvaloniaTestBase.RenderInWindow(chip, 80, 24);
        Assert.True(chip.IsMeasureValid);
    }
}
