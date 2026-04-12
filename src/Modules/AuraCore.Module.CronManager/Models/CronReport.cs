namespace AuraCore.Module.CronManager.Models;

public sealed record CronReport(
    IReadOnlyList<CronJobInfo> UserJobs,
    IReadOnlyList<CronJobInfo> SystemJobs,
    int TotalJobs,
    int DeadJobCount,      // command doesn't exist
    int InvalidJobCount,   // invalid syntax
    bool IsAvailable)
{
    public static CronReport None() => new(
        Array.Empty<CronJobInfo>(), Array.Empty<CronJobInfo>(), 0, 0, 0, false);
}
