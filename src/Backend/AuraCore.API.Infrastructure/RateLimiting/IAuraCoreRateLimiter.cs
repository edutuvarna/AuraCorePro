using System;
using System.Threading;
using System.Threading.Tasks;

namespace AuraCore.API.Infrastructure.RateLimiting;

/// <summary>
/// Phase 6.15.3 — token-bucket rate limiter that pulls policy parameters from
/// the cached IRateLimitConfigService. Operator UI edits invalidate the cache;
/// the next TryAcquireAsync call sees the new policy immediately.
/// </summary>
public interface IAuraCoreRateLimiter
{
    Task<RateLimitResult> TryAcquireAsync(string policyName, string bucketKey, CancellationToken ct);
}

public readonly record struct RateLimitResult(bool Allowed, TimeSpan RetryAfter);
