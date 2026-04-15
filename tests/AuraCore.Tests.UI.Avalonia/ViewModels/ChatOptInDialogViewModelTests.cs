using AuraCore.UI.Avalonia;
using AuraCore.UI.Avalonia.ViewModels;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.ViewModels;

public class ChatOptInDialogViewModelTests
{
    [Fact]
    public void Initialize_Unacknowledged_StartsAtStep1()
    {
        var settings = new AppSettings { ChatOptInAcknowledged = false };
        var vm = new ChatOptInDialogViewModel(settings);
        Assert.Equal(1, vm.CurrentStep);
        Assert.True(vm.IsStep1);
    }

    [Fact]
    public void Initialize_Acknowledged_SkipsToStep2()
    {
        var settings = new AppSettings { ChatOptInAcknowledged = true };
        var vm = new ChatOptInDialogViewModel(settings);
        Assert.Equal(2, vm.CurrentStep);
        Assert.True(vm.IsStep2);
    }

    [Fact]
    public void ContinueFromStep1_SetsAcknowledgedAndAdvances()
    {
        var settings = new AppSettings { ChatOptInAcknowledged = false };
        var vm = new ChatOptInDialogViewModel(settings);
        vm.ContinueFromStep1.Execute(null);
        Assert.True(settings.ChatOptInAcknowledged);
        Assert.Equal(2, vm.CurrentStep);
    }

    [Fact]
    public void CancelFromStep1_LeavesAcknowledgedFalse()
    {
        var settings = new AppSettings { ChatOptInAcknowledged = false };
        var vm = new ChatOptInDialogViewModel(settings);
        var closed = false;
        vm.RequestClose = _ => closed = true;
        vm.CancelFromStep1.Execute(null);
        Assert.False(settings.ChatOptInAcknowledged);
        Assert.True(closed);
    }

    [Fact]
    public void CompleteFromStep2_SetsActiveModelAndEnablesChat()
    {
        var settings = new AppSettings { ChatOptInAcknowledged = true };
        var vm = new ChatOptInDialogViewModel(settings);
        vm.CompleteFromStep2("phi3-mini-q4km");
        Assert.Equal("phi3-mini-q4km", settings.ActiveChatModelId);
        Assert.True(settings.ChatEnabled);
    }

    [Fact]
    public void CancelFromStep2_KeepsAcknowledgedTrueButChatDisabled()
    {
        var settings = new AppSettings { ChatOptInAcknowledged = true };
        var vm = new ChatOptInDialogViewModel(settings);
        vm.CancelFromStep2.Execute(null);
        Assert.True(settings.ChatOptInAcknowledged);
        Assert.False(settings.ChatEnabled);
        Assert.Null(settings.ActiveChatModelId);
    }
}
