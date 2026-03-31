using System.Diagnostics;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.Module.TimeMachineManager;

public class TimeMachineManagerModule : IOptimizationModule
{
    public string Id => "timemachine-manager";
    public string DisplayName => "Time Machine Manager";
    public OptimizationCategory Category => OptimizationCategory.SystemHealth;
    public RiskLevel Risk => RiskLevel.Low;
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

public static class TimeMachineManagerRegistration
{
    public static void AddTimeMachineManagerModule(IServiceCollection sc)
        => sc.AddSingleton<IOptimizationModule, TimeMachineManagerModule>();
}
