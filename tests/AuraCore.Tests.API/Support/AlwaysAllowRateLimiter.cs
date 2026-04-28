using System;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.API.Infrastructure.RateLimiting;

namespace AuraCore.Tests.API.Support;

/// <summary>
/// Phase 6.15.3 test double — bypasses the token-bucket so multi-call tests
/// (timing-attack assertions, sweep tests) aren't gated by the production
/// auth.login budget of 5 / 30 min. Real limiter behavior is covered by
/// CustomRateLimiterTests + RateLimiterMiddlewareTests.
/// </summary>
public sealed class AlwaysAllowRateLimiter : IAuraCoreRateLimiter
{
    public Task<RateLimitResult> TryAcquireAsync(string policyName, string bucketKey, CancellationToken ct)
        => Task.FromResult(new RateLimitResult(true, TimeSpan.Zero));
}
