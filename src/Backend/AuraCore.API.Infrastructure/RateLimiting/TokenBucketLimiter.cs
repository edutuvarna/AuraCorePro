using System;

namespace AuraCore.API.Infrastructure.RateLimiting;

/// <summary>
/// Single token bucket. Atomically refills based on elapsed monotonic time and
/// the current policy parameters. Per-bucket lock; lock-free reads not used
/// here because the consume operation must be all-or-nothing under contention.
/// </summary>
internal sealed class TokenBucketState
{
    private readonly object _gate = new();
    private double _tokens;
    private long _lastRefillTicks;
    private int _lastBudget;

    public TokenBucketState(int initialBudget, long nowTicks)
    {
        _tokens = initialBudget;
        _lastBudget = initialBudget;
        _lastRefillTicks = nowTicks;
    }

    public RateLimitResult TryConsume(int budget, int windowSeconds, long nowTicks)
    {
        if (budget <= 0 || windowSeconds <= 0) return new RateLimitResult(true, TimeSpan.Zero);

        lock (_gate)
        {
            // Reflect a policy budget change: shift the token count by the delta
            // so an operator increasing the budget hands out new allowances
            // immediately, and a decrease clamps without going negative.
            // Cache invalidates on UpdateAsync, so this branch runs on the next
            // call after an operator save.
            if (budget != _lastBudget)
            {
                var delta = budget - _lastBudget;
                _tokens = Math.Clamp(_tokens + delta, 0, budget);
                _lastBudget = budget;
            }

            // Refill based on elapsed time. Tokens accumulate at budget/windowSeconds per second.
            var elapsedSeconds = (double)(nowTicks - _lastRefillTicks) / TimeSpan.TicksPerSecond;
            if (elapsedSeconds > 0)
            {
                var refillRatePerSecond = (double)budget / windowSeconds;
                _tokens = Math.Min(budget, _tokens + elapsedSeconds * refillRatePerSecond);
                _lastRefillTicks = nowTicks;
            }
            else if (_tokens > budget)
            {
                // Defensive: same instant after a budget shrink — clamp.
                _tokens = budget;
            }

            if (_tokens >= 1)
            {
                _tokens -= 1;
                return new RateLimitResult(true, TimeSpan.Zero);
            }

            // Compute retry-after: how long until 1 token regenerates.
            var deficit = 1 - _tokens;
            var refillRate = (double)budget / windowSeconds;
            var retrySeconds = deficit / refillRate;
            return new RateLimitResult(false, TimeSpan.FromSeconds(retrySeconds));
        }
    }
}
