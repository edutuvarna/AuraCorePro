using AuraCore.API.Domain.Entities;

namespace AuraCore.API.Application.Interfaces;

public interface ICrashReportRepository
{
    Task<CrashReport> CreateAsync(CrashReport report, CancellationToken ct = default);
    Task<List<CrashReport>> GetRecentAsync(int count, CancellationToken ct = default);
}
