namespace AuraCore.Application.Interfaces.Platform;

/// <summary>
/// Phase 6.17: pre-flight check before invoking a privileged action.
/// Implemented by the UI shell; module code calls TryGuardAsync before
/// kicking off any operation that requires the privilege helper.
/// On Windows, the implementation short-circuits to true (UAC handles
/// elevation per-process). On Linux/macOS, the implementation surfaces
/// an actionable modal explaining why the helper is needed and how to
/// install it; returns false so the caller short-circuits to
/// OperationResult.Skipped.
/// </summary>
public interface IPrivilegedActionGuard
{
    Task<bool> TryGuardAsync(
        string actionDescription,
        string? remediationCommandOverride = null,
        CancellationToken ct = default);
}
