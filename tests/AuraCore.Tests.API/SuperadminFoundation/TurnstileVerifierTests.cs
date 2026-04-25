using System.Net;
using System.Net.Http;
using AuraCore.API.Infrastructure.Services.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

/// <summary>
/// Verifier-only tests — Polly resilience pipeline tested separately in
/// CaptchaCircuitBreakerTests via DI integration.
/// </summary>
public class TurnstileVerifierTests
{
    private static HttpClient ClientThatReturns(HttpStatusCode status, string body)
        => new(new StubHandler(status, body)) { BaseAddress = new Uri("https://challenges.cloudflare.com/") };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        public HttpRequestMessage? LastRequest;
        public string? LastBody;
        public StubHandler(HttpStatusCode status, string body) { _status = status; _body = body; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            LastRequest = req;
            if (req.Content is not null) LastBody = await req.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(_status) { Content = new StringContent(_body) };
        }
    }

    [Fact]
    public async Task Verify_returns_true_when_CF_responds_success_true()
    {
        var http = ClientThatReturns(HttpStatusCode.OK, "{\"success\":true}");
        Environment.SetEnvironmentVariable("TURNSTILE_SECRET_KEY", "test-secret");
        var v = new TurnstileVerifier(http, NullLogger<TurnstileVerifier>.Instance);
        var ok = await v.VerifyAsync("user-token", "1.2.3.4");
        Assert.True(ok);
    }

    [Fact]
    public async Task Verify_returns_false_when_CF_responds_success_false()
    {
        var http = ClientThatReturns(HttpStatusCode.OK, "{\"success\":false,\"error-codes\":[\"invalid-input-response\"]}");
        Environment.SetEnvironmentVariable("TURNSTILE_SECRET_KEY", "test-secret");
        var v = new TurnstileVerifier(http, NullLogger<TurnstileVerifier>.Instance);
        var ok = await v.VerifyAsync("bad-token", "1.2.3.4");
        Assert.False(ok);
    }

    [Fact]
    public async Task Verify_sends_form_data_with_secret_response_remoteip()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{\"success\":true}");
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://challenges.cloudflare.com/") };
        Environment.SetEnvironmentVariable("TURNSTILE_SECRET_KEY", "the-secret");
        var v = new TurnstileVerifier(http, NullLogger<TurnstileVerifier>.Instance);
        await v.VerifyAsync("user-token", "1.2.3.4");
        Assert.NotNull(handler.LastBody);
        Assert.Contains("secret=the-secret", handler.LastBody);
        Assert.Contains("response=user-token", handler.LastBody);
        Assert.Contains("remoteip=1.2.3.4", handler.LastBody);
    }

    [Fact]
    public async Task Verify_returns_false_on_HTTP_error_status()
    {
        var http = ClientThatReturns(HttpStatusCode.InternalServerError, "");
        Environment.SetEnvironmentVariable("TURNSTILE_SECRET_KEY", "test-secret");
        var v = new TurnstileVerifier(http, NullLogger<TurnstileVerifier>.Instance);
        var ok = await v.VerifyAsync("user-token", "1.2.3.4");
        Assert.False(ok);
    }
}
