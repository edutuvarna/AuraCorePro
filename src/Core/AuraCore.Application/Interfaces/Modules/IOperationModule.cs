using AuraCore.Application;
using AuraCore.Application.Interfaces.Platform;

namespace AuraCore.Application.Interfaces.Modules;

/// <summary>
/// Phase 6.17: optional extension contract for modules that need to
/// communicate rich operation status (Success / Skipped / Failed +
/// reason + remediation) to the UI. Modules opt in by implementing
/// this on top of <see cref="IOptimizationModule"/>.
///
/// The default <see cref="IOptimizationModule.OptimizeAsync"/> shape
/// continues to work for the other 40+ modules; only the 6 highest-pain
/// modules adopt RunOperationAsync in Phase 6.17 (RamOptimizer,
/// JunkCleaner, SystemdManager, SwapOptimizer, PackageCleaner,
/// JournalCleaner). Incremental opt-in migration to Phase 6.18+.
/// </summary>
public interface IOperationModule : IOptimizationModule
{
    Task<OperationResult> RunOperationAsync(
        OptimizationPlan plan,
        IPrivilegedActionGuard guard,
        IProgress<TaskProgress>? progress = null,
        CancellationToken ct = default);
}
