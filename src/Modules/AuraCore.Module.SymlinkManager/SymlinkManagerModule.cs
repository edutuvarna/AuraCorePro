using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.Module.SymlinkManager;

public class SymlinkManagerModule : IOptimizationModule
{
    public string Id => "symlink-manager";
    public string DisplayName => "Symlink Manager";
    public OptimizationCategory Category => OptimizationCategory.ShellCustomization;
    public RiskLevel Risk => RiskLevel.Medium;
    public SupportedPlatform Platform => SupportedPlatform.All;

    public Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        // TODO: Implement scan for Symlink Manager
        return Task.FromResult(new ScanResult(Id, true, 0, 0));
    }

    public Task<OptimizationResult> OptimizeAsync(
        OptimizationPlan plan,
        IProgress<TaskProgress>? progress = null,
        CancellationToken ct = default)
    {
        // TODO: Implement optimization for Symlink Manager
        return Task.FromResult(new OptimizationResult(Id, Guid.NewGuid().ToString()[..8], true, 0, 0, TimeSpan.Zero));
    }

    public Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default)
        => Task.FromResult(false);

    public Task RollbackAsync(string operationId, CancellationToken ct = default)
        => Task.CompletedTask;
}

public static class SymlinkManagerRegistration
{
    public static void AddSymlinkManagerModule(IServiceCollection sc)
        => sc.AddSingleton<IOptimizationModule, SymlinkManagerModule>();
}
