using AuraCore.API.Application.Interfaces;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Infrastructure.Repositories;

public sealed class LicenseService : ILicenseService
{
    private readonly AuraCoreDbContext _db;
    public LicenseService(AuraCoreDbContext db) => _db = db;

    public async Task<License?> ValidateAsync(string licenseKey, string deviceFingerprint, CancellationToken ct = default)
    {
        // Use a transaction to prevent race conditions on device count check + insert
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var license = await _db.Licenses
                .Include(l => l.Devices)
                .FirstOrDefaultAsync(l => l.Key == licenseKey && l.Status == "active", ct);

            if (license is null) return null;
            if (license.ExpiresAt.HasValue && license.ExpiresAt.Value < DateTimeOffset.UtcNow) return null;

            var device = license.Devices.FirstOrDefault(d => d.HardwareFingerprint == deviceFingerprint);
            if (device is not null)
            {
                device.LastSeenAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return license;
            }

            // Re-check device count inside transaction to prevent race condition
            if (license.Devices.Count >= license.MaxDevices)
            {
                await transaction.CommitAsync(ct);
                return null;
            }

            await transaction.CommitAsync(ct);
            return license;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<License> CreateAsync(Guid userId, string tier, int maxDevices, CancellationToken ct = default)
    {
        var license = new License
        {
            UserId = userId,
            Key = Guid.NewGuid().ToString("N"),
            Tier = tier,
            MaxDevices = maxDevices
        };
        _db.Licenses.Add(license);
        await _db.SaveChangesAsync(ct);
        return license;
    }
}
