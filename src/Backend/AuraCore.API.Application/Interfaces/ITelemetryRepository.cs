using AuraCore.API.Domain.Entities;

namespace AuraCore.API.Application.Interfaces;

public interface ITelemetryRepository
{
    Task InsertBatchAsync(IEnumerable<TelemetryEvent> events, CancellationToken ct = default);
}
