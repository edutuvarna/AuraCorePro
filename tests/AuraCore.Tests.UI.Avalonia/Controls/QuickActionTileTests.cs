using System.Linq;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using AuraCore.UI.Avalonia.Views.Controls;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Controls;

public class QuickActionTileTests
{
    [AvaloniaFact]
    public void QuickActionTile_Defaults()
    {
        var tile = new QuickActionTile();
        Assert.Equal(string.Empty, tile.Title);
        Assert.Equal(string.Empty, tile.SubLabel);
    }

    [AvaloniaFact]
    public void QuickActionTile_CommandInvokes()
    {
        var fired = false;
        var tile = new QuickActionTile
        {
            Title = "Clean Junk",
            Command = new DelegateCmd(() => fired = true)
        };
        tile.Command!.Execute(null);
        Assert.True(fired);
    }

    [AvaloniaFact]
    public void QuickActionTile_RendersInWindow()
    {
        var tile = new QuickActionTile { Title = "Clean Junk", SubLabel = "Temp, cache, logs" };
        using var window = AvaloniaTestBase.RenderInWindow(tile, 200, 80);
        Assert.True(tile.IsMeasureValid);
    }

    [AvaloniaFact]
    public void QuickActionTile_ClickingTile_InvokesBoundCommand()
    {
        var invoked = false;
        var tile = new QuickActionTile
        {
            Title = "Clean Junk",
            Command = new DelegateCmd(() => invoked = true)
        };
        using var window = AvaloniaTestBase.RenderInWindow(tile, 200, 80);

        var button = tile.GetLogicalDescendants().OfType<Button>().FirstOrDefault();
        Assert.NotNull(button);
        Assert.NotNull(button.Command);
        button.Command.Execute(null);
        Assert.True(invoked);
    }

    private sealed class DelegateCmd : ICommand
    {
        private readonly System.Action _a;
        public DelegateCmd(System.Action a) => _a = a;
        public event System.EventHandler? CanExecuteChanged;
        public bool CanExecute(object? p) => true;
        public void Execute(object? p) => _a();
    }
}
