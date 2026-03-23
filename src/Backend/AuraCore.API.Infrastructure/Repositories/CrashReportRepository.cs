using AuraCore.API.Application.Interfaces;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Infrastructure.Repositories;

public sealed class CrashReportRepository : ICrashReportRepository
{
    private readonly AuraCoreDbContext _db;
    public CrashReportRepository(AuraCoreDbContext db) => _db = db;

    public async Task<CrashReport> CreateAsync(CrashReport report, CancellationToken ct = default)
    {
        _db.CrashReports.Add(report);
        await _db.SaveChangesAsync(ct);
        return report;
    }

    public async Task<List<CrashReport>> GetRecentAsync(int count, CancellationToken ct = default)
        => await _db.CrashReports
            .OrderByDescending(c => c.CreatedAt)
            .Take(count)
            .ToListAsync(ct);
}
