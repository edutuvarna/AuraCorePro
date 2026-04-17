using AuraCore.UI.Avalonia.Views.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Dashboard;

/// <summary>
/// Phase 5.5.2.2: DiskHealthSummaryCard render tests.
/// </summary>
public class DiskHealthSummaryCardTests
{
    [AvaloniaFact]
    public void Card_instantiates_without_throwing()
    {
        var card = new DiskHealthSummaryCard();
        Assert.NotNull(card);
    }

    [AvaloniaFact]
    public void Card_Initialize_with_null_nav_does_not_throw()
    {
        var card = new DiskHealthSummaryCard();
        // null nav is the fallback when DI is unavailable — must not throw
        var ex = Record.Exception(() => card.Initialize(null, "No disks found", "Unknown", "—"));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void Card_Initialize_sets_StatusText()
    {
        var card = new DiskHealthSummaryCard();
        card.Initialize(null, "All disks OK", "Passed", "38 °C");
        Assert.Equal("All disks OK", card.StatusText);
    }

    [AvaloniaFact]
    public void Card_Initialize_sets_SmartText()
    {
        var card = new DiskHealthSummaryCard();
        card.Initialize(null, "All disks OK", "Passed", "38 °C");
        Assert.Equal("Passed", card.SmartText);
    }

    [AvaloniaFact]
    public void Card_Initialize_sets_WorstTempText()
    {
        var card = new DiskHealthSummaryCard();
        card.Initialize(null, "All disks OK", "Passed", "38 °C");
        Assert.Equal("38 °C", card.WorstTempText);
    }
}
