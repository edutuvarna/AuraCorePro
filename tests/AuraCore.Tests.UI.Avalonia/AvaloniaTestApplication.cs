using Avalonia;
using Avalonia.Themes.Fluent;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.BloatwareRemoval;
using AuraCore.Module.RamOptimizer;
using AuraCore.Module.SystemHealth;
using AuraCore.UI.Avalonia;
using AuraCore.UI.Avalonia.Converters;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.Tests.UI.Avalonia;

public class AvaloniaTestApplication : global::Avalonia.Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());

        // Register converters as static resources for UI tests.
        // Phase 5.3/5.4 narrow-mode + text-transform converters.
        Resources.Add("BoundsToIsNarrowModeConverter", new BoundsToIsNarrowModeConverter());
        Resources.Add("NarrowToColumnCountConverter", new NarrowToColumnCountConverter());
        Resources.Add("BoolToGridLengthConverter", new BoolToGridLengthConverter());
        Resources.Add("NarrowModeGridLengthChainConverter", new NarrowModeGridLengthChainConverter());
        Resources.Add("UppercaseTransformConverter", new UppercaseTransformConverter());

        // Phase debt-B2: minimal DI bootstrap so pilot views that call
        // App.Services.GetServices<IOptimizationModule>().OfType<T>() can resolve
        // in the headless Avalonia test harness.
        //
        // Only the 3 concrete modules needed by the pilot views are registered.
        // All three have parameterless constructors so no transitive dependencies.
        var services = new ServiceCollection();

        // SystemHealthView needs GetServices<IOptimizationModule>().OfType<SystemHealthModule>()
        services.AddSingleton<IOptimizationModule, SystemHealthModule>();

        // BloatwareRemovalView needs GetServices<IOptimizationModule>().OfType<BloatwareRemovalModule>()
        services.AddSingleton<IOptimizationModule, BloatwareRemovalModule>();

        // RamOptimizerView needs GetServices<IOptimizationModule>().OfType<RamOptimizerModule>()
        services.AddSingleton<IOptimizationModule, RamOptimizerModule>();

        App.Services = services.BuildServiceProvider();
    }
}
