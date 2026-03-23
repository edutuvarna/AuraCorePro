using AuraCore.API.Application.Interfaces;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Infrastructure.Repositories;

public sealed class DeviceRepository : IDeviceRepository
{
    private readonly AuraCoreDbContext _db;
    public DeviceRepository(AuraCoreDbContext db) => _db = db;

    public async Task<Device?> GetByFingerprintAsync(Guid licenseId, string fingerprint, CancellationToken ct = default)
        => await _db.Devices.FirstOrDefaultAsync(d => d.LicenseId == licenseId && d.HardwareFingerprint == fingerprint, ct);

    public async Task<Device> RegisterAsync(Device device, CancellationToken ct = default)
    {
        _db.Devices.Add(device);
        await _db.SaveChangesAsync(ct);
        return device;
    }

    public async Task UpdateLastSeenAsync(Guid deviceId, CancellationToken ct = default)
    {
        await _db.Devices.Where(d => d.Id == deviceId)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.LastSeenAt, DateTimeOffset.UtcNow), ct);
    }
}
