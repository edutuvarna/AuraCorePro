namespace AuraCore.Application.Interfaces.Engines;

public interface ITaskSchedulerEngine
{
    Task<string> ScheduleAsync(ScheduleDefinition definition, CancellationToken ct = default);
    Task CancelAsync(string taskId, CancellationToken ct = default);
}

public sealed record ScheduleDefinition(
    string ModuleId,
    string CronExpression,
    ScanOptions Options);
