using Microsoft.Extensions.DependencyInjection;
using System.Reactive.Subjects;
using AuraCore.Application.Interfaces.Engines;

namespace AuraCore.Engine.SystemMonitor;

public sealed class SystemMonitorEngine : ISystemMonitorEngine, IDisposable
{
    private readonly Subject<SystemSnapshot> _snapshots = new();
    public IObservable<SystemSnapshot> Snapshots => _snapshots;

    public Task<SystemSnapshot> GetCurrentSnapshotAsync(CancellationToken ct = default)
        => Task.FromResult(new SystemSnapshot(0, 0, 0, DateTimeOffset.UtcNow));

    public void Dispose() => _snapshots.Dispose();
}

public static class SystemMonitorRegistration
{
    public static IServiceCollection AddSystemMonitor(this IServiceCollection services)
    {
        services.AddSingleton<SystemMonitorEngine>();
        services.AddSingleton<ISystemMonitorEngine>(sp => sp.GetRequiredService<SystemMonitorEngine>());
        return services;
    }
}
