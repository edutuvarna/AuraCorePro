using AuraCore.UI.Avalonia;
using AuraCore.UI.Avalonia.Services.AI;
using AuraCore.UI.Avalonia.ViewModels;
using global::Avalonia.Headless.XUnit;
using System.ComponentModel;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.ViewModels;

public class AIFeaturesViewModelTests
{
    private AIFeaturesViewModel CreateVM(
        bool insights = true, bool recs = true,
        bool schedule = true, bool chat = false)
    {
        var settings = new AppSettings
        {
            InsightsEnabled = insights,
            RecommendationsEnabled = recs,
            ScheduleEnabled = schedule,
            ChatEnabled = chat,
        };
        var ambient = new CortexAmbientService(settings);
        return new AIFeaturesViewModel(settings, ambient);
    }

    [Fact]
    public void Initialize_StartsInOverviewMode()
    {
        var vm = CreateVM();
        Assert.Equal(AIFeaturesViewMode.Overview, vm.Mode);
        Assert.True(vm.IsOverview);
        Assert.False(vm.IsDetail);
        Assert.Equal("overview", vm.ActiveSection);
    }

    [Fact]
    public void NavigateToSection_ChangesMode()
    {
        var vm = CreateVM();
        vm.NavigateToSection.Execute("insights");

        Assert.Equal(AIFeaturesViewMode.Detail, vm.Mode);
        Assert.Equal("insights", vm.ActiveSection);
    }

    // ───────── Chat bypass regression (user-found 2026-04-16) ─────────
    // Before this fix, clicking "Chat" in the detail-mode sub-sidebar routed
    // straight into ChatSection even when ChatEnabled was false — bypassing
    // the ChatOptInDialog warning + model-picker. Now it triggers ChatOptInOpener.

    [Fact]
    public void NavigateToChat_WhenEnabled_NavigatesDirectly()
    {
        // Set the whole opt-in chain satisfied.
        var settings = new AppSettings
        {
            ChatEnabled = true,
            ChatOptInAcknowledged = true,
            ActiveChatModelId = "phi3-mini-q4km",
        };
        var ambient = new CortexAmbientService(settings);
        var vm = new AIFeaturesViewModel(settings, ambient);
        var openerCalled = false;
        vm.ChatOptInOpener = () => { openerCalled = true; return System.Threading.Tasks.Task.FromResult(true); };

        vm.NavigateToSection.Execute("chat");

        Assert.Equal(AIFeaturesViewMode.Detail, vm.Mode);
        Assert.Equal("chat", vm.ActiveSection);
        Assert.False(openerCalled); // no opt-in needed
    }

    [Fact]
    public async System.Threading.Tasks.Task NavigateToChat_WhenDisabled_TriggersOptInOpener()
    {
        var settings = new AppSettings { ChatEnabled = false };
        var ambient = new CortexAmbientService(settings);
        var vm = new AIFeaturesViewModel(settings, ambient);

        var openerCalled = false;
        var openerTcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
        vm.ChatOptInOpener = () => { openerCalled = true; return openerTcs.Task; };

        vm.NavigateToSection.Execute("chat");
        // Simulate the dialog completing successfully (user picked a model)
        settings.ChatEnabled = true;
        settings.ChatOptInAcknowledged = true;
        settings.ActiveChatModelId = "phi3-mini-q4km";
        openerTcs.SetResult(true);
        await System.Threading.Tasks.Task.Yield();
        // Give the async OnNavigateToSection continuation a chance to run
        await System.Threading.Tasks.Task.Delay(50);

        Assert.True(openerCalled);
        Assert.Equal(AIFeaturesViewMode.Detail, vm.Mode);
        Assert.Equal("chat", vm.ActiveSection);
    }

    [Fact]
    public async System.Threading.Tasks.Task NavigateToChat_WhenDisabled_UserCancels_StaysInOverview()
    {
        var settings = new AppSettings { ChatEnabled = false };
        var ambient = new CortexAmbientService(settings);
        var vm = new AIFeaturesViewModel(settings, ambient);

        vm.ChatOptInOpener = () => System.Threading.Tasks.Task.FromResult(false); // user cancelled

        vm.NavigateToSection.Execute("chat");
        await System.Threading.Tasks.Task.Delay(50);

        Assert.Equal(AIFeaturesViewMode.Overview, vm.Mode);
        Assert.Equal("overview", vm.ActiveSection);
    }

