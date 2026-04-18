using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.UI.Avalonia.Helpers;

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
        ApplyLocalization();
        LocalizationService.LanguageChanged += () =>
            Dispatcher.UIThread.Post(ApplyLocalization);
    }

    private void ApplyLocalization()
    {
        var titleText = this.FindControl<TextBlock>("TitleText");
        if (titleText is not null)
            titleText.Text = LocalizationService.Get("diskHealth.card.title");

        var viewDetailsBtn = this.FindControl<Button>("ViewDetailsBtn");
        if (viewDetailsBtn is not null)
            viewDetailsBtn.Content = LocalizationService.Get("diskHealth.card.viewDetails");

        var worstTempLabel = this.FindControl<TextBlock>("WorstTempLabel");
        if (worstTempLabel is not null)
            worstTempLabel.Text = LocalizationService.Get("diskHealth.card.worstTemp");
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

    /// <summary>
    /// Updates the card's three summary fields from a completed scan result.
    /// Must be called on the UI thread (use <see cref="Dispatcher.UIThread"/> when
    /// posting from a background task).
    /// </summary>
    public void ApplyScanResult(DiskHealthScanResult result)
    {
        StatusText    = result.StatusText;
        SmartText     = result.SmartText;
        WorstTempText = result.WorstTempText;
        StatusLine.Text = result.StatusText;
        SmartBadge.Text = result.SmartText;
        WorstTemp.Text  = result.WorstTempText;
    }

    private void ViewDetails_Click(object? sender, RoutedEventArgs e)
        => _nav?.NavigateTo("disk-health");
}
