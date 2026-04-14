using System.Windows.Input;
using Avalonia.Headless.XUnit;
using AuraCore.UI.Avalonia.Views.Controls;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Controls;

public class HeroCTATests
{
    [AvaloniaFact]
    public void HeroCTA_Defaults()
    {
        var hero = new HeroCTA();
        Assert.Equal(string.Empty, hero.Kicker);
        Assert.Equal(string.Empty, hero.Title);
        Assert.Equal("Go", hero.PrimaryButtonText);
    }

    [AvaloniaFact]
    public void HeroCTA_PrimaryCommand_Invokes()
    {
        var invoked = false;
        var hero = new HeroCTA
        {
            PrimaryCommand = new TestCommand(() => invoked = true)
        };
        hero.PrimaryCommand.Execute(null);
        Assert.True(invoked);
    }

    [AvaloniaFact]
    public void HeroCTA_RendersInWindow()
    {
        var hero = new HeroCTA
        {
            Kicker = "CORTEX RECOMMENDS",
            Title = "Smart Optimize Now",
            Body = "RAM cleanup + 3 bloatware apps"
        };
        using var window = AvaloniaTestBase.RenderInWindow(hero, 400, 200);
        Assert.True(hero.IsMeasureValid);
    }

    private sealed class TestCommand : ICommand
    {
        private readonly System.Action _action;
        public TestCommand(System.Action action) => _action = action;
        public event System.EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _action();
    }
}
