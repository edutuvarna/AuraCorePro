using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuraCore.API.HostedServices;

/// <summary>
/// Daily GC for unbounded-growth tables from Phase 6.11. Does NOT touch
/// audit_log — spec explicitly defers audit_log retention to Phase 6.12.
///
/// Deletions:
///  - revoked_tokens older than 2h (access-token TTL is 15 min; 2h buffer)
///  - admin_invitations where ExpiresAt &lt; now()-30d AND ConsumedAt IS NULL
///
/// Consumed invitations are PRESERVED for audit trail — they're small
/// (~200 bytes) and bounded by admin creation rate.
/// </summary>
public sealed class RetentionJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan RevokedTokenRetention = TimeSpan.FromHours(2);
    private static readonly TimeSpan ExpiredInvitationRetention = TimeSpan.FromDays(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RetentionJob> _logger;

    public RetentionJob(IServiceScopeFactory scopeFactory, ILogger<RetentionJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app finish booting before first iteration.
        try { await Task.Delay(StartupDelay, stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
                await SweepAsync(db, _logger, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "RetentionJob iteration failed; will retry in 24h");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>
    /// The actual sweep logic. Exposed as a static so unit tests can call it
    /// with a fresh DbContext and assert outcomes without spinning up the
    /// BackgroundService lifecycle.
    /// </summary>
    public static async Task SweepAsync(AuraCoreDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var tokenCutoff = now - RevokedTokenRetention;
        var inviteCutoff = now - ExpiredInvitationRetention;
        int tokensDeleted, invitesDeleted;

        if (db.Database.IsRelational())
        {
            // Bulk set-based delete on Postgres — avoids loading rows into
            // memory when revoked_tokens grows to millions after a year of
            // uptime (every logout / forced signout adds a row).
            tokensDeleted = await db.RevokedTokens
                .Where(r => r.RevokedAt < tokenCutoff)
                .ExecuteDeleteAsync(ct);
            invitesDeleted = await db.AdminInvitations
                .Where(i => i.ExpiresAt < inviteCutoff && i.ConsumedAt == null)
                .ExecuteDeleteAsync(ct);
        }
        else
        {
            // InMemory provider (used by xUnit tests) does not support
            // ExecuteDelete — fall back to load-then-RemoveRange.
            var oldTokens = await db.RevokedTokens
                .Where(r => r.RevokedAt < tokenCutoff)
                .ToListAsync(ct);
            db.RevokedTokens.RemoveRange(oldTokens);
            tokensDeleted = oldTokens.Count;

            var oldInvites = await db.AdminInvitations
                .Where(i => i.ExpiresAt < inviteCutoff && i.ConsumedAt == null)
                .ToListAsync(ct);
            db.AdminInvitations.RemoveRange(oldInvites);
            invitesDeleted = oldInvites.Count;

            if (tokensDeleted > 0 || invitesDeleted > 0)
            {
                await db.SaveChangesAsync(ct);
            }
        }

        logger.Log(
            LogLevel.Information,
            "RetentionJob sweep complete: revoked_tokens -{Tokens}, expired invitations -{Invites}",
            tokensDeleted, invitesDeleted);
    }
}
