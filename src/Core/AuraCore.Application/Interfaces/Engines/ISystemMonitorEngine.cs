namespace AuraCore.Application.Interfaces.Engines;

public interface ISystemMonitorEngine
{
    Task<SystemSnapshot> GetCurrentSnapshotAsync(CancellationToken ct = default);
    IObservable<SystemSnapshot> Snapshots { get; }
}

public sealed record SystemSnapshot(
    double CpuPercent,
    double MemoryPercent,
    long DiskFreeBytes,
    DateTimeOffset Timestamp);
