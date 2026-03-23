using AuraCore.API.Domain.Entities;

namespace AuraCore.API.Application.Interfaces;

public interface IDeviceRepository
{
    Task<Device?> GetByFingerprintAsync(Guid licenseId, string fingerprint, CancellationToken ct = default);
    Task<Device> RegisterAsync(Device device, CancellationToken ct = default);
    Task UpdateLastSeenAsync(Guid deviceId, CancellationToken ct = default);
}
