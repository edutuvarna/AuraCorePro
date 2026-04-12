namespace AuraCore.Module.SystemdManager.Models;

public sealed record SystemdReport(
    IReadOnlyList<SystemdServiceInfo> Services,
    int TotalCount,
    int RunningCount,
    int FailedCount,
    int RecommendationCount,
    bool IsAvailable)
{
    public static SystemdReport None() => new(Array.Empty<SystemdServiceInfo>(), 0, 0, 0, 0, false);
}
