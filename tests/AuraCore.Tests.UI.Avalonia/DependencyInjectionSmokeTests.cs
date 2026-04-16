using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.DnsFlusher;
using AuraCore.Module.DockerCleaner;
using AuraCore.Module.GrubManager;
using AuraCore.Module.JournalCleaner;
using AuraCore.Module.KernelCleaner;
using AuraCore.Module.LinuxAppInstaller;
using AuraCore.Module.SnapFlatpakCleaner;
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

    /// <summary>
    /// Phase 4.3.2: SnapFlatpakCleanerViewModel must resolve via the same
    /// registration pattern App.axaml.cs uses — concrete module registered
    /// first, then aliased to IOptimizationModule, then the VM as transient.
    /// </summary>
    [Fact]
    public void SnapFlatpakCleanerViewModel_ResolvesFromContainer()
    {
        var sc = new ServiceCollection();
        // Mirror App.axaml.cs Phase 4.3.2 block
        sc.AddSingleton<SnapFlatpakCleanerModule>();
        sc.AddSingleton<IOptimizationModule>(sp => sp.GetRequiredService<SnapFlatpakCleanerModule>());
        sc.AddTransient<SnapFlatpakCleanerViewModel>();

        using var sp = sc.BuildServiceProvider();

        var module = sp.GetRequiredService<SnapFlatpakCleanerModule>();
        var asInterface = sp.GetRequiredService<IOptimizationModule>();
        Assert.Same(module, asInterface); // Single instance aliased

        var vm = sp.GetRequiredService<SnapFlatpakCleanerViewModel>();
        Assert.NotNull(vm);
        Assert.Equal(0, vm.SnapDisabledCount);
        Assert.Equal(0, vm.FlatpakUnusedCount);
    }

    /// <summary>
    /// Phase 4.3.3: DockerCleanerViewModel must resolve via the same
    /// registration pattern App.axaml.cs uses — concrete module registered
    /// first, then aliased to IOptimizationModule, then the VM as transient.
    /// </summary>
    [Fact]
    public void DockerCleanerViewModel_ResolvesFromContainer()
    {
        var sc = new ServiceCollection();
        // Mirror App.axaml.cs Phase 4.3.3 block
        sc.AddSingleton<DockerCleanerModule>();
        sc.AddSingleton<IOptimizationModule>(sp => sp.GetRequiredService<DockerCleanerModule>());
        sc.AddTransient<DockerCleanerViewModel>();

        using var sp = sc.BuildServiceProvider();

        var module = sp.GetRequiredService<DockerCleanerModule>();
        var asInterface = sp.GetRequiredService<IOptimizationModule>();
        Assert.Same(module, asInterface); // Single instance aliased

        var vm = sp.GetRequiredService<DockerCleanerViewModel>();
        Assert.NotNull(vm);
        Assert.Null(vm.Report);
        Assert.False(vm.DockerAvailable);
        Assert.False(vm.VolumeRiskAcknowledged);
    }

    /// <summary>
    /// Phase 4.3.4: KernelCleanerViewModel must resolve via the same
    /// registration pattern App.axaml.cs uses — concrete module registered
    /// first, then aliased to IOptimizationModule, then the VM as transient.
    /// </summary>
    [Fact]
    public void KernelCleanerViewModel_ResolvesFromContainer()
    {
        var sc = new ServiceCollection();
        // Mirror App.axaml.cs Phase 4.3.4 block
        sc.AddSingleton<KernelCleanerModule>();
        sc.AddSingleton<IOptimizationModule>(sp => sp.GetRequiredService<KernelCleanerModule>());
        sc.AddTransient<KernelCleanerViewModel>();

        using var sp = sc.BuildServiceProvider();

        var module = sp.GetRequiredService<KernelCleanerModule>();
        var asInterface = sp.GetRequiredService<IOptimizationModule>();
        Assert.Same(module, asInterface); // Single instance aliased

        var vm = sp.GetRequiredService<KernelCleanerViewModel>();
        Assert.NotNull(vm);
        Assert.Null(vm.Report);
        Assert.False(vm.PackageManagerAvailable);
        Assert.False(vm.DangerAcknowledged);
        Assert.Empty(vm.KernelItems);
    }

    /// <summary>
    /// Phase 4.3.5: LinuxAppInstallerViewModel must resolve via the same
    /// registration pattern App.axaml.cs uses — concrete module registered
    /// first, then aliased to IOptimizationModule, then the VM as transient.
    /// </summary>
    [Fact]
    public void LinuxAppInstallerViewModel_ResolvesFromContainer()
    {
        var sc = new ServiceCollection();
        // Mirror App.axaml.cs Phase 4.3.5 block
        sc.AddSingleton<LinuxAppInstallerModule>();
        sc.AddSingleton<IOptimizationModule>(sp => sp.GetRequiredService<LinuxAppInstallerModule>());
        sc.AddTransient<LinuxAppInstallerViewModel>();

        using var sp = sc.BuildServiceProvider();

        var module = sp.GetRequiredService<LinuxAppInstallerModule>();
        var asInterface = sp.GetRequiredService<IOptimizationModule>();
        Assert.Same(module, asInterface); // Single instance aliased

        var vm = sp.GetRequiredService<LinuxAppInstallerViewModel>();
        Assert.NotNull(vm);
        Assert.NotEmpty(vm.AllBundles);
        Assert.False(vm.HasSelection);
    }

    /// <summary>
    /// Phase 4.3.6: GrubManagerViewModel must resolve via the same
    /// registration pattern App.axaml.cs uses — concrete module registered
    /// first, then aliased to IOptimizationModule, then the VM as transient.
    /// </summary>
    [Fact]
    public void GrubManagerViewModel_ResolvesFromContainer()
    {
        var sc = new ServiceCollection();
        // Mirror App.axaml.cs Phase 4.3.6 block
        sc.AddSingleton<GrubManagerModule>();
        sc.AddSingleton<IOptimizationModule>(sp => sp.GetRequiredService<GrubManagerModule>());
        sc.AddTransient<GrubManagerViewModel>();

        using var sp = sc.BuildServiceProvider();

        var module = sp.GetRequiredService<GrubManagerModule>();
        var asInterface = sp.GetRequiredService<IOptimizationModule>();
        Assert.Same(module, asInterface); // Single instance aliased

        var vm = sp.GetRequiredService<GrubManagerViewModel>();
        Assert.NotNull(vm);
        Assert.False(vm.HasPendingChanges);
        Assert.False(vm.HasBackup);
        Assert.False(vm.BackupAcknowledged);
    }

    /// <summary>
    /// Phase 4.4.1: DnsFlusherViewModel must resolve via the same
    /// registration pattern App.axaml.cs uses — concrete module registered
    /// first, then aliased to IOptimizationModule, then the VM as transient.
    /// </summary>
    [Fact]
    public void DnsFlusherViewModel_ResolvesFromContainer()
    {
        var sc = new ServiceCollection();
        // Mirror App.axaml.cs Phase 4.4.1 block
        sc.AddSingleton<DnsFlusherModule>();
        sc.AddSingleton<IOptimizationModule>(sp => sp.GetRequiredService<DnsFlusherModule>());
        sc.AddTransient<DnsFlusherViewModel>();

        using var sp = sc.BuildServiceProvider();

        var module = sp.GetRequiredService<DnsFlusherModule>();
        var asInterface = sp.GetRequiredService<IOptimizationModule>();
        Assert.Same(module, asInterface); // Single instance aliased

        var vm = sp.GetRequiredService<DnsFlusherViewModel>();
        Assert.NotNull(vm);
        Assert.Null(vm.Report);
        Assert.False(vm.DscacheutilAvailable);
        Assert.Null(vm.LastFlush);
    }
}
