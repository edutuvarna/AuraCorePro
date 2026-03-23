using AuraCore.API.Domain.Entities;

namespace AuraCore.API.Application.Interfaces;

public interface ILicenseService
{
    Task<License?> ValidateAsync(string licenseKey, string deviceFingerprint, CancellationToken ct = default);
    Task<License> CreateAsync(Guid userId, string tier, int maxDevices, CancellationToken ct = default);
}
