using Avalonia.Controls;

namespace AuraCore.UI.Avalonia.Services;

/// <summary>
/// Phase 6.16: UI-layer module dispatcher with availability gating.
/// Replaces the hardcoded MainWindow.SetActiveContent switch.
/// Distinct from Application-layer INavigationService (event-based deep-link bus).
/// </summary>
public interface IModuleNavigator
{
    /// <summary>Register a view factory for a module id (called once at app startup, per platform).</summary>
    void RegisterView(string moduleId, Func<UserControl> factory);

    /// <summary>
    /// Resolve a module id to its rendered UserControl. Internally:
    ///   1) Looks up the IOptimizationModule by id.
    ///   2) Calls module.CheckRuntimeAvailabilityAsync().
    ///   3) On Available -> invokes the registered view factory.
    ///   4) Otherwise -> returns a configured UnavailableModuleView with retry callback.
    /// </summary>
    Task<UserControl> ResolveAsync(string moduleId, CancellationToken ct = default);
}
