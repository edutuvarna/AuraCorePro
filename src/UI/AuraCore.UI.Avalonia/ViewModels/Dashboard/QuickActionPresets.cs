using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuraCore.UI.Avalonia.ViewModels.Dashboard;

/// <summary>
/// Factory for the Dashboard Quick Action tile collection.
/// The Func delegates are injected by DashboardViewModel so this class
/// remains decoupled from specific module types and easy to test.
/// </summary>
public static class QuickActionPresets
{
    /// <summary>
    /// Returns the Quick Action tiles in display order, filtered by current platform.
    /// On non-Windows, the "Remove Windows bloat" tile is excluded (the underlying
    /// BloatwareRemoval module is Windows-only).
    /// </summary>
    public static IReadOnlyList<QuickActionTileVM> Default(
        Func<Task> quickCleanup,
        Func<Task> optimizeRam,
        Func<Task> removeBloat)
    {
        var tiles = new List<QuickActionTileVM>
        {
            new QuickActionTileVM(
                id: "quick-cleanup",
                label: LocalizationService.Get("quickaction.quickcleanup.label"),
                subLabel: LocalizationService.Get("quickaction.quickcleanup.sublabel"),
                iconGlyph: "\U0001F9F9",
                accentToken: "AccentTealBrush",
                execute: quickCleanup),

            new QuickActionTileVM(
                id: "optimize-ram",
                label: LocalizationService.Get("quickaction.optimizeram.label"),
                subLabel: LocalizationService.Get("quickaction.optimizeram.sublabel"),
                iconGlyph: "⚡",
                accentToken: "AccentPurpleBrush",
                execute: optimizeRam),
        };

        if (OperatingSystem.IsWindows())
        {
            tiles.Add(new QuickActionTileVM(
                id: "remove-bloat",
                label: LocalizationService.Get("quickaction.removebloat.label"),
                subLabel: LocalizationService.Get("quickaction.removebloat.sublabel"),
                iconGlyph: "\U0001F5D1",
                accentToken: "AccentAmberBrush",
                execute: removeBloat));
        }

        return tiles;
    }
}
