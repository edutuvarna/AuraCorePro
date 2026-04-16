using AuraCore.Application.Interfaces.Modules;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.Module.DnsFlusher;
using AuraCore.Module.DockerCleaner;
using AuraCore.Module.GrubManager;
using AuraCore.Module.JournalCleaner;
using AuraCore.Module.KernelCleaner;
using AuraCore.Module.LinuxAppInstaller;
using AuraCore.Module.MacAppInstaller;
using AuraCore.Module.PurgeableSpaceManager;
using AuraCore.Module.SnapFlatpakCleaner;
using AuraCore.Module.SpotlightManager;
using AuraCore.Module.XcodeCleaner;
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
        // Mirror App.axaml.cs Phase 4.3.2 block.
        // IShellCommandService is required since Phase 5.2.1.11a migration.
        sc.AddSingleton<IShellCommandService>(_ => new StubShellCommandService());
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

    /// <summary>
    /// Phase 4.4.2: PurgeableSpaceManagerViewModel must resolve via the same
    /// registration pattern App.axaml.cs uses — concrete module registered
    /// first, then aliased to IOptimizationModule, then the VM as transient.
    /// </summary>
    [Fact]
    public void PurgeableSpaceManagerViewModel_ResolvesFromContainer()
    {
        var sc = new ServiceCollection();
        // Mirror App.axaml.cs Phase 4.4.2 block
        sc.AddSingleton<PurgeableSpaceManagerModule>();
        sc.AddSingleton<IOptimizationModule>(sp => sp.GetRequiredService<PurgeableSpaceManagerModule>());
        sc.AddTransient<PurgeableSpaceManagerViewModel>();

        using var sp = sc.BuildServiceProvider();

        var module = sp.GetRequiredService<PurgeableSpaceManagerModule>();
        var asInterface = sp.GetRequiredService<IOptimizationModule>();
        Assert.Same(module, asInterface); // Single instance aliased

        var vm = sp.GetRequiredService<PurgeableSpaceManagerViewModel>();
        Assert.NotNull(vm);
        Assert.Null(vm.Report);
        Assert.Equal(0, vm.TotalCapacityBytes);
        Assert.Equal("1*,0*,0*", vm.ColumnDefinitions);
    }

    /// <summary>
    /// Phase 4.4.3: SpotlightManagerViewModel must resolve via the same
    /// registration pattern App.axaml.cs uses — concrete module registered
    /// first, then aliased to IOptimizationModule, then the VM as transient.
    /// </summary>
    [Fact]
    public void SpotlightManagerViewModel_ResolvesFromContainer()
    {
        var sc = new ServiceCollection();
        // Mirror App.axaml.cs Phase 4.4.3 block
        sc.AddSingleton<SpotlightManagerModule>();
        sc.AddSingleton<IOptimizationModule>(sp => sp.GetRequiredService<SpotlightManagerModule>());
        sc.AddTransient<SpotlightManagerViewModel>();

        using var sp = sc.BuildServiceProvider();

        var module = sp.GetRequiredService<SpotlightManagerModule>();
        var asInterface = sp.GetRequiredService<IOptimizationModule>();
        Assert.Same(module, asInterface); // Single instance aliased

        var vm = sp.GetRequiredService<SpotlightManagerViewModel>();
        Assert.NotNull(vm);
        Assert.Null(vm.Report);
        Assert.False(vm.MdutilAvailable);
        Assert.Null(vm.PendingRebuildVolume);
        Assert.False(vm.HasPendingRebuild);
        Assert.Empty(vm.VolumeItems);
    }

    /// <summary>
    /// Phase 4.4.4: XcodeCleanerViewModel must resolve via the same
    /// registration pattern App.axaml.cs uses — concrete module registered
    /// first, then aliased to IOptimizationModule, then the VM as transient.
    /// </summary>
    [Fact]
    public void XcodeCleanerViewModel_ResolvesFromContainer()
    {
        var sc = new ServiceCollection();
        // Mirror App.axaml.cs Phase 4.4.4 block
        sc.AddSingleton<XcodeCleanerModule>();
        sc.AddSingleton<IOptimizationModule>(sp => sp.GetRequiredService<XcodeCleanerModule>());
        sc.AddTransient<XcodeCleanerViewModel>();

        using var sp = sc.BuildServiceProvider();

        var module = sp.GetRequiredService<XcodeCleanerModule>();
        var asInterface = sp.GetRequiredService<IOptimizationModule>();
        Assert.Same(module, asInterface); // Single instance aliased

        var vm = sp.GetRequiredService<XcodeCleanerViewModel>();
        Assert.NotNull(vm);
        Assert.Null(vm.Report);
        Assert.False(vm.XcodeInstalled);
        Assert.False(vm.DangerAcknowledged);
        Assert.Empty(vm.SafeCategoriesItems);
        Assert.Empty(vm.GranularCategoriesItems);
        Assert.Empty(vm.DangerCategoriesItems);
    }

    /// <summary>
    /// Phase 4.4.5: MacAppInstallerViewModel must resolve via the same
    /// registration pattern App.axaml.cs uses — concrete module registered
    /// first, then aliased to IOptimizationModule, then the VM as transient.
    /// </summary>
    [Fact]
    public void MacAppInstallerViewModel_ResolvesFromContainer()
    {
        var sc = new ServiceCollection();
        // Mirror App.axaml.cs Phase 4.4.5 block
        sc.AddSingleton<MacAppInstallerModule>();
        sc.AddSingleton<IOptimizationModule>(sp => sp.GetRequiredService<MacAppInstallerModule>());
        sc.AddTransient<MacAppInstallerViewModel>();

        using var sp = sc.BuildServiceProvider();

        var module = sp.GetRequiredService<MacAppInstallerModule>();
        var asInterface = sp.GetRequiredService<IOptimizationModule>();
        Assert.Same(module, asInterface); // Single instance aliased

        var vm = sp.GetRequiredService<MacAppInstallerViewModel>();
        Assert.NotNull(vm);
        Assert.NotEmpty(vm.AllBundles);
        Assert.False(vm.HasSelection);
    }
}

/// <summary>
/// Minimal hand-rolled stub for IShellCommandService used in DI smoke tests.
/// Returns HelperMissing for every call — sufficient to prove the container
/// resolves SnapFlatpakCleanerModule without invoking any real privilege path.
/// </summary>
file sealed class StubShellCommandService : IShellCommandService
{
    public Task<ShellResult> RunPrivilegedAsync(PrivilegedCommand command, CancellationToken ct = default)
        => Task.FromResult(new ShellResult(
            Success: false,
            ExitCode: -1,
            Stdout: string.Empty,
            Stderr: string.Empty,
            AuthResult: PrivilegeAuthResult.HelperMissing));
}
