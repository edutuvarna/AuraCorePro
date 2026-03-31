using System.Diagnostics;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.Module.SystemdManager;

public class SystemdManagerModule : IOptimizationModule
{
    public string Id => "systemd-manager";
    public string DisplayName => "Systemd Manager";
    public OptimizationCategory Category => OptimizationCategory.SystemHealth;
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

public static class SystemdManagerRegistration
{
    public static void AddSystemdManagerModule(IServiceCollection sc)
        => sc.AddSingleton<IOptimizationModule, SystemdManagerModule>();
}
