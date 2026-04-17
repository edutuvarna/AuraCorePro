using AuraCore.UI.Avalonia.Helpers;
using AuraCore.UI.Avalonia.Views.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Dashboard;

/// <summary>
/// Phase 5.5.2.2 / 5.5.2.2.1: DiskHealthSummaryCard render and real-data-path tests.
/// </summary>
public class DiskHealthSummaryCardTests
{
    // -------------------------------------------------------------------------
    // Existing placeholder / instantiation tests (kept green)
    // -------------------------------------------------------------------------

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

    // -------------------------------------------------------------------------
    // Phase 5.5.2.2.1: real-data-path tests (ApplyScanResult)
    // -------------------------------------------------------------------------

    [AvaloniaFact]
    public void ApplyScanResult_updates_StatusText()
    {
        var card   = new DiskHealthSummaryCard();
        var result = new DiskHealthScanResult("All drives healthy", "OK", "—");
        card.ApplyScanResult(result);
        Assert.Equal("All drives healthy", card.StatusText);
    }

    [AvaloniaFact]
    public void ApplyScanResult_updates_SmartText()
    {
        var card   = new DiskHealthSummaryCard();
        var result = new DiskHealthScanResult("All drives healthy", "OK", "—");
        card.ApplyScanResult(result);
        Assert.Equal("OK", card.SmartText);
    }

    [AvaloniaFact]
    public void ApplyScanResult_updates_WorstTempText()
    {
        var card   = new DiskHealthSummaryCard();
        var result = new DiskHealthScanResult("All drives healthy", "OK", "45°C");
        card.ApplyScanResult(result);
        Assert.Equal("45°C", card.WorstTempText);
    }

    [AvaloniaFact]
    public void ApplyScanResult_overrides_Initialize_placeholders()
    {
        var card = new DiskHealthSummaryCard();
        // Card starts with placeholders from Initialize …
        card.Initialize(null, "—", "—", "—");
        // … then real data arrives from the background scan
        var result = new DiskHealthScanResult("1 drive nearly full", "Warning", "—");
        card.ApplyScanResult(result);
        Assert.Equal("1 drive nearly full", card.StatusText);
        Assert.Equal("Warning", card.SmartText);
    }

    [AvaloniaFact]
    public void ApplyScanResult_with_Placeholder_result_does_not_throw()
    {
        // Verifies graceful degradation: if the scanner returns its own
        // Placeholder sentinel (scan error path), the card must not crash
        // and must retain placeholder strings.
        var card = new DiskHealthSummaryCard();
        card.Initialize(null, "—", "—", "—");

        var ex = Record.Exception(() => card.ApplyScanResult(DiskHealthScanner.Placeholder));
        Assert.Null(ex);
        Assert.Equal("—", card.StatusText);
        Assert.Equal("—", card.SmartText);
        Assert.Equal("—", card.WorstTempText);
    }

    // -------------------------------------------------------------------------
    // DiskHealthScanResult unit tests (no Avalonia needed, but AvaloniaFact is fine)
    // -------------------------------------------------------------------------

    [AvaloniaFact]
    public void DiskHealthScanResult_Placeholder_has_dash_values()
    {
        Assert.Equal("—", DiskHealthScanner.Placeholder.StatusText);
        Assert.Equal("—", DiskHealthScanner.Placeholder.SmartText);
        Assert.Equal("—", DiskHealthScanner.Placeholder.WorstTempText);
    }
}
