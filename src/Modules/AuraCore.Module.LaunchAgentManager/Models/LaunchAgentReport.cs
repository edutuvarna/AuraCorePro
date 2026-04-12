namespace AuraCore.Module.LaunchAgentManager.Models;

public sealed record LaunchAgentReport(
    IReadOnlyList<LaunchAgentInfo> Agents,
    int TotalCount,
    int LoadedCount,
    int BloatwareCount,
    bool IsAvailable)
{
    public static LaunchAgentReport None() => new(Array.Empty<LaunchAgentInfo>(), 0, 0, 0, false);
}
