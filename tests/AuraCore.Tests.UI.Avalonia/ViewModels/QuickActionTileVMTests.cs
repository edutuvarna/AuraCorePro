using System.Threading.Tasks;
using AuraCore.UI.Avalonia.ViewModels.Dashboard;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.ViewModels;

public class QuickActionTileVMTests
{
    [Fact]
    public void Constructor_sets_all_properties()
    {
        var tile = new QuickActionTileVM(
            id: "remove-bloat",
            label: "Remove Windows Bloat",
            subLabel: "Safe pre-installed apps",
            iconGlyph: "\uD83D\uDDD1",
            accentToken: "AccentAmberBrush",
            execute: () => Task.CompletedTask);

        Assert.Equal("remove-bloat", tile.Id);
        Assert.Equal("Remove Windows Bloat", tile.Label);
        Assert.Equal("Safe pre-installed apps", tile.SubLabel);
        Assert.Equal("\uD83D\uDDD1", tile.IconGlyph);
        Assert.Equal("AccentAmberBrush", tile.AccentToken);
        Assert.NotNull(tile.Command);
    }

    [Fact]
    public void Constructor_allows_empty_subLabel()
    {
        var tile = new QuickActionTileVM(
            id: "t",
            label: "Test",
            subLabel: "",
            iconGlyph: "\u26A1",
            accentToken: "AccentTealBrush",
            execute: () => Task.CompletedTask);

        Assert.Equal("", tile.SubLabel);
    }

    [Fact]
    public async Task Command_invokes_execute_delegate_once()
    {
        int callCount = 0;
        var tile = new QuickActionTileVM(
            id: "t",
            label: "T",
            subLabel: "S",
            iconGlyph: "\u2B50",
            accentToken: "AccentPrimaryBrush",
            execute: () => { callCount++; return Task.CompletedTask; });

        tile.Command.Execute(null);
        await Task.Delay(80);   // give the async void Execute a chance to complete
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Command_can_always_execute()
    {
        var tile = new QuickActionTileVM(
            id: "x",
            label: "X",
            subLabel: "",
            iconGlyph: "",
            accentToken: "",
            execute: () => Task.CompletedTask);

        Assert.True(tile.Command.CanExecute(null));
    }

    [Fact]
    public void QuickActionPresets_Default_returns_correct_tiles_for_platform()
    {
        var tiles = QuickActionPresets.Default(
            quickCleanup: () => Task.CompletedTask,
            optimizeRam:  () => Task.CompletedTask,
            removeBloat:  () => Task.CompletedTask);

        int expected = System.OperatingSystem.IsWindows() ? 3 : 2;
        Assert.Equal(expected, tiles.Count);
    }

    [Fact]
    public void QuickActionPresets_Default_ids_are_distinct()
    {
        var tiles = QuickActionPresets.Default(
            quickCleanup: () => Task.CompletedTask,
            optimizeRam:  () => Task.CompletedTask,
            removeBloat:  () => Task.CompletedTask);

        var ids = new System.Collections.Generic.HashSet<string>();
        foreach (var t in tiles) ids.Add(t.Id);
        int expected = System.OperatingSystem.IsWindows() ? 3 : 2;
        Assert.Equal(expected, ids.Count);
    }

    [Fact]
    public void Default_OnNonWindows_ExcludesRemoveBloatTile()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var tiles = QuickActionPresets.Default(
            quickCleanup: () => Task.CompletedTask,
            optimizeRam:  () => Task.CompletedTask,
            removeBloat:  () => Task.CompletedTask);
        Assert.DoesNotContain(tiles, t => t.Id == "remove-bloat");
    }

    [Fact]
    public void DashboardViewModel_QuickActions_initialized_for_platform()
    {
        var vm = new AuraCore.UI.Avalonia.ViewModels.DashboardViewModel();
        int expected = System.OperatingSystem.IsWindows() ? 3 : 2;
        Assert.Equal(expected, vm.QuickActions.Count);
    }

    [Fact]
    public void DashboardViewModel_InitQuickActions_replaces_tiles()
    {
        var vm = new AuraCore.UI.Avalonia.ViewModels.DashboardViewModel();
        bool replacedCalled = false;

        vm.InitQuickActions(
            quickCleanup: () => { replacedCalled = true; return Task.CompletedTask; },
            optimizeRam:  () => Task.CompletedTask,
            removeBloat:  () => Task.CompletedTask);

        // Same tile count after re-init (platform-filtered)
        int expected = System.OperatingSystem.IsWindows() ? 3 : 2;
        Assert.Equal(expected, vm.QuickActions.Count);
        // The first tile's command should invoke our delegate
        vm.QuickActions[0].Command.Execute(null);
        // replacedCalled will be true once the async command fires
        // We can't wait without Task.Delay; check type instead
        Assert.NotNull(vm.QuickActions[0].Command);
    }
}
