using System.Collections.Generic;
using System.ComponentModel;
using AuraCore.UI.Avalonia;
using AuraCore.UI.Avalonia.Services.AI;
using AuraCore.UI.Avalonia.ViewModels;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.ViewModels;

/// <summary>
/// Task 32 (spec §5.3): DashboardViewModel subscribes to CortexAmbientService
/// and exposes ripple properties so toggling features in AIFeaturesView visibly
/// changes Dashboard state.
/// </summary>
public class DashboardViewModelRippleTests
{
    private static (AppSettings settings, CortexAmbientService ambient, DashboardViewModel vm) Setup(
        bool insights = true, bool recs = true, bool schedule = false, bool chat = false)
    {
        var settings = new AppSettings
        {
            InsightsEnabled = insights,
            RecommendationsEnabled = recs,
            ScheduleEnabled = schedule,
            ChatEnabled = chat,
        };
        var ambient = new CortexAmbientService(settings);
        var vm = new DashboardViewModel(ambient, settings);
        return (settings, ambient, vm);
    }

    [Fact]
    public void ShowCortexInsightsCard_True_WhenInsightsEnabled()
    {
        var (_, _, vm) = Setup(insights: true);
        Assert.True(vm.ShowCortexInsightsCard);
    }

    [Fact]
    public void ShowCortexInsightsCard_False_WhenInsightsDisabled()
    {
        var (_, _, vm) = Setup(insights: false);
        Assert.False(vm.ShowCortexInsightsCard);
    }

    [Fact]
    public void ShowCortexSubtitle_FollowsInsightsEnabled()
    {
        var (_, _, vmOn)  = Setup(insights: true);
        var (_, _, vmOff) = Setup(insights: false);
        Assert.True(vmOn.ShowCortexSubtitle);
        Assert.False(vmOff.ShowCortexSubtitle);
    }

    [Fact]
    public void CortexChipState_ReturnsOn_WhenAnyFeatureEnabled()
    {
        var (_, _, vm) = Setup(insights: true, recs: false, schedule: false, chat: false);
        Assert.Equal("ON", vm.CortexChipState);
    }

    [Fact]
    public void CortexChipState_ReturnsOff_WhenAllFeaturesDisabled()
    {
        var (_, _, vm) = Setup(insights: false, recs: false, schedule: false, chat: false);
        Assert.Equal("OFF", vm.CortexChipState);
    }

    [Fact]
    public void CortexChipLabel_FormatsAsCortexAIState()
    {
        var (_, _, vmOn)  = Setup(insights: true);
        var (_, _, vmOff) = Setup(insights: false, recs: false, schedule: false, chat: false);
        Assert.Equal("Cortex AI · ON",  vmOn.CortexChipLabel);
        Assert.Equal("Cortex AI · OFF", vmOff.CortexChipLabel);
    }

    [Fact]
    public void SmartOptimizeEnabled_FollowsRecommendationsEnabled()
    {
        var (_, _, vmOn)  = Setup(recs: true);
        var (_, _, vmOff) = Setup(recs: false);
        Assert.True(vmOn.SmartOptimizeEnabled);
        Assert.False(vmOff.SmartOptimizeEnabled);
    }

    [Fact]
    public void ToggleInsights_FiresPropertyChanged_ForRippleProps()
    {
        var (settings, ambient, vm) = Setup(insights: false);
        var fired = new List<string>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => fired.Add(e.PropertyName ?? "");

        settings.InsightsEnabled = true;
        ambient.Refresh();

        Assert.Contains(nameof(DashboardViewModel.ShowCortexInsightsCard), fired);
        Assert.Contains(nameof(DashboardViewModel.ShowCortexSubtitle), fired);
    }

    [Fact]
    public void ToggleRecommendations_FiresSmartOptimizeEnabledChanged()
    {
        var (settings, ambient, vm) = Setup(recs: true);
        var fired = new List<string>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => fired.Add(e.PropertyName ?? "");

        settings.RecommendationsEnabled = false;
        ambient.Refresh();

        Assert.Contains(nameof(DashboardViewModel.SmartOptimizeEnabled), fired);
    }

    [Fact]
    public void ParameterlessCtor_StillWorks_NullAmbientReturnsSafeDefaults()
    {
        var vm = new DashboardViewModel();
        // Defaults: treat as "on" when no ambient wired — avoids hiding UI on first launch
        Assert.True(vm.ShowCortexInsightsCard);
        Assert.True(vm.ShowCortexSubtitle);
        Assert.True(vm.SmartOptimizeEnabled);
        // Chip state defaults to OFF when no ambient — clearer "not yet wired" signal than ON
        Assert.Equal("OFF", vm.CortexChipState);
    }

    [Fact]
    public void TurnOffAllFeatures_ChipGoesOff_InsightsCardHides_SmartOptimizeDisables()
    {
        var (settings, ambient, vm) = Setup(insights: true, recs: true, schedule: false, chat: false);
        // Sanity
        Assert.Equal("ON", vm.CortexChipState);
        Assert.True(vm.ShowCortexInsightsCard);
        Assert.True(vm.SmartOptimizeEnabled);

        settings.InsightsEnabled = false;
        settings.RecommendationsEnabled = false;
        ambient.Refresh();

        Assert.Equal("OFF", vm.CortexChipState);
        Assert.False(vm.ShowCortexInsightsCard);
        Assert.False(vm.SmartOptimizeEnabled);
    }
}
