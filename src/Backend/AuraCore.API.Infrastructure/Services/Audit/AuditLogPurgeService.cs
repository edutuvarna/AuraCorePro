using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AuraCore.API.Infrastructure.Services.Audit;

/// <summary>
/// Purges login_attempts older than 90 days once per day. Runs forever
/// while the app is up. Logs deletion counts. Does NOT purge audit_log
/// (admin mutations kept forever per Phase 6.8 spec D1).
/// </summary>
public sealed class AuditLogPurgeService : BackgroundService
{
    private const int RetentionDays = 90;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditLogPurgeService> _logger;

    public AuditLogPurgeService(IServiceScopeFactory scopeFactory, ILogger<AuditLogPurgeService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial delay — let app boot settle before first sweep
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
                var cutoff = DateTimeOffset.UtcNow.AddDays(-RetentionDays);
                var deleted = await db.LoginAttempts
                    .Where(la => la.CreatedAt < cutoff)
                    .ExecuteDeleteAsync(stoppingToken)
                    .ConfigureAwait(false);

                if (deleted > 0)
                    _logger.LogInformation("AuditLogPurge: removed {Count} login_attempts older than {Cutoff}", deleted, cutoff);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AuditLogPurge sweep failed");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { return; }
        }
    }
}
