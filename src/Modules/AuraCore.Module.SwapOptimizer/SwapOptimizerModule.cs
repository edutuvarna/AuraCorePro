using System.Diagnostics;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.Module.SwapOptimizer;

public class SwapOptimizerModule : IOptimizationModule
{
    public string Id => "swap-optimizer";
    public string DisplayName => "Swap Optimizer";
    public OptimizationCategory Category => OptimizationCategory.MemoryOptimization;
    public RiskLevel Risk => RiskLevel.Medium;
    public SupportedPlatform Platform => SupportedPlatform.Linux;

    public Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        return Task.FromResult(new ScanResult(Id, true, 0, 0));
    }

    public Task<OptimizationResult> OptimizeAsync(
        OptimizationPlan plan,
        IProgress<TaskProgress>? progress = null,
        CancellationToken ct = default)
    {
        return Task.FromResult(new OptimizationResult(Id, Guid.NewGuid().ToString()[..8], true, 0, 0, TimeSpan.Zero));
    }

    public Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default)
        => Task.FromResult(false);

    public Task RollbackAsync(string operationId, CancellationToken ct = default)
        => Task.CompletedTask;
}

public static class SwapOptimizerRegistration
{
    public static void AddSwapOptimizerModule(IServiceCollection sc)
        => sc.AddSingleton<IOptimizationModule, SwapOptimizerModule>();
}
