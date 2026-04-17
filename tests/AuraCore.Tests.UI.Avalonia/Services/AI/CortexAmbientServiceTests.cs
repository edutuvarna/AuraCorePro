using AuraCore.UI.Avalonia;
using AuraCore.UI.Avalonia.Services.AI;
using System.ComponentModel;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Services.AI;

public class CortexAmbientServiceTests
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
    public void AllOff_ReportsPaused_WhenAIFirstEnabledAtSet()
    {
        var settings = FreshSettings(firstEnabled: DateTime.UtcNow.AddDays(-2));
        var svc = new CortexAmbientService(settings);

        Assert.Equal(CortexActiveness.Paused, svc.Activeness);
        Assert.False(svc.AnyFeatureEnabled);
    }

    [Fact]
    public void AllOff_ReportsReady_WhenAIFirstEnabledAtNull()
    {
        var settings = FreshSettings();
        var svc = new CortexAmbientService(settings);

        Assert.Equal(CortexActiveness.Ready, svc.Activeness);
    }

    [Fact]
    public void AnyOn_ReportsActive()
    {
        var settings = FreshSettings(insights: true);
        var svc = new CortexAmbientService(settings);

        Assert.Equal(CortexActiveness.Active, svc.Activeness);
        Assert.True(svc.AnyFeatureEnabled);
    }

    [Fact]
    public void EnabledFeatureCount_CountsCorrectly()
    {
        var settings = FreshSettings(insights: true, recs: true);
        var svc = new CortexAmbientService(settings);

        Assert.Equal(2, svc.EnabledFeatureCount);
        Assert.Equal(4, svc.TotalFeatureCount);
    }

    [Fact]
    public void LearningDay_Null_ReturnsZero()
    {
        var settings = FreshSettings();
        var svc = new CortexAmbientService(settings);

        Assert.Equal(0, svc.LearningDay);
    }

    [Fact]
    public void LearningDay_TwoDaysAgo_ReturnsTwo()
    {
        var settings = FreshSettings(firstEnabled: DateTime.UtcNow.AddDays(-2));
        var svc = new CortexAmbientService(settings);

        Assert.InRange(svc.LearningDay, 1, 3);
    }

    [Fact]
    public void LearningDay_FutureTimestamp_ClampedToZero()
    {
        var settings = FreshSettings(firstEnabled: DateTime.UtcNow.AddDays(5));
        var svc = new CortexAmbientService(settings);

        Assert.Equal(0, svc.LearningDay);
    }

    [Fact]
    public void RecomputeState_AfterEnableFlag_FiresPropertyChanged()
    {
        var settings = FreshSettings();
        var svc = new CortexAmbientService(settings);

        var changed = new List<string>();
        ((INotifyPropertyChanged)svc).PropertyChanged += (_, e) =>
            changed.Add(e.PropertyName ?? "");

        settings.InsightsEnabled = true;
        svc.Refresh();

        Assert.Contains(nameof(ICortexAmbientService.AnyFeatureEnabled), changed);
        Assert.Contains(nameof(ICortexAmbientService.Activeness), changed);
    }

    [Fact]
    public void Refresh_FirstEnable_StampsAIFirstEnabledAt()
    {
        var settings = FreshSettings();
        Assert.Null(settings.AIFirstEnabledAt);

        settings.InsightsEnabled = true;
        var svc = new CortexAmbientService(settings);
        svc.Refresh();

        Assert.NotNull(settings.AIFirstEnabledAt);
    }

    [Fact]
    public void Refresh_SubsequentEnable_DoesNotOverwriteAIFirstEnabledAt()
    {
        var original = DateTime.UtcNow.AddDays(-5);
        var settings = FreshSettings(insights: true, firstEnabled: original);
        var svc = new CortexAmbientService(settings);

        settings.RecommendationsEnabled = true;
        svc.Refresh();

        Assert.Equal(original, settings.AIFirstEnabledAt);
    }

    [Fact]
    public void AggregatedStatusText_Active_ContainsLearningDay()
    {
        var settings = FreshSettings(insights: true, firstEnabled: DateTime.UtcNow.AddDays(-2));
        var svc = new CortexAmbientService(settings);

        Assert.Contains("day", svc.AggregatedStatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AggregatedStatusText_Paused_SaysPaused()
    {
        var settings = FreshSettings(firstEnabled: DateTime.UtcNow.AddDays(-1));
        var svc = new CortexAmbientService(settings);

        Assert.Contains("paused", svc.AggregatedStatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AggregatedStatusText_Ready_SaysReady()
    {
        var settings = FreshSettings();
        var svc = new CortexAmbientService(settings);

        Assert.Contains("ready", svc.AggregatedStatusText, StringComparison.OrdinalIgnoreCase);
    }
}
