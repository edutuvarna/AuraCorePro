using System.Text.Json;
using AuraCore.API.Application.Services.RateLimiting;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AuraCore.API.Infrastructure.Services.RateLimiting;

public sealed class RateLimitConfigService : IRateLimitConfigService
{
    private const string CacheKey = "rate_limit_policies";
    private const string SettingKey = "rate_limit_policies";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly AuraCoreDbContext _db;
    private readonly IMemoryCache _cache;

    public RateLimitConfigService(AuraCoreDbContext db, IMemoryCache cache)
    {
        _db = db; _cache = cache;
    }

    // Defensive deserialize: a corrupted system_settings row (hand-edited SQL,
    // failed prior migration, etc.) must not brick GET/PUT. Return empty map
    // on malformed JSON so the next UpdateAsync can overwrite cleanly.
    private static Dictionary<string, RateLimitPolicy> SafeDeserialize(string raw)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, RateLimitPolicy>>(raw, JsonOpts)
                   ?? new Dictionary<string, RateLimitPolicy>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, RateLimitPolicy>();
        }
    }

    public async Task<IReadOnlyDictionary<string, RateLimitPolicy>> GetAllAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue<Dictionary<string, RateLimitPolicy>>(CacheKey, out var cached) && cached is not null)
            return cached;
        var row = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == SettingKey, ct);
        var raw = row?.Value ?? "{}";
        var map = SafeDeserialize(raw);
        _cache.Set(CacheKey, map, CacheTtl);
        return map;
    }

    public async Task UpdateAsync(string endpoint, RateLimitPolicy policy, CancellationToken ct = default)
    {
        var row = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == SettingKey, ct);
        if (row is null)
        {
            row = new SystemSetting { Key = SettingKey, Value = "{}" };
            _db.SystemSettings.Add(row);
        }
        var map = SafeDeserialize(row.Value);
        map[endpoint] = policy;
        row.Value = JsonSerializer.Serialize(map);
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        _cache.Remove(CacheKey);
    }
}
