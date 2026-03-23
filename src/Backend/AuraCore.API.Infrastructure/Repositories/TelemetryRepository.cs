using AuraCore.API.Application.Interfaces;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;

namespace AuraCore.API.Infrastructure.Repositories;

public sealed class TelemetryRepository : ITelemetryRepository
{
    private readonly AuraCoreDbContext _db;
    public TelemetryRepository(AuraCoreDbContext db) => _db = db;

    public async Task InsertBatchAsync(IEnumerable<TelemetryEvent> events, CancellationToken ct = default)
    {
        _db.TelemetryEvents.AddRange(events);
        await _db.SaveChangesAsync(ct);
    }
}
