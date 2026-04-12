namespace AuraCore.Module.DefaultsOptimizer.Models;

public sealed record DefaultsReport(
    IReadOnlyList<DefaultsTweak> Tweaks,
    int TotalCount,
    int AppliedCount,
    int PendingCount,
    bool IsAvailable)
{
    public static DefaultsReport None() => new(Array.Empty<DefaultsTweak>(), 0, 0, 0, false);
}
