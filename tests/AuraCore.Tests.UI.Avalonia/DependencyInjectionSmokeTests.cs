using AuraCore.UI.Avalonia;
using AuraCore.UI.Avalonia.Services.AI;
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
}
