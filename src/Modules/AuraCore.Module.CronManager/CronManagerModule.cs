using System.Diagnostics;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.Module.CronManager;

public class CronManagerModule : IOptimizationModule
{
    public string Id => "cron-manager";
    public string DisplayName => "Cron Manager";
    public OptimizationCategory Category => OptimizationCategory.ShellCustomization;
    public RiskLevel Risk => RiskLevel.Low;
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

public static class CronManagerRegistration
{
    public static void AddCronManagerModule(IServiceCollection sc)
        => sc.AddSingleton<IOptimizationModule, CronManagerModule>();
}
