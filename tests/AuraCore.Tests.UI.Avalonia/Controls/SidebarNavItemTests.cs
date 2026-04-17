using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using AuraCore.UI.Avalonia.Views.Controls;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Controls;

public class SidebarNavItemTests
{
    [AvaloniaFact]
    public void SidebarNavItem_Defaults()
    {
        var nav = new SidebarNavItem();
        Assert.Equal(string.Empty, nav.Label);
        Assert.False(nav.IsActive);
        Assert.Equal(string.Empty, nav.TrailingChipText);
    }

    [AvaloniaFact]
    public void SidebarNavItem_IsActiveToggles()
    {
        var nav = new SidebarNavItem { Label = "Dashboard" };
        nav.IsActive = true;
        Assert.True(nav.IsActive);
    }

    [AvaloniaFact]
    public void SidebarNavItem_RendersInWindow()
    {
        var nav = new SidebarNavItem { Label = "AI Features", TrailingChipText = "CORTEX" };
        using var window = AvaloniaTestBase.RenderInWindow(nav, 220, 32);
        Assert.True(nav.IsMeasureValid);
    }

    [AvaloniaFact]
    public void SidebarNavItem_ClickingNav_InvokesBoundCommand()
    {
        var invoked = false;
        var nav = new SidebarNavItem
        {
            Label = "Dashboard",
            Command = new DelegateCmd(() => invoked = true)
        };
        using var window = AvaloniaTestBase.RenderInWindow(nav, 220, 32);

        var button = nav.GetLogicalDescendants().OfType<Button>().FirstOrDefault();
        Assert.NotNull(button);
        Assert.NotNull(button.Command);
        button.Command.Execute(null);
        Assert.True(invoked);
    }

    private sealed class DelegateCmd : System.Windows.Input.ICommand
    {
        private readonly System.Action _a;
        public DelegateCmd(System.Action a) => _a = a;
        public event System.EventHandler? CanExecuteChanged;
        public bool CanExecute(object? p) => true;
        public void Execute(object? p) => _a();
    }
}