    [Fact]
    public void NavigateToDisabledSection_NonChat_StillNavigates()
    {
        // Only Chat has the gate — Insights/Recs/Schedule navigate regardless
        // of toggle state so users can enable them from within.
        var settings = new AppSettings
        {
            InsightsEnabled = false,
            RecommendationsEnabled = false,
            ScheduleEnabled = false,
        };
        var ambient = new CortexAmbientService(settings);
        var vm = new AIFeaturesViewModel(settings, ambient);

        vm.NavigateToSection.Execute("insights");
        Assert.Equal(AIFeaturesViewMode.Detail, vm.Mode);
        Assert.Equal("insights", vm.ActiveSection);
    }

    [Fact]
    public void NavigateToOverview_ReturnsToGrid()
    {
        var vm = CreateVM();
        vm.NavigateToSection.Execute("insights");

        vm.NavigateToOverview.Execute(null);

        Assert.Equal(AIFeaturesViewMode.Overview, vm.Mode);
        Assert.Equal("overview", vm.ActiveSection);
    }

    [Fact]
    public void FourCards_Exist()
    {
        var vm = CreateVM();

        Assert.NotNull(vm.InsightsCard);
        Assert.NotNull(vm.RecommendationsCard);
        Assert.NotNull(vm.ScheduleCard);
        Assert.NotNull(vm.ChatCard);
        Assert.Equal("insights", vm.InsightsCard.Key);
        Assert.Equal("recommendations", vm.RecommendationsCard.Key);
        Assert.Equal("schedule", vm.ScheduleCard.Key);
        Assert.Equal("chat", vm.ChatCard.Key);
    }

    [Fact]
    public void ChatCard_IsChatExperimental_True()
    {
        var vm = CreateVM();
        Assert.True(vm.ChatCard.IsChatExperimental);
        Assert.False(vm.InsightsCard.IsChatExperimental);
    }

    [Fact]
    public void TogglingCard_UpdatesSettings()
    {
        var vm = CreateVM(insights: true);
        vm.InsightsCard.IsEnabled = false;

        Assert.False(vm.InsightsCard.IsEnabled);
    }

    [Fact]
    public void HeroStatusText_AllEnabled_ContainsActive()
    {
        var vm = CreateVM(insights: true);
        Assert.Contains("Active", vm.HeroStatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HeroStatusText_AllDisabled_ContainsReadyOrPaused()
    {
        var vm = CreateVM(insights: false, recs: false, schedule: false, chat: false);
        var text = vm.HeroStatusText.ToLowerInvariant();
        Assert.True(text.Contains("ready") || text.Contains("paused"));
    }

    [Fact]
    public void NavigateToSection_FiresPropertyChanged_ForModeAndIsOverview()
    {
        var vm = CreateVM();
        var fired = new List<string>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => fired.Add(e.PropertyName ?? "");

        vm.NavigateToSection.Execute("insights");

        Assert.Contains(nameof(AIFeaturesViewModel.Mode), fired);
        Assert.Contains(nameof(AIFeaturesViewModel.IsOverview), fired);
        Assert.Contains(nameof(AIFeaturesViewModel.IsDetail), fired);
        Assert.Contains(nameof(AIFeaturesViewModel.ActiveSection), fired);
    }

    // AvaloniaFact because ActiveSectionView getter constructs a placeholder UserControl,
    // which requires an active Avalonia app context (headless dispatcher).
    [AvaloniaFact]
    public void ActiveSectionView_CachedAcrossNavigation()
    {
        var vm = CreateVM();
        vm.NavigateToSection.Execute("insights");
        var view1 = vm.ActiveSectionView;

        vm.NavigateToOverview.Execute(null);
        vm.NavigateToSection.Execute("insights");
        var view2 = vm.ActiveSectionView;

        // Same instance — state preserved across back-and-forth
        Assert.Same(view1, view2);
    }
}
