using AuraCore.API.Domain.Entities;
using AuraCore.API.Helpers;
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuraCore.API.Services;

/// <summary>
/// One-time idempotent grandfather migration (spec D4 "Grandfather migration").
/// For every role='admin' user with zero active (non-revoked) grants,
/// creates Trusted-template grants so existing admins aren't locked out.
/// Runs on every startup — guaranteed idempotent because the "zero grants"
/// check only matches newcomers.
/// </summary>
public class GrandfatherMigrationService
{
    private readonly AuraCoreDbContext _db;
    private readonly ILogger<GrandfatherMigrationService> _logger;

    public GrandfatherMigrationService(AuraCoreDbContext db, ILogger<GrandfatherMigrationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        var adminsWithoutGrants = await _db.Users
            .Where(u => u.Role == "admin")
            .Where(u => !_db.PermissionGrants.Any(g => g.AdminUserId == u.Id && g.RevokedAt == null))
            .ToListAsync(ct);

        if (adminsWithoutGrants.Count == 0)
        {
            _logger.LogInformation("Grandfather migration: no un-granted admins found; skipping");
            return;
        }

        // Attribute new grants to the first superadmin (if any) so audit trails are clean.
        // If none exists yet (Wave 1 before SUPERADMIN_EMAILS runs), self-attribute — we'll
        // re-attribute in a future pass if needed.
        var attribution = await _db.Users
            .Where(u => u.Role == "superadmin")
            .OrderBy(u => u.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (attribution is null)
            _logger.LogWarning("Grandfather migration: no superadmin exists yet; attributing grants self-to-self for {Count} admin(s). Re-run after SUPERADMIN_EMAILS bootstrap to normalize attribution.", adminsWithoutGrants.Count);

        var trustedKeys = PermissionTemplates.GetPermissionsForTemplate(PermissionTemplates.Trusted);

        foreach (var admin in adminsWithoutGrants)
        {
            var attributionId = attribution?.Id ?? admin.Id;
            foreach (var key in trustedKeys)
            {
                _db.PermissionGrants.Add(new PermissionGrant
                {
                    AdminUserId  = admin.Id,
                    PermissionKey = key,
                    GrantedBy    = attributionId,
                    GrantedAt    = DateTimeOffset.UtcNow,
                });
            }
            _logger.LogInformation("Grandfather migration: granted {Count} Trusted keys to {Email}", trustedKeys.Count, admin.Email);
        }

        await _db.SaveChangesAsync(ct);
    }
}
