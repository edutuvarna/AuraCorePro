using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Engines;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.Engine.Optimization;

public sealed class OptimizationEngine : IOptimizationEngine
{
    private readonly IEnumerable<IOptimizationModule> _modules;
    public OptimizationEngine(IEnumerable<IOptimizationModule> modules) => _modules = modules;

    public async Task<IReadOnlyList<ScanResult>> ScanAllAsync(ScanOptions options, CancellationToken ct = default)
    {
        var results = new List<ScanResult>();
        foreach (var m in _modules) results.Add(await m.ScanAsync(options, ct));
        return results;
    }

    public async Task<OptimizationResult> OptimizeAsync(OptimizationPlan plan, IProgress<TaskProgress>? progress = null, CancellationToken ct = default)
    {
        var module = _modules.FirstOrDefault(m => m.Id == plan.ModuleId)
            ?? throw new InvalidOperationException($"Module '{plan.ModuleId}' not found.");
        return await module.OptimizeAsync(plan, progress, ct);
    }
}

public static class OptimizationEngineRegistration
{
    public static IServiceCollection AddOptimizationEngine(this IServiceCollection services)
        => services.AddSingleton<IOptimizationEngine, OptimizationEngine>();
}
