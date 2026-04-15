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
