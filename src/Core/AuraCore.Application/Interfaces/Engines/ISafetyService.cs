namespace AuraCore.Application.Interfaces.Engines;

public interface ISafetyService
{
    Task<string> CreateRestorePointAsync(string description, CancellationToken ct = default);
    Task RollbackAsync(string operationId, CancellationToken ct = default);
}
