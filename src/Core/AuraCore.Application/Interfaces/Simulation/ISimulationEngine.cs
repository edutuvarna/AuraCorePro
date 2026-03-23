using AuraCore.Domain.Enums;

namespace AuraCore.Application.Interfaces.Simulation;

public interface ISimulationEngine
{
    Task<SimulationResult> SimulateAsync(
        IReadOnlyList<string> moduleIds,
        ScanOptions options,
        IProgress<TaskProgress>? progress = null,
        CancellationToken ct = default);
}

public sealed record SimulationResult(
    string SimulationId,
    IReadOnlyList<SimulationEntry> Changes,
    long EstimatedBytesFreed,
    RiskLevel HighestRisk,
    TimeSpan Duration);

public sealed record SimulationEntry(
    string ModuleId,
    string Category,
    string Target,
    string Description,
    RiskLevel Risk,
    bool Reversible);
