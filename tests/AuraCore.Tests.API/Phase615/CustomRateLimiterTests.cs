using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.API.Application.Services.RateLimiting;
using AuraCore.API.Infrastructure.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.API.Phase615;

public sealed class CustomRateLimiterTests
{
    private static (IAuraCoreRateLimiter limiter, FakePolicies policies) Build()
    {
        var policies = new FakePolicies();
        var sp = new ServiceCollection()
            .AddSingleton<IRateLimitConfigService>(policies)
            .BuildServiceProvider();
        var limiter = new AuraCoreRateLimiter(sp);
        return (limiter, policies);
    }

    [Fact]
    public async Task TryAcquire_AllowsUpToBudget_ThenBlocks()
    {
        var (limiter, policies) = Build();
        policies.Set("auth.login", 5, 1800);

        for (int i = 0; i < 5; i++)
        {
            var r = await limiter.TryAcquireAsync("auth.login", "1.2.3.4", CancellationToken.None);
            Assert.True(r.Allowed, $"req {i} should be allowed");
        }
        var sixth = await limiter.TryAcquireAsync("auth.login", "1.2.3.4", CancellationToken.None);
        Assert.False(sixth.Allowed);
        Assert.True(sixth.RetryAfter > TimeSpan.Zero);
    }

    [Fact]
    public async Task TryAcquire_DifferentBucketKeys_AreIndependent()
    {
        var (limiter, policies) = Build();
        policies.Set("auth.login", 2, 1800);

        Assert.True((await limiter.TryAcquireAsync("auth.login", "1.1.1.1", default)).Allowed);
        Assert.True((await limiter.TryAcquireAsync("auth.login", "1.1.1.1", default)).Allowed);
        Assert.False((await limiter.TryAcquireAsync("auth.login", "1.1.1.1", default)).Allowed);
        // Different IP — own bucket — fresh budget
        Assert.True((await limiter.TryAcquireAsync("auth.login", "2.2.2.2", default)).Allowed);
    }

    [Fact]
    public async Task TryAcquire_PolicyChange_TakesEffectImmediately()
    {
        var (limiter, policies) = Build();
        policies.Set("auth.login", 1, 1800);
        Assert.True((await limiter.TryAcquireAsync("auth.login", "ip1", default)).Allowed);
        Assert.False((await limiter.TryAcquireAsync("auth.login", "ip1", default)).Allowed);

        // Operator increases budget via UI -> service invalidates cache
        policies.Set("auth.login", 10, 1800);

        // Next call must see the new policy. Bucket budget grew from 1 to 10
        // so at least one more allowance immediately.
        Assert.True((await limiter.TryAcquireAsync("auth.login", "ip1", default)).Allowed);
    }

    [Fact]
    public async Task TryAcquire_UnknownPolicy_AllowsAndDoesNotThrow()
    {
        var (limiter, _) = Build();
        var r = await limiter.TryAcquireAsync("unknown.policy", "ip", default);
        Assert.True(r.Allowed);
    }

    [Fact]
    public async Task TryAcquire_ConcurrentSameBucket_TotalDoesNotExceedBudget()
    {
        var (limiter, policies) = Build();
        policies.Set("admin.all", 100, 60);

        var allowed = 0;
        await Parallel.ForAsync(0, 200, async (_, ct) =>
        {
            var r = await limiter.TryAcquireAsync("admin.all", "ip-x", ct);
            if (r.Allowed) Interlocked.Increment(ref allowed);
        });
        Assert.True(allowed <= 100, $"expected <= 100 allowed, got {allowed}");
        Assert.True(allowed >= 95, $"expected most allowed near budget, got {allowed}");
    }

    private sealed class FakePolicies : IRateLimitConfigService
    {
        private readonly Dictionary<string, RateLimitPolicy> _policies = new();
        public void Set(string name, int requests, int windowSeconds) =>
            _policies[name] = new RateLimitPolicy(requests, windowSeconds);
        public Task<IReadOnlyDictionary<string, RateLimitPolicy>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, RateLimitPolicy>>(_policies);
        public Task UpdateAsync(string endpoint, RateLimitPolicy policy, CancellationToken ct = default)
        { Set(endpoint, policy.Requests, policy.WindowSeconds); return Task.CompletedTask; }
    }
}
