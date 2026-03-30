using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.Module.FirewallRules;

public class FirewallRulesModule : IOptimizationModule
{
    public string Id => "firewall-rules";
    public string DisplayName => "Firewall Rules";
    public OptimizationCategory Category => OptimizationCategory.NetworkTools;
    public RiskLevel Risk => RiskLevel.Medium;
    public SupportedPlatform Platform => SupportedPlatform.Windows;

    public Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        // TODO: Implement scan for Firewall Rules
        return Task.FromResult(new ScanResult(Id, true, 0, 0));
    }

    public Task<OptimizationResult> OptimizeAsync(
        OptimizationPlan plan,
        IProgress<TaskProgress>? progress = null,
        CancellationToken ct = default)
    {
        // TODO: Implement optimization for Firewall Rules
        return Task.FromResult(new OptimizationResult(Id, Guid.NewGuid().ToString()[..8], true, 0, 0, TimeSpan.Zero));
    }

    public Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default)
        => Task.FromResult(false);

    public Task RollbackAsync(string operationId, CancellationToken ct = default)
        => Task.CompletedTask;
}

public static class FirewallRulesRegistration
{
    public static void AddFirewallRulesModule(IServiceCollection sc)
        => sc.AddSingleton<IOptimizationModule, FirewallRulesModule>();
}
