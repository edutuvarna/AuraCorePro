// tests/AuraCore.Tests.API/SuperadminFoundation/RateLimitConfigServiceTests.cs
using AuraCore.API.Application.Services.RateLimiting;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using AuraCore.API.Infrastructure.Services.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class RateLimitConfigServiceTests
{
    private static (AuraCoreDbContext db, RateLimitConfigService svc) Build()
    {
        var opt = new DbContextOptionsBuilder<AuraCoreDbContext>().UseInMemoryDatabase($"rl-{Guid.NewGuid()}").Options;
        var db = new AuraCoreDbContext(opt);
        var cache = new MemoryCache(new MemoryCacheOptions());
        return (db, new RateLimitConfigService(db, cache));
    }

    [Fact]
    public async Task GetAll_returns_seeded_default_policies()
    {
        var (db, svc) = Build();
        db.SystemSettings.Add(new SystemSetting { Key = "rate_limit_policies",
            Value = "{\"auth.login\":{\"requests\":5,\"windowSeconds\":1800}}" });
        await db.SaveChangesAsync();
        var all = await svc.GetAllAsync();
        Assert.Equal(5, all["auth.login"].Requests);
    }

    [Fact]
    public async Task Update_mutates_json_and_invalidates_cache()
    {
        var (db, svc) = Build();
        db.SystemSettings.Add(new SystemSetting { Key = "rate_limit_policies",
            Value = "{\"auth.login\":{\"requests\":5,\"windowSeconds\":1800}}" });
        await db.SaveChangesAsync();
        _ = await svc.GetAllAsync();
        await svc.UpdateAsync("auth.login", new RateLimitPolicy(10, 900));
        var all = await svc.GetAllAsync();
        Assert.Equal(10, all["auth.login"].Requests);
        Assert.Equal(900, all["auth.login"].WindowSeconds);
    }
}
