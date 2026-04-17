using AuraCore.UI.Avalonia;
using AuraCore.UI.Avalonia.Services.AI;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

/// <summary>
/// Task 33 (spec §5.3): the global status bar baseline reflects the CORTEX
/// ambient state. Formatting is centralized on <see cref="ICortexAmbientService.FormattedStatusText"/>
/// so the status-bar consumer (MainWindow) doesn't embed the "✦ Cortex · "
/// prefix literal. Ambient state→text mapping itself is covered by
/// CortexAmbientServiceTests.
/// </summary>
public class StatusBarTests
{
    // Phase 5.4.1.2 (commit ecb312c) wired CortexAmbientService.ComputeStatusText
    // through LocalizationService.Get, so state-labels now vary by active locale
    // (EN "Active" / TR "Aktif", EN "Paused" / TR "Duraklatıldı", EN "Ready to start"
    // / TR "Başlamaya Hazır"). Tests now pull the expected label from
    // LocalizationService at assert-time, which keeps them green on any locale.

    [Fact]
    public void FormattedStatusText_PrefixesWithCortex_WhenAnyFeatureEnabled()
    {
        var settings = new AppSettings { InsightsEnabled = true };
        var ambient = new CortexAmbientService(settings);

        Assert.StartsWith("\u2728 Cortex \u00B7", ambient.FormattedStatusText);
        Assert.Contains(LocalizationService.Get("cortex.status.active"), ambient.FormattedStatusText);
    }

    [Fact]
    public void FormattedStatusText_PrefixesWithCortex_WhenPaused()
    {
        // All four features off + AIFirstEnabledAt set = Paused.
        // (AppSettings defaults turn Insights/Recommendations/Schedule ON,
        // so we must explicitly disable them for the Paused scenario.)
        var settings = new AppSettings
        {
            InsightsEnabled = false,
            RecommendationsEnabled = false,
            ScheduleEnabled = false,
            ChatEnabled = false,
            AIFirstEnabledAt = System.DateTime.UtcNow,
        };
        var ambient = new CortexAmbientService(settings);

        Assert.StartsWith("\u2728 Cortex \u00B7", ambient.FormattedStatusText);
        Assert.Contains(LocalizationService.Get("cortex.status.paused"), ambient.FormattedStatusText);
    }

    [Fact]
    public void FormattedStatusText_PrefixesWithCortex_WhenReady()
    {
        // All off + AIFirstEnabledAt null = Ready (never activated).
        var settings = new AppSettings
        {
            InsightsEnabled = false,
            RecommendationsEnabled = false,
            ScheduleEnabled = false,
            ChatEnabled = false,
            AIFirstEnabledAt = null,
        };
        var ambient = new CortexAmbientService(settings);

        Assert.StartsWith("\u2728 Cortex \u00B7", ambient.FormattedStatusText);
        Assert.Contains(LocalizationService.Get("cortex.status.ready"), ambient.FormattedStatusText);
    }

    [Fact]
    public void FormattedStatusText_UpdatesAfterRefresh_WhenFeaturesToggled()
    {
        var settings = new AppSettings
        {
            InsightsEnabled = false,
            RecommendationsEnabled = false,
            ScheduleEnabled = false,
            ChatEnabled = false,
        };
        var ambient = new CortexAmbientService(settings);

        // Ready initially (never activated)
        Assert.Contains(LocalizationService.Get("cortex.status.ready"), ambient.FormattedStatusText);

        settings.InsightsEnabled = true;
        ambient.Refresh();

        Assert.Contains(LocalizationService.Get("cortex.status.active"), ambient.FormattedStatusText);

        settings.InsightsEnabled = false;
        ambient.Refresh();

        // After any feature was ever on, off means Paused (not Ready) —
        // Refresh stamps AIFirstEnabledAt on first transition to enabled.
        Assert.Contains(LocalizationService.Get("cortex.status.paused"), ambient.FormattedStatusText);
    }
}
