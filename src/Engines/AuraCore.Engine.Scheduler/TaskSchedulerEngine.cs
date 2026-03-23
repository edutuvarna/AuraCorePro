using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Engines;

namespace AuraCore.Engine.Scheduler;

public sealed class TaskSchedulerEngine : ITaskSchedulerEngine
{
    public Task<string> ScheduleAsync(ScheduleDefinition definition, CancellationToken ct = default)
        => Task.FromResult(Guid.NewGuid().ToString());
    public Task CancelAsync(string taskId, CancellationToken ct = default) => Task.CompletedTask;
}

public static class SchedulerRegistration
{
    public static IServiceCollection AddTaskScheduler(this IServiceCollection services)
        => services.AddSingleton<ITaskSchedulerEngine, TaskSchedulerEngine>();
}
