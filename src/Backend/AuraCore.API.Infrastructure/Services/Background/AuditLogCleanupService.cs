using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AuraCore.API.Infrastructure.Services.Background;

/// <summary>
/// Phase 6.15.5 — periodic cleanup of audit_log rows older than the configured
/// retention window. Runs once per day; manual triggers go through
/// AuditRetentionController.RunNow which calls the same RunCleanupAsync helper.
/// </summary>
public sealed class AuditLogCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditLogCleanupService> _logger;

    public AuditLogCleanupService(IServiceScopeFactory scopeFactory, ILogger<AuditLogCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(StartupDelay, stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
                await RunCleanupAsync(db, _logger, stoppingToken);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audit log cleanup tick failed");
            }
            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    public static async Task<int> RunCleanupAsync(AuraCoreDbContext db, ILogger logger, CancellationToken ct)
    {
        var setting = await db.SystemSettings.FirstOrDefaultAsync(s => s.Key == "audit_retention.retentionDays", ct);
        var days = setting != null && int.TryParse(setting.Value, out var d) ? d : 365;
        var cutoff = DateTimeOffset.UtcNow.AddDays(-days);

        int deleted;
        if (db.Database.IsRelational())
        {
            deleted = await db.AuditLogs.Where(a => a.CreatedAt < cutoff).ExecuteDeleteAsync(ct);
        }
        else
        {
            var rows = await db.AuditLogs.Where(a => a.CreatedAt < cutoff).ToListAsync(ct);
            db.AuditLogs.RemoveRange(rows);
            deleted = rows.Count;
            await db.SaveChangesAsync(ct);
        }

        // Self-audit row so the cleanup itself is traceable.
        db.AuditLogs.Add(new AuditLogEntry
        {
            ActorEmail = "system",
            Action = "AuditRetentionRun",
            TargetType = "System",
            AfterData = $"{{\"deleted\":{deleted},\"retentionDays\":{days}}}",
            CreatedAt = DateTimeOffset.UtcNow,
        });

        await UpsertSettingAsync(db, "audit_retention.lastRunAt", DateTimeOffset.UtcNow.ToString("O"), ct);
        await UpsertSettingAsync(db, "audit_retention.lastRunDeletedRows", deleted.ToString(), ct);

        await db.SaveChangesAsync(ct);

        if (deleted > 10000)
            logger.LogWarning("Audit retention deleted {Deleted} rows in one run — investigate growth", deleted);

        return deleted;
    }

    private static async Task UpsertSettingAsync(AuraCoreDbContext db, string key, string value, CancellationToken ct)
    {
        var existing = await db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (existing == null)
            db.SystemSettings.Add(new SystemSetting { Key = key, Value = value, UpdatedAt = DateTimeOffset.UtcNow });
        else
        {
            existing.Value = value;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}
