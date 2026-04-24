namespace AuraCore.API.Application.Services.RateLimiting;

public sealed record RateLimitPolicy(int Requests, int WindowSeconds);

public interface IRateLimitConfigService
{
    Task<IReadOnlyDictionary<string, RateLimitPolicy>> GetAllAsync(CancellationToken ct = default);
    Task UpdateAsync(string endpoint, RateLimitPolicy policy, CancellationToken ct = default);
}
