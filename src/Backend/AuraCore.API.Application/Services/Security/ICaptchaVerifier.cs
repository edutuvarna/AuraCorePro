namespace AuraCore.API.Application.Services.Security;

/// <summary>
/// Verifies a CAPTCHA challenge token, returning true when the token is
/// valid and false otherwise. Implementations may emit a fail-open response
/// (returning true with a warning log) when the upstream provider is
/// unreachable — see TurnstileVerifier for the Polly circuit-breaker policy.
/// </summary>
public interface ICaptchaVerifier
{
    Task<bool> VerifyAsync(string token, string remoteIp, CancellationToken ct = default);
}
