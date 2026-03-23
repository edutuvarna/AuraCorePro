using AuraCore.Domain.Enums;

namespace AuraCore.Application.Interfaces.Modules;

public interface IOptimizationModule
{
    string Id { get; }
    string DisplayName { get; }
    OptimizationCategory Category { get; }
    RiskLevel Risk { get; }

    Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default);
    Task<OptimizationResult> OptimizeAsync(
        OptimizationPlan plan,
        IProgress<TaskProgress>? progress = null,
        CancellationToken ct = default);
    Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default);
    Task RollbackAsync(string operationId, CancellationToken ct = default);
}
