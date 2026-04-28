using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.API.Application.Services.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.API.Infrastructure.RateLimiting;

public sealed class AuraCoreRateLimiter : IAuraCoreRateLimiter
{
    private readonly IServiceProvider _root;
    private readonly ConcurrentDictionary<(string policy, string key), TokenBucketState> _buckets = new();

    public AuraCoreRateLimiter(IServiceProvider root)
    {
        _root = root;
    }

    public async Task<RateLimitResult> TryAcquireAsync(string policyName, string bucketKey, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(policyName)) return new RateLimitResult(true, TimeSpan.Zero);

        // Resolve current policy from cached service. UI edits invalidate the
        // cache → next call here sees the new policy. 5-min TTL is the
        // defensive safety net only.
        using var scope = _root.CreateScope();
        var cfg = scope.ServiceProvider.GetRequiredService<IRateLimitConfigService>();
        var all = await cfg.GetAllAsync(ct);
        if (!all.TryGetValue(policyName, out var policy))
        {
            // Unknown policy → fail-open. Operator declares policies via UI;
            // we don't 429 because of a typo in [RateLimited(...)] attribute.
            return new RateLimitResult(true, TimeSpan.Zero);
        }

        var nowTicks = Environment.TickCount64 * TimeSpan.TicksPerMillisecond;
        var bucket = _buckets.GetOrAdd(
            (policyName, bucketKey),
            _ => new TokenBucketState(policy.Requests, nowTicks));
        return bucket.TryConsume(policy.Requests, policy.WindowSeconds, nowTicks);
    }
}
