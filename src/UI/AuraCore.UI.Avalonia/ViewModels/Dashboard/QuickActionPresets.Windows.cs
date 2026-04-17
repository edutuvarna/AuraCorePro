using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuraCore.UI.Avalonia.ViewModels.Dashboard;

/// <summary>
/// Factory for the Windows Quick Action tile collection shown on the Dashboard.
/// The three Func delegates are injected by DashboardViewModel so this class
/// remains decoupled from specific module types and easy to test.
/// </summary>
public static class QuickActionPresets
{
    /// <summary>
    /// Returns the standard Windows Quick Action tiles in display order.
    /// </summary>
    public static IReadOnlyList<QuickActionTileVM> Windows(
        Func<Task> quickCleanup,
        Func<Task> optimizeRam,
        Func<Task> removeBloat)
    {
        return new[]
        {
            new QuickActionTileVM(
                id: "quick-cleanup",
                label: LocalizationService.Get("quickaction.quickcleanup.label"),
                subLabel: LocalizationService.Get("quickaction.quickcleanup.sublabel"),
                iconGlyph: "\U0001F9F9",   // 🧹 broom
                accentToken: "AccentTealBrush",
                execute: quickCleanup),

            new QuickActionTileVM(
                id: "optimize-ram",
                label: LocalizationService.Get("quickaction.optimizeram.label"),
                subLabel: LocalizationService.Get("quickaction.optimizeram.sublabel"),
                iconGlyph: "\u26A1",       // ⚡ lightning
                accentToken: "AccentPurpleBrush",
                execute: optimizeRam),

            new QuickActionTileVM(
                id: "remove-bloat",
                label: LocalizationService.Get("quickaction.removebloat.label"),
                subLabel: LocalizationService.Get("quickaction.removebloat.sublabel"),
                iconGlyph: "\U0001F5D1",   // 🗑 wastebasket
                accentToken: "AccentAmberBrush",
                execute: removeBloat),
        };
    }
}
