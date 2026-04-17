using Avalonia.Controls;
using Avalonia.Interactivity;
using AuraCore.Application.Interfaces.Platform;

namespace AuraCore.UI.Avalonia.Views.Controls;

/// <summary>
/// Compact Disk Health summary card shown on the Dashboard (Phase 5.5.2.2).
/// Replaces the sidebar entry — the full DiskHealthView is still reachable
/// via the "View details" link which navigates to the "disk-health" module.
/// </summary>
public partial class DiskHealthSummaryCard : UserControl
{
    private INavigationService? _nav;

    // Public read-back for tests — mirrors the named AXAML controls.
    public string StatusText    { get; private set; } = "—";
    public string SmartText     { get; private set; } = "—";
    public string WorstTempText { get; private set; } = "—";

    public DiskHealthSummaryCard()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Wires navigation and populates the summary fields.
    /// Pass placeholder strings ("—") when live data is unavailable.
    /// </summary>
    public void Initialize(INavigationService? nav, string status, string smartBadge, string worstTemp)
    {
        _nav = nav;
        StatusText    = status;
        SmartText     = smartBadge;
        WorstTempText = worstTemp;
        StatusLine.Text = status;
        SmartBadge.Text = smartBadge;
        WorstTemp.Text  = worstTemp;
    }

    private void ViewDetails_Click(object? sender, RoutedEventArgs e)
        => _nav?.NavigateTo("disk-health");
}
