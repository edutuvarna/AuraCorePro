using System.Diagnostics;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.Module.WakeOnLan;

public class WakeOnLanModule : IOptimizationModule
{
    public string Id => "wake-on-lan";
    public string DisplayName => "Wake-on-LAN";
    public OptimizationCategory Category => OptimizationCategory.NetworkTools;
    public RiskLevel Risk => RiskLevel.Low;
    public SupportedPlatform Platform => SupportedPlatform.Windows;

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

public static class WakeOnLanRegistration
{
    public static void AddWakeOnLanModule(IServiceCollection sc)
        => sc.AddSingleton<IOptimizationModule, WakeOnLanModule>();
}
