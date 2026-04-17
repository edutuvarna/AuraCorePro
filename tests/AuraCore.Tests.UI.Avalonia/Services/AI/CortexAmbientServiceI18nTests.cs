using AuraCore.UI.Avalonia;
using AuraCore.UI.Avalonia.Services.AI;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Services.AI;

public class CortexAmbientServiceI18nTests
{
    private AppSettings FreshSettings(
        bool insights = false, bool recs = false,
        bool schedule = false, bool chat = false,
        DateTime? firstEnabled = null)
    => new()
    {
        InsightsEnabled = insights,
        RecommendationsEnabled = recs,
        ScheduleEnabled = schedule,
        ChatEnabled = chat,
        AIFirstEnabledAt = firstEnabled,
    };

    [Fact]
    public void ComputeStatusText_returns_localized_string_for_Active()
    {
        var settings = FreshSettings(insights: true, firstEnabled: DateTime.UtcNow.AddDays(-5));
        var svc = new CortexAmbientService(settings);
        var text = svc.AggregatedStatusText;

        // Should contain the localized "Active" part (via LocalizationService)
        Assert.NotNull(text);
        Assert.NotEmpty(text);
        // Verify it contains the expected localized active key
        Assert.Contains("Active", text);
    }

    [Fact]
    public void ComputeStatusText_returns_localized_string_for_Paused()
    {
        var settings = FreshSettings(firstEnabled: DateTime.UtcNow.AddDays(-1));
        var svc = new CortexAmbientService(settings);
        var text = svc.AggregatedStatusText;

        Assert.Equal(LocalizationService.Get("cortex.status.paused"), text);
    }

    [Fact]
    public void ComputeStatusText_returns_localized_string_for_Ready()
    {
        var settings = FreshSettings();
        var svc = new CortexAmbientService(settings);
        var text = svc.AggregatedStatusText;

        Assert.Equal(LocalizationService.Get("cortex.status.ready"), text);
    }

    [Fact]
    public void Localization_Key_cortex_status_paused_is_defined()
    {
        var value = LocalizationService.Get("cortex.status.paused");
        Assert.NotNull(value);
        Assert.NotEmpty(value);
        Assert.NotEqual("cortex.status.paused", value); // Should not be a fallback
    }

    [Fact]
    public void Localization_Key_cortex_status_ready_is_defined()
    {
        var value = LocalizationService.Get("cortex.status.ready");
        Assert.NotNull(value);
        Assert.NotEmpty(value);
        Assert.NotEqual("cortex.status.ready", value); // Should not be a fallback
    }

    [Fact]
    public void Localization_Key_cortex_status_active_is_defined()
    {
        var value = LocalizationService.Get("cortex.status.active");
        Assert.NotNull(value);
        Assert.NotEmpty(value);
        Assert.NotEqual("cortex.status.active", value); // Should not be a fallback
    }
}
