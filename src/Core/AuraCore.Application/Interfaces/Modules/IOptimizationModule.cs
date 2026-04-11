using AuraCore.Domain.Enums;

namespace AuraCore.Application.Interfaces.Modules;

public interface IOptimizationModule
{
    string Id { get; }
    string DisplayName { get; }
    OptimizationCategory Category { get; }
    RiskLevel Risk { get; }

    /// <summary>
    /// Which platform(s) this module supports.
    /// Default: Windows (all existing modules are Windows-only).
    /// Override to SupportedPlatform.All for cross-platform modules (e.g. HostsEditor).
    /// </summary>
    SupportedPlatform Platform => SupportedPlatform.Windows;

    /// <summary>
    /// Whether the module should be hidden from default UI views.
    /// Advanced modules are shown only in the "Advanced" category/filter.
    /// Default: false (module is visible).
    /// </summary>
    bool IsAdvanced => false;

    Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default);
    Task<OptimizationResult> OptimizeAsync(
        OptimizationPlan plan,
        IProgress<TaskProgress>? progress = null,
        CancellationToken ct = default);
    Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default);
    Task RollbackAsync(string operationId, CancellationToken ct = default);
}
