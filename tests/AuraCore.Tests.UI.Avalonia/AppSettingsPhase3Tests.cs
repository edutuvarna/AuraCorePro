using AuraCore.UI.Avalonia;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia;

public class AppSettingsPhase3Tests
{
    [Fact]
    public void InsightsEnabled_Default_IsTrue()
    {
        var settings = new AppSettings();
        Assert.True(settings.InsightsEnabled);
    }

    [Fact]
    public void RecommendationsEnabled_Default_IsTrue()
    {
        var settings = new AppSettings();
        Assert.True(settings.RecommendationsEnabled);
    }

    [Fact]
    public void ScheduleEnabled_Default_IsTrue()
    {
        var settings = new AppSettings();
        Assert.True(settings.ScheduleEnabled);
    }

    [Fact]
    public void ChatEnabled_Default_IsFalse()
    {
        var settings = new AppSettings();
        Assert.False(settings.ChatEnabled);
    }

    [Fact]
    public void ChatOptInAcknowledged_Default_IsFalse()
    {
        var settings = new AppSettings();
        Assert.False(settings.ChatOptInAcknowledged);
    }

    [Fact]
    public void ActiveChatModelId_Default_IsNull()
    {
        var settings = new AppSettings();
        Assert.Null(settings.ActiveChatModelId);
    }

    [Fact]
    public void AIFirstEnabledAt_Default_IsNull()
    {
        var settings = new AppSettings();
        Assert.Null(settings.AIFirstEnabledAt);
    }
}
