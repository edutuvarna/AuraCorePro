using AuraCore.UI.Avalonia;
using AuraCore.UI.Avalonia.ViewModels;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

public class ChatOptInFlowIntegrationTests
{
    [Fact]
    public void Step1Continue_AdvancesToStep2()
    {
        var settings = new AppSettings { ChatOptInAcknowledged = false };
        var vm = new ChatOptInDialogViewModel(settings);
        vm.ContinueFromStep1.Execute(null);
        Assert.Equal(2, vm.CurrentStep);
        Assert.True(settings.ChatOptInAcknowledged);
    }

    [Fact]
    public void Step2Completion_EnablesChatAndSetsModel()
    {
        var settings = new AppSettings { ChatOptInAcknowledged = true };
        var vm = new ChatOptInDialogViewModel(settings);
        vm.CompleteFromStep2("phi3-mini-q4km");
        Assert.True(settings.ChatEnabled);
        Assert.Equal("phi3-mini-q4km", settings.ActiveChatModelId);
    }

    [Fact]
    public void Step2Cancel_KeepsAcknowledgedButChatOff()
    {
        var settings = new AppSettings { ChatOptInAcknowledged = true };
        var vm = new ChatOptInDialogViewModel(settings);
        vm.CancelFromStep2.Execute(null);
        Assert.True(settings.ChatOptInAcknowledged);
        Assert.False(settings.ChatEnabled);
        Assert.Null(settings.ActiveChatModelId);
    }
}
