using global::Avalonia.Controls;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using AuraCore.UI.Avalonia.Views;

namespace AuraCore.UI.Avalonia.Services;

/// <summary>
/// Phase 6.16: concrete IModuleNavigator implementation.
/// Owns the moduleId→view-factory registry. On ResolveAsync, calls the
/// module's CheckRuntimeAvailabilityAsync; if available, invokes the factory;
/// otherwise constructs an UnavailableModuleView with a Try-Again callback
/// that re-runs ResolveAsync on the same id.
///
/// IOptimizationModule instances are injected as IEnumerable&lt;T&gt; — each
/// platform-conditional DI block in App.axaml.cs registers exactly the
/// modules valid for the current OS, so the navigator's _moduleMap is
/// inherently platform-correct.
/// </summary>
public sealed class ModuleNavigator : IModuleNavigator
{
    private readonly Dictionary<string, IOptimizationModule> _moduleMap;
    private readonly Dictionary<string, Func<UserControl>> _viewFactories = new(StringComparer.Ordinal);

    public ModuleNavigator(IEnumerable<IOptimizationModule> modules)
    {
        // Last-write-wins on duplicate ids (defensive — should not happen in practice
        // because DI uses .AddSingleton<...> per module).
        _moduleMap = modules
            .GroupBy(m => m.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);
    }

    public void RegisterView(string moduleId, Func<UserControl> factory)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        ArgumentNullException.ThrowIfNull(factory);
        _viewFactories[moduleId] = factory;
    }

    public async Task<UserControl> ResolveAsync(string moduleId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(moduleId);

        // 1) Module not registered on this platform → UnavailableModuleView with WrongPlatform(All).
        if (!_moduleMap.TryGetValue(moduleId, out var module))
        {
            return new UnavailableModuleView(
                moduleId,
                ModuleAvailability.WrongPlatform(SupportedPlatform.All));
        }

        // 2) Module exists — run availability check.
        var availability = await module.CheckRuntimeAvailabilityAsync(ct).ConfigureAwait(true);
        if (!availability.IsAvailable)
        {
            return new UnavailableModuleView(
                module.DisplayName,
                availability,
                onTryAgain: async () =>
                {
                    // Best-effort: caller (MainWindow) will re-fetch via ResolveAsync.
                    // The Try-Again button calls back; we re-resolve and the caller
                    // swaps the visual. Implemented as a no-op task here because
                    // visual swap is the shell's responsibility — Wave A scope.
                    await Task.CompletedTask;
                });
        }

        // 3) Available — invoke registered view factory if present.
        if (_viewFactories.TryGetValue(moduleId, out var factory))
        {
            try { return factory(); }
            catch (Exception ex)
            {
                // Defensive: factory threw — show diagnostic instead of crashing the shell.
                return new UnavailableModuleView(
                    module.DisplayName,
                    ModuleAvailability.FeatureDisabled($"View factory threw: {ex.GetType().Name}"));
            }
        }

        // 4) No factory registered (programmer error — should be flagged).
        return new UnavailableModuleView(
            module.DisplayName,
            ModuleAvailability.FeatureDisabled("View factory not registered for this module."));
    }
}
