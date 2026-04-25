using System.Text.Json;
using AuraCore.API.Application.Services.Security;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;

namespace AuraCore.API.Infrastructure.Services.Security;

/// <summary>
/// Cloudflare Turnstile CAPTCHA verifier. Posts to
/// challenges.cloudflare.com/turnstile/v0/siteverify and returns the
/// upstream's "success" boolean.
///
/// The HttpClient is configured with a Polly resilience pipeline in
/// Program.cs (T4) — circuit-breaker opens after 5 consecutive failures
/// in a 60s window and stays open for 60s. When the breaker is open,
/// VerifyAsync catches BrokenCircuitException and returns true (fail-open)
/// plus emits a Warning-level log. Rate-limit + BCrypt timing defense
/// continue to protect the auth path during bypass mode.
/// </summary>
public sealed class TurnstileVerifier : ICaptchaVerifier
{
    private const string Endpoint = "turnstile/v0/siteverify";
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;
    private readonly ILogger<TurnstileVerifier> _logger;

    public TurnstileVerifier(HttpClient http, ILogger<TurnstileVerifier> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<bool> VerifyAsync(string token, string remoteIp, CancellationToken ct = default)
    {
        var secret = Environment.GetEnvironmentVariable("TURNSTILE_SECRET_KEY");
        if (string.IsNullOrEmpty(secret))
        {
            _logger.LogError("TURNSTILE_SECRET_KEY env var is missing — failing the verify (closed)");
            return false;
        }

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("secret", secret),
            new KeyValuePair<string, string>("response", token ?? ""),
            new KeyValuePair<string, string>("remoteip", remoteIp ?? ""),
        });

        try
        {
            using var resp = await _http.PostAsync(Endpoint, form, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Turnstile verify returned non-success status {Status}", resp.StatusCode);
                return false;
            }
            var json = await resp.Content.ReadAsStringAsync(ct);
            var parsed = JsonSerializer.Deserialize<TurnstileResponse>(json, JsonOpts);
            return parsed?.Success ?? false;
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning("Turnstile circuit open — bypass mode (fail-open). " +
                               "Rate limit + BCrypt + 2FA still active.");
            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Turnstile verify HTTP failure — closed");
            return false;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Turnstile verify timeout — closed");
            return false;
        }
    }

    private sealed record TurnstileResponse(bool Success);
}
