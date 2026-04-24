using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuraCore.API.Services;

/// <summary>
/// Reads SUPERADMIN_EMAILS env var (comma-separated) and promotes matching
/// registered users to role='superadmin' on startup. Idempotent. Never creates
/// accounts — the user must have registered first. Spec D3.
/// </summary>
public class SuperadminBootstrapService
{
    private readonly AuraCoreDbContext _db;
    private readonly ILogger<SuperadminBootstrapService> _logger;

    public SuperadminBootstrapService(AuraCoreDbContext db, ILogger<SuperadminBootstrapService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        var raw = Environment.GetEnvironmentVariable("SUPERADMIN_EMAILS");
        if (string.IsNullOrWhiteSpace(raw))
        {
            _logger.LogInformation("SUPERADMIN_EMAILS env var unset; superadmin bootstrap skipped");
            return;
        }

        var emails = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.ToLowerInvariant())
            .Distinct()
            .ToArray();

        var existing = await _db.Users
            .Where(u => emails.Contains(u.Email))
            .ToListAsync(ct);

        var promoted = 0;
        foreach (var user in existing)
        {
            if (user.Role != "superadmin")
            {
                user.Role = "superadmin";
                _logger.LogWarning("Promoted user {Email} to superadmin via SUPERADMIN_EMAILS bootstrap", user.Email);
                promoted++;
            }
        }

        if (promoted > 0)
            await _db.SaveChangesAsync(ct);

        var missing = emails.Except(existing.Select(u => u.Email)).ToArray();
        if (missing.Length > 0)
        {
            _logger.LogWarning(
                "SUPERADMIN_EMAILS contains emails not registered in users table: {Emails}. They must register first at /api/auth/register; the next backend startup will promote them.",
                string.Join(", ", missing));
        }
    }
}
