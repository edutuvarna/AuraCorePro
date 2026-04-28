using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.API.Infrastructure.RateLimiting;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AuraCore.Tests.API.Phase615;

public sealed class RateLimiterMiddlewareTests
{
    [Fact]
    public async Task NoEndpoint_PassesThrough()
    {
        var ctx = new DefaultHttpContext();
        var fakeLimiter = new FakeLimiter(allow: false);
        var mw = new RateLimiterMiddleware((_) => Task.CompletedTask);

        await mw.InvokeAsync(ctx, fakeLimiter);

        Assert.Equal(0, fakeLimiter.Calls);
        Assert.Equal(200, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task EndpointWithoutAttribute_PassesThrough()
    {
        var ctx = new DefaultHttpContext();
        ctx.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "test"));
        var fakeLimiter = new FakeLimiter(allow: false);
        var mw = new RateLimiterMiddleware((_) => Task.CompletedTask);

        await mw.InvokeAsync(ctx, fakeLimiter);

        Assert.Equal(0, fakeLimiter.Calls);
    }

    [Fact]
    public async Task EndpointWithAttribute_Allowed_CallsNext()
    {
        var nextCalled = false;
        var ctx = new DefaultHttpContext();
        ctx.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new RateLimitedAttribute("auth.login")),
            "test"));
        var fakeLimiter = new FakeLimiter(allow: true);
        var mw = new RateLimiterMiddleware((_) => { nextCalled = true; return Task.CompletedTask; });

        await mw.InvokeAsync(ctx, fakeLimiter);

        Assert.Equal(1, fakeLimiter.Calls);
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task EndpointWithAttribute_Blocked_Returns429WithRetryAfter()
    {
        var nextCalled = false;
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new System.IO.MemoryStream();
        ctx.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new RateLimitedAttribute("auth.login")),
            "test"));
        var fakeLimiter = new FakeLimiter(allow: false, retryAfter: System.TimeSpan.FromSeconds(42));
        var mw = new RateLimiterMiddleware((_) => { nextCalled = true; return Task.CompletedTask; });

        await mw.InvokeAsync(ctx, fakeLimiter);

        Assert.False(nextCalled);
        Assert.Equal((int)HttpStatusCode.TooManyRequests, ctx.Response.StatusCode);
        Assert.Equal("42", ctx.Response.Headers["Retry-After"].ToString());
    }

    private sealed class FakeLimiter : IAuraCoreRateLimiter
    {
        private readonly bool _allow;
        private readonly System.TimeSpan _retry;
        public int Calls;
        public FakeLimiter(bool allow, System.TimeSpan? retryAfter = null)
        {
            _allow = allow;
            _retry = retryAfter ?? System.TimeSpan.Zero;
        }
        public Task<RateLimitResult> TryAcquireAsync(string policyName, string bucketKey, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new RateLimitResult(_allow, _retry));
        }
    }
}
