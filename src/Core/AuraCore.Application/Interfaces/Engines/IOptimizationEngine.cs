namespace AuraCore.Application.Interfaces.Engines;

public interface IOptimizationEngine
{
    Task<IReadOnlyList<ScanResult>> ScanAllAsync(ScanOptions options, CancellationToken ct = default);
    Task<OptimizationResult> OptimizeAsync(
        OptimizationPlan plan,
        IProgress<TaskProgress>? progress = null,
        CancellationToken ct = default);
}
