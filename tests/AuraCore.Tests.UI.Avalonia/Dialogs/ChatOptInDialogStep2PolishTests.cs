using AuraCore.UI.Avalonia;
using AuraCore.UI.Avalonia.ViewModels;
using System.ComponentModel;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Dialogs;

public class ChatOptInDialogStep2PolishTests
{
    [Fact]
    public void ChatOptInDialogViewModel_exposes_RecommendedId_property()
    {
        var prop = typeof(ChatOptInDialogViewModel).GetProperty("RecommendedId");
        Assert.NotNull(prop);
        Assert.True(
            prop!.PropertyType == typeof(string) || prop.PropertyType == typeof(string),
            "RecommendedId must be string type");
    }

    [Fact]
    public void ChatOptInDialogViewModel_RecommendedId_nullable_string()
    {
        var prop = typeof(ChatOptInDialogViewModel).GetProperty("RecommendedId");
        Assert.NotNull(prop);
        // nullable string — underlying type is string, nullable annotation tracked at compile time
        Assert.Equal(typeof(string), prop!.PropertyType);
    }

    [Fact]
    public void ChatOptInDialogViewModel_implements_INPC()
    {
        Assert.True(
            typeof(INotifyPropertyChanged).IsAssignableFrom(typeof(ChatOptInDialogViewModel)),
            "ChatOptInDialogViewModel must implement INotifyPropertyChanged for hero card binding");
    }

    [Fact]
    public void ChatOptInDialogViewModel_exposes_IsNotStep1_property()
    {
        var prop = typeof(ChatOptInDialogViewModel).GetProperty("IsNotStep1");
        Assert.NotNull(prop);
        Assert.Equal(typeof(bool), prop!.PropertyType);
    }

    [Fact]
    public void ChatOptInDialogViewModel_exposes_IsNotStep2_property()
    {
        var prop = typeof(ChatOptInDialogViewModel).GetProperty("IsNotStep2");
        Assert.NotNull(prop);
        Assert.Equal(typeof(bool), prop!.PropertyType);
    }

    [Fact]
    public void ChatOptInDialogViewModel_exposes_RecommendedDisplayName_property()
    {
        var prop = typeof(ChatOptInDialogViewModel).GetProperty("RecommendedDisplayName");
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop!.PropertyType);
    }

    [Fact]
    public void IsNotStep1_is_true_when_at_step2()
    {
        var settings = new AppSettings { ChatOptInAcknowledged = true };
        var vm = new ChatOptInDialogViewModel(settings);
        Assert.True(vm.IsStep2);
        Assert.True(vm.IsNotStep1);
        Assert.False(vm.IsNotStep2);
    }

    [Fact]
    public void IsNotStep2_is_true_when_at_step1()
    {
        var settings = new AppSettings { ChatOptInAcknowledged = false };
        var vm = new ChatOptInDialogViewModel(settings);
        Assert.True(vm.IsStep1);
        Assert.False(vm.IsNotStep1);
        Assert.True(vm.IsNotStep2);
    }

    [Fact]
    public void RecommendedId_can_be_set_and_raises_INPC()
    {
        var settings = new AppSettings { ChatOptInAcknowledged = false };
        var vm = new ChatOptInDialogViewModel(settings);

        string? changedProperty = null;
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ChatOptInDialogViewModel.RecommendedId))
                changedProperty = e.PropertyName;
        };

        vm.RecommendedId = "phi3-mini-q4km";

        Assert.Equal("phi3-mini-q4km", vm.RecommendedId);
        Assert.Equal(nameof(ChatOptInDialogViewModel.RecommendedId), changedProperty);
    }

    [Fact]
    public void RecommendedDisplayName_can_be_set_and_raises_INPC()
    {
        var settings = new AppSettings { ChatOptInAcknowledged = false };
        var vm = new ChatOptInDialogViewModel(settings);

        string? changedProperty = null;
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ChatOptInDialogViewModel.RecommendedDisplayName))
                changedProperty = e.PropertyName;
        };

        vm.RecommendedDisplayName = "Phi-3 Mini (Q4 K-M)";

        Assert.Equal("Phi-3 Mini (Q4 K-M)", vm.RecommendedDisplayName);
        Assert.Equal(nameof(ChatOptInDialogViewModel.RecommendedDisplayName), changedProperty);
    }

    [Fact]
    public void IsNotStep1_and_IsNotStep2_are_consistent_at_step1()
    {
        var settings = new AppSettings { ChatOptInAcknowledged = false };
        var vm = new ChatOptInDialogViewModel(settings);

        Assert.True(vm.IsStep1);
        Assert.False(vm.IsNotStep1);
        Assert.True(vm.IsNotStep2);
        // IsStep1 and IsNotStep1 are always inverse
        Assert.Equal(vm.IsStep1, !vm.IsNotStep1);
        Assert.Equal(vm.IsStep2, !vm.IsNotStep2);
    }

    [Fact]
    public void IsNotStep1_and_IsNotStep2_are_consistent_at_step2()
    {
        var settings = new AppSettings { ChatOptInAcknowledged = true };
        var vm = new ChatOptInDialogViewModel(settings);

        Assert.True(vm.IsStep2);
        Assert.True(vm.IsNotStep1);
        Assert.False(vm.IsNotStep2);
        Assert.Equal(vm.IsStep1, !vm.IsNotStep1);
        Assert.Equal(vm.IsStep2, !vm.IsNotStep2);
    }

    [Fact]
    public void RecommendedId_INPC_also_notifies_RecommendedDisplayName()
    {
        // Setting RecommendedId re-raises RecommendedDisplayName notification too
        var settings = new AppSettings { ChatOptInAcknowledged = false };
        var vm = new ChatOptInDialogViewModel(settings);

        var notified = new System.Collections.Generic.List<string?>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        vm.RecommendedId = "test-model";

        Assert.Contains(nameof(ChatOptInDialogViewModel.RecommendedId), notified);
        Assert.Contains(nameof(ChatOptInDialogViewModel.RecommendedDisplayName), notified);
    }
}
