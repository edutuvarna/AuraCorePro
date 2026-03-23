using AuraCore.Domain.Enums;

namespace AuraCore.Application;

public sealed record ScanResult(
    string ModuleId,
    bool Success,
    int ItemsFound,
    long EstimatedBytesFreed,
    string? BlockedReason = null)
{
    public static ScanResult Blocked(string reason) =>
        new("", false, 0, 0, reason);
}

public sealed record ScanOptions(
    bool DeepScan = false,
    IReadOnlyList<string>? IncludePaths = null);

public sealed record OptimizationPlan(
    string ModuleId,
    IReadOnlyList<string> SelectedItemIds);

public sealed record OptimizationResult(
    string ModuleId,
    string OperationId,
    bool Success,
    int ItemsProcessed,
    long BytesFreed,
    TimeSpan Duration);

public sealed record TaskProgress(
    string ModuleId,
    double Percentage,
    string StatusText);
