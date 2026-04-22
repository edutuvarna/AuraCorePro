using AuraCore.API.Application.Services.Security;
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Infrastructure.Services.Security;

public sealed class WhitelistService : IWhitelistService
{
    private readonly AuraCoreDbContext _db;
    public WhitelistService(AuraCoreDbContext db) => _db = db;

    public Task<bool> IsWhitelistedAsync(string ipAddress, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(ipAddress)) return Task.FromResult(false);
        return _db.IpWhitelists.AnyAsync(w => w.IpAddress == ipAddress, ct);
    }
}
