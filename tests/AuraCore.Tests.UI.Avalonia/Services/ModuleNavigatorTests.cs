using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using AuraCore.UI.Avalonia.Services;
using AuraCore.UI.Avalonia.Views;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Services;

public class ModuleNavigatorTests
{
    private sealed class StubModule : IOptimizationModule
    {
        public string Id { get; init; } = "stub";
        public string DisplayName { get; init; } = "Stub";
        public OptimizationCategory Category => OptimizationCategory.SystemHealth;
        public RiskLevel Risk => RiskLevel.None;
        public SupportedPlatform Platform { get; init; } = SupportedPlatform.All;
        public Func<ModuleAvailability>? AvailabilityFactory { get; init; }

        public Task<ScanResult> ScanAsync(ScanOptions o, CancellationToken ct = default)
            => Task.FromResult(new ScanResult(Id, true, 0, 0));
        public Task<OptimizationResult> OptimizeAsync(OptimizationPlan p, IProgress<TaskProgress>? pr = null, CancellationToken ct = default)
            => Task.FromResult(new OptimizationResult(Id, "op", true, 0, 0, TimeSpan.Zero));
        public Task<bool> CanRollbackAsync(string id, CancellationToken ct = default) => Task.FromResult(false);
        public Task RollbackAsync(string id, CancellationToken ct = default) => Task.CompletedTask;

        // Override the default availability check when the test wants a specific result.
        public Task<ModuleAvailability> CheckRuntimeAvailabilityAsync(CancellationToken ct = default)
            => Task.FromResult(AvailabilityFactory?.Invoke()
                ?? (Platform switch
                {
                    SupportedPlatform.Windows when !OperatingSystem.IsWindows() => ModuleAvailability.WrongPlatform(SupportedPlatform.Windows),
                    SupportedPlatform.Linux   when !OperatingSystem.IsLinux()   => ModuleAvailability.WrongPlatform(SupportedPlatform.Linux),
                    SupportedPlatform.MacOS   when !OperatingSystem.IsMacOS()   => ModuleAvailability.WrongPlatform(SupportedPlatform.MacOS),
                    _ => ModuleAvailability.Available,
                }));
    }

    [AvaloniaFact]
    public async Task ResolveAsync_AvailableModule_ReturnsRegisteredFactoryView()
    {
        var module = new StubModule { Id = "m1", DisplayName = "M1", Platform = SupportedPlatform.All };
        var nav = new ModuleNavigator(new[] { module });
        var sentinel = new UserControl { Tag = "real-view" };
        nav.RegisterView("m1", () => sentinel);

        var result = await nav.ResolveAsync("m1");

        Assert.Same(sentinel, result);
    }

    [AvaloniaFact]
    public async Task ResolveAsync_HelperNotRunning_ReturnsUnavailableModuleView()
    {
        var module = new StubModule
        {
            Id = "m2", DisplayName = "M2",
            AvailabilityFactory = () => ModuleAvailability.HelperNotRunning("sudo bash install.sh"),
        };
        var nav = new ModuleNavigator(new[] { module });
        nav.RegisterView("m2", () => new UserControl { Tag = "should-not-be-shown" });

        var result = await nav.ResolveAsync("m2");

        Assert.IsType<UnavailableModuleView>(result);
    }

    [AvaloniaFact]
    public async Task ResolveAsync_UnknownModuleId_ReturnsUnavailableModuleView()
    {
        var nav = new ModuleNavigator(Array.Empty<IOptimizationModule>());

        var result = await nav.ResolveAsync("nonexistent");

        Assert.IsType<UnavailableModuleView>(result);
    }

    [AvaloniaFact]
    public async Task ResolveAsync_ModuleAvailableButNoFactory_ReturnsUnavailableModuleView()
    {
        var module = new StubModule { Id = "m3", DisplayName = "M3", Platform = SupportedPlatform.All };
        var nav = new ModuleNavigator(new[] { module });
        // No RegisterView call.

        var result = await nav.ResolveAsync("m3");

        Assert.IsType<UnavailableModuleView>(result);
    }

    [AvaloniaFact]
    public async Task ResolveAsync_FactoryThrows_ReturnsUnavailableModuleView_DoesNotPropagate()
    {
        var module = new StubModule { Id = "m4", DisplayName = "M4", Platform = SupportedPlatform.All };
        var nav = new ModuleNavigator(new[] { module });
        nav.RegisterView("m4", () => throw new InvalidOperationException("boom"));

        var result = await nav.ResolveAsync("m4");

        Assert.IsType<UnavailableModuleView>(result);
    }
}
