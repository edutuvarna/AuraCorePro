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

    public async Task<UserControl> ResolveAsync(
        string moduleId,
        Func<string, Task>? onRetryRequested = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(moduleId);

        Func<Task>? onTryAgain = onRetryRequested is null
            ? null
            : () => onRetryRequested(moduleId);

        bool hasFactory = _viewFactories.TryGetValue(moduleId, out var factory);
        bool hasModule  = _moduleMap.TryGetValue(moduleId, out var module);

        // 1) No factory registered → no shell-level rendering possible. Show diagnostic.
        // Virtual ids (dashboard/settings/ai-features) DO have factories even though they
        // are not IOptimizationModule services — they fall through to step 3.
        if (!hasFactory)
        {
            return new UnavailableModuleView(
                module?.DisplayName ?? moduleId,
                ModuleAvailability.WrongPlatform(SupportedPlatform.All),
                onTryAgain);
        }

        // 2) Module-backed factory → run availability check first; render UnavailableModuleView on failure.
        if (hasModule)
        {
            var availability = await module!.CheckRuntimeAvailabilityAsync(ct).ConfigureAwait(true);
            if (!availability.IsAvailable)
            {
                return new UnavailableModuleView(
                    module.DisplayName,
                    availability,
                    onTryAgain);
            }
        }

        // 3) Invoke the factory. Wrap in try/catch — a factory throw must not crash the shell.
        try { return factory!(); }
        catch (Exception ex)
        {
            return new UnavailableModuleView(
                module?.DisplayName ?? moduleId,
                ModuleAvailability.FeatureDisabled($"View factory threw: {ex.GetType().Name}: {ex.Message}"),
                onTryAgain);
        }
    }
}
