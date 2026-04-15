using AuraCore.UI.Avalonia.ViewModels;
using System.ComponentModel;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.ViewModels;

public class AIFeatureCardVMTests
{
    [Fact]
    public void Construct_WithInitialValues_ExposesThem()
    {
        var vm = new AIFeatureCardVM(
            key: "insights",
            title: "Cortex Insights",
            accentColor: "AccentPurple",
            iconKey: "IconSparklesFilled",
            isChatExperimental: false);

        Assert.Equal("insights", vm.Key);
        Assert.Equal("Cortex Insights", vm.Title);
        Assert.Equal("AccentPurple", vm.AccentColor);
        Assert.Equal("IconSparklesFilled", vm.IconKey);
        Assert.False(vm.IsChatExperimental);
    }

    [Fact]
    public void IsEnabled_DefaultFalse_CanBeSet()
    {
        var vm = new AIFeatureCardVM("insights", "T", "A", "I", false);
        Assert.False(vm.IsEnabled);

        vm.IsEnabled = true;

        Assert.True(vm.IsEnabled);
    }

    [Fact]
    public void IsEnabled_Change_FiresPropertyChanged()
    {
        var vm = new AIFeatureCardVM("insights", "T", "A", "I", false);
        var fired = new List<string>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => fired.Add(e.PropertyName ?? "");

        vm.IsEnabled = true;

        Assert.Contains(nameof(AIFeatureCardVM.IsEnabled), fired);
    }

    [Fact]
    public void PreviewSummary_Settable()
    {
        var vm = new AIFeatureCardVM("insights", "T", "A", "I", false);
        vm.PreviewSummary = "3 active";

        Assert.Equal("3 active", vm.PreviewSummary);
    }

    [Fact]
    public void HighlightText_NullableSettable()
    {
        var vm = new AIFeatureCardVM("insights", "T", "A", "I", false);
        vm.HighlightText = "Spike detected";
        vm.HighlightIcon = "⚠";

        Assert.Equal("Spike detected", vm.HighlightText);
        Assert.Equal("⚠", vm.HighlightIcon);
    }

    [Fact]
    public void IsChatExperimental_TrueForChat()
    {
        var vm = new AIFeatureCardVM("chat", "Chat", "AccentPink", "IconMessageSquare", true);
        Assert.True(vm.IsChatExperimental);
    }
}
