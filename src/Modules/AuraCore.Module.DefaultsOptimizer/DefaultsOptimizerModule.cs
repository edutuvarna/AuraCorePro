using System.Diagnostics;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.Module.DefaultsOptimizer;

public class DefaultsOptimizerModule : IOptimizationModule
{
    public string Id => "defaults-optimizer";
    public string DisplayName => "Defaults Optimizer";
    public OptimizationCategory Category => OptimizationCategory.ShellCustomization;
    public RiskLevel Risk => RiskLevel.Medium;
    public SupportedPlatform Platform => SupportedPlatform.MacOS;

    public Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
        => Task.FromResult(new ScanResult(Id, true, 0, 0));

    public Task<OptimizationResult> OptimizeAsync(
        OptimizationPlan plan, IProgress<TaskProgress>? progress = null, CancellationToken ct = default)
        => Task.FromResult(new OptimizationResult(Id, Guid.NewGuid().ToString()[..8], true, 0, 0, TimeSpan.Zero));

    public Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default)
        => Task.FromResult(false);

    public Task RollbackAsync(string operationId, CancellationToken ct = default)
        => Task.CompletedTask;
}

public static class DefaultsOptimizerRegistration
{
    public static void AddDefaultsOptimizerModule(IServiceCollection sc)
        => sc.AddSingleton<IOptimizationModule, DefaultsOptimizerModule>();
}
