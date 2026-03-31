using System.Diagnostics;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.Module.NetworkMonitor;

public class NetworkMonitorModule : IOptimizationModule
{
    public string Id => "network-monitor";
    public string DisplayName => "Network Monitor";
    public OptimizationCategory Category => OptimizationCategory.NetworkOptimization;
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

public static class NetworkMonitorRegistration
{
    public static void AddNetworkMonitorModule(IServiceCollection sc)
        => sc.AddSingleton<IOptimizationModule, NetworkMonitorModule>();
}
