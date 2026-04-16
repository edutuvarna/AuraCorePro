using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.JournalCleaner;
using AuraCore.UI.Avalonia;
using AuraCore.UI.Avalonia.Services.AI;
using AuraCore.UI.Avalonia.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia;

public class DependencyInjectionSmokeTests
{
    [Fact]
    public void AllPhase3Services_ResolveFromContainer()
    {
        // Emulate App.axaml.cs registrations
        var sc = new ServiceCollection();
        sc.AddSingleton<IModelCatalog, ModelCatalog>();
        sc.AddSingleton<IInstalledModelStore>(sp => new InstalledModelStore(sp.GetRequiredService<IModelCatalog>()));
        sc.AddSingleton<AppSettings>(_ => new AppSettings());
        sc.AddSingleton<ICortexAmbientService, CortexAmbientService>();
        sc.AddSingleton<ITierService, TierService>();
        sc.AddSingleton(new ModelDownloadSettings("https://test", Path.GetTempPath(), 30, 256, "Test/1.0"));
        sc.AddSingleton<System.Net.Http.HttpClient>(_ => new System.Net.Http.HttpClient());
        sc.AddTransient<IModelDownloadService, ModelDownloadService>();

        using var sp = sc.BuildServiceProvider();

        Assert.NotNull(sp.GetRequiredService<IModelCatalog>());
        Assert.NotNull(sp.GetRequiredService<IInstalledModelStore>());
        Assert.NotNull(sp.GetRequiredService<AppSettings>());
        Assert.NotNull(sp.GetRequiredService<ICortexAmbientService>());
        Assert.NotNull(sp.GetRequiredService<ITierService>());
        Assert.NotNull(sp.GetRequiredService<IModelDownloadService>());
    }

    /// <summary>
    /// Phase 4.3.1: JournalCleanerViewModel must resolve via the same registration
    /// pattern App.axaml.cs uses — concrete JournalCleanerModule registered first,
    /// then aliased to IOptimizationModule, then the VM as transient. Guards
    /// against accidental DI cycles or missing registrations.
    /// </summary>
    [Fact]
    public void JournalCleanerViewModel_ResolvesFromContainer()
    {
        var sc = new ServiceCollection();
        // Mirror App.axaml.cs Phase 4.3.1 block
        sc.AddSingleton<JournalCleanerModule>();
        sc.AddSingleton<IOptimizationModule>(sp => sp.GetRequiredService<JournalCleanerModule>());
        sc.AddTransient<JournalCleanerViewModel>();

        using var sp = sc.BuildServiceProvider();

        var module = sp.GetRequiredService<JournalCleanerModule>();
        var asInterface = sp.GetRequiredService<IOptimizationModule>();
        Assert.Same(module, asInterface); // Single instance aliased

        var vm = sp.GetRequiredService<JournalCleanerViewModel>();
        Assert.NotNull(vm);
        Assert.Null(vm.Report);
    }
}
