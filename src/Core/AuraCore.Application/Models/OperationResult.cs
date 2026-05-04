namespace AuraCore.Application;

public enum OperationStatus
{
    Success,
    Skipped,        // Operation skipped (precondition not met — typically helper missing)
    Failed,         // Operation attempted but errored
}

/// <summary>
/// Phase 6.17: rich-result return type for module operations that need to
/// communicate Success vs Skipped vs Failed (and why) to the UI. Replaces
/// the lossy <see cref="OptimizationResult"/> shape for modules where the
/// distinction matters to the user (e.g. "skipped because privilege helper
/// missing" vs "actually freed 2.3 GB").
/// </summary>
public sealed record OperationResult(
    OperationStatus Status,
    long BytesFreed,
    int ItemsAffected,
    string? Reason,
    string? RemediationCommand,
    TimeSpan Duration)
{
    public static OperationResult Success(long bytesFreed, int itemsAffected, TimeSpan duration) =>
        new(OperationStatus.Success, bytesFreed, itemsAffected, null, null, duration);

    public static OperationResult Skipped(string reason, string? remediationCommand = null) =>
        new(OperationStatus.Skipped, 0, 0, reason, remediationCommand, TimeSpan.Zero);

    public static OperationResult Failed(string reason, TimeSpan duration) =>
        new(OperationStatus.Failed, 0, 0, reason, null, duration);
}
