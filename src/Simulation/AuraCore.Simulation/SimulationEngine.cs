using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Application.Interfaces.Simulation;
using AuraCore.Domain.Enums;

namespace AuraCore.Simulation;

public sealed class SimulationEngine : ISimulationEngine
{
    private readonly IEnumerable<IOptimizationModule> _modules;
    public SimulationEngine(IEnumerable<IOptimizationModule> modules) => _modules = modules;

    public Task<SimulationResult> SimulateAsync(IReadOnlyList<string> moduleIds, ScanOptions options,
        IProgress<TaskProgress>? progress = null, CancellationToken ct = default)
        => Task.FromResult(new SimulationResult(Guid.NewGuid().ToString(), Array.Empty<SimulationEntry>(), 0, RiskLevel.None, TimeSpan.Zero));
}

public static class SimulationRegistration
{
    public static IServiceCollection AddSimulationEngine(this IServiceCollection services) => services.AddSingleton<ISimulationEngine, SimulationEngine>();
}
